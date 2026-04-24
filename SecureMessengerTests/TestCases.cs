using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using SecureMessenger.Core;
using SecureMessenger.Network;
using Xunit;

namespace SecureMessenger.Tests;

public class ResilienceTests
{
    [Fact]
    public async Task KillPeerProcess_DetectedAsFailed()
    {
        var monitor = new HeartbeatMonitor();

        string failedPeerId = "";
        var failedEvent = new ManualResetEventSlim(false);

        monitor.OnConnectionFailed += peerId =>
        {
            failedPeerId = peerId;
            failedEvent.Set();
        };

        monitor.Start();
        monitor.StartMonitoring("peer-a");

        ForceHeartbeatAge(monitor, "peer-a", TimeSpan.FromSeconds(20));

        Assert.True(failedEvent.Wait(3000), "Peer failure was not detected.");
        Assert.Equal("peer-a", failedPeerId);

        monitor.Stop();
    }

    [Fact]
    public async Task NetworkInterruption_ReconnectionAttempted()
    {
        var peerDiscovery = new PeerDiscovery();
        var clientHandler = new TcpClientHandler(peerDiscovery);
        var reconnectPolicy = new ReconnectionPolicy(clientHandler);

        var peer = new Peer
        {
            Id = "peer-a",
            Address = IPAddress.Loopback,
            Port = GetFreeTcpPort(),
            IsConnected = false
        };

        int attempts = 0;
        var attemptEvent = new ManualResetEventSlim(false);

        reconnectPolicy.OnReconnectAttempt += (_, attempt) =>
        {
            attempts = attempt;
            attemptEvent.Set();
        };

        bool result = await reconnectPolicy.TryReconnect(peer);

        Assert.False(result);
        Assert.True(attemptEvent.IsSet, "Reconnect attempt event was not fired.");
        Assert.True(attempts > 0, "No reconnect attempts were recorded.");
        Assert.Equal(5, reconnectPolicy.GetAttemptCount(peer.Id));
    }

    [Fact]
    public async Task PeerRejoins_ConnectionRestored()
    {
        int port = GetFreeTcpPort();

        using var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();

        var acceptTask = Task.Run(async () =>
        {
            using TcpClient accepted = await listener.AcceptTcpClientAsync();
            await Task.Delay(500);
        });

        var peerDiscovery = new PeerDiscovery();
        var clientHandler = new TcpClientHandler(peerDiscovery);
        var reconnectPolicy = new ReconnectionPolicy(clientHandler);

        var peer = new Peer
        {
            Id = "peer-a",
            Address = IPAddress.Loopback,
            Port = port,
            IsConnected = false
        };

        bool successEventFired = false;

        reconnectPolicy.OnReconnectSuccess += peerId =>
        {
            if (peerId == peer.Id)
                successEventFired = true;
        };

        bool result = await reconnectPolicy.TryReconnect(peer);

        Assert.True(result, "Reconnect should succeed when peer is listening again.");
        Assert.True(peer.IsConnected, "Peer should be marked connected after reconnect.");
        Assert.True(successEventFired, "Reconnect success event was not fired.");
        Assert.Equal(0, reconnectPolicy.GetAttemptCount(peer.Id));

        clientHandler.Disconnect(peer);
        listener.Stop();
    }

    private static void ForceHeartbeatAge(
        HeartbeatMonitor monitor,
        string peerId,
        TimeSpan age)
    {
        FieldInfo? field = typeof(HeartbeatMonitor).GetField(
            "_lastHeartbeat",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(field);

        var dictionary = Assert.IsType<ConcurrentDictionary<string, DateTime>>(
            field!.GetValue(monitor));

        dictionary[peerId] = DateTime.UtcNow - age;
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        listener.Stop();
        return port;
    }
}

public class PeerToPeerTests
{
    [Fact]
    public void ThreePlusPeersCanFormMesh_AllPeersConnected()
    {
        var discovery = new PeerDiscovery();

        discovery.UpdatePeer("peer1", new Peer { Id = "peer1", IsConnected = true });
        discovery.UpdatePeer("peer2", new Peer { Id = "peer2", IsConnected = true });
        discovery.UpdatePeer("peer3", new Peer { Id = "peer3", IsConnected = true });

        var connected = discovery.GetConnectedPeerIDS().ToList();

        Assert.Equal(3, connected.Count);
        Assert.Contains("peer1", connected);
        Assert.Contains("peer2", connected);
        Assert.Contains("peer3", connected);
    }

