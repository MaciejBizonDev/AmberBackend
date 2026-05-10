using AmberBackend.Movement;

namespace AmberBackend.Zones
{
    /// <summary>
    /// Defines a portal/transition point between zones.
    /// When player enters trigger area, they teleport to destination zone.
    /// </summary>
    public class ZonePortal
    {
        public string PortalId { get; set; }
        public string SourceZoneId { get; set; }
        public TilePosition TriggerPosition { get; set; }
        public string DestinationZoneId { get; set; }
        public TilePosition DestinationPosition { get; set; }

        // Predefined portals
        public static ZonePortal TestZoneToTown => new ZonePortal
        {
            PortalId = "portal_test_to_town",
            SourceZoneId = "test_zone",
            TriggerPosition = new TilePosition(10, -5), // Portal at this location in test_zone
            DestinationZoneId = "town_1",
            DestinationPosition = new TilePosition(3, -2) // Spawn here in town
        };

        public static ZonePortal TownToTestZone => new ZonePortal
        {
            PortalId = "portal_town_to_test",
            SourceZoneId = "town_1",
            TriggerPosition = new TilePosition(6, -2), // Portal in town
            DestinationZoneId = "test_zone",
            DestinationPosition = new TilePosition(2, 8) // Return to test zone
        };
    }
}