using System;
using System.Collections.Generic;

namespace AmberBackend.AI.Conditions
{
    /// <summary>
    /// Check if this NPC has a quest available for any nearby player.
    /// Sets quest markers per-player.
    /// </summary>
    public class HasQuestForPlayer : BehaviorNode
    {
        public override NodeStatus Execute(AIContext context)
        {
            // Get all players in range
            var nearbyPlayers = GetPlayersInRange(context, 5);

            bool hasQuestForAnyPlayer = false;

            foreach (var playerId in nearbyPlayers)
            {
                // Check if player can accept quest
                bool canAcceptQuest = CheckQuestAvailability(context, playerId);

                if (canAcceptQuest)
                {
                    // Show quest marker ONLY to this player
                    context.StateManager.SetQuestMarker(context.EntityId, playerId, true);
                    SendQuestMarkerToPlayer(context, playerId, true);
                    hasQuestForAnyPlayer = true;
                }
                else
                {
                    // Hide quest marker for this player
                    context.StateManager.SetQuestMarker(context.EntityId, playerId, false);
                    SendQuestMarkerToPlayer(context, playerId, false);
                }
            }

            return hasQuestForAnyPlayer ? NodeStatus.Success : NodeStatus.Failure;
        }

        private List<string> GetPlayersInRange(AIContext context, int range)
        {
            var players = new List<string>();

            // For now, just check if we have a target
            if (!string.IsNullOrEmpty(context.TargetPlayerId) && context.TargetPosition != null)
            {
                int distance = Math.Abs(context.TargetPosition.X - context.CurrentPosition.X) +
                              Math.Abs(context.TargetPosition.Y - context.CurrentPosition.Y);

                if (distance <= range)
                {
                    players.Add(context.TargetPlayerId);
                }
            }

            return players;
        }

        private bool CheckQuestAvailability(AIContext context, string playerId)
        {
            // TODO: Implement actual quest system check
            // For now, always return true (quest available)
            return true;
        }

        private void SendQuestMarkerToPlayer(AIContext context, string playerId, bool show)
        {
            if (context.WebSocketServer == null)
                return;

            var message = new
            {
                type = "npc_quest_marker",
                npcId = context.EntityId,
                show = show
            };

            context.WebSocketServer.SendToPlayer(playerId, message);
        }
    }
}