using AmberBackend.Movement;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// NPC Service for client-authoritative movement.
/// NPCs still move via server commands (server-authoritative for NPCs).
/// Players use client-authoritative movement.
/// </summary>
public class NPCService
{
    private class NpcState
    {
        public string NpcId;
        public TilePosition CurrentPosition;
        public List<TilePosition> PatrolPath;
        public int CurrentPathIndex;
        public float Speed;
        public float TimeSinceLastMove;
        public float MoveInterval; // Time between moves
    }

    private readonly Dictionary<string, NpcState> _npcs = new();
    private readonly TilemapRepository _tilemaps;
    private readonly GridAStarPathfinder _pathfinder;

    // Event: Server tells clients to move NPC
    public event Action<string, TilePosition, TilePosition, float> OnNpcMove; // npcId, from, to, duration

    public NPCService(TilemapRepository tilemaps, GridAStarPathfinder pathfinder)
    {
        _tilemaps = tilemaps;
        _pathfinder = pathfinder;
    }

    /// <summary>
    /// Spawn an NPC with a patrol path.
    /// </summary>
    public void SpawnNpc(string npcId, TilePosition startPosition, List<TilePosition> patrolPath, float speed = 2f)
    {
        var npc = new NpcState
        {
            NpcId = npcId,
            CurrentPosition = startPosition,
            PatrolPath = patrolPath,
            CurrentPathIndex = 0,
            Speed = speed,
            TimeSinceLastMove = 0f,
            MoveInterval = 1f / speed // e.g., 2 tiles/sec = 0.5s per tile
        };

        _npcs[npcId] = npc;

        Console.WriteLine($"[NPCService] Spawned {npcId} at {startPosition} with {patrolPath.Count} waypoints (speed: {speed} tiles/sec)");
    }

    /// <summary>
    /// Remove an NPC.
    /// </summary>
    public void RemoveNpc(string npcId)
    {
        if (_npcs.Remove(npcId))
        {
            Console.WriteLine($"[NPCService] Removed {npcId}");
        }
    }

    /// <summary>
    /// Update NPCs - call this regularly (e.g., every 100ms).
    /// </summary>
    public void Tick(float deltaTime)
    {
        foreach (var npc in _npcs.Values.ToList())
        {
            npc.TimeSinceLastMove += deltaTime;

            // Time to move?
            if (npc.TimeSinceLastMove >= npc.MoveInterval)
            {
                npc.TimeSinceLastMove = 0f;

                // Get next position in patrol path
                if (npc.PatrolPath == null || npc.PatrolPath.Count == 0)
                    continue;

                var targetPosition = npc.PatrolPath[npc.CurrentPathIndex];

                // Are we already at target? Move to next waypoint
                if (npc.CurrentPosition.X == targetPosition.X &&
                    npc.CurrentPosition.Y == targetPosition.Y)
                {
                    npc.CurrentPathIndex = (npc.CurrentPathIndex + 1) % npc.PatrolPath.Count;
                    targetPosition = npc.PatrolPath[npc.CurrentPathIndex];
                }

                // Calculate path to next waypoint (one tile at a time)
                var path = _pathfinder.FindPath(npc.CurrentPosition, targetPosition);

                if (path != null && path.Count > 1)
                {
                    // Move to next tile in path (path[0] is current position)
                    var nextTile = path[1];

                    // Validate it's adjacent (one tile movement)
                    int distance = Math.Abs(nextTile.X - npc.CurrentPosition.X) +
                                   Math.Abs(nextTile.Y - npc.CurrentPosition.Y);

                    if (distance == 1)
                    {
                        var oldPosition = npc.CurrentPosition;
                        npc.CurrentPosition = nextTile;

                        // Broadcast NPC movement to all clients
                        OnNpcMove?.Invoke(npc.NpcId, oldPosition, nextTile, npc.MoveInterval);

                        Console.WriteLine($"[NPCService] {npc.NpcId} moved: {oldPosition} -> {nextTile}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Get all NPCs for snapshot.
    /// </summary>
    public List<EntityStateDto> GetAllNpcsSnapshot()
    {
        var list = new List<EntityStateDto>();

        foreach (var npc in _npcs.Values)
        {
            list.Add(new EntityStateDto
            {
                playerId = npc.NpcId,
                x = npc.CurrentPosition.X,
                y = npc.CurrentPosition.Y,
                status = "Idle"
            });
        }

        return list;
    }
}