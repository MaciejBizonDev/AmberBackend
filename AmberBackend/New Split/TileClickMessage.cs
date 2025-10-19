namespace AmberBackend.New_Split
{
    public class TileClickMessage
    {
        public string type { get; set; }   // "tile_click"
        public int x { get; set; }
        public int y { get; set; }
        public string playerId { get; set; } // may be empty; server uses session-bound id
    }
}