    [Fact]
    public void PeerDiscoveryWorks_NewPeerFoundAutomatically()
    {
        var discovery = new PeerDiscovery();

        Peer? foundPeer = null;

        discovery.OnPeerDiscovered += peer => foundPeer = peer;

        InvokeDiscovery(discovery, "PEER:testpeer:6000", IPAddress.Loopback);

        Assert.NotNull(foundPeer);
        Assert.Equal("testpeer", foundPeer!.Id);
        Assert.Equal(6000, foundPeer.Port);
        Assert.NotNull(discovery.GetPeer("testpeer"));
    }

    private static void StopTimeoutLoopOnly(PeerDiscovery discovery)
    {
        FieldInfo? tokenField = typeof(PeerDiscovery).GetField(
            "_cancellationTokenSource",
            BindingFlags.NonPublic | BindingFlags.Instance);

        var cts = tokenField!.GetValue(discovery) as CancellationTokenSource;
        cts?.Cancel();
    }
    [Fact]
    public async Task PeerLeavingDetected_RemovedFromPeerList()
    {
        var discovery = new PeerDiscovery();

        bool lostRaised = false;

        discovery.OnPeerLost += peer =>
        {
            if (peer.Id == "peer1")
                lostRaised = true;
        };

        discovery.UpdatePeer("peer1", new Peer
        {
            Id = "peer1",
            Address = IPAddress.Loopback,
            Port = 5000,
            LastSeen = DateTime.Now.AddSeconds(-40)
        });

        StartTimeoutLoop(discovery);

        await Task.Delay(6000);

        Assert.True(lostRaised);
        Assert.Null(discovery.GetPeer("peer1"));

        StopTimeoutLoopOnly(discovery);
    }

    [Fact]
    public async Task ReconnectionAfterFailure_ConnectionRestored()
    {
        int port = GetFreeTcpPort();

        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, port);
        listener.Start();

        _ = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            await Task.Delay(500);
        });

        var peerDiscovery = new PeerDiscovery();
        var clientHandler = new TcpClientHandler(peerDiscovery);
        var reconnect = new ReconnectionPolicy(clientHandler);

        var peer = new Peer
        {
            Id = "peer1",
            Address = IPAddress.Loopback,
            Port = port
        };

        bool success = await reconnect.TryReconnect(peer);

        Assert.True(success);
        Assert.True(peer.IsConnected);

        clientHandler.Disconnect(peer);
        listener.Stop();
    }

    private static void InvokeDiscovery(
        PeerDiscovery discovery,
        string message,
        IPAddress ip)
    {
        MethodInfo? method = typeof(PeerDiscovery).GetMethod(
            "ProcessDiscoveryMessage",
            BindingFlags.NonPublic | BindingFlags.Instance);

        method!.Invoke(discovery, new object[] { message, ip });
    }

    private static void StartTimeoutLoop(PeerDiscovery discovery)
    {
        FieldInfo? tokenField = typeof(PeerDiscovery).GetField(
            "_cancellationTokenSource",
            BindingFlags.NonPublic | BindingFlags.Instance);

        tokenField!.SetValue(discovery, new CancellationTokenSource());

        MethodInfo? method = typeof(PeerDiscovery).GetMethod(
            "TimeoutCheckLoop",
            BindingFlags.NonPublic | BindingFlags.Instance);

        _ = Task.Run(async () =>
        {
            await (Task)method!.Invoke(discovery, null)!;
        });
    }

    private static int GetFreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        int port =
            ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;

        listener.Stop();
        return port;
    }
}