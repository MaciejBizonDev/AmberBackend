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
            // Authoritative: use current positions from MovementService, not what caller claims
            var actualSourcePos = _movementService.GetEntityPosition(sourceId);
            var actualTargetPos = _movementService.GetEntityPosition(targetId);

            if (actualSourcePos == null || actualTargetPos == null)
                return;

            sourcePos = actualSourcePos;
            targetPos = actualTargetPos;

            var ability = GetAbilityDefinition(abilityId);
            if (ability == null)
            {
                Console.WriteLine($"[Combat] Unknown ability: {abilityId}");
                return;
            }

            if (!_entityStats.TryGetValue(sourceId, out var sourceStats))
                return;

            if (IsOnCooldown(sourceId, abilityId))
                return;

            if (sourceStats.Mana < ability.ManaCost)
                return;

            var context = new AbilityContext
            {
                SourceId = sourceId,
                TargetId = targetId,
                SourcePosition = sourcePos,
                TargetPosition = targetPos,
                SourceStats = sourceStats
            };

            foreach (var step in ability.Steps)
            {
                bool continueExecution = step.Execute(context, this);
                if (!continueExecution)
                    return;
            }

            sourceStats.Mana = Math.Max(0, sourceStats.Mana - ability.ManaCost);
            StartCooldown(sourceId, abilityId, ability.Cooldown);

            // Broadcast results
            foreach (var affectedId in context.AffectedEntities)
            {
                var targetStats = GetStats(affectedId);
                if (targetStats == null)
                    continue;

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
                    _webSocketServer.BroadcastToZone(_zoneId, result);

                // One clean line per affected entity
                var outcome = targetStats.IsDead ? " [KILLED]" : "";
                Console.WriteLine($"[Combat] {sourceId} -> {affectedId}: {abilityId} {context.DamageAmount}dmg (HP {targetStats.Hp}/{targetStats.MaxHp}){outcome}");
            }

            // Broadcast stats updates
            if (_webSocketServer != null && !string.IsNullOrEmpty(_zoneId))
            {
                foreach (var affectedId in context.AffectedEntities)
                {
                    var targetStats = GetStats(affectedId);
                    if (targetStats == null)
                        continue;

                    _webSocketServer.BroadcastToZone(_zoneId, new StatsUpdateMessage
                    {
                        playerId = affectedId,
                        hp = targetStats.Hp,
                        maxHp = targetStats.MaxHp,
                        mana = targetStats.Mana,
                        maxMana = targetStats.MaxMana,
                        level = targetStats.Level
                    });
                }
            }
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

        public void RemoveEntity(string entityId)
        {
            _entityStats.Remove(entityId);
            _cooldowns.Remove(entityId);
            Console.WriteLine($"[CombatService] Removed entity {entityId}");
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

        public void TickRegen(float deltaSeconds)
        {
            foreach (var stats in _entityStats.Values)
            {
                if (stats.IsDead) continue;

                // HP regen: 1% of max HP per second
                int hpRegen = Math.Max(1, (int)(stats.MaxHp * 0.01f * deltaSeconds));
                if (stats.Hp < stats.MaxHp)
                {
                    stats.Hp = Math.Min(stats.MaxHp, stats.Hp + hpRegen);
                }

                // Mana regen: 5% of max mana per second
                int manaRegen = Math.Max(1, (int)(stats.MaxMana * 0.05f * deltaSeconds));
                if (stats.Mana < stats.MaxMana)
                {
                    stats.Mana = Math.Min(stats.MaxMana, stats.Mana + manaRegen);
                }
            }
        }
    }
}