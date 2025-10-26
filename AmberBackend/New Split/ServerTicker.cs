using System;
using System.Threading;
using System.Threading.Tasks;

public static class ServerTicker
{
    public static async Task RunAsync(MovementService movement, int ticksPerSecond, CancellationToken ct)
    {
        var dt = 1f / ticksPerSecond;
        var delay = TimeSpan.FromMilliseconds(1000.0 / ticksPerSecond);

        while (!ct.IsCancellationRequested)
        {
            movement.Tick(dt);
            try { await Task.Delay(delay, ct); }
            catch (TaskCanceledException) { break; }
        }
    }
}
