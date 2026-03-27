using SecureMessenger.Core;

class Rooms
{
    private Dictionary<int, HashSet<Peer>> _rooms = new(); 
    private readonly object _lock = new(); 

    public bool ContainsPeer(int room, Peer peer)
    {
        bool output = false; 
        lock(_lock)
        {
            output = _rooms[room].Contains(peer); 
        }
        return output; 
    }

    public bool RoomExists(int room)
    {
        bool output = false; 
        lock(_lock)
        {
            output = _rooms.ContainsKey(room); 
        }
        return output; 
    }

    public HashSet<Peer> GetRoom(int room)
    {
        lock(_lock)
        {
            return _rooms[room]; 
        }
    }

    public bool CreateRoom(int room)
    {
        if(RoomExists(room)) return false; 
        lock(_lock)
        {
            _rooms.Add(room, new HashSet<Peer>{}); 
        }
        return true; 
    }

    public bool AddPeer(int room, Peer peer)
    {
        if(!RoomExists(room)) return false; 
        if(ContainsPeer(room, peer)) return false; 
        bool output;
        lock(_lock)
        {
            output = _rooms[room].Add(peer);
        }
        return output; 
    }

    public bool RemovePeer(int room, Peer peer)
    {
        if(!RoomExists(room)) return false; 
        if(ContainsPeer(room, peer)) return false; 
        bool output;
        lock(_lock)
        {
            output = _rooms[room].Remove(peer);
        }
        return output; 
    }

    public void RemovePeerAllRooms(Peer peer)
    {
        foreach(var room in _rooms.Keys)
        {
            RemovePeer(room, peer); 
        }
    }

    public IEnumerable<int> GetRooms()
    {
        lock(_lock)
        {
            return _rooms.Keys.ToList(); 
        }
    }


}