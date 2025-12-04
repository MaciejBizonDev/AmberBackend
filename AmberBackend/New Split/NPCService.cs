using System;
using System.Collections.Generic;
using System.Linq;

public class NPC
{
    public string Id { get; set; }
    public string Name { get; set; }
    public TilePosition SpawnPosition { get; set; }

    public TilePosition PointA { get; set; }
    public TilePosition PointB { get; set; }
    public TilePosition CurrentTarget { get; set; }

    public float WanderIntervalMin { get; set; } = 3f;
    public float WanderIntervalMax { get; set; } = 8f;
    public float NextWanderTime { get; set; }
}

public class NPCService
{
    private readonly Dictionary<string, NPC> _npcs = new();
    private readonly MovementService _movementService;
    private readonly GridAStarPathfinder _pathfinder;
    private readonly Random _random = new();

    public NPCService(MovementService movementService, GridAStarPathfinder pathfinder)
    {
        _movementService = movementService;
        _pathfinder = pathfinder;
        _movementService.OnEntityPathComplete += OnNPCPathComplete;
    }

    /// <summary>
    /// Spawn an NPC at a position. Server will make it wander automatically.
    /// </summary>
    public void SpawnPatrolNPC(string id, string name, TilePosition pointA, TilePosition pointB, float speed = 2f)
    {
        var npc = new NPC
        {
            Id = id,
            Name = name,
            SpawnPosition = pointA,
            PointA = pointA,
            PointB = pointB,
            CurrentTarget = pointB, // Start by going to point B
            WanderIntervalMin = 0f, // Move immediately
            WanderIntervalMax = 0f,
            NextWanderTime = 0f
        };

        _npcs[id] = npc;

        // Register with MovementService
        _movementService.RegisterEntity(id, pointA, speed);

        Console.WriteLine($"[NPCService] Spawned patrol NPC '{name}' from {pointA} to {pointB}");
    }

    /// <summary>
    /// Update NPCs - called every tick.
    /// Makes idle NPCs pick new random destinations.
    /// </summary>
    public void Tick(float currentTime)
    {
        foreach (var npc in _npcs.Values)
        {
            var state = _movementService.GetEntityState(npc.Id);
            if (state == null) continue;

            // Only act if idle AND no path queued
            // This prevents spamming RequestMove() every tick
            if (state.Status != MovementStatus.Idle) continue;
            if (state.QueuedPath.Count > 0) continue; // Already has a path queued!

            // Check if we've reached the current target
            if (state.CurrentCell.X == npc.CurrentTarget.X &&
                state.CurrentCell.Y == npc.CurrentTarget.Y)
            {
                // Reached target! Switch to other point
                npc.CurrentTarget = (npc.CurrentTarget.X == npc.PointA.X &&
                                    npc.CurrentTarget.Y == npc.PointA.Y)
                    ? npc.PointB
                    : npc.PointA;

                Console.WriteLine($"[NPCService] NPC '{npc.Name}' reached target, switching to {npc.CurrentTarget}");
            }

            // Calculate path to current target
            var path = _pathfinder.FindPath(state.CurrentCell, npc.CurrentTarget);

            if (path != null && path.Count > 0)
            {
                _movementService.RequestMove(npc.Id, path);
                // Only log once per path request, not every tick
                Console.WriteLine($"[NPCService] NPC '{npc.Name}' patrolling to {npc.CurrentTarget} (path: {path.Count} tiles)");
            }
        }
    }

    private void OnNPCPathComplete(string npcId, TilePosition currentCell)
    {
        // Check if this is actually an NPC we manage
        if (!_npcs.TryGetValue(npcId, out var npc)) return;

        var state = _movementService.GetEntityState(npcId);
        if (state == null) return;

        // Check if we've reached the patrol target
        if (state.CurrentCell.X == npc.CurrentTarget.X &&
            state.CurrentCell.Y == npc.CurrentTarget.Y)
        {
            // Reached target! Switch to other point
            npc.CurrentTarget = (npc.CurrentTarget.X == npc.PointA.X &&
                                npc.CurrentTarget.Y == npc.PointA.Y)
                ? npc.PointB
                : npc.PointA;

            Console.WriteLine($"[NPCService] NPC '{npc.Name}' reached target, switching to {npc.CurrentTarget}");
        }

        // Immediately request new path (no delay!)
        var path = _pathfinder.FindPath(state.CurrentCell, npc.CurrentTarget);

        if (path != null && path.Count > 0)
        {
            _movementService.RequestMove(npcId, path);
            Console.WriteLine($"[NPCService] NPC '{npc.Name}' patrolling to {npc.CurrentTarget} (path: {path.Count} tiles)");
        }
        else
        {
            Console.WriteLine($"[NPCService] No path found for NPC '{npc.Name}' from {state.CurrentCell} to {npc.CurrentTarget}");
        }
    }

    public IEnumerable<string> GetAllNPCIds() => _npcs.Keys;

    public void RemoveNPC(string id)
    {
        if (_npcs.Remove(id))
        {
            _movementService.RemoveEntity(id);
            Console.WriteLine($"[NPCService] Removed NPC {id}");
        }
    }
}