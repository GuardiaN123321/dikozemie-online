using System;

namespace Dikozemie.Online
{
    internal static class DikozemieArenaSceneRules
    {
        internal const string ImperialArenaScene = "arena_empire_a";
        internal const string DesertArenaScene = "arena_aserai_a";
        internal const string FieldBattleScene = "dikozemie_desert_field_a";
        internal const string NavalBoardingScene = "dikozemie_open_sea_a";
        internal const string ImperialServerMarker = "Арена — имперская";
        internal const string DesertServerMarker = "Арена — пустынная";
        internal const string FieldServerMarker =
            "Арена — ровное пустынное поле";
        // Optional format tags in a server name: the lobby search prefers a
        // server reserved for the chosen format, so duellists meet duellists
        // and team players gather on one team server.
        internal const string DuelServerTag = "дуэли";
        internal const string TeamServerTag = "командные";
        internal const int MaximumImperialArenaTeamSize = 10;
        internal const int MinimumMountedDesertTeamSize = 10;
        internal const int MinimumMountedFieldTeamSize = 25;

        internal static bool IsNavalBoardingScene(string scene) =>
            string.Equals(
                scene,
                NavalBoardingScene,
                StringComparison.OrdinalIgnoreCase);

        internal static string ResolveScene(
            int teamSize,
            DikozemieArenaDiscipline discipline)
        {
            if (discipline == DikozemieArenaDiscipline.Mounted)
            {
                return teamSize >= MinimumMountedFieldTeamSize
                    ? FieldBattleScene
                    : teamSize >= MinimumMountedDesertTeamSize
                        ? DesertArenaScene
                        : ImperialArenaScene;
            }
            return teamSize <= MaximumImperialArenaTeamSize
                    ? ImperialArenaScene
                    : DesertArenaScene;
        }

        internal static string FormatSceneName(string scene)
        {
            if (string.Equals(
                scene,
                ImperialArenaScene,
                StringComparison.OrdinalIgnoreCase))
            {
                return "имперская арена";
            }
            if (string.Equals(
                scene,
                FieldBattleScene,
                StringComparison.OrdinalIgnoreCase))
            {
                return "ровное пустынное поле";
            }
            if (IsNavalBoardingScene(scene))
            {
                return "сетевая гавань";
            }
            return "пустынная арена";
        }

        internal static string ResolveServerMarker(
            int teamSize,
            DikozemieArenaDiscipline discipline)
        {
            string scene = ResolveScene(teamSize, discipline);
            if (string.Equals(
                scene,
                ImperialArenaScene,
                StringComparison.OrdinalIgnoreCase))
            {
                return ImperialServerMarker;
            }
            return string.Equals(
                scene,
                FieldBattleScene,
                StringComparison.OrdinalIgnoreCase)
                    ? FieldServerMarker
                    : DesertServerMarker;
        }

        internal static bool IsTeamSizeAllowedOnScene(
            string scene,
            int teamSize,
            DikozemieArenaDiscipline discipline)
        {
            return string.Equals(
                scene,
                ResolveScene(teamSize, discipline),
                StringComparison.OrdinalIgnoreCase);
        }

        // 1 = the server is reserved for this format, 0 = it takes every
        // format, -1 = it is reserved for the other format (still usable
        // when nothing better is free: every arena server runs both).
        internal static int RateServerForFormat(string serverName, int teamSize)
        {
            string name = serverName ?? string.Empty;
            bool duel = name.IndexOf(
                DuelServerTag,
                StringComparison.OrdinalIgnoreCase) >= 0;
            bool team = name.IndexOf(
                TeamServerTag,
                StringComparison.OrdinalIgnoreCase) >= 0;
            if (duel == team)
            {
                return 0;
            }
            return (teamSize <= 1) == duel ? 1 : -1;
        }

        internal static bool IsServerNameForTeamSize(
            string serverName,
            int teamSize,
            DikozemieArenaDiscipline discipline)
        {
            string marker = ResolveServerMarker(teamSize, discipline);
            return (serverName?.IndexOf(
                marker,
                StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
        }
    }
}
