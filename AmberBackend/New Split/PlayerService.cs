using AmberBackend.Movement;

/// <summary>
/// Player service with authentication support.
/// </summary>
public class PlayerService
{
    private readonly PlayerDatabase _database;

    public PlayerService(PlayerDatabase database)
    {
        _database = database;
    }

    /// <summary>
    /// Authenticate user and return their player data.
    /// </summary>
    public PlayerData Login(string username, string password)
    {
        return _database.LoginUser(username, password);
    }

    public TilePosition GetSpawnPosition(string playerId)
    {
        var playerData = _database.LoadPlayer(playerId);
        return playerData != null
            ? new TilePosition(playerData.LastX, playerData.LastY)
            : new TilePosition(5, -5);
    }

    public void UpdatePlayerPosition(string playerId, TilePosition position, string zoneId)
    {
        _database.SavePlayer(playerId, position.X, position.Y, zoneId);
    }
}