# Sprint 2 Documentation
## Secure Distributed Messenger

**Team Name:** Group 5

**Team Members:**
- Alia Ulanbek Kyzy - [Role/Responsibilities]
- Michael Reizenstein - [Role/Responsibilities]
- Sean Gaines - [Role/Responsibilities]

**Date: 3/27/26**

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

- **RSA Key Pair:** [When generated, how stored]
- **AES Session Key:** [When generated, lifetime]

### Key Storage
[Describe how keys are stored during runtime]

### Key Lifetime
| Key Type | Generated When | Expires When |
|----------|----------------|--------------|
| RSA Key Pair | | |
| AES Session Key | | |

---

## Wire Protocol

### Message Format
```
[Describe your message format, e.g.:]
[4 bytes: length][1 byte: type][payload]
```

### Message Types
| Type ID | Name | Description |
|---------|------|-------------|
| 0x01 | PUBLIC_KEY | RSA public key exchange |
| 0x02 | SESSION_KEY | Encrypted AES session key |
| 0x03 | MESSAGE | Encrypted chat message |
| 0x04 | SIGNED_MESSAGE | Signed and encrypted message |
| | | |

---

## Threat Model

### Assets Protected
- [What are you protecting? e.g., message content, user identity]

### Threats Addressed
| Threat | Mitigation |
|--------|------------|
| Eavesdropping | AES encryption of all messages |
| Man-in-the-middle | [Your mitigation] |
| Message tampering | Digital signatures |
| Replay attacks | [Your mitigation, if any] |
| | |

### Known Limitations
[What threats are NOT addressed by your implementation?]

---

## Features Implemented

- [ ] AES encryption of messages
- [ ] RSA key pair generation
- [ ] RSA key exchange
- [ ] AES session key exchange (encrypted with RSA)
- [ ] Message signing
- [ ] Signature verification
- [ ] Multiple simultaneous conversations
- [ ] Per-conversation encryption keys

---

## Testing Performed

### Security Tests
| Test | Expected Result | Actual Result | Pass/Fail |
|------|-----------------|---------------|-----------|
| Messages are encrypted on wire | Cannot read plaintext in network capture | | |
| Key exchange completes | Both peers have shared session key | | |
| Tampered message rejected | Signature verification fails | | |
| Different keys per conversation | Each peer pair has unique keys | | |

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
