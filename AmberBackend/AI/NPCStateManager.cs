using System;
using System.Collections.Generic;
using System.Linq;

namespace AmberBackend.AI
{
    /// <summary>
    /// Tracks what each player should see from an NPC.
    /// </summary>
    public class NPCPlayerState
    {
        public string NpcId { get; set; }
        public string PlayerId { get; set; }

        // Visual states
        public bool ShowQuestMarker { get; set; }
        public bool ShowExclamation { get; set; }
        public string CurrentAnimation { get; set; } = "idle";

        // Interaction state
        public bool IsInteracting { get; set; }
        public DateTime LastInteractionTime { get; set; }
    }

    /// <summary>
    /// Manages per-player NPC states.
    /// </summary>
    public class NPCStateManager
    {
        // Key: "npcId:playerId" → State
        private readonly Dictionary<string, NPCPlayerState> _states = new Dictionary<string, NPCPlayerState>();

        public NPCPlayerState GetState(string npcId, string playerId)
        {
            string key = $"{npcId}:{playerId}";
            if (!_states.TryGetValue(key, out var state))
            {
                state = new NPCPlayerState
                {
                    NpcId = npcId,
                    PlayerId = playerId,
                    CurrentAnimation = "idle"
                };
                _states[key] = state;
            }
            return state;
        }

        public void SetAnimation(string npcId, string playerId, string animation)
        {
            var state = GetState(npcId, playerId);
            state.CurrentAnimation = animation;
        }

        public void SetQuestMarker(string npcId, string playerId, bool show)
        {
            var state = GetState(npcId, playerId);
            state.ShowQuestMarker = show;
        }

        public void ClearPlayerStates(string playerId)
        {
            // Clean up when player logs out
            var keysToRemove = _states.Keys.Where(k => k.EndsWith($":{playerId}")).ToList();
            foreach (var key in keysToRemove)
            {
                _states.Remove(key);
            }
        }

        public void ClearNPCStates(string npcId)
        {
            // Clean up when NPC is removed
            var keysToRemove = _states.Keys.Where(k => k.StartsWith($"{npcId}:")).ToList();
            foreach (var key in keysToRemove)
            {
                _states.Remove(key);
            }
        }
    }
}