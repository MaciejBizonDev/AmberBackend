using Npgsql;
using System;

/// <summary>
/// PostgreSQL database for player persistence with authentication.
/// </summary>
public class PlayerDatabase
{
    private readonly string _connectionString;

    public PlayerDatabase(string host = "localhost", int port = 5432, string database = "mmorpg",
                         string username = "gameserver", string password = "game123")
    {
        _connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();

        // Create Players table with CurrentZone
        command.CommandText = @"
        CREATE TABLE IF NOT EXISTS Players (
            PlayerId TEXT PRIMARY KEY,
            LastX INTEGER NOT NULL,
            LastY INTEGER NOT NULL,
            CurrentZone TEXT NOT NULL DEFAULT 'test_zone',
            LastLogin TIMESTAMP NOT NULL,
            CreatedAt TIMESTAMP NOT NULL
        )";
        command.ExecuteNonQuery();

        // Create Users table
        command.CommandText = @"
        CREATE TABLE IF NOT EXISTS Users (
            Username TEXT PRIMARY KEY,
            PasswordHash TEXT NOT NULL,
            PlayerId TEXT UNIQUE NOT NULL,
            CreatedAt TIMESTAMP NOT NULL,
            FOREIGN KEY (PlayerId) REFERENCES Players(PlayerId)
        )";
        command.ExecuteNonQuery();

        // Create indexes
        command.CommandText = @"
        CREATE INDEX IF NOT EXISTS idx_players_lastlogin ON Players(LastLogin);
        CREATE INDEX IF NOT EXISTS idx_users_playerid ON Users(PlayerId);
    ";
        command.ExecuteNonQuery();

        Console.WriteLine("[PlayerDatabase] PostgreSQL database initialized");
    }

    /// <summary>
    /// Authenticate user and return player data.
    /// WARNING: Placeholder - password is NOT hashed!
    /// For production, use BCrypt.Net or similar.
    /// </summary>
    public PlayerData LoginUser(string username, string password)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT u.PlayerId, p.LastX, p.LastY, p.LastLogin, p.CreatedAt
            FROM Users u
            JOIN Players p ON u.PlayerId = p.PlayerId
            WHERE u.Username = @username 
            AND u.PasswordHash = @password";

        command.Parameters.AddWithValue("username", username);
        command.Parameters.AddWithValue("password", password);  // ⚠️ Not hashed!

        using var reader = command.ExecuteReader();

        if (reader.Read())
        {
            var playerId = reader.GetString(0);

            var playerData = new PlayerData
            {
                PlayerId = playerId,
                LastX = reader.GetInt32(1),
                LastY = reader.GetInt32(2),
                LastLogin = reader.GetDateTime(3),
                CreatedAt = reader.GetDateTime(4)
            };

            // Close reader before updating
            reader.Close();

            // Update last login
            UpdateLastLogin(playerId);

            return playerData;
        }

        return null;
    }

    private void UpdateLastLogin(string playerId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Players 
            SET LastLogin = @now 
            WHERE PlayerId = @playerId";

        command.Parameters.AddWithValue("now", DateTime.UtcNow);
        command.Parameters.AddWithValue("playerId", playerId);
        command.ExecuteNonQuery();
    }

    public void SavePlayer(string playerId, int x, int y, string zoneId = "test_zone")
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
        INSERT INTO Players (PlayerId, LastX, LastY, CurrentZone, LastLogin, CreatedAt)
        VALUES (@playerId, @x, @y, @zoneId, @now, @now)
        ON CONFLICT(PlayerId) 
        DO UPDATE SET 
            LastX = @x,
            LastY = @y,
            CurrentZone = @zoneId,
            LastLogin = @now";

        command.Parameters.AddWithValue("playerId", playerId);
        command.Parameters.AddWithValue("x", x);
        command.Parameters.AddWithValue("y", y);
        command.Parameters.AddWithValue("zoneId", zoneId);
        command.Parameters.AddWithValue("now", DateTime.UtcNow);

        command.ExecuteNonQuery();
    }

    public PlayerData LoadPlayer(string playerId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
        SELECT PlayerId, LastX, LastY, CurrentZone, LastLogin, CreatedAt
        FROM Players
        WHERE PlayerId = @playerId";

        command.Parameters.AddWithValue("playerId", playerId);

        using var reader = command.ExecuteReader();

        if (reader.Read())
        {
            return new PlayerData
            {
                PlayerId = reader.GetString(0),
                LastX = reader.GetInt32(1),
                LastY = reader.GetInt32(2),
                CurrentZone = reader.GetString(3),
                LastLogin = reader.GetDateTime(4),
                CreatedAt = reader.GetDateTime(5)
            };
        }

        return null;
    }

    public int CleanupOldPlayers(int daysInactive = 30)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            var cutoffDate = DateTime.UtcNow.AddDays(-daysInactive);

            using var transaction = connection.BeginTransaction();

            // Delete users that reference the old players FIRST (respect FK constraint)
            var deleteUsers = connection.CreateCommand();
            deleteUsers.Transaction = transaction;
            deleteUsers.CommandText = @"
            DELETE FROM users
            WHERE playerid IN (
                SELECT playerid FROM players WHERE lastlogin < @cutoffDate
            )";
            deleteUsers.Parameters.AddWithValue("cutoffDate", cutoffDate);
            int usersDeleted = deleteUsers.ExecuteNonQuery();

            // Then delete the players
            var deletePlayers = connection.CreateCommand();
            deletePlayers.Transaction = transaction;
            deletePlayers.CommandText = @"
            DELETE FROM players
            WHERE lastlogin < @cutoffDate";
            deletePlayers.Parameters.AddWithValue("cutoffDate", cutoffDate);
            int playersDeleted = deletePlayers.ExecuteNonQuery();

            transaction.Commit();

            Console.WriteLine($"[PlayerDatabase] Cleaned up {playersDeleted} inactive players and {usersDeleted} users (>{daysInactive} days)");
            return playersDeleted;
        }
        catch (Exception ex)
        {
            // Cleanup failure should NEVER crash server startup
            Console.WriteLine($"[PlayerDatabase] CleanupOldPlayers failed (non-fatal): {ex.Message}");
            return 0;
        }
    }

    public int GetPlayerCount()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Players";

        var result = command.ExecuteScalar();
        return Convert.ToInt32(result);
    }

    public int GetActivePlayerCount(int days = 7)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT COUNT(*) FROM Players
            WHERE LastLogin > @cutoffDate";

        command.Parameters.AddWithValue("cutoffDate", DateTime.UtcNow.AddDays(-days));

        var result = command.ExecuteScalar();
        return Convert.ToInt32(result);
    }
}

public class PlayerData
{
    public string PlayerId { get; set; }
    public int LastX { get; set; }
    public int LastY { get; set; }
    public string CurrentZone { get; set; }
    public DateTime LastLogin { get; set; }
    public DateTime CreatedAt { get; set; }
}