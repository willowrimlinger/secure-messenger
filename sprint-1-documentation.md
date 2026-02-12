# Sprint 1 Documentation
## Secure Distributed Messenger

**Team Name:** Group 5

**Team Members:**
- Alia Ulanbek Kyzy - [Role/Responsibilities]
- Michael Reizenstein - [Role/Responsibilities]
- Sean Gaines - [Role/Responsibilities]
- Willow Rimlinger - TCPServer.cs

**Date:** [Submission Date]

---

## Build Instructions

### Prerequisites
- .NET SDK 9
- [Any other dependencies]

### Building the Project
```
dotnet run
```

---

## Run Instructions

### Starting the Application
```
[Commands to run the application]
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
| | | |

---

## Architecture Overview

### Threading Model
[Describe your threading approach - which threads exist and what each does]

- **Main Thread:** Listen for commands, display the UI, and manage the message queue
- **Receive Thread:** Displays messages that the client receives
- **Send Thread:** Receives messages from peers and broadcasts it to all other peers
- [Additional threads...]

### Thread-Safe Message Queue

The message queue is implemented using a BlockingCollection which guaruntees
atomic enqueues and dequeues.

---

## Features Implemented

- [x] Multi-threaded architecture
- [x] Thread-safe message queue
- [x] TCP server (listen for connections)
- [x] TCP client (connect to peers)
- [ ] Send/receive text messages
- [ ] Graceful disconnection handling
- [x] Console UI with commands

---

## Testing Performed

### Test Cases
| Test | Expected Result | Actual Result | Pass/Fail |
|------|-----------------|---------------|-----------|
| Two instances can connect | Connection established | | |
| Messages sent and received | Message appears on other instance | | |
| Disconnection handled | No crash, appropriate message | | |
| Thread safety under load | No race conditions | | |

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
