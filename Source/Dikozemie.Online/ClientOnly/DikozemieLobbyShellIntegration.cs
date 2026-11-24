using System;
using System.IO;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Multiplayer;
using TaleWorlds.MountAndBlade.Multiplayer.GauntletUI;
using TaleWorlds.MountAndBlade.Diamond;
using TaleWorlds.MountAndBlade.Multiplayer.ViewModelCollection.Lobby;
using TaleWorlds.MountAndBlade.Multiplayer.ViewModelCollection.Lobby.OfficialGame;
using TaleWorlds.ScreenSystem;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.InputSystem;

namespace Dikozemie.Online
{
    internal static class DikozemieLobbyShellIntegration
    {
        private static MultiplayerLobbyGauntletScreen _screen;
        private static MPLobbyVM _lobby;
        private static bool _isArenaContext;
        private static bool _isHeroContext;
        private static bool _restoreArenaAfterTrainingExit;
        private static bool _pendingArenaSearch;
        private static DateTimeOffset _lastLobbyRecoveryActionUtc;
        private static int _selectedTabIndex;
        private static bool _ownsPvpSearch;
        private static DateTimeOffset _suppressNativePvpSearchUntil;
        private static ButtonWidget _boundArenaStartButton;
        private static ButtonWidget _boundLobbyExitButton;
        private static Widget _nativeGameCreationRoot;
        private static DikozemieLobbyTabButtonWidget _nativeGameCreationTab;
        private static int _lastTopTabIndex = -1;
        private static DateTimeOffset _lastTopTabActionUtc;
        private static DateTimeOffset _lastArenaButtonActionUtc;
        private static DateTimeOffset _lastExitButtonActionUtc;
        private static bool _liveTabSmokeFinished;
        private static int _liveTabSmokeCurrent = -1;
        private static int _liveTabSmokeNext;
        private static DateTimeOffset _liveTabSmokeVerifyAt;
        private static bool _liveExitSmokeHandled;
        private static DateTimeOffset _liveExitSmokeAt;
        private static bool _liveNavalSmokeHandled;
        private static int _liveNavalSmokePhase;
        private static DateTimeOffset _liveNavalSmokeNextAt;
        private static DateTimeOffset _liveNavalSmokeDiagnosticAt;
        private static bool _exitRequestInProgress;
        private static bool _exitAwaitingSearchRelease;
        private static DateTimeOffset _exitReleaseRequestedAt;
        private static DateTimeOffset _nextExitReleaseAttemptAt;
        private static bool _exitAwaitingNativeDisconnect;
        private static DateTimeOffset _nativeExitRequestedAt;

        internal static bool IsArenaContext => _isArenaContext;
        internal static bool IsHeroContext => _isHeroContext;
        internal static bool IsFriendsListOpen =>
            _lobby?.Friends?.IsListEnabled == true;

        internal static void OpenVenueFromMap(DikozemieBattleVenue venue) =>
            SelectArenaVenue(venue);

        // Stage 1 of the plan (customer, 2026-11-05): no separate tabs, the
        // map is the hub and the town menu leads to the arena, the port and
        // the market.
        internal static void OpenVenueFromTown(DikozemieBattleVenue venue)
        {
            TraceButtonAction("town venue " + venue);
            SelectArenaVenue(venue);
        }

        internal static void OpenMarketFromTown()
        {
            TraceButtonAction("town market kind=" + DikozemieTown.MarketKind);
            SelectTopTab(DikozemieLobbyTabButtonWidget.InventorySectionIndex);
        }

        // The map, with the town screen of the place the hero stands in
        // (townScreen) or the open map.
        internal static void OpenMap(bool townScreen)
        {
            DikozemieTown.RequestTownScreen(townScreen);
            SelectTopTab(DikozemieLobbyTabButtonWidget.MapSectionIndex);
        }

        // A step of the location line under the header was clicked.
        internal static void NavigateFromLocationBar(int tabIndex, bool townScreen)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (now - _lastLocationActionUtc < TimeSpan.FromMilliseconds(300))
            {
                return;
            }
            _lastLocationActionUtc = now;
            if (DikozemieNavalBattleLauncher.IsPending)
            {
                TraceButtonAction("location " + tabIndex + " ignored during naval battle");
                return;
            }
            TraceButtonAction("location tab=" + tabIndex + " town=" + townScreen);
            if (tabIndex == DikozemieLobbyTabButtonWidget.MapSectionIndex)
            {
                OpenMap(townScreen);
                return;
            }
            if (tabIndex == DikozemieLobbyTabButtonWidget.InventorySectionIndex)
            {
                DikozemieTown.OpenArmory(false);
            }
            SelectTopTab(tabIndex);
        }

        private static DateTimeOffset _lastLocationActionUtc;

        // The «Арена» tab of the header and the home card: the hero's place
        // on the map, or hero creation for a new player.
        internal static void OpenArenaHub()
        {
            if (!DikozemieHeroProfile.IsCreated)
            {
                SelectTopTab(DikozemieLobbyTabButtonWidget.HeroSectionIndex);
                return;
            }
            DikozemieTravelState.EnsureLoadedForHero();
            TraceButtonAction("arena hub here=" + (DikozemieTown.Here?.Id ?? "road"));
            OpenMap(DikozemieTown.Here != null);
        }

        private static bool _roadBattleStartPending;

        // Stays set until the battle is over: the profile is sent after joining.
        internal static bool IsRoadBattlePending { get; private set; }

        internal static void StartRoadBattle(DikozemieBattleVenue venue, int teamSize,
            DikozemieArenaDiscipline discipline = DikozemieArenaDiscipline.Open)
        {
            DikozemieArenaMatchSelection.SelectVenue(venue);
            if (venue == DikozemieBattleVenue.Arena)
            {
                // The open style fits whatever the hero carries; a style the
                // rotation left behind (horse archers) may not. Two heroes
                // who met on the road duel on foot (stage 5): the battle
                // servers take a reserved pair only so.
                DikozemieArenaMatchSelection.SelectArenaBattle(
                    teamSize, discipline, DikozemieArenaComposition.Mixed);
            }
            IsRoadBattlePending = true;
            _roadBattleStartPending = true;
            SelectArenaVenue(venue);
        }

        private static bool _roadBattleFought;

        // The arena reported how a fight of the map ended.
        internal static void NoteRoadBattleResult()
        {
            if (IsRoadBattlePending)
            {
                _roadBattleFought = true;
            }
        }

        internal static void ClearRoadBattle()
        {
            IsRoadBattlePending = false;
            _roadBattleStartPending = false;
            // No result came: a captive stays a captive and may try again.
            DikozemieRoads.CancelCaptiveFight();
            if (_roadBattleFought)
            {
                // Back where the fight began: the town shows what came of it.
                _roadBattleFought = false;
                TraceButtonAction("road battle over: back to the map");
                OpenMap(DikozemieTown.Here != null);
            }
        }

        // The observer of the watch goes to his battle from the arena page,
        // as every battle does: on the map page the map stayed drawn over
        // the mission (2026-11-11).
        private static DikozemieBattleVenue? _observerVenue;

        private static DateTime _observerVenueAt;

        internal static void StartObserverWatch(DikozemieBattleVenue venue)
        {
            _observerVenue = venue;
            _observerVenueAt = DateTime.UtcNow;
            SelectTopTab(DikozemieLobbyTabButtonWidget.ArenaTabIndex);
        }

        private static void TickObserverStart()
        {
            if (_observerVenue == null)
            {
                return;
            }
            if (!DikozemieWatch.IsWatching)
            {
                _observerVenue = null;
                return;
            }
            if (!_isArenaContext)
            {
                // The arena page did not open (another page took over): the
                // watch ends here, not some minutes later by itself.
                if ((DateTime.UtcNow - _observerVenueAt).TotalSeconds > 10)
                {
                    _observerVenue = null;
                    DikozemieWatch.StopWatching();
                    TraceButtonAction("observer search dropped: the arena page did not open");
                }
                return;
            }
            DikozemieBattleVenue venue = _observerVenue.Value;
            _observerVenue = null;
            var client = NetworkMain.GameClient;
            bool started = venue == DikozemieBattleVenue.Naval
                ? DikozemiePvpSearchController.StartNaval(client)
                : DikozemiePvpSearchController.Start(client);
            TraceButtonAction("observer search from the arena page started=" + started);
            if (!started)
            {
                DikozemieWatch.StopWatching();
            }
        }

        private static void TickRoadBattleStart()
        {
            if (!_roadBattleStartPending || !_isArenaContext)
            {
                return;
            }
            _roadBattleStartPending = false;
            TraceButtonAction("road battle start");
            TryHandleArenaStartButtonClick();
        }

        // The first login attempt often fails while Steam and the TaleWorlds
        // service are still starting, and players were left on «Вход не
        // выполнен» with a button they did not understand.
        private static void RetryLogin(MPLobbyVM lobby)
        {
            // Leaving through «Выход» passes the login page: logging in again
            // there made the button look dead (2026-10-31).
            // Steam's ticket check at TaleWorlds failed for the customer for
            // a minute at a time (2026-11-02 and 11-04, «Could not get access
            // from your platform service») and then passed. Keep trying for
            // about a minute and a half, waiting a little longer each time.
            if (lobby.IsLoggedIn || lobby.Login?.CanTryLogin != true ||
                _loginRetries >= 8 || _exitRequestInProgress)
            {
                return;
            }
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (now < _nextLoginRetry)
            {
                return;
            }
            _loginRetries++;
            _nextLoginRetry = now.AddSeconds(Math.Min(15, 2 + _loginRetries * 2));
            TraceButtonAction("login retry " + _loginRetries);
            lobby.Login.ExecuteLogin();
        }

        private static DateTimeOffset _nextLobbyScan = DateTimeOffset.MinValue;
        private static MultiplayerLobbyGauntletScreen _lastScannedScreen;
        private static int _loginRetries;
        private static DateTimeOffset _nextLoginRetry = DateTimeOffset.MinValue;

        internal static void Tick()
        {
            ScreenBase topScreen = ScreenManager.TopScreen;
            if (topScreen == null)
            {
                return;
            }

            MultiplayerLobbyGauntletScreen currentScreen =
                topScreen as MultiplayerLobbyGauntletScreen;
            if (currentScreen == null)
            {
                return;
            }

            MPLobbyVM lobby = currentScreen.DataSource;
            if (lobby == null)
            {
                return;
            }
            TickRoadBattleStart();
            TickObserverStart();
            if (IsRoadBattlePending && !_roadBattleStartPending &&
                !DikozemiePvpSearchController.IsSearching &&
                TaleWorlds.MountAndBlade.Mission.Current == null)
            {
                // Search cancelled or the battle is over: the next one is ordinary.
                ClearRoadBattle();
            }

            if (!ReferenceEquals(_screen, currentScreen))
            {
                // TaleWorlds replaces MultiplayerLobbyGauntletScreen when a
                // native page swaps its content movie. That replacement is
                // not a user navigation event: keep our custom Map, Hero,
                // Arena or Skills context until SelectTopTab explicitly
                // chooses another tab.
                bool preserveCustomContext =
                    _screen != null && IsCustomTopTab(_selectedTabIndex);
                if (!preserveCustomContext)
                {
                    DikozemieHeroMenuController.Hide();
                }
                DikozemieArenaLobbyMenuController.Show(lobby);
                _screen = currentScreen;
                _lobby = lobby;
                if (!_exitAwaitingSearchRelease &&
                    !_exitAwaitingNativeDisconnect)
                {
                    _exitRequestInProgress = false;
                }
                if (preserveCustomContext)
                {
                    _isArenaContext = _selectedTabIndex == 5;
                    _isHeroContext =
                        _selectedTabIndex == 4 || _selectedTabIndex == 7;
                    DikozemieArenaLobbyMenuController.SetMapVisible(
                        _selectedTabIndex == 3);
                }
                else
                {
                    _isArenaContext = false;
                    _isHeroContext = false;
                    _selectedTabIndex = GetNativeTabIndex(lobby.CurrentPage);
                }
                _ownsPvpSearch = DikozemiePvpSearchController.IsSearching;
            }

            RetryLogin(lobby);
            DikozemieHorseUpkeep.Tick();
            if (CompletePendingLobbyExit(lobby))
            {
                return;
            }
            if (CompletePendingNativeLobbyExit(lobby))
            {
                return;
            }

            if (_isArenaContext &&
                (lobby.CurrentPage != MPLobbyVM.LobbyPage.Matchmaking ||
                 lobby.Matchmaking?.SelectedSubPageIndex != 0))
            {
                // Lobby page changes are asynchronous. Keep the selected
                // Arena context authoritative until the player explicitly
                // chooses another top tab; a short grace timer can expire
                // while the native lobby is still reporting Home.
                ActivateNativeLobbyPage(lobby, 5);
                lobby.Matchmaking?.TrySetMatchmakingSubPage(
                    MPMatchmakingVM.MatchmakingSubPages.QuickPlay);
            }

            if (_isHeroContext &&
                lobby.CurrentPage != MPLobbyVM.LobbyPage.Home)
            {
                // SetPage is asynchronous. Clearing the custom context here
                // used to close Hero/Inventory on the next frame while the
                // native lobby still reported Matchmaking.
                ActivateNativeLobbyPage(lobby, 4);
            }

            if (!_isArenaContext && !_isHeroContext &&
                !IsCustomTopTab(_selectedTabIndex))
            {
                _selectedTabIndex = GetNativeTabIndex(lobby.CurrentPage);
            }

            RestoreLobbyAfterTrainingExit(lobby);
            ResumePendingArenaSearch(lobby);

            DikozemieLobbyTabButtonWidget.SetSelectedTab(_selectedTabIndex);
            // These walk the whole widget tree: 2 ms of a 7 ms frame when done
            // on every frame, and a button does not change that often.
            DateTimeOffset scanNow = DateTimeOffset.UtcNow;
            if (scanNow >= _nextLobbyScan ||
                !ReferenceEquals(_lastScannedScreen, currentScreen))
            {
                _nextLobbyScan = scanNow.AddMilliseconds(250);
                _lastScannedScreen = currentScreen;
                SyncGameCreationTabVisibility(currentScreen);
                EnsureLobbyExitButtonBound(currentScreen);
                EnsureArenaStartButtonBound(currentScreen);
            }
            ApplyLobbyLabels(lobby);
            ApplyMatchmakingLayout(lobby);
            HandleDirectLobbyMouseInput(currentScreen);
            ShowPendingTrainingResult();
            RunLiveTabSmoke();
            RunLiveNavalMultiplayerSmoke();
            RunLiveLobbyExitSmoke();
            HandlePvpSearch(lobby);
            DikozemieArenaLobbyMenuController.TickMapOverlay(lobby);
            if (_isHeroContext &&
                !DikozemieHeroMenuController.IsOpen &&
                !DikozemieHeroAppearanceEditor.IsOpen &&
                !DikozemieNavalBattleLauncher.IsPending)
            {
                DikozemieHeroMenuController.Show();
            }
        }

        internal static void SelectTopTab(int tabIndex)
        {
            MPLobbyVM lobby = _lobby;
            if (lobby == null)
            {
                return;
            }

            if ((tabIndex == 3 || tabIndex == 5 || tabIndex == 7) &&
                !DikozemieHeroProfile.IsCreated &&
                !DikozemieHeroAppearanceEditor.IsOpen)
            {
                tabIndex = 4;
            }

            if (tabIndex != 5 && DikozemiePvpSearchController.IsSearching)
            {
                DikozemiePvpSearchController.Cancel();
                _ownsPvpSearch = false;
                SuppressNativeMatchmaking(lobby);
                TraceButtonAction("arena-search cancelled by top-tab " + tabIndex);
            }
            if (tabIndex != 5)
            {
                _pendingArenaSearch = false;
            }

            _isArenaContext = tabIndex == 5;
            _isHeroContext = tabIndex == 4 || tabIndex == 7;
            DikozemieArenaLobbyMenuController.SetMapVisible(tabIndex == 3);
            if (!_isHeroContext)
            {
                DikozemieHeroMenuController.Hide();
            }
            ActivateNativeLobbyPage(lobby, tabIndex);
            switch (tabIndex)
            {
                case 0:
                    break;
                case 1:
                    break;
                case 2:
                    lobby.Matchmaking?.TrySetMatchmakingSubPage(
                        MPMatchmakingVM.MatchmakingSubPages.QuickPlay);
                    break;
                case 3:
                    // Карта пока использует безопасную штатную главную страницу.
                    break;
                case 4:
                    DikozemieHeroMenuController.Show();
                    if (DikozemieHeroProfile.IsCreated)
                    {
                        DikozemieHeroMenuController.ShowProfileSection(2);
                    }
                    break;
                case 5:
                    // «Всё в одном меню»: друзья и отряд сразу под рукой.
                    if (lobby.Friends != null)
                    {
                        lobby.Friends.IsListEnabled = true;
                    }
                    ClearModeSelection(lobby.Matchmaking?.CustomGameTypes);
                    lobby.Matchmaking?.TrySetMatchmakingSubPage(
                        MPMatchmakingVM.MatchmakingSubPages.QuickPlay);
                    break;
                case 6:
                    break;
                case 7:
                    DikozemieHeroMenuController.Show();
                    DikozemieHeroMenuController.ShowProfileSection(1);
                    break;
                default:
                    return;
            }

            _selectedTabIndex = tabIndex;
            DikozemieLobbyTabButtonWidget.SetSelectedTab(tabIndex);
            ApplyMatchmakingLayout(lobby);
        }

        private static void RunLiveTabSmoke()
        {
            if (_liveTabSmokeFinished)
            {
                return;
            }

            string requested = Environment.GetEnvironmentVariable(
                "DIKOZEMIE_LOBBY_TAB_SMOKE");
            if (!string.Equals(requested, "1", StringComparison.Ordinal))
            {
                _liveTabSmokeFinished = true;
                return;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (_liveTabSmokeCurrent >= 0)
            {
                if (now < _liveTabSmokeVerifyAt)
                {
                    return;
                }

                bool verified =
                    _selectedTabIndex == _liveTabSmokeCurrent &&
                    _isArenaContext == (_liveTabSmokeCurrent == 5) &&
                    _isHeroContext ==
                        (_liveTabSmokeCurrent == 4 ||
                         _liveTabSmokeCurrent == 7) &&
                    IsExpectedLiveSmokePage(
                        _liveTabSmokeCurrent,
                        _lobby.CurrentPage);
                Console.WriteLine(
                    "DikozemieOnline: live tab smoke " +
                    (verified ? "verified" : "FAILED") + " tab=" +
                    _liveTabSmokeCurrent + " selected=" + _selectedTabIndex +
                    " page=" + _lobby?.CurrentPage + " arena=" +
                    _isArenaContext + " hero=" + _isHeroContext);
                _liveTabSmokeCurrent = -1;
                if (!verified)
                {
                    _liveTabSmokeFinished = true;
                    return;
                }
            }

            if (_liveTabSmokeNext < 8)
            {
                int tabIndex = _liveTabSmokeNext++;
                SelectTopTab(tabIndex);
                _liveTabSmokeCurrent = tabIndex;
                _liveTabSmokeVerifyAt = now.AddMilliseconds(900);
                Console.WriteLine(
                    "DikozemieOnline: live tab smoke requested tab=" +
                    tabIndex);
                return;
            }

            SelectTopTab(5);
            _liveTabSmokeFinished = true;
            Console.WriteLine(
                "DikozemieOnline: live tab smoke completed tabs=0..7 " +
                "returned=5");
        }

        private static bool IsExpectedLiveSmokePage(
            int tabIndex,
            MPLobbyVM.LobbyPage page)
        {
            switch (tabIndex)
            {
                case 0:
                case 3:
                case 4:
                case 7:
                    return page == MPLobbyVM.LobbyPage.Home;
                case 1:
                    return page == MPLobbyVM.LobbyPage.Armory;
                case 2:
                case 5:
                    return page == MPLobbyVM.LobbyPage.Matchmaking;
                case 6:
                    return page == MPLobbyVM.LobbyPage.Profile;
                default:
                    return false;
            }
        }

        private static void RunLiveNavalMultiplayerSmoke()
        {
            if (_liveNavalSmokeHandled)
            {
                return;
            }

            string requested = Environment.GetEnvironmentVariable(
                "DIKOZEMIE_NAVAL_MULTIPLAYER_SMOKE");
            bool navalRequested = string.Equals(
                requested,
                "1",
                StringComparison.Ordinal);
            if (!navalRequested &&
                TryConsumeLiveNavalSmokeRequest(out string requestedClass))
            {
                navalRequested = true;
                Environment.SetEnvironmentVariable(
                    "DIKOZEMIE_NAVAL_MULTIPLAYER_SMOKE",
                    "1");
                Environment.SetEnvironmentVariable(
                    "DIKOZEMIE_NAVAL_MULTIPLAYER_SMOKE_CLASS",
                    requestedClass);
                Environment.SetEnvironmentVariable(
                    "DIKOZEMIE_NAVAL_CAPTURE",
                    "1");
                Environment.SetEnvironmentVariable(
                    "DIKOZEMIE_NAVAL_CAPTURE_DIR",
                    Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.LocalApplicationData),
                        "Dikozemie",
                        "naval-captures",
                        requestedClass));
            }
            bool arenaRequested = string.Equals(
                Environment.GetEnvironmentVariable(
                    "DIKOZEMIE_ARENA_MULTIPLAYER_SMOKE"),
                "1",
                StringComparison.Ordinal);
            if (!navalRequested && !arenaRequested)
            {
                _liveNavalSmokeHandled = true;
                return;
            }

            DateTimeOffset diagnosticNow = DateTimeOffset.UtcNow;
            if (diagnosticNow >= _liveNavalSmokeDiagnosticAt)
            {
                LobbyClient diagnosticClient = GetLobbyClient();
                TraceButtonAction(
                    (navalRequested ? "naval" : "arena") +
                    "-smoke state tabFinished=" + _liveTabSmokeFinished +
                    " hero=" + DikozemieHeroProfile.IsCreated +
                    " client=" +
                    (diagnosticClient?.CurrentState.ToString() ?? "none") +
                    " connected=" + (diagnosticClient?.Connected ?? false) +
                    " atLobby=" + (diagnosticClient?.AtLobby ?? false) +
                    " phase=" + _liveNavalSmokePhase);
                _liveNavalSmokeDiagnosticAt = diagnosticNow.AddSeconds(5);

                if (diagnosticClient?.Connected != true &&
                    _lobby?.Login?.CanTryLogin == true)
                {
                    try
                    {
                        TraceButtonAction(
                            (navalRequested ? "naval" : "arena") +
                            "-smoke requesting native multiplayer login");
                        _lobby.Login.ExecuteLogin();
                    }
                    catch (Exception error)
                    {
                        TraceButtonAction(
                            "naval-smoke login failed=" + error.Message);
                    }
                }
            }

            if (!_liveTabSmokeFinished)
            {
                return;
            }
            if (!DikozemieHeroProfile.IsCreated)
            {
                _liveNavalSmokeHandled = true;
                Console.WriteLine(
                    "DikozemieOnline: live naval multiplayer smoke FAILED: " +
                    "created hero is required");
                return;
            }

            LobbyClient client = GetLobbyClient();
            if (client?.Connected != true || !client.AtLobby)
            {
                return;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (_liveNavalSmokePhase == 0)
            {
                DikozemieArenaMatchSelection.SelectVenue(
                    navalRequested
                        ? DikozemieBattleVenue.Naval
                        : DikozemieBattleVenue.Arena);
                if (navalRequested)
                {
                    DikozemieArenaMatchSelection.SelectNavalClass(
                        ResolveLiveNavalSmokeClass());
                }
                else
                {
                    DikozemieArenaMatchSelection.SelectTeamSize(
                        ResolveLiveArenaSmokeTeamSize());
                    TraceButtonAction(
                        "arena-smoke selected teamSize=" +
                        DikozemieArenaMatchSelection.TeamSize);
                }
                SelectTopTab(5);
                _liveNavalSmokePhase = 1;
                _liveNavalSmokeNextAt = now.AddSeconds(1);
                Console.WriteLine(
                    "DikozemieOnline: live naval multiplayer smoke selected " +
                    DikozemieArenaMatchSelection.SelectedNavalBattle.HullId);
                return;
            }

            if (_liveNavalSmokePhase != 1 || now < _liveNavalSmokeNextAt)
            {
                return;
            }

            bool started;
            string errorMessage = string.Empty;
            if (navalRequested)
            {
                started = DikozemieArenaLobbyController.TryStartNavalBattle(
                    out errorMessage);
            }
            else
            {
                LobbyClient arenaClient = GetLobbyClient();
                started = DikozemieArenaMatchSelection.TryValidateCurrent(
                        out errorMessage) &&
                    DikozemiePvpSearchController.Start(arenaClient);
            }
            if (!started)
            {
                _liveNavalSmokeHandled = true;
                Console.WriteLine(
                    "DikozemieOnline: live naval multiplayer smoke FAILED: " +
                    errorMessage);
                return;
            }

            _ownsPvpSearch = true;
            _liveNavalSmokeHandled = true;
            Console.WriteLine(
                "DikozemieOnline: live naval multiplayer smoke search started");
        }

        private static bool TryConsumeLiveNavalSmokeRequest(
            out string requestedClass)
        {
            requestedClass = string.Empty;
            string requestPath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "Dikozemie",
                "naval-multiplayer-smoke-request.txt");
            try
            {
                if (!File.Exists(requestPath))
                {
                    return false;
                }

                requestedClass = File.ReadAllText(requestPath).Trim();
                File.Delete(requestPath);
                if (!string.Equals(
                        requestedClass,
                        "light",
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(
                        requestedClass,
                        "medium",
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(
                        requestedClass,
                        "heavy",
                        StringComparison.OrdinalIgnoreCase))
                {
                    requestedClass = "light";
                }
                TraceButtonAction(
                    "naval-smoke request file consumed class=" +
                    requestedClass);
                return true;
            }
            catch (Exception error)
            {
                TraceButtonAction(
                    "naval-smoke request file failed=" + error.Message);
                return false;
            }
        }

        private static DikozemieNavalShipClass ResolveLiveNavalSmokeClass()
        {
            string requestedClass = Environment.GetEnvironmentVariable(
                "DIKOZEMIE_NAVAL_MULTIPLAYER_SMOKE_CLASS");
            if (string.Equals(
                    requestedClass,
                    "heavy",
                    StringComparison.OrdinalIgnoreCase))
            {
                return DikozemieNavalShipClass.Heavy;
            }
            if (string.Equals(
                    requestedClass,
                    "medium",
                    StringComparison.OrdinalIgnoreCase))
            {
                return DikozemieNavalShipClass.Medium;
            }
            return DikozemieNavalShipClass.Light;
        }

        private static int ResolveLiveArenaSmokeTeamSize()
        {
            string requested = Environment.GetEnvironmentVariable(
                "DIKOZEMIE_ARENA_MULTIPLAYER_SMOKE_TEAM_SIZE");
            return int.TryParse(requested, out int teamSize)
                ? teamSize
                : DikozemieArenaMatchSelection.TeamSize;
        }

        private static void RunLiveLobbyExitSmoke()
        {
            if (_liveExitSmokeHandled)
            {
                return;
            }

            string requested = Environment.GetEnvironmentVariable(
                "DIKOZEMIE_LOBBY_EXIT_SMOKE");
            if (!string.Equals(requested, "1", StringComparison.Ordinal))
            {
                _liveExitSmokeHandled = true;
                return;
            }

            if (!_liveTabSmokeFinished || _liveTabSmokeNext < 8)
            {
                return;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (_liveExitSmokeAt == default)
            {
                _liveExitSmokeAt = now.AddSeconds(1);
                return;
            }
            if (now < _liveExitSmokeAt)
            {
                return;
            }

            bool routed = TryHandleLobbyExitButtonClick();
            _liveExitSmokeHandled = true;
            Console.WriteLine(
                "DikozemieOnline: live lobby exit smoke routed=" + routed);
        }

        private static bool IsCustomTopTab(int tabIndex)
        {
            return tabIndex == 3 || tabIndex == 4 ||
                tabIndex == 5 || tabIndex == 7;
        }

        private static void ActivateNativeLobbyPage(
            MPLobbyVM lobby,
            int tabIndex)
        {
            LobbyState lobbyState =
                Game.Current?.GameStateManager?.ActiveState as LobbyState;
            if (lobbyState != null)
            {
                switch (tabIndex)
                {
                    case 0:
                    case 3:
                    case 4:
                    case 7:
                        lobbyState.OnActivateHome();
                        return;
                    case 1:
                        lobbyState.OnActivateArmory();
                        return;
                    case 2:
                    case 5:
                        lobbyState.OnActivateMatchmaking();
                        return;
                    case 6:
                        lobbyState.OnActivateProfile();
                        return;
                }
            }

            // Unit harnesses and the first lobby frame can have a view model
            // before LobbyState becomes active. Keep direct VM routing only
            // as that narrow fallback; live navigation must go through the
            // state activators so the screen swaps its native content movie.
            switch (tabIndex)
            {
                case 0:
                case 3:
                case 4:
                case 7:
                    lobby.SetPage(MPLobbyVM.LobbyPage.Home);
                    break;
                case 1:
                    lobby.SetPage(MPLobbyVM.LobbyPage.Armory);
                    break;
                case 2:
                case 5:
                    lobby.SetPage(
                        MPLobbyVM.LobbyPage.Matchmaking,
                        MPMatchmakingVM.MatchmakingSubPages.QuickPlay);
                    break;
                case 6:
                    lobby.SetPage(MPLobbyVM.LobbyPage.Profile);
                    break;
            }
        }

        internal static void TryHandleTopTabClick(int tabIndex)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (_lastTopTabIndex == tabIndex &&
                now - _lastTopTabActionUtc < TimeSpan.FromMilliseconds(350))
            {
                return;
            }

            _lastTopTabIndex = tabIndex;
            _lastTopTabActionUtc = now;
            if (DikozemieNavalBattleLauncher.IsPending)
            {
                // The naval battle owns the screen until the multiplayer lobby
                // is back. A hero menu opened over its native teardown took
                // the whole game down (2026-10-27 20:43, PID 15912).
                TraceButtonAction("top-tab " + tabIndex + " ignored during naval battle");
                return;
            }
            TraceButtonAction("top-tab " + tabIndex);
            if (tabIndex == DikozemieLobbyTabButtonWidget.ArenaTabIndex)
            {
                // The header's «Арена» opens the map: the arena is in the towns.
                OpenArenaHub();
                return;
            }
            if (tabIndex == DikozemieLobbyTabButtonWidget.ArenaSectionIndex ||
                tabIndex == DikozemieLobbyTabButtonWidget.NavalSectionIndex)
            {
                SelectArenaVenue(
                    tabIndex == DikozemieLobbyTabButtonWidget.NavalSectionIndex
                        ? DikozemieBattleVenue.Naval
                        : DikozemieBattleVenue.Arena);
                return;
            }
            SelectTopTab(tabIndex);
        }

        // A search belongs to the venue it started in.
        private static void SelectArenaVenue(DikozemieBattleVenue venue)
        {
            if (DikozemieArenaMatchSelection.MenuVenue != venue &&
                DikozemiePvpSearchController.IsSearching)
            {
                DikozemiePvpSearchController.Cancel();
                _ownsPvpSearch = false;
            }
            DikozemieArenaMatchSelection.SelectVenue(venue);
            SelectTopTab(DikozemieLobbyTabButtonWidget.ArenaTabIndex);
        }

        internal static bool TryHandleArenaStartButtonClick()
        {
            MPLobbyVM lobby = _lobby;
            MPMatchmakingVM matchmaking = lobby?.Matchmaking;
            if (!_isArenaContext || matchmaking == null)
            {
                return false;
            }

            if (!DikozemieHeroProfile.IsCreated)
            {
                SelectTopTab(4);
                return true;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            // The same physical click can reach the custom widget, its bound
            // handler and the raw mouse fallback in adjacent frames. Keep the
            // guard long enough to cover a delayed release event as the busy
            // curtain disappears after a failed custom-game join.
            if (now - _lastArenaButtonActionUtc < TimeSpan.FromMilliseconds(750))
            {
                return true;
            }
            _lastArenaButtonActionUtc = now;

            // A click used only to restore window focus can land on the arena
            // button in the exact frame TaleWorlds returns from a stale lobby
            // registration. The search task is still resolving that join and
            // will retry automatically, so the arena button must not turn the
            // focus click into a cancellation. The dedicated cancel overlay
            // remains available for an intentional cancellation.
            if (DikozemiePvpSearchController.IsJoinPending)
            {
                TraceButtonAction("arena-toggle ignored while join is resolving");
                UpdatePvpSearchLabels(matchmaking);
                return true;
            }

            if (matchmaking.SelectedSubPageIndex == 1)
            {
                int selectedIndex = GetSelectedTrainingMode(matchmaking);
                if (selectedIndex < 0)
                {
                    matchmaking.SelectionInfoText =
                        "Сначала выберите режим тренировки";
                    TraceButtonAction("arena-training start rejected: no mode selected");
                    return true;
                }

                TraceButtonAction("arena-training stance requested mode=" + selectedIndex);
                ChooseBattleStance(() =>
                {
                    TraceButtonAction("arena-training start mode=" + selectedIndex);
                    PrepareForTrainingLaunch(lobby);
                    string errorMessage;
                    bool started = selectedIndex == 2
                        ? DikozemieArenaLobbyController.StartTournament(
                            out errorMessage)
                        : DikozemieArenaLobbyController.StartTrainingBattle(
                            Math.Min(selectedIndex, 1),
                            out errorMessage);
                    if (!started)
                    {
                        matchmaking.SelectionInfoText = errorMessage;
                    }
                });
                return true;
            }

            if (DikozemieArenaMatchSelection.MenuVenue ==
                DikozemieBattleVenue.Naval)
            {
                SuppressNativeMatchmaking(lobby);
                if (DikozemiePvpSearchController.IsSearching)
                {
                    DikozemiePvpSearchController.Cancel();
                    _ownsPvpSearch = false;
                    UpdatePvpSearchLabels(matchmaking);
                    return true;
                }

                // The sailor section picks one exact ship: no rotation.
                DikozemieArenaMatchSelection.SelectVenue(DikozemieBattleVenue.Naval);
                TraceButtonAction("naval-battle online search requested");
                StartNavalSearch(lobby, matchmaking);
                UpdatePvpSearchLabels(matchmaking);
                return true;
            }

            TraceButtonAction(
                "arena-toggle custom searching=" +
                DikozemiePvpSearchController.IsSearching);

            SuppressNativeMatchmaking(lobby);
            _suppressNativePvpSearchUntil = now.AddSeconds(15);

            // The legacy arena layer and the native lobby shell can both
            // start the same controller. Cancellation must depend on the
            // controller's real state, not on which presentation layer
            // happened to start it.
            if (DikozemiePvpSearchController.IsSearching)
            {
                DikozemiePvpSearchController.Cancel();
                _ownsPvpSearch = false;
            }
            else if (IsRoadBattlePending)
            {
                // A captive's fight for freedom or a fight met on the way
                // goes exactly where it was sent: the rotation sent a captive
                // of the man-catchers to sea (2026-11-07).
                TraceButtonAction("road battle exact venue=" + DikozemieArenaMatchSelection.Venue +
                    " team=" + DikozemieArenaMatchSelection.TeamSize);
                if (!DikozemieArenaMatchSelection.TryValidateCurrent(out string roadLoadoutError))
                {
                    matchmaking.SelectionInfoText = roadLoadoutError;
                    TraceButtonAction("road battle rejected by loadout: " + roadLoadoutError);
                    ClearRoadBattle();
                }
                else
                {
                    DikozemieArenaSeriesSelection.Select(1);
                    StartPvpSearch(lobby, matchmaking);
                }
            }
            else
            {
                StartRotationBattle(lobby, matchmaking);
            }

            UpdatePvpSearchLabels(matchmaking);
            return true;
        }

        // The Arena section plays the enabled kinds in turn (customer,
        // 2026-11-04): every start takes the next one, arena or sea.
        private static void StartRotationBattle(
            MPLobbyVM lobby,
            MPMatchmakingVM matchmaking)
        {
            DikozemieBattlePlan plan = DikozemieBattleRotation.ApplyNext(
                out string rotationError);
            if (plan == null)
            {
                matchmaking.SelectionInfoText = rotationError;
                TraceButtonAction("arena rotation found no battle");
                return;
            }
            TraceButtonAction("arena rotation next=" + plan.Describe());
            if (plan.IsNaval)
            {
                StartNavalSearch(lobby, matchmaking);
                return;
            }
            // The rotation already skips what the hero's gear cannot play;
            // this catches gear changed between the preview and the click.
            if (!DikozemieArenaMatchSelection.TryValidateCurrent(out string loadoutError))
            {
                matchmaking.SelectionInfoText = loadoutError;
                TraceButtonAction(
                    "arena-start rejected by equipped loadout: " + loadoutError);
                return;
            }
            // The public Arena now has one continuous ranked queue.
            DikozemieArenaSeriesSelection.Select(1);
            StartPvpSearch(lobby, matchmaking);
        }

        internal static bool TryHandleLobbyExitButtonClick()
        {
            if (_lobby?.Menu == null)
            {
                return false;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (_exitRequestInProgress)
            {
                if (now - _lastExitButtonActionUtc <
                    TimeSpan.FromMilliseconds(750))
                {
                    TraceButtonAction("lobby-exit duplicate suppressed");
                    return true;
                }

                // ExecuteExit can open a confirmation dialog. If the player
                // cancels it, the next deliberate click must be allowed.
                _exitRequestInProgress = false;
                TraceButtonAction("lobby-exit retry after cooldown");
            }

            _exitRequestInProgress = true;
            _lastExitButtonActionUtc = now;

            // The hero overlay and the lobby header used to process the same
            // physical click independently. That issued Logout twice and
            // could leave the native login screen behind a permanent blocker.
            DikozemieHeroMenuController.Hide();
            _isArenaContext = false;
            _isHeroContext = false;
            _selectedTabIndex = 0;
            DikozemieArenaLobbyMenuController.SetMapVisible(false);
            bool customSearchActive =
                DikozemiePvpSearchController.IsSearching;
            if (customSearchActive)
            {
                DikozemiePvpSearchController.Cancel();
                _ownsPvpSearch = false;
            }

            LobbyClient gameClient = NetworkMain.GameClient;
            bool nativeSearchActive =
                _lobby.IsSearchingGame ||
                _lobby.Matchmaking?.IsFindingMatch == true;
            bool customGameReleaseRequired =
                gameClient?.Connected == true && !gameClient.AtLobby;
            TraceButtonAction(
                "lobby-exit requested once state=" +
                (gameClient?.CurrentState.ToString() ?? "none"));
            if (customSearchActive || nativeSearchActive ||
                customGameReleaseRequired)
            {
                // QuitFromCustomGame and CancelFindGame are asynchronous.
                // Executing the native logout in this same frame makes it
                // reject the click with "cancel game search first". Keep the
                // one user click pending until Diamond confirms the lobby is
                // released, then execute the normal native exit exactly once.
                SuppressNativeMatchmaking(_lobby);
                ReleaseCustomGameReservation(gameClient);
                _exitAwaitingSearchRelease = true;
                _exitReleaseRequestedAt = now;
                _nextExitReleaseAttemptAt = now.AddMilliseconds(500);
                TraceButtonAction(
                    "lobby-exit waiting for search release state=" +
                    (gameClient?.CurrentState.ToString() ?? "none"));
                return true;
            }

            BeginNativeLobbyExit(_lobby, now);
            return true;
        }

        private static bool CompletePendingLobbyExit(MPLobbyVM lobby)
        {
            if (!_exitAwaitingSearchRelease)
            {
                return false;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (DikozemiePvpSearchController.IsSearching)
            {
                DikozemiePvpSearchController.Cancel();
                _ownsPvpSearch = false;
            }

            bool nativeSearchActive =
                lobby?.IsSearchingGame == true ||
                lobby?.Matchmaking?.IsFindingMatch == true;
            if (nativeSearchActive)
            {
                SuppressNativeMatchmaking(lobby);
            }

            LobbyClient gameClient = NetworkMain.GameClient;
            if (now >= _nextExitReleaseAttemptAt)
            {
                ReleaseCustomGameReservation(gameClient);
                _nextExitReleaseAttemptAt = now.AddMilliseconds(500);
            }

            bool cancellationGraceElapsed =
                now - _exitReleaseRequestedAt >= TimeSpan.FromSeconds(1);
            bool lobbyReleased = gameClient == null ||
                !gameClient.Connected ||
                gameClient.AtLobby;
            if (!cancellationGraceElapsed || nativeSearchActive ||
                DikozemiePvpSearchController.IsSearching || !lobbyReleased)
            {
                return true;
            }

            _exitAwaitingSearchRelease = false;
            _exitReleaseRequestedAt = default;
            _nextExitReleaseAttemptAt = default;
            TraceButtonAction(
                "lobby-exit search released; executing native exit state=" +
                (gameClient?.CurrentState.ToString() ?? "none"));
            BeginNativeLobbyExit(lobby, now);
            return true;
        }

        private static void BeginNativeLobbyExit(
            MPLobbyVM lobby,
            DateTimeOffset now)
        {
            // Execute the complete native exit path exactly once. ExecuteExit
            // owns the Logout request; its async-void path normally receives
            // OnDisconnected and switches the VM to Authentication, which
            // also clears the native "Please wait" blocker. Some successful
            // disconnect replies lose that UI callback, so remember the
            // request for a bounded UI-only repair.
            _exitAwaitingNativeDisconnect = true;
            _nativeExitRequestedAt = now;
            lobby.Menu.ExecuteExit();
        }

        private static bool CompletePendingNativeLobbyExit(MPLobbyVM lobby)
        {
            if (!_exitAwaitingNativeDisconnect)
            {
                return false;
            }

            // The mod has no main menu behind the lobby: «Выход» closes the game
            // once the account is logged out, or after a short wait if the
            // service does not answer.
            DateTimeOffset now = DateTimeOffset.UtcNow;
            LobbyClient gameClient = NetworkMain.GameClient;
            bool loggedOut =
                lobby.CurrentPage == MPLobbyVM.LobbyPage.Authentication ||
                gameClient == null || !gameClient.Connected;
            if (!loggedOut &&
                now - _nativeExitRequestedAt < TimeSpan.FromSeconds(3))
            {
                return true;
            }

            _exitAwaitingNativeDisconnect = false;
            _nativeExitRequestedAt = default;
            TraceButtonAction(
                "lobby-exit closing the game loggedOut=" + loggedOut);
            MBInitialScreenBase.DoExitButtonAction();
            return true;
        }

        private static void ReleaseCustomGameReservation(
            LobbyClient gameClient)
        {
            if (gameClient?.Connected != true)
            {
                return;
            }

            try
            {
                gameClient.QuitFromCustomGame();
            }
            catch (Exception error)
            {
                TraceButtonAction(
                    "lobby-exit search release retry failed=" +
                    error.Message);
            }
        }

        internal static void PrepareForTrainingExit()
        {
            _restoreArenaAfterTrainingExit = true;
            _lastLobbyRecoveryActionUtc = default;
        }

        private static bool _restoreMapAfterRoadFight;
        private static bool _restoreAfterTownWalk;

        // A scene of the town walk is closing: through a door into the next
        // one, or back to the menu of the place.
        internal static void PrepareForTownWalkExit()
        {
            _restoreArenaAfterTrainingExit = true;
            _restoreAfterTownWalk = true;
            _lastLobbyRecoveryActionUtc = default;
        }

        internal static void PrepareForRoadFightExit()
        {
            _restoreArenaAfterTrainingExit = true;
            _restoreMapAfterRoadFight = true;
            _lastLobbyRecoveryActionUtc = default;
        }

        internal static void PrepareForNextArenaSearch()
        {
            _restoreArenaAfterTrainingExit = true;
            _pendingArenaSearch = true;
            _lastLobbyRecoveryActionUtc = default;
            TraceButtonAction("arena next battle queued after scene transition");
        }

        internal static void AbandonForApplicationShutdown()
        {
            _screen = null;
            _lobby = null;
            _isArenaContext = false;
            _isHeroContext = false;
            _restoreArenaAfterTrainingExit = false;
            _restoreAfterTownWalk = false;
            _pendingArenaSearch = false;
            _lastLobbyRecoveryActionUtc = default;
            _ownsPvpSearch = false;
            _exitRequestInProgress = false;
            _exitAwaitingSearchRelease = false;
            _exitReleaseRequestedAt = default;
            _nextExitReleaseAttemptAt = default;
            _exitAwaitingNativeDisconnect = false;
            _nativeExitRequestedAt = default;
            _suppressNativePvpSearchUntil = default;
            _lastTopTabIndex = -1;
            _lastTopTabActionUtc = default;
            _liveTabSmokeFinished = false;
            _liveTabSmokeCurrent = -1;
            _liveTabSmokeNext = 0;
            _liveTabSmokeVerifyAt = default;
            _liveExitSmokeHandled = false;
            _liveExitSmokeAt = default;
            _liveNavalSmokeHandled = false;
            _liveNavalSmokePhase = 0;
            _liveNavalSmokeNextAt = default;
            _liveNavalSmokeDiagnosticAt = default;
            UnbindArenaStartButton();
            UnbindLobbyExitButton();
            DikozemiePvpSearchController.Reset();
            DikozemieArenaLobbyController.Reset();
            DikozemieHeroMenuController.AbandonForApplicationShutdown();
            DikozemieArenaLobbyMenuController.AbandonForApplicationShutdown();
        }

        private static void ApplyLobbyLabels(MPLobbyVM lobby)
        {
            if (lobby.Menu != null)
            {
                lobby.Menu.ArmoryText = "Арсенал";
                lobby.Menu.MatchmakingText = "Создание игры";
            }
        }

        private static void ApplyMatchmakingLayout(MPLobbyVM lobby)
        {
            MPMatchmakingVM matchmaking = lobby.Matchmaking;
            if (matchmaking == null)
            {
                return;
            }

            bool isArena = _isArenaContext;
            matchmaking.HasUnofficialModulesLoaded = false;
            matchmaking.IsQuickplayGamesEnabled = true;
            matchmaking.IsCustomGamesEnabled = true;
            matchmaking.IsCustomServerListEnabled = !isArena;
            matchmaking.IsPremadeGamesEnabled = !isArena;
            matchmaking.IsCustomServerFeatureSupported = true;
            matchmaking.IsClansFeatureSupported = true;
            matchmaking.QuickPlayText = isArena ? "PvP" : "Подбор игроков";
            matchmaking.CustomGameText = isArena
                ? "PvE"
                : "Пользовательская игра";
            matchmaking.CustomServerListText = "Пользовательские серверы";
            matchmaking.TeamMatchesText = "Командные игры";

            if (isArena)
            {
                EnsureTournamentMode(matchmaking.CustomGameTypes);
                RenameModes(
                    matchmaking.CustomGameTypes,
                    "Один на один",
                    "Тренировочный бой",
                    "Турнир");
            }
        }

        private static void HandlePvpSearch(MPLobbyVM lobby)
        {
            if (!_isArenaContext || lobby?.Matchmaking == null)
            {
                return;
            }

            MPMatchmakingVM matchmaking = lobby.Matchmaking;
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (lobby.IsSearchGameRequested)
            {
                // A custom overlay click and the native button below it can
                // produce the same request one frame apart. The custom click
                // already toggled our search, so this delayed native request
                // is an echo and must only be cleared. Treating it as another
                // toggle used to start and immediately cancel matchmaking.
                bool isNativeEcho = now < _suppressNativePvpSearchUntil;
                SuppressNativeMatchmaking(lobby);
                _suppressNativePvpSearchUntil = now.AddSeconds(15);

                if (isNativeEcho)
                {
                    TraceButtonAction("arena-native-echo suppressed");
                }
                else if (DikozemieArenaMatchSelection.MenuVenue ==
                    DikozemieBattleVenue.Naval)
                {
                    if (DikozemiePvpSearchController.IsSearching)
                    {
                        TraceButtonAction("naval-native search cancel");
                        DikozemiePvpSearchController.Cancel();
                        _ownsPvpSearch = false;
                    }
                    else
                    {
                        TraceButtonAction("naval-native online search requested");
                        DikozemieArenaMatchSelection.SelectVenue(DikozemieBattleVenue.Naval);
                        StartNavalSearch(lobby, matchmaking);
                    }
                }
                else if (DikozemiePvpSearchController.IsSearching)
                {
                    TraceButtonAction("arena-toggle native cancel");
                    DikozemiePvpSearchController.Cancel();
                    _ownsPvpSearch = false;
                }
                else
                {
                    TraceButtonAction("arena-toggle native start");
                    if (!DikozemieHeroProfile.IsCreated)
                    {
                        SelectTopTab(4);
                    }
                    else
                    {
                        StartRotationBattle(lobby, matchmaking);
                    }
                }
            }

            // The native privilege check can finish a few frames after we
            // replace its request. Keep canceling only that stale Quick Play
            // request, never the custom-server polling task.
            if (now < _suppressNativePvpSearchUntil &&
                (lobby.IsSearchingGame || matchmaking.IsFindingMatch))
            {
                SuppressNativeMatchmaking(lobby);
            }

            if (_ownsPvpSearch && !DikozemiePvpSearchController.IsSearching)
            {
                _ownsPvpSearch = false;
            }

            UpdatePvpSearchLabels(matchmaking);
        }

        internal static void TraceButtonAction(string message)
        {
            try
            {
                string folder = System.IO.Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "Dikozemie",
                    "logs");
                System.IO.Directory.CreateDirectory(folder);
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(folder, "lobby-buttons.txt"),
                    DateTimeOffset.Now.ToString("O") + " " + message +
                    Environment.NewLine);
            }
            catch
            {
                // Diagnostics must never break lobby input.
            }
        }

        private static void ChooseBattleStance(Action launch)
        {
            InformationManager.ShowInquiry(
                new InquiryData(
                    "КАК ВЫСТУПИТЬ В БОЮ?",
                    "Выберите стойку перед выходом на арену. Сохранённое снаряжение героя не меняется.",
                    true,
                    true,
                    "НА КОНЕ",
                    "ПЕШКОМ",
                    () =>
                    {
                        DikozemieHeroProfile.SetBattleMounted(true);
                        launch?.Invoke();
                    },
                    () =>
                    {
                        DikozemieHeroProfile.SetBattleMounted(false);
                        launch?.Invoke();
                    }),
                true,
                true);
        }

        private static void StartPvpSearch(
            MPLobbyVM lobby,
            MPMatchmakingVM matchmaking)
        {
            LobbyClient client = GetLobbyClient();
            if (client == null || !client.Connected || !client.AtLobby)
            {
                _pendingArenaSearch = true;
                DikozemiePvpSearchController.PrepareLobbyRecovery();
                RecoverLobbyConnection(lobby, client, force: true);
                UpdatePvpSearchLabels(matchmaking);
                return;
            }

            _pendingArenaSearch = false;
            _ownsPvpSearch = DikozemiePvpSearchController.Start(client);
            UpdatePvpSearchLabels(matchmaking);
        }

        private static void StartNavalSearch(
            MPLobbyVM lobby,
            MPMatchmakingVM matchmaking)
        {
            if (!DikozemieLaunchGuard.IsNavalAvailable)
            {
                matchmaking.SelectionInfoText =
                    DikozemieLaunchGuard.NavalUnavailableText;
                TraceButtonAction("naval search blocked: War Sails not loaded");
                return;
            }
            LobbyClient client = GetLobbyClient();
            if (client == null || !client.Connected || !client.AtLobby)
            {
                matchmaking.SelectionInfoText =
                    "Сетевое лобби ещё не готово. Подождите несколько секунд";
                RecoverLobbyConnection(lobby, client, force: true);
                return;
            }
            _ownsPvpSearch = DikozemiePvpSearchController.StartNaval(client);
        }

        // The single-player War Sails battle stays as a separate choice.
        internal static void StartLocalNavalBattle()
        {
            MPLobbyVM lobby = _lobby;
            if (lobby == null)
            {
                return;
            }
            SuppressNativeMatchmaking(lobby);
            if (DikozemiePvpSearchController.IsSearching)
            {
                DikozemiePvpSearchController.Cancel();
                _ownsPvpSearch = false;
            }
            TraceButtonAction("naval-battle local War Sails requested");
            if (DikozemieArenaLobbyController.TryStartNavalBattle(
                    out string navalError))
            {
                _ownsPvpSearch = true;
            }
            else if (lobby.Matchmaking != null)
            {
                lobby.Matchmaking.SelectionInfoText = navalError;
            }
        }

        private static void ResumePendingArenaSearch(MPLobbyVM lobby)
        {
            if (!_pendingArenaSearch || !_isArenaContext ||
                DikozemiePvpSearchController.IsSearching)
            {
                return;
            }

            LobbyClient client = GetLobbyClient();
            if (client?.Connected == true && client.AtLobby)
            {
                _pendingArenaSearch = false;
                TraceButtonAction("arena lobby recovered; starting queued search venue=" +
                    DikozemieArenaMatchSelection.Venue);
                // A queued next battle keeps the venue its plan chose: the
                // rotation can move a player from the arena to the sea.
                if (DikozemieArenaMatchSelection.Venue == DikozemieBattleVenue.Naval)
                {
                    StartNavalSearch(lobby, lobby.Matchmaking);
                }
                else
                {
                    StartPvpSearch(lobby, lobby.Matchmaking);
                }
                return;
            }

            RecoverLobbyConnection(lobby, client, force: false);
        }

        private static LobbyClient GetLobbyClient()
        {
            return (Game.Current?.GameStateManager?.ActiveState as LobbyState)
                ?.LobbyClient ?? NetworkMain.GameClient;
        }

        private static void RecoverLobbyConnection(
            MPLobbyVM lobby,
            LobbyClient client,
            bool force)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (!force && now - _lastLobbyRecoveryActionUtc <
                TimeSpan.FromSeconds(3))
            {
                return;
            }
            _lastLobbyRecoveryActionUtc = now;

            TraceButtonAction(
                "arena lobby recovery state=" +
                (client?.CurrentState.ToString() ?? "none") +
                " connected=" + (client?.Connected ?? false) +
                " loggedIn=" + (client?.LoggedIn ?? false) +
                " atLobby=" + (client?.AtLobby ?? false));

            try
            {
                if (client?.LoggedIn == true)
                {
                    // Returning from a custom mission can leave the Diamond
                    // client logged in but still reserved in the finished
                    // custom game.  This idempotent call releases that stale
                    // reservation; AtLobby then becomes true without forcing
                    // the player through the login screen.
                    client.QuitFromCustomGame();
                    return;
                }

                if (lobby?.Login?.CanTryLogin == true)
                {
                    lobby.Login.ExecuteLogin();
                }
            }
            catch (Exception error)
            {
                TraceButtonAction(
                    "arena lobby recovery failed=" + error.Message);
            }
        }

        private static void UpdatePvpSearchLabels(MPMatchmakingVM matchmaking)
        {
            if (DikozemieArenaMatchSelection.MenuVenue ==
                DikozemieBattleVenue.Naval)
            {
                DikozemieNavalBattleDefinition battle =
                    DikozemieArenaMatchSelection.SelectedNavalBattle;
                matchmaking.PlayText = DikozemiePvpSearchController.IsSearching
                    ? "ОТМЕНИТЬ • " +
                      DikozemiePvpSearchController.SearchElapsedText
                    : "ВСТАТЬ МАТРОСОМ";
                matchmaking.SelectionInfoText =
                    DikozemiePvpSearchController.IsSearching
                        ? DikozemiePvpSearchController.Status
                        : battle.DisplayName + " · " +
                          battle.CrewCapacity + "×" +
                          battle.CrewCapacity + " матросов";
                return;
            }

            matchmaking.PlayText = DikozemiePvpSearchController.IsSearching
                ? "ОТМЕНИТЬ • " + DikozemiePvpSearchController.SearchElapsedText
                : "ВОЙТИ НА АРЕНУ";
            if (DikozemiePvpSearchController.IsSearching)
            {
                matchmaking.SelectionInfoText =
                    DikozemiePvpSearchController.Status;
                return;
            }
            DikozemieBattlePlan next = DikozemieBattleRotation.PreviewNext();
            matchmaking.SelectionInfoText = next == null
                ? "Нет подходящего боя: включите другие виды или смените снаряжение"
                : "Следующий бой: " + next.Describe();
        }

        private static void EnsureArenaStartButtonBound(
            MultiplayerLobbyGauntletScreen screen)
        {
            if (!_isArenaContext)
            {
                UnbindArenaStartButton();
                return;
            }

            Widget root = screen?.LobbyLayer
                ?.GetMovieIdentifier("Lobby")
                ?.Movie
                ?.RootWidget;
            if (root == null)
            {
                return;
            }

            ButtonWidget nativeFallback = null;
            ButtonWidget candidate = null;
            foreach (ButtonWidget button in
                     root.GetAllChildrenOfTypeRecursive<ButtonWidget>())
            {
                if (string.Equals(
                        button.Id,
                        "ArenaOverlayStartButton",
                        StringComparison.Ordinal))
                {
                    candidate = button;
                    break;
                }
                if (string.Equals(
                        button.Id,
                        "ArenaStartButton",
                        StringComparison.Ordinal))
                {
                    candidate = button;
                    break;
                }
                if (string.Equals(
                        button.Id,
                        "FindGameButton",
                        StringComparison.Ordinal))
                {
                    nativeFallback = button;
                }
            }
            candidate ??= nativeFallback;
            if (candidate == null)
            {
                return;
            }

            if (!ReferenceEquals(_boundArenaStartButton, candidate))
            {
                UnbindArenaStartButton();
                _boundArenaStartButton = candidate;
                _boundArenaStartButton.ClickEventHandlers.Add(
                    OnArenaStartButtonClicked);
                Console.WriteLine(
                    "DikozemieOnline: Arena start button bound: " +
                    candidate.Id + " (" + candidate.GetType().Name + ")");
            }

            // Official matchmaking is disabled for clients with custom
            // modules. Arena uses the custom-server browser, so only the
            // equipped hero loadout controls start availability. Cancellation
            // always remains possible for a running custom search.
            bool canToggle = DikozemieArenaMatchSelection.MenuVenue ==
                    DikozemieBattleVenue.Naval ||
                DikozemiePvpSearchController.IsSearching ||
                DikozemieBattleRotation.PreviewNext() != null;
            candidate.IsEnabled = canToggle;
            candidate.IsVisible = true;
            candidate.DoNotAcceptEvents = !canToggle;
        }

        private static void EnsureLobbyExitButtonBound(
            MultiplayerLobbyGauntletScreen screen)
        {
            Widget root = screen?.LobbyLayer
                ?.GetMovieIdentifier("Lobby")
                ?.Movie
                ?.RootWidget;
            if (root == null)
            {
                return;
            }

            ButtonWidget candidate = null;
            foreach (ButtonWidget button in
                     root.GetAllChildrenOfTypeRecursive<ButtonWidget>())
            {
                if (string.Equals(
                        button.Id,
                        "DikozemieExitButton",
                        StringComparison.Ordinal))
                {
                    candidate = button;
                    break;
                }
            }
            if (candidate == null)
            {
                return;
            }

            if (!ReferenceEquals(_boundLobbyExitButton, candidate))
            {
                UnbindLobbyExitButton();
                _boundLobbyExitButton = candidate;
            }
            candidate.IsEnabled = true;
            candidate.IsVisible = true;
            candidate.DoNotAcceptEvents = false;
        }

        // Mod headers (hero menu, FaceGen) mirror the native «Создание игры»
        // tab, whose visibility follows the lobby's matchmaking support.
        private static void SyncGameCreationTabVisibility(
            MultiplayerLobbyGauntletScreen screen)
        {
            Widget root = screen?.LobbyLayer
                ?.GetMovieIdentifier("Lobby")
                ?.Movie
                ?.RootWidget;
            if (root == null)
            {
                return;
            }

            if (!ReferenceEquals(root, _nativeGameCreationRoot))
            {
                _nativeGameCreationRoot = root;
                _nativeGameCreationTab = null;
                foreach (DikozemieLobbyTabButtonWidget tab in
                         root.GetAllChildrenOfTypeRecursive<
                             DikozemieLobbyTabButtonWidget>())
                {
                    if (tab.TabIndex ==
                        DikozemieLobbyTabButtonWidget.GameCreationTabIndex)
                    {
                        _nativeGameCreationTab = tab;
                        break;
                    }
                }
            }

            if (_nativeGameCreationTab != null)
            {
                DikozemieLobbyTabButtonWidget.SetGameCreationTabVisible(
                    _nativeGameCreationTab.IsVisible);
            }
        }

        private static void HandleDirectLobbyMouseInput(
            MultiplayerLobbyGauntletScreen screen)
        {
            if (screen?.LobbyLayer?.Input == null ||
                !screen.LobbyLayer.Input.IsKeyReleased(InputKey.LeftMouseButton))
            {
                return;
            }

            var rawMousePosition = screen.LobbyLayer.Input.GetMousePositionPixel();
            var mousePosition = new System.Numerics.Vector2(
                rawMousePosition.X,
                rawMousePosition.Y);

            Widget root = screen.LobbyLayer
                .GetMovieIdentifier("Lobby")
                ?.Movie
                ?.RootWidget;
            if (root != null)
            {
                DikozemieLobbyTabButtonWidget closestTab = null;
                float closestDistanceSquared = float.MaxValue;
                foreach (DikozemieLobbyTabButtonWidget tab in
                         root.GetAllChildrenOfTypeRecursive<
                             DikozemieLobbyTabButtonWidget>())
                {
                    // The arena sections keep their measured area while the
                    // strip around them is hidden on the other lobby pages.
                    if (tab.IsRecursivelyVisible() &&
                        tab.IsPointInsideMeasuredArea(mousePosition))
                    {
                        // Hover-glow widgets intentionally extend beyond a
                        // tab's visual cell, so adjacent hit rectangles can
                        // overlap.  Picking the first match made a click near
                        // the centre of Inventory reopen Hero.  Route the
                        // click to the nearest measured tab centre instead.
                        System.Numerics.Vector2 center =
                            tab.GlobalPosition + (tab.Size * 0.5f);
                        float distanceSquared =
                            System.Numerics.Vector2.DistanceSquared(
                                mousePosition,
                                center);
                        if (distanceSquared < closestDistanceSquared)
                        {
                            closestTab = tab;
                            closestDistanceSquared = distanceSquared;
                        }
                    }
                }
                if (closestTab != null)
                {
                    TryHandleTopTabClick(closestTab.TabIndex);
                    return;
                }
            }

            if (_isArenaContext &&
                _boundArenaStartButton != null &&
                _boundArenaStartButton.IsPointInsideMeasuredArea(mousePosition))
            {
                TryHandleArenaStartButtonClick();
                return;
            }

        }

        private static void OnArenaStartButtonClicked(Widget widget)
        {
            Console.WriteLine(
                "DikozemieOnline: Arena start button runtime handler received click");
            TryHandleArenaStartButtonClick();
        }

        private static void UnbindArenaStartButton()
        {
            ButtonWidget button = _boundArenaStartButton;
            if (button == null)
            {
                return;
            }

            button.ClickEventHandlers.Remove(OnArenaStartButtonClicked);
            _boundArenaStartButton = null;
        }

        private static void UnbindLobbyExitButton()
        {
            _boundLobbyExitButton = null;
        }

        private static int GetSelectedTrainingMode(MPMatchmakingVM matchmaking)
        {
            var modes = matchmaking?.CustomGameTypes;
            if (modes == null)
            {
                return -1;
            }

            for (int index = 0; index < modes.Count; index++)
            {
                if (modes[index].IsSelected)
                {
                    return index;
                }
            }
            return -1;
        }

        private static void SuppressNativeMatchmaking(MPLobbyVM lobby)
        {
            // CancelFindGame is not a harmless reset. Calling it while the
            // client is idle moves MBGameClient from AtLobby to
            // RequestingToCancelSearchBattle, so our custom search then sees
            // the lobby as unavailable and refuses to start. Only send the
            // network cancellation when a native search is actually active.
            bool nativeSearchActive =
                lobby?.IsSearchingGame == true ||
                lobby?.Matchmaking?.IsFindingMatch == true;
            if (nativeSearchActive)
            {
                NetworkMain.GameClient?.CancelFindGame();
            }
            lobby?.OnSearchBattleCanceled();
        }

        private static void EnsureTournamentMode(
            TaleWorlds.Library.MBBindingList<MPMatchmakingItemVM> modes)
        {
            if (modes == null || modes.Count != 2)
            {
                return;
            }

            modes.Add(new MPMatchmakingItemVM(MultiplayerGameType.Skirmish)
            {
                Name = "Турнир",
                IsAvailable = true,
                IsSelected = false
            });
        }

        private static void RenameModes(
            TaleWorlds.Library.MBBindingList<MPMatchmakingItemVM> modes,
            string firstName,
            string secondName,
            string thirdName)
        {
            if (modes == null)
            {
                return;
            }

            for (int index = 0; index < modes.Count; index++)
            {
                modes[index].Name = index == 0
                    ? firstName
                    : index == 1
                        ? secondName
                        : index == 2 && thirdName != null
                            ? thirdName
                            : modes[index].Name;
                modes[index].IsAvailable = index < (thirdName == null ? 2 : 3);
            }
        }

        private static void ClearModeSelection(
            TaleWorlds.Library.MBBindingList<MPMatchmakingItemVM> modes)
        {
            if (modes == null)
            {
                return;
            }

            foreach (MPMatchmakingItemVM mode in modes)
            {
                mode.IsSelected = false;
            }
        }

        private static void PrepareForTrainingLaunch(MPLobbyVM lobby)
        {
            MPMatchmakingVM matchmaking = lobby?.Matchmaking;
            if (matchmaking != null)
            {
                ClearModeSelection(matchmaking.CustomGameTypes);
            }
        }

        private static void RestoreLobbyAfterTrainingExit(MPLobbyVM lobby)
        {
            if (!_restoreArenaAfterTrainingExit)
            {
                return;
            }

            LobbyClient client = GetLobbyClient();
            if (_restoreAfterTownWalk && TaleWorlds.MountAndBlade.Mission.Current == null &&
                DikozemieTownWalk.HasDoorPending)
            {
                // Through a door: the next scene of the walk opens at once,
                // whatever the lobby connection is doing (the walk is local).
                _restoreAfterTownWalk = false;
                _restoreArenaAfterTrainingExit = false;
                _lastLobbyRecoveryActionUtc = default;
                TraceButtonAction("town walk: through the door");
                if (DikozemieTownWalk.TryContinueThroughDoor())
                {
                    return;
                }
                _restoreAfterTownWalk = true;
                _restoreArenaAfterTrainingExit = true;
            }
            if (client?.Connected == true && client.AtLobby && _restoreAfterTownWalk)
            {
                // The walk is over: the menu of the place shows what came of it.
                _restoreArenaAfterTrainingExit = false;
                _restoreAfterTownWalk = false;
                _restoreMapAfterRoadFight = false;
                _lastLobbyRecoveryActionUtc = default;
                TraceButtonAction("town walk over: back to the town menu");
                OpenMap(DikozemieTown.Here != null);
                return;
            }
            if (client?.Connected == true && client.AtLobby && _restoreMapAfterRoadFight)
            {
                // A road fight returns to the map, where its outcome waits.
                _restoreArenaAfterTrainingExit = false;
                _restoreMapAfterRoadFight = false;
                _lastLobbyRecoveryActionUtc = default;
                TraceButtonAction("road fight over: back to the map");
                OpenMap(false);
                return;
            }
            if (client?.Connected == true && client.AtLobby)
            {
                _restoreArenaAfterTrainingExit = false;
                _lastLobbyRecoveryActionUtc = default;
                _isArenaContext = true;
                _isHeroContext = false;
                _selectedTabIndex = 5;
                ClearModeSelection(lobby.Matchmaking?.CustomGameTypes);
                ActivateNativeLobbyPage(lobby, 5);
                lobby.Matchmaking?.TrySetMatchmakingSubPage(
                    MPMatchmakingVM.MatchmakingSubPages.QuickPlay);
                DikozemiePvpSearchController.ConfirmLobbyReady();
                return;
            }

            RecoverLobbyConnection(lobby, client, force: false);
        }

        private static void ShowPendingTrainingResult()
        {
            if (!DikozemieArmoryProgression.TryConsumeTrainingResult(
                    out string message,
                    out bool playerVictory))
            {
                return;
            }

            InformationManager.DisplayMessage(
                new InformationMessage(
                    message,
                    playerVictory ? Colors.Green : Colors.Red));
            DikozemieHeroMenuController.Refresh();
        }

        private static int GetNativeTabIndex(MPLobbyVM.LobbyPage page)
        {
            switch (page)
            {
                case MPLobbyVM.LobbyPage.Home:
                    return 0;
                case MPLobbyVM.LobbyPage.Armory:
                    return 1;
                case MPLobbyVM.LobbyPage.Matchmaking:
                    return 2;
                case MPLobbyVM.LobbyPage.Profile:
                    return 6;
                default:
                    return 0;
            }
        }
    }
}
