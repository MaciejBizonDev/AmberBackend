using System;
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
    public float Speed { get; set; } = 4f;

    // Command-based movement (players only)
    public bool WaitingForAcknowledgment { get; set; } = false;
    public float TimeSinceLastCommand { get; set; } = 0f;
    public float CommandInterval => 1f / Speed;

    // Timestamp tracking
    public double LastCommandSentTime { get; set; } = 0.0;
}

public class MovementService
{
    private readonly ConcurrentDictionary<string, EntityMovementState> _entities = new();

    // Server uptime for timestamps (seconds since server start)
    private double _serverUptime = 0.0;

    // Callback to send move commands to clients (now with timestamp!)
    public event Action<string, TilePosition, TilePosition, float, double> OnSendMoveCommand;
    public event Action<string, TilePosition> OnEntityPathComplete;

    /// <summary>
    /// Main tick loop - sends timestamped move commands for both players and NPCs.
    /// Players: Wait for acknowledgment before sending next command.
    /// NPCs: Send commands continuously with precise timestamps.
    /// </summary>
    public void Tick(float dt)
    {
        _serverUptime += dt;

        foreach (var kvp in _entities)
        {
            var playerId = kvp.Key;
            var state = kvp.Value;

            bool isNPC = playerId.StartsWith("npc_");

            if (isNPC)
            {
                // === NPCs: Timestamp-based movement (NO PAUSE between cells) ===

                // Only send next command if we have path and not currently moving
                if (state.Status == MovementStatus.Idle && state.QueuedPath.Count > 0)
                {
                    var fromCell = state.CurrentCell;
                    var toCell = state.QueuedPath.Dequeue();

                    float duration = state.CommandInterval;
                    double timestamp = _serverUptime;

                    // Broadcast command with timestamp
                    OnSendMoveCommand?.Invoke(playerId, fromCell, toCell, duration, timestamp);

                    // Update server state
                    state.CurrentCell = toCell;
                    state.Status = MovementStatus.Moving;
                    state.LastCommandSentTime = timestamp;

                    // Set timer for when this command should complete
                    state.TimeSinceLastCommand = 0f;
                }
                else if (state.Status == MovementStatus.Moving)
                {
                    // Track time to know when movement completes server-side
                    state.TimeSinceLastCommand += dt;

                    if (state.TimeSinceLastCommand >= state.CommandInterval)
                    {
                        // Movement complete on server
                        state.TimeSinceLastCommand = 0f;

                        // ✅ SEND NEXT COMMAND IMMEDIATELY (no pause!)
                        if (state.QueuedPath.Count > 0)
                        {
                            var fromCell = state.CurrentCell;
                            var toCell = state.QueuedPath.Dequeue();

                            float duration = state.CommandInterval;
                            double timestamp = _serverUptime;

                            // Broadcast next command immediately
                            OnSendMoveCommand?.Invoke(playerId, fromCell, toCell, duration, timestamp);

                            state.CurrentCell = toCell;
                            state.Status = MovementStatus.Moving; // Stay moving!
                            state.LastCommandSentTime = timestamp;
                            state.TimeSinceLastCommand = 0f;
                        }
                        else
                        {
                            // Path complete - now go idle
                            state.Status = MovementStatus.Idle;
                            OnEntityPathComplete?.Invoke(playerId, state.CurrentCell);
                        }
                    }
                }
            }
            else
            {
                // === Players: Command-based with acknowledgment ===
                if (state.WaitingForAcknowledgment) continue;
                if (state.QueuedPath.Count == 0) continue;

                state.TimeSinceLastCommand += dt;

                if (state.TimeSinceLastCommand >= state.CommandInterval)
                {
                    var toCell = state.QueuedPath.Dequeue();
                    float duration = state.CommandInterval;
                    double timestamp = _serverUptime;

                    OnSendMoveCommand?.Invoke(playerId, state.CurrentCell, toCell, duration, timestamp);

                    state.NextTargetCell = toCell;
                    state.WaitingForAcknowledgment = true;
                    state.TimeSinceLastCommand = 0f;
                    state.Status = MovementStatus.Moving;
                    state.LastCommandSentTime = timestamp;
                }
            }

            _entities[playerId] = state;
        }
    }

    public void RegisterEntity(string playerId, TilePosition spawnCell, float speed = 4f)
    {
        var state = new EntityMovementState
        {
            CurrentCell = spawnCell,
            Status = MovementStatus.Idle,
            Speed = speed,
            WaitingForAcknowledgment = false,
            TimeSinceLastCommand = 0f,
            LastCommandSentTime = 0.0
        };
        _entities[playerId] = state;

        Console.WriteLine($"[MovementService] Registered {playerId} at {spawnCell} with speed {speed}");
    }

    /// <summary>
    /// Queue a path for command-based sending.
    /// Commands will be sent one tile at a time by Tick().
    /// </summary>
    public void RequestMove(string entityId, List<TilePosition> newPath)
    {
        if (!_entities.TryGetValue(entityId, out var state)) return;
        if (newPath == null || newPath.Count == 0) return;

        bool isNPC = entityId.StartsWith("npc_");

        // Clear old path
        state.QueuedPath.Clear();

        // Determine where we'll be when this new path starts
        TilePosition startCell;

        if (isNPC)
        {
            // NPCs: Always use current cell
            startCell = state.CurrentCell;
        }
        else
        {
            // Players: Use NextTargetCell if waiting for acknowledgment
            startCell = (state.WaitingForAcknowledgment && state.NextTargetCell.HasValue)
                ? state.NextTargetCell.Value
                : state.CurrentCell;
        }

        // Filter out the start cell (pathfinder includes it)
        var filteredPath = newPath.Where(cell =>
            cell.X != startCell.X || cell.Y != startCell.Y
        ).ToList();

        // Enqueue filtered path
        foreach (var cell in filteredPath)
        {
            state.QueuedPath.Enqueue(cell);
        }

        Console.WriteLine($"[MovementService] Queued path for {entityId}: {filteredPath.Count} tiles " +
            $"(start: {startCell}, filtered from {newPath.Count})");

        _entities[entityId] = state;
    }

    /// <summary>
    /// Handle client acknowledgment that movement completed (players only).
    /// ✅ FIXED: Send next command immediately (no pause!)
    /// </summary>
    public void OnClientMovementComplete(string entityId, TilePosition completedCell)
    {
        if (!_entities.TryGetValue(entityId, out var state)) return;

        bool isNPC = entityId.StartsWith("npc_");
        if (isNPC) return; // NPCs don't send acknowledgments

        // Reset waiting state
        state.WaitingForAcknowledgment = false;

        // Verify position
        if (state.NextTargetCell.HasValue &&
            state.NextTargetCell.Value.X == completedCell.X &&
            state.NextTargetCell.Value.Y == completedCell.Y)
        {
            // Position matches - update normally
            state.CurrentCell = completedCell;
            state.NextTargetCell = null;
            state.TimeSinceLastCommand = 0f;
        }
        else
        {
            // Position mismatch - force resync
            Console.WriteLine($"[MovementService] Position mismatch for {entityId}. " +
                $"Expected: {state.NextTargetCell}, Got: {completedCell}. Resyncing.");
            state.CurrentCell = completedCell;
            state.NextTargetCell = null;
            state.TimeSinceLastCommand = 0f;
        }

        // ✅ SEND NEXT COMMAND IMMEDIATELY (no pause!)
        if (state.QueuedPath.Count > 0)
        {
            var toCell = state.QueuedPath.Dequeue();
            float duration = state.CommandInterval;
            double timestamp = _serverUptime;

            // Send next command immediately
            OnSendMoveCommand?.Invoke(entityId, state.CurrentCell, toCell, duration, timestamp);

            state.NextTargetCell = toCell;
            state.WaitingForAcknowledgment = true;
            state.TimeSinceLastCommand = 0f;
            state.Status = MovementStatus.Moving; // Stay moving!
            state.LastCommandSentTime = timestamp;

            Console.WriteLine($"[MovementService] Player {entityId} completed move to {completedCell}, immediately sending next command");
        }
        else
        {
            // Path complete - now go idle
            state.Status = MovementStatus.Idle;
            Console.WriteLine($"[MovementService] Player {entityId} completed move to {completedCell}, path finished");
        }

        _entities[entityId] = state;
    }

    public EntityMovementState? GetEntityState(string playerId)
        => _entities.TryGetValue(playerId, out var s) ? s : null;

    public void RemoveEntity(string playerId)
    {
        _entities.TryRemove(playerId, out _);
        Console.WriteLine($"[MovementService] Removed {playerId}");
    }

    public List<string> GetCellOccupants(TilePosition cell)
        => _entities.Where(kv =>
               (kv.Value.CurrentCell.X == cell.X && kv.Value.CurrentCell.Y == cell.Y) ||
               (kv.Value.NextTargetCell.HasValue &&
                kv.Value.NextTargetCell.Value.X == cell.X &&
                kv.Value.NextTargetCell.Value.Y == cell.Y))
           .Select(kv => kv.Key).ToList();

    /// <summary>
    /// Get snapshot of all entities.
    /// IMPORTANT: NPCs ARE included for initial spawning on clients!
    /// Clients will ignore NPC position updates, but need them to spawn the GameObjects.
    /// </summary>
    public List<EntityStateDto> GetAllEntitiesSnapshot()
    {
        var list = new List<EntityStateDto>();

        foreach (var kvp in _entities)
        {
            var state = kvp.Value;
            list.Add(new EntityStateDto
            {
                playerId = kvp.Key,
                x = state.CurrentCell.X,
                y = state.CurrentCell.Y,
                status = state.Status.ToString()
            });
        }

        return list;
    }

    /// <summary>
    /// Get current server uptime in seconds (for timestamp synchronization).
    /// Clients can use this to calculate time offset.
    /// </summary>
    public double GetServerUptime() => _serverUptime;
}
