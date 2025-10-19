using System;
using System.Collections.Concurrent;

public class PlayerService
{
    private readonly ConcurrentDictionary<string, Player> _players = new();

    public string RegisterPlayer()
    {
        string playerId = Guid.NewGuid().ToString();
        var player = new Player
        {
            Id = playerId,
            Position = new TilePosition { X = 5, Y = -5 },
            Speed = 1f // default speed
        };
        _players[playerId] = player;
        return playerId;
    }

    public bool TryGetPlayer(string playerId, out Player player) => _players.TryGetValue(playerId, out player);

    public void UpdatePlayerPosition(string playerId, TilePosition pos)
    {
        if (_players.TryGetValue(playerId, out var player))
            player.Position = pos;
    }
}
