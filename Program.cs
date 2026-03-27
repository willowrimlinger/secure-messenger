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
    private static byte[]? _myPublicKey;
    // current peer id 
    private static string _myId = Guid.NewGuid().ToString();
    private static Rooms _rooms = new(); 
    private static readonly object _roomsLock = new();

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
                int room = msg.RoomId; 
                if(_rooms.AddPeer(room, peer))
                {
                    Message response = new Message
                    {
                        Sender = _myId, 
                        Type = MessageType.Text,  
                        Content = $"Added to room {room}.",
                        TargetPeerId = peer.Id
                    }; 
                    _messageQueue.EnqueueOutgoing(response); 
                }
                else
                {
                    Message response = new Message
                    {
                        Sender = _myId,
                        Type = MessageType.Text,
                        Content = $"Failed to join room {room}.",
                        TargetPeerId = peer.Id 
                    };
                    _messageQueue.EnqueueOutgoing(response); 
                }
            }
            if(msg.Type == MessageType.CreateRoom)
            {
                if(_rooms.CreateRoom(msg.RoomId))
                {
                    Message response = new Message
                    {
                        Sender = _myId,
                        Type = MessageType.Text,
                        Content = $"Created room {msg.RoomId}.",
                        TargetPeerId = peer.Id 
                    };
                    _messageQueue.EnqueueOutgoing(response);
                }
            }
            
        };
        _server.OnPeerDisconnected += (peer) => 
        {
            _consoleUI.DisplaySystem($"Client disconnected: {peer}");
        };

        _client.OnConnected += (peer) =>  
        {
            _consoleUI.DisplaySystem($"Connected to Server: {peer}");
        };

        _client.OnMessageReceived += async (peer, msg) => 
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
            _messageQueue.EnqueueIncoming(msg);
        };

        _client.OnDisconnected += (peer) => 
        {
            _rooms.RemovePeerAllRooms(peer);
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
                    if (!parsed_input.IsCommand) 
                    {
                        Message msg = new Message 
                        {
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
                                int roomID = int.Parse(parsed_input.Args[0]);
                                Message msg = new Message
                                {
                                    Sender = _myId,
                                    Type = MessageType.CreateRoom,
                                    RoomId = roomID
                                };
                                _messageQueue.EnqueueOutgoing(msg);
                                break;
                            }

                        case CommandType.JoinRoom:
                            {
                                int roomID = int.Parse(parsed_input.Args[0]);
                                Message joinRequest = new Message
                                {
                                    Sender = _myId,
                                    Type = MessageType.JoinRoomRequest,
                                    RoomId = roomID
                                };
                                _messageQueue.EnqueueOutgoing(joinRequest);
                                break;
                            }
                        case CommandType.LeaveRoom:
                            {
                                int roomID = int.Parse(parsed_input.Args[0]);
                                Message msg = new Message
                                {
                                    Sender = _myId,
                                    Type = MessageType.LeaveRoom,
                                    RoomId = roomID 
                                };
                                _messageQueue.EnqueueOutgoing(msg);
                                break;
                            }

                        case CommandType.ListRooms:
                            {
                                foreach(var room in _rooms.GetRooms())
                                {
                                    Console.WriteLine(room); 
                                }
                            }
                            break;

                        case CommandType.SendToRoom:
                            {
                                int roomId = int.Parse(parsed_input.Args[0]);
                                string content = string.Join(" ", parsed_input.Args.Skip(1));
                                Message msg = new Message 
                                {
                                    Sender = _myId,
                                    Type = MessageType.Text,
                                    Content = content,
                                    RoomId = roomId 

                                }; 
                                _messageQueue.EnqueueOutgoing(msg);
                                break;
                                
                            }
                        case CommandType.Unknown:
                            Console.WriteLine($"\n{parsed_input.Message}. Please try again!\n");
                            break;}break;
            }

        }

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

