// [Michael Reizenstein]
// CSCI 251 - Secure Distributed Messenger

using System.Net;
using System.Net.Sockets;
using SecureMessenger.Core;
using System.Text.Json; 
using System;
using System.Text;

namespace SecureMessenger.Network;

/// <summary>
/// Handles outgoing TCP connections to other peers.
/// </summary>
public class TcpClientHandler
{
    private readonly Dictionary<string, Peer> _connections = new();
    private readonly object _lock = new();

    public event Action<Peer>? OnConnected;
    public event Action<Peer>? OnDisconnected;
    public event Action<Peer, Message>? OnMessageReceived;

    /// <summary>
    /// Connect to a peer at the specified address and port.
    ///
    /// 1. Create a new TcpClient
    /// 2. Connect asynchronously to the host and port
    /// 3. Create a Peer object with:
    ///    - Client = the TcpClient
    ///    - Stream = client.GetStream()
    ///    - Address = parsed from host string
    ///    - Port = the port parameter
    ///    - IsConnected = true
    /// 4. Add to _connections dictionary (with proper locking)
    /// 5. Invoke OnConnected event
    /// 6. Start a background task running ReceiveLoop for this peer
    /// 7. Return true on success
    /// 8. Handle SocketException - print error and return false
    /// </summary>
    public async Task<bool> ConnectAsync(string host, int port)
    {
        try
        {
            /// Creates a new TCP client and connects to the host and port given
            TcpClient client = new TcpClient();
            await client.ConnectAsync(host, port);
            /// Once connected, get the network stream for communication
            NetworkStream stream = client.GetStream();
            /// Creates new peer object
            Peer peer = new Peer
            {
                Client = client,
                Stream = stream,
                Address = IPAddress.Parse(host),
                Port = port,
                IsConnected = true
            };
            /// Locking threads to only allow one thread to access the connections dictionary at a time
            lock (_lock)
            {
                _connections[peer.Id] = peer;
            }
            /// Connection event (prevents a null connection)
            OnConnected?.Invoke(peer);
            /// Starts the background task to receive messages from the peer
            _ = Task.Run(() => ReceiveLoop(peer));
            return true;
        }
        /// Handles network errors
        catch (SocketException ex)
        {
            System.Console.WriteLine($"Failed to connect to {host}:{port}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Receive loop for a connected peer - reads messages until disconnection.
    ///
    /// 1. Create a StreamReader from the peer's stream
    /// 2. Loop while peer is connected
    /// 3. Read a line asynchronously (ReadLineAsync)
    /// 4. If line is null, connection was closed - break
    /// 5. Create a Message object with the received content
    /// 6. Invoke OnMessageReceived event
    /// 7. Handle IOException (connection lost)
    /// 8. In finally block, call Disconnect
    /// </summary>
    private async Task ReceiveLoop(Peer peer)
    {
        try
        {
            var reader = peer.Stream;
            /// Loops while the peer is still connected
            
            byte[] messageLengthBuffer = new byte[4];
            byte[] byteMessage; 

            while (peer.IsConnected)
            {
                int totalBytes = 0; 
                /// Reads a line of text asynchronously from the stream
                while(totalBytes < 4)
                {
                    int bytesRead = await reader.ReadAsync(messageLengthBuffer, totalBytes, 4 - totalBytes);
                    if(bytesRead == 0)
                    {
                        peer.IsConnected = false; 
                        break; 
                    }
                    totalBytes += bytesRead; 
                }

                if(!peer.IsConnected)
                    break; 

                totalBytes = 0; 
                int length = BitConverter.ToInt32(messageLengthBuffer, 0); 

                byteMessage = new byte[length]; 
                
                while(totalBytes < length)
                {
                    int bytesRead = await reader.ReadAsync(byteMessage, totalBytes, length - totalBytes); 
                    if(bytesRead == 0)
                    {
                        peer.IsConnected = false; 
                        break; 
                    }
                    totalBytes += bytesRead; 
                }

                if(!peer.IsConnected)
                    break; 

                string line = Encoding.UTF8.GetString(byteMessage, 0, length);

                if (line == null)
                {
                    break;
                }
                /// Creates a new message object with the received content
                Message message = JsonSerializer.Deserialize<Message>(line);
                /// Invokes the message received event
                OnMessageReceived?.Invoke(peer, message);
            }
        }
        catch (IOException ex)
        {
            System.Console.WriteLine($"Error reading from peer {peer}: {ex.Message}");
        }
        finally
        {
            Disconnect(peer.Id);
        }
    }

    /// <summary>
    /// Send a message to a specific peer.
    ///
    /// 1. Look up the peer in _connections by peerId (with proper locking)
    /// 2. If peer exists and is connected with a valid stream:
    ///    - Create a StreamWriter (with leaveOpen: true)
    ///    - Write the message line asynchronously
    ///    - Flush the writer
    /// </summary>
    public async Task SendAsync(string peerId, Message message)
    {
        /// Looks up the peer in the connections dictionary
        Peer? peer;
        /// Locks threads to only allow one thread to access the connections dictionary at a time
        lock (_lock)
        {
            _connections.TryGetValue(peerId, out peer);
        }
        /// Sends the message if the peer is connected and has a valid stream
        if (peer != null && peer.IsConnected && peer.Stream != null)
        {
            try
            {
                byte[] byteMessage = message.ToByteArray(); 
                /// Creates a stream writer to send the message to the peer
                var writer = peer.Stream;
                /// Writes the message line asynchronously
                await writer.WriteAsync(byteMessage, 0, byteMessage.Length);
                /// Flushes the writer to ensure the message is sent
                await writer.FlushAsync();
            }
            catch (IOException ex)
            {
                System.Console.WriteLine($"Error sending to peer {peerId}: {ex.Message}");
                Disconnect(peerId);
            }
        }
    }

    /// <summary>
    /// Broadcast a message to all connected peers.
    ///
    /// 1. Get a copy of all peers (with proper locking)
    /// 2. Loop through each peer and call SendAsync
    /// </summary>
    public async Task BroadcastAsync(Message message)
    {
        List<Peer> peers;
        lock (_lock)
        {
            peers = _connections.Values.ToList();
        }
        foreach (var peer in peers)
        {
            await SendAsync(peer.Id, message);
        }
    }

    /// <summary>
    /// Disconnect from a peer.
    ///
    /// 1. Remove the peer from _connections (with proper locking)
    /// 2. If peer was found:
    ///    - Set IsConnected to false
    ///    - Dispose the Client and Stream
    ///    - Invoke OnDisconnected event
    /// </summary>
    public void Disconnect(string peerId)
    {
        Peer? peer;
        lock (_lock)
        {
            if (_connections.TryGetValue(peerId, out peer))
            {
                _connections.Remove(peerId);
            }
        }
        if (peer != null)
        {
            peer.IsConnected = false;
            peer.Client?.Dispose();
            peer.Stream?.Dispose();
            OnDisconnected?.Invoke(peer);
        }
    }
    /// <summary>
    /// Disconnect from all peers.
    ///
    /// 1. loops through all peers; at each peer
    ///     - disconnect(peer) is called
    ///
    /// </summary>
    public void DisconnectAll()
    {
        lock (_lock)
        {
            foreach (var peer in _connections.Keys) {
                Disconnect(peer);
            }
        }
    }

    /// <summary>
    /// Get all currently connected peers.
    /// Remember to use proper locking when accessing _connections.
    /// </summary>
    public IEnumerable<Peer> GetConnectedPeers()
    {
        lock (_lock)
        {
            return _connections.Values.ToList();
        }
    }
}
