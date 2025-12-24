using AmberBackend.Movement;
using System;
using System.Collections.Generic;

/// <summary>
/// Client-authoritative movement service.
/// Receives position updates from clients and validates them.
/// Broadcasts to other clients and can force corrections.
/// </summary>
public class MovementService
{
    private class EntityState
    {
        public TilePosition CurrentPosition;
        public DateTime LastUpdateTime;
        public float Speed; // tiles per second
        public Queue<TilePosition> RecentPositions = new(); // For cheat detection
    }

    private readonly Dictionary<string, EntityState> _entities = new();
    private readonly TilemapRepository _tilemaps;
    public event Action<string> OnEntityRemoved;
    // Validation settings
    private const float MaxSpeedMultiplier = 1.5f; // Allow 50% faster than normal (lag compensation)
    private const int RecentPositionHistorySize = 10;

    public event Action<string, TilePosition, TilePosition, float> OnEntityMove; // playerId, from, to, duration
    public event Action<string, TilePosition, string> OnPositionCorrected; // playerId, correctedPos, reason

    public MovementService(TilemapRepository tilemaps)
    {
        _tilemaps = tilemaps;
    }

    /// <summary>
    /// Broadcast NPC movement to all clients.
    /// Called by NPCService when an NPC moves.
    /// </summary>
    public void BroadcastNpcMovement(string npcId, TilePosition from, TilePosition to, float duration)
    {
        // Also update the entity position in our tracking
        if (_entities.TryGetValue(npcId, out var state))
        {
            state.CurrentPosition = to;
            state.LastUpdateTime = DateTime.UtcNow;
        }

        // Trigger the event for WebSocketServer to broadcast
        OnEntityMove?.Invoke(npcId, from, to, duration);

        Console.WriteLine($"[MovementService] Broadcasting NPC movement: {npcId} {from} -> {to}");
    }

    public void RegisterEntity(string entityId, TilePosition spawnPosition, float speed = 4f)
    {
        _entities[entityId] = new EntityState
        {
            CurrentPosition = spawnPosition,
            LastUpdateTime = DateTime.UtcNow,
            Speed = speed
        };

        Console.WriteLine($"[MovementService] Registered {entityId} at {spawnPosition} (speed: {speed} tiles/sec)");
    }

    /// <summary>
    /// Handle position update from client (client-authoritative).
    /// Validates and broadcasts to other clients.
    /// </summary>
    public void OnPositionUpdate(string entityId, TilePosition newPosition)
    {
        if (!_entities.TryGetValue(entityId, out var state))
        {
            Console.WriteLine($"[MovementService] Unknown entity: {entityId}");
            return;
        }

        var oldPosition = state.CurrentPosition;
        var now = DateTime.UtcNow;
        var timeDelta = (now - state.LastUpdateTime).TotalSeconds;

        // Validation 1: Check if tile is walkable
        if (!_tilemaps.IsWalkable(newPosition))
        {
            Console.WriteLine($"[MovementService] {entityId} tried to move to unwalkable tile {newPosition}");
            OnPositionCorrected?.Invoke(entityId, oldPosition, "unwalkable_tile");
            return;
        }

        // Validation 2: Check speed (teleport detection)
        int distance = Math.Abs(newPosition.X - oldPosition.X) + Math.Abs(newPosition.Y - oldPosition.Y);
        float maxAllowedDistance = (float)(state.Speed * MaxSpeedMultiplier * timeDelta);

        if (distance > maxAllowedDistance && timeDelta > 0.1) // Give 100ms grace period
        {
            Console.WriteLine($"[MovementService] {entityId} moved too fast! Distance: {distance}, Max allowed: {maxAllowedDistance:F2} (time: {timeDelta:F3}s)");
            OnPositionCorrected?.Invoke(entityId, oldPosition, "speed_hack");
            return;
        }

        // Validation 3: Check teleporting (can't skip tiles)
        if (distance > 1 && timeDelta < 0.5) // Moving >1 tile in <0.5s is suspicious
        {
            Console.WriteLine($"[MovementService] {entityId} possible teleport detected: {oldPosition} -> {newPosition} in {timeDelta:F3}s");
            OnPositionCorrected?.Invoke(entityId, oldPosition, "teleport_detected");
            return;
        }

        // Update accepted
        state.CurrentPosition = newPosition;
        state.LastUpdateTime = now;

        // Track position history for pattern detection
        state.RecentPositions.Enqueue(newPosition);
        if (state.RecentPositions.Count > RecentPositionHistorySize)
        {
            state.RecentPositions.Dequeue();
        }

        // Calculate actual duration based on distance and speed
        float duration = distance / state.Speed;

        // Broadcast movement to other clients
        OnEntityMove?.Invoke(entityId, oldPosition, newPosition, duration);

        Console.WriteLine($"[MovementService] {entityId} moved: {oldPosition} -> {newPosition} (validated, distance: {distance})");
    }

    /// <summary>
    /// Queue a path for an entity (for mouse clicks - server calculates path).
    /// This sends individual move commands one at a time.
    /// </summary>
    public void RequestPath(string entityId, TilePosition target)
    {
        if (!_entities.TryGetValue(entityId, out var state))
        {
            Console.WriteLine($"[MovementService] Unknown entity: {entityId}");
            return;
        }

        // For now, just accept the target and let client handle pathfinding
        // In production, you'd validate the entire path here
        Console.WriteLine($"[MovementService] {entityId} requested path to {target}");

        // Client will move itself, we just validate each step
    }

    public TilePosition GetEntityPosition(string entityId)
    {
        if (_entities.TryGetValue(entityId, out var state))
        {
            return state.CurrentPosition;
        }
        return null;
    }

    public List<EntityStateDto> GetAllEntitiesSnapshot()
    {
        var list = new List<EntityStateDto>();

        foreach (var kvp in _entities)
        {
            var pos = kvp.Value.CurrentPosition;
            list.Add(new EntityStateDto
            {
                playerId = kvp.Key,
                x = pos.X,
                y = pos.Y,
                status = "Idle" // In client-auth, we don't track server-side status
            });
        }

        return list;
    }

    public void RemoveEntity(string entityId)
    {
        if (_entities.Remove(entityId))
        {
            Console.WriteLine($"[MovementService] Removed entity {entityId}");

            // Optional: Broadcast entity removal to other clients
            OnEntityRemoved?.Invoke(entityId);
        }
    }
}