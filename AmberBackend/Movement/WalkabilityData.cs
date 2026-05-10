using System.Collections.Generic;

namespace AmberBackend.Movement
{
    /// <summary>
    /// Represents a single walkable/unwalkable tile change.
    /// </summary>
    public class WalkabilityChange
    {
        public int X { get; set; }
        public int Y { get; set; }
        public bool Walkable { get; set; }
    }

    /// <summary>
    /// Full walkability grid data for initial sync.
    /// </summary>
    public class WalkabilityData
    {
        public int MinX { get; set; }
        public int MinY { get; set; }
        public int MaxX { get; set; }
        public int MaxY { get; set; }
        public List<WalkableTile> WalkableTiles { get; set; } = new List<WalkableTile>();
    }

    public class WalkableTile
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
}