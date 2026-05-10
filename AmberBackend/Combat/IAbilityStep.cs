namespace AmberBackend.Combat
{
    /// <summary>
    /// Represents a single step in an ability execution chain.
    /// </summary>
    public interface IAbilityStep
    {
        /// <summary>
        /// Execute this step. Can modify context.
        /// Returns true if execution should continue, false to stop chain.
        /// </summary>
        bool Execute(AbilityContext context, CombatService combatService);
    }
}