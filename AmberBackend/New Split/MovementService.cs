using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

public struct TilePosition { public int X; public int Y; }

public enum MovementStatus { Idle, Moving }

public class EntityMovementState
{
    public TilePosition CurrentCell { get; set; }
    public TilePosition? NextTargetCell { get; set; } = null;
    public Queue<TilePosition> QueuedPath { get; } = new();
    public MovementStatus Status { get; set; } = MovementStatus.Idle;
    public float Speed { get; set; } = 4f;          // tiles/sec (tune)
    public float ProgressToNext { get; set; } = 0f; // 0..1
}

public class MovementService
{
    private readonly ConcurrentDictionary<string, EntityMovementState> _entities = new();

    public void Tick(float dt)
    {
        foreach (var kvp in _entities)
        {
            var state = kvp.Value;
            if (!state.NextTargetCell.HasValue) continue;

            state.ProgressToNext += state.Speed * dt; // Speed in tiles/sec

            while (state.ProgressToNext >= 1f) // support overshoot on big dt
            {
                state.ProgressToNext -= 1f;

                state.CurrentCell = state.NextTargetCell.Value;

                if (state.QueuedPath.Count > 0)
                {
                    state.NextTargetCell = state.QueuedPath.Dequeue();
                    state.Status = MovementStatus.Moving;
                }
                else
                {
                    state.NextTargetCell = null;
                    state.Status = MovementStatus.Idle;
                    state.ProgressToNext = 0f;
                    break;
                }
            }
        }
    }

    public void RegisterEntity(string playerId, TilePosition spawnCell, float speed = 6f)
    {
        var state = new EntityMovementState
        {
            CurrentCell = spawnCell,
            Status = MovementStatus.Idle,
            Speed = speed
        };
        _entities[playerId] = state;
    }

    public void RequestMove(string playerId, List<TilePosition> newPath)
    {
        if (!_entities.TryGetValue(playerId, out var state)) return;
        if (newPath == null || newPath.Count == 0) return;

        // start from in-flight step if present
        var pathStart = state.NextTargetCell ?? state.CurrentCell;
        // newPath must already begin at pathStart (ensure in the caller/pathfinder)

        state.QueuedPath.Clear();
        foreach (var p in newPath) state.QueuedPath.Enqueue(p);

        if (!state.NextTargetCell.HasValue && state.QueuedPath.Count > 0)
        {
            state.NextTargetCell = state.QueuedPath.Dequeue();
            state.Status = MovementStatus.Moving;
            state.ProgressToNext = 0f;
        }
    }

    public EntityMovementState GetEntityState(string playerId)
        => _entities.TryGetValue(playerId, out var s) ? s : null;

    public List<string> GetCellOccupants(TilePosition cell)
        => _entities.Where(kv =>
               (kv.Value.CurrentCell.X == cell.X && kv.Value.CurrentCell.Y == cell.Y) ||
               (kv.Value.NextTargetCell.HasValue &&
                kv.Value.NextTargetCell.Value.X == cell.X &&
                kv.Value.NextTargetCell.Value.Y == cell.Y))
           .Select(kv => kv.Key).ToList();

    public List<EntitySnapshot> GetSnapshot(IEnumerable<string> playerIds)
    {
        var list = new List<EntitySnapshot>();
        foreach (var id in playerIds)
        {
            if (_entities.TryGetValue(id, out var s))
            {
                list.Add(new EntitySnapshot
                {
                    playerId = id,
                    x = s.CurrentCell.X,
                    y = s.CurrentCell.Y,
                    status = s.Status.ToString()
                });
            }
        }
        return list;
    }
}

public class EntitySnapshot
{
    public string playerId { get; set; }
    public int x { get; set; }
    public int y { get; set; }
    public string status { get; set; } // "Idle" | "Moving"
}