using AmberBackend.Combat.Steps;
using AmberBackend.Movement;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AmberBackend.Combat
{
    public class CombatService
    {
        private readonly Dictionary<string, PlayerStats> _entityStats = new();
        private readonly Dictionary<string, Dictionary<string, DateTime>> _cooldowns = new();
        private readonly TilemapRepository _tilemaps;
        private readonly MovementService _movementService;

        // Zone-aware broadcasting
        private WebSocketServer _webSocketServer;
        private string _zoneId;

        public event Action<AbilityResultMessage> OnAbilityResult;
        public event Action<StatsUpdateMessage> OnStatsUpdate;
        public event Action<CooldownMessage> OnCooldownStart;

        public CombatService(TilemapRepository tilemaps, MovementService movementService)
        {
            _tilemaps = tilemaps;
            _movementService = movementService;
        }

        public void SetBroadcaster(WebSocketServer webSocketServer, string zoneId)
        {
            _webSocketServer = webSocketServer;
            _zoneId = zoneId;
        }

        public void RegisterEntity(string entityId, PlayerStats stats = null)
        {
            _entityStats[entityId] = stats ?? new PlayerStats { PlayerId = entityId };
            _cooldowns[entityId] = new Dictionary<string, DateTime>();

            Console.WriteLine($"[CombatService] Registered entity {entityId} with {_entityStats[entityId].Hp}/{_entityStats[entityId].MaxHp} HP");
        }

        public PlayerStats GetStats(string entityId)
        {
            return _entityStats.TryGetValue(entityId, out var stats) ? stats : null;
        }

        public void UseAbility(string sourceId, string abilityId, string targetId, TilePosition sourcePos, TilePosition targetPos)
        {
            Console.WriteLine($"[CombatService] ===== ABILITY EXECUTION START =====");
            Console.WriteLine($"[CombatService] Source: {sourceId}");
            Console.WriteLine($"[CombatService] Ability: {abilityId}");
            Console.WriteLine($"[CombatService] Target: {targetId}");
            Console.WriteLine($"[CombatService] Source pos: ({sourcePos.X}, {sourcePos.Y})");
            Console.WriteLine($"[CombatService] Target pos: ({targetPos.X}, {targetPos.Y})");

            var ability = GetAbilityDefinition(abilityId);
            if (ability == null)
            {
                Console.WriteLine($"[CombatService] ERROR: Unknown ability: {abilityId}");
                return;
            }

            Console.WriteLine($"[CombatService] Ability definition found");
            Console.WriteLine($"[CombatService] Steps: {ability.Steps.Count}");
            Console.WriteLine($"[CombatService] Cooldown: {ability.Cooldown}s");
            Console.WriteLine($"[CombatService] Mana cost: {ability.ManaCost}");

            if (!_entityStats.TryGetValue(sourceId, out var sourceStats))
            {
                Console.WriteLine($"[CombatService] ERROR: Source {sourceId} not found in entity stats");
                return;
            }

            Console.WriteLine($"[CombatService] Source HP: {sourceStats.Hp}/{sourceStats.MaxHp}");
            Console.WriteLine($"[CombatService] Source Mana: {sourceStats.Mana}/{sourceStats.MaxMana}");

            if (IsOnCooldown(sourceId, abilityId))
            {
                Console.WriteLine($"[CombatService] FAILED: {sourceId} ability {abilityId} on cooldown");
                return;
            }

            if (sourceStats.Mana < ability.ManaCost)
            {
                Console.WriteLine($"[CombatService] FAILED: {sourceId} not enough mana for {abilityId} (has {sourceStats.Mana}, needs {ability.ManaCost})");
                return;
            }

            Console.WriteLine($"[CombatService] Creating ability context...");

            var context = new AbilityContext
            {
                SourceId = sourceId,
                TargetId = targetId,
                SourcePosition = sourcePos,
                TargetPosition = targetPos,
                SourceStats = sourceStats
            };

            Console.WriteLine($"[CombatService] Executing {ability.Steps.Count} steps...");

            foreach (var step in ability.Steps)
            {
                Console.WriteLine($"[CombatService] Executing step: {step.GetType().Name}");
                bool continueExecution = step.Execute(context, this);

                if (!continueExecution)
                {
                    Console.WriteLine($"[CombatService] Step {step.GetType().Name} halted execution");
                    return;
                }
                Console.WriteLine($"[CombatService] Step {step.GetType().Name} completed successfully");
            }

            Console.WriteLine($"[CombatService] All steps executed. Applying costs and cooldowns...");

            sourceStats.Mana = Math.Max(0, sourceStats.Mana - ability.ManaCost);
            StartCooldown(sourceId, abilityId, ability.Cooldown);

            Console.WriteLine($"[CombatService] Broadcasting results to {context.AffectedEntities.Count} affected entities...");

            foreach (var affectedId in context.AffectedEntities)
            {
                var targetStats = GetStats(affectedId);
                if (targetStats != null)
                {
                    Console.WriteLine($"[CombatService] Affected entity: {affectedId} - Damage: {context.DamageAmount}, Heal: {context.HealAmount}, HP: {targetStats.Hp}/{targetStats.MaxHp}, Killed: {targetStats.IsDead}");

                    var result = new AbilityResultMessage
                    {
                        sourceId = sourceId,
                        targetId = affectedId,
                        abilityId = abilityId,
                        damage = context.DamageAmount,
                        healing = context.HealAmount,
                        newTargetHp = targetStats.Hp,
                        newTargetMaxHp = targetStats.MaxHp,
                        wasKilled = targetStats.IsDead,
                        resultType = "hit"
                    };

                    OnAbilityResult?.Invoke(result);

                    if (_webSocketServer != null && !string.IsNullOrEmpty(_zoneId))
                    {
                        _webSocketServer.BroadcastToZone(_zoneId, result);
                        Console.WriteLine($"[CombatService] Broadcasted ability result to zone {_zoneId}");
                    }
                }
            }

            if (_webSocketServer != null && !string.IsNullOrEmpty(_zoneId))
            {
                foreach (var affectedId in context.AffectedEntities)
                {
                    var targetStats = GetStats(affectedId);
                    if (targetStats != null)
                    {
                        _webSocketServer.BroadcastToZone(_zoneId, new StatsUpdateMessage
                        {
                            playerId = affectedId,
                            hp = targetStats.Hp,
                            maxHp = targetStats.MaxHp,
                            mana = targetStats.Mana,
                            maxMana = targetStats.MaxMana,
                            level = targetStats.Level
                        });
                        Console.WriteLine($"[CombatService] Broadcasted stats update for {affectedId}");
                    }
                }
            }

            Console.WriteLine($"[CombatService] ===== ABILITY EXECUTION COMPLETE =====");
            Console.WriteLine($"[CombatService] Ability {abilityId} complete. Affected {context.AffectedEntities.Count} entities");
        }

        public List<string> GetEntitiesAtCells(List<TilePosition> cells)
        {
            var entities = new List<string>();

            foreach (var cell in cells)
            {
                foreach (var kvp in _entityStats)
                {
                    var entityId = kvp.Key;
                    var pos = _movementService.GetEntityPosition(entityId);

                    if (pos != null && pos.X == cell.X && pos.Y == cell.Y)
                    {
                        entities.Add(entityId);
                    }
                }
            }

            return entities;
        }

        private void StartCooldown(string playerId, string abilityId, float duration)
        {
            var expiresAt = DateTime.UtcNow.AddSeconds(duration);
            _cooldowns[playerId][abilityId] = expiresAt;

            OnCooldownStart?.Invoke(new CooldownMessage
            {
                playerId = playerId,
                abilityId = abilityId,
                duration = duration
            });

            if (_webSocketServer != null && !string.IsNullOrEmpty(_zoneId))
            {
                _webSocketServer.SendToPlayer(playerId, new CooldownMessage
                {
                    playerId = playerId,
                    abilityId = abilityId,
                    duration = duration
                });
            }
        }

        private bool IsOnCooldown(string playerId, string abilityId)
        {
            if (!_cooldowns.TryGetValue(playerId, out var playerCooldowns))
                return false;

            if (!playerCooldowns.TryGetValue(abilityId, out var expiresAt))
                return false;

            return DateTime.UtcNow < expiresAt;
        }

        private AbilityDefinition GetAbilityDefinition(string abilityId)
        {
            return abilityId switch
            {
                "basic_attack" => AbilityDefinition.BasicAttack,
                "power_strike" => AbilityDefinition.PowerStrike,
                "fireball" => AbilityDefinition.Fireball,
                _ => null
            };
        }
    }
}