using Npgsql;

public class RegistrationService
{
    private readonly string _connectionString;

    public RegistrationService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<(bool success, string message)> RegisterUser(string username, string password)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
            return (false, "Username must be at least 3 characters");

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return (false, "Password must be at least 6 characters");

        try
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Check if username exists
            using var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM users WHERE username = @username", conn);
            checkCmd.Parameters.AddWithValue("username", username);
            var count = (long)(await checkCmd.ExecuteScalarAsync() ?? 0);

            if (count > 0)
                return (false, "Username already taken");

            // Generate player ID
            string playerId = $"{username}-{Guid.NewGuid().ToString().Substring(0, 8)}";

            // Start transaction
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // Create player entry
                using var playerCmd = new NpgsqlCommand(
                    "INSERT INTO players (playerid, lastx, lasty, currentzone, lastlogin, createdat) VALUES (@id, @x, @y, @zone, @now, @now)",
                    conn, transaction);
                playerCmd.Parameters.AddWithValue("id", playerId);
                playerCmd.Parameters.AddWithValue("x", 6);
                playerCmd.Parameters.AddWithValue("y", -7);
                playerCmd.Parameters.AddWithValue("zone", "test_zone");
                playerCmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                await playerCmd.ExecuteNonQueryAsync();

                // Create user entry
                using var userCmd = new NpgsqlCommand(
                    "INSERT INTO users (username, passwordhash, playerid, createdat) VALUES (@username, @password, @playerid, @now)",
                    conn, transaction);
                userCmd.Parameters.AddWithValue("username", username);
                userCmd.Parameters.AddWithValue("password", password); // TODO: Hash this!
                userCmd.Parameters.AddWithValue("playerid", playerId);
                userCmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                await userCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
                Console.WriteLine($"[RegistrationService] Created user: {username} (ID: {playerId})");
                return (true, "Registration successful");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RegistrationService] Error: {ex.Message}");
            return (false, "Registration failed");
        }
    }
}