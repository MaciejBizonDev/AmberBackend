using AmberBackend.Combat;
using AmberBackend.Database;
using AmberBackend.Inventory;
using AmberBackend.Movement;
using AmberBackend.Zones;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

public class Program
{
    public static async Task Main(string[] args)
    {
        var cts = new CancellationTokenSource();

        // Database config
        // Read from environment variables with fallback to defaults for local dev
        string dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        int dbPort = int.Parse(Environment.GetEnvironmentVariable("DB_PORT") ?? "5432");
        string dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "mmorpg";
        string dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "gameserver";
        string dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "game123";
        string connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword}";

        var tilemaps = new TilemapRepository("Resources/Tilemaps");
        var pathfinder = new GridAStarPathfinder(tilemaps);

        var database = new PlayerDatabase(dbHost, dbPort, dbName, dbUser, dbPassword);
        var playerService = new PlayerService(database);
        var registrationService = new RegistrationService(connectionString);
        // NEW: Item and inventory services
        var itemDatabase = new ItemDatabase(dbHost, dbPort, dbName, dbUser, dbPassword);
        var inventoryService = new InventoryService(connectionString, itemDatabase);

        var sessionManager = new PlayerSessionManager();
        var enemyRepository = new EnemyRepository(connectionString);
        var zoneRepository = new ZoneRepository(connectionString);
        var abilityRepository = new AbilityRepository(connectionString);
        var zoneManager = new ZoneManager(tilemaps, pathfinder, enemyRepository, zoneRepository);
        var zoneTransitionService = new ZoneTransitionService(zoneManager, sessionManager, playerService);

        // Pass inventoryService to message handler
        var messageHandler = new MessageHandlerService(
            playerService,
            sessionManager,
            zoneManager,
            zoneTransitionService,
            inventoryService,
            registrationService,
            abilityRepository
        );

        var wsServer = new WebSocketServer(
            messageHandler,
            playerService,
            zoneManager,
            sessionManager
        );

        zoneManager.SetWebSocketServer(wsServer);

        // Create both zones
        var zoneDefinitions = zoneRepository.LoadAllZones();
        foreach (var zoneDef in zoneDefinitions)
        {
            zoneManager.CreateZone(zoneDef);  // NPCs + portals loaded inside CreateZone
        }

        database.CleanupOldPlayers(daysInactive: 30);
        Console.WriteLine($"[Database] Total players: {database.GetPlayerCount()}");
        Console.WriteLine($"[Database] Active (7 days): {database.GetActivePlayerCount(7)}");

        Console.WriteLine("=== Zone-Based MMORPG Server ===");
        Console.WriteLine($"Active zones: test_zone, town_1");
        Console.WriteLine($"Portals: test_zone(10,-5) <-> town_1(15,14)");
        Console.WriteLine("====================================");

        var wsTask = wsServer.StartAsync(cts.Token);

        var autoSaveTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(30000, cts.Token);
                    var savedCount = wsServer.SaveAllPlayerPositions();
                    if (savedCount > 0)
                    {
                        Console.WriteLine($"[AutoSave] Saved {savedCount} player positions");
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, cts.Token);

        //Console.WriteLine("Server started on ws:http://0.0.0.0:5000/");
        Console.WriteLine("Auto-save: Every 30 seconds");
        Console.WriteLine("Press Ctrl+C to stop.");

        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\n[Server] Shutting down...");
            var savedCount = wsServer.SaveAllPlayerPositions();
            Console.WriteLine($"[Server] Final save: {savedCount} players");
            zoneManager.StopAll();
            cts.Cancel();
        };

        await Task.WhenAll(wsTask, autoSaveTask);
        Console.WriteLine("Server stopped.");
    }

}