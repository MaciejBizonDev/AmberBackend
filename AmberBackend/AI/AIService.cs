using AmberBackend.AI.Actions;
using AmberBackend.AI.Composites;
using AmberBackend.AI.Conditions;
using AmberBackend.Combat;
using AmberBackend.Movement;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AmberBackend.AI
{
    public class AIService
    {
        private readonly Dictionary<string, AIController> _aiControllers = new Dictionary<string, AIController>();
        private readonly MovementService _movementService;
        private readonly CombatService _combatService;
        private readonly GridAStarPathfinder _pathfinder;
        private readonly string _zoneId;
        private readonly NPCStateManager _stateManager;
        private readonly WebSocketServer _webSocketServer;

        public AIService(
            MovementService movementService,
            CombatService combatService,
            GridAStarPathfinder pathfinder,
            string zoneId,
            NPCStateManager stateManager,
            WebSocketServer webSocketServer)
        {
            _movementService = movementService;
            _combatService = combatService;
            _pathfinder = pathfinder;
            _zoneId = zoneId;
            _stateManager = stateManager;
            _webSocketServer = webSocketServer;
        }

        public void RegisterEnemy(
            string enemyId,
            TilePosition spawnPosition,
            AIBehaviorType behaviorType,
            List<TilePosition> patrolPath = null)
        {
            var context = new AIContext
            {
                EntityId = enemyId,
                CurrentPosition = spawnPosition,
                SpawnPosition = spawnPosition,
                PatrolPath = patrolPath,
                MovementService = _movementService,
                CombatService = _combatService,
                Pathfinder = _pathfinder,
                StateManager = _stateManager,
                WebSocketServer = _webSocketServer,
                ZoneId = _zoneId,
                AggroRange = 5,
                AttackRange = 1,
                FleeHealthThreshold = 0.2f
            };

            ConfigureAbilities(context, behaviorType);
            var behaviorTree = CreateBehaviorTree(behaviorType);
            var controller = new AIController(enemyId, behaviorTree, context);

            _aiControllers[enemyId] = controller;

            Console.WriteLine($"[AIService:{_zoneId}] Registered AI for {enemyId} ({behaviorType})");
        }

        public void UnregisterEnemy(string enemyId)
        {
            if (_aiControllers.Remove(enemyId))
            {
                _stateManager.ClearNPCStates(enemyId);
                Console.WriteLine($"[AIService:{_zoneId}] Unregistered AI for {enemyId}");
            }
        }

        public void Tick(float deltaTime)
        {
            foreach (var controller in _aiControllers.Values)
            {
                try
                {
                    controller.Tick(deltaTime);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AIService:{_zoneId}] Error ticking {controller.EntityId}: {ex.Message}");
                }
            }
        }

        public void SetTarget(string enemyId, string targetPlayerId, TilePosition targetPosition)
        {
            if (_aiControllers.TryGetValue(enemyId, out var controller))
            {
                controller.Context.TargetPlayerId = targetPlayerId;
                controller.Context.TargetPosition = targetPosition;
                Console.WriteLine($"[AIService:{_zoneId}] {enemyId} acquired target {targetPlayerId}");
            }
        }

        public void NotifyPlayerNearby(string playerId, TilePosition playerPosition)
        {
            foreach (var controller in _aiControllers.Values)
            {
                // Skip if AI already has a target
                if (!string.IsNullOrEmpty(controller.Context.TargetPlayerId))
                    continue;

                // Check if player is in aggro range
                int distance = Math.Abs(playerPosition.X - controller.Context.CurrentPosition.X) +
                              Math.Abs(playerPosition.Y - controller.Context.CurrentPosition.Y);

                if (distance <= controller.Context.AggroRange)
                {
                    SetTarget(controller.EntityId, playerId, playerPosition);
                }
            }
        }

        private void ConfigureAbilities(AIContext context, AIBehaviorType behaviorType)
        {
            switch (behaviorType)
            {
                case AIBehaviorType.MeleeAggressive:
                    context.Abilities.Add(new AIAbility("basic_attack", AbilityDefinition.BasicAttack));
                    context.Abilities.Add(new AIAbility("power_strike", AbilityDefinition.PowerStrike));
                    break;

                case AIBehaviorType.RangedKiter:
                    context.Abilities.Add(new AIAbility("basic_attack", AbilityDefinition.BasicAttack));
                    break;

                case AIBehaviorType.Merchant:
                case AIBehaviorType.QuestGiver:
                case AIBehaviorType.Critter:
                case AIBehaviorType.PassivePatrol:
                case AIBehaviorType.Passive:
                    // No combat abilities
                    break;
            }
        }

        private BehaviorNode CreateBehaviorTree(AIBehaviorType behaviorType)
        {
            return behaviorType switch
            {
                AIBehaviorType.MeleeAggressive => CreateMeleeAggressiveTree(),
                AIBehaviorType.Merchant => CreateMerchantTree(),
                AIBehaviorType.QuestGiver => CreateQuestGiverTree(),
                AIBehaviorType.Critter => CreateCritterTree(),
                AIBehaviorType.PassivePatrol => CreatePassivePatrolTree(),
                AIBehaviorType.Passive => CreatePassiveTree(),
                _ => CreatePassiveTree()
            };
        }

        private BehaviorNode CreateMeleeAggressiveTree()
        {
            return new Selector(
                // Combat behaviors
                new Sequence(
                    new IsHealthLow(0.2f),
                    new FleeToSpawn()
                ),
                new Sequence(
                    new HasTarget(),
                    new IsPlayerInRange(1),
                    new IsHealthLow(0.5f),
                    new IsAbilityReady("power_strike"),
                    new UseAbility("power_strike")
                ),
                new Sequence(
                    new HasTarget(),
                    new IsPlayerInRange(1),
                    new IsAbilityReady("basic_attack"),
                    new UseAbility("basic_attack")
                ),
                new Sequence(
                    new HasTarget(),
                    new IsPlayerInRange(10),
                    new ChaseTarget()
                ),

                // Idle: Patrol if path exists
                new PatrolAction()
            );
        }

        private BehaviorNode CreateMerchantTree()
        {
            return new Selector(
                new Sequence(
                    new IsPlayerNearby(3),
                    new FaceNearestPlayer()
                ),
                new IdleAction()
            );
        }

        private BehaviorNode CreateQuestGiverTree()
        {
            return new Selector(
                new Sequence(
                    new HasQuestForPlayer(),
                    new WaveAtPlayer()
                ),
                new Sequence(
                    new IsPlayerNearby(2),
                    new FaceNearestPlayer()
                ),
                new IdleAction()
            );
        }

        private BehaviorNode CreateCritterTree()
        {
            return new Selector(
                new Sequence(
                    new IsPlayerNearby(2),
                    new FleeToSpawn()
                ),
                new WanderAction()
            );
        }

        private BehaviorNode CreatePassivePatrolTree()
        {
            return new PatrolAction();
        }

        private BehaviorNode CreatePassiveTree()
        {
            return new IdleAction();
        }
    }
}

public enum AIBehaviorType
{
    // Combat
    MeleeAggressive,
    RangedKiter,
    Boss,

    // Non-Combat
    Merchant,
    QuestGiver,
    Critter,
    PassivePatrol,
    Passive
}
