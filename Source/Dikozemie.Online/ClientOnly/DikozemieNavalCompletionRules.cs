using System;

namespace Dikozemie.Online
{
    internal static class DikozemieNavalCompletionRules
    {
        internal static readonly TimeSpan OverboardGrace =
            TimeSpan.FromSeconds(8d);
        internal static readonly TimeSpan MissionExitRetryDelay =
            TimeSpan.FromSeconds(5d);
        internal static readonly TimeSpan NativeShutdownFallbackDelay =
            TimeSpan.FromSeconds(10d);
        internal static readonly TimeSpan LobbyLoginWait =
            TimeSpan.FromSeconds(45d);

        internal static bool ShouldResolveSwimmer(
            bool boardingConnected,
            bool activeHuman,
            bool inWater,
            TimeSpan swimmingFor)
        {
            return boardingConnected && activeHuman && inWater &&
                swimmingFor >= OverboardGrace;
        }

        internal static bool ShouldRetryMissionExit(
            bool missionStillOpen,
            TimeSpan returnElapsed)
        {
            return missionStillOpen &&
                returnElapsed >= MissionExitRetryDelay;
        }

        // Bannerlord 1.5.7 starts the multiplayer game again by itself after
        // the custom game ends; the return then only waits for its lobby.
        internal static bool IsMultiplayerBackOnItsOwn(
            bool customGameEnded,
            bool multiplayerGameActive)
        {
            return customGameEnded && multiplayerGameActive;
        }

        // The lobby's own login (and its retries) may take longer: do not keep
        // the lobby locked as "a naval battle is loading" past this wait.
        internal static bool ShouldStopWaitingForLobbyLogin(
            bool multiplayerLobbyActive,
            TimeSpan waited)
        {
            return multiplayerLobbyActive && waited >= LobbyLoginWait;
        }

        internal static bool ShouldForceNativeShutdown(
            bool missionStillOpen,
            TimeSpan returnElapsed)
        {
            return missionStillOpen &&
                returnElapsed >= NativeShutdownFallbackDelay;
        }
    }
}
