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
using System.Security.Cryptography;
using Microsoft.VisualBasic;
using System.Net;

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
    private static RsaEncryption _myRsa = new RsaEncryption();
    public readonly static byte[] _myPublicKey = _myRsa.ExportPublicKey();
    private static MessageSigner? _signer; 
    private static int _currentRoom = -1;

    // current peer id 
    private static Rooms _rooms = new(); 
    private static readonly object _roomsLock = new();

    private static PeerDiscovery _peerDiscovery = new(); 
    // sprint 3 - instance of message history 
    private static MessageHistory _history = new MessageHistory();

    private static void ForwardMessage(Message msg)
    {

        string[] forward = 
            [.. _peerDiscovery.SetSubtract(msg.SeenBy).Where(peer => _peerDiscovery.GetPeer(peer).IsConnected)];
        if(msg.RoomId > -1 && forward.Length > 0)
        {
            forward = [.. forward.Where(peer => _rooms.ContainsPeer(msg.RoomId, peer))]; 
        }
        
        if(forward.Length > 0)
        {

            //Console.WriteLine($"Forwaring to : {string.Join(", ", forward)}");
            msg.TargetPeerId = forward; 
            foreach(var peerID in forward)
            {
                _messageQueue.EnqueueOutgoing(msg); 
            }
        } 
    }

    static async Task Main(string[] args)
    {
        Console.WriteLine("Secure Distributed Messenger");
        Console.WriteLine("============================");

        _messageQueue = new MessageQueue();
        _consoleUI = new ConsoleUI(_messageQueue);
        _server = new TcpServer(_peerDiscovery);
        _client = new TcpClientHandler(_peerDiscovery);
        _cancellationTokenSource = new CancellationTokenSource();
        _signer = new MessageSigner(_myRsa.GetRSA());


        _server.OnPeerConnected += (peer) => 
        {
            peer.AesKey = AesEncryption.GenerateKey(); 
            peer.Aes = new AesEncryption(peer.AesKey); 
            byte[] encryptedKey = _myRsa.EncryptSessionKey(peer.AesKey, peer.PublicKey); 
            byte[] signature = _signer.SignData(encryptedKey); 
            Message sessionKey = new Message
            {
                Sender = _peerDiscovery.LocalPeerId,
                Type = MessageType.SessionKey,
                Signature = signature,
                EncryptedContent = encryptedKey,
                TargetPeerId = [peer.Id]
            };
            _messageQueue.EnqueueOutgoing(sessionKey); 
            _consoleUI.DisplaySystem($"Client connected: {peer}");
        };

        _server.OnMessageReceived += (peer, msg) =>
        {
            if(msg.Source == _peerDiscovery.LocalPeerId) 
                return; 
            _messageQueue.EnqueueIncoming(msg); 
        };
        _server.OnPeerDisconnected += (peer) => 
        {
            _consoleUI.DisplaySystem($"Client disconnected: {peer}");
        };

        _client.OnConnected += (peer) =>  
        {
            _consoleUI.DisplaySystem($"Connected to Peer: {peer}");
        };

        _client.OnMessageReceived += async (peer, msg) => 
        {
            if(msg.Source == _peerDiscovery.LocalPeerId) 
                return; 
            _messageQueue.EnqueueIncoming(msg); 
        };

        _client.OnDisconnected += (peer) => 
        {
            _rooms.RemovePeerAllRooms(peer.Id);
            _consoleUI.DisplaySystem($"Disconnected from Peer: {peer}");
        };

        _peerDiscovery.OnPeerDiscovered += (peer) =>
        {
            _consoleUI.DisplaySystem($"Found Peer {peer.Address}:{peer.Port}"); 
        };

        _peerDiscovery.OnPeerLost += (peer) =>
        {
            _consoleUI.DisplaySystem($"Lost Peer {peer.Address}:{peer.Port}"); 
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
                    Peer peer = _peerDiscovery.GetPeer(msg.Sender); 
                    if(peer == null)
                    {
                        _consoleUI.DisplaySystem($"Message failed. Peer {peer} not found among known peers."); 
                        break; 
                    }
                    if(peer.PublicKey != null)
                    {
                        if(!_signer.VerifyData(msg.EncryptedContent, msg.Signature, peer.PublicKey))
                            _consoleUI.DisplaySystem($"Message {msg.Id} contains invalid signature, rejecting.");
                    }
                    switch(msg.Type)
                    {
                        case MessageType.KeyExchange:
                            if(msg.PublicKey == null)
                            {
                                _consoleUI.DisplaySystem($"Key exchange failed. No public key included in message."); 
                                break; 
                            }
                            if(peer.PublicKey != null) break; 
                            peer.PublicKey = msg.PublicKey;
                            Message response = new Message
                            {
                                Sender = _peerDiscovery.LocalPeerId,
                                Type = MessageType.KeyExchange,
                                PublicKey = _myPublicKey,
                                TargetPeerId = [peer.Id],
                            };
                            _messageQueue.EnqueueOutgoing(response); 
                            break;
                        case MessageType.SessionKey: 
                        {
                            if(peer.PublicKey == null)
                            {
                                _consoleUI.DisplaySystem($"Session key sent before public key exchange");
                                break;
                            }
                            byte[] aesKey = _myRsa.DecryptSessionKey(msg.EncryptedContent); 
                            peer.AesKey = aesKey; 
                            peer.Aes = new AesEncryption(aesKey);

                            Message roomSync = new Message
                            {
                                Source = _peerDiscovery.LocalPeerId,
                                Type = MessageType.SyncRoomInformation,
                                TargetPeerId = [peer.Id],
                                Content = $"{JsonSerializer.Serialize(_rooms)};SendResponse"
                            }; 
                            _messageQueue.EnqueueOutgoing(roomSync); 
                            break;
                        }

                        case MessageType.SyncRoomInformation:
                        {
                            msg.Content = Encoding.UTF8.GetString(peer.Aes.Decrypt(msg.EncryptedContent)); 
                            string[] parts = msg.Content.Split(";"); 
                            if(parts[1] == "SendResponse")
                            {
                                Message roomSync = new Message
                                {
                                    Source = _peerDiscovery.LocalPeerId,
                                    Type = MessageType.SyncRoomInformation,
                                    TargetPeerId = [peer.Id],
                                    Content = $"{JsonSerializer.Serialize(_rooms)};NoResponse"
                                }; 
                                _messageQueue.EnqueueOutgoing(roomSync); 
                            }
                            Rooms otherRooms = JsonSerializer.Deserialize<Rooms>(parts[0]); 
                            _rooms.Merge(otherRooms); 
                            break; 
                        }

                        case MessageType.Text: 
                            msg.Content = Encoding.UTF8.GetString(peer.Aes.Decrypt(msg.EncryptedContent)); 
                            _consoleUI.DisplayMessage(msg);
                            if(msg.RoomId != -1)
                            {
                                ForwardMessage(msg);
                                _history.SaveMessage(msg);
                            }
                            break; 
                        case MessageType.CreateRoom:
                        {
                            msg.Content = Encoding.UTF8.GetString(peer.Aes.Decrypt(msg.EncryptedContent));
                            int roomnumber = int.Parse(msg.Content); 
                            _rooms.CreateRoom(roomnumber); 
                            _consoleUI.DisplaySystem($"{msg.Source} created room {roomnumber}"); 
                            ForwardMessage(msg); 
                            break;
                        }
                        case MessageType.JoinRoom: 
                        {
                            msg.Content = Encoding.UTF8.GetString(peer.Aes.Decrypt(msg.EncryptedContent));
                            msg.EncryptedContent = []; 
                            msg.Signature = [];
                            int roomnumber = int.Parse(msg.Content); 

                            if(_rooms.ContainsPeer(roomnumber, _peerDiscovery.LocalPeerId))
                                _consoleUI.DisplaySystem($"{msg.Source} joined room {roomnumber}");
                            _rooms.AddPeer(roomnumber, msg.Source); 
                            ForwardMessage(msg); 
                            break; 
                        }
                        case MessageType.LeaveRoom: 
                        {
                            msg.Content = Encoding.UTF8.GetString(peer.Aes.Decrypt(msg.EncryptedContent));
                            msg.EncryptedContent = []; 
                            msg.Signature = [];
                            int roomnumber = int.Parse(msg.Content); 

                            if(_rooms.ContainsPeer(roomnumber, _peerDiscovery.LocalPeerId))
                                _consoleUI.DisplaySystem($"{msg.Source} left room {roomnumber}");
                            _rooms.RemovePeer(roomnumber, msg.Source); 
                            ForwardMessage(msg); 
                            break; 
                        }
                    }
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
                    Message msg = _messageQueue.DequeueOutgoing(_cancellationTokenSource.Token);
                    msg.Sender = _peerDiscovery.LocalPeerId;

                    if(msg.TargetPeerId == null)
                    {
                        msg.TargetPeerId = _peerDiscovery.GetConnectedPeerIDS().ToArray(); 
                    }
                    msg.SeenBy = [.. msg.SeenBy, .. msg.TargetPeerId, _peerDiscovery.LocalPeerId]; 
                    for(int i = 0; i < msg.TargetPeerId.Length; i++)
                    {
                        if(msg.TargetPeerId[i] == _peerDiscovery.LocalPeerId) continue; 
                        var peer = _peerDiscovery.GetPeer(msg.TargetPeerId[i]);
                        Message cpy = new Message(msg);
                        if(msg.Content != string.Empty)
                        {
                            cpy.EncryptedContent = peer.Aes.Encrypt(msg.Content);
                            cpy.Signature = _signer.SignData(cpy.EncryptedContent);
                        }
                        cpy.Content = "";
                        _ = _client.SendAsync(peer, cpy); 
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
                            Sender = _peerDiscovery.LocalPeerId,
                            Type = MessageType.Text,
                            Content = parsed_input.Message
                        }; 
                        _messageQueue.EnqueueOutgoing(msg); 
                        continue; 
                    }

                    switch (parsed_input.CommandType)
                    {
                        case CommandType.Connect:
                        {
                            Peer? peer = null; 
                            if(parsed_input.Args.Length == 1)
                            {
                                string peerId = parsed_input.Args[0]; 
                                peer = _peerDiscovery.GetPeer(peerId); 
                            }
                            else if (parsed_input.Args.Length > 1)
                            {
                                IPAddress iPAddress = IPAddress.Parse(parsed_input.Args[0]);
                                IPEndPoint endPoint = 
                                    new IPEndPoint(iPAddress, int.Parse(parsed_input.Args[1])); 
                                peer = _peerDiscovery.GetPeer(endPoint); 
                            }
                            if(peer == null)
                            {
                                _consoleUI.DisplaySystem("No such peer found.");
                                break; 
                            }
                            _ =  _client!.ConnectAsync(peer);
                            break;
                        }
                        case CommandType.Listen:
                        if (!_server.IsListening) 
                        {
                            try
                            {
                                int port = int.Parse(parsed_input.Args[0]);
                                _server.Start(port);
                                _peerDiscovery.Start(port);
                            }
                            catch(Exception e)
                            {
                                _consoleUI.DisplaySystem($"Invalid format {parsed_input.Args[0]}. Port should be a single integer 0-65535"); 
                            }
                        } 
                        else 
                        {
                            _consoleUI.DisplaySystem("Server is already listening");
                        }
                        break;
                        
                        case CommandType.ListPeers:
                        foreach(var peer in _peerDiscovery.GetKnownPeers())
                        {
                            Console.WriteLine(peer); 
                        }
                        break;
                        
                        case CommandType.History:
                            if (_currentRoom == -1)
                            {
                                Console.WriteLine("Please join a room to view history.");
                                break;
                            }

                            var history = _history.GetHistory().Where(m => m.RoomId == _currentRoom).OrderBy(m => m.Timestamp).TakeLast(100);

                            Console.WriteLine($"Room {_currentRoom} History:");

                            foreach (var msg in history)
                            {
                                Console.WriteLine(msg.ToString());
                            }
                            break;

                        case CommandType.Quit:
                            running = false;
                            break;

                        case CommandType.CreateRoom:
                        {
                            string arg = parsed_input.Args[0]; 
                            if(arg[0] == '#')
                                arg = arg[1..];
                            int roomID = int.Parse(arg);
                            Message msg = new Message
                            {
                                Source = _peerDiscovery.LocalPeerId,
                                Type = MessageType.CreateRoom,
                                Content = $"{roomID}"
                            };
                            _rooms.CreateRoom(roomID); 
                            _messageQueue.EnqueueOutgoing(msg);
                            break;
                        }

                        case CommandType.JoinRoom:
                        {
                            string arg = parsed_input.Args[0]; 
                            if(arg[0] == '#')
                                arg = arg[1..];
                            int roomID = int.Parse(arg);
                            Message msg = new Message
                            {
                                Source = _peerDiscovery.LocalPeerId, 
                                Type = MessageType.JoinRoom,
                                Content = $"{roomID}"
                            };
                            _rooms.AddPeer(roomID, _peerDiscovery.LocalPeerId);
                            _messageQueue.EnqueueOutgoing(msg); 
                            _currentRoom = roomID;
                            _consoleUI.DisplaySystem($"Joined room {roomID}");
                            break;
                        }
                        case CommandType.LeaveRoom:
                        {
                            string arg = parsed_input.Args[0]; 
                            if(arg[0] == '#')
                                arg = arg[1..];
                            int roomID = int.Parse(arg);
                            Message msg = new Message
                            {
                                Source = _peerDiscovery.LocalPeerId, 
                                Type = MessageType.LeaveRoom,
                                Content = $"{roomID}"
                            };
                            _rooms.RemovePeer(roomID, _peerDiscovery.LocalPeerId); 
                            _messageQueue.EnqueueOutgoing(msg); 
                            if (_currentRoom == roomID)
                                _currentRoom = -1;
                            _consoleUI.DisplaySystem($"Left room {roomID}");
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

                        case CommandType.Message:
                        {
                            string dest = parsed_input.Args[0];
                            string content = string.Join(" ", parsed_input.Args.Skip(1));
                            Message msg; 
                            if(dest[0] == '@')
                            {
                                msg = new Message 
                                {
                                    Source = _peerDiscovery.LocalPeerId,
                                    TargetPeerId = [ dest[1..] ],
                                    Type = MessageType.Text,
                                    Content = content,
                                }; 

                                _consoleUI.DisplayMessage(msg); 
                                _history.SaveMessage(msg);
                                _messageQueue.EnqueueOutgoing(msg);
                            }
                            else if(dest[0] == '#')
                            {
                                int roomnumber = int.Parse(dest[1..]);
                                if(!_rooms.RoomExists(roomnumber))
                                {
                                    _consoleUI.DisplaySystem($"No such room {roomnumber}"); 
                                    break; 
                                }
                                msg = new Message
                                {
                                    Source = _peerDiscovery.LocalPeerId,
                                    TargetPeerId = [.. _rooms.GetRoom(roomnumber)
                                        .Where(peer => peer != _peerDiscovery.LocalPeerId && _peerDiscovery.GetPeer(peer).IsConnected)], 
                                    RoomId = roomnumber, 
                                    Type = MessageType.Text,
                                    Content = content,
                                };
                                _consoleUI.DisplayMessage(msg); 
                                _history.SaveMessage(msg);
                                _messageQueue.EnqueueOutgoing(msg); 
                            }
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

        incomingThread.Join();
        outgoingThread.Join();

        Console.WriteLine("Goodbye!");
    }
    
}

