using System;
using System.IO;
using System.Threading.Tasks;
using SandBox;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Diamond;
using TaleWorlds.SaveSystem.Load;
using TaleWorlds.ScreenSystem;

namespace Dikozemie.Online
{
    internal static class DikozemieRankedHandoffClient
    {
        private static readonly TimeSpan LobbyReadyTimeout = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(75);
        private static readonly TimeSpan MissionStartTimeout = TimeSpan.FromSeconds(90);
        private static readonly TimeSpan LobbyReturnTimeout = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan MissingHandoffGrace = TimeSpan.FromSeconds(5);
        private const string ArenaGameType = "DikozemieArena";
        private const string ArenaServerNameMarker = "Арена —";

        private static readonly object JoinResultGate = new object();
        private static bool _screenEventsRegistered;
        private static bool _joinStarted;
        private static bool _joinCompleted;
        private static bool _joinResultReady;
        private static bool _joinResultSucceeded;
        private static string _joinResultFailure;
        private static int _joinGeneration;
        private static bool _missionObserved;
        private static bool _returnStarted;
        private static bool _terminalRecovery;
        private static bool _terminalExitStarted;
        private static bool _invalidHandoffReported;
        private static bool _handoffLoaded;
        private static bool _directLobbyMode;
        private static Guid _battleId;
        private static DikozemieArenaHandoffState _handoff;
        private static DikozemieArenaDirectResult _directResult;
        private static DateTimeOffset _directResultReceivedAt;
        private static DateTimeOffset _handoffLoadedAt;
        private static DateTimeOffset _joinStartedAt;
        private static DateTimeOffset _joinCompletedAt;
        private static DateTimeOffset _missionEndedAt;
        private static DateTimeOffset _multiplayerEnteredAt;

        private static string HandoffPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Dikozemie",
            "arena",
            "handoff.json");

        internal static DikozemieArenaCharacterProfile CharacterProfile =>
            _handoff?.CharacterProfile;

        internal static void RecordMatchResult(
            bool victory,
            int delta,
            int rewardGold,
            int rating,
            int wins,
            int losses)
        {
            if (_handoff == null || string.IsNullOrWhiteSpace(_handoff.MatchId) ||
                string.IsNullOrWhiteSpace(_handoff.CharacterId))
            {
                return;
            }

            _directResult = new DikozemieArenaDirectResult
            {
                ResultId = Guid.NewGuid().ToString(),
                MatchId = _handoff.MatchId,
                CharacterId = _handoff.CharacterId,
                Victory = victory,
                Delta = Math.Max(0, delta),
                RewardGold = Math.Max(0, rewardGold),
                Rating = Math.Max(0, rating),
                Wins = Math.Max(0, wins),
                Losses = Math.Max(0, losses)
            };
            _directResultReceivedAt = DateTimeOffset.UtcNow;
        }

        public static void Initialize()
        {
            if (_screenEventsRegistered)
            {
                return;
            }

            ScreenManager.OnPushScreen += OnScreenPushed;
            ScreenManager.OnPopScreen += OnScreenPopped;
            _screenEventsRegistered = true;
        }

        public static void Shutdown()
        {
            if (_screenEventsRegistered)
            {
                ScreenManager.OnPushScreen -= OnScreenPushed;
                ScreenManager.OnPopScreen -= OnScreenPopped;
                _screenEventsRegistered = false;
            }
            DikozemieRankedTransitionController.Hide();
            ResetForNextMatch();
        }

        public static void Tick()
        {
            if (GameNetwork.IsDedicatedServer)
            {
                return;
            }

            if (_terminalRecovery)
            {
                DikozemieRankedTransitionController.Show(
                    "Не удалось восстановить бой. Закрываем сетевую сессию без потери кампании…");
                if (!_terminalExitStarted)
                {
                    _terminalExitStarted = true;
                    MBGameManager.EndGame();
                }

                if (!(Game.Current?.GameType is MultiplayerGame) && !IsNativeLobbyVisible())
                {
                    ResetForNextMatch();
                    DikozemieRankedTransitionController.Hide();
                }
                return;
            }

            if (_returnStarted && Game.Current?.GameType is Campaign)
            {
                // Keep the cover until the multiplayer lobby is actually gone.
                // GameType changes before every screen transition has necessarily
                // completed.
                if (!IsNativeLobbyVisible())
                {
                    DikozemieArenaStateFiles.TryDelete(HandoffPath);
                    ResetForNextMatch();
                    DikozemieRankedTransitionController.Hide();
                }
                return;
            }

            // Both Dikozemie modules are loaded during the campaign. The
            // campaign-side producer publishes handoff.json only after SaveAs
            // has completed; never let this multiplayer consumer race that save,
            // reject the fresh handoff, or consume a stale one from a campaign.
            if (!(Game.Current?.GameType is MultiplayerGame))
            {
                return;
            }

            if (_multiplayerEnteredAt == default)
            {
                _multiplayerEnteredAt = DateTimeOffset.UtcNow;
            }

            if (_directLobbyMode)
            {
                DikozemieRankedTransitionController.Hide();
                return;
            }

            if (Mission.Current == null)
            {
                DikozemieRankedTransitionController.Show(
                    _returnStarted
                        ? "Возвращаем героя в кампанию…"
                        : "Ищем арену в сетевом лобби TaleWorlds…");
            }

            if (!_handoffLoaded && File.Exists(HandoffPath))
            {
                LoadHandoff();
            }

            if (_returnStarted || _terminalRecovery)
            {
                return;
            }

            if (!_handoffLoaded)
            {
                if (DateTimeOffset.UtcNow - _multiplayerEnteredAt >= MissingHandoffGrace)
                {
                    if (!_directLobbyMode)
                    {
                        _directLobbyMode = true;
                        DikozemieRankedTransitionController.Hide();
                        Console.WriteLine("DikozemieOnline: no campaign handoff; keeping the native multiplayer lobby open.");
                    }
                }
                return;
            }

            ProcessJoinResult();
            if (_returnStarted || _terminalRecovery)
            {
                return;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (Mission.Current != null && GameNetwork.IsClient)
            {
                _missionObserved = true;
                _missionEndedAt = default;
                DikozemieRankedTransitionController.Hide();
            }
            else if (_missionObserved && _missionEndedAt == default)
            {
                _missionEndedAt = now;
            }

            if (_directResult != null && Mission.Current != null &&
                now - _directResultReceivedAt >= TimeSpan.FromSeconds(2))
            {
                ReturnToCampaign(stageResult: true);
                return;
            }

            if (_joinCompleted && _missionObserved && Mission.Current == null &&
                Game.Current?.GameStateManager?.ActiveState is LobbyState)
            {
                ReturnToCampaign();
                return;
            }

            if (!_joinStarted && now - _handoffLoadedAt >= LobbyReadyTimeout)
            {
                FailAndReturn("сетевое лобби не открылось вовремя");
                return;
            }
            if (_joinStarted && !_joinCompleted && now - _joinStartedAt >= JoinTimeout)
            {
                FailAndReturn("сервер боя не ответил вовремя");
                return;
            }
            if (_joinCompleted && !_missionObserved && now - _joinCompletedAt >= MissionStartTimeout)
            {
                FailAndReturn("бой не начал загружаться");
                return;
            }
            if (_missionObserved && Mission.Current == null && _missionEndedAt != default &&
                now - _missionEndedAt >= LobbyReturnTimeout)
            {
                FailAndReturn("после боя не удалось вернуться через сетевое лобби");
                return;
            }

            if (_joinStarted || !(Game.Current?.GameStateManager?.ActiveState is LobbyState lobby))
            {
                return;
            }

            LobbyClient client = lobby.LobbyClient;
            if (client == null || !client.Connected || !client.AtLobby)
            {
                return;
            }

            _joinStarted = true;
            _joinStartedAt = now;
            DikozemieRankedTransitionController.Show("Ищем свободную арену 1×1…");
            int generation;
            lock (JoinResultGate)
            {
                generation = ++_joinGeneration;
            }
            _ = DiscoverAndJoinAsync(client, generation);
        }

        private static async Task DiscoverAndJoinAsync(
            LobbyClient client,
            int generation)
        {
            bool succeeded = false;
            string failure = null;
            try
            {
                Guid battleId = _battleId;
                GameServerEntry arena = null;
                if (battleId == Guid.Empty)
                {
                    for (int attempt = 0; attempt < 20 && arena == null; attempt++)
                    {
                        AvailableCustomGames games = await client.GetCustomGameServerList();
                        arena = games?.CustomGameServerInfos?.Find(server =>
                            server != null && !server.PasswordProtected &&
                            string.Equals(
                                server.GameType,
                                ArenaGameType,
                                StringComparison.OrdinalIgnoreCase) &&
                            (server.ServerName?.IndexOf(
                                ArenaServerNameMarker,
                                StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 &&
                            DikozemiePvpSearchController.HasFighterSeat(server));
                        if (arena == null)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(3));
                        }
                    }

                    if (arena == null || arena.Id == CustomBattleId.Empty)
                    {
                        failure = "свободный сервер арены не найден";
                        return;
                    }

                    battleId = arena.Id.Guid;
                    _battleId = battleId;
                }

                bool joined = await client.RequestJoinCustomGame(
                    new CustomBattleId(battleId),
                    CustomGameJoinType.Player,
                    string.Empty);
                if (!joined)
                {
                    failure = "сетевой сервер отклонил подключение";
                }
                else
                {
                    succeeded = true;
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: ranked auto-join failed: " + error);
                failure = "не удалось подключить рейтинговый бой";
            }
            finally
            {
                lock (JoinResultGate)
                {
                    if (generation == _joinGeneration)
                    {
                        _joinResultSucceeded = succeeded;
                        _joinResultFailure = failure;
                        _joinResultReady = true;
                    }
                }
            }
        }

        private static void ProcessJoinResult()
        {
            bool ready;
            bool succeeded;
            string failure;
            lock (JoinResultGate)
            {
                ready = _joinResultReady;
                succeeded = _joinResultSucceeded;
                failure = _joinResultFailure;
                _joinResultReady = false;
                _joinResultFailure = null;
            }

            if (!ready || _returnStarted)
            {
                return;
            }
            if (!succeeded)
            {
                FailAndReturn(failure ?? "сервер боя отклонил подключение");
                return;
            }

            _joinCompleted = true;
            _joinCompletedAt = DateTimeOffset.UtcNow;
            Console.WriteLine("DikozemieOnline: auto-joined ranked battle " + _battleId);
        }

        private static void LoadHandoff()
        {
            try
            {
                if (!DikozemieArenaStateFiles.TryRead(HandoffPath, out DikozemieArenaHandoffState handoff))
                {
                    RejectHandoff("файл перехода повреждён");
                    return;
                }

                // Retain the return-save name before validating the network
                // envelope. An expired or unsupported match must still be able
                // to restore the campaign that was saved before handoff.
                _handoff = handoff;

                bool hasBattleId = Guid.TryParse(handoff.BattleId, out Guid battleId) &&
                    battleId != Guid.Empty;
                if (string.IsNullOrWhiteSpace(handoff.ReturnSave) ||
                    (handoff.SchemaVersion < 3 && !hasBattleId))
                {
                    RejectHandoff("в переходе нет сервера или сохранения");
                    return;
                }
                if (handoff.SchemaVersion < 0 || handoff.SchemaVersion > 3)
                {
                    RejectHandoff("версия перехода не поддерживается");
                    return;
                }
                if (handoff.SchemaVersion >= 1 && handoff.SchemaVersion <= 2 &&
                    (string.IsNullOrWhiteSpace(handoff.HandoffId) ||
                     string.IsNullOrWhiteSpace(handoff.MatchId) ||
                     string.IsNullOrWhiteSpace(handoff.PlayerId) ||
                     string.IsNullOrWhiteSpace(handoff.PlayerToken) ||
                     string.IsNullOrWhiteSpace(handoff.MatchmakerUrl)))
                {
                    RejectHandoff("переход не содержит защищённый контекст матча");
                    return;
                }
                if (handoff.SchemaVersion >= 2 &&
                    (!Guid.TryParse(handoff.CharacterId, out Guid characterId) ||
                     characterId == Guid.Empty))
                {
                    RejectHandoff("в переходе нет персонажа кампании");
                    return;
                }
                if (handoff.SchemaVersion >= 3 &&
                    (string.IsNullOrWhiteSpace(handoff.HandoffId) ||
                     string.IsNullOrWhiteSpace(handoff.MatchId) ||
                     string.IsNullOrWhiteSpace(handoff.PlayerId) ||
                     handoff.CharacterProfile == null ||
                     !string.Equals(
                         handoff.CharacterProfile.CharacterId,
                         handoff.CharacterId,
                         StringComparison.OrdinalIgnoreCase) ||
                     !string.Equals(
                         handoff.CharacterProfile.PlatformPlayerId,
                         handoff.PlayerId,
                         StringComparison.Ordinal)))
                {
                    RejectHandoff("переход не содержит профиль бойца");
                    return;
                }

                DateTimeOffset expiresAt;
                if (!DateTimeOffset.TryParse(handoff.ExpiresAt, out expiresAt))
                {
                    // Schema 0 compatibility: the first prototype only carried createdAt.
                    if (!DateTimeOffset.TryParse(handoff.CreatedAt, out DateTimeOffset createdAt))
                    {
                        RejectHandoff("у перехода нет срока действия");
                        return;
                    }
                    expiresAt = createdAt.AddMinutes(30);
                }

                if (DateTimeOffset.UtcNow >= expiresAt.ToUniversalTime())
                {
                    RejectHandoff("срок подключения к бою истёк");
                    return;
                }
                if (MBSaveLoad.LoadSaveGameData(handoff.ReturnSave) == null)
                {
                    RejectHandoff("возвратное сохранение не найдено");
                    return;
                }

                _handoff = handoff;
                _battleId = battleId;
                _handoffLoaded = true;
                _invalidHandoffReported = false;
                _handoffLoadedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: handoff read failed: " + error.Message);
                RejectHandoff("не удалось прочитать переход");
            }
        }

        private static void FailAndReturn(string reason)
        {
            if (_returnStarted)
            {
                return;
            }

            Console.WriteLine("DikozemieOnline: ranked handoff recovery: " + reason);
            InformationManager.DisplayMessage(new InformationMessage(
                "Рейтинговый бой прерван: " + reason + ". Возвращаю героя в сохранённую кампанию."));
            ReturnToCampaign(stageResult: _missionObserved);
        }

        private static void RejectHandoff(string reason)
        {
            _battleId = Guid.Empty;
            Console.WriteLine("DikozemieOnline: ranked handoff rejected: " + reason);
            if (!_invalidHandoffReported)
            {
                _invalidHandoffReported = true;
                InformationManager.DisplayMessage(new InformationMessage(
                    "Рейтинговый бой не запущен: " + reason + ". Кампания сохранена."));
            }

            if (TryLoadReturnSave(out _))
            {
                DikozemieRankedTransitionController.Show(
                    "Бой недоступен. Возвращаем героя в кампанию…");
                ReturnToCampaign(stageResult: false);
                return;
            }

            EnterTerminalRecovery(reason);
        }

        private static void ReturnToCampaign(bool stageResult = true)
        {
            if (_returnStarted || _handoff == null)
            {
                return;
            }

            _returnStarted = true;
            try
            {
                LoadResult result = MBSaveLoad.LoadSaveGameData(_handoff.ReturnSave);
                if (result == null)
                {
                    throw new InvalidOperationException("return save could not be loaded");
                }

                // Durable before the process switches game managers. The campaign can
                // query /players/{id}/results/pending and apply the outcome exactly once.
                if (stageResult)
                {
                    DikozemieArenaResultInbox.Stage(_handoff, _directResult);
                }
                DikozemieRankedTransitionController.Show("Возвращаем героя в кампанию…");
                DikozemieGameRelaunch.StartCampaignAndExit(_handoff.ReturnSave);
                Console.WriteLine(
                    "DikozemieOnline: relaunching campaign save " + _handoff.ReturnSave);
            }
            catch (Exception error)
            {
                _returnStarted = false;
                Console.WriteLine("DikozemieOnline: campaign return failed: " + error);
                EnterTerminalRecovery("не удалось загрузить возвратное сохранение");
            }
        }

        private static bool TryLoadReturnSave(out LoadResult result)
        {
            result = null;
            if (_handoff == null || string.IsNullOrWhiteSpace(_handoff.ReturnSave))
            {
                return false;
            }

            try
            {
                result = MBSaveLoad.LoadSaveGameData(_handoff.ReturnSave);
                return result != null;
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: return save probe failed: " + error.Message);
                return false;
            }
        }

        private static void EnterTerminalRecovery(string reason)
        {
            if (_terminalRecovery)
            {
                return;
            }

            DikozemieArenaStateFiles.TryDelete(HandoffPath);
            _battleId = Guid.Empty;
            _terminalRecovery = true;
            _terminalExitStarted = false;
            Console.WriteLine("DikozemieOnline: terminal ranked recovery: " + reason);
            DikozemieRankedTransitionController.Show(
                "Бой не запущен: " + reason + ". Закрываем сетевую сессию; сохранение кампании не удалено.");
        }

        private static bool IsNativeLobbyVisible()
        {
            if (Game.Current?.GameStateManager?.ActiveState is LobbyState)
            {
                return true;
            }

            return IsLobbyScreen(ScreenManager.TopScreen);
        }

        private static bool IsLobbyScreen(ScreenBase screen)
        {
            return screen != null && screen.GetType().Name.IndexOf(
                "Lobby",
                StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsMissionScreen(ScreenBase screen)
        {
            return screen != null && screen.GetType().Name.IndexOf(
                "MissionScreen",
                StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void OnScreenPushed(ScreenBase screen)
        {
            if (IsMissionScreen(screen))
            {
                if (_terminalRecovery || _returnStarted)
                {
                    DikozemieRankedTransitionController.Show(
                        "Закрываем сетевую сессию и восстанавливаем кампанию…");
                }
                else
                {
                    DikozemieRankedTransitionController.Hide();
                }
                return;
            }

            if (IsLobbyScreen(screen))
            {
                if (_directLobbyMode)
                {
                    DikozemieRankedTransitionController.Hide();
                    return;
                }

                DikozemieRankedTransitionController.Show(
                    _returnStarted
                        ? "Возвращаем героя в кампанию…"
                        : "Ищем арену в сетевом лобби TaleWorlds…");
            }
        }

        private static void OnScreenPopped(ScreenBase screen)
        {
            if (IsMissionScreen(screen) && (_battleId != Guid.Empty || _returnStarted))
            {
                // OnPopScreen fires before the mission screen is removed. Cover
                // the resumed LobbyState synchronously, before it can render.
                DikozemieRankedTransitionController.Show("Возвращаем героя в кампанию…");
            }
        }

        private static void ResetForNextMatch()
        {
            _joinStarted = false;
            _joinCompleted = false;
            lock (JoinResultGate)
            {
                _joinGeneration++;
                _joinResultReady = false;
                _joinResultSucceeded = false;
                _joinResultFailure = null;
            }
            _missionObserved = false;
            _returnStarted = false;
            _terminalRecovery = false;
            _terminalExitStarted = false;
            _invalidHandoffReported = false;
            _handoffLoaded = false;
            _directLobbyMode = false;
            _battleId = Guid.Empty;
            _handoff = null;
            _directResult = null;
            _directResultReceivedAt = default;
            _handoffLoadedAt = default;
            _joinStartedAt = default;
            _joinCompletedAt = default;
            _missionEndedAt = default;
            _multiplayerEnteredAt = default;
        }
    }
}
