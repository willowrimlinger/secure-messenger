# Sprint 1 Documentation
## Secure Distributed Messenger

**Team Name:** Group 5

**Team Members:**
- Alia Ulanbek Kyzy - Program.cs, Console.UI
- Michael Reizenstein - TcpClientHandler.cs
- Sean Gaines - Console.UI, TcpServer.cs
- Willow Rimlinger - TCPServer.cs

**Date:** 02/27/26

---

## Build Instructions

### Prerequisites
- .NET SDK 9

### Building the Project
```
dotnet run
```

---

## Run Instructions

### Starting the Application
```
dotnet run
```

### Command Line Arguments (if any)
| Argument | Description | Example |
|----------|-------------|---------|
| | | |

---

## Application Commands

| Command | Description | Example |
|---------|-------------|---------|
| `/connect <ip> <port>` | Connect to a peer | `/connect 192.168.1.100 5000` |
| `/listen <port>` | Start listening for connections | `/listen 5000` |
| `/quit` | Exit the application | `/quit` |
| `/peers` | List connected peers | `/peers` |
| `/history` | View message history | `/history` |

---

## Architecture Overview

### Threading Model

- **Main Thread:** Listen for commands, display the UI, and manage the message queue
- **Receive Thread:** Displays messages from the incoming message queue
- **Send Thread:** Takes messages from the outgoing message queue and broadcasts them to all other peers
- **TcpServer Listen Thread** Listen for incoming connections and once received, spawn a new receive thread to handle messages
- **TcpServer Receive Thread** Receives messages from a single peer and adds them to the incoming messages queue. One thread per peer.

### Thread-Safe Message Queue

The message queue is implemented using a BlockingCollection which guaruntees
atomic enqueues and dequeues.

---

## Features Implemented

- [x] Multi-threaded architecture
- [x] Thread-safe message queue
- [x] TCP server (listen for connections)
- [x] TCP client (connect to peers)
- [x] Send/receive text messages
- [x] Graceful disconnection handling
- [x] Console UI with commands

---

## Testing Performed

### Test Cases
| Test | Expected Result | Actual Result | Pass/Fail |
|------|-----------------|---------------|-----------|
| Two instances can connect | Connection established | Connection established | Pass |
| Messages sent and received | Message appears on other instance | Message appears on other instance | Pass |
| Disconnection handled | No crash, appropriate message | No crash, appropriate message | Pass |
| Thread safety under load | No race conditions | No race conditions | Pass |

---

## Known Issues

| Issue | Description | Workaround |
|-------|-------------|------------|
| | | |

---

## Video Demo Checklist

Your demo video (3-5 minutes) should show:
- [ ] Starting two instances of the application
- [ ] Connecting the instances
- [ ] Sending messages in both directions
- [ ] Disconnecting gracefully
- [ ] (Optional) Showing thread-safe behavior under load
