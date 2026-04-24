# Sprint 3 Documentation (Final)
## Secure Distributed Messenger

**Team Name:** Group 5

**Team Members:**
- Alia Ulanbek Kyzy - [Role/Responsibilities]
- Michael Reizenstein - [Role/Responsibilities]
- Sean Gaines - [peer discovery/]

**Date:** [4/23/26]

---

## Build & Run Instructions

### Prerequisites
- n/a

### Building
```
dotnet build
```

### Running
```
dotnet run
```

### Command Line Arguments
n/a, project does not require arguments in the command line
| Argument | Description | Default |
|----------|-------------|---------|
| | | |

---

## Application Commands

| Command | Description | Example |
|---------|-------------|---------|
| `/connect <ip> <port>` | Connect to a peer | `/connect 192.168.1.100 5000` |
| `/listen <port>` | Start listening | `/listen 5000` |
| `/peers` | List known peers | `/peers` |
| `/history` | View message history | `/history` |
| `/quit` | Exit application | `/quit` |
| `/create <# room>` | Create room | `/create 2` |
| `/join  <# room>` | Join a room | `/join 3` |
| `/leave  <# room>` | Leave a room | `/leave 3` |
| `/rooms` |  List all rooms | `/leave 3` |
| `/msg  <# room> <msg>` | Send a message to a room | `/msg 3 hello` |
| `/msg  <@ room> <msg>` | Send a message to a peer | `/msg 3` |

---

## Architecture Diagram

```
[Insert ASCII diagram of your system architecture]
[Show major components and how they interact]
                 ┌──────────────────────┐
                 │     Console UI       │
                 └─────────┬────────────┘
                           │
                           ▼
                 ┌──────────────────────┐
      -------->  │      Program         │
                 └─────────┬────────────┘
                           │
        ┌──────────────────┼──────────────────┐
        ▼                  ▼                  ▼
┌──────────────┐   ┌──────────────┐  ┌───────────────┐
│ MessageQueue │   │   Rooms      │  │ MessageHistory│
└──────┬───────┘   └──────┬───────┘  └──────┬────────┘
       │                  │                 │
       └───────────┬──────┴──────┬──────────┘
                   ▼             ▼
          ┌─────────────────────────────┐
          │   Security Layer            │
          │ RSA / AES / Message Signing │
          └─────────┬───────────────────┘
                    │
        ┌───────────┼───────────────┐
        ▼           ▼               ▼
┌────────────┐ ┌────────────┐ ┌──────────────┐
│ TcpServer  │ │ TcpClient  │ │ PeerDiscovery│
└─────┬──────┘ └─────┬──────┘ └──────┬───────┘
      │              │               │
      ▼              ▼               ▼
    ┌────────────────────────────────┐
    │         Remote Peers           │
    └────────────────────────────────┘

       ┌──────────────────────────────┐
       │ HeartbeatMonitor (Resilience)│
       └──────────────────────────────┘
```

### Component Descriptions

| Component | Responsibility |
|-----------|----------------|
| Program | Entry point, main functionality|
| MessageQueue | queue to store messages |
| Message | model for messages |
| Peer | information about peer |
| TcpServer | listens for connection|
| TcpClientHandler | handles one client connection |
| PeerDiscovery | UDP broadcast |
| AesEncryption | AES encrypt decrypt|
| RsaEncryption | RSA Key Management |
| MessageSigner | Sign and verify messages |
| ConsoleUI | User Interface|

---

## Protocol Specification

### Connection Establishment
[Describe the full connection handshake]

```
Peer A                          Peer B
  |                                |
  |-------- [Step 1] ------------->|
  |<------- [Step 2] --------------|
  |-------- [Step 3] ------------->|
  |                                |
```

### Message Flow
Messages start with user input. It is either registered as a command or a message. If its a command, it will go through ConsoleUI where the command will be parsed and returned back to Program.cs.

Users create messages → messages are queued into MessageQueue → encrypted per peer → sent over TCP → received by network threads → verified and decrypted → filtered by room membership → displayed and stored in history → optionally forwarded to other peers in the same room.

### Peer Discovery Protocol
Our system uses a UDP-based peer discovery system to find and track other peers on the local network automatically without needing a central server. Each peer periodically broadcasts a small UDP message on a shared port in the format "PEER:{peerId}:{tcpPort}", where peerId uniquely identifies the peer and tcpPort indicates where it is listening for TCP connections. These broadcasts are sent every few seconds to the network broadcast address, allowing all machines on the same subnet to receive them. When a peer receives such a message, it parses the string, extracts the sender’s identity and connection details, and either adds the peer to its local known peer list or updates the peer’s last-seen timestamp if it already exists. Peers that stop broadcasting are eventually considered inactive and removed after a timeout period, ensuring the discovery list stays up to date and reflects only currently active participants.

