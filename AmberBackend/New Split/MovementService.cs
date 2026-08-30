using AmberBackend.Movement;
using AmberBackend.Zones;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;

public class MovementService
{
    private readonly TilemapRepository _tilemaps;
    private readonly Dictionary<string, EntityState> _entities = new();
    private WebSocketServer _webSocketServer;
    private string _zoneId;

    public event Action<string, TilePosition, TilePosition, float> OnEntityMove;
    public event Action<string, TilePosition, string> OnPositionCorrected;
    public event Action<string> OnEntityRemoved;
    private readonly Dictionary<string, Direction> _entityFacing = new();
    public MovementService(TilemapRepository tilemaps)
    {
        _tilemaps = tilemaps;
    }

    public void SetBroadcaster(WebSocketServer webSocketServer, string zoneId)
    {
        _webSocketServer = webSocketServer;
        _zoneId = zoneId;
    }

    public void RegisterEntity(string entityId, TilePosition position, float speed)
    {
        _entities[entityId] = new EntityState
        {
            EntityId = entityId,
            Position = position,
            Speed = speed,
            Status = "idle"
        };
        _entityFacing[entityId] = Direction.Down;
        Console.WriteLine($"[MovementService] Registered {entityId} at ({position.X}, {position.Y})");
    }

    public void RemoveEntity(string entityId)
    {
        if (_entities.Remove(entityId))
        {
            _entityFacing.Remove(entityId);
            Console.WriteLine($"[MovementService] Removed entity {entityId}");
            OnEntityRemoved?.Invoke(entityId);
        }
    }

    public TilePosition GetEntityPosition(string entityId)
    {
        return _entities.TryGetValue(entityId, out var state) ? state.Position : null;
    }

    public void OnPositionUpdate(string entityId, TilePosition newPosition)
    {
        if (!_entities.TryGetValue(entityId, out var state))
            return;

        if (!IsWalkable(newPosition))
        {
            Console.WriteLine($"[MovementService] Position correction: {entityId} tried to move to unwalkable {newPosition}");
            OnPositionCorrected?.Invoke(entityId, state.Position, "unwalkable_tile");

            if (_webSocketServer != null && !string.IsNullOrEmpty(_zoneId))
            {
                _webSocketServer.SendToPlayer(entityId, new
                {
                    type = "position_correction",
                    playerId = entityId,
                    x = state.Position.X,
                    y = state.Position.Y,
                    reason = "unwalkable_tile"
                });
            }
            return;
        }

        var fromPosition = state.Position;
        state.Position = newPosition;

        // NEW: Broadcast to other players in zone
        if (_webSocketServer != null && !string.IsNullOrEmpty(_zoneId))
        {
            float duration = 1f / state.Speed; // Match client move duration

            _webSocketServer.BroadcastToZoneExcept(_zoneId, entityId, new
            {
                type = "move_command",
                playerId = entityId,
                fromX = fromPosition.X,
                fromY = fromPosition.Y,
                toX = newPosition.X,
                toY = newPosition.Y,
                duration = duration
            });
        }
    }

    public void BroadcastNpcMovement(string npcId, TilePosition from, TilePosition to, float duration)
    {
        OnEntityMove?.Invoke(npcId, from, to, duration);

        if (_webSocketServer != null && !string.IsNullOrEmpty(_zoneId))
        {
            _webSocketServer.BroadcastToZone(_zoneId, new
            {
                type = "move_command",
                playerId = npcId,
                fromX = from.X,
                fromY = from.Y,
                toX = to.X,
                toY = to.Y,
                duration = duration
            });
        }
    }

    public List<EntityStateDto> GetAllEntitiesSnapshot()
    {
        var snapshot = new List<EntityStateDto>();
        foreach (var kvp in _entities)
        {
            var state = kvp.Value;
            snapshot.Add(new EntityStateDto
            {
                playerId = state.EntityId,
                x = state.Position.X,
                y = state.Position.Y,
                status = state.Status
            });
        }
        return snapshot;
    }

    // NEW: Get walkability data for client sync
    public WalkabilityData GetWalkabilityData()
    {
        return _tilemaps.GetWalkabilityData();
    }

    private bool IsWalkable(TilePosition position)
    {
        return _tilemaps.IsWalkable(position);
    }

    public Direction GetEntityFacing(string entityId)
    {
        return _entityFacing.GetValueOrDefault(entityId, Direction.Down);
    }

    public void BroadcastEntityTurn(string entityId, Direction direction)
    {
        if (_webSocketServer != null && !string.IsNullOrEmpty(_zoneId))
        {
            _webSocketServer.BroadcastToZoneExcept(_zoneId, entityId, new
            {
                type = "entity_turned",
                playerId = entityId,
                direction = direction.ToString().ToLower()
            });

            //Console.WriteLine($"[MovementService] Broadcasted {entityId} turn to {direction}");
        }
    }

    public void SetEntityFacing(string entityId, Direction facing)
    {
        _entityFacing[entityId] = facing;
        //Console.WriteLine($"[MovementService] {entityId} now facing {facing}");
    }

    private class EntityState
    {
        public string EntityId { get; set; }
        public TilePosition Position { get; set; }
        public float Speed { get; set; }
        public string Status { get; set; }
    }

    public enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }
}