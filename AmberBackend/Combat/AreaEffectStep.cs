using AmberBackend.Movement;
using System.Collections.Generic;

namespace AmberBackend.Combat.Steps
{
    /// <summary>
    /// Finds all entities in an area around a point.
    /// Adds them to AffectedEntities list.
    /// </summary>
    public class AreaEffectStep : IAbilityStep
    {
        public AreaPattern Pattern { get; set; }
        public OriginSource OriginSource { get; set; } = OriginSource.ImpactPoint;

        public bool Execute(AbilityContext context, CombatService combatService)
        {
            // Determine origin
            TilePosition origin = GetOrigin(context);
            if (origin == null)
            {
                System.Console.WriteLine("[AreaEffectStep] No valid origin found");
                return false;
            }

            // Get affected cells based on pattern
            var affectedCells = GetAffectedCells(origin);

            // Find entities at those cells
            var foundEntities = combatService.GetEntitiesAtCells(affectedCells);

            // Add to affected list (avoid duplicates)
            foreach (var entityId in foundEntities)
            {
                if (!context.AffectedEntities.Contains(entityId))
                {
                    context.AffectedEntities.Add(entityId);
                }
            }

            System.Console.WriteLine($"[AreaEffectStep] Found {foundEntities.Count} entities in area. Total affected: {context.AffectedEntities.Count}");
            return true;
        }

        private TilePosition GetOrigin(AbilityContext context)
        {
            return OriginSource switch
            {
                OriginSource.Caster => context.SourcePosition,
                OriginSource.Target => context.TargetPosition,
                OriginSource.ImpactPoint => context.ImpactPoint ?? context.TargetPosition,
                _ => context.SourcePosition
            };
        }

        private List<TilePosition> GetAffectedCells(TilePosition origin)
        {
            var cells = new List<TilePosition>();

            switch (Pattern)
            {
                case AreaPattern.Single:
                    cells.Add(origin);
                    break;

                case AreaPattern.Cross3x3:
                    cells.Add(origin);
                    cells.Add(new TilePosition(origin.X + 1, origin.Y));
                    cells.Add(new TilePosition(origin.X - 1, origin.Y));
                    cells.Add(new TilePosition(origin.X, origin.Y + 1));
                    cells.Add(new TilePosition(origin.X, origin.Y - 1));
                    break;

                case AreaPattern.Square3x3:
                    for (int x = -1; x <= 1; x++)
                    {
                        for (int y = -1; y <= 1; y++)
                        {
                            cells.Add(new TilePosition(origin.X + x, origin.Y + y));
                        }
                    }
                    break;

                case AreaPattern.Square5x5:
                    for (int x = -2; x <= 2; x++)
                    {
                        for (int y = -2; y <= 2; y++)
                        {
                            cells.Add(new TilePosition(origin.X + x, origin.Y + y));
                        }
                    }
                    break;
            }

            return cells;
        }
    }

    public enum AreaPattern
    {
        Single,
        Cross3x3,
        Square3x3,
        Square5x5
    }

    public enum OriginSource
    {
        Caster,
        Target,
        ImpactPoint
    }
}