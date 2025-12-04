using System;
using System.Threading;
using System.Threading.Tasks;

// EXAMPLE: How to wire up your server startup with timestamp-based system
// This is NOT a complete file - adapt to your existing Program.cs or Startup.cs

public class ServerStartupExample_Timestamp
{
    public static async Task Main(string[] args)
    {
        var cts = new CancellationTokenSource();

        // Initialize services
        var tilemapRepository = new TilemapRepository("Resources/Tilemaps");
        var pathfinder = new GridAStarPathfinder(tilemapRepository);
        var playerService = new PlayerService();
        var movementService = new MovementService(); // Now tracks server uptime
        var movementWsHandler = new MovementWebSocketHandler(movementService, pathfinder);
        var messageHandler = new MessageHandlerService(playerService, movementService, movementWsHandler);

        // ✅ IMPORTANT: Pass movementService to WebSocketServer for time sync
        var wsServer = new WebSocketServer(messageHandler, movementService);

        var npcService = new NPCService(movementService, pathfinder);
        // ✅ Event now has 5 parameters (added timestamp)
        movementService.OnSendMoveCommand += async (playerId, from, to, duration, timestamp) =>
        {
            await wsServer.SendMoveCommandToPlayer(playerId, from, to, duration, timestamp);
        };

        // Spawn some NPCs
        npcService.SpawnPatrolNPC("npc_guard_1", "Guard Alpha",
            new TilePosition { X = 0, Y = 0 },
            new TilePosition { X = 5, Y = 0 },
            speed: 2f);

        npcService.SpawnPatrolNPC("npc_guard_2", "Guard Beta",
            new TilePosition { X = 0, Y = 3 },
            new TilePosition { X = 5, Y = 3 },
            speed: 2f);

        Console.WriteLine("=== Timestamp-Based Movement System ===");
        Console.WriteLine("Server sends commands with timestamps");
        Console.WriteLine("Clients automatically adjust speed based on latency");
        Console.WriteLine("No manual buffer management needed!");
        Console.WriteLine("======================================");

        // Start server tasks
        var wsTask = wsServer.StartAsync(cts.Token);

        // ✅ Recommended: 10-20 Hz tick rate is sufficient
        // Lower tick rate = less bandwidth, still smooth with timestamps
        var tickTask = ServerTicker.RunAsync(movementService, ticksPerSecond: 10, cts.Token);

        // Also tick NPCs (for patrol logic)
        var npcTickTask = Task.Run(async () =>
        {
            var lastTime = DateTime.UtcNow;
            while (!cts.Token.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;
                var currentTime = (float)now.TimeOfDay.TotalSeconds;

                npcService.Tick(currentTime);

                await Task.Delay(100, cts.Token); // 10 Hz
            }
        }, cts.Token);

        Console.WriteLine("Server started. Press Ctrl+C to stop.");
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        await Task.WhenAll(wsTask, tickTask, npcTickTask);
    }
}