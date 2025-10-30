using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

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
        if (state == null) return;

        var entityState = state;
        
        // Build path from the correct start (in-flight if any)
        var start = entityState.NextTargetCell ?? entityState.CurrentCell;

        var path = _pathfinder.FindPath(start, target); // Ensure your A* respects walkability
        if (path == null) path = new List<TilePosition>();

        _movement.RequestMove(playerId, path);

        // Don't include the first cell in path sent to client
        // Path starts from NextTargetCell which client is already moving to/at
        var pathToClient = path.Count > 0 ? path.Skip(1).ToList() : path;

        // Re-fetch state to get updated speed
        entityState = _movement.GetEntityState(playerId);
        int tileDurationMs = (int)Math.Round(1000.0 / Math.Max(0.0001, entityState.Speed));

        var response = new
        {
            type = "path",
            playerId,
            path = pathToClient,
            tileDurationMs
        };

        string json = JsonConvert.SerializeObject(response);
        byte[] buffer = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
    }
}
