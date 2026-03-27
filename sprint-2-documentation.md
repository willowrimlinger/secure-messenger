# Sprint 2 Documentation
## Secure Distributed Messenger

**Team Name:** Group 5

**Team Members:**
- Alia Ulanbek Kyzy - [Role/Responsibilities]
- Michael Reizenstein - [Role/Responsibilities]
- Sean Gaines - [Role/Responsibilities]

**Date:** [3/27/26]

---

## Build & Run Instructions

[Update from Sprint 1 if needed, or reference Sprint 1 documentation]

---

## Security Protocol Overview

### Encryption Protocol

#### Key Exchange Process
[Describe step-by-step how keys are exchanged when two peers connect]

1. Client 1 creates a RSA key pair
2. Client 1 sends out their public key
3. Client 2 stores Client 1's public key
4. Client 2 generates and Encrypts the AES session key with Client 1's public key
5. Client 1 decrypts the sent AES session key with their own private key

#### Message Encryption
[Describe how messages are encrypted before sending]
- **Before we send the message out the server we encrypt the message using the AES-256-CBC algorithm**
- **Algorithm: AES-256-CBC** [e.g., AES-256-CBC]
- **Key Size: 32 bytes (256 bits)** 
- **IV Generation: We use the System.Security.Cryptography to randomly generate our 16 byte IV**

#### Message Signing
[Describe how messages are signed and verified]
- **When signing a message the first thing that happens is that we create an RSA instance and then when signing the data we use the built in rsa.SignData which uses the current private key and returns the messanger signature that has been hashed and padded**
- **Algorithm: RSA with SHA-256** 
- **Key Size: 2048** 

---

## Key Management

### Key Generation
[Describe when and how keys are generated]

- **RSA Key Pair:** Generated once at program startup by each messenger instance using new RsaEncryption(). The public key is exported and shared in KeyExchange messages. The private key appears to remain only inside the running process.
- **AES Session Key:** Generated after a peer’s public RSA key is learned, using AesEncryption.GenerateKey(). It is encrypted with the peer’s RSA public key, sent in a SessionKey message, and then used for message encryption/decryption with that peer.

### Key Storage
Each peer instance stores PublicKey, PrivateKey, AESKey and _myRsa everything else is in local variables.

### Key Lifetime
| Key Type | Generated When | Expires When |
|----------|----------------|--------------|
| RSA Key Pair | At program startup | When the program ends |
| AES Session Key | After a peer public key is recieved | When the peer disconnects |

---

## Wire Protocol

### Message Format
```
[Describe your message format, e.g.:]
[4 bytes: length][JSON serialized message object]
```

### Message Types
| Type ID | Name | Description |
|---------|------|-------------|
| 0x01 | TEXT | General text |
| 0x02 | KeyExchange | RSA public key |
| 0x03 | SessionKey | AES Key | 
| 0x04 | HeartBeat| connection health check |
| 0x05 | PeerDiscovery | Announce presence to peers | 
| 0x06 | CreateRoom | Create a new room | 
| 0x07 | JoinRoomRequest | Request to join a room |
| 0x08 | LeaveRoom | Leave a room | 
| 0x09 | GetRooms | request a list of rooms |
|||||

---

## Threat Model

### Assets Protected
- [What are you protecting? e.g., message content, user identity]

### Threats Addressed
| Threat | Mitigation |
|--------|------------|
| Eavesdropping | AES encryption of all messages |
| Man-in-the-middle | We havent done anything in specific to mitage the threat of a man in the middle attack, due to the lack of the necessity/use of CAs |
| Message tampering | Digital signatures |
| Replay attacks |  |
| | |

### Known Limitations
[What threats are NOT addressed by your implementation?]

---

## Features Implemented

- [x] AES encryption of messages
- [x] RSA key pair generation
- [x] RSA key exchange
- [x] AES session key exchange (encrypted with RSA)
- [x] Message signing
- [x] Signature verification
- [x] Multiple simultaneous conversations
- [x] Per-conversation encryption keys

---

## Testing Performed

### Security Tests
| Test | Expected Result | Actual Result | Pass/Fail |
|------|-----------------|---------------|-----------|
| Messages are encrypted on wire | Cannot read plaintext in network capture | | Pass |
| Key exchange completes | Both peers have shared session key | | Pass |
| Tampered message rejected | Signature verification fails | | Pass |
| Different keys per conversation | Each peer pair has unique keys | | Pass |

---

## Known Issues

| Issue | Description | Workaround |
|-------|-------------|------------|
| | | |

---

## Video Demo Checklist

Your demo video (5-7 minutes) should show:
- [ ] Two peers connecting and exchanging keys
- [ ] Sending encrypted messages
- [ ] Showing that messages are encrypted (e.g., log output)
- [ ] Demonstrating signature verification
- [ ] Showing what happens with a tampered message (if possible)
- [ ] Multiple simultaneous conversations
