// [Michael Reizenstein]
// CSCI 251 - Secure Distributed Messenger

using System.Collections.Concurrent;
using System.Diagnostics;
using SecureMessenger.Core;

namespace SecureMessenger.Network;

/// <summary>
/// Sprint 3: Heartbeat monitoring for connection health.
/// Detects failed connections by tracking when heartbeats were last received.
///
/// Configuration:
/// - Heartbeat interval: 5 seconds (how often to send heartbeats)
/// - Timeout: 15 seconds (how long without heartbeat before considered failed)
/// </summary>
public class HeartbeatMonitor
{
    private readonly ConcurrentDictionary<string, DateTime> _lastHeartbeat = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

    public event Action<string>? OnConnectionFailed;
    public event Action<string>? OnHeartbeatReceived;
    /// <summary>
    /// The interval at which heartbeats should be sent.
    /// Use this when implementing heartbeat sending in your main program.
    /// </summary>
    public TimeSpan HeartbeatInterval => _heartbeatInterval;

    private Task? _monitorTask;
    /// <summary>
    /// Start the heartbeat monitoring loop.
    ///
    /// TODO: Implement the following:
    /// 1. Create a new CancellationTokenSource
    /// 2. Start MonitorLoop as a background task
    /// </summary>
    public void Start()
    {
        if (_monitorTask != null && !_monitorTask.IsCompleted)
        {
            return; // Already running
        }
        _cancellationTokenSource = new CancellationTokenSource();
        _monitorTask = Task.Run(MonitorLoop);
    }

    /// <summary>
    /// Start monitoring a specific peer.
    /// Call this when a peer connects.
    ///
    /// TODO: Implement the following:
    /// 1. Record current time as the peer's last heartbeat
    /// </summary>
    public void StartMonitoring(string peerId)
    {
        _lastHeartbeat[peerId] = DateTime.UtcNow;
    }

    /// <summary>
    /// Record that a heartbeat was received from a peer.
    /// Call this when you receive a heartbeat message.
    ///
    /// TODO: Implement the following:
    /// 1. Update the peer's last heartbeat time to now
    /// 2. Invoke OnHeartbeatReceived event
    /// </summary>
    public void RecordHeartbeat(string peerId)
    {
        _lastHeartbeat[peerId] = DateTime.UtcNow;
        OnHeartbeatReceived?.Invoke(peerId);
    }

    /// <summary>
    /// Stop monitoring a peer.
    /// Call this when a peer disconnects normally.
    ///
    /// TODO: Implement the following:
    /// 1. Remove the peer from _lastHeartbeat dictionary
    /// </summary>
    public void StopMonitoring(string peerId)
    {
        _lastHeartbeat.TryRemove(peerId, out _);
    }

    /// <summary>
    /// Main monitoring loop - checks for timed out connections.
    ///
    /// TODO: Implement the following:
    /// 1. Loop while cancellation not requested:
    ///    a. Get current time
    ///    b. Iterate through all entries in _lastHeartbeat
    ///    c. Calculate elapsed time since last heartbeat
    ///    d. If elapsed > _timeout:
    ///       - Log timeout message
    ///       - Invoke OnConnectionFailed event
    ///       - Call StopMonitoring for that peer
    ///    e. Delay 1 second between checks
    /// </summary>
    private async Task MonitorLoop()
    {
        if (_cancellationTokenSource == null)
        {
            return; // Not started
        }
        CancellationToken token = _cancellationTokenSource.Token;
        while (!token.IsCancellationRequested)
        {
            DateTime now = DateTime.UtcNow;

            foreach (var entry in _lastHeartbeat)
            {
                string peerId = entry.Key;
                DateTime lastSeen = entry.Value;
                TimeSpan elapsed = now - lastSeen;

                if (elapsed > _timeout)
                {
                    Console.WriteLine($"Peer {peerId} timed out.");
                    OnConnectionFailed?.Invoke(peerId);
                    StopMonitoring(peerId);
                }
            }

            try
            {
                await Task.Delay(1000, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Check if a peer is still alive (received heartbeat recently).
    ///
    /// TODO: Implement the following:
    /// 1. Try to get the peer's last heartbeat time
    /// 2. If found, return true if (now - lastSeen) < _timeout
    /// 3. If not found, return false
    /// </summary>
    public bool IsAlive(string peerId)
    {
        if (_lastHeartbeat.TryGetValue(peerId, out DateTime lastSeen))
        {
            return (DateTime.UtcNow - lastSeen) < _timeout;
        }

        return false;
    }

    /// <summary>
    /// Stop monitoring all peers.
    ///
    /// TODO: Implement the following:
    /// 1. Cancel the cancellation token
    /// </summary>
    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
    }
}
