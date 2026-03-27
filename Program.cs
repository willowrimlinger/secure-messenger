// Sean Gaines, Alia Ulanbek Kyzy, Mikey Reizenstein
// CSCI 251 - Secure Distributed Messenger
// Group Project
//
// SPRINT 1: Threading & Basic Networking
// Due: Week 5 | Work on: Weeks 3-4
// (Continue enhancing in Sprints 2 & 3)
//

using SecureMessenger.Core;
using SecureMessenger.Network;
using SecureMessenger.Security;
using SecureMessenger.UI;
using System.Text.Json; 
using System.Text;
using System.Net.WebSockets;

namespace SecureMessenger;

/// <summary>
/// Main entry point for the Secure Distributed Messenger.
///
/// Architecture Overview:
/// This application uses multiple threads to handle concurrent operations:
///
/// 1. Main Thread (UI Thread)
///    - Reads user input from console
///    - Parses commands using ConsoleUI
///    - Dispatches commands to appropriate handlers
///
/// 2. Listen Thread (Server)
///    - Runs TcpServer to accept incoming connections
///    - Each accepted connection spawns a receive thread
///
/// 3. Receive Thread(s)
///    - One per connected peer
///    - Reads messages from network
///    - Enqueues to incoming message queue
///
/// 4. Send Thread
///    - Dequeues from outgoing message queu
///    - Sends messages to connected peers
///
/// 5. Process Thread (Optional)
///    - Dequeues from incoming message queue
///    - Displays messages to user
///    - Handles decryption and verification
///
/// Thread Communication:
/// - Use MessageQueue for thread-safe message passing
/// - Use CancellationToken for graceful shutdown
/// - Use events for peer connection/disconnection notifications
///
/// Sprint Progression:
/// - Sprint 1: Basic threading and networking (connect, send, receive)
/// - Sprint 2: Add encryption (key exchange, AES encryption, signing)
/// - Sprint 3: Add resilience (peer discovery, heartbeat, reconnection)
/// </summary>
class Program
{
    private static MessageQueue? _messageQueue;
    private static TcpServer? _server;
    private static TcpClientHandler? _client;
    private static ConsoleUI? _consoleUI;
    private static CancellationTokenSource? _cancellationTokenSource;
    private static RsaEncryption? _myRsa;
    private static byte[] _myPublicKey;
    // current peer id 
    private static string _myId = Guid.NewGuid().ToString();
    private static int _myListeningPort;
    private static string _myHost = "127.0.0.1";
    private static readonly Dictionary<string, List<RoomMember>> _rooms = new ();
    private static readonly object _roomsLock = new object();
    // Helper Functions
    private static RoomMember? GetRoomMemberByPeerId(string roomId, string peerId)
    {
        if (!_rooms.ContainsKey(roomId))
            return null;

        return _rooms[roomId].FirstOrDefault(m => m.PeerId == peerId);
    }
    private static bool IsSelfInRoom(string roomId)
    {
        if (!_rooms.ContainsKey(roomId))
            return false;

        return _rooms[roomId].Any(m => m.PeerId == _myId);
    }
    private static void RemovePeerFromAllRooms(string peerId)
    {
        lock (_roomsLock)
        {
            var emptyRooms = new List<string>();

            foreach (var room in _rooms)
            {
                room.Value.RemoveAll(member => member.PeerId == peerId);
                if (room.Value.Count == 0)
                    emptyRooms.Add(room.Key);
            }

            foreach (string roomId in emptyRooms)
                _rooms.Remove(roomId);
        }
    }

    private static Peer? FindPeerById(string peerId)
    {
        Peer? peer = _client?.GetPeer(peerId);
        if (peer != null) return peer;

        return _server?.GetPeer(peerId);
    }

    static async Task Main(string[] args)
    {
        Console.WriteLine("Secure Distributed Messenger");
        Console.WriteLine("============================");

        // 1. Create CancellationTokenSource for shutdown signaling
        // 2. Create MessageQueue for thread communication
        // 3. Create ConsoleUI for user interface
        // 4. Create TcpServer for incoming connections
        // 5. Create TcpClientHandler for outgoing connections

        _messageQueue = new MessageQueue();
        _consoleUI = new ConsoleUI(_messageQueue);
        _server = new TcpServer();
        _client = new TcpClientHandler();
        _cancellationTokenSource = new CancellationTokenSource();


        // 1. TcpServer.OnPeerConnected - handle new incoming connections
        // 2. TcpServer.OnMessageReceived - handle received messages
        // 3. TcpServer.OnPeerDisconnected - handle disconnections
        // 4. TcpClientHandler events (same pattern)

        // Sprint 2:

        _myRsa = new RsaEncryption();
        _myPublicKey = _myRsa.ExportPublicKey();

        _server.OnPeerConnected += (peer) => 
        { 
            _consoleUI.DisplaySystem($"Client connected: {peer}");
            Message keyExchange = new Message
            {
                Sender = _myId, 
                Type = MessageType.KeyExchange, 
                PublicKey = _myPublicKey,
                TargetPeerId = peer.Id
            }; 
            _messageQueue.EnqueueOutgoing(keyExchange); 
        };

        _server.OnMessageReceived += (peer, msg) => 
        {
            if(msg.Type == MessageType.Text && peer.Aes != null)
            {
                msg.Content = Encoding.UTF8.GetString(peer.Aes.Decrypt(msg.EncryptedContent)); 
                msg.EncryptedContent = null; 
                _consoleUI.DisplayMessage(msg);
                _messageQueue.EnqueueOutgoing(msg);
            }
            if(msg.Type == MessageType.KeyExchange)
            {
                peer.PublicKey = msg.PublicKey; 
                peer.PeerRsa = new RsaEncryption(); 
                peer.PeerRsa.ImportPublicKey(peer.PublicKey); 
            }
            if(msg.Type == MessageType.SessionKey)
            {
                byte[] key = _myRsa.DecryptSessionKey(msg.EncryptedContent); 
                peer.AesKey = key; 
                peer.Aes = new AesEncryption(peer.AesKey); 
            }
            if(msg.Type == MessageType.JoinRoomRequest)
            {
                if (string.IsNullOrWhiteSpace(msg.RoomId))
                {
                    _consoleUI.DisplaySystem("Received JoinRoomRequest with no RoomId.");
                    return;
                }

                List<RoomPeerInfo> roomPeers;

                lock (_roomsLock)
                {
                    if (!_rooms.ContainsKey(msg.RoomId))
                    {
                        _consoleUI.DisplaySystem($"Join request rejected. Room '{msg.RoomId}' does not exist.");
                        return;
                    }

                    RoomMember? existing = GetRoomMemberByPeerId(msg.RoomId, msg.Sender);

                    if (existing == null)
                    {
                        _rooms[msg.RoomId].Add(new RoomMember
                        {
                            PeerId = msg.Sender,
                            Host = msg.Host ?? "127.0.0.1",
                            Port = msg.Port
                        });

                        _consoleUI.DisplaySystem($"Peer {msg.Sender} joined room {msg.RoomId}");
                    }

                    roomPeers = _rooms[msg.RoomId]
                        .Select(m => new RoomPeerInfo
                        {
                            PeerId = m.PeerId,
                            Host = m.Host,
                            Port = m.Port
                        })
                        .ToList();
                }

                Message response = new Message
                {
                    Sender = _myId,
                    Type = MessageType.JoinRoomResponse,
                    RoomId = msg.RoomId,
                    RoomPeers = roomPeers,
                    TargetPeerId = peer.Id
                };

                _messageQueue.EnqueueOutgoing(response);
            }
        };
        _server.OnPeerDisconnected += (peer) => 
        {
            RemovePeerFromAllRooms(peer.Id);
            _consoleUI.DisplaySystem($"Client disconnected: {peer}");
        };

        _client.OnConnected += (peer) =>  
        {
            _consoleUI.DisplaySystem($"Connected to Server: {peer}");
        };

        _client.OnMessageReceived += (peer, msg) => 
        {
            if (msg.Type == MessageType.KeyExchange ) 
            {
                peer.PublicKey = msg.PublicKey;
                peer.PeerRsa = new RsaEncryption();
                peer.PeerRsa.ImportPublicKey(peer.PublicKey);
                

                peer.AesKey = AesEncryption.GenerateKey();
                peer.Aes = new AesEncryption(peer.AesKey);
                byte[] encryptedSessionKey = peer.PeerRsa.EncryptSessionKey(peer.AesKey, peer.PublicKey);
                Message response = new Message {
                    Sender = _myId,
                    Type = MessageType.SessionKey,
                    EncryptedContent = encryptedSessionKey, 
                    TargetPeerId = peer.Id
                }; 
                _messageQueue.EnqueueOutgoing(response);
            }
            else if (msg.Type == MessageType.SessionKey) 
            {
                byte[] decryptedKey = _myRsa.DecryptSessionKey(msg.EncryptedContent);
                peer.AesKey = decryptedKey;
                peer.Aes = new AesEncryption(peer.AesKey);
            }

            if (msg.Type == MessageType.Text && peer.Aes != null)
            {
                msg.Content = Encoding.UTF8.GetString(peer.Aes.Decrypt(msg.EncryptedContent));
            }
            if (msg.Type == MessageType.JoinRoomResponse)
            {
                if (string.IsNullOrWhiteSpace(msg.RoomId) || msg.RoomPeers == null)
                {
                    _consoleUI.DisplaySystem("Received invalid JoinRoomResponse.");
                    return;}
                lock (_roomsLock)
                {
                    if (!_rooms.ContainsKey(msg.RoomId))
                        _rooms[msg.RoomId] = new List<RoomMember>();

                    _rooms[msg.RoomId].Clear();

                    foreach (var roomPeer in msg.RoomPeers)
                    {
                        _rooms[msg.RoomId].Add(new RoomMember
                        {
                            PeerId = roomPeer.PeerId,
                            Host = roomPeer.Host,
                            Port = roomPeer.Port
                        });}}
                _consoleUI.DisplaySystem($"Joined room {msg.RoomId}. Connecting to room peers...");
                foreach (var roomPeer in msg.RoomPeers)
                {
                    if (roomPeer.PeerId == _myId)
                        continue;

                    if (FindPeerById(roomPeer.PeerId) != null)
                        continue;
                    try
                    {
                        await _client.ConnectAsync(roomPeer.Host, roomPeer.Port);
                    }
                    catch (Exception ex)
                    {
                        _consoleUI.DisplaySystem(
                            $"Failed to connect to room peer {roomPeer.PeerId} at {roomPeer.Host}:{roomPeer.Port} - {ex.Message}"
                        );}}return;}
            _messageQueue.EnqueueIncoming(msg);
        };

        _client.OnDisconnected += (peer) => 
        {
            RemovePeerFromAllRooms(peer.Id);
            _consoleUI.DisplaySystem($"Disconnect from server: {peer}");
        };

        // 1. Start a thread/task for processing incoming messages
        // 2. Start a thread/task for sending outgoing messages
        // Note: TcpServer.Start() will create its own listen thread
        var incomingThread = new Thread(() =>
        {
            try 
            {
                while (!_cancellationTokenSource!.IsCancellationRequested)
                {
                    Message msg = _messageQueue.DequeueIncoming(_cancellationTokenSource.Token);
                    if(msg.Type == MessageType.Text)
                        _consoleUI.DisplayMessage(msg);
                }
            } catch (OperationCanceledException)
            {
            } 
            catch (InvalidOperationException) 
            {
            }
        });

        var outgoingThread = new Thread(() =>
        {
            try 
            {
                while (!_cancellationTokenSource!.IsCancellationRequested)
                {
                    Message msg = _messageQueue.DequeueOutgoing();
                    //msg.printLong(); 

                    Peer? peer; 
                    if(msg.TargetPeerId != null && 
                       ((peer = _client.GetPeer(msg.TargetPeerId)) != null))
                    {
                        if(msg.Type == MessageType.Text)
                        {
                            msg.EncryptedContent = peer.Aes.Encrypt(msg.Content);  
                            msg.Content = ""; 
                        }
                        _ = _client.SendAsync(msg.TargetPeerId, msg); 
                    }
                    else
                    {
                        foreach(Peer p in _client.GetConnectedPeers())
                        {
                            Message send = new(msg); 
                            if(msg.Type == MessageType.Text)
                            {
                                send.EncryptedContent = p.Aes.Encrypt(msg.Content);  
                                send.Content = ""; 
                            }
                            _ = _client.SendAsync(p.Id, send); 
                        }
                    }

                    if(_server.IsListening) 
                    {
                        if(msg.TargetPeerId != null && 
                        ((peer = _server.GetPeer(msg.TargetPeerId)) != null))
                        {
                            if(msg.Type == MessageType.Text)
                            {
                                msg.EncryptedContent = peer.Aes.Encrypt(msg.Content);  
                                msg.Content = ""; 
                            }
                            _ = _server.SendAsync(msg.TargetPeerId, msg); 
                        }
                        else
                        {
                            foreach(Peer p in _server.GetConnectedPeers())
                            {
                                Message send = new(msg); 

                                if(msg.Type == MessageType.Text)
                                {
                                    send.EncryptedContent = p.Aes.Encrypt(msg.Content);  
                                    send.Content = ""; 
                                }
                                _ = _server.SendAsync(p.Id, send); 
                            }
                        }
                    }

                    
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        });

        incomingThread.Start();
        outgoingThread.Start();


        Console.WriteLine("Type /help for available commands");
        Console.WriteLine();

        // Main loop - handle user input
        bool running = true;
        while (running)
        {
            // 1. Read a line from the console
            // 2. Skip empty input
            // 3. Parse the input using ConsoleUI.ParseCommand()
            // 4. Handle the command based on CommandType:
            //    - Connect: Call TcpClientHandler.ConnectAsync()
            //    - Listen: Call TcpServer.Start()
            //    - ListPeers: Display connected peers
            //    - History: Show message history
            //    - Quit: Set running = false
            //    - Not a command: Send as a message to peers

            var input = Console.ReadLine();
            if (string.IsNullOrEmpty(input)) continue;
            
            switch (input.ToLower())
            {
                case "/help":
                    _consoleUI.ShowHelp(); 
                    break;
                default:
                    var parsed_input = _consoleUI.ParseCommand(input);
                    if (!parsed_input.IsCommand) {
                        Message msg = new Message {
                            Sender = _myId,
                            Type = MessageType.Text,
                            Content = parsed_input.Message
                        }; 
                        _messageQueue.EnqueueOutgoing(msg); 
                        continue; 
                    }

                    switch (parsed_input.CommandType)
                    {
                        case CommandType.Connect:
                            await _client!.ConnectAsync(parsed_input.Args[0], int.Parse(parsed_input.Args[1]));
                            break;

                        case CommandType.Listen:
                            if (!_server.IsListening) {
                                try
                                {
                                    _server.Start(int.Parse(parsed_input.Args[0]));
                                    _myListeningPort = int.Parse(parsed_input.Args[0]);
                                }
                                catch(Exception e)
                                {
                                    _consoleUI.DisplaySystem($"Invalid format {parsed_input.Args[0]}. Port should be a single integer 0-65535"); 
                                }
                            } else {
                                _consoleUI.DisplaySystem("Server is already listening");
                            }
                            break;
                        
                        case CommandType.ListPeers:
                            foreach(var peer in _server.GetConnectedPeers())
                            {
                                Console.WriteLine(peer); 
                            }
                            break;
                        
                        case CommandType.History:
                            break;

                        case CommandType.Quit:
                            running = false;
                            break;

                        case CommandType.CreateRoom:
                            {
                                string roomID = parsed_input.Args[0];
                                lock(_roomsLock)
                                {
                                    if(!_rooms.ContainsKey(roomID))
                                    {
                                        _rooms[roomID] = new List<RoomMember>();
                                        _rooms[roomID].Add(new RoomMember
                                        {
                                            PeerId = _myId,
                                            Host = _myHost,
                                            Port = _myListeningPort
                                        });
                                        _messageQueue.EnqueueOutgoing(joinRequest);
                                        _consoleUI.DisplaySystem($"Room {roomID} created");
                                    }
                                    else
                                    {
                                        _consoleUI.DisplaySystem($"Room {roomID} already exists");
                                    }
                                }
                            break;
                            }

                        case CommandType.JoinRoom:
                            {
                                string roomID = parsed_input.Args[0];
                                lock(_roomsLock)
                                {
                                    if(!_rooms.ContainsKey(roomID))
                                    {
                                        _consoleUI.DisplaySystem($"Room {roomID} does not exist");
                                        break;
                                    }
                                    
                                    else
                                    {
                                        _consoleUI.DisplaySystem($"Already in room {roomID}");
                                    }
                                }
                            break;
                            }

                        case CommandType.LeaveRoom:
                            {
                                string roomID = parsed_input.Args[0];
                                lock(_roomsLock)
                                {
                                    if(!_rooms.ContainsKey(roomID))
                                    {
                                        _consoleUI.DisplaySystem($"Room {roomID} does not exist");
                                        break;
                                    }
                                    int removed = _rooms[roomID].RemoveAll(member => member.PeerId == _myId);
                                    if (removed > 0)
                                    {
                                        _consoleUI.DisplaySystem($"Left room {roomID}");
                                    }
                                    else
                                    {
                                        _consoleUI.DisplaySystem($"Not in room {roomID}");
                                    }
                                }
                            break;
                            }

                        case CommandType.ListRooms:
                            {
                                lock(_roomsLock)
                                {
                                    if(_rooms.Count == 0)
                                    {
                                        _consoleUI.DisplaySystem("No rooms available");
                                        break;
                                    }
                                    foreach(var room in _rooms)
                                    {
                                        Console.WriteLine($"{room.Key} ({room.Value.Count} members)");
                                    }
                                }
                            }
                            break;

                        case CommandType.SendToRoom:
                            {
                                string roomId = parsed_input.Args[0];
                                string content = string.Join(" ", parsed_input.Args.Skip(1));

                                List<string> roomPeerIds;

                                lock (_roomsLock)
                                {
                                    if (!_rooms.ContainsKey(roomId))
                                    {
                                        _consoleUI.DisplaySystem($"Room '{roomId}' does not exist.");
                                        break;
                                    }
                                    roomPeerIds = _rooms[roomId]
                                        .Select(member => member.PeerId)
                                        .ToList();
                                }
                                if (roomPeerIds.Count == 0)
                                {
                                    _consoleUI.DisplaySystem($"Room '{roomId}' is empty.");
                                    break;
                                }
                                foreach (string peerId in roomPeerIds)
                                {
                                    if (FindPeerById(peerId) == null)
                                        continue;

                                    Message msg = new Message
                                    {
                                        Sender = _myId,
                                        Type = MessageType.Text,
                                        Content = content,
                                        TargetPeerId = peerId
                                    };

                                    _messageQueue.EnqueueOutgoing(msg);
                                }
                            }
                            break;
                        case CommandType.Unknown:
                            Console.WriteLine($"\n{parsed_input.Message}. Please try again!\n");
                            break;}break;}

        // 1. Cancel the CancellationTokenSource
        // 2. Stop the TcpServer
        // 3. Disconnect all clients
        // 4. Complete the MessageQueue
        // 5. Wait for background threads to finish

        _cancellationTokenSource.Cancel();

        _messageQueue.CompleteAdding();

        _server?.Stop();
        _client?.DisconnectAll();

        incomingThread.Join();
        outgoingThread.Join();

        Console.WriteLine("Goodbye!");
    }
}
}

