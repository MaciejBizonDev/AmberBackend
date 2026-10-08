using AmberBackend.Movement;
using System.Collections.Generic;

namespace AmberBackend.Zones
{
    /// <summary>
    /// Holds one zone's walkability data: bounds + obstacle tiles.
    /// Walkable = any tile within bounds that is NOT an obstacle.
    /// If bounds are unset (all zero) and no obstacles, everything is walkable (fallback for unpainted zones).
    /// </summary>
    public class ZoneWalkability
    {
        public int MinX { get; }
        public int MinY { get; }
        public int MaxX { get; }
        public int MaxY { get; }

        private readonly HashSet<(int x, int y)> _obstacles;
        private readonly bool _hasBounds;

        public ZoneWalkability(int minX, int minY, int maxX, int maxY, IEnumerable<(int x, int y)> obstacles)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
            _obstacles = new HashSet<(int, int)>(obstacles ?? new List<(int, int)>());

            // Bounds are "set" if they describe a real area (not all zeros / inverted)
            _hasBounds = !(minX == 0 && minY == 0 && maxX == 0 && maxY == 0) && maxX >= minX && maxY >= minY;
        }

        public bool IsWalkable(TilePosition pos)
        {
            // Obstacle = never walkable
            if (_obstacles.Contains((pos.X, pos.Y)))
                return false;

            // If we have real bounds, the tile must be inside them
            if (_hasBounds)
            {
                if (pos.X < MinX || pos.X > MaxX || pos.Y < MinY || pos.Y > MaxY)
                    return false;
            }

            // Inside bounds (or no bounds set) and not an obstacle -> walkable
            return true;
        }

        /// <summary>
        /// Build the walkable-tile list to send to clients (bounds minus obstacles).
        /// </summary>
        public WalkabilityData GetWalkabilityData()
        {
            var data = new WalkabilityData
            {
                MinX = MinX,
                MinY = MinY,
                MaxX = MaxX,
                MaxY = MaxY,
                WalkableTiles = new List<WalkableTile>()
            };

            if (!_hasBounds)
            {
                // No bounds painted yet - nothing explicit to send.
                // Client falls back to "everything walkable" in this case.
                System.Console.WriteLine("[ZoneWalkability] No bounds set - sending empty walkable list (client treats all as walkable)");
                return data;
            }

            for (int x = MinX; x <= MaxX; x++)
            {
                for (int y = MinY; y <= MaxY; y++)
                {
                    if (!_obstacles.Contains((x, y)))
                    {
                        data.WalkableTiles.Add(new WalkableTile { X = x, Y = y });
                    }
                }
            }

            System.Console.WriteLine($"[ZoneWalkability] Computed {data.WalkableTiles.Count} walkable tiles (bounds {MinX},{MinY} to {MaxX},{MaxY}, {_obstacles.Count} obstacles)");
            return data;
        }
    }
}