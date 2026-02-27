using SecureMessenger.Network;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

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
        await sender.SendAsync(senderPeerId!, "hello world");
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