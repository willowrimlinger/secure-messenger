// Sean Gaines, Alia Ulanbek Kyzy, Mikey Reizentein
// CSCI 251 - Secure Distributed Messenger

namespace SecureMessenger.Core;
using System.Text.Json; 
using System.Text;

public enum MessageType
{
    Text,           // Regular chat message
    KeyExchange,    // Sprint 2: Public key exchange
    SessionKey,     // Sprint 2: Encrypted session key
    Heartbeat,      // Sprint 3: Connection health check
    PeerDiscovery,   // Sprint 3: Peer announcement
    CreateRoom,
    JoinRoomRequest,

    LeaveRoom,
    GetRooms
    
}
/// <summary>
/// Represents a message in the system
/// </summary>
public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Sender { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Message type - determines how the message is processed.
    /// Sprint 1: Always MessageType.Text
    /// Sprint 2-3: Use other types for protocol messages
    /// </summary>
    public MessageType Type { get; set; } = MessageType.Text;

    // Sprint 2: Security fields
    public byte[]? Signature { get; set; }
    public byte[]? EncryptedContent { get; set; }
    public byte[]? PublicKey { get; set; }
    public int RoomId { get; set; } = -1; 

    // Sprint 3: Target peer for directed messages
    public string? TargetPeerId { get; set; }

    public override string ToString()
    {
        return $"[{Timestamp:HH:mm:ss}] {Sender}: {Content}";
    }

    public void printLong()
    {
        Console.WriteLine(JsonSerializer.Serialize(this)); 
    }

    public Message() {}

    public Message(Message o)
    {
        Sender = o.Sender; 
        Content = o.Content; 
        Timestamp = o.Timestamp; 
        Type = o.Type; 
        Signature = o.Signature; 
        EncryptedContent = o.EncryptedContent; 
        PublicKey = o.PublicKey; 
        RoomId = o.RoomId;
    }

    /// <summary> 
    /// Returns the byte array representation of the message. 
    /// Byte array is a 4-byte long length prefix, followed 
    /// by a json representation of the message class 
    /// </summary>
    public byte[] ToByteArray()
    {
        string message = JsonSerializer.Serialize(this); 
        byte[] byteMessage = new byte[message.Length + 4]; 
        int length = Encoding.UTF8.GetBytes(
            message, 0, message.Length, byteMessage, 4
            ); 
        byte[] lenBytes = BitConverter.GetBytes(length); 
        for(int i = 0; i < 4; i++)
            byteMessage[i] = lenBytes[i]; 

        return byteMessage; 
    }
}
