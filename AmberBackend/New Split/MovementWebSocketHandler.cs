using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class MovementWebSocketHandler
{
    private readonly MovementService _movementService;

    public MovementWebSocketHandler(MovementService movementService)
    {
        _movementService = movementService;
    }

    // Strongly-typed handler the message layer should call via a wrapper
    public async Task HandleTileClick(WebSocket ws, string playerId, TilePosition target)
    {
        var state = _movementService.GetEntityState(playerId);
        if (state == null) return;

        // TODO: replace with your A* over server tilemaps
        List<TilePosition> path = CalculatePath(
            state.NextTargetCell ?? state.CurrentCell,
            target
        );

        _movementService.RequestMove(playerId, path);

        var response = new
        {
            type = "path",
            playerId,
            path
        };

        string json = JsonConvert.SerializeObject(response);
        byte[] buffer = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    // Placeholder path (cardinal-first straight line). Replace with A*.
    private List<TilePosition> CalculatePath(TilePosition start, TilePosition target)
    {
        var path = new List<TilePosition>();

        int x = start.X;
        int y = start.Y;

        // Horizontal first, then vertical (prevents diagonals)
        while (x != target.X)
        {
            x += Math.Sign(target.X - x);
            path.Add(new TilePosition { X = x, Y = y });
        }
        while (y != target.Y)
        {
            y += Math.Sign(target.Y - y);
            path.Add(new TilePosition { X = x, Y = y });
        }

        return path;
    }
}
