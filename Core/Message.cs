// Sean Gaines, Alia Ulanbek Kyzy
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
    PeerDiscovery   // Sprint 3: Peer announcement
}

/// <summary>
/// Represents a message in the system
/// </summary>
public class Message
{
    private static readonly UTF8Encoding _utf8 = new(); 
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Sender { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    // Sprint 2: Security fields
    public byte[]? Signature { get; set; }
    public byte[]? EncryptedContent { get; set; }
    public byte[]? PublicKey { get; set; }

    // Sprint 3: Target peer for directed messages
    public string? TargetPeerId { get; set; }

    public override string ToString()
    {
        return $"[{Timestamp:HH:mm:ss}] {Sender}: {Content}";
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
        int length = _utf8.GetBytes(
            message, 0, message.Length, byteMessage, 4
            ); 
        byte[] lenBytes = BitConverter.GetBytes(length); 
        for(int i = 0; i < 4; i++)
            byteMessage[i] = lenBytes[i]; 

        return byteMessage; 
    }
}
