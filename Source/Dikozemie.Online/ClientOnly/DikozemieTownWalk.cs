using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Dikozemie.Online
{
    // Stage 4 of the plan («Прогулка по городу»): the town menu opens the real
    // singleplayer 3D scene of the place. The hero walks the streets, enters
    // the tavern, the arena and the lord's hall through their doors, talks to
    // the people and may end up in a street fight. The walk is a local
    // mission of this computer, as the road fights are.
    internal enum DikozemieWalkPlace
    {
        // The streets of a town, the village green or the castle yard.
        Streets,
        Tavern,
        Arena,
        Hall
    }

    // Where the town screen goes when the walk ends.
    internal enum DikozemieWalkExit
    {
        Town,
        Tavern,
        Market,
        Armory,
        Exchange,
        Contracts,
        Workshops,
        People,
        ArenaFights,
        Wanted,
        Port,
        Castle
    }

    internal sealed class DikozemieTownWalkSetup
    {
        internal DikozemieLobbyMapNode Node;
        internal DikozemieWalkPlace Place;
        // The door the hero came out of (null: from the town menu).
        internal DikozemieWalkPlace? From;
        internal string Scene = string.Empty;
        internal string Levels = string.Empty;
        internal string Season = "summer";
        internal int Hour = 12;
        internal bool Night;
        internal string Culture = "empire";

        internal bool IsTown => DikozemieTown.IsTown(Node);
        internal bool IsCastle => DikozemieTown.IsCastle(Node);
        internal bool IsVillage => DikozemieTown.IsVillage(Node);
    }

    internal static class DikozemieTownWalk
    {
        internal const string MissionName = "DikozemieTownWalk";
        private const string HeroFallbackId = "mp_heavy_infantry_empire_troop";

        private static DikozemieTownWalkSetup _nextDoor;
        private static DikozemieWalkExit? _exit;
        private static string _exitNote = string.Empty;
        private static bool? _sandBoxLoaded;

        internal static DikozemieTownWalkSetup Active { get; private set; }

        internal static bool IsRunning => Active != null && Mission.Current != null &&
            string.Equals(Mission.Current.SceneName, Active.Scene, StringComparison.OrdinalIgnoreCase);

        // The scenes of the walk live in the SandBox module, and its scripts
        // (doors, benches, market stalls) in SandBox.dll: the Arena shortcut
        // starts the game with it; the TaleWorlds launcher's multiplayer tab
        // does not.
        internal static bool IsSandBoxLoaded
        {
            get
            {
                if (_sandBoxLoaded == null)
                {
                    _sandBoxLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
                        string.Equals(assembly.GetName().Name, "SandBox", StringComparison.OrdinalIgnoreCase));
                }
                return _sandBoxLoaded.Value;
            }
        }

        internal static DikozemieTownScenes ScenesOf(DikozemieLobbyMapNode node) =>
            node == null ? new DikozemieTownScenes() : DikozemieTownCatalog.Get(node.Id).Scenes ?? new DikozemieTownScenes();

        internal static string SceneFor(DikozemieLobbyMapNode node, DikozemieWalkPlace place)
        {
            DikozemieTownScenes scenes = ScenesOf(node);
            switch (place)
            {
                case DikozemieWalkPlace.Tavern: return scenes.Tavern ?? string.Empty;
                case DikozemieWalkPlace.Arena: return scenes.Arena ?? string.Empty;
                case DikozemieWalkPlace.Hall: return scenes.Hall ?? string.Empty;
                default: return scenes.Center ?? string.Empty;
            }
        }

        internal static bool CanWalk(DikozemieLobbyMapNode node) =>
            node != null && !string.IsNullOrEmpty(SceneFor(node, DikozemieWalkPlace.Streets));

        // «Прогуляться по городу» in the menu of the place.
        internal static string MenuLabel(DikozemieLobbyMapNode node) =>
            DikozemieTown.IsCastle(node)
                ? "ПРОЙТИСЬ ПО ЗАМКУ"
                : DikozemieTown.IsVillage(node) ? "ПРОЙТИСЬ ПО ДЕРЕВНЕ" : "ПРОГУЛЯТЬСЯ ПО ГОРОДУ";

        internal static string PlaceName(DikozemieTownWalkSetup setup)
        {
            if (setup == null)
            {
                return string.Empty;
            }
            switch (setup.Place)
            {
                case DikozemieWalkPlace.Tavern: return "ТАВЕРНА";
                case DikozemieWalkPlace.Arena: return "АРЕНА";
                case DikozemieWalkPlace.Hall: return "ЗАЛ ЛОРДА";
                default:
                    return setup.IsCastle ? "ДВОР ЗАМКА" : setup.IsVillage ? "ДЕРЕВНЯ" : "УЛИЦЫ ГОРОДА";
            }
        }

        internal static bool TryStart(DikozemieLobbyMapNode node, DikozemieWalkPlace place,
            DikozemieWalkPlace? from, out string error)
        {
            error = string.Empty;
            if (node == null)
            {
                error = "Вы не в городе.";
                return false;
            }
            if (!IsSandBoxLoaded)
            {
                error = "Сцены городов не загружены: запустите игру ярлыком «Арена».";
                return false;
            }
            string scene = SceneFor(node, place);
            if (string.IsNullOrEmpty(scene))
            {
                error = "Здесь нечего обходить пешком.";
                return false;
            }
            DikozemieTownScenes scenes = ScenesOf(node);
            string culture = DikozemieTownFolk.FolkCulture(node.Culture);
            var setup = new DikozemieTownWalkSetup
            {
                Node = node,
                Place = place,
                From = from,
                Scene = scene,
                Culture = culture,
                Season = DikozemieRoadScenes.Season(culture == "nord" ? "sturgia" : culture),
                Hour = DikozemieRoadScenes.AtmosphereHour(DikozemieRoads.HourOfDay),
                Night = DikozemieRoads.IsNight
            };
            // As the singleplayer game opens them: the streets, the castle yard
            // and the rooms show the buildings of the walls' level; the arena and
            // a village have no levels.
            bool levelled = place != DikozemieWalkPlace.Arena && !DikozemieTown.IsVillage(node) && scenes.Wall > 0;
            setup.Levels = levelled ? "civilian level_" + Math.Min(3, scenes.Wall) : string.Empty;
            return TryStart(setup, out error);
        }

        private static bool TryStart(DikozemieTownWalkSetup setup, out string error)
        {
            error = string.Empty;
            if (Mission.Current != null)
            {
                error = "Сначала завершите текущий бой.";
                return false;
            }
            if (DikozemiePvpSearchController.IsSearching)
            {
                DikozemiePvpSearchController.Cancel();
            }
            // As in a road fight: the multiplayer stat model needs a
            // multiplayer troop under every agent (2026-11-07).
            BasicCharacterObject hero =
                MBObjectManager.Instance.GetObject<BasicCharacterObject>(
                    DikozemieRoadFolk.FaceTroop(DikozemieHeroProfile.CultureId)) ??
                MBObjectManager.Instance.GetObject<BasicCharacterObject>(HeroFallbackId);
            if (hero == null)
            {
                error = "Нет данных героя.";
                return false;
            }
            try
            {
                Active = setup;
                _exit = null;
                _exitNote = string.Empty;
                Game.Current.PlayerTroop = hero;
                DikozemieArenaLobbyMenuController.SetMapVisible(false);
                var record = new MissionInitializerRecord(setup.Scene)
                {
                    DoNotUseLoadingScreen = false,
                    PlayingInCampaignMode = false,
                    SceneLevels = setup.Levels,
                    // DecalAtlasGroup.Town, as the singleplayer town missions.
                    DecalAtlasGroup = 3,
                    AtmosphereOnCampaign = BannerlordMissions.CreateAtmosphereInfoForMission(
                        setup.Season, setup.Hour)
                };
                DikozemieLobbyShellIntegration.TraceButtonAction(
                    "town walk start node=" + setup.Node.Id + " place=" + setup.Place + " from=" +
                    (setup.From?.ToString() ?? "menu") + " scene=" + setup.Scene + " levels='" + setup.Levels +
                    "' hour=" + setup.Hour + " night=" + setup.Night);
                MissionState.OpenNew(
                    MissionName,
                    record,
                    mission => Behaviors(hero, setup),
                    true,
                    true);
                return true;
            }
            catch (Exception exception)
            {
                Active = null;
                Console.WriteLine("DikozemieOnline: town walk failed to open: " + exception);
                error = "Не удалось открыть сцену города.";
                return false;
            }
        }

        private static IEnumerable<MissionBehavior> Behaviors(BasicCharacterObject hero, DikozemieTownWalkSetup setup)
        {
            var behaviors = new List<MissionBehavior>
            {
                new AgentHumanAILogic(),
                new MissionHardBorderPlacer(),
                // The game froze on a scene without any boundary (2026-11-09).
                new MissionBoundaryPlacer()
            };
            // Walking out of the town gate is leaving the town, as in the
            // singleplayer game. The arena's boundary is the sand: the
            // arena master and the stands are outside it, so no countdown there.
            if (setup.Place != DikozemieWalkPlace.Arena)
            {
                behaviors.Add(new MissionBoundaryCrossingHandler());
            }
            behaviors.Add(new DikozemieTownWalkController(hero, setup));
            return behaviors;
        }

        // Through a door: this scene closes, the next one opens as soon as
        // the game is back from it.
        internal static void GoThrough(DikozemieWalkPlace place)
        {
            DikozemieTownWalkSetup current = Active;
            if (current == null)
            {
                return;
            }
            DikozemieTownScenes scenes = ScenesOf(current.Node);
            string scene = SceneFor(current.Node, place);
            if (string.IsNullOrEmpty(scene))
            {
                return;
            }
            bool levelled = place != DikozemieWalkPlace.Arena && !current.IsVillage && scenes.Wall > 0;
            _nextDoor = new DikozemieTownWalkSetup
            {
                Node = current.Node,
                Place = place,
                From = current.Place,
                Scene = scene,
                Culture = current.Culture,
                Season = current.Season,
                Hour = DikozemieRoadScenes.AtmosphereHour(DikozemieRoads.HourOfDay),
                Night = DikozemieRoads.IsNight,
                Levels = levelled ? "civilian level_" + Math.Min(3, scenes.Wall) : string.Empty
            };
            DikozemieLobbyShellIntegration.TraceButtonAction(
                "town walk door " + current.Place + " -> " + place + " scene=" + scene);
            EndMission();
        }

        // Out of the walk: back to the menu of the place, maybe straight to
        // one of its sections (the market, the trade yard, the arena page).
        internal static void Leave(DikozemieWalkExit exit, string note = "")
        {
            _nextDoor = null;
            _exit = exit;
            _exitNote = note ?? string.Empty;
            DikozemieLobbyShellIntegration.TraceButtonAction(
                "town walk leave exit=" + exit + " place=" + Active?.Place);
            EndMission();
        }

        private static void EndMission()
        {
            DikozemieLobbyShellIntegration.PrepareForTownWalkExit();
            Mission mission = Mission.Current;
            if (mission != null && !mission.MissionEnded && !mission.IsMissionEnding)
            {
                mission.EndMission();
            }
        }

        internal static bool HasDoorPending => _nextDoor != null;

        // The shell calls this once the lobby is back after a scene.
        internal static bool TryContinueThroughDoor()
        {
            DikozemieTownWalkSetup next = _nextDoor;
            if (next == null || Mission.Current != null)
            {
                return false;
            }
            _nextDoor = null;
            if (!TryStart(next, out string error))
            {
                Active = null;
                _exit = DikozemieWalkExit.Town;
                _exitNote = error;
                return false;
            }
            return true;
        }

        internal static bool TryConsumeExit(out DikozemieWalkExit exit, out string note)
        {
            exit = DikozemieWalkExit.Town;
            note = string.Empty;
            if (_exit == null || Mission.Current != null || _nextDoor != null)
            {
                return false;
            }
            exit = _exit.Value;
            note = _exitNote;
            _exit = null;
            _exitNote = string.Empty;
            Active = null;
            return true;
        }

        // The walk ended without a word (the game closed the scene): back to
        // the town menu all the same.
        internal static void OnMissionGone()
        {
            if (Active != null && _exit == null && _nextDoor == null)
            {
                _exit = DikozemieWalkExit.Town;
                DikozemieLobbyShellIntegration.TraceButtonAction("town walk ended by the game: back to the town menu");
                DikozemieLobbyShellIntegration.PrepareForTownWalkExit();
            }
        }
    }
}
