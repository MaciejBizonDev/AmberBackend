namespace AmberBackend.Movement
{
    /// <summary>
    /// Represents a tile coordinate in the grid.
    /// </summary>
    public class TilePosition
    {
        public int X { get; set; }
        public int Y { get; set; }

        public TilePosition() { }

        public TilePosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public override string ToString()
        {
            return $"({X}, {Y})";
        }

        public override bool Equals(object obj)
        {
            if (obj is TilePosition other)
            {
                return X == other.X && Y == other.Y;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public static bool operator ==(TilePosition a, TilePosition b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            return a.X == b.X && a.Y == b.Y;
        }

        public static bool operator !=(TilePosition a, TilePosition b)
        {
            return !(a == b);
        }
    }
}
