// Sean Gaines
// CSCI 251 - Secure Distributed Messenger

using System.Collections.Concurrent;

namespace SecureMessenger.Core;

/// <summary>
/// Thread-safe message queue for incoming/outgoing messages.
///
/// This class implements the Producer/Consumer pattern:
/// - Producers add messages to the queue (network threads, UI thread)
/// - Consumers take messages from the queue (processing threads, send threads)
///
/// Thread Safety Options:
/// 1. BlockingCollection<T> - recommended, handles blocking and thread safety
/// 2. ConcurrentQueue<T> with manual synchronization
/// 3. Queue<T> with explicit locking
///
/// The blocking behavior is important:
/// - Take() should block when the queue is empty
/// - This allows consumer threads to wait efficiently without busy-waiting
/// </summary>
public class MessageQueue
{
    BlockingCollection<Message> _incomingQueue = new BlockingCollection<Message>(); 
    BlockingCollection<Message> _outgoingQueue = new BlockingCollection<Message>(); 

    /// <summary>
    /// Enqueue an incoming message (received from network).
    /// </summary>
    public void EnqueueIncoming(Message message)
    {
        _incomingQueue.Add(message); 
    }

    /// <summary>
    /// Dequeue an incoming message for processing.
    /// This method should BLOCK if the queue is empty.
    /// </summary>
    public Message DequeueIncoming(CancellationToken cancellationToken = default)
    {
        return _incomingQueue.Take(cancellationToken);
    }

    /// <summary>
    /// Try to dequeue an incoming message without blocking.
    /// </summary>
    public bool TryDequeueIncoming(out Message? message)
    {
        return _incomingQueue.TryTake(out message);
    }

    /// <summary>
    /// Enqueue an outgoing message (to be sent to network).
    /// </summary>
    public void EnqueueOutgoing(Message message)
    {
        _outgoingQueue.Add(message);
    }

    /// <summary>
    /// Dequeue an outgoing message for sending.
    /// This method should BLOCK if the queue is empty.
    /// </summary>
    public Message DequeueOutgoing(CancellationToken cancellationToken = default)
    {
        return _outgoingQueue.Take(cancellationToken);
    }

    /// <summary>
    /// Get the count of incoming messages waiting to be processed.
    /// </summary>
    public int IncomingCount => _incomingQueue.Count; 

    /// <summary>
    /// Get the count of outgoing messages waiting to be sent.
    /// </summary>
    public int OutgoingCount => _outgoingQueue.Count;

    /// <summary>
    /// Signal that no more messages will be added.
    /// Call this during shutdown to unblock waiting consumers.
    /// </summary>
    public void CompleteAdding()
    {
        _outgoingQueue.CompleteAdding(); 
        _incomingQueue.CompleteAdding(); 
    }
}
