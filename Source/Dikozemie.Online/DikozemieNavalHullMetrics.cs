using System;

namespace Dikozemie.Online
{
    /// <summary>
    /// The War Sails hulls as the dedicated server needs them: it has no NavalDLC
    /// and cannot measure them, yet it keeps crews on the deck the clients draw.
    /// The numbers are generated from the ships' navmesh prefabs. Every hull has
    /// a deck plane of its own in the sea scene, at the height of its real deck
    /// above the water, so the ship floats where War Sails floats it.
    /// </summary>
    internal sealed partial class DikozemieNavalHullMetrics
    {
        private readonly float[] _stationLeft;
        private readonly float[] _stationRight;

        private DikozemieNavalHullMetrics(
            string mesh,
            float halfBeam,
            float halfLength,
            float deckZ,
            float deckPlaneHeight,
            float deckMinY,
            float deckMaxY,
            float seaX,
            float seaY,
            float[] stationLeft,
            float[] stationRight)
        {
            Mesh = mesh;
            HalfBeam = halfBeam;
            HalfLength = halfLength;
            DeckZ = deckZ;
            DeckPlaneHeight = deckPlaneHeight;
            DeckMinY = deckMinY;
            DeckMaxY = deckMaxY;
            SeaX = seaX;
            SeaY = seaY;
            _stationLeft = stationLeft;
            _stationRight = stationRight;
            float widest = 0f;
            for (int index = 0; index < stationLeft.Length; index++)
            {
                widest = Math.Max(
                    widest,
                    Math.Max(-stationLeft[index], stationRight[index]));
            }
            WalkableHalfWidth = widest;
            WalkableHalfLength = (deckMaxY - deckMinY) * 0.5f;
        }

        internal string Mesh { get; }
        internal float HalfBeam { get; }
        internal float HalfLength { get; }
        internal float DeckZ { get; }
        internal float DeckMinY { get; }
        internal float DeckMaxY { get; }
        internal float SeaX { get; }
        internal float SeaY { get; }
        internal float WalkableHalfWidth { get; }
        internal float WalkableHalfLength { get; }
        internal float DeckCenterY => (DeckMinY + DeckMaxY) * 0.5f;

        // The height of this hull's plane in the scene.
        internal float DeckPlaneHeight { get; }

        // mission_ships.xml of War Sails: the complete ship of each hull.
        private static readonly System.Collections.Generic.Dictionary<string, string> Prefabs =
            new System.Collections.Generic.Dictionary<string, string>
            {
                ["ship_lightlongship"] = "lightlongship_ship_nested",
                ["ship_catship"] = "catship_ship_nested",
                ["ship_galley"] = "galley_ship_nested",
                ["ship_meditlight"] = "meditlight_ship_nested",
                ["ship_knarr"] = "knarr_ship_nested",
                ["ship_dhow"] = "dhow_ship_nested",
                ["nord_mediumballista_ship"] = "nordheavy_ship_nested",
                ["ship_longship"] = "longship_ship_nested",
                ["ship_catship_medium"] = "catship_medium_ship_nested",
                ["ship_cog"] = "cog_ship_nested",
                ["ship_meditmedium"] = "meditmedium_ship_nested",
                ["ship_liburna"] = "liburna_ship_nested",
                ["ship_trade_cog"] = "trade_cog_ship_nested",
                ["ship_corbita"] = "corbita_ship_nested",
                ["ship_sturgianheavylongship"] = "sturgian_heavy_longship_nested",
                ["ship_drakkar"] = "drakkar_ship_nested",
                ["ship_roundship"] = "roundship_nested",
                ["ship_xebec"] = "xebec_ship_nested",
                ["ship_lightdromon"] = "lightdromon_ship_nested",
                ["ship_meditheavy"] = "meditheavy_ship_nested",
                ["ship_dromon"] = "dromon_ship_nested"
            };

        internal static string PrefabFor(string missionShipId) =>
            Prefabs.TryGetValue(missionShipId ?? string.Empty, out string prefab)
                ? prefab
                : null;

        internal static DikozemieNavalHullMetrics Resolve(string missionShipId) =>
            Hulls.TryGetValue(
                missionShipId ?? string.Empty,
                out DikozemieNavalHullMetrics hull)
                    ? hull
                    : Hulls["ship_lightlongship"];

        /// <summary>
        /// Moves a point given in the hull's own coordinates onto the walkable
        /// main deck. The deck narrows towards bow and stern.
        /// </summary>
        internal void ClampToDeck(ref float x, ref float y)
        {
            y = Math.Max(DeckMinY, Math.Min(DeckMaxY, y));
            float position = DeckMaxY <= DeckMinY
                ? 0f
                : (y - DeckMinY) / (DeckMaxY - DeckMinY) *
                    (_stationLeft.Length - 1);
            int low = Math.Min((int)position, _stationLeft.Length - 2);
            float share = position - low;
            float left = _stationLeft[low] +
                ((_stationLeft[low + 1] - _stationLeft[low]) * share);
            float right = _stationRight[low] +
                ((_stationRight[low + 1] - _stationRight[low]) * share);
            x = Math.Max(left, Math.Min(right, x));
        }

        internal bool IsOnDeck(float x, float y)
        {
            float keptX = x;
            float keptY = y;
            ClampToDeck(ref keptX, ref keptY);
            return Math.Abs(keptX - x) < 0.01f && Math.Abs(keptY - y) < 0.01f;
        }
    }
}
