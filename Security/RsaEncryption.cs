// [Michael Reizenstein]
// CSCI 251 - Secure Distributed Messenger

using System.Security.Cryptography;

namespace SecureMessenger.Security;

/// <summary>
/// Sprint 2: RSA encryption for key exchange.
/// Used to securely exchange AES session keys between peers.
///
/// RSA Configuration:
/// - Key size: 2048 bits
/// - Padding: OAEP with SHA-256 (RSAEncryptionPadding.OaepSHA256)
///
/// Usage:
/// 1. Each peer generates their own RSA key pair
/// 2. Peers exchange public keys
/// 3. One peer generates an AES session key
/// 4. That peer encrypts the AES key with the other's public key
/// 5. The encrypted key is sent and decrypted with the private key
/// 6. Both peers now have the same AES session key
/// </summary>
public class RsaEncryption
{
    private readonly RSA _rsa;
    // added peer RSA instance for encryption/decryption of session keys
    private RSA? _peerRsa;

    /// <summary>
    /// Create a new RSA key pair.
    ///
    /// TODO: Implement the following:
    /// 1. Create a 2048-bit RSA key pair using RSA.Create(2048)
    /// </summary>
    public RsaEncryption()
    {
        // TODO: Generate RSA key pair (2048 bits)
        _rsa = RSA.Create(2048);
    }

    /// <summary>
    /// Export our public key to send to a peer.
    ///
    /// TODO: Implement the following:
    /// 1. Use _rsa.ExportRSAPublicKey() to get the public key bytes
    /// 2. Return the byte array
    /// </summary>
    public byte[] ExportPublicKey()
    {
        try
        {
            // Export the public key and return it
            byte[] publicKey = _rsa.ExportRSAPublicKey();
            return publicKey;
        }
        catch (CryptographicException ex)
        {
            Console.WriteLine($"ERROR: Failed to export public key - {ex.Message}");
            throw; // Rethrow exception to indicate export failure
        }
    }

    /// <summary>
    /// Import a peer's public key.
    ///
    /// TODO: Implement the following:
    /// 1. Use _rsa.ImportRSAPublicKey(publicKey, out _) to import the key
    ///
    /// Note: The 'out _' discards the bytes read count which we don't need
    /// </summary>
    public void ImportPublicKey(byte[] publicKey)
    {
        try
        {
            _peerRsa?.Dispose(); // Dispose previous peer RSA if exists
            _peerRsa = RSA.Create(); // Create new RSA instance for the peer's key
            _peerRsa.ImportRSAPublicKey(publicKey, out _); // Import the peer's public key
        }
        catch (CryptographicException ex)
        {
            Console.WriteLine($"ERROR: Failed to import public key - {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Encrypt an AES session key with a peer's public key.
    ///
    /// TODO: Implement the following:
    /// 1. Create a new RSA instance for the peer's key using RSA.Create()
    /// 2. Import the peer's public key into this new instance
    /// 3. Encrypt the AES key using peerRsa.Encrypt() with OaepSHA256 padding
    /// 4. Return the encrypted key bytes
    ///
    /// Important: Use RSAEncryptionPadding.OaepSHA256 for security
    /// </summary>
    public byte[] EncryptSessionKey(byte[] aesKey, byte[] peerPublicKey)
    {
        try
        {
            using (RSA peerRsa = RSA.Create())
            {
                // Import the peer's public key and encrypt the AES key
                peerRsa.ImportRSAPublicKey(peerPublicKey, out _);
                return peerRsa.Encrypt(aesKey, RSAEncryptionPadding.OaepSHA256);
            }
        }
        catch (CryptographicException ex)
        {
            Console.WriteLine($"ERROR: Failed to encrypt session key - {ex.Message}");
            throw; // Rethrow exception to indicate encryption failure
        }
    }

    /// <summary>
    /// Decrypt an AES session key with our private key.
    ///
    /// TODO: Implement the following:
    /// 1. Use _rsa.Decrypt() with OaepSHA256 padding to decrypt
    /// 2. Return the decrypted AES key bytes
    ///
    /// Important: Use RSAEncryptionPadding.OaepSHA256 (must match encryption)
    /// </summary>
    public byte[] DecryptSessionKey(byte[] encryptedKey)
    {
        try
        {
            // Decrypt the session key with our private key
            byte[] aesKey = _rsa.Decrypt(encryptedKey, RSAEncryptionPadding.OaepSHA256);
            return aesKey;
        }
        catch (CryptographicException ex)
        {
            Console.WriteLine($"ERROR: Failed to decrypt session key - {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Dispose of RSA resources
    /// </summary>
    public void Dispose()
    {
        _peerRsa?.Dispose();
        _rsa?.Dispose();
    }
}
