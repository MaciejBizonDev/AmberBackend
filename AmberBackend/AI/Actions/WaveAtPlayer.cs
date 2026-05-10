using System.Collections.Generic;

namespace AmberBackend.AI.Actions
{
    /// <summary>
    /// Wave at players who can accept quests.
    /// Each player sees the wave individually.
    /// </summary>
    public class WaveAtPlayer : BehaviorNode
    {
        private float _waveTimer = 0f;
        private const float WaveInterval = 3f;
        private HashSet<string> _wavedAtPlayers = new HashSet<string>();

        public override NodeStatus Execute(AIContext context)
        {
            _waveTimer += 0.1f;

            if (_waveTimer < WaveInterval)
                return NodeStatus.Running;

            _waveTimer = 0f;

            // Find players with quests available
            var playersWithQuests = GetPlayersWithQuests(context, 5);

            foreach (var playerId in playersWithQuests)
            {
                // Only wave once per player
                if (_wavedAtPlayers.Contains(playerId))
                    continue;

                // Send wave animation ONLY to this player
                SendWaveToPlayer(context, playerId);
                _wavedAtPlayers.Add(playerId);
            }

            // Clean up players who left range
            _wavedAtPlayers.RemoveWhere(p => !playersWithQuests.Contains(p));

            return NodeStatus.Success;
        }

        private List<string> GetPlayersWithQuests(AIContext context, int range)
        {
            var players = new List<string>();

            if (!string.IsNullOrEmpty(context.TargetPlayerId))
            {
                var state = context.StateManager.GetState(context.EntityId, context.TargetPlayerId);
                if (state.ShowQuestMarker)
                {
                    players.Add(context.TargetPlayerId);
                }
            }

            return players;
        }

        private void SendWaveToPlayer(AIContext context, string playerId)
        {
            if (context.WebSocketServer == null)
                return;

            var message = new
            {
                type = "npc_animation",
                npcId = context.EntityId,
                animation = "wave",
                duration = 1.0f
            };

            context.WebSocketServer.SendToPlayer(playerId, message);

            System.Console.WriteLine($"[AI:{context.EntityId}] Waved at {playerId}");
        }

        public override void Reset()
        {
            _waveTimer = 0f;
        }
    }
}