using SecureMessenger.Network;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using SecureMessenger.Core;

namespace SecureMessenger.Tests;

public class ConnectionTests
{
    [Fact]
    /// Test Case: Two instances can connect to each other
    public async Task TwoInstancesCanConnect()
    {
        /// Get anyfree TCP port to avoid conflicts with other tests or applications
        int port = TestHelpers.GetFreeTcpPort();
        /// Create a server and a client instance
        var server = new TcpServer();
        /// Avoid null reference exceptions
        server.OnPeerConnected += _ => { };
        server.OnMessageReceived += (_, __) => { };
        var client = new TcpClientHandler();
        /// Create events to wait for connection notifications
        var serverConnected = new ManualResetEventSlim(false);

        server.OnPeerConnected += _ => serverConnected.Set();
        var clientConnected = new ManualResetEventSlim(false);
        server.OnPeerConnected += _ => serverConnected.Set();
        client.OnConnected += _ => clientConnected.Set();
        /// Starting Server
        server.Start(port);
        /// Connect client to server and then check to see if it was successful
        bool ok = await client.ConnectAsync("127.0.0.1", port);
        Assert.True(ok);
        /// Wait for both sides to report the connection
        Assert.True(TestHelpers.Wait(clientConnected), "Client did not report connection.");
        Assert.True(TestHelpers.Wait(serverConnected), "Server did not report incoming connection.");
        /// Clean up
        client.DisconnectAll();
        server.Stop();
    }
}

public class MessagingTests
{
    [Fact]
    /// Test Case: Messages sent by one instance are received by the other
    public async Task MessagesSentAndReceived()
    {
        /// Get anyfree TCP port to avoid conflicts with other tests or applications
        int port = TestHelpers.GetFreeTcpPort();
        /// Create a server and two client instances
        var server = new TcpServer();
        /// Avoid null reference exceptions
        server.OnPeerConnected += _ => { };
        server.OnMessageReceived += (_, __) => { };
        server.OnMessageReceived += async (peer, msg) => await server.BroadcastAsync(msg);
        var sender = new TcpClientHandler();
        var receiver = new TcpClientHandler();
        /// Variables to track the sender's peer ID and the content received by the receiver
        string? senderPeerId = null;
        string? receivedContent = null;
        /// Create an event to wait for the receiver to get the message
        var receiverGotMessage = new ManualResetEventSlim(false);
        // When server receives a message, broadcast it to everyone except the sender
        server.OnMessageReceived += async (peer, msg) => await server.BroadcastAsync(msg);
        /// When sender connects, save its peer ID for later use
        sender.OnConnected += peer => senderPeerId = peer.Id;
        /// When receiver gets a message, save the content and signal that we got it
        receiver.OnMessageReceived += (_, msg) =>
        {
            receivedContent = msg.Content;
            receiverGotMessage.Set();
        };
        /// Start the server and connect both sender and receiver to it
        server.Start(port);
        Assert.True(await sender.ConnectAsync("127.0.0.1", port));
        Assert.True(await receiver.ConnectAsync("127.0.0.1", port));
        /// Wait for the sender to report its connection and get its peer ID
        SpinWait.SpinUntil(() => senderPeerId != null, 3000);
        Assert.NotNull(senderPeerId);
        /// Send a message from the sender to the server, which will broadcast it to the receiver
        var outgoing = new Message
        {
            Id = Guid.NewGuid(),
            Sender = "test",
            Content = "hello world",
            Timestamp = DateTime.Now
        };
        await sender.SendAsync(senderPeerId!, outgoing);
        /// Wait for the receiver to get the message and then check that the content is correct
        Assert.True(TestHelpers.Wait(receiverGotMessage), "Receiver never got the broadcast message.");
        Assert.Equal("hello world", receivedContent);
        /// Clean up
        sender.DisconnectAll();
        receiver.DisconnectAll();
        server.Stop();
    }
}

public class DisconnectTests
{
    [Fact]
    /// Test Case: Disconnections are handled properly by both sides
    public async Task DisconnectionHandled()
    {
        /// Get anyfree TCP port to avoid conflicts with other tests or applications
        int port = TestHelpers.GetFreeTcpPort();
        /// Create a server and a client instance
        var server = new TcpServer();
        /// Avoid null reference exceptions
        server.OnPeerConnected += _ => { };
        server.OnMessageReceived += (_, __) => { };
        var client = new TcpClientHandler();
        /// Variable to track the client's peer ID
        string? clientPeerId = null;
        /// Create events to wait for disconnection notifications
        var serverSawDisconnect = new ManualResetEventSlim(false);
        var clientSawDisconnect = new ManualResetEventSlim(false);
        /// When client connects, save its peer ID for later use
        client.OnConnected += peer => clientPeerId = peer.Id;
        client.OnDisconnected += _ => clientSawDisconnect.Set();
        server.OnPeerDisconnected += _ => serverSawDisconnect.Set();
        /// Start the server and connect the client to it
        server.Start(port);
        try{
            /// Wait for the client to report its connection and get its peer ID
            Assert.True(await client.ConnectAsync("127.0.0.1", port));
            SpinWait.SpinUntil(() => clientPeerId != null, 3000);
            Assert.NotNull(clientPeerId);
            /// Disconnect the client and then check that both sides reported the disconnection
            client.Disconnect(clientPeerId!);
            /// Wait for both sides to report the disconnection
            Assert.True(TestHelpers.Wait(clientSawDisconnect), "Client did not raise OnDisconnected.");
            Assert.True(TestHelpers.Wait(serverSawDisconnect), "Server did not raise OnPeerDisconnected.");
        }
        finally
        {
            client.DisconnectAll();
            server.Stop();
        }
    }
}

public class LoadTests
{
    [Fact]
    public async Task ServerHandlesTwentyClientsConnectingAndBroadcast()
    {
        /// Get anyfree TCP port to avoid conflicts with other tests or applications
        int port = TestHelpers.GetFreeTcpPort();
        /// Create a server instance
        var server = new TcpServer();
        server.OnMessageReceived += (_, __) => { };
        /// Track how many clients have connected to the server
        int connectedCount = 0;
        var allConnected = new ManualResetEventSlim(false);
        /// When a client connects, increment the count and if we reach 20, signal that all are connected
        server.OnPeerConnected += _ =>
        {
            if (Interlocked.Increment(ref connectedCount) == 20)
                allConnected.Set();
        };

        /// When server receives any message, broadcast it to everyone.
        server.OnMessageReceived += async (_, msg) => await server.BroadcastAsync(msg);
        /// Start the server
        server.Start(port);
        /// Create 20 clients and connect them to the server,
        /// setting up events to track their connections and message receptions
        var clients = new List<TcpClientHandler>();
        var clientConnected = new ManualResetEventSlim[20];
        var clientGotBroadcast = new ManualResetEventSlim[20];

        /// Store peer ids so we can send from each client (if needed)
        var peerIds = new string?[20];

        /// Track how many clients received the broadcast
        int receivedCount = 0;
        /// Set up each client to connect and listen for messages, 
        /// incrementing the received count when they get the broadcast message
        try
        {
            for (int i = 0; i < 20; i++)
            {
                var client = new TcpClientHandler();
                clients.Add(client);

                clientConnected[i] = new ManualResetEventSlim(false);
                clientGotBroadcast[i] = new ManualResetEventSlim(false);

                int idx = i;

                client.OnConnected += peer =>
                {
                    peerIds[idx] = peer.Id;
                    clientConnected[idx].Set();
                };

                client.OnMessageReceived += (_, msg) =>
                {
                    if (msg.Content == "load-test")
                    {
                        Interlocked.Increment(ref receivedCount);
                        clientGotBroadcast[idx].Set();
                    }
                };
            }

            /// Connect all clients concurrently
            var connectTasks = clients.Select(c => c.ConnectAsync("127.0.0.1", port)).ToArray();
            bool[] results = await Task.WhenAll(connectTasks);
            Assert.All(results, Assert.True);

            /// Wait until each client reports connected
            for (int i = 0; i < 20; i++)
                Assert.True(TestHelpers.Wait(clientConnected[i]), $"Client {i} did not report connection.");

            /// Wait until server reports all 20 connections
            Assert.True(TestHelpers.Wait(allConnected), "Server did not observe 20 client connections.");

            /// Send one message from client 0 and expect broadcast to others
            var msgToSend = new Message
            {
                Id = Guid.NewGuid(),
                Sender = "load-test-sender",
                Content = "load-test",
                Timestamp = DateTime.Now
            };

            Assert.NotNull(peerIds[0]);
            await clients[0].SendAsync(peerIds[0]!, msgToSend);

            /// Wait for broadcasts; allow a little slack if your server excludes sender, etc.
            /// Expect at least 19 receivers if sender doesn't receive its own broadcast.
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 4000 && Volatile.Read(ref receivedCount) < 19)
                Thread.Sleep(10);

            Assert.True(receivedCount >= 19, $"Expected >= 19 clients to receive broadcast, got {receivedCount}.");
        }
        /// Clean up
        finally
        {
            foreach (var c in clients)
                c.DisconnectAll();

            server.Stop();
        }
    }
}