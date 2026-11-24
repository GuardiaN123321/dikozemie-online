using System;
using System.IO;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Multiplayer;
using TaleWorlds.MountAndBlade.Diamond;
using TaleWorlds.MountAndBlade.Multiplayer.Missions;
using TaleWorlds.MountAndBlade.Source.Missions;

namespace Dikozemie.Online
{
    internal static class DikozemieArenaLobbyController
    {
        internal const string LocalArenaMissionName = "MultiplayerPractice";

        private static readonly object Gate = new object();
        private static string _status = "Загружаем имперскую арену…";
        private static bool _isBusy;
        private static bool _hostRequested;
        private static DateTimeOffset _hostingRequestedAt;

        internal static string Status
        {
            get
            {
                lock (Gate)
                {
                    if (DikozemiePvpSearchController.IsNavalSearch)
                    {
                        return DikozemiePvpSearchController.Status;
                    }
                    if (DikozemieNavalBattleLauncher.IsPending)
                    {
                        return DikozemieNavalBattleLauncher.Status;
                    }
                    return _status;
                }
            }
        }

        internal static bool IsBusy
        {
            get
            {
                lock (Gate)
                {
                    return _isBusy;
                }
            }
        }

        internal static void EnterArena()
        {
            if (!DikozemieArenaMatchSelection.TryValidateCurrent(
                    out string loadoutError))
            {
                lock (Gate)
                {
                    _isBusy = false;
                    _hostRequested = false;
                    _status = loadoutError;
                }
                return;
            }

            lock (Gate)
            {
                if (_isBusy || Mission.Current != null)
                {
                    return;
                }

                _isBusy = true;
                _hostRequested = true;
                _status = "Загружаем большую асерайскую арену…";
            }

            DikozemieArenaRuntimeSettings.Configure(
                winsRequired: 1,
                opponentWaitSeconds: 5,
                botDifficulty: DikozemieArenaBotDifficulty.Hard);
        }

        internal static bool TryStartNavalBattle(out string errorMessage)
        {
            return DikozemieNavalBattleLauncher.TryStart(
                DikozemieArenaMatchSelection.SelectedNavalBattle,
                out errorMessage);
        }

        internal static bool StartTrainingBattle(int variant, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (Mission.Current != null)
            {
                errorMessage = "Сначала завершите текущий бой.";
                return false;
            }

            try
            {
                DikozemieLocalArenaMission.OpenTrainingBattle(variant);
                return true;
            }
            catch (Exception error)
            {
                Console.WriteLine(
                    "DikozemieOnline: local training battle failed: " + error);
                errorMessage = "Не удалось загрузить тренировочный бой.";
                return false;
            }
        }

        internal static bool StartTournament(out string errorMessage)
        {
            errorMessage = string.Empty;
            if (Mission.Current != null)
            {
                errorMessage = "Сначала завершите текущий бой.";
                return false;
            }

            try
            {
                DikozemieLocalArenaMission.OpenTournament();
                return true;
            }
            catch (Exception error)
            {
                Console.WriteLine(
                    "DikozemieOnline: local tournament failed: " + error);
                errorMessage = "Не удалось загрузить турнир.";
                return false;
            }
        }

        internal static void Tick()
        {
            bool shouldHost;
            lock (Gate)
            {
                shouldHost = _hostRequested;
                _hostRequested = false;
            }

            if (shouldHost)
            {
                HostLocalArena();
            }

            if (_hostingRequestedAt != default &&
                DateTimeOffset.UtcNow - _hostingRequestedAt > TimeSpan.FromSeconds(25) &&
                Mission.Current == null &&
                Game.Current?.GameStateManager?.ActiveState is LobbyState lobby &&
                lobby.LobbyClient?.AtLobby == true)
            {
                _hostingRequestedAt = default;
                Fail("Не удалось загрузить имперскую арену.");
            }
        }

        internal static void Reset()
        {
            lock (Gate)
            {
                _isBusy = false;
                _hostRequested = false;
                _status = "Загружаем имперскую арену…";
            }
            _hostingRequestedAt = default;
        }

        internal static DikozemieArenaCharacterProfile CreateDirectLobbyProfile()
        {
            NetworkCommunicator peer = GameNetwork.MyPeer;
            // In a direct custom-game join Bannerlord exposes the local mission
            // peer before its client-side PlayerID becomes valid.  The server
            // already has the authoritative id at this point, so do not block
            // the saved hero profile on the client's late id initialization.
            string platformPlayerId =
                peer?.PlayerConnectionInfo != null &&
                peer.PlayerConnectionInfo.PlayerID.IsValid
                    ? peer.PlayerConnectionInfo.PlayerID.ToString()
                    : string.Empty;
            DikozemieHeroProfileState saved = DikozemieHeroProfile.Snapshot;
            string displayName = !string.IsNullOrWhiteSpace(peer?.UserName)
                ? peer.UserName
                : !string.IsNullOrWhiteSpace(saved?.Name)
                    ? saved.Name
                    : "Боец";
            PlayerData playerData = NetworkMain.GameClient?.PlayerData;
            return DikozemieHeroProfile.CreateNetworkProfile(
                platformPlayerId,
                displayName,
                playerData?.BodyProperties.ToString() ?? string.Empty,
                playerData?.IsFemale ?? false,
                0);
        }

        private static void HostLocalArena()
        {
            if (!(Game.Current?.GameStateManager?.ActiveState is LobbyState lobby) ||
                lobby.LobbyClient == null || !lobby.LobbyClient.Connected ||
                !lobby.LobbyClient.AtLobby)
            {
                Fail("Сетевое лобби ещё не готово. Вернитесь на главную и снова откройте арену.");
                return;
            }

            try
            {
                string arenaScene = DikozemieArenaSceneRules.ResolveScene(
                    DikozemieArenaMatchSelection.TeamSize,
                    DikozemieArenaMatchSelection.Discipline);
                MultiplayerOptions.OptionType.GameType.SetValue(
                    DikozemieSubModule.ArenaGameType);
                MultiplayerOptions.OptionType.Map.SetValue(arenaScene);
                MultiplayerOptions.OptionType.ServerName.SetValue(
                    DikozemieArenaSceneRules.ResolveServerMarker(
                        DikozemieArenaMatchSelection.TeamSize,
                        DikozemieArenaMatchSelection.Discipline));
                MultiplayerOptions.OptionType.WelcomeMessage.SetValue(
                    "Бой один на один начинается сразу.");
                MultiplayerOptions.OptionType.CultureTeam1.SetValue("empire");
                MultiplayerOptions.OptionType.CultureTeam2.SetValue("empire");
                MultiplayerOptions.OptionType.MaxNumberOfPlayers.SetValue(52);
                MultiplayerOptions.OptionType.MinNumberOfPlayersForMatchStart.SetValue(1);
                MultiplayerOptions.OptionType.NumberOfBotsTeam1.SetValue(0);
                MultiplayerOptions.OptionType.NumberOfBotsTeam2.SetValue(0);
                MultiplayerOptions.OptionType.NumberOfBotsPerFormation.SetValue(0);
                MultiplayerOptions.OptionType.RespawnPeriodTeam1.SetValue(3);
                MultiplayerOptions.OptionType.RespawnPeriodTeam2.SetValue(3);
                MultiplayerOptions.OptionType.RoundTimeLimit.SetValue(300);
                MultiplayerOptions.OptionType.MapTimeLimit.SetValue(30);
                MultiplayerOptions.OptionType.DisableInactivityKick.SetValue(true);

                _hostingRequestedAt = DateTimeOffset.UtcNow;
                DikozemieLocalArenaMission.Open(arenaScene);
                _hostingRequestedAt = default;
                lock (Gate)
                {
                    _status = "Бой начался";
                }
                Console.WriteLine(
                    "DikozemieOnline: local arena mission opened scene=" +
                    arenaScene);
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: 3D arena hosting failed: " + error);
                Fail("Не удалось создать тестовую арену.");
            }
        }

        private static string GetOrCreateDirectCharacterId()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Dikozemie",
                "arena");
            string path = Path.Combine(folder, "multiplayer-character-id.txt");
            try
            {
                if (File.Exists(path) &&
                    Guid.TryParse(File.ReadAllText(path).Trim(), out Guid existing) &&
                    existing != Guid.Empty)
                {
                    return existing.ToString();
                }

                Directory.CreateDirectory(folder);
                string created = Guid.NewGuid().ToString();
                File.WriteAllText(path, created);
                return created;
            }
            catch (Exception error)
            {
                Console.WriteLine(
                    "DikozemieOnline: direct character id persistence failed: " +
                    error.Message);
                return Guid.NewGuid().ToString();
            }
        }

        private static void Fail(string message)
        {
            lock (Gate)
            {
                _isBusy = false;
                _hostRequested = false;
                _status = message;
            }
            InformationManager.DisplayMessage(
                new InformationMessage(message, Colors.Red));
        }
    }
}
