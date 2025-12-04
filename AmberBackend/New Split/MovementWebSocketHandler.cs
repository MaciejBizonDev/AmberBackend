using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;

public class MovementWebSocketHandler
{
    private readonly MovementService _movement;
    private readonly GridAStarPathfinder _pathfinder;

    public MovementWebSocketHandler(MovementService movement, GridAStarPathfinder pathfinder)
    {
        _movement = movement;
        _pathfinder = pathfinder;
    }

    public async Task HandleTileClick(WebSocket ws, string playerId, TilePosition target)
    {
        var state = _movement.GetEntityState(playerId);
        if (state == null)
        {
            System.Console.WriteLine($"[MovementHandler] Unknown player: {playerId}");
            return;
        }

        // Build path from correct start
        var start = state.NextTargetCell ?? state.CurrentCell;

        System.Console.WriteLine($"[MovementHandler] {playerId} clicked {target}. Calculating path from {start}");

        var path = _pathfinder.FindPath(start, target);
        if (path == null || path.Count == 0)
        {
            System.Console.WriteLine($"[MovementHandler] No path found for {playerId} to {target}");
            return;
        }

        // NEW: Just queue the path - MovementService.Tick() will send commands
        _movement.RequestMove(playerId, path);

        System.Console.WriteLine($"[MovementHandler] Queued path for {playerId}: {path.Count} tiles");

        // No need to send anything to client here!
        // MovementService.Tick() will send move_command messages one tile at a time

        await Task.CompletedTask; // Keep async signature
    }
}