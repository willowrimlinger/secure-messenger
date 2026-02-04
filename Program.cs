// Sean Gaines
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
///    - Dequeues from outgoing message queue
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

    // [X] TODO: Declare your components as fields if needed for access across methods
    // Examples:
    // [X] private static MessageQueue? _messageQueue;
    // [X] private static TcpServer? _server;
    // [X] private static TcpClientHandler? _client;
    // [X] private static ConsoleUI? _consoleUI;
    // [X] private static CancellationTokenSource? _cancellationTokenSource;

    private static MessageQueue? _messageQueue;
    private static TcpServer? _server;
    private static TcpClientHandler? _client;
    private static ConsoleUI? _consoleUI;
    private static CancellationTokenSource? _cancellationTokenSource;

    static async Task Main(string[] args)
    {
        Console.WriteLine("Secure Distributed Messenger");
        Console.WriteLine("============================");

        // [X] TODO: Initialize components
        // [X] 1. Create CancellationTokenSource for shutdown signaling
        // [X] 2. Create MessageQueue for thread communication
        // [X] 3. Create ConsoleUI for user interface
        // [X] 4. Create TcpServer for incoming connections
        // [X] 5. Create TcpClientHandler for outgoing connections

        _messageQueue = new MessageQueue();
        _consoleUI = new ConsoleUI(_messageQueue);
        _server = new TcpServer();
        _client = new TcpClientHandler();

        // TODO: Subscribe to events
        // 1. TcpServer.OnPeerConnected - handle new incoming connections
        _tcpServer.OnPeerConnected += (peer) => {};
        // 2. TcpServer.OnMessageReceived - handle received messages
        _tcpServer.OnMessageReceived += (peer, message) => {}; 
        // 3. TcpServer.OnPeerDisconnected - handle disconnections
        _tcpServer.OnPeerConnected += (peer) => {}; 

        // 4. TcpClientHandler events (same pattern)

        _server.OnPeerConnected += (peer) => 
        { 
            Console.WriteLine($"Client connected: {peer}");
        };

        _server.OnMessageReceived += (peer, msg) => 
        {
            Console.WriteLine($"[{peer}]: {msg}");
        };

        _client.OnConnected += (peer) =>  
        {
            Console.WriteLine($"Connected to Server: {peer}");
        };

        _client.OnMessageReceived += (peer, msg) => 
        {
            Console.WriteLine($"Server: {msg}");
        };

        // TODO: Start background threads
        // 1. Start a thread/task for processing incoming messages
        // 2. Start a thread/task for sending outgoing messages
        // Note: TcpServer.Start() will create its own listen thread



        Console.WriteLine("Type /help for available commands");
        Console.WriteLine();

        // Main loop - handle user input
        bool running = true;
        while (running)
        {
            // TODO: Implement the main input loop
            // [X] 1. Read a line from the console
            // [X] 2. Skip empty input
            // [X] 3. Parse the input using ConsoleUI.ParseCommand()
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
                    consoleUI.ShowHelp();
                    break;
                default:
                    // TODO: Handle other commands and messages
                    break;
            }

            var parsed_input = _consoleUI.ParseCommand(input);
            if (!parsed_input.IsCommand) {
                Console.WriteLine("TODO: not a command: Send as a message to peers")
            }


            switch (parsed_input.CommandType)
            {
                case CommandType.Quit:
                    Console.WriteLine("In switch")
                    running = false;
                    break;
                case "/help":
                    ShowHelp();
                    break;
                default:

                    var parsed_input = _consoleUI.ParseCommand(input);
                    if (!parsed_input.IsCommand) {
                        Console.WriteLine("TODO: not a command: Send as a message to peers");
                    }

                    switch (parsed_input.CommandType)
                    {
                        case CommandType.Connect:
                            _client.ConnectAsync(parsed_input.Args[0], parsed_input.Args[1]);
                            break;
                        case CommandType.Listen:
                            _server.Start(parsed_input.Args[0]);
                            break;
                        case CommandType.ListPeers:
                            break;
                        case CommandType.History:
                            break;
                        case CommandType.Quit:
                            running = false;
                            break;
                        case CommandType.Unknown:
                            Console.WriteLine($"\n{parsed_input.Message}. Please try again!\n");
                            break;
                        default:
                            break;
                    }
                    break;
            }
        }

        // TODO: Implement graceful shutdown
        // 1. Cancel the CancellationTokenSource
        // 2. Stop the TcpServer
        // 3. Disconnect all clients
        // 4. Complete the MessageQueue
        // 5. Wait for background threads to finish

        Console.WriteLine("Goodbye!");
    }

    // TODO: Add helper methods as needed
    // Examples:
    // - ProcessIncomingMessages() - background task to process received messages
    // - SendOutgoingMessages() - background task to send queued messages
    // - HandlePeerConnected(Peer peer) - event handler for new connections
    // - HandleMessageReceived(Peer peer, Message message) - event handler for messages
}
