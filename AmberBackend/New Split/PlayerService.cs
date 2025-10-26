using System;

public class PlayerService
{
    private readonly HashSet<string> _players = new();
    public string RegisterPlayer()
    {
        var id = Guid.NewGuid().ToString();
        _players.Add(id);
        return id;
    }
    public IEnumerable<string> GetAllPlayerIds() => _players;
    public bool TryGetPlayer(string id, out string pid) { pid = id; return _players.Contains(id); }
}

