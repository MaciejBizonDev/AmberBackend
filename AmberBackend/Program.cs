using AmberBackend.Movement;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class Program
{
    public static async Task Main(string[] args)
    {
        if (!TestDatabaseConnection())
        {
            Console.WriteLine("Database connection failed. Exiting.");
            return;
        }



        ///
        var cts = new CancellationTokenSource();

        // Initialize services
        var tilemaps = new TilemapRepository("Resources/Tilemaps");
        var pathfinder = new GridAStarPathfinder(tilemaps);
        var playerService = new PlayerService();
        var movementService = new MovementService(tilemaps);
        var npcService = new NPCService(tilemaps, pathfinder);
        var messageHandler = new MessageHandlerService(playerService, movementService);
        var wsServer = new WebSocketServer(messageHandler, movementService);

        // ✅ Wire NPC movements to broadcast to all clients
        npcService.OnNpcMove += (npcId, from, to, duration) =>
        {
            movementService.BroadcastNpcMovement(npcId, from, to, duration);
        };

        // Spawn some NPCs with patrol paths
        var guard1Path = new List<TilePosition>
        {
            new TilePosition(0, 0),
            new TilePosition(5, 0)
        };
        npcService.SpawnNpc("npc_guard_1", new TilePosition(0, 0), guard1Path, speed: 2f);

        var guard2Path = new List<TilePosition>
        {
            new TilePosition(0, 3),
            new TilePosition(5, 3)
        };
        npcService.SpawnNpc("npc_guard_2", new TilePosition(0, 3), guard2Path, speed: 2f);

        // Register NPCs in movement service (so they appear in snapshots)
        movementService.RegisterEntity("npc_guard_1", new TilePosition(0, 0), speed: 2f);
        movementService.RegisterEntity("npc_guard_2", new TilePosition(0, 3), speed: 2f);

        Console.WriteLine("=== Client-Authoritative Movement System ===");
        Console.WriteLine("Players: Client moves immediately, server validates");
        Console.WriteLine("NPCs: Server-authoritative movement");
        Console.WriteLine("Much simpler than acknowledgment system!");
        Console.WriteLine("===========================================");

        // ✅ Start WebSocket server (this is the key line!)
        var wsTask = wsServer.StartAsync(cts.Token);

        // ✅ NPC update loop (10 Hz = every 100ms)
        var npcTickTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    npcService.Tick(0.1f);
                    await Task.Delay(100, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, cts.Token);

        Console.WriteLine("Server started on ws://localhost:5000/ws");
        Console.WriteLine("Press Ctrl+C to stop.");

        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        await Task.WhenAll(wsTask, npcTickTask);

        Console.WriteLine("Server stopped.");
    }

    private static bool TestDatabaseConnection()
    {
        var connectionString = "Host=localhost;Port=5432;Database=mmorpg;Username=gameserver;Password=game123";

        try
        {
            using var conn = new Npgsql.NpgsqlConnection(connectionString);
            conn.Open();
            Console.WriteLine("✅ Connected to PostgreSQL successfully!");
            Console.WriteLine($"PostgreSQL version: {conn.ServerVersion}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Database connection failed: {ex.Message}");
            return false;
        }
    }
}