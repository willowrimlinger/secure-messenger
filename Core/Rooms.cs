using SecureMessenger.Core;

class Rooms
{
    public Dictionary<int, HashSet<string>> _rooms { get; set; } = new(); 
    private readonly object _lock = new(); 

    public bool ContainsPeer(int room, string peerID)
    {
        bool output = false; 
        lock(_lock)
        {
            output = _rooms[room].Contains(peerID); 
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

    public HashSet<string> GetRoom(int room)
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
            _rooms.Add(room, new HashSet<string>{}); 
        }
        return true; 
    }

    public bool AddPeer(int room, string peerID)
    {
        if(!RoomExists(room)) return false; 
        if(ContainsPeer(room, peerID)) return false; 
        bool output;
        lock(_lock)
        {
            output = _rooms[room].Add(peerID);
        }
        return output; 
    }

    public bool RemovePeer(int room, string peerID)
    {
        if(!RoomExists(room)) return false; 
        if(!ContainsPeer(room, peerID)) return false; 
        bool output;
        lock(_lock)
        {
            output = _rooms[room].Remove(peerID);
        }
        return output; 
    }

    public void RemovePeerAllRooms(string peerID)
    {
        foreach(var room in _rooms.Keys)
        {
            RemovePeer(room, peerID); 
        }
    }

    public IEnumerable<int> GetRooms()
    {
        lock(_lock)
        {
            return _rooms.Keys.ToList(); 
        }
    }

    public void Merge(Rooms other)
    {
        lock(_lock)
        {
            foreach(var id in other._rooms.Keys)
            {
                if(_rooms.ContainsKey(id))
                {
                    _rooms[id].UnionWith(other._rooms[id]); 
                }
                else
                {
                    _rooms[id] = other._rooms[id]; 
                }
            }
        }
    }


}