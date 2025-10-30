using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

public static class ServerTicker
{
    public static async Task RunAsync(MovementService movement, int ticksPerSecond, CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(1.0 / ticksPerSecond);
        var sw = Stopwatch.StartNew();
        var lastTime = sw.Elapsed.TotalSeconds;

        while (!ct.IsCancellationRequested)
        {
            var currentTime = sw.Elapsed.TotalSeconds;
            var dt = (float)(currentTime - lastTime);
            lastTime = currentTime;

            movement.Tick(dt);

            try 
            { 
                await Task.Delay(delay, ct); 
            }
            catch (TaskCanceledException) 
            { 
                break; 
            }
        }
    }
}
