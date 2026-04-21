using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SecureMessenger.Core;
using SecureMessenger.Network;
using SecureMessenger.Security;
using Xunit;

namespace SecureMessenger.Tests;

// public class TestCasesSprint2
// {
//     [Fact]
//     public async Task MessagesAreEncryptedOnWire()
//     {
//         // We set up a TCP server and client, perform a simple key exchange to establish a shared AES key, and then send an encrypted message from the client to the server. 
//         // The test checks that the server receives the encrypted message, that the plaintext is not present in the serialized message, and that the server can successfully 
//         // decrypt the message to retrieve the original plaintext.
        
//         // Set up
//         int port = GetFreeTcpPort();
//         var server = new TcpServer();
//         var client = new TcpClientHandler();
//         server.OnPeerConnected += _ => { };
//         server.OnMessageReceived += (_, __) => { };
//         string? clientPeerId = null;
//         Peer? serverPeer = null;
//         Message? received = null;
//         var receivedEvent = new ManualResetEventSlim(false);

//         // Set up event handlers to capture the client peer ID, server peer, and received message
//         client.OnConnected += peer => clientPeerId = peer.Id;
//         server.OnPeerConnected += peer => serverPeer = peer;
//         server.OnMessageReceived += (_, msg) =>
//         {
//             received = msg;
//             receivedEvent.Set();
//         };
//         try
//         {
//             // Start the server and connect the client
//             server.Start(port);
//             // Perform a simple key exchange to establish a shared AES key
//             Assert.True(await client.ConnectAsync("127.0.0.1", port));
//             Assert.True(SpinWait.SpinUntil(() => clientPeerId != null && serverPeer != null, 3000));
//             // Generate a shared AES key and assign it to both the client and server peers
//             byte[] sharedKey = AesEncryption.GenerateKey();
//             var clientAes = new AesEncryption(sharedKey);
//             serverPeer!.AesKey = sharedKey;
//             serverPeer.Aes = new AesEncryption(sharedKey);
//             // Encrypt a plaintext message using the client's AES instance and send it to the server
//             const string plaintext = "Sprint 2 secret over TCP";
//             byte[] ciphertext = clientAes.Encrypt(plaintext);
//             // Create a message with the encrypted content and send it to the server
//             var outgoing = new Message
//             {
//                 Sender = "client-a",
//                 Type = MessageType.Text,
//                 Content = string.Empty,
//                 EncryptedContent = ciphertext
//             };
//             // Send the message to the server
//             await client.SendAsync(clientPeerId!, outgoing);
//             // Wait for the server to receive the message and verify that it is encrypted on the wire
//             Assert.True(Wait(receivedEvent), "Server did not receive the encrypted message.");
//             Assert.NotNull(received);
//             Assert.Equal(string.Empty, received!.Content);
//             Assert.NotNull(received.EncryptedContent);
//             // Verify that the plaintext is not present in the serialized message
//             string serialized = Encoding.UTF8.GetString(outgoing.ToByteArray(), 4, outgoing.ToByteArray().Length - 4);
//             Assert.DoesNotContain(plaintext, serialized);
//             // Verify that the server can successfully decrypt the message to retrieve the original plaintext
//             string decrypted = Encoding.UTF8.GetString(serverPeer.Aes!.Decrypt(received.EncryptedContent!));
//             Assert.Equal(plaintext, decrypted);
//         }
//         finally
//         {
//             DisconnectAll(client);
//             server.Stop();
//         }
//     }

//     [Fact]
//     public async Task KeyExchangeCompletes()
//     {   
//         // set up
//         int port = GetFreeTcpPort();
//         var server = new TcpServer();
//         var client = new TcpClientHandler();
//         var serverRsa = new RsaEncryption();
//         byte[] serverPublicKey = serverRsa.ExportPublicKey();
//         var clientRsa = new RsaEncryption();
//         var clientSigner = new MessageSigner(clientRsa.GetRSA());
//         string? serverPeerId = null;
//         string? clientPeerId = null;
//         byte[]? serverSessionKey = null;
//         byte[]? clientSessionKey = null;
//         var handshakeDone = new ManualResetEventSlim(false);

