// Willow Rimlinger
// Sean Gaines
// CSCI 251 - Secure Distributed Messenger

using System.Net;
using System.Net.Sockets;
using SecureMessenger.Core;
using System.Text.Json; 
using System.Text;

namespace SecureMessenger.Network;

/// <summary>
/// TCP server that listens for incoming peer connections.
/// Each peer runs both a server (to accept connections) and client (to initiate connections).
/// </summary>
public class TcpServer
{
    private TcpListener? _listener;
    private object _connectedPeersLock = new object();
    private CancellationTokenSource? _cancellationTokenSource;
    private Thread? _listenThread;
    private List<Thread> _receiveThreads = new();
    private object _receiveThreadsLock = new object();
    private PeerDiscovery _peerDiscovery; 

    public event Action<Peer>? OnPeerConnected;
    public event Action<Peer>? OnPeerDisconnected;
    public event Action<Peer, Message>? OnMessageReceived;

    public int Port { get; private set; }
    public bool IsListening { get; private set; }

    public TcpServer(PeerDiscovery peerDiscovery)
    {
        _peerDiscovery = peerDiscovery; 
    }

    /// <summary>
    /// Start listening for incoming connections on the specified port.
    ///
    /// 1. Store the port number
    /// 2. Create a new CancellationTokenSource
    /// 3. Create and start a TcpListener on IPAddress.Any and the specified port
    /// 4. Set IsListening to true
    /// 5. Create and start a new Thread running ListenLoop
    /// 6. Print a message indicating the server is listening
    /// </summary>
    public void Start(int port)
    {
        try 
        {
            this.Port = port;
            this._cancellationTokenSource = new CancellationTokenSource();
            this._listener = new TcpListener(IPAddress.Any, port);
            this._listener.Start();
            this.IsListening = true;
            this._listenThread = new Thread(ListenLoop);
            this._listenThread.Start();
            Console.WriteLine("Server is listening...");
        }
        catch (SocketException e)
        {
            Console.WriteLine($"SocketException attempting to listen on port {port}: {e}"); 
            this.Stop(); 
        }
        catch (IOException e)
        {
            Console.WriteLine($"IOException attempting to listen on port {port}: {e}"); 
            this.Stop(); 
        }
    }

    /// <summary>
    /// Main loop that accepts incoming connections.
    ///
    /// 1. Loop while cancellation is not requested
    /// 2. Check if a connection is pending using _listener.Pending()
    /// 3. If pending, accept the connection with AcceptTcpClient()
    /// 4. Call HandleNewConnection with the new client
    /// 5. If not pending, sleep briefly (e.g., 100ms) to avoid busy-waiting
    /// 6. Handle SocketException and IOException appropriately
    /// </summary>
    private void ListenLoop()
    {
        if (_cancellationTokenSource is null || _listener is null) {
            throw new NullReferenceException("Must call Start() before invoking ListenLoop");
        }
        while (!_cancellationTokenSource.Token.IsCancellationRequested) {
            try {
                if (this._listener.Pending()) {
                    TcpClient client = _listener.AcceptTcpClient();
                    _ = HandleNewConnection(client);
                } else {
                    Thread.Sleep(100);
                }
            } catch (SocketException e) {
                Console.WriteLine("SocketException: {0}", e);
            } catch (IOException e) {
                Console.WriteLine("IOException: {0}", e);
            }
        }
    }

    /// <summary>
    /// Handle a new incoming connection by creating a Peer and starting its receive thread.
    ///
    /// 1. Create a new Peer object with:
    ///    - Client = the TcpClient
    ///    - Stream = client.GetStream()
    ///    - Address = extracted from client.Client.RemoteEndPoint
    ///    - Port = extracted from client.Client.RemoteEndPoint
    ///    - IsConnected = true
    /// 2. Add the peer to _connectedPeers (with proper locking)
    /// 3. Invoke OnPeerConnected event
    /// 4. Create and start a new Thread running ReceiveLoop for this peer
    /// </summary>
    private async Task HandleNewConnection(TcpClient client)
    {
        if (client.Client.RemoteEndPoint is null) {
            throw new NullReferenceException("Client must have a remote endpoint assigned");
        }
        if (OnPeerConnected is null) {
            throw new NullReferenceException("OnPeerConnected is null");
        }

        Message keyExchange = new Message
        {
            Sender = _peerDiscovery.LocalPeerId, 
            Type = MessageType.KeyExchange, 
            PublicKey = Program._myPublicKey
        }; 

        Peer peer; 
        try 
        {
            await SendAsync(client, keyExchange);
            Peer fakePeer = new Peer
            {
                Client = client,
                Stream = client.GetStream(),
                IsConnected = true
            }; 
            Message? message = await ReadMessage(fakePeer);
            if(message == null)
                throw new IOException("Failed to read message."); 
            if(message.Type != MessageType.KeyExchange)
                throw new IOException("Message of incorrect type."); 
            if(message.PublicKey == null)
                throw new IOException("No publickey in message body."); 

            string peerId = message.Sender; 
            peer = _peerDiscovery.GetPeer(peerId); 

            peer.Client = client;
            peer.Stream = client.GetStream();
            peer.Address = ((IPEndPoint) client.Client.RemoteEndPoint).Address;
            peer.Port = ((IPEndPoint) client.Client.RemoteEndPoint).Port;
            peer.IsConnected = true;
            peer.PublicKey = message.PublicKey; 

            _peerDiscovery.EndpointMap((IPEndPoint) client.Client.RemoteEndPoint, peerId); 
        }
        catch(IOException e)
        {
            Console.WriteLine($"Key exchange with incomming connection {client} failed. Closing connection.");
            Console.WriteLine($"Error: {e}");
            client.Close(); 
            return;
        }

        OnPeerConnected(peer);
        lock (_receiveThreadsLock) {
            // start a receive loop for this specific peer
            Thread receiveThread = new Thread(() => ReceiveLoop(peer));
            // receiveThread.IsBackground = true;
            _receiveThreads.Add(receiveThread);
            receiveThread.Start(); 
        }
    }

    private async Task<Message?> ReadMessage(Peer peer)
    {
        NetworkStream? reader = peer.Stream; 
        if(reader == null) 
            throw new IOException($"Tcp stream for peer {peer} is null"); 
        byte[] messageLengthBuffer = new byte[4];
        byte[] byteMessage; 

        int totalBytes = 0; 
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
            throw new IOException($"Connection lost to Peer {peer}");

        totalBytes = 0; 
        int length = BitConverter.ToInt32(messageLengthBuffer, 0); 

        byteMessage = new byte[length]; 
        
        while(totalBytes < length)
        {
            int bytesRead = await reader.ReadAsync(byteMessage, totalBytes, length - totalBytes); 
            if(bytesRead == 0)
            {
                peer.IsConnected = false; 
                 throw new IOException($"Connection lost to Peer {peer}");
            }
            totalBytes += bytesRead; 
        }

        if(!peer.IsConnected)
            throw new IOException($"Connection lost to Peer {peer}");

        string line = Encoding.UTF8.GetString(byteMessage, 0, length);

        if (line == null)
        {
             throw new IOException($"Encoding of message failed");
        }
        /// Creates a new message object with the received content
        Message? message = JsonSerializer.Deserialize<Message>(line);
        return message; 
    }

    /// <summary>
    /// Receive loop for a specific peer - reads messages until disconnection.
    ///
    /// 1. Create a StreamReader from the peer's stream
    /// 2. Loop while peer is connected and cancellation not requested
    /// 3. Read a line from the stream (ReadLine blocks until data available)
    /// 4. If line is null, the connection was closed - break the loop
    /// 5. Create a Message object with the received content
    /// 6. Invoke OnMessageReceived event with the peer and message
    /// 7. Handle IOException (connection lost)
    /// 8. In finally block, call DisconnectPeer
    /// </summary>
    private async Task ReceiveLoop(Peer peer)
    {
        if (peer.Stream is null)
            throw new NullReferenceException("Peer does not have a stream to receive from");

        if (this._cancellationTokenSource is null) 
            throw new NullReferenceException("_cancellationTokenSource is null");

        if (this.OnMessageReceived is null) 
            throw new NullReferenceException("OnMessageReceived is null");

        try 
        {
            // try to receive a message from this specific peer
            var reader = peer.Stream; 
            while(peer.IsConnected && !_cancellationTokenSource.IsCancellationRequested)
            {
                Message? message = await ReadMessage(peer); 
                if(message != null)
                    OnMessageReceived(peer, message);
            }

        } 
        catch (IOException e) 
        {
            Console.WriteLine("IOException: {0}", e);
        } 
        finally 
        {
            this.DisconnectPeer(peer);
        }
    }


    public async Task SendAsync(Peer peer, Message message)
    {
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
                System.Console.WriteLine($"Error sending to peer {peer}: {ex.Message}");
                DisconnectPeer(peer);
            }
        }
    }

    public async Task SendAsync(TcpClient client, Message message)
    {
        /// Sends the message if the peer is connected and has a valid stream
        if (client != null && client.Connected && client.GetStream() != null)
        {
            byte[] byteMessage = message.ToByteArray(); 
            /// Creates a stream writer to send the message to the peer
            var writer = client.GetStream();
            /// Writes the message line asynchronously
            await writer.WriteAsync(byteMessage, 0, byteMessage.Length);
            /// Flushes the writer to ensure the message is sent
            await writer.FlushAsync();
        }
    }

    /// <summary>
    /// Clean up a disconnected peer.
    ///
    /// 1. Set peer.IsConnected to false
    /// 2. Dispose the peer's Client and Stream
    /// 3. Remove the peer from _connectedPeers (with proper locking)
    /// 4. Invoke OnPeerDisconnected event
    /// </summary>
    private void DisconnectPeer(Peer peer)
    {
        if (peer == null)
            return;

        peer.IsConnected = false;

        try
        {
            peer.Stream?.Dispose();
        }
        catch
        {
        }

        try
        {
            peer.Client?.Dispose();
        }
        catch
        {
        }

        OnPeerDisconnected?.Invoke(peer);
    }

    /// <summary>
    /// Stop the server and close all connections.
    ///
    /// 1. Cancel the cancellation token
    /// 2. Stop the listener
    /// 3. Set IsListening to false
    /// 4. Disconnect all connected peers (with proper locking)
    /// 5. Wait for the listen thread to finish (with timeout)
    /// </summary>
    public void Stop()
    {
        _cancellationTokenSource?.Cancel();

        try
        {
            _listener?.Stop();
        }
        catch
        {
        }

        IsListening = false;

        foreach (var peerId in _peerDiscovery.GetKnownPeers())
        {
            DisconnectPeer(peerId);
        }

        if (_listenThread != null && _listenThread.IsAlive)
        {
            _listenThread.Join();
        }
    }
}
