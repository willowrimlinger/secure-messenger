// [Your Name Here]
// CSCI 251 - Secure Distributed Messenger

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;
using SecureMessenger.Core;

namespace SecureMessenger.Network;

/// <summary>
/// Sprint 3: UDP-based peer discovery using broadcast.
/// Broadcasts presence and listens for other peers on the local network.
///
/// Discovery Protocol:
/// - Message format: "PEER:{peerId}:{tcpPort}"
/// - Example: "PEER:abc12345:5000"
/// - Broadcast every 5 seconds
/// - Peers timeout after 30 seconds of no broadcasts
/// </summary>
public class PeerDiscovery
{
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly ConcurrentDictionary<string, Peer> _knownPeers = new();
    private readonly int _broadcastPort = 5001;
    private Thread? _listenThread;
    private Thread? _broadcastThread;

    public event Action<Peer>? OnPeerDiscovered;
    public event Action<Peer>? OnPeerLost;

    public int TcpPort { get; private set; }
    public string LocalPeerId { get; } = Guid.NewGuid().ToString()[..8];

    /// <summary>
    /// Start broadcasting presence and listening for other peers.
    ///
    /// TODO: Implement the following:
    /// 1. Store the TCP port
    /// 2. Create a new CancellationTokenSource
    /// 3. Create a UdpClient on the broadcast port
    /// 4. Enable broadcast on the UDP client
    /// 5. Create and start a thread for ListenLoop
    /// 6. Create and start a thread for BroadcastLoop
    /// 7. Start a background task for TimeoutCheckLoop
    /// </summary>
    public void Start(int tcpPort)
    {
        TcpPort = tcpPort; 
        _cancellationTokenSource = new CancellationTokenSource(); 
        _udpClient = new UdpClient()
        {
            EnableBroadcast = true,
            ExclusiveAddressUse = false,
        };
        _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, _broadcastPort));
        _listenThread = new Thread(ListenLoop); 
        _listenThread.Start(); 
        _broadcastThread = new Thread(BroadcastLoop); 
        _broadcastThread.Start(); 
        Task.Run(TimeoutCheckLoop);
    }

    /// <summary>
    /// Periodically broadcast our presence to the network.
    ///
    /// TODO: Implement the following:
    /// 1. Create an IPEndPoint for broadcast (IPAddress.Broadcast, _broadcastPort)
    /// 2. Loop while cancellation not requested:
    ///    a. Create discovery message: "PEER:{LocalPeerId}:{TcpPort}"
    ///    b. Convert to bytes using UTF8 encoding
    ///    c. Send via UDP client to the broadcast endpoint
    ///    d. Handle SocketException (ignore broadcast errors)
    ///    e. Sleep for 5 seconds between broadcasts
    /// </summary>
    private void BroadcastLoop()
    {
        IPEndPoint iPEndPoint = new(IPAddress.Broadcast, _broadcastPort); 
        while(!_cancellationTokenSource!.IsCancellationRequested)
        {
            string discovery = $"PEER:{LocalPeerId}:{TcpPort}";
            byte[] msg = Encoding.UTF8.GetBytes(discovery);  
            try 
            {
                _udpClient!.Send(msg, msg.Length, iPEndPoint); 
            }
            catch(SocketException) 
            { /*Ignore SocketException */}
            Thread.Sleep(5000); 
        }
    }

    /// <summary>
    /// Listen for peer broadcast messages.
    ///
    /// TODO: Implement the following:
    /// 1. Create an IPEndPoint for receiving (IPAddress.Any, _broadcastPort)
    /// 2. Loop while cancellation not requested:
    ///    a. Receive data from UDP client (blocks until data available)
    ///    b. Convert bytes to string using UTF8 encoding
    ///    c. If message starts with "PEER:", call ProcessDiscoveryMessage
    ///    d. Handle SocketException (ignore receive errors)
    /// </summary>
    private void ListenLoop()
    {
        IPEndPoint iPEndPoint = new(IPAddress.Any, _broadcastPort); 
        while(!_cancellationTokenSource!.IsCancellationRequested)
        {
            try 
            {
                byte[] data = _udpClient!.Receive(ref iPEndPoint); 
                string message = Encoding.UTF8.GetString(data); 

                if(message.StartsWith("PEER:"))
                {
                    ProcessDiscoveryMessage(message, iPEndPoint.Address);
                }
            }
            catch(SocketException)
            {/*Ignore SocketException*/}
        }
    }

    /// <summary>
    /// Parse a discovery message and add/update the peer.
    ///
    /// TODO: Implement the following:
    /// 1. Split the message by ':' - format is "PEER:peerId:port"
    /// 2. Validate we have at least 3 parts
    /// 3. Extract peerId (parts[1]) and port (parts[2])
    /// 4. If peerId equals LocalPeerId, return (don't add ourselves)
    /// 5. Create a Peer object with the extracted info and current timestamp
    /// 6. Try to add to _knownPeers:
    ///    - If new peer, invoke OnPeerDiscovered
    ///    - If existing peer, update LastSeen timestamp
    /// </summary>
    private void ProcessDiscoveryMessage(string message, IPAddress senderAddress)
    {
        var parts = message.Split(":"); 
        string peerId = parts[1]; 
        string port = parts[2]; 

        if(peerId == LocalPeerId) 
            return; 
        
        if(_knownPeers.ContainsKey(peerId))
        {
            Peer? peer; 
            _knownPeers.TryGetValue(peerId, out peer); 
            Peer? newPeer = peer; 
            newPeer!.LastSeen = DateTime.Now; 
            _knownPeers.TryUpdate(peerId, newPeer, peer!); 
        }
        else
        {
            Peer peer = new Peer()
            {
                Id = peerId, 
                Address = senderAddress,
                Port = int.Parse(port)
            };
            _knownPeers.TryAdd(peerId, peer); 
            OnPeerDiscovered!(peer); 
        }
    }
    /// <summary>
    /// Periodically check for peers that have timed out (no broadcast in 30 seconds).
    ///
    /// TODO: Implement the following:
    /// 1. Loop while cancellation not requested:
    ///    a. Define timeout as 30 seconds
    ///    b. Get current time
    ///    c. Iterate through _knownPeers
    ///    d. If (now - peer.LastSeen) > timeout:
    ///       - Remove from _knownPeers
    ///       - Invoke OnPeerLost
    ///    e. Delay 5 seconds between checks
    /// </summary>
    private async Task TimeoutCheckLoop()
    {
        while(!_cancellationTokenSource!.IsCancellationRequested)
        {
            DateTime now = DateTime.Now;
            int timeout = 30;

            foreach(var peer in GetKnownPeers())
            {
                if((now - peer.LastSeen).Seconds > timeout)
                {
                    _knownPeers.TryRemove(peer.Id, out _); 
                    OnPeerLost!(peer);
                }
            }
            Thread.Sleep(5000); 
        }
    }

    /// <summary>
    /// Get list of known peers.
    /// </summary>
    public IEnumerable<Peer> GetKnownPeers()
    {
        return _knownPeers.Values.ToList();
    }

    /// <summary>
    /// Stop discovery.
    ///
    /// TODO: Implement the following:
    /// 1. Cancel the cancellation token
    /// 2. Close the UDP client
    /// 3. Wait for threads to finish (with timeout)
    /// </summary>
    public void Stop()
    {
        _cancellationTokenSource!.Cancel(); 
        _udpClient!.Close();
        _listenThread!.Join(10000);
        _broadcastThread!.Join(10000); 
    }
}
