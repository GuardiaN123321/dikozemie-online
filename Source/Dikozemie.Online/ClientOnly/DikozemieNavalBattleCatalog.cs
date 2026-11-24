using System;
using System.Collections.Generic;
using System.Linq;

namespace Dikozemie.Online
{
    internal enum DikozemieNavalShipClass
    {
        Light,
        Medium,
        Heavy
    }

    internal sealed class DikozemieNavalBattleDefinition
    {
        internal DikozemieNavalBattleDefinition(
            string hullId,
            string missionShipId,
            string displayName,
            DikozemieNavalShipClass shipClass,
            int crewCapacity)
        {
            HullId = hullId ?? string.Empty;
            MissionShipId = missionShipId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            ShipClass = shipClass;
            CrewCapacity = Math.Max(1, crewCapacity);
        }

        internal string HullId { get; }

        internal string MissionShipId { get; }

        internal string DisplayName { get; }

        internal DikozemieNavalShipClass ShipClass { get; }

        internal int CrewCapacity { get; }

        internal int TotalCombatants => CrewCapacity * 2;
    }

    /// <summary>
    /// Every standard hull exposed by Naval Custom Battle in Bannerlord 1.4.8
    /// / NavalDLC 1.2.8, unchanged in 1.5.7 / 1.3.7 (ids, mission ships and
    /// crew capacities checked 2026-11-14). Story and fishing props are
    /// excluded. Trade hulls stay available because identical ships and crew
    /// sizes make them fair, distinctive mirrored presets.
    /// </summary>
    internal static class DikozemieNavalBattleCatalog
    {
        // These are the actual capacities used by War Sails Custom Battle.
        // The abandoned multiplayer prop prototype temporarily reduced them
        // to 1/2/3 while debugging fake decks; native ships do not need that
        // substitute limit.
        private static readonly DikozemieNavalBattleDefinition[] Battles =
        {
            new DikozemieNavalBattleDefinition(
                "northern_light_ship", "ship_lightlongship",
                "Легкая нордская ладья", DikozemieNavalShipClass.Light, 32),
            new DikozemieNavalBattleDefinition(
                "battanian_light_ship", "ship_catship",
                "Бирлинн", DikozemieNavalShipClass.Light, 36),
            new DikozemieNavalBattleDefinition(
                "western_light_ship", "ship_galley",
                "Западная галера", DikozemieNavalShipClass.Light, 32),
            new DikozemieNavalBattleDefinition(
                "central_light_ship", "ship_meditlight",
                "Восточная галера", DikozemieNavalShipClass.Light, 30),
            new DikozemieNavalBattleDefinition(
                "northern_trade_ship", "ship_knarr",
                "Кнарр", DikozemieNavalShipClass.Light, 18),
            new DikozemieNavalBattleDefinition(
                "eastern_trade_ship", "ship_dhow",
                "Дау", DikozemieNavalShipClass.Light, 16),

            new DikozemieNavalBattleDefinition(
                "nord_mediumballista_ship", "nord_mediumballista_ship",
                "Боевой кнарр", DikozemieNavalShipClass.Medium, 52),
            new DikozemieNavalBattleDefinition(
                "northern_medium_ship", "ship_longship",
                "Нордская ладья", DikozemieNavalShipClass.Medium, 54),
            new DikozemieNavalBattleDefinition(
                "battanian_medium_ship", "ship_catship_medium",
                "Барлиннгер", DikozemieNavalShipClass.Medium, 50),
            new DikozemieNavalBattleDefinition(
                "western_medium_ship", "ship_cog",
                "Когг", DikozemieNavalShipClass.Medium, 60),
            new DikozemieNavalBattleDefinition(
                "eastern_medium_ship", "ship_meditmedium",
                "Самбук", DikozemieNavalShipClass.Medium, 60),
            new DikozemieNavalBattleDefinition(
                "empire_medium_ship", "ship_liburna",
                "Либурна", DikozemieNavalShipClass.Medium, 60),
            new DikozemieNavalBattleDefinition(
                "western_trade_ship", "ship_trade_cog",
                "Торговый когг", DikozemieNavalShipClass.Medium, 30),
            new DikozemieNavalBattleDefinition(
                "empire_trade_ship", "ship_corbita",
                "Корбита", DikozemieNavalShipClass.Medium, 35),

            new DikozemieNavalBattleDefinition(
                "sturgia_heavy_ship", "ship_sturgianheavylongship",
                "Ладья", DikozemieNavalShipClass.Heavy, 96),
            new DikozemieNavalBattleDefinition(
                "nord_medium_ship", "ship_drakkar",
                "Драккар", DikozemieNavalShipClass.Heavy, 106),
            new DikozemieNavalBattleDefinition(
                "vlandia_heavy_ship", "ship_roundship",
                "Большой когг", DikozemieNavalShipClass.Heavy, 108),
            new DikozemieNavalBattleDefinition(
                "khuzait_heavy_ship", "ship_xebec",
                "Калгук", DikozemieNavalShipClass.Heavy, 100),
            new DikozemieNavalBattleDefinition(
                "eastern_heavy_ship", "ship_lightdromon",
                "Дромакион", DikozemieNavalShipClass.Heavy, 94),
            new DikozemieNavalBattleDefinition(
                "aserai_heavy_ship", "ship_meditheavy",
                "Гураб", DikozemieNavalShipClass.Heavy, 112),
            new DikozemieNavalBattleDefinition(
                "empire_heavy_ship", "ship_dromon",
                "Дромон", DikozemieNavalShipClass.Heavy, 120)
        };

        internal static IReadOnlyList<DikozemieNavalBattleDefinition> All =>
            Battles;

        internal static DikozemieNavalBattleDefinition Find(
            string hullId)
        {
            return Battles.FirstOrDefault(battle => string.Equals(
                       battle.HullId,
                       hullId,
                       StringComparison.Ordinal)) ??
                FindFirst(DikozemieNavalShipClass.Light);
        }

        internal static DikozemieNavalBattleDefinition FindFirst(
            DikozemieNavalShipClass shipClass)
        {
            return Battles.First(battle => battle.ShipClass == shipClass);
        }

        internal static IReadOnlyList<DikozemieNavalBattleDefinition> FindByClass(
            DikozemieNavalShipClass shipClass)
        {
            return Battles.Where(battle => battle.ShipClass == shipClass).ToArray();
        }
    }
}
