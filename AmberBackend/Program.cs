using System;
using System.Threading;
using System.Threading.Tasks;

public class Program
{
    public static async Task Main()
    {
        // Create server instance (loads tilemaps automatically)
        var pathfinder = new GridAStarPathfinder(new TilemapRepository("Resources/Tilemaps"));
        var server = new WebSocketServer(pathfinder);

        // Handle Ctrl+C to stop server gracefully
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        Console.WriteLine("Starting WebSocket server...");
        try
        {
            await server.StartAsync(cts.Token); // pass the cancellation token
        }
        catch (OperationCanceledException)
        {
            // Expected on Ctrl+C
        }

        Console.WriteLine("Server stopped.");
    }
}
