using AmberBackend.AI;
using AmberBackend.Combat;
using AmberBackend.Movement;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmberBackend.Zones
{
    /// <summary>
    /// A single game zone with its own services and entities.
    /// Isolated from other zones.
    /// </summary>
    public class Zone
    {
        public string ZoneId { get; }
        public string Name { get; }

        // Zone-specific services
        public MovementService MovementService { get; }
        public CombatService CombatService { get; }
        public NPCService NPCService { get; }
        public AIService AIService { get; private set; }

        private readonly NPCStateManager _npcStateManager = new NPCStateManager();

        // Entities in this zone
        private readonly HashSet<string> _playerIds = new HashSet<string>();
        private readonly Dictionary<string, EnemySpawnPoint> _enemySpawns = new Dictionary<string, EnemySpawnPoint>();
        private readonly List<ZonePortal> _portals = new List<ZonePortal>();
        private readonly HashSet<string> _aiEnabledNpcs = new HashSet<string>();

        private CancellationTokenSource _cts;
        private Task _npcUpdateTask;
        private Task _respawnTask;

        private WebSocketServer _webSocketServer;
        private readonly TilemapRepository _tilemaps;
        private readonly GridAStarPathfinder _pathfinder;
        private readonly Dictionary<string, NpcSpawnPoint> _npcSpawns = new Dictionary<string, NpcSpawnPoint>();
        private int _statsBroadcastCounter = 0;

        public Zone(ZoneDefinition definition, TilemapRepository tilemaps, GridAStarPathfinder pathfinder)
        {
            ZoneId = definition.ZoneId;
            Name = definition.Name;

            _tilemaps = tilemaps;
            _pathfinder = pathfinder;

            MovementService = new MovementService(tilemaps);
            NPCService = new NPCService(tilemaps, pathfinder);
            CombatService = new CombatService(tilemaps, MovementService);

            CombatService.OnAbilityResult += HandleAbilityResult;

            NPCService.OnNpcMove += (npcId, from, to, duration) =>
            {
                MovementService.BroadcastNpcMovement(npcId, from, to, duration);
            };

            Console.WriteLine($"[Zone:{ZoneId}] Zone created: {Name}");

            foreach (var npc in definition.NpcSpawns)
            {
                _npcSpawns[npc.SpawnId] = npc;
            }
        }

        /// <summary>
        /// Set the WebSocket broadcaster for this zone (called after construction).
        /// </summary>
        public void SetBroadcaster(WebSocketServer webSocketServer)
        {
            _webSocketServer = webSocketServer;
            MovementService.SetBroadcaster(webSocketServer, ZoneId);
            CombatService.SetBroadcaster(webSocketServer, ZoneId);

            // Create AIService now that we have WebSocketServer
            AIService = new AIService(
                MovementService,
                CombatService,
                _pathfinder,
                ZoneId,
                _npcStateManager,
                webSocketServer
            );

            SpawnAllNpcs();
        }

        /// <summary>
        /// Add a portal to this zone.
        /// </summary>
        public void AddPortal(ZonePortal portal)
        {
            _portals.Add(portal);
            Console.WriteLine($"[Zone:{ZoneId}] Added portal {portal.PortalId} at ({portal.TriggerPosition.X}, {portal.TriggerPosition.Y}) -> {portal.DestinationZoneId}");
        }

        /// <summary>
        /// Check if a position has a portal.
        /// </summary>
        public ZonePortal CheckForPortal(TilePosition position)
        {
            foreach (var portal in _portals)
            {
                if (portal.TriggerPosition.X == position.X && portal.TriggerPosition.Y == position.Y)
                {
                    return portal;
                }
            }
            return null;
        }

        public void LoadEnemySpawns(List<EnemySpawnPoint> spawns)
        {
            _enemySpawns.Clear();
            foreach (var spawn in spawns)
            {
                _enemySpawns[spawn.SpawnId] = spawn;
            }
            Console.WriteLine($"[Zone:{ZoneId}] Loaded {spawns.Count} enemy spawns from DB");
        }

        private void SpawnAllEnemies()
        {
            foreach (var spawn in _enemySpawns.Values)
            {
                SpawnEnemy(spawn);
            }
        }

        private void SpawnEnemy(EnemySpawnPoint spawn)
        {
            var template = spawn.Template;

            NPCService.SpawnNpc(spawn.EnemyId, spawn.SpawnPosition, spawn.PatrolPath, template.Speed);
            MovementService.RegisterEntity(spawn.EnemyId, spawn.SpawnPosition, template.Speed);

            CombatService.RegisterEntity(spawn.EnemyId, new PlayerStats
            {
                PlayerId = spawn.EnemyId,
                Hp = template.MaxHp,
                MaxHp = template.MaxHp,
                Mana = 0,
                MaxMana = 0,
                Level = 1,
                AttackPower = template.AttackPower,
                IsAttackable = true
            });

            if (template.AIBehavior != AIBehaviorType.Passive)
            {
                AIService.RegisterEnemy(spawn.EnemyId, spawn.SpawnPosition, template.AIBehavior, spawn.PatrolPath);
                NPCService.DisableNpc(spawn.EnemyId);
            }

            spawn.IsAlive = true;
            Console.WriteLine($"[Zone:{ZoneId}] Spawned enemy: {spawn.EnemyId} ({template.DisplayName}) at ({spawn.SpawnPosition.X}, {spawn.SpawnPosition.Y}) with {template.MaxHp} HP");
        }

        private void SpawnAllNpcs()
        {
            foreach (var npc in _npcSpawns.Values)
            {
                SpawnNpc(npc);
            }
        }

        private void SpawnNpc(NpcSpawnPoint npc)
        {
            NPCService.SpawnNpc(npc.NpcId, npc.SpawnPosition, npc.PatrolPath, npc.Speed);
            MovementService.RegisterEntity(npc.NpcId, npc.SpawnPosition, npc.Speed);
            CombatService.RegisterEntity(npc.NpcId);

            var stats = CombatService.GetStats(npc.NpcId);
            if (stats != null)
            {
                stats.IsAttackable = false;
            }

            Console.WriteLine($"[Zone:{ZoneId}] Spawned NPC: {npc.NpcId} ({npc.Role}) at ({npc.SpawnPosition.X}, {npc.SpawnPosition.Y})");
        }

        private void UpdateAIPlayerDetection()
        {
            foreach (var playerId in _playerIds)
            {
                var playerPos = MovementService.GetEntityPosition(playerId);
                if (playerPos != null)
                {
                    AIService.NotifyPlayerNearby(playerId, playerPos);
                }
            }
        }

        private void HandleAbilityResult(AbilityResultMessage result)
        {
            if (!result.wasKilled)
                return;

            var spawn = _enemySpawns.Values.FirstOrDefault(s => s.EnemyId == result.targetId);
            if (spawn == null)
                return;

            AIService.UnregisterEnemy(spawn.EnemyId);
            MovementService.RemoveEntity(spawn.EnemyId);
            CombatService.RemoveEntity(spawn.EnemyId);

            spawn.IsAlive = false;
            spawn.DeathTime = DateTime.UtcNow;

            var respawnTime = spawn.Template?.RespawnTime ?? 10f;
            Console.WriteLine($"[Zone:{ZoneId}] Enemy {spawn.EnemyId} killed. Respawning in {respawnTime}s");

            if (_webSocketServer != null)
            {
                _webSocketServer.BroadcastToZone(ZoneId, new
                {
                    type = "entity_died",
                    playerId = spawn.EnemyId
                });
            }
        }

        /// <summary>
        /// Start zone update loop (NPC AI, respawns).
        /// </summary>
        public void Start()
        {
            SpawnAllEnemies();
            _cts = new CancellationTokenSource();
            _npcUpdateTask = Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        TickNonAINPCs();
                        UpdateAIPlayerDetection();
                        AIService.Tick(0.1f);
                        CombatService.TickRegen(0.1f);
                        CheckRespawns();

                        // Broadcast player stats every 1 second (10 ticks × 100ms)
                        _statsBroadcastCounter++;
                        if (_statsBroadcastCounter >= 10)
                        {
                            _statsBroadcastCounter = 0;
                            BroadcastPlayerStats();
                        }

                        await Task.Delay(100, _cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Zone:{ZoneId}] Update loop error: {ex.Message}");
                    }
                }
            }, _cts.Token);

            Console.WriteLine($"[Zone:{ZoneId}] Started update loops");
        }

        private void TickNonAINPCs()
        {
            // TODO: NPCService needs a way to tick only specific NPCs
            // For now, this will tick all - AI movement will override
            NPCService.Tick(0.1f);
        }

        /// <summary>
        /// Call this when walkability changes (door opens/closes, wall destroyed, etc.)
        /// </summary>
        /// <summary>
        /// Call this when walkability changes (door opens/closes, wall destroyed, etc.)
        /// </summary>
        public void BroadcastWalkabilityChange(int x, int y, bool walkable)
        {
            if (_webSocketServer == null)
            {
                Console.WriteLine($"[Zone:{ZoneId}] Cannot broadcast walkability - no WebSocketServer");
                return;
            }

            var message = new
            {
                type = "walkability_delta",
                changes = new[]
                {
            new { x = x, y = y, walkable = walkable }
        }
            };

            _webSocketServer.BroadcastToZone(ZoneId, message);

            Console.WriteLine($"[Zone:{ZoneId}] Broadcast walkability change: ({x},{y}) = {walkable}");
        }

        private void CheckRespawns()
        {
            var now = DateTime.UtcNow;
            foreach (var spawn in _enemySpawns.Values)
            {
                if (spawn.IsAlive)
                    continue;

                var timeSinceDeath = (now - spawn.DeathTime).TotalSeconds;
                var respawnTime = spawn.Template?.RespawnTime ?? 10f;

                if (timeSinceDeath >= respawnTime)
                {
                    Console.WriteLine($"[Zone:{ZoneId}] Respawning {spawn.EnemyId}");
                    SpawnEnemy(spawn);

                    if (_webSocketServer != null)
                    {
                        _webSocketServer.BroadcastToZone(ZoneId, new
                        {
                            type = "entity_spawned",
                            playerId = spawn.EnemyId,
                            x = spawn.SpawnPosition.X,
                            y = spawn.SpawnPosition.Y,
                            entityType = "enemy"
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Stop zone update loop.
        /// </summary>
        public void Stop()
        {
            _cts?.Cancel();
            _npcUpdateTask?.Wait(1000);
            _respawnTask?.Wait(1000);
            Console.WriteLine($"[Zone:{ZoneId}] Stopped");
        }

        /// <summary>
        /// Add player to this zone.
        /// </summary>
        public void AddPlayer(string playerId, TilePosition spawnPosition)
        {
            if (_playerIds.Contains(playerId))
            {
                Console.WriteLine($"[Zone:{ZoneId}] Player {playerId} already in zone");
                return;
            }
            _playerIds.Add(playerId);
            MovementService.RegisterEntity(playerId, spawnPosition, speed: 4f);
            CombatService.RegisterEntity(playerId);

            // NEW: Broadcast to other players in zone
            if (_webSocketServer != null)
            {
                _webSocketServer.BroadcastToZoneExcept(ZoneId, playerId, new
                {
                    type = "entity_spawned",
                    playerId = playerId,
                    x = spawnPosition.X,
                    y = spawnPosition.Y,
                    entityType = "player"
                });
                Console.WriteLine($"[Zone:{ZoneId}] Broadcasted player spawn: {playerId}");
            }

            Console.WriteLine($"[Zone:{ZoneId}] Player {playerId} entered. Total players: {_playerIds.Count}");
        }

        /// <summary>
        /// Remove player from this zone.
        /// </summary>
        public void RemovePlayer(string playerId)
        {
            if (!_playerIds.Contains(playerId))
            {
                Console.WriteLine($"[Zone:{ZoneId}] Player {playerId} not in zone");
                return;
            }

            _playerIds.Remove(playerId);
            MovementService.RemoveEntity(playerId);
            _npcStateManager.ClearPlayerStates(playerId); // NEW - Clean up per-player NPC states

            Console.WriteLine($"[Zone:{ZoneId}] Player {playerId} left. Total players: {_playerIds.Count}");
        }

        /// <summary>
        /// Get snapshot of all entities in zone (for new players joining).
        /// Only includes alive enemies.
        /// </summary>
        public List<EntityStateDto> GetSnapshot()
        {
            var snapshot = MovementService.GetAllEntitiesSnapshot();

            var enemyIds = _enemySpawns.Values
                .Select(s => s.EnemyId)
                .ToHashSet();

            var aliveEnemyIds = _enemySpawns.Values
                .Where(s => s.IsAlive)
                .Select(s => s.EnemyId)
                .ToHashSet();

            var npcIds = _npcSpawns.Values
                .Select(s => s.NpcId)
                .ToHashSet();

            var filtered = snapshot.Where(e =>
                _playerIds.Contains(e.playerId) ||
                aliveEnemyIds.Contains(e.playerId) ||
                npcIds.Contains(e.playerId)
            ).ToList();

            foreach (var entity in filtered)
            {
                if (_playerIds.Contains(entity.playerId))
                    entity.entityType = "player";
                else if (enemyIds.Contains(entity.playerId))
                    entity.entityType = "enemy";
                else if (npcIds.Contains(entity.playerId))
                    entity.entityType = "npc";
                else
                    entity.entityType = "unknown";
            }

            return filtered;
        }

        public bool HasPlayer(string playerId)
        {
            return _playerIds.Contains(playerId);
        }

        public int PlayerCount => _playerIds.Count;

        private void BroadcastPlayerStats()
        {
            if (_webSocketServer == null) return;

            foreach (var playerId in _playerIds)
            {
                var stats = CombatService.GetStats(playerId);
                if (stats == null) continue;

                // Only broadcast if not full HP or full mana (to save bandwidth)
                if (stats.Hp >= stats.MaxHp && stats.Mana >= stats.MaxMana) continue;

                _webSocketServer.BroadcastToZone(ZoneId, new
                {
                    type = "stats_update",
                    playerId = playerId,
                    hp = stats.Hp,
                    maxHp = stats.MaxHp,
                    mana = stats.Mana,
                    maxMana = stats.MaxMana,
                    level = stats.Level
                });
            }
        }
    }
}

