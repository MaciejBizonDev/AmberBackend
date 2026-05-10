using System.Threading.Tasks;

namespace AmberBackend.Combat.Steps
{
    /// <summary>
    /// Adds a delay between steps (for projectile travel time, etc).
    /// </summary>
    public class WaitStep : IAbilityStep
    {
        public float Seconds { get; set; }

        public bool Execute(AbilityContext context, CombatService combatService)
        {
            // In real implementation, this would be async
            // For now, just log it
            System.Console.WriteLine($"[WaitStep] Waiting {Seconds} seconds");

            // Store for client-side animation timing
            if (!context.CustomData.ContainsKey("totalWaitTime"))
            {
                context.CustomData["totalWaitTime"] = 0f;
            }
            context.CustomData["totalWaitTime"] = (float)context.CustomData["totalWaitTime"] + Seconds;

            return true;
        }
    }
}