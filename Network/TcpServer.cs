// Willow Rimlinger
// CSCI 251 - Secure Distributed Messenger

using System.Net;
using System.Net.Sockets;
using SecureMessenger.Core;

namespace SecureMessenger.Network;

/// <summary>
/// TCP server that listens for incoming peer connections.
/// Each peer runs both a server (to accept connections) and client (to initiate connections).
/// </summary>
public class TcpServer
{
    private TcpListener? _listener;
    private readonly List<Peer> _connectedPeers = new();
    private object _connectedPeersLock = new object();
    private CancellationTokenSource? _cancellationTokenSource;
    private Thread? _listenThread;
    private List<Thread> _receiveThreads = new();
    private object _receiveThreadsLock = new object();

    public event Action<Peer>? OnPeerConnected;
    public event Action<Peer>? OnPeerDisconnected;
    public event Action<Peer, Message>? OnMessageReceived;

    public int Port { get; private set; }
    public bool IsListening { get; private set; }

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
        this.Port = port;
        this._cancellationTokenSource = new CancellationTokenSource();
        this._listener = new TcpListener(IPAddress.Any, port);
        this._listener.Start();
        this.IsListening = true;
        this._listenThread = new Thread(ListenLoop);
        this._listenThread.Start();
        Console.WriteLine("Server is listening...");
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
        if (this._cancellationTokenSource is null || this._listener is null) {
            throw new NullReferenceException("Must call Start() before invoking ListenLoop");
        }
        while (!this._cancellationTokenSource.Token.IsCancellationRequested) {
            try {
                if (this._listener.Pending()) {
                    TcpClient client = this._listener.AcceptTcpClient();
                    this.HandleNewConnection(client);
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
    private void HandleNewConnection(TcpClient client)
    {
        if (client.Client.RemoteEndPoint is null) {
            throw new NullReferenceException("Client must have a remote endpoint assigned");
        }
        if (this.OnPeerConnected is null) {
            throw new NullReferenceException("OnPeerConnected is null");
        }

        // create a port for the server to get track of
        Peer peer = new Peer();
        peer.Client = client;
        peer.Stream = client.GetStream();
        peer.Address = ((IPEndPoint) client.Client.RemoteEndPoint).Address;
        peer.Port = ((IPEndPoint) client.Client.RemoteEndPoint).Port;
        peer.IsConnected = true;

        lock (_connectedPeersLock) {
            this._connectedPeers.Add(peer);
        }

        this.OnPeerConnected(peer);
        lock (_receiveThreadsLock) {
            // start a receive loop for this specific peer
            Thread receiveThread = new Thread(() => ReceiveLoop(peer));
            this._receiveThreads.Add(receiveThread);
            receiveThread.Start(); 
        }
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
    private void ReceiveLoop(Peer peer)
    {
        if (peer.Stream is null) {
            throw new NullReferenceException("Peer does not have a stream to receive from");
        }
        if (this._cancellationTokenSource is null) {
            throw new NullReferenceException("_cancellationTokenSource is null");
        }
        if (this.OnMessageReceived is null) {
            throw new NullReferenceException("OnMessageReceived is null");
        }

        try {
            // try to receive a message from this specific peer
            var streamReader = new StreamReader(peer.Stream);

            while (peer.IsConnected && !this._cancellationTokenSource.Token.IsCancellationRequested) {
                //Console.WriteLine("Flag");
                var line = streamReader.ReadLine();
                if (line is null) {
                    // connection closed
                    break;
                }
                var message = new Message();
                message.Content = line;
                // if we received a message, call callback
                // which adds it to the outgoing queue for broadcast to all other peers
                this.OnMessageReceived(peer, message);
            }
        } catch (IOException e) {
            Console.WriteLine("IOException: {0}", e);
        } finally {
            this.DisconnectPeer(peer);
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
        peer.IsConnected = false;
        if (peer.Client is not null) {
            peer.Client.Dispose();
        }
        if (peer.Stream is not null) {
            peer.Stream.Dispose();
        }
        lock (this._connectedPeersLock) {
            this._connectedPeers.Remove(peer);
        }
        if (this.OnPeerDisconnected is not null) {
            this.OnPeerDisconnected(peer);
        }
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
        if (this._cancellationTokenSource is not null) {
            this._cancellationTokenSource.Cancel();
        }
        if (this._listener is not null) {
            this._listener.Stop();
        }
        this.IsListening = false;
        lock (this._connectedPeersLock) {
            this._connectedPeers.Clear();
        }
        if (this._listenThread is not null) {
            this._listenThread.Join();
        }
    }

    /// <summary>
    /// Get a list of currently connected peers.
    /// Remember to use proper locking when accessing _connectedPeers.
    /// </summary>
    public IEnumerable<Peer> GetConnectedPeers()
    {
        lock (_connectedPeers)
        {
            return _connectedPeers.ToList();
        }
    }
}