//         server.OnPeerConnected += peer =>
//         {
//             serverPeerId = peer.Id;

//             var keyExchange = new Message
//             {
//                 Sender = "server",
//                 Type = MessageType.KeyExchange,
//                 PublicKey = serverPublicKey,
//                 TargetPeerId = peer.Id
//             };

//             server.SendAsync(peer.Id, keyExchange).GetAwaiter().GetResult();
//         };

//         server.OnMessageReceived += (peer, msg) =>
//         {
//             if (msg.Type == MessageType.SessionKey)
//             {
//                 byte[] key = serverRsa.DecryptSessionKey(msg.EncryptedContent!);
//                 peer.AesKey = key;
//                 peer.Aes = new AesEncryption(key);
//                 serverSessionKey = key;

//                 if (clientSessionKey != null)
//                     handshakeDone.Set();
//             }
//         };

//         client.OnConnected += peer => clientPeerId = peer.Id;

//         client.OnMessageReceived += (peer, msg) =>
//         {
//             if (msg.Type == MessageType.KeyExchange)
//             {
//                 peer.PublicKey = msg.PublicKey;
//                 peer.PeerRsa = new RsaEncryption();
//                 peer.PeerRsa.ImportPublicKey(peer.PublicKey);

//                 peer.AesKey = AesEncryption.GenerateKey();
//                 peer.Aes = new AesEncryption(peer.AesKey);
//                 clientSessionKey = peer.AesKey;

//                 byte[] encryptedSessionKey = peer.PeerRsa.EncryptSessionKey(peer.AesKey, peer.PublicKey);
//                 var response = new Message
//                 {
//                     Sender = "client",
//                     Type = MessageType.SessionKey,
//                     EncryptedContent = encryptedSessionKey,
//                     Signature = clientSigner.SignData(encryptedSessionKey)
//                 };

//                 client.SendAsync(peer.Id, response).GetAwaiter().GetResult();

//                 if (serverSessionKey != null)
//                     handshakeDone.Set();
//             }
//         };

//         try
//         {
//             // Start the server and connect the client to initiate the handshake
//             server.Start(port);
//             Assert.True(await client.ConnectAsync("127.0.0.1", port));
//             Assert.True(SpinWait.SpinUntil(() => serverPeerId != null && clientPeerId != null, 3000));
//             Assert.True(Wait(handshakeDone), "Session key exchange did not complete.");
//             // Verify that both the client and server have derived the same session key
//             Assert.NotNull(serverSessionKey);
//             Assert.NotNull(clientSessionKey);
//             Assert.Equal(
//                 Convert.ToBase64String(clientSessionKey!),
//                 Convert.ToBase64String(serverSessionKey!));
//             // Verify that the server has an AES instance initialized with the derived session key
//             Peer? serverPeer = server.GetPeer(serverPeerId!);
//             Assert.NotNull(serverPeer);
//             Assert.NotNull(serverPeer!.Aes);
//             // Verify that the client can encrypt a message with the session key and the server can decrypt it successfully
//             const string plaintext = "handshake-complete";
//             byte[] ciphertext = serverPeer.Aes!.Encrypt(plaintext);
//             string decrypted = Encoding.UTF8.GetString(new AesEncryption(clientSessionKey!).Decrypt(ciphertext));
//             Assert.Equal(plaintext, decrypted);
//         }
//         finally
//         {
//             DisconnectAll(client);
//             server.Stop();
//         }
//     }

//     [Fact]
//     public async Task TamperedMessageRejected()
//     {
//         // setup
//         int port = GetFreeTcpPort();
//         var server = new TcpServer();
//         var client = new TcpClientHandler();
//         var serverRsa = new RsaEncryption();
//         byte[] serverPublicKey = serverRsa.ExportPublicKey();
//         var clientRsa = new RsaEncryption();
//         byte[] clientPublicKey = clientRsa.ExportPublicKey();
//         var clientSigner = new MessageSigner(clientRsa.GetRSA());
//         var verifier = new MessageSigner(serverRsa.GetRSA());
//         byte[]? sharedSessionKey = null;
//         var handshakeDone = new ManualResetEventSlim(false);
//         var tamperedRejected = new ManualResetEventSlim(false);
//         var tamperedAccepted = new ManualResetEventSlim(false);

//         server.OnPeerConnected += peer =>
//         {
//             var serverHello = new Message
//             {
//                 Sender = "server",
//                 Type = MessageType.KeyExchange,
//                 PublicKey = serverPublicKey,
//                 TargetPeerId = peer.Id
//             };
//             server.SendAsync(peer.Id, serverHello).GetAwaiter().GetResult();
//         };

//         server.OnMessageReceived += (peer, msg) =>
//         {
//             if (msg.Type == MessageType.KeyExchange)
//             {
//                 peer.PublicKey = msg.PublicKey;
//                 peer.PeerRsa = new RsaEncryption();
//                 peer.PeerRsa.ImportPublicKey(peer.PublicKey);
//                 return;
//             }

//             if (msg.Type == MessageType.SessionKey)
//             {
//                 byte[] key = serverRsa.DecryptSessionKey(msg.EncryptedContent!);
//                 peer.AesKey = key;
//                 peer.Aes = new AesEncryption(key);
//                 sharedSessionKey = key;
//                 handshakeDone.Set();
//                 return;
//             }

//             if (msg.Type == MessageType.Text)
//             {
//                 bool verified = verifier.VerifyData(msg.EncryptedContent!, msg.Signature!, peer.PublicKey!);
//                 if (!verified)
//                 {
//                     tamperedRejected.Set();
//                     return;
//                 }

//                 tamperedAccepted.Set();
//             }
//         };

//         client.OnMessageReceived += (peer, msg) =>
//         {
//             if (msg.Type != MessageType.KeyExchange)
//                 return;

//             peer.PublicKey = msg.PublicKey;
//             peer.PeerRsa = new RsaEncryption();
//             peer.PeerRsa.ImportPublicKey(peer.PublicKey);

//             var clientHello = new Message
//             {
//                 Sender = "client",
//                 Type = MessageType.KeyExchange,
//                 PublicKey = clientPublicKey
//             };
//             client.SendAsync(peer.Id, clientHello).GetAwaiter().GetResult();

//             peer.AesKey = AesEncryption.GenerateKey();
//             peer.Aes = new AesEncryption(peer.AesKey);

//             byte[] encryptedSessionKey = peer.PeerRsa.EncryptSessionKey(peer.AesKey, peer.PublicKey);
//             var sessionKeyMessage = new Message
//             {
//                 Sender = "client",
//                 Type = MessageType.SessionKey,
//                 EncryptedContent = encryptedSessionKey,
//                 Signature = clientSigner.SignData(encryptedSessionKey)
//             };

//             client.SendAsync(peer.Id, sessionKeyMessage).GetAwaiter().GetResult();
//         };

//         try
//         {
//             // Start the server and connect the client to perform the handshake
//             server.Start(port);
//             Assert.True(await client.ConnectAsync("127.0.0.1", port));
//             Assert.True(Wait(handshakeDone), "Handshake never completed.");
//             // Create a valid encrypted message and then tamper with the ciphertext to simulate
//             byte[] ciphertext = new AesEncryption(sharedSessionKey!).Encrypt("do not trust this");
//             byte[] signature = clientSigner.SignData(ciphertext);
//             // Tamper with the ciphertext by flipping a bit
//             byte[] tamperedCiphertext = (byte[])ciphertext.Clone();
//             tamperedCiphertext[tamperedCiphertext.Length - 1] ^= 0x01;
//             // Create a message with the tampered ciphertext and send it to the server
//             var tamperedMessage = new Message
//             {
//                 Sender = "client",
//                 Type = MessageType.Text,
//                 Content = string.Empty,
//                 EncryptedContent = tamperedCiphertext,
//                 Signature = signature
//             };
//             // Send the tampered message to the server
//             Peer? clientPeer = client.GetConnectedPeers().FirstOrDefault();
//             Assert.NotNull(clientPeer);
    
//             await client.SendAsync(clientPeer!.Id, tamperedMessage);
//             // Wait for the server to process the message and verify that it was rejected due to failed signature verification
//             Assert.True(Wait(tamperedRejected), "Tampered message was not rejected.");
//             Assert.False(tamperedAccepted.IsSet, "Tampered message was accepted.");
//         }
//         finally
//         {
//             DisconnectAll(client);
//             server.Stop();
//         }
//     }

//     [Fact]
//     public async Task DifferentKeysPerConversation()
//     {
//         byte[] first = await CompleteHandshakeAndReturnKey();
//         byte[] second = await CompleteHandshakeAndReturnKey();

//         Assert.NotEqual(
//             Convert.ToBase64String(first),
//             Convert.ToBase64String(second));
//     }

//     private static async Task<byte[]> CompleteHandshakeAndReturnKey()
//     {
//         // Set up
//         int port = GetFreeTcpPort();
//         var server = new TcpServer();
//         var client = new TcpClientHandler();
//         var serverRsa = new RsaEncryption();
//         byte[] serverPublicKey = serverRsa.ExportPublicKey();
//         byte[]? sessionKey = null;
//         var handshakeDone = new ManualResetEventSlim(false);

//         server.OnPeerConnected += peer =>
//         {
//             var keyExchange = new Message
//             {
//                 Sender = "server",
//                 Type = MessageType.KeyExchange,
//                 PublicKey = serverPublicKey,
//                 TargetPeerId = peer.Id
//             };

//             server.SendAsync(peer.Id, keyExchange).GetAwaiter().GetResult();
//         };

//         server.OnMessageReceived += (peer, msg) =>
//         {
//             if (msg.Type != MessageType.SessionKey)
//                 return;

//             sessionKey = serverRsa.DecryptSessionKey(msg.EncryptedContent!);
//             peer.AesKey = sessionKey;
//             peer.Aes = new AesEncryption(sessionKey);
//             handshakeDone.Set();
//         };

//         client.OnMessageReceived += (peer, msg) =>
//         {
//             if (msg.Type != MessageType.KeyExchange)
//                 return;

//             peer.PublicKey = msg.PublicKey;
//             peer.PeerRsa = new RsaEncryption();
//             peer.PeerRsa.ImportPublicKey(peer.PublicKey);

//             peer.AesKey = AesEncryption.GenerateKey();
//             peer.Aes = new AesEncryption(peer.AesKey);

//             byte[] encryptedSessionKey = peer.PeerRsa.EncryptSessionKey(peer.AesKey, peer.PublicKey);
//             var response = new Message
//             {
//                 Sender = "client",
//                 Type = MessageType.SessionKey,
//                 EncryptedContent = encryptedSessionKey
//             };

//             client.SendAsync(peer.Id, response).GetAwaiter().GetResult();
//         };

//         try
//         {
//             // Start the server and connect the client to initiate the handshake
//             server.Start(port);
//             // Perform the handshake and wait for it to complete
//             Assert.True(await client.ConnectAsync("127.0.0.1", port));
//             // Wait for the handshake to complete and verify that a session key was established
//             Assert.True(Wait(handshakeDone), "Handshake never completed.");
//             // Verify that a session key was established and is not null
//             Assert.NotNull(sessionKey);
//             return sessionKey!;
//         }
//         finally
//         {
//             DisconnectAll(client);
//             server.Stop();
//         }
//     }
//     // Helper methods
//     private static bool Wait(ManualResetEventSlim evt, int timeoutMs = 5000)
//     {
//         return evt.Wait(timeoutMs);
//     }

//     private static int GetFreeTcpPort()
//     {
//         var listener = new TcpListener(IPAddress.Loopback, 0);
//         listener.Start();
//         int port = ((IPEndPoint)listener.LocalEndpoint).Port;
//         listener.Stop();
//         return port;
//     }

//     private static void DisconnectAll(TcpClientHandler client)
//     {
//         foreach (var peer in client.GetConnectedPeers().ToList())
//         {
//             client.Disconnect(peer.Id);
//         }
//     }
// }
