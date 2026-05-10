using AmberBackend.Movement;
using System.Collections.Generic;

public class NPCService
{
    private readonly Dictionary<string, NPC> _npcs = new Dictionary<string, NPC>();
    private readonly HashSet<string> _disabledNpcs = new HashSet<string>();
    private readonly TilemapRepository _tilemaps;
    private readonly GridAStarPathfinder _pathfinder;

    public event System.Action<string, TilePosition, TilePosition, float> OnNpcMove;

    public NPCService(TilemapRepository tilemaps, GridAStarPathfinder pathfinder)
    {
        _tilemaps = tilemaps;
        _pathfinder = pathfinder;
    }

    public void SpawnNpc(string npcId, TilePosition startPosition, List<TilePosition> patrolPath, float speed)
    {
        var npc = new NPC
        {
            NpcId = npcId,
            CurrentPosition = startPosition,
            PatrolPath = patrolPath,
            Speed = speed,
            CurrentPathIndex = 0
        };

        _npcs[npcId] = npc;
        System.Console.WriteLine($"[NPCService] Spawned NPC: {npcId}");
    }

    public void DisableNpc(string npcId)
    {
        _disabledNpcs.Add(npcId);
        System.Console.WriteLine($"[NPCService] Disabled patrol for {npcId}");
    }

    public void EnableNpc(string npcId)
    {
        _disabledNpcs.Remove(npcId);
        System.Console.WriteLine($"[NPCService] Enabled patrol for {npcId}");
    }

    public void Tick(float deltaTime)
    {
        foreach (var kvp in _npcs)
        {
            var npcId = kvp.Key;
            var npc = kvp.Value;

            // Skip disabled NPCs (AI-controlled)
            if (_disabledNpcs.Contains(npcId))
                continue;

            // Skip if no patrol path
            if (npc.PatrolPath == null || npc.PatrolPath.Count == 0)
                continue;

            // Patrol logic (simple)
            npc.MoveTimer += deltaTime;
            if (npc.MoveTimer >= 1f / npc.Speed)
            {
                npc.MoveTimer = 0f;

                var currentWaypoint = npc.PatrolPath[npc.CurrentPathIndex];

                if (npc.CurrentPosition.X == currentWaypoint.X &&
                    npc.CurrentPosition.Y == currentWaypoint.Y)
                {
                    // Reached waypoint, move to next
                    npc.CurrentPathIndex = (npc.CurrentPathIndex + 1) % npc.PatrolPath.Count;
                }
                else
                {
                    // Move toward waypoint
                    var nextPos = GetNextPosition(npc.CurrentPosition, currentWaypoint);
                    OnNpcMove?.Invoke(npcId, npc.CurrentPosition, nextPos, 1f / npc.Speed);
                    npc.CurrentPosition = nextPos;
                }
            }
        }
    }

    private TilePosition GetNextPosition(TilePosition from, TilePosition to)
    {
        int dx = System.Math.Sign(to.X - from.X);
        int dy = System.Math.Sign(to.Y - from.Y);

        if (dx != 0)
            return new TilePosition(from.X + dx, from.Y);
        else if (dy != 0)
            return new TilePosition(from.X, from.Y + dy);
        else
            return from;
    }

    private class NPC
    {
        public string NpcId { get; set; }
        public TilePosition CurrentPosition { get; set; }
        public List<TilePosition> PatrolPath { get; set; }
        public int CurrentPathIndex { get; set; }
        public float Speed { get; set; }
        public float MoveTimer { get; set; }
    }
}