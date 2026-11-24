using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Dikozemie.Online
{
    // One planned battle: either an arena format or a ship class.
    internal sealed class DikozemieBattlePlan
    {
        internal DikozemieBattleVenue Venue;
        internal int TeamSize;
        internal DikozemieArenaDiscipline Discipline;
        internal DikozemieArenaComposition Composition;
        internal DikozemieNavalShipClass ShipClass;

        internal bool IsNaval => Venue == DikozemieBattleVenue.Naval;

        internal string Describe()
        {
            if (IsNaval)
            {
                return "морской бой · " + DikozemieBattleRotation.ShipClassName(ShipClass);
            }
            return TeamSize + "×" + TeamSize + " · " +
                DikozemieBattleRotation.DisciplineName(Discipline) + " · " +
                DikozemieBattleRotation.CompositionName(Composition);
        }

        // Where it is fought: the arena follows from the format and the
        // style (DikozemieArenaSceneRules), the sea from the ship.
        internal string DescribeVenue()
        {
            if (IsNaval)
            {
                return "открытое море";
            }
            return DikozemieArenaSceneRules.FormatSceneName(
                DikozemieArenaSceneRules.ResolveScene(TeamSize, Discipline));
        }

        // Two lines for the panels: where, then what.
        internal string DescribeForPanel()
        {
            return ("Следующий бой · " + DescribeVenue()).ToUpperInvariant() +
                "\n" + Describe().ToUpperInvariant();
        }
    }

    // The customer, 2026-11-04: «по умолчанию все включены, а потом
    // чекбоксами игрок может отключить режимы» and «поставил режим и хочешь,
    // чтобы эти типы боёв крутились». Every format, style, troop mix and ship
    // class starts switched on; the player switches off what he dislikes, and
    // battles follow each other through the enabled kinds without a trip back
    // to the lobby. A sea battle comes after every three arena battles.
    internal static class DikozemieBattleRotation
    {
        internal static readonly int[] TeamSizes =
            DikozemieArenaMatchSelection.TeamSizeOptions;
        internal static readonly DikozemieArenaDiscipline[] Disciplines =
        {
            DikozemieArenaDiscipline.Foot,
            DikozemieArenaDiscipline.Mounted,
            DikozemieArenaDiscipline.Open
        };
        internal static readonly DikozemieArenaComposition[] Compositions =
        {
            DikozemieArenaComposition.Mixed,
            DikozemieArenaComposition.Melee,
            DikozemieArenaComposition.Ranged
        };
        internal static readonly DikozemieNavalShipClass[] ShipClasses =
        {
            DikozemieNavalShipClass.Light,
            DikozemieNavalShipClass.Medium,
            DikozemieNavalShipClass.Heavy
        };
        private const int ArenaBattlesPerSeaBattle = 3;

        private static readonly object Gate = new object();
        private static readonly HashSet<int> EnabledSizes = new HashSet<int>(TeamSizes);
        private static readonly HashSet<DikozemieArenaDiscipline> EnabledDisciplines =
            new HashSet<DikozemieArenaDiscipline>(Disciplines);
        private static readonly HashSet<DikozemieArenaComposition> EnabledCompositions =
            new HashSet<DikozemieArenaComposition>(Compositions);
        private static readonly HashSet<DikozemieNavalShipClass> EnabledShips =
            new HashSet<DikozemieNavalShipClass>(ShipClasses);
        private static int _slotCursor = -1;
        private static int _disciplineCursor = -1;
        private static int _compositionCursor = -1;
        private static int _version;
        private static bool _loaded;
        private static DikozemieBattlePlan _preview;
        private static int _previewVersion = -1;

        // Changes whenever a switch or the rotation moves; widgets redraw on it.
        internal static int Version
        {
            get
            {
                lock (Gate)
                {
                    return _version;
                }
            }
        }

        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Dikozemie",
            "arena",
            "battle-rotation.txt");

        internal static bool IsSizeEnabled(int teamSize)
        {
            lock (Gate)
            {
                EnsureLoaded();
                return EnabledSizes.Contains(teamSize);
            }
        }

        internal static bool IsDisciplineEnabled(DikozemieArenaDiscipline discipline)
        {
            lock (Gate)
            {
                EnsureLoaded();
                return EnabledDisciplines.Contains(discipline);
            }
        }

        internal static bool IsCompositionEnabled(DikozemieArenaComposition composition)
        {
            lock (Gate)
            {
                EnsureLoaded();
                return EnabledCompositions.Contains(composition);
            }
        }

        internal static bool IsShipEnabled(DikozemieNavalShipClass shipClass)
        {
            lock (Gate)
            {
                EnsureLoaded();
                return EnabledShips.Contains(shipClass);
            }
        }

        // Switching off the last kind of a group is refused with a reason:
        // a rotation without any style or troop mix has nothing to play.
        internal static bool ToggleSize(int teamSize, out string refusal)
        {
            lock (Gate)
            {
                EnsureLoaded();
                if (EnabledSizes.Contains(teamSize))
                {
                    if (EnabledSizes.Count == 1 && EnabledShips.Count == 0)
                    {
                        refusal = "Оставьте хотя бы один вид боя";
                        return false;
                    }
                    EnabledSizes.Remove(teamSize);
                }
                else
                {
                    EnabledSizes.Add(teamSize);
                }
                return Changed(out refusal);
            }
        }

        internal static bool ToggleDiscipline(
            DikozemieArenaDiscipline discipline,
            out string refusal)
        {
            lock (Gate)
            {
                EnsureLoaded();
                if (EnabledDisciplines.Contains(discipline))
                {
                    if (EnabledDisciplines.Count == 1)
                    {
                        refusal = "Нужен хотя бы один стиль боя";
                        return false;
                    }
                    EnabledDisciplines.Remove(discipline);
                }
                else
                {
                    EnabledDisciplines.Add(discipline);
                }
                return Changed(out refusal);
            }
        }

        internal static bool ToggleComposition(
            DikozemieArenaComposition composition,
            out string refusal)
        {
            lock (Gate)
            {
                EnsureLoaded();
                if (EnabledCompositions.Contains(composition))
                {
                    if (EnabledCompositions.Count == 1)
                    {
                        refusal = "Нужен хотя бы один состав войск";
                        return false;
                    }
                    EnabledCompositions.Remove(composition);
                }
                else
                {
                    EnabledCompositions.Add(composition);
                }
                return Changed(out refusal);
            }
        }

        internal static bool ToggleShip(
            DikozemieNavalShipClass shipClass,
            out string refusal)
        {
            lock (Gate)
            {
                EnsureLoaded();
                if (EnabledShips.Contains(shipClass))
                {
                    if (EnabledShips.Count == 1 && EnabledSizes.Count == 0)
                    {
                        refusal = "Оставьте хотя бы один вид боя";
                        return false;
                    }
                    EnabledShips.Remove(shipClass);
                }
                else
                {
                    EnabledShips.Add(shipClass);
                }
                return Changed(out refusal);
            }
        }

        // What «В бой» will start, without moving the rotation.
        internal static DikozemieBattlePlan PreviewNext()
        {
            lock (Gate)
            {
                EnsureLoaded();
                // Gear changes in the hero menu do not touch the version, so
                // the preview is also refreshed every two seconds.
                DateTime now = DateTime.UtcNow;
                if (_previewVersion != _version || now - _previewAt > TimeSpan.FromSeconds(2))
                {
                    _preview = Plan(advance: false);
                    _previewVersion = _version;
                    _previewAt = now;
                }
                return _preview;
            }
        }

        private static DateTime _previewAt = DateTime.MinValue;

        // Moves the rotation one battle on and makes the planned battle the
        // current match selection (what the search and the server profile use).
        internal static DikozemieBattlePlan ApplyNext(out string error)
        {
            DikozemieBattlePlan plan;
            lock (Gate)
            {
                EnsureLoaded();
                plan = Plan(advance: true);
                _version++;
                Save();
            }
            if (plan == null)
            {
                error = "Нет подходящего боя: включите другие виды или смените снаряжение";
                return null;
            }

            if (plan.IsNaval)
            {
                DikozemieArenaMatchSelection.SelectNavalClassForBattle(plan.ShipClass);
            }
            else
            {
                DikozemieArenaMatchSelection.SelectArenaBattle(
                    plan.TeamSize,
                    plan.Discipline,
                    plan.Composition);
            }
            error = null;
            return plan;
        }

        internal static string DescribeEnabled()
        {
            lock (Gate)
            {
                EnsureLoaded();
                int arena = EnabledSizes.Count;
                int sea = EnabledShips.Count;
                var parts = new List<string>();
                parts.Add(arena == TeamSizes.Length
                    ? "все размеры"
                    : arena == 0 ? "арена выключена" : "размеров: " + arena);
                parts.Add(sea == ShipClasses.Length
                    ? "все корабли"
                    : sea == 0 ? "море выключено" : "кораблей: " + sea);
                return string.Join(" · ", parts);
            }
        }

        internal static string DisciplineName(DikozemieArenaDiscipline discipline)
        {
            switch (discipline)
            {
                case DikozemieArenaDiscipline.Foot:
                    return "пеший";
                case DikozemieArenaDiscipline.Mounted:
                    return "конный";
                default:
                    return "открытый";
            }
        }

        internal static string CompositionName(DikozemieArenaComposition composition)
        {
            switch (composition)
            {
                case DikozemieArenaComposition.Melee:
                    return "ближний бой";
                case DikozemieArenaComposition.Ranged:
                    return "дальний бой";
                default:
                    return "смешанный";
            }
        }

        internal static string ShipClassName(DikozemieNavalShipClass shipClass)
        {
            switch (shipClass)
            {
                case DikozemieNavalShipClass.Medium:
                    return "средний корабль";
                case DikozemieNavalShipClass.Heavy:
                    return "тяжёлый корабль";
                default:
                    return "лёгкий корабль";
            }
        }

        private static bool Changed(out string refusal)
        {
            _version++;
            Save();
            refusal = null;
            return true;
        }

        // The slot list interleaves sea battles with arena formats:
        // three arena sizes, one ship, three arena sizes, the next ship...
        private static List<object> BuildSlots()
        {
            var slots = new List<object>();
            int[] sizes = TeamSizes.Where(EnabledSizes.Contains).ToArray();
            DikozemieNavalShipClass[] ships =
                ShipClasses.Where(EnabledShips.Contains).ToArray();
            if (sizes.Length == 0 || ships.Length == 0)
            {
                slots.AddRange(sizes.Cast<object>());
                slots.AddRange(ships.Cast<object>());
                return slots;
            }
            // One sea battle after every three arena battles, as the panel
            // says. With few sizes switched on the sizes repeat, so every
            // ship class still waits for three arena battles (one size and
            // three ships: 1×1, 1×1, 1×1, light, 1×1 … — not three sea
            // battles in a row).
            int arenaBattles = Math.Max(
                sizes.Length,
                ships.Length * ArenaBattlesPerSeaBattle);
            arenaBattles = (arenaBattles + sizes.Length - 1) / sizes.Length *
                sizes.Length;
            int shipIndex = 0;
            for (int index = 0; index < arenaBattles; index++)
            {
                slots.Add(sizes[index % sizes.Length]);
                if ((index + 1) % ArenaBattlesPerSeaBattle == 0)
                {
                    slots.Add(ships[shipIndex % ships.Length]);
                    shipIndex++;
                }
            }
            return slots;
        }

        private static DikozemieBattlePlan Plan(bool advance)
        {
            List<object> slots = BuildSlots();
            if (slots.Count == 0)
            {
                return null;
            }

            int slotCursor = _slotCursor;
            int disciplineCursor = _disciplineCursor;
            int compositionCursor = _compositionCursor;
            for (int attempt = 0; attempt < slots.Count; attempt++)
            {
                slotCursor = (slotCursor + 1 + slots.Count) % slots.Count;
                object slot = slots[slotCursor];
                if (slot is DikozemieNavalShipClass shipClass)
                {
                    if (!DikozemieLaunchGuard.IsNavalAvailable)
                    {
                        continue;
                    }
                    if (advance)
                    {
                        _slotCursor = slotCursor;
                    }
                    return new DikozemieBattlePlan
                    {
                        Venue = DikozemieBattleVenue.Naval,
                        ShipClass = shipClass,
                        TeamSize = 1,
                        Discipline = DikozemieArenaDiscipline.Foot,
                        Composition = DikozemieArenaComposition.Mixed
                    };
                }

                int teamSize = (int)slot;
                if (TryPickStyle(
                        teamSize,
                        ref disciplineCursor,
                        ref compositionCursor,
                        out DikozemieArenaDiscipline discipline,
                        out DikozemieArenaComposition composition))
                {
                    if (advance)
                    {
                        _slotCursor = slotCursor;
                        _disciplineCursor = disciplineCursor;
                        _compositionCursor = compositionCursor;
                    }
                    return new DikozemieBattlePlan
                    {
                        Venue = DikozemieBattleVenue.Arena,
                        TeamSize = teamSize,
                        Discipline = discipline,
                        Composition = composition
                    };
                }
            }
            return null;
        }

        // Styles and troop mixes take turns too; a style the hero cannot use
        // (mounted without a horse) or a size the style does not support is
        // skipped.
        private static bool TryPickStyle(
            int teamSize,
            ref int disciplineCursor,
            ref int compositionCursor,
            out DikozemieArenaDiscipline discipline,
            out DikozemieArenaComposition composition)
        {
            DikozemieArenaDiscipline[] disciplines =
                Disciplines.Where(EnabledDisciplines.Contains).ToArray();
            DikozemieArenaComposition[] compositions =
                Compositions.Where(EnabledCompositions.Contains).ToArray();
            for (int disciplineStep = 1; disciplineStep <= disciplines.Length; disciplineStep++)
            {
                int disciplineIndex = (disciplineCursor + disciplineStep + disciplines.Length * 4) %
                    disciplines.Length;
                DikozemieArenaDiscipline candidate = disciplines[disciplineIndex];
                if (!DikozemieArenaTeamRules.IsTeamSizeSupported(teamSize, candidate) ||
                    !DikozemieArenaMatchSelection.IsDisciplineAvailable(candidate, out _))
                {
                    continue;
                }
                for (int compositionStep = 1; compositionStep <= compositions.Length; compositionStep++)
                {
                    int compositionIndex =
                        (compositionCursor + compositionStep + compositions.Length * 4) %
                        compositions.Length;
                    DikozemieArenaComposition mix = compositions[compositionIndex];
                    if (!DikozemieArenaMatchSelection.IsCompositionPlayable(candidate, mix))
                    {
                        continue;
                    }
                    disciplineCursor = disciplineIndex;
                    compositionCursor = compositionIndex;
                    discipline = candidate;
                    composition = mix;
                    return true;
                }
            }
            discipline = DikozemieArenaDiscipline.Open;
            composition = DikozemieArenaComposition.Mixed;
            return false;
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }
            _loaded = true;
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return;
                }
                foreach (string raw in File.ReadAllLines(SettingsPath, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    int equals = line.IndexOf('=');
                    if (equals <= 0)
                    {
                        continue;
                    }
                    string key = line.Substring(0, equals).Trim();
                    string value = line.Substring(equals + 1).Trim();
                    switch (key)
                    {
                        case "off-sizes":
                            foreach (int size in ParseInts(value))
                            {
                                EnabledSizes.Remove(size);
                            }
                            break;
                        case "off-styles":
                            foreach (int style in ParseInts(value))
                            {
                                EnabledDisciplines.Remove((DikozemieArenaDiscipline)style);
                            }
                            break;
                        case "off-mixes":
                            foreach (int mix in ParseInts(value))
                            {
                                EnabledCompositions.Remove((DikozemieArenaComposition)mix);
                            }
                            break;
                        case "off-ships":
                            foreach (int ship in ParseInts(value))
                            {
                                EnabledShips.Remove((DikozemieNavalShipClass)ship);
                            }
                            break;
                        case "cursor":
                            int[] cursors = ParseInts(value).ToArray();
                            if (cursors.Length == 3)
                            {
                                _slotCursor = cursors[0];
                                _disciplineCursor = cursors[1];
                                _compositionCursor = cursors[2];
                            }
                            break;
                    }
                }
                // A corrupt file must not leave the arena without battles.
                if (EnabledDisciplines.Count == 0)
                {
                    EnabledDisciplines.UnionWith(Disciplines);
                }
                if (EnabledCompositions.Count == 0)
                {
                    EnabledCompositions.UnionWith(Compositions);
                }
                if (EnabledSizes.Count == 0 && EnabledShips.Count == 0)
                {
                    EnabledSizes.UnionWith(TeamSizes);
                    EnabledShips.UnionWith(ShipClasses);
                }
            }
            catch (Exception error)
            {
                DikozemieLobbyShellIntegration.TraceButtonAction(
                    "battle rotation settings unreadable: " + error.Message);
            }
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                var lines = new[]
                {
                    "# Arena battle rotation: kinds switched off by the player",
                    "off-sizes=" + string.Join(",", TeamSizes.Where(size => !EnabledSizes.Contains(size))),
                    "off-styles=" + string.Join(",", Disciplines.Where(style => !EnabledDisciplines.Contains(style)).Select(style => (int)style)),
                    "off-mixes=" + string.Join(",", Compositions.Where(mix => !EnabledCompositions.Contains(mix)).Select(mix => (int)mix)),
                    "off-ships=" + string.Join(",", ShipClasses.Where(ship => !EnabledShips.Contains(ship)).Select(ship => (int)ship)),
                    "cursor=" + _slotCursor + "," + _disciplineCursor + "," + _compositionCursor
                };
                File.WriteAllLines(SettingsPath, lines, new UTF8Encoding(false));
            }
            catch (Exception error)
            {
                DikozemieLobbyShellIntegration.TraceButtonAction(
                    "battle rotation settings not saved: " + error.Message);
            }
        }

        private static IEnumerable<int> ParseInts(string value)
        {
            foreach (string part in (value ?? string.Empty).Split(','))
            {
                if (int.TryParse(part.Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int parsed))
                {
                    yield return parsed;
                }
            }
        }
    }
}
