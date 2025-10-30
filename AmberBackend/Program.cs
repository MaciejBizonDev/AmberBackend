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
        var serverTask = server.StartAsync(cts.Token);
        
        // Wait for either to complete (both run until cancellation)
        await Task.WhenAny(tickerTask, serverTask);
    }
}