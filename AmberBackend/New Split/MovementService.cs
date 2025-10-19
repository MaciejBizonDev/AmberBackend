using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

// Represents a grid tile
public struct TilePosition
{
    public int X;
    public int Y;
}

// Entity movement status
public enum MovementStatus
{
    Idle,
    Moving
}

// State for a single entity
public class EntityMovementState
{
    public TilePosition CurrentCell { get; set; }
    public TilePosition? NextTargetCell { get; set; } = null;
    public Queue<TilePosition> QueuedPath { get; private set; } = new();
    public MovementStatus Status { get; set; } = MovementStatus.Idle;
    public float Speed { get; set; } = 1f; // tiles per second
    public float ProgressToNext { get; set; } = 0f; // 0..1
}

// Service to track and update all entities
public class MovementService
{
    private readonly ConcurrentDictionary<string, EntityMovementState> _entities = new();

    // Called every tick (server-side)
    public void Tick(float deltaTime)
    {
        foreach (var kvp in _entities)
        {
            var state = kvp.Value;
            if (state.NextTargetCell.HasValue)
            {
                // Advance progress
                state.ProgressToNext += state.Speed * deltaTime;

                if (state.ProgressToNext >= 1f)
                {
                    // Reached next cell
                    state.CurrentCell = state.NextTargetCell.Value;
                    state.ProgressToNext = 0f;

                    if (state.QueuedPath.Count > 0)
                    {
                        state.NextTargetCell = state.QueuedPath.Dequeue();
                        state.Status = MovementStatus.Moving;
                    }
                    else
                    {
                        state.NextTargetCell = null;
                        state.Status = MovementStatus.Idle;
                    }
                }
            }
        }
    }

    public void RegisterEntity(string playerId, TilePosition spawnCell, float speed = 1f)
    {
        var state = new EntityMovementState
        {
            CurrentCell = spawnCell,
            Status = MovementStatus.Idle,
            Speed = speed
        };
        _entities[playerId] = state;
    }

    // Request a path for the entity
    public void RequestMove(string playerId, List<TilePosition> newPath)
    {
        if (!_entities.TryGetValue(playerId, out var state)) return;

        TilePosition start = state.NextTargetCell ?? state.CurrentCell;

        if (newPath.Count == 0) return;

        // Compute path from current in-flight position
        state.QueuedPath.Clear();
        foreach (var pos in newPath)
            state.QueuedPath.Enqueue(pos);

        // Set first next target if not moving
        if (!state.NextTargetCell.HasValue)
        {
            state.NextTargetCell = state.QueuedPath.Dequeue();
            state.Status = MovementStatus.Moving;
            state.ProgressToNext = 0f;
        }
    }

    public EntityMovementState GetEntityState(string playerId)
    {
        _entities.TryGetValue(playerId, out var state);
        return state;
    }

    public List<string> GetCellOccupants(TilePosition cell)
    {
        return _entities.Where(kvp =>
        {
            var s = kvp.Value;
            if (s.CurrentCell.X == cell.X && s.CurrentCell.Y == cell.Y) return true;
            if (s.NextTargetCell.HasValue && s.NextTargetCell.Value.X == cell.X && s.NextTargetCell.Value.Y == cell.Y) return true;
            return false;
        }).Select(kvp => kvp.Key).ToList();
    }
}