#### Broadcast Message Format
```
PEER:{peerId}:{tcpPort}
Example: "PEER:abc12345:5000"
PEER — fixed prefix identifying the message as a discovery packet
peerId — a unique identifier for the peer (generated at startup, e.g., a short GUID)
tcpPort — the TCP listening port the peer is using for direct messaging connections
```

#### Discovery Process
1. Broadcasting presence- Each peer periodically sends a UDP broadcast message on the local network in the format PEER:{peerId}:{tcpPort}.
2. Receiving broadcast messages - All peers listening on the shared UDP discovery port receive these broadcast packets. 
3. Parse peer information - The message is split into components to extract the sender’s peerId and tcpPort.
4. Filtering self-messages - If the discovered peerId matches the local peer’s ID, the message is ignored 
5. Updating or adding peers If the peer already exists in the known peer list, its LastSeen timestamp is updated. If it is a new peer, it is added to the _knownPeers collection and stored along with its IP and port.
6. Event notification - When a new peer is discovered, an event (OnPeerDiscovered) is triggered so the rest of the system (UI, connection manager) can react, such as displaying the peer or attempting a TCP connection.
7. Timeout-based cleanup -  Separately, a timeout loop periodically checks all known peers. If a peer has not broadcast within the timeout window, it is removed and an OnPeerLost event is triggered.

### Heartbeat Protocol
The heartbeat mechanism is used to monitor the health of active peer connections and detect failures in a timely manner. Each peer periodically signals that it is still active, and peers track these signals to determine whether a connection is still valid.

- **Interval:** 5 seconds
- **Timeout:** 15 seconds
- **Action on timeout:** system triggers a connection failure event, removes the peer from the active peer list, and notifies other components (such as the UI and messaging system).

---

## P2P Architecture

### Peer Management
[Describe how peers are tracked and managed]

### Connection Strategy
[Describe how connections are established and maintained]

### Message Routing
[Describe how messages are routed between peers]

---

## Resilience Features

### Failure Detection
[Describe how connection failures are detected]

### Automatic Reconnection
[Describe your reconnection strategy]

- **Initial delay:** [e.g., 1 second]
- **Backoff strategy:** [e.g., exponential, max 30 seconds]
- **Max attempts:** [e.g., 5]

### Graceful Degradation
[Describe how the system behaves when peers are unavailable]

---

## Message History

### Storage Format
[Describe how messages are stored locally]

### File Location
[Where is history stored?]

### History Commands
[How users interact with history]

---

## User Guide

### Getting Started
1. [Step 1: Start the application]
2. [Step 2: ...]
3. ...

### Connecting to Peers
[Instructions for connecting]

### Sending Messages
run /help to see different message commands. To send message to a room, join a room and then type a message without a command. To send a message to a peer or a specific room use the /msg command

### Viewing Peer Status
run command /peers to list all peers

### Troubleshooting
| Problem | Solution |
|---------|----------|
| Cannot connect to peer | [Check firewall, verify IP/port] |
| Messages not sending | [Check connection status] |
| | |

---

## Features Implemented

### Core Features
- [x] P2P architecture (no central server)
- [x] Peer discovery (UDP broadcast)
- [x] Automatic peer connection
- [x] Heartbeat monitoring
- [x] Failure detection
- [x] Automatic reconnection
- [x] Message history (file-based)
- [x] Parallel message processing

### Security Features (from Sprint 2)
- [x] AES encryption
- [x] RSA key exchange
- [x] Message signing

### Bonus Features (if implemented)
- [x] Message relay through intermediate peers
- [ ] Encrypted history storage
- [ ] Peer persistence (save/load known peers)

---

## Testing Performed

### P2P Tests
| Test | Expected Result | Actual Result | Pass/Fail |
|------|-----------------|---------------|-----------|
| 3+ peers can form mesh | All peers connected | | Pass |
| Peer discovery works | New peer found automatically | | PAss |
| Peer leaving detected | Removed from peer list | | Pass |
| Reconnection after failure | Connection restored | | Pass |

### Resilience Tests
| Test | Expected Result | Actual Result | Pass/Fail |
|------|-----------------|---------------|-----------|
| Kill peer process | Detected as failed | | |
| Network interruption | Reconnection attempted | | |
| Peer rejoins | Connection restored | | |

---

## Known Issues

| Issue | Description | Severity | Workaround |
|-------|-------------|----------|------------|
| | | | |

---

## Future Improvements

[What would you improve with more time?]

---

## Video Demo Checklist

Your demo video (8-10 minutes) should show:
- [x] Starting 3+ peer instances
- [x] Peer discovery in action
- [x] Messages between multiple peers
- [x] Killing a peer and showing failure detection
- [x] Automatic reconnection when peer returns
- [x] Message history feature
- [x] `/peers` command showing connected peers
