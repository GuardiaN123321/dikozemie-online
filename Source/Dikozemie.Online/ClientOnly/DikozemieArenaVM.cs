using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Diamond;
using TaleWorlds.ObjectSystem;

namespace Dikozemie.Online
{
    public sealed class DikozemieArenaVM : ViewModel
    {
        private const float RefreshInterval = 2.0f;
        private const int LeaderboardPageSize = 13;
        private readonly MissionMultiplayerDikozemieArenaClient _client;
        private readonly MissionMultiplayerGameModeBaseClient _gameMode;
        private readonly MissionScoreboardComponent _scoreboard;
        private float _refreshElapsed;
        private bool _subscribedToRepresentative;
        private bool _isEnabled;
        private bool _isDialogueVisible;
        private bool _dialogueWasShown;
        private bool _duelRequested;
        private DateTimeOffset _searchStartedAt;
        private string _remainingRoundTime = "00:00";
        private string _arenaStatus = "Осмотритесь и поговорите с распорядителем";
        private string _rulesText;
        private MBBindingList<DikozemieArenaPlayerVM> _players;
        private bool _isResultVisible;
        private bool _isOutcomeVisible;
        private bool _isLeaderboardVisible;
        private bool _hadActivePlayerAgent;
        private Agent _previousBattleAgent;
        private bool _wasPlayerReady;
        private bool _isSpectatorNoticeVisible;
        private bool _deathCinematicShown;
        private bool _isDeathCinematicVisible;
        private bool _isDeathChoiceVisible;
        private bool _isDeathReportVisible;
        private bool _isResultCinematicVisible;
        private bool _isResultExperienceVisible;
        private bool _isResultGloryVisible;
        private bool _isResultGoldVisible;
        private bool _isResultContinueVisible;
        private bool _areResultActionsVisible;
        private bool _canStartNextBattle;
        private float _deathCinematicElapsed;
        private int _deathCinematicPulseToken;
        private int _resultCinematicPulseToken;
        private int _deathExperienceEarned;
        private int _resultExperienceEarned;
        private int _resultGloryEarned;
        private int _resultGoldEarned;
        private int _resultCoinPenalty;
        private string _deathCinematicExperience = "+0 ОПЫТА";
        private string _resultExperienceReveal = "+0 ОПЫТА";
        private string _resultGloryReveal = "+0 СЛАВЫ";
        private string _resultGoldReveal = "+0 ЗОЛОТА";
        private int _spectatorPersonalKills;
        private int _spectatorRewardEarned;
        private string _spectatorLastVictim = string.Empty;
        private int _spectatorLastVictimRating;
        private int _spectatorLastReward;
        private int _spectatorLastMultiplier = 1;
        private readonly List<string> _personalKillReportLines =
            new List<string>();
        private MBBindingList<DikozemieArenaRewardRowVM> _deathRewardRows;
        private string _deathReportSummary =
            "ИТОГО ЗА УБИЙСТВА: +0 СЛАВЫ · +0 ЗОЛОТА\n" +
            "КОМАНДНЫЙ БОНУС: ОЖИДАЕТ ИСХОДА";
        private string _deathReportStatus =
            "Союзники продолжают бой. Итог обновится автоматически.";
        private string _spectatorStatus =
            "Наблюдайте за оставшимися бойцами до окончания сражения";
        private string _resultTitle = "БОЙ ЗАВЕРШЁН";
        private string _resultPlayerName = "Герой";
        private string _resultOpponentName = "Соперник";
        private string _resultWinnerName = string.Empty;
        private string _resultPlayerScore = "0";
        private string _resultOpponentScore = "0";
        private string _resultPlayerRating = string.Empty;
        private string _resultOpponentRating = string.Empty;
        private string _resultSummary = string.Empty;
        private string _resultReward = string.Empty;
        private string _resultKillReport = "Личных убийств: 0";
        private string _resultProgress = string.Empty;
        private MBBindingList<DikozemieArenaLeaderboardRowVM> _leaderboardRows;
        private int _leaderboardPage = -1;
        private bool _leaderboardCenteredOnPlayer = true;
        private int _leaderboardFocusToken;
        private string _leaderboardPageText = string.Empty;
        private bool _exitRequested;
        private bool _nativeExitReasserted;
        private float _exitRequestElapsed;
        private bool _resultSmokeMenuOpened;
        private bool _resultSmokeHandled;
        private float _resultSmokeMenuElapsed;

        // ---- One result screen, next battle by rotation (2026-11-04) ----
        // The customer found the six screens after a battle unclear and the
        // "next battle" button led nowhere. The result now opens at once with
        // everything on it, and the next battle of the rotation starts by
        // itself after a short countdown unless the player pauses it.
        private const float AutoNextDelay = 20f;
        private float _autoNextSeconds = -1f;
        private bool _autoNextPaused;
        private float _countdownTextElapsed;
        private string _nextBattleText = string.Empty;
        private string _nextBattleCountdown = string.Empty;
        private string _autoNextButtonText = "ПАУЗА";
        private string _resultModeText = string.Empty;
        private string _resultGoldText = "0";
        private string _resultRatingText = "0";
        private string _resultExperienceText = "0";
        private string _resultKillsText = "0";
        private string _resultOpponentLine = string.Empty;
        private string _resultSubtitle = string.Empty;
        private string _resultFooterHint = string.Empty;
        private bool _isResultFinal;
        private bool _hasKillRows;
        private string _spectatorHint = "F10 — ИТОГ БОЯ";

        // ---- Waiting for people (customer 2026-11-05) ----
        // «Игрокам написать, что ждём минуту … должна быть кнопка "начать с
        // ботом"». The server sends the countdown once a second, for a duel
        // as 1 of 2 players; the button asks it to stop waiting.
        private bool _isWaitingPanelVisible;
        private string _waitingTitle = string.Empty;
        private string _waitingText = string.Empty;
        private string _waitingButtonText = string.Empty;
        private bool _canStartWithBots;
        private bool _startNowSent;
        private bool _battleWasNaval;
        private int _battleTeamSize;
        private string _battleScene;
        private string _battleHullId;

        public DikozemieArenaVM(MissionMultiplayerDikozemieArenaClient client)
        {
            _battleWasNaval = DikozemieArenaMatchSelection.Venue ==
                DikozemieBattleVenue.Naval;
            _battleTeamSize = DikozemieArenaMatchSelection.TeamSize;
            _battleScene = MultiplayerOptions.OptionType.Map.GetStrValue();
            _battleHullId = DikozemieArenaMatchSelection.SelectedNavalBattle?.HullId;
            _client = client;
            if (_client != null)
            {
                _client.MyRepresentativeAssigned += OnRepresentativeAssigned;
                _client.MatchSeriesCompleted += OnMatchSeriesCompleted;
                _client.TrainingSeriesCompleted += OnTrainingSeriesCompleted;
                _client.KillProgressUpdated += OnKillProgressUpdated;
                _client.DeathSummaryReceived += OnDeathSummaryReceived;
                _client.SkillProgressUpdated += OnSkillProgressUpdated;
            }
            DikozemieArenaLeaderboardTransport.LeaderboardUpdated +=
                RefreshLeaderboard;
            _gameMode = Mission.Current.GetMissionBehavior<MissionMultiplayerGameModeBaseClient>();
            _scoreboard = Mission.Current.GetMissionBehavior<MissionScoreboardComponent>();
            Players = new MBBindingList<DikozemieArenaPlayerVM>();
            LeaderboardRows = new MBBindingList<DikozemieArenaLeaderboardRowVM>();
            DeathRewardRows = new MBBindingList<DikozemieArenaRewardRowVM>();
            UpdateBattleTexts();
            DikozemieArenaLeaderboardTransport.EnsureClientCatalogSeed();
            RefreshLeaderboard();
        }

        // The header line and the result's subtitle for the battle the
        // selection holds now: when the mission opens, and again when the
        // next battle starts on this same server (a ship's crew and the
        // deck's rules come from the hull, not from the arena switches).
        private void UpdateBattleTexts()
        {
            int selectedTeamSize = DikozemieArenaMatchSelection.TeamSize;
            bool naval = DikozemieArenaMatchSelection.Venue ==
                DikozemieBattleVenue.Naval;
            string arenaName = naval
                ? ("МОРСКОЙ БОЙ · " +
                    DikozemieArenaMatchSelection.SelectedNavalBattle.DisplayName)
                    .ToUpperInvariant()
                : DikozemieArenaSceneRules.FormatSceneName(
                    DikozemieArenaSceneRules.ResolveScene(
                        selectedTeamSize,
                        DikozemieArenaMatchSelection.Discipline))
                    .ToUpperInvariant();
            // A crew's size and the deck's rules come from the ship, not from
            // what the arena page had selected.
            int navalCrew =
                DikozemieArenaMatchSelection.SelectedNavalBattle.CrewCapacity;
            string battleFormat = naval
                ? navalCrew + "×" + navalCrew
                : DikozemieArenaMatchSelection.FormatTeamSize();
            string discipline = naval
                ? "АБОРДАЖ"
                : DikozemieArenaMatchSelection.FormatDiscipline()
                    .ToUpperInvariant();
            RulesText = arenaName + " · " + battleFormat + " · " +
                discipline;
            ResultModeText = naval
                ? arenaName + " · " + battleFormat
                : arenaName + " · " + battleFormat + " · " + discipline +
                    " · " + DikozemieArenaMatchSelection.FormatComposition()
                        .ToUpperInvariant();
            if (DikozemieWorldBattle.IsSiege)
            {
                // Stage 6: a siege's storm, not an arena bout.
                RulesText = DikozemieWorldBattle.SiegeLine;
                ResultModeText = DikozemieWorldBattle.SiegeLine;
            }
            if (DikozemieWatch.IsWatching)
            {
                // The observer of the watch looks on; he has no battle of his own.
                RulesText = "НАБЛЮДЕНИЕ · " + (DikozemieWatch.WatchTitle ?? string.Empty).ToUpperInvariant();
            }
        }

        public event Action<bool> DialogueVisibilityChanged;

        public override void OnFinalize()
        {
            if (_client != null)
            {
                _client.MyRepresentativeAssigned -= OnRepresentativeAssigned;
                _client.MatchSeriesCompleted -= OnMatchSeriesCompleted;
                _client.TrainingSeriesCompleted -= OnTrainingSeriesCompleted;
                _client.KillProgressUpdated -= OnKillProgressUpdated;
                _client.DeathSummaryReceived -= OnDeathSummaryReceived;
                _client.SkillProgressUpdated -= OnSkillProgressUpdated;
            }
            DikozemieArenaLeaderboardTransport.LeaderboardUpdated -=
                RefreshLeaderboard;
            if (_subscribedToRepresentative && _client?.MyRepresentative != null)
            {
                _client.MyRepresentative.ProgressUpdated -= OnProgressUpdated;
            }
            base.OnFinalize();
        }

        public void Tick(float dt)
        {
            TickExitRequest(dt);
            TickCinematicPresentation(dt);
            TickLiveResultSmoke(dt);
            if (_exitRequested)
            {
                return;
            }
            TickAutoNext(dt);

            DikozemieLocalArenaController local =
                Mission.Current?.GetMissionBehavior<DikozemieLocalArenaController>();
            if (!_duelRequested && !IsResultVisible &&
                local == null && GameNetwork.IsClient)
            {
                // The dedicated server queues clients automatically and starts
                // a rating-matched bot duel immediately. Suppress the local
                // arena-master dialogue while the server prepares both agents.
                _duelRequested = true;
                _searchStartedAt = DateTimeOffset.UtcNow;
                _dialogueWasShown = true;
                IsDialogueVisible = false;
                ArenaStatus = "Подбираем противника по вашему рейтингу…";
            }
            MissionPeer localPeer = GameNetwork.MyPeer?.GetComponent<MissionPeer>();
            // A hero who lived through the last battle on this server still
            // stands there until the server clears the field for the next one:
            // that figure is not "in the fight", and its removal is not a death
            // (the «ВЫ ВЫБЫЛИ» bar used to hang over the next gathering).
            bool playerReady =
                IsFightingAgent(local?.PlayerAgent) ||
                IsFightingAgent(localPeer?.ControlledAgent);
            if (playerReady)
            {
                _previousBattleAgent = null;
                _hadActivePlayerAgent = true;
                _wasPlayerReady = true;
                _deathCinematicShown = false;
                IsDeathCinematicVisible = false;
                IsSpectatorNoticeVisible = false;
            }
            else if (_hadActivePlayerAgent && !IsResultVisible)
            {
                if (_wasPlayerReady || !_deathCinematicShown)
                {
                    BeginDeathCinematic(_deathExperienceEarned);
                }
                _wasPlayerReady = false;
                if (!IsDeathCinematicVisible &&
                    !IsDeathChoiceVisible &&
                    IsSpectatorNoticeVisible)
                {
                    UpdateSpectatorNotice();
                }
            }
            if (!_dialogueWasShown && !_duelRequested &&
                !IsResultVisible && playerReady)
            {
                _dialogueWasShown = true;
                IsDialogueVisible = true;
            }

            if (_duelRequested)
            {
                UpdateSearchStatus();
            }
            UpdateWaitingPanel();

            if (_gameMode != null &&
                _gameMode.CheckTimer(out int remaining, out int warning, false))
            {
                // While players gather the only clock that matters is the one to the start.
                bool gathering = _client != null &&
                    (DateTimeOffset.UtcNow - _client.QueueStatusAt).TotalSeconds < 3;
                RemainingRoundTime = gathering ||
                        DikozemieArenaMatchSelection.Venue == DikozemieBattleVenue.Naval
                    ? string.Empty
                    : TimeSpan.FromSeconds(remaining).ToString("mm':'ss");
            }

            _refreshElapsed += dt;
            if (_refreshElapsed >= RefreshInterval)
            {
                RefreshPlayers();
                _refreshElapsed = 0.0f;
            }
        }

        // Shown while the server reports a wait (duel: 1 of 2; team or sea:
        // live players of the places), hidden as soon as the reports stop.
        private void UpdateWaitingPanel()
        {
            bool waiting = _client != null && GameNetwork.IsClient &&
                !IsResultVisible && !IsSpectatorNoticeVisible &&
                _client.QueueCapacity > 0 &&
                _client.QueuePlayers < _client.QueueCapacity &&
                (DateTimeOffset.UtcNow - _client.QueueStatusAt).TotalSeconds < 2.5;
            if (!waiting)
            {
                IsWaitingPanelVisible = false;
                _startNowSent = false;
                return;
            }

            bool naval = DikozemieArenaMatchSelection.Venue ==
                DikozemieBattleVenue.Naval;
            bool duel = !naval && _client.QueueCapacity == 2;
            // The server counts once a second; count down in between.
            int seconds = Math.Max(
                0,
                _client.QueueSecondsLeft - (int)Math.Floor(
                    (DateTimeOffset.UtcNow - _client.QueueStatusAt).TotalSeconds));
            string clock = TimeSpan.FromSeconds(seconds).ToString("m':'ss");
            if (duel)
            {
                WaitingTitle = "ИЩЕМ ЖИВОГО СОПЕРНИКА · " + clock;
                WaitingText = _startNowSent
                    ? "Выпускаем бота по вашему рейтингу…"
                    : "Ждём второго игрока до минуты. Если никто не придёт, " +
                      "против вас выйдет бот по вашему рейтингу.";
                WaitingButtonText = _startNowSent
                    ? "НАЧИНАЕМ…"
                    : "НАЧАТЬ С БОТОМ · F9";
            }
            else
            {
                WaitingTitle = (naval ? "СОБИРАЕМ ЭКИПАЖИ · " : "СОБИРАЕМ БОЙ · ") +
                    "ЖИВЫХ ИГРОКОВ " + _client.QueuePlayers + " ИЗ " +
                    _client.QueueCapacity + " · " + clock;
                WaitingText = _startNowSent
                    ? "Начинаем: свободные места займут боты…"
                    : naval
                        ? "Ждём живых игроков. Когда время выйдет, пустые места на палубах займут боты."
                        : "Ждём живых игроков. Когда время выйдет, свободные места займут боты.";
                WaitingButtonText = _startNowSent
                    ? "НАЧИНАЕМ…"
                    : naval ? "ОТПЛЫТЬ С БОТАМИ · F9" : "НАЧАТЬ С БОТАМИ · F9";
            }
            CanStartWithBots = !_startNowSent;
            IsWaitingPanelVisible = true;
        }

        public void ExecuteStartWithBots()
        {
            if (!IsWaitingPanelVisible || _startNowSent ||
                !GameNetwork.IsClient || GameNetwork.IsServer)
            {
                return;
            }
            _startNowSent = true;
            GameNetwork.BeginModuleEventAsClient();
            GameNetwork.WriteMessage(DikozemieArenaDuelRequestMessage.StartNow());
            GameNetwork.EndModuleEventAsClient();
            DikozemieLobbyShellIntegration.TraceButtonAction(
                "arena-start-now players=" + _client?.QueuePlayers + "/" +
                _client?.QueueCapacity + " secondsLeft=" + _client?.QueueSecondsLeft);
            UpdateWaitingPanel();
        }

        public void ExecuteAcceptDuel()
        {
            if (_duelRequested)
            {
                return;
            }

            bool accepted;
            DikozemieLocalArenaController local =
                Mission.Current?.GetMissionBehavior<DikozemieLocalArenaController>();
            if (local != null)
            {
                accepted = local.RequestDuel();
            }
            else if (GameNetwork.IsServer)
            {
                MissionMultiplayerDikozemieArena server =
                    Mission.Current.GetMissionBehavior<MissionMultiplayerDikozemieArena>();
                accepted = server?.RequestDuel(GameNetwork.MyPeer) == true;
            }
            else
            {
                GameNetwork.BeginModuleEventAsClient();
                GameNetwork.WriteMessage(new DikozemieArenaDuelRequestMessage(
                    DikozemieArenaMatchSelection.TeamSize,
                    DikozemieArenaMatchSelection.Discipline,
                    DikozemieArenaMatchSelection.Composition));
                GameNetwork.EndModuleEventAsClient();
                accepted = true;
            }

            if (!accepted)
            {
                ArenaStatus = "Распорядитель пока не готов принять заявку";
                return;
            }

            _duelRequested = true;
            _searchStartedAt = DateTimeOffset.UtcNow;
            IsDialogueVisible = false;
            ArenaStatus = "Ищем реального игрока… 5";
        }

        public void ExecuteNextBattle()
        {
            if (!CanStartNextBattle)
            {
                NextBattleCountdown = "Дождитесь итога боя";
                ArenaStatus = "Дождитесь окончательного итога боя";
                return;
            }
            _autoNextSeconds = -1f;

            // The rotation picks the next kind; the player's switches on this
            // screen are already in it.
            DikozemieBattlePlan plan = DikozemieBattleRotation.ApplyNext(
                out string rotationError);
            if (plan == null)
            {
                NextBattleCountdown = rotationError;
                ArenaStatus = rotationError;
                return;
            }
            DikozemieLobbyShellIntegration.TraceButtonAction(
                "arena-next rotation=" + plan.Describe());

            string currentScene =
                MultiplayerOptions.OptionType.Map.GetStrValue();
            string selectedScene = plan.IsNaval
                ? DikozemieArenaSceneRules.NavalBoardingScene
                : DikozemieArenaSceneRules.ResolveScene(
                    plan.TeamSize,
                    plan.Discipline);
            // Stay on this server only for the same kind of server: a duel
            // arena takes duels, a team arena team battles, and a ship class
            // is part of the profile sent when a player joins.
            bool sameScene = string.Equals(
                selectedScene,
                currentScene,
                StringComparison.OrdinalIgnoreCase);
            bool sameServerKind = plan.IsNaval
                ? _battleWasNaval && string.Equals(
                    DikozemieArenaMatchSelection.SelectedNavalBattle?.HullId,
                    _battleHullId,
                    StringComparison.Ordinal)
                : !_battleWasNaval &&
                    (plan.TeamSize == 1) == (_battleTeamSize == 1);
            if (!sameScene || !sameServerKind)
            {
                ArenaStatus = "Переходим на следующий бой: " + plan.Describe();
                NextBattleCountdown = "Переходим: " + plan.Describe();
                DikozemieLobbyShellIntegration.PrepareForNextArenaSearch();
                BeginArenaExit("arena-next-map requested");
                return;
            }

            ResetPostFightPresentation();
            MissionPeer nextPeer = GameNetwork.MyPeer?.GetComponent<MissionPeer>();
            _previousBattleAgent =
                Mission.Current?.GetMissionBehavior<DikozemieLocalArenaController>()?.PlayerAgent ??
                nextPeer?.ControlledAgent;
            ResultProgress = string.Empty;
            _duelRequested = true;
            _searchStartedAt = DateTimeOffset.UtcNow;
            _battleTeamSize = plan.TeamSize;
            UpdateBattleTexts();
            ArenaStatus = "Следующий бой: " + plan.Describe();
            GameNetwork.BeginModuleEventAsClient();
            GameNetwork.WriteMessage(new DikozemieArenaDuelRequestMessage(
                    DikozemieArenaMatchSelection.TeamSize,
                    DikozemieArenaMatchSelection.Discipline,
                    DikozemieArenaMatchSelection.Composition));
            GameNetwork.EndModuleEventAsClient();
        }

        public void ExecuteToggleAutoNext()
        {
            _autoNextPaused = !_autoNextPaused;
            AutoNextButtonText = _autoNextPaused ? "АВТОЗАПУСК" : "ПАУЗА";
            if (!_autoNextPaused && CanStartNextBattle && _autoNextSeconds < 0f)
            {
                _autoNextSeconds = AutoNextDelay;
            }
            UpdateNextBattleTexts();
        }

        private void TickAutoNext(float dt)
        {
            if (!IsResultVisible)
            {
                return;
            }
            if (CanStartNextBattle && !_autoNextPaused && _autoNextSeconds > 0f)
            {
                _autoNextSeconds -= Math.Max(0f, dt);
                if (_autoNextSeconds <= 0f)
                {
                    _autoNextSeconds = -1f;
                    DikozemieLobbyShellIntegration.TraceButtonAction(
                        "arena-next started by countdown");
                    ExecuteNextBattle();
                    return;
                }
            }
            _countdownTextElapsed += Math.Max(0f, dt);
            if (_countdownTextElapsed >= 0.25f)
            {
                _countdownTextElapsed = 0f;
                UpdateNextBattleTexts();
            }
        }

        private bool IsFightingAgent(Agent agent)
        {
            return agent != null &&
                agent != _previousBattleAgent &&
                agent.State == AgentState.Active;
        }

        private void UpdateNextBattleTexts()
        {
            DikozemieBattlePlan next = DikozemieBattleRotation.PreviewNext();
            NextBattleText = next == null
                ? "Нет подходящего боя: включите другие виды"
                : "Следующий бой: " + next.Describe();
            if (!CanStartNextBattle)
            {
                NextBattleCountdown = "Бой ещё идёт: итог появится здесь";
            }
            else if (_autoNextPaused)
            {
                NextBattleCountdown = "Автозапуск на паузе";
            }
            else if (_autoNextSeconds > 0f)
            {
                NextBattleCountdown = "Начнётся сам через " +
                    (int)Math.Ceiling(_autoNextSeconds) + " с";
            }
        }

        public void ExecuteExitArena()
        {
            BeginArenaExit("arena-exit requested");
        }

        public void ExecuteFinishBattle()
        {
            OpenIntermissionMenu();
        }

        // F10 while watching: the same result screen, waiting for the team's
        // outcome. Rewards so far are on it, the next battle can be set up.
        private void OpenIntermissionMenu()
        {
            IsSpectatorNoticeVisible = false;
            IsDeathChoiceVisible = false;
            IsDeathCinematicVisible = false;
            IsDeathReportVisible = false;
            UpdateDeathRewardSummary(false);
            ResultTitle = "ВЫ ВЫБЫЛИ";
            ResultWinnerName = "КОМАНДНЫЙ ИТОГ ЕЩЁ ПОДСЧИТЫВАЕТСЯ";
            ResultSummary = "Можно настроить следующий бой уже сейчас";
            ResultReward = DeathReportSummary;
            ResultKillReport = BuildKillReportText();
            _isResultFinal = false;
            ResultGoldText = FormatSignedCoinChange(_spectatorRewardEarned);
            ResultRatingText = "+" + _spectatorRewardEarned;
            ResultExperienceText = "+" + _deathExperienceEarned;
            ResultKillsText = _spectatorPersonalKills.ToString();
            ResultFooterHint = "Итог команды придёт сам, когда бой закончится";
            ResultSubtitle = ResultModeText;
            HasKillRows = DeathRewardRows.Count > 0;
            IsResultCinematicVisible = false;
            IsResultContinueVisible = false;
            IsOutcomeVisible = true;
            IsLeaderboardVisible = true;
            AreResultActionsVisible = true;
            CanStartNextBattle = false;
            IsResultVisible = true;
            InformationManager.ClearAllMessages();
            RefreshLeaderboard();
            LeaderboardFocusToken++;
            UpdateNextBattleTexts();
            ArenaStatus = "Настройте следующий бой; запуск откроется после итога";
        }

        private void BeginArenaExit(string traceAction)
        {
            if (_exitRequested)
            {
                return;
            }

            _exitRequested = true;
            _nativeExitReasserted = false;
            _exitRequestElapsed = 0f;
            ResetPostFightPresentation();
            ArenaStatus = traceAction == "arena-next-map requested"
                ? "Загружаем выбранную арену…"
                : "Выходим из арены…";
            DikozemieLobbyShellIntegration.PrepareForTrainingExit();
            DikozemieLobbyShellIntegration.TraceButtonAction(
                traceAction);

            try
            {
                Mission mission = Mission.Current;
                MissionLobbyComponent missionLobby =
                    mission?.GetMissionBehavior<MissionLobbyComponent>();
                if (missionLobby != null)
                {
                    // This is the same entry point used by Bannerlord's native
                    // multiplayer escape menu. It updates the custom-game
                    // lobby state before mission teardown begins.
                    missionLobby.QuitMission();
                }
                else
                {
                    NetworkMain.GameClient?.QuitFromCustomGame();
                }

                // QuitFromCustomGame normally reaches this through the lobby
                // session handler. Call it explicitly as well: the custom
                // arena can briefly report LobbyState while its Mission still
                // owns the foreground screen, causing the handler to skip it.
                BannerlordNetwork.EndMultiplayerLobbyMission();
            }
            catch (Exception error)
            {
                Console.WriteLine(
                    "DikozemieOnline: arena exit request failed: " + error.Message);
                DikozemieLobbyShellIntegration.TraceButtonAction(
                    "arena-exit native request failed: " + error.Message);
            }
        }

        private void TickLiveResultSmoke(float dt)
        {
            if (_resultSmokeHandled)
            {
                return;
            }

            string requested = Environment.GetEnvironmentVariable(
                "DIKOZEMIE_ARENA_RESULT_SMOKE_NEXT");
            if (!string.Equals(requested, "1", StringComparison.Ordinal))
            {
                _resultSmokeHandled = true;
                return;
            }

            if (!_resultSmokeMenuOpened)
            {
                if (!IsResultVisible || !CanStartNextBattle)
                {
                    return;
                }

                _resultSmokeMenuOpened = true;
                _resultSmokeMenuElapsed = 0f;
                DikozemieLobbyShellIntegration.TraceButtonAction(
                    "arena-result-smoke menu opened reward=" + ResultReward +
                    " rating=" + ResultPlayerRating +
                    " progress=" + ResultProgress);
                return;
            }

            _resultSmokeMenuElapsed += Math.Max(0f, dt);
            if (_resultSmokeMenuElapsed < 1f || !CanStartNextBattle)
            {
                return;
            }

            _resultSmokeHandled = true;
            DikozemieLobbyShellIntegration.TraceButtonAction(
                "arena-result-smoke next battle requested");
            ExecuteNextBattle();
        }

        private void TickExitRequest(float dt)
        {
            if (!_exitRequested)
            {
                return;
            }

            _exitRequestElapsed += Math.Max(0f, dt);
            Mission mission = Mission.Current;
            if (mission == null || mission.MissionEnded)
            {
                return;
            }

            if (!_nativeExitReasserted && _exitRequestElapsed >= 0.75f)
            {
                _nativeExitReasserted = true;
                DikozemieLobbyShellIntegration.TraceButtonAction(
                    "arena-exit reassert native teardown");
                BannerlordNetwork.EndMultiplayerLobbyMission();
            }

            if (_exitRequestElapsed < 3f || mission.IsMissionEnding)
            {
                return;
            }

            // Last-resort local teardown. OnRemoveBehavior invokes the native
            // MissionLobbyComponent quit path again, so the network session is
            // still cleaned up even if the lobby callback was lost.
            DikozemieLobbyShellIntegration.TraceButtonAction(
                "arena-exit fallback mission end");
            mission.EndMission();
        }

        public void ExecuteLeaderboardMine()
        {
            _leaderboardPage = -1;
            _leaderboardCenteredOnPlayer = true;
            RefreshLeaderboard();
            LeaderboardFocusToken++;
        }

        public void ExecuteLeaderboardPrevious()
        {
            if (_leaderboardPage <= 0)
            {
                return;
            }
            _leaderboardCenteredOnPlayer = false;
            _leaderboardPage--;
            RefreshLeaderboard();
        }

        public void ExecuteLeaderboardNext()
        {
            int entryCount =
                DikozemieArenaLeaderboardTransport.LatestEntries.Count;
            int pageCount = Math.Max(
                1,
                (entryCount + LeaderboardPageSize - 1) /
                    LeaderboardPageSize);
            if (_leaderboardPage >= pageCount - 1)
            {
                return;
            }
            _leaderboardCenteredOnPlayer = false;
            _leaderboardPage++;
            RefreshLeaderboard();
        }

        public void ExecuteShowLeaderboard()
        {
            _leaderboardPage = -1;
            _leaderboardCenteredOnPlayer = true;
            RefreshLeaderboard();
            IsOutcomeVisible = false;
            IsLeaderboardVisible = true;
            LeaderboardFocusToken++;
        }

        private void OnTrainingSeriesCompleted(
            DikozemieArenaTrainingResultMessage message)
        {
            ResultPlayerName = ResolvePlayerName();
            // A team battle against live players only names no bot.
            BasicCharacterObject opponent =
                string.IsNullOrEmpty(message.OpponentCharacterId)
                    ? null
                    : MBObjectManager.Instance
                        .GetObject<BasicCharacterObject>(message.OpponentCharacterId);
            ResultOpponentName = string.IsNullOrEmpty(message.OpponentCharacterId)
                ? "Команда соперника"
                : DikozemieArenaBotNames.Format(
                    message.OpponentCharacterId,
                    opponent?.Name?.ToString());
            ResultWinnerName = message.IsDraw ? "НИЧЬЯ — ОБА БОЙЦА ПОГИБЛИ" : "ПОБЕДИТЕЛЬ: " +
                (message.Victory ? ResultPlayerName : ResultOpponentName);
            ResultPlayerScore = message.PlayerScore.ToString();
            ResultOpponentScore = message.BotScore.ToString();
            ResultPlayerRating = "Рейтинг " + message.PlayerRating +
                "  ·  Победы " + message.PlayerWins;
            ResultOpponentRating = "Рейтинг " + message.OpponentRating +
                "  ·  Победы " + message.OpponentWins;
            ResultTitle = DikozemieWorldBattle.IsSiege
                ? DikozemieWorldBattle.SiegeTitle(message.Victory, message.IsDraw)
                : message.IsDraw ? "НИЧЬЯ" : message.Victory
                ? DikozemieArenaSceneRules.IsNavalBoardingScene(
                    MultiplayerOptions.OptionType.Map.GetStrValue())
                    ? "ПОБЕДА НА МОРЕ"
                    : "ПОБЕДА НА АРЕНЕ"
                : "ПОРАЖЕНИЕ";
            ResultSummary = "Бои: " + message.SeriesBattles +
                "   Победы: " + message.SeriesWins +
                "   Поражения: " + message.SeriesLosses +
                "   Рейтинг: " + message.PlayerRating;
            _resultGoldEarned = _client?.LastCoinChange ??
                Math.Max(0, message.CoinsEarned) -
                DikozemieArenaMatchSelection.CalculateLossCoinPenalty(
                    message.SeriesLosses);
            _resultCoinPenalty = Math.Max(
                0,
                Math.Max(0, message.CoinsEarned) - _resultGoldEarned);
            _resultGloryEarned = Math.Max(0, message.RatingEarned);
            ResultReward = FormatSignedCoinChange(_resultGoldEarned) +
                " золота   +" +
                _resultGloryEarned + " славы";
            ResultKillReport = BuildFinalRewardReportText(
                _resultGoldEarned);
            DeathReportSummary = ResultKillReport;
            DeathReportStatus = ResultTitle;
            // One bot against the hero: name him. A team battle has no
            // single opponent, the subtitle shows the format instead.
            ResultOpponentLine =
                !string.IsNullOrEmpty(message.OpponentCharacterId) &&
                DikozemieArenaMatchSelection.TeamSize <= 1 &&
                DikozemieArenaMatchSelection.Venue != DikozemieBattleVenue.Naval
                    ? "СОПЕРНИК: " + ResultOpponentName +
                        " · РЕЙТИНГ " + message.OpponentRating
                    : string.Empty;
            PreparePostFightResult();
        }

        private void OnDeathSummaryReceived(int heroExperience)
        {
            _deathExperienceEarned = Math.Max(0, heroExperience);
            BeginDeathCinematic(heroExperience);
        }

        private void OnSkillProgressUpdated(string progress, int heroExperience)
        {
            _resultExperienceEarned = Math.Max(0, heroExperience);
            ResultProgress = progress ?? string.Empty;
            ResultExperienceReveal = "+" + _resultExperienceEarned +
                " ОПЫТА";
        }

        private void OnKillProgressUpdated(
            DikozemieArenaKillProgressMessage message)
        {
            if (message.PersonalKills > _spectatorPersonalKills)
            {
                _personalKillReportLines.Add(
                    "#" + message.PersonalKills + " " + message.VictimName +
                    " · рейтинг " + message.VictimRating +
                    (message.RewardMultiplier > 1
                        ? " · ×" + message.RewardMultiplier
                        : string.Empty) +
                    " · +" + message.LastReward + " славы" +
                    " · +" + message.LastReward + " золота");
                DeathRewardRows.Add(new DikozemieArenaRewardRowVM(
                    message.PersonalKills,
                    message.VictimName,
                    message.VictimRating,
                    message.RewardMultiplier,
                    message.LastReward));
            }
            _spectatorPersonalKills = message.PersonalKills;
            _spectatorRewardEarned = message.RewardEarned;
            _spectatorLastVictim = message.VictimName;
            _spectatorLastVictimRating = message.VictimRating;
            _spectatorLastReward = message.LastReward;
            _spectatorLastMultiplier = message.RewardMultiplier;
            UpdateDeathRewardSummary(false);
            if (IsSpectatorNoticeVisible)
            {
                UpdateSpectatorNotice();
            }
        }

        private void OnMatchSeriesCompleted(
            DikozemieArenaMatchResultMessage message)
        {
            bool winner = message.Winner == GameNetwork.MyPeer;
            int battles = winner
                ? message.WinnerSeriesBattles
                : message.LoserSeriesBattles;
            int wins = winner
                ? message.WinnerSeriesWins
                : message.LoserSeriesWins;
            int losses = winner
                ? message.WinnerSeriesLosses
                : message.LoserSeriesLosses;
            int coins = winner
                ? message.WinnerSeriesCoins
                : message.LoserSeriesCoins;
            int coinPenalty = DikozemieArenaMatchSelection.CalculateLossCoinPenalty(
                losses);
            int rating = winner ? message.WinnerRating : message.LoserRating;
            int ratingEarned = winner ? message.Delta : 0;
            NetworkCommunicator opponent = winner ? message.Loser : message.Winner;
            ResultPlayerName = ResolvePlayerName();
            ResultOpponentName = opponent?.UserName ?? "Соперник";
            ResultWinnerName = message.IsDraw ? "НИЧЬЯ — ОБА БОЙЦА ПОГИБЛИ" : "ПОБЕДИТЕЛЬ: " +
                (winner ? ResultPlayerName : ResultOpponentName);
            ResultPlayerScore = message.IsDraw ? "0" : winner ? "1" : "0";
            ResultOpponentScore = message.IsDraw ? "0" : winner ? "0" : "1";
            ResultPlayerRating = "Рейтинг " + rating + "  ·  Победы " + wins;
            int opponentRating = winner ? message.LoserRating : message.WinnerRating;
            int opponentWins = winner ? message.LoserWins : message.WinnerWins;
            ResultOpponentRating = "Рейтинг " + opponentRating +
                "  ·  Победы " + opponentWins;
            ResultTitle = message.IsDraw ? "НИЧЬЯ" : winner ? "ПОБЕДА НА АРЕНЕ" : "ПОРАЖЕНИЕ";
            ResultSummary = "Бои: " + battles + "   Победы: " + wins +
                "   Поражения: " + losses + "   Рейтинг: " + rating;
            _resultGoldEarned = _client?.LastCoinChange ??
                Math.Max(0, coins) - Math.Max(0, coinPenalty);
            _resultCoinPenalty = Math.Max(
                0,
                Math.Max(0, coins) - _resultGoldEarned);
            _resultGloryEarned = Math.Max(0, ratingEarned);
            ResultReward = FormatSignedCoinChange(_resultGoldEarned) +
                " золота   +" +
                _resultGloryEarned + " славы";
            ResultKillReport = BuildFinalRewardReportText(
                _resultGoldEarned);
            DeathReportSummary = ResultKillReport;
            DeathReportStatus = ResultTitle;
            ResultOpponentLine = "СОПЕРНИК: " + ResultOpponentName +
                " · РЕЙТИНГ " + opponentRating;
            PreparePostFightResult();
        }

        // The result opens at once: no reveal step, no «open summary» click.
        private void PreparePostFightResult()
        {
            _duelRequested = false;
            IsEnabled = true;
            ArenaStatus = "Бой завершён";
            IsDeathCinematicVisible = false;
            IsDeathChoiceVisible = false;
            IsDeathReportVisible = false;
            IsSpectatorNoticeVisible = false;
            IsResultCinematicVisible = false;
            IsResultContinueVisible = false;
            _isResultFinal = true;
            ResultGoldText = FormatSignedCoinChange(_resultGoldEarned);
            int playerRating = DikozemieArmoryProgression.PlayerRating;
            ResultRatingText = "+" + _resultGloryEarned +
                (playerRating > 0 ? "  →  " + playerRating : string.Empty);
            ResultExperienceText = _resultExperienceEarned > 0
                ? "+" + _resultExperienceEarned
                : "+" + _deathExperienceEarned;
            ResultKillsText = _spectatorPersonalKills.ToString();
            HasKillRows = DeathRewardRows.Count > 0;
            ResultSubtitle = string.IsNullOrEmpty(ResultOpponentLine)
                ? ResultModeText
                : ResultModeText + "   ·   " + ResultOpponentLine;
            ResultFooterHint = _resultCoinPenalty > 0
                ? "Штраф за поражение уже учтён в золоте"
                : string.Empty;
            IsOutcomeVisible = true;
            IsLeaderboardVisible = true;
            AreResultActionsVisible = true;
            CanStartNextBattle = true;
            IsResultVisible = true;
            // The engine's damage lines («Получено 50 ед. урона…») sit above
            // this layer in the bottom-left corner; the result starts clean.
            InformationManager.ClearAllMessages();
            if (DikozemieLobbyShellIntegration.IsRoadBattlePending)
            {
                // A fight of the map (a captive's fight for freedom, a road
                // fight at sea) leads back to the map, not into the rotation.
                _autoNextPaused = true;
                AutoNextButtonText = "АВТОЗАПУСК";
                _autoNextSeconds = -1f;
            }
            else if (_autoNextSeconds < 0f && !_autoNextPaused)
            {
                _autoNextSeconds = AutoNextDelay;
            }
            // The rows are already current: ApplyTrainingResult and the
            // leaderboard transport refresh them before the result arrives.
            UpdateNextBattleTexts();
            LeaderboardFocusToken++;
            ResultCinematicPulseToken++;
        }

        private void BeginDeathCinematic(int heroExperience)
        {
            if (IsResultVisible)
            {
                return;
            }

            _deathExperienceEarned = Math.Max(
                _deathExperienceEarned,
                Math.Max(0, heroExperience));
            _deathCinematicShown = true;
            _deathCinematicElapsed = 0f;
            DeathCinematicExperience = "+" + _deathExperienceEarned +
                " ОПЫТА ЗАРАБОТАНО";
            DeathCinematicPulseToken++;
            IsDeathChoiceVisible = false;
            IsDeathReportVisible = false;
            IsSpectatorNoticeVisible = false;
            IsDeathCinematicVisible = true;
            ArenaStatus = "Вы пали. Бой продолжается";
        }

        private void TickCinematicPresentation(float dt)
        {
            float safeDt = Math.Max(0f, dt);
            if (IsDeathCinematicVisible)
            {
                _deathCinematicElapsed += safeDt;
                if (_deathCinematicElapsed >= 1.65f)
                {
                    IsDeathCinematicVisible = false;
                    if (!IsResultVisible)
                    {
                        // Straight to watching: a small bar, no modal choice.
                        UpdateDeathRewardSummary(false);
                        UpdateSpectatorNotice();
                    }
                }
            }
        }

        private void ResetPostFightPresentation()
        {
            _leaderboardPage = -1;
            _hadActivePlayerAgent = false;
            _wasPlayerReady = false;
            _deathCinematicShown = false;
            _deathCinematicElapsed = 0f;
            _deathExperienceEarned = 0;
            _resultExperienceEarned = 0;
            _resultGloryEarned = 0;
            _resultGoldEarned = 0;
            _resultCoinPenalty = 0;
            ResultOpponentLine = string.Empty;
            _spectatorPersonalKills = 0;
            _spectatorRewardEarned = 0;
            _spectatorLastVictim = string.Empty;
            _spectatorLastVictimRating = 0;
            _spectatorLastReward = 0;
            _spectatorLastMultiplier = 1;
            _personalKillReportLines.Clear();
            DeathRewardRows.Clear();
            DeathReportSummary =
                "ИТОГО ЗА УБИЙСТВА: +0 СЛАВЫ · +0 ЗОЛОТА\n" +
                "КОМАНДНЫЙ БОНУС: ОЖИДАЕТ ИСХОДА";
            DeathReportStatus =
                "Союзники продолжают бой. Итог обновится автоматически.";
            ResultKillReport = "Личных убийств: 0";
            IsSpectatorNoticeVisible = false;
            IsDeathCinematicVisible = false;
            IsDeathChoiceVisible = false;
            IsDeathReportVisible = false;
            IsResultCinematicVisible = false;
            IsResultExperienceVisible = false;
            IsResultGloryVisible = false;
            IsResultGoldVisible = false;
            IsResultContinueVisible = false;
            AreResultActionsVisible = false;
            CanStartNextBattle = false;
            IsOutcomeVisible = false;
            IsLeaderboardVisible = false;
            IsResultVisible = false;
            _autoNextSeconds = -1f;
            _isResultFinal = false;
            HasKillRows = false;
            ResultGoldText = "0";
            ResultRatingText = "0";
            ResultExperienceText = "0";
            ResultKillsText = "0";
            ResultFooterHint = string.Empty;
            NextBattleCountdown = string.Empty;
        }

        private void UpdateSpectatorNotice()
        {
            Mission mission = Mission.Current;
            MissionPeer localPeer = GameNetwork.MyPeer?.GetComponent<MissionPeer>();
            Team friendly = localPeer?.Team ?? mission?.AttackerTeam;
            Team enemy = friendly == mission?.DefenderTeam
                ? mission?.AttackerTeam
                : mission?.DefenderTeam;
            int alliesAlive = friendly?.ActiveAgents.Count(agent =>
                agent != null && agent.State == AgentState.Active) ?? 0;
            int enemiesAlive = enemy?.ActiveAgents.Count(agent =>
                agent != null && agent.State == AgentState.Active) ?? 0;

            // One line in the bar at the bottom of the screen.
            string personalResult =
                "Лично: убито " + _spectatorPersonalKills +
                " · +" + _spectatorRewardEarned + " золота";
            SpectatorStatus = alliesAlive == 0 || enemiesAlive == 0
                ? personalResult + "   ·   подводим итоги боя"
                : personalResult + "   ·   союзников " + alliesAlive +
                    " · противников " + enemiesAlive;
            ArenaStatus = "Вы выбыли. Бой продолжается";
            IsSpectatorNoticeVisible = true;
        }

        private string BuildKillReportText()
        {
            var lines = new List<string>
            {
                "Личных убийств: " + _spectatorPersonalKills +
                " · за убийства: +" + _spectatorRewardEarned +
                " славы / +" + _spectatorRewardEarned + " золота"
            };
            int first = Math.Max(0, _personalKillReportLines.Count - 3);
            if (first > 0)
            {
                lines.Add("… ещё убийств: " + first);
            }
            lines.AddRange(_personalKillReportLines.Skip(first));
            return string.Join("\n", lines);
        }

        private void UpdateDeathRewardSummary(bool final)
        {
            DeathReportSummary = final
                ? BuildFinalRewardReportText(_resultGoldEarned)
                : "ИТОГО ЗА УБИЙСТВА: +" + _spectatorRewardEarned +
                    " СЛАВЫ · +" + _spectatorRewardEarned + " ЗОЛОТА\n" +
                    "КОМАНДНЫЙ БОНУС: ОЖИДАЕТ ИСХОДА";
        }

        private string BuildFinalRewardReportText(int totalGold)
        {
            int grossGold = Math.Max(0, totalGold + _resultCoinPenalty);
            int teamBonus = Math.Max(
                0,
                grossGold - _spectatorRewardEarned);
            return "ИТОГО ЗА УБИЙСТВА: +" + _spectatorRewardEarned +
                " СЛАВЫ · +" + _spectatorRewardEarned + " ЗОЛОТА\n" +
                "КОМАНДНЫЙ БОНУС: +" + teamBonus + " ЗОЛОТА\n" +
                (_resultCoinPenalty > 0
                    ? "ШТРАФ ЗА ПОРАЖЕНИЕ: -" + _resultCoinPenalty +
                      " ЗОЛОТА\n"
                    : string.Empty) +
                "ВСЕГО: +" + _resultGloryEarned + " СЛАВЫ · " +
                FormatSignedCoinChange(totalGold) + " ЗОЛОТА";
        }

        private static string FormatSignedCoinChange(int coinChange)
        {
            if (coinChange > 0)
            {
                return "+" + coinChange;
            }
            if (coinChange < 0)
            {
                return "-" + Math.Abs(coinChange);
            }
            return "0";
        }

        private static string ResolvePlayerName()
        {
            string name = DikozemieHeroProfile.Snapshot?.Name;
            return string.IsNullOrWhiteSpace(name)
                ? GameNetwork.MyPeer?.UserName ?? "Герой"
                : name;
        }

        private void RefreshLeaderboard()
        {
            if (LeaderboardRows == null)
            {
                return;
            }
            IReadOnlyList<DikozemieArenaLeaderboardEntry> entries =
                DikozemieArenaLeaderboardTransport.LatestEntries;
            string heroId = DikozemieHeroProfile.Snapshot?.HeroId;
            int playerPlace = 0;
            int playerIndex = -1;
            int playerRating = DikozemieArmoryProgression.PlayerRating;
            for (int index = 0; index < entries.Count; index++)
            {
                DikozemieArenaLeaderboardEntry entry = entries[index];
                if (!string.IsNullOrWhiteSpace(heroId) &&
                    string.Equals(
                        entry?.CharacterId,
                        heroId,
                        StringComparison.Ordinal))
                {
                    playerIndex = index;
                    playerPlace = entry?.Rank ?? 0;
                    playerRating = entry?.Rating ?? playerRating;
                    break;
                }
            }

            int pageCount = Math.Max(
                1,
                (entries.Count + LeaderboardPageSize - 1) /
                    LeaderboardPageSize);
            if (_leaderboardPage < 0)
            {
                _leaderboardPage = playerIndex >= 0
                    ? playerIndex / LeaderboardPageSize
                    : 0;
                _leaderboardCenteredOnPlayer = playerIndex >= 0;
            }
            _leaderboardPage = Math.Max(
                0,
                Math.Min(pageCount - 1, _leaderboardPage));

            // Only materialize the visible page. Assigning the full 1,600+
            // fighter table makes Gauntlet synchronously create every row and
            // blocks the network result handler for several seconds.
            var rows = new MBBindingList<DikozemieArenaLeaderboardRowVM>();
            int firstEntry = _leaderboardCenteredOnPlayer && playerIndex >= 0
                ? Math.Max(
                    0,
                    Math.Min(
                        entries.Count - LeaderboardPageSize,
                        playerIndex - LeaderboardPageSize / 2))
                : _leaderboardPage * LeaderboardPageSize;
            int lastEntry = Math.Min(
                entries.Count,
                firstEntry + LeaderboardPageSize);
            for (int index = firstEntry; index < lastEntry; index++)
            {
                DikozemieArenaLeaderboardEntry entry = entries[index];
                bool isPlayer = !string.IsNullOrWhiteSpace(heroId) &&
                    string.Equals(entry?.CharacterId, heroId, StringComparison.Ordinal);
                rows.Add(
                    new DikozemieArenaLeaderboardRowVM(entry, isPlayer));
            }
            LeaderboardRows = rows;
            LeaderboardPageText = "МЕСТА " +
                (entries.Count > 0 ? firstEntry + 1 : 0) + "–" +
                lastEntry + " ИЗ " + entries.Count +
                "   •   ВАШЕ МЕСТО: " +
                (playerPlace > 0 ? playerPlace.ToString() : "—") +
                "   •   РЕЙТИНГ: " + playerRating +
                "   •   БОЙЦОВ В ТАБЛИЦЕ: " + entries.Count;
        }

        private void UpdateSearchStatus()
        {
            if (DikozemieWatch.IsWatching)
            {
                ArenaStatus = "Вас не видят · камера свободная · F5 — меры к бойцу";
                return;
            }
            if (_client != null &&
                (DateTimeOffset.UtcNow - _client.QueueStatusAt).TotalSeconds < 3)
            {
                if (DikozemieArenaMatchSelection.Venue != DikozemieBattleVenue.Naval &&
                    _client.QueueCapacity == 2)
                {
                    ArenaStatus = "Ищем живого соперника: ещё " +
                        _client.QueueSecondsLeft + " с, потом бот";
                    return;
                }
                ArenaStatus = DikozemieArenaMatchSelection.Venue ==
                        DikozemieBattleVenue.Naval
                    ? "Живых игроков " + _client.QueuePlayers + " из " +
                      _client.QueueCapacity + ". Отплываем через " +
                      _client.QueueSecondsLeft + " с, остальных заменят боты"
                    : "Игроков " + _client.QueuePlayers + " из " +
                      _client.QueueCapacity + ". Старт через " +
                      _client.QueueSecondsLeft + " с, остальных заменят боты";
                return;
            }

            bool humanOpponent = Mission.Current?.DefenderTeam?.ActiveAgents.Any(
                agent => agent?.MissionPeer != null &&
                    agent.State == AgentState.Active) == true;
            if (humanOpponent)
            {
                ArenaStatus = "Игрок найден. Поединок начинается";
                return;
            }

            bool botOpponent = Mission.Current?.DefenderTeam?.ActiveAgents.Any(
                agent => agent != null && agent.MissionPeer == null &&
                    agent.State == AgentState.Active) == true;
            if (botOpponent)
            {
                ArenaStatus = DikozemieArenaMatchSelection.Venue ==
                        DikozemieBattleVenue.Naval
                    ? DikozemieNavalBoardingBehavior.Active?.CrewsMayFight == true
                        ? "Абордаж! Бейтесь за корабль"
                        : "Корабли сходятся. Готовьтесь к абордажу"
                    : DikozemieArenaMatchSelection.TeamSize > 1
                        ? "Бой начался"
                        : "Игрок не найден. Против вас выходит бот";
                return;
            }

            if (GameNetwork.IsClient &&
                Mission.Current?.GetMissionBehavior<DikozemieLocalArenaController>() == null)
            {
                int elapsed = Math.Max(
                    0,
                    (int)Math.Floor(
                        (DateTimeOffset.UtcNow - _searchStartedAt).TotalSeconds));
                // Stage 5: a fight of the road waits for that very hero.
                string waitFor = DikozemieWorldBattle.WaitingText;
                ArenaStatus = (string.IsNullOrEmpty(waitFor) ? "Подбираем рейтингового противника… " : waitFor + " ") +
                    TimeSpan.FromSeconds(elapsed).ToString("mm':'ss");
                return;
            }

            int remaining = Math.Max(
                0,
                5 - (int)Math.Floor((DateTimeOffset.UtcNow - _searchStartedAt).TotalSeconds));
            ArenaStatus = remaining > 0
                ? "Ищем реального игрока… " + remaining
                : "Игрок не найден. Готовим бота…";
        }

        private void OnRepresentativeAssigned()
        {
            if (_client?.MyRepresentative == null)
            {
                return;
            }
            _client.MyRepresentative.ProgressUpdated += OnProgressUpdated;
            _subscribedToRepresentative = true;
            RefreshPlayers();
        }

        private void OnProgressUpdated(
            NetworkCommunicator peer,
            int roundWins,
            int deaths,
            int rating,
            int ratingWins)
        {
            DikozemieArenaPlayerVM player = Players.FirstOrDefault(p => p.Peer?.GetNetworkPeer() == peer);
            if (player == null)
            {
                RefreshPlayers();
                return;
            }
            player.RoundWins = roundWins;
            player.Deaths = deaths;
            player.Rating = rating;
            player.RatingWins = ratingWins;
        }

        private void RefreshPlayers()
        {
            Players.Clear();
            if (_scoreboard == null)
            {
                return;
            }
            MissionPeer localPeer = GameNetwork.MyPeer?.GetComponent<MissionPeer>();
            BattleSideEnum first = localPeer?.Team?.Side == BattleSideEnum.Defender
                ? BattleSideEnum.Defender
                : BattleSideEnum.Attacker;
            AddSide(first);
            AddSide(first == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker);
        }

        private void AddSide(BattleSideEnum side)
        {
            var scoreboardSide = _scoreboard.Sides.FirstOrDefault(s => s != null && s.Side == side);
            if (scoreboardSide == null)
            {
                return;
            }

            foreach (MissionPeer peer in scoreboardSide.Players)
            {
                DikozemieArenaRepresentative representative = peer.GetNetworkPeer().GetComponent<DikozemieArenaRepresentative>();
                var row = new DikozemieArenaPlayerVM(peer, (int)side)
                {
                    RoundWins = representative?.RoundWins ?? 0,
                    Deaths = representative?.Deaths ?? 0,
                    Rating = representative?.Rating ?? 0,
                    RatingWins = representative?.RatingWins ?? 0
                };
                Players.Add(row);
            }
        }

        [DataSourceProperty]
        public string NextBattleText
        {
            get => _nextBattleText;
            set { if (_nextBattleText == value) return; _nextBattleText = value; OnPropertyChangedWithValue(value); }
        }

        [DataSourceProperty]
        public string NextBattleCountdown
        {
            get => _nextBattleCountdown;
            set { if (_nextBattleCountdown == value) return; _nextBattleCountdown = value; OnPropertyChangedWithValue(value); }
        }

        [DataSourceProperty]
        public string AutoNextButtonText
        {
            get => _autoNextButtonText;
            set { if (_autoNextButtonText == value) return; _autoNextButtonText = value; OnPropertyChangedWithValue(value); }
        }

        [DataSourceProperty]
        public string ResultModeText
        {
            get => _resultModeText;
            set { if (_resultModeText == value) return; _resultModeText = value; OnPropertyChangedWithValue(value); }
        }

        [DataSourceProperty]
        public string ResultGoldText
        {
            get => _resultGoldText;
            set { if (_resultGoldText == value) return; _resultGoldText = value; OnPropertyChangedWithValue(value); }
        }

        [DataSourceProperty]
        public string ResultRatingText
        {
            get => _resultRatingText;
            set { if (_resultRatingText == value) return; _resultRatingText = value; OnPropertyChangedWithValue(value); }
        }

        [DataSourceProperty]
        public string ResultExperienceText
        {
            get => _resultExperienceText;
            set { if (_resultExperienceText == value) return; _resultExperienceText = value; OnPropertyChangedWithValue(value); }
        }

        [DataSourceProperty]
        public string ResultKillsText
        {
            get => _resultKillsText;
            set { if (_resultKillsText == value) return; _resultKillsText = value; OnPropertyChangedWithValue(value); }
        }

        [DataSourceProperty]
        public string ResultSubtitle
        {
            get => _resultSubtitle;
            set { if (_resultSubtitle == value) return; _resultSubtitle = value; OnPropertyChangedWithValue(value); }
        }

        [DataSourceProperty]
        public string ResultOpponentLine
        {
            get => _resultOpponentLine;
            set { if (_resultOpponentLine == value) return; _resultOpponentLine = value; OnPropertyChangedWithValue(value); }
        }

        [DataSourceProperty]
        public string ResultFooterHint
        {
            get => _resultFooterHint;
            set { if (_resultFooterHint == value) return; _resultFooterHint = value; OnPropertyChangedWithValue(value); }
        }

        [DataSourceProperty]
        public bool HasKillRows
        {
            get => _hasKillRows;
            set { if (_hasKillRows == value) return; _hasKillRows = value; OnPropertyChangedWithValue(value); }
        }

        [DataSourceProperty]
        public string SpectatorHint
        {
            get => _spectatorHint;
            set { if (_spectatorHint == value) return; _spectatorHint = value; OnPropertyChangedWithValue(value); }
        }

        [DataSourceProperty]
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value) return;
                _isEnabled = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool IsDialogueVisible
        {
            get => _isDialogueVisible;
            set
            {
                if (_isDialogueVisible == value) return;
                _isDialogueVisible = value;
                OnPropertyChangedWithValue(value);
                UpdateInputCapture();
            }
        }

        [DataSourceProperty]
        public bool IsResultVisible
        {
            get => _isResultVisible;
            set
            {
                if (_isResultVisible == value) return;
                _isResultVisible = value;
                OnPropertyChangedWithValue(value);
                UpdateInputCapture();
            }
        }

        [DataSourceProperty]
        public bool IsOutcomeVisible
        {
            get => _isOutcomeVisible;
            set
            {
                if (_isOutcomeVisible == value) return;
                _isOutcomeVisible = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool IsLeaderboardVisible
        {
            get => _isLeaderboardVisible;
            set
            {
                if (_isLeaderboardVisible == value) return;
                _isLeaderboardVisible = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool IsWaitingPanelVisible
        {
            get => _isWaitingPanelVisible;
            set
            {
                if (_isWaitingPanelVisible == value) return;
                _isWaitingPanelVisible = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string WaitingTitle
        {
            get => _waitingTitle;
            set
            {
                value = value ?? string.Empty;
                if (_waitingTitle == value) return;
                _waitingTitle = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string WaitingText
        {
            get => _waitingText;
            set
            {
                value = value ?? string.Empty;
                if (_waitingText == value) return;
                _waitingText = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string WaitingButtonText
        {
            get => _waitingButtonText;
            set
            {
                value = value ?? string.Empty;
                if (_waitingButtonText == value) return;
                _waitingButtonText = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool CanStartWithBots
        {
            get => _canStartWithBots;
            set
            {
                if (_canStartWithBots == value) return;
                _canStartWithBots = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool IsSpectatorNoticeVisible
        {
            get => _isSpectatorNoticeVisible;
            set
            {
                if (_isSpectatorNoticeVisible == value) return;
                _isSpectatorNoticeVisible = value;
                OnPropertyChangedWithValue(value);
                // The spectator HUD is informational. Never focus the Gauntlet
                // layer here: native camera and next/previous fighter controls
                // must continue receiving unmodified input.
                UpdateInputCapture();
            }
        }

        [DataSourceProperty]
        public bool IsDeathCinematicVisible
        {
            get => _isDeathCinematicVisible;
            set
            {
                if (_isDeathCinematicVisible == value) return;
                _isDeathCinematicVisible = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool IsDeathChoiceVisible
        {
            get => _isDeathChoiceVisible;
            set
            {
                if (_isDeathChoiceVisible == value) return;
                _isDeathChoiceVisible = value;
                OnPropertyChangedWithValue(value);
                UpdateInputCapture();
            }
        }

        [DataSourceProperty]
        public bool IsDeathReportVisible
        {
            get => _isDeathReportVisible;
            set
            {
                if (_isDeathReportVisible == value) return;
                _isDeathReportVisible = value;
                OnPropertyChangedWithValue(value);
                UpdateInputCapture();
            }
        }

        private void UpdateInputCapture()
        {
            DialogueVisibilityChanged?.Invoke(
                IsDialogueVisible || IsResultVisible ||
                IsDeathChoiceVisible || IsDeathReportVisible);
        }

        [DataSourceProperty]
        public bool IsResultCinematicVisible
        {
            get => _isResultCinematicVisible;
            set
            {
                if (_isResultCinematicVisible == value) return;
                _isResultCinematicVisible = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool IsResultExperienceVisible
        {
            get => _isResultExperienceVisible;
            set
            {
                if (_isResultExperienceVisible == value) return;
                _isResultExperienceVisible = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool IsResultGloryVisible
        {
            get => _isResultGloryVisible;
            set
            {
                if (_isResultGloryVisible == value) return;
                _isResultGloryVisible = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool IsResultGoldVisible
        {
            get => _isResultGoldVisible;
            set
            {
                if (_isResultGoldVisible == value) return;
                _isResultGoldVisible = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool IsResultContinueVisible
        {
            get => _isResultContinueVisible;
            set
            {
                if (_isResultContinueVisible == value) return;
                _isResultContinueVisible = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool AreResultActionsVisible
        {
            get => _areResultActionsVisible;
            set
            {
                if (_areResultActionsVisible == value) return;
                _areResultActionsVisible = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool CanStartNextBattle
        {
            get => _canStartNextBattle;
            set
            {
                if (_canStartNextBattle == value) return;
                _canStartNextBattle = value;
                OnPropertyChangedWithValue(value);
                OnPropertyChanged(nameof(NextBattleButtonText));
            }
        }

        // Until the team's result arrives the button is locked; its label
        // says so instead of looking like a button that does nothing.
        [DataSourceProperty]
        public string NextBattleButtonText =>
            _canStartNextBattle ? "В БОЙ" : "ЖДЁМ ИТОГ";

        [DataSourceProperty]
        public string DeathCinematicExperience
        {
            get => _deathCinematicExperience;
            set
            {
                if (_deathCinematicExperience == value) return;
                _deathCinematicExperience = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string ResultExperienceReveal
        {
            get => _resultExperienceReveal;
            set
            {
                if (_resultExperienceReveal == value) return;
                _resultExperienceReveal = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string ResultGloryReveal
        {
            get => _resultGloryReveal;
            set
            {
                if (_resultGloryReveal == value) return;
                _resultGloryReveal = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string ResultGoldReveal
        {
            get => _resultGoldReveal;
            set
            {
                if (_resultGoldReveal == value) return;
                _resultGoldReveal = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public int DeathCinematicPulseToken
        {
            get => _deathCinematicPulseToken;
            set
            {
                if (_deathCinematicPulseToken == value) return;
                _deathCinematicPulseToken = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public int ResultCinematicPulseToken
        {
            get => _resultCinematicPulseToken;
            set
            {
                if (_resultCinematicPulseToken == value) return;
                _resultCinematicPulseToken = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string SpectatorStatus
        {
            get => _spectatorStatus;
            set
            {
                if (_spectatorStatus == value) return;
                _spectatorStatus = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string DeathReportSummary
        {
            get => _deathReportSummary;
            set
            {
                if (_deathReportSummary == value) return;
                _deathReportSummary = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string DeathReportStatus
        {
            get => _deathReportStatus;
            set
            {
                if (_deathReportStatus == value) return;
                _deathReportStatus = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string ResultTitle
        {
            get => _resultTitle;
            set
            {
                if (_resultTitle == value) return;
                _resultTitle = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string ResultSummary
        {
            get => _resultSummary;
            set
            {
                if (_resultSummary == value) return;
                _resultSummary = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string ResultReward
        {
            get => _resultReward;
            set
            {
                if (_resultReward == value) return;
                _resultReward = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string ResultKillReport
        {
            get => _resultKillReport;
            set
            {
                if (_resultKillReport == value) return;
                _resultKillReport = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string ResultProgress
        {
            get => _resultProgress;
            set
            {
                if (_resultProgress == value) return;
                _resultProgress = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string ResultPlayerName
        {
            get => _resultPlayerName;
            set
            {
                if (_resultPlayerName == value) return;
                _resultPlayerName = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string ResultOpponentName
        {
            get => _resultOpponentName;
            set
            {
                if (_resultOpponentName == value) return;
                _resultOpponentName = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string ResultWinnerName
        {
            get => _resultWinnerName;
            set
            {
                if (_resultWinnerName == value) return;
                _resultWinnerName = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string ResultPlayerScore
        {
            get => _resultPlayerScore;
            set
            {
                if (_resultPlayerScore == value) return;
                _resultPlayerScore = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string ResultOpponentScore
        {
            get => _resultOpponentScore;
            set
            {
                if (_resultOpponentScore == value) return;
                _resultOpponentScore = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string ResultPlayerRating
        {
            get => _resultPlayerRating;
            set
            {
                if (_resultPlayerRating == value) return;
                _resultPlayerRating = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string ResultOpponentRating
        {
            get => _resultOpponentRating;
            set
            {
                if (_resultOpponentRating == value) return;
                _resultOpponentRating = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public MBBindingList<DikozemieArenaLeaderboardRowVM> LeaderboardRows
        {
            get => _leaderboardRows;
            set
            {
                if (_leaderboardRows == value) return;
                _leaderboardRows = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string LeaderboardPageText
        {
            get => _leaderboardPageText;
            set
            {
                if (_leaderboardPageText == value) return;
                _leaderboardPageText = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public MBBindingList<DikozemieArenaRewardRowVM> DeathRewardRows
        {
            get => _deathRewardRows;
            set
            {
                if (_deathRewardRows == value) return;
                _deathRewardRows = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public int LeaderboardFocusToken
        {
            get => _leaderboardFocusToken;
            set
            {
                if (_leaderboardFocusToken == value) return;
                _leaderboardFocusToken = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string ArenaStatus
        {
            get => _arenaStatus;
            set
            {
                if (_arenaStatus == value) return;
                _arenaStatus = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string RemainingRoundTime
        {
            get => _remainingRoundTime;
            set
            {
                if (_remainingRoundTime == value) return;
                _remainingRoundTime = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public MBBindingList<DikozemieArenaPlayerVM> Players
        {
            get => _players;
            set
            {
                if (_players == value) return;
                _players = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string RulesText
        {
            get => _rulesText;
            set
            {
                if (_rulesText == value) return;
                _rulesText = value;
                OnPropertyChangedWithValue(value);
            }
        }
    }

    public sealed class DikozemieArenaLeaderboardRowVM : ViewModel
    {
        internal DikozemieArenaLeaderboardRowVM(
            DikozemieArenaLeaderboardEntry entry,
            bool isPlayer)
        {
            Rank = entry?.Rank ?? 0;
            Name = DikozemieArenaBotNames.Format(
                entry?.CharacterId,
                entry?.DisplayName);
            Rating = entry?.Rating ?? 0;
            Wins = entry?.Wins ?? 0;
            Losses = entry?.Losses ?? 0;
            IsPlayer = isPlayer;
        }

        [DataSourceProperty] public int Rank { get; }
        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public int Rating { get; }
        [DataSourceProperty] public int Wins { get; }
        [DataSourceProperty] public int Losses { get; }
        [DataSourceProperty] public bool IsPlayer { get; }
    }

    public sealed class DikozemieArenaRewardRowVM : ViewModel
    {
        internal DikozemieArenaRewardRowVM(
            int killNumber,
            string victimName,
            int victimRating,
            int multiplier,
            int reward)
        {
            KillNumber = "#" + Math.Max(1, killNumber);
            VictimName = string.IsNullOrWhiteSpace(victimName)
                ? "Неизвестный боец"
                : victimName;
            VictimRating = "Рейтинг " + Math.Max(0, victimRating);
            Multiplier = "×" + Math.Max(1, multiplier);
            Glory = "+" + Math.Max(0, reward) + " славы";
            Gold = "+" + Math.Max(0, reward) + " золота";
        }

        [DataSourceProperty] public string KillNumber { get; }
        [DataSourceProperty] public string VictimName { get; }
        [DataSourceProperty] public string VictimRating { get; }
        [DataSourceProperty] public string Multiplier { get; }
        [DataSourceProperty] public string Glory { get; }
        [DataSourceProperty] public string Gold { get; }
    }
}
