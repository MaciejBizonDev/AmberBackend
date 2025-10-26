using System;
using System.Threading;
using System.Threading.Tasks;

public class Program
{
    public static async Task Main()
    {
        var movement = new MovementService();
        var tilemaps = new TilemapRepository("Resources/Tilemaps");
        var pathfinder = new GridAStarPathfinder(tilemaps);
        var players = new PlayerService();
        var moveHandler = new MovementWebSocketHandler(movement, pathfinder);
        var handlers = new MessageHandlerService(players, movement, moveHandler);
        var server = new WebSocketServer(handlers);

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

        // 20 ticks/sec authoritative movement
        var tickerTask = ServerTicker.RunAsync(movement, 20, cts.Token);
        await server.StartAsync(cts.Token);
        await tickerTask;
        RunGameLoopAsync(movementService: movement, token: cts.Token).Wait();
    }

    // Program.cs — add this helper and call it from Main()
    private static async Task RunGameLoopAsync(MovementService movementService, CancellationToken token)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        double last = 0.0;
        const double TICK_HZ = 20.0; // 20 ticks/sec
        var tickDelay = TimeSpan.FromSeconds(1.0 / TICK_HZ);

        while (!token.IsCancellationRequested)
        {
            double now = sw.Elapsed.TotalSeconds;
            float dt = (float)(now - last);
            last = now;

            movementService.Tick(dt);

            try { await Task.Delay(tickDelay, token); }
            catch (TaskCanceledException) { break; }
        }
    }

}
