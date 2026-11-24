using System;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Diamond;
using TaleWorlds.MountAndBlade.Multiplayer.ViewModelCollection.Lobby;
using TaleWorlds.MountAndBlade.Multiplayer.ViewModelCollection.Lobby.OfficialGame;
using TaleWorlds.ScreenSystem;

namespace Dikozemie.Online
{
    internal sealed class DikozemieArenaLobbyMenuVM : ViewModel
    {
        private readonly Action<int> _selectTopTab;
        private readonly Action _togglePvpSearch;
        private readonly Action<int> _startTrainingBattle;
        private readonly Action _startTournament;
        private readonly Action _startNavalBattle;
        private bool _isVisible;
        private bool _isArenaOpen;
        private bool _isHomeCardVisible;
        private bool _isMapOpen;
        private bool _isHeroesOpen;
        private int _selectedTopTabIndex;
        private string _statusText;
        private bool _isPvpSection;
        private bool _isTrainingSection;
        private bool _isSynchronizingTrainingSelection;
        private int _selectedTrainingVariant;
        private string _playButtonText;
        private bool _showTournamentAnnouncementOnEntry;
        private bool _isTournamentAnnouncementVisible;

        internal DikozemieArenaLobbyMenuVM(
            Action<int> selectTopTab,
            Action togglePvpSearch,
            Action<int> startTrainingBattle,
            Action startTournament,
            Action startNavalBattle)
        {
            _selectTopTab = selectTopTab;
            _togglePvpSearch = togglePvpSearch;
            _startTrainingBattle = startTrainingBattle;
            _startTournament = startTournament;
            _startNavalBattle = startNavalBattle;
            ArenaCards = new MBBindingList<MPMatchmakingItemVM>();
            TrainingCards = new MBBindingList<MPMatchmakingItemVM>();
            MPMatchmakingItemVM regularTraining = new MPMatchmakingItemVM(
                MultiplayerGameType.TeamDeathmatch)
            {
                Name = "Один на один",
                IsAvailable = true,
                IsSelected = true
            };
            MPMatchmakingItemVM trainingVariantTwo = new MPMatchmakingItemVM(
                MultiplayerGameType.Battle)
            {
                Name = "Тренировочный бой",
                IsAvailable = true,
                IsSelected = false
            };
            MPMatchmakingItemVM tournament = new MPMatchmakingItemVM(
                MultiplayerGameType.Skirmish)
            {
                Name = "Турнир",
                IsAvailable = true,
                IsSelected = false
            };
            regularTraining.OnSelectionChanged += OnTrainingSelectionChanged;
            trainingVariantTwo.OnSelectionChanged += OnTrainingSelectionChanged;
            tournament.OnSelectionChanged += OnTrainingSelectionChanged;
            TrainingCards.Add(regularTraining);
            TrainingCards.Add(trainingVariantTwo);
            TrainingCards.Add(tournament);
            _isPvpSection = true;
            _statusText = "Рейтинговая арена";
            _playButtonText = "ВОЙТИ НА АРЕНУ";
            _showTournamentAnnouncementOnEntry =
                DikozemieArenaAnnouncementStore.ShouldShow();
        }

        [DataSourceProperty]
        public MBBindingList<MPMatchmakingItemVM> ArenaCards { get; }

        [DataSourceProperty]
        public MBBindingList<MPMatchmakingItemVM> TrainingCards { get; }

        [DataSourceProperty]
        public bool IsPvpSection
        {
            get => _isPvpSection;
            set
            {
                if (_isPvpSection == value)
                {
                    return;
                }

                _isPvpSection = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool IsTrainingSection
        {
            get => _isTrainingSection;
            set
            {
                if (_isTrainingSection == value)
                {
                    return;
                }

                _isTrainingSection = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible == value)
                {
                    return;
                }

                _isVisible = value;
                OnPropertyChangedWithValue(value);
                if (!value)
                {
                    IsTournamentAnnouncementVisible = false;
                }
                else if (_showTournamentAnnouncementOnEntry)
                {
                    IsTournamentAnnouncementVisible = true;
                }
            }
        }

        [DataSourceProperty]
        public bool IsTournamentAnnouncementVisible
        {
            get => _isTournamentAnnouncementVisible;
            set
            {
                if (_isTournamentAnnouncementVisible == value)
                {
                    return;
                }

                _isTournamentAnnouncementVisible = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool IsArenaOpen
        {
            get => _isArenaOpen;
            set
            {
                if (_isArenaOpen == value)
                {
                    return;
                }

                _isArenaOpen = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool IsHomeCardVisible
        {
            get => _isHomeCardVisible;
            set
            {
                if (_isHomeCardVisible == value)
                {
                    return;
                }

                _isHomeCardVisible = value;
                OnPropertyChangedWithValue(value);
            }
        }

        public void ExecuteOpenArenaFromHome()
        {
            DikozemieLobbyShellIntegration.OpenArenaHub();
        }

        [DataSourceProperty]
        public bool IsMapOpen
        {
            get => _isMapOpen;
            set
            {
                if (_isMapOpen == value)
                {
                    return;
                }

                _isMapOpen = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool IsHeroesOpen
        {
            get => _isHeroesOpen;
            set
            {
                if (_isHeroesOpen == value)
                {
                    return;
                }

                _isHeroesOpen = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public int SelectedTopTabIndex
        {
            get => _selectedTopTabIndex;
            set
            {
                if (_selectedTopTabIndex == value)
                {
                    return;
                }

                _selectedTopTabIndex = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (string.Equals(_statusText, value, StringComparison.Ordinal))
                {
                    return;
                }

                _statusText = value ?? string.Empty;
                OnPropertyChangedWithValue(_statusText);
            }
        }

        [DataSourceProperty]
        public string PlayButtonText
        {
            get => _playButtonText;
            set
            {
                if (string.Equals(_playButtonText, value, StringComparison.Ordinal))
                {
                    return;
                }

                _playButtonText = value ?? string.Empty;
                OnPropertyChangedWithValue(_playButtonText);
            }
        }

        public void ExecuteOpenArena()
        {
            _selectTopTab?.Invoke(5);
        }

        public void ExecuteHome() => _selectTopTab?.Invoke(0);

        public void ExecuteArmory() => _selectTopTab?.Invoke(1);

        public void ExecuteHeroes() => _selectTopTab?.Invoke(4);

        public void ExecuteGameCreation() => _selectTopTab?.Invoke(2);

        public void ExecuteArena() => _selectTopTab?.Invoke(5);

        public void ExecuteProfile() => _selectTopTab?.Invoke(6);

        public void ExecuteAcknowledgeTournamentAnnouncement()
        {
            DikozemieArenaAnnouncementStore.MarkSeen();
            _showTournamentAnnouncementOnEntry = false;
            IsTournamentAnnouncementVisible = false;
        }

        public void ExecuteShowPvp()
        {
            IsPvpSection = true;
            IsTrainingSection = false;
            StatusText = "Рейтинговая арена";
            PlayButtonText = "ВОЙТИ НА АРЕНУ";
        }

        public void ExecuteShowTraining()
        {
            IsPvpSection = false;
            IsTrainingSection = true;
            StatusText = TrainingCards[_selectedTrainingVariant].Name;
            PlayButtonText = "НАЧАТЬ";
        }

        public void ExecutePlay()
        {
            if (IsTrainingSection)
            {
                if (_selectedTrainingVariant == 2)
                {
                    StatusText = "Загружаем турнир…";
                    _startTournament?.Invoke();
                }
                else
                {
                    StatusText = "Загружаем тренировочный бой…";
                    _startTrainingBattle?.Invoke(_selectedTrainingVariant);
                }
                return;
            }

            if (DikozemieArenaMatchSelection.MenuVenue ==
                DikozemieBattleVenue.Naval)
            {
                DikozemieNavalBattleDefinition battle =
                    DikozemieArenaMatchSelection.SelectedNavalBattle;
                StatusText = "Готовим " + battle.DisplayName + " к выходу…";
                _startNavalBattle?.Invoke();
                return;
            }

            StatusText = "Ищем живого игрока…";
            _togglePvpSearch?.Invoke();
        }

        internal void OpenPvpSection()
        {
            ExecuteShowPvp();
        }

        internal void FinalizeViewModel()
        {
            foreach (MPMatchmakingItemVM item in TrainingCards)
            {
                item.OnSelectionChanged -= OnTrainingSelectionChanged;
            }
            OnFinalize();
        }

        private void OnTrainingSelectionChanged(
            MPMatchmakingItemVM selectedItem,
            bool isSelected)
        {
            if (_isSynchronizingTrainingSelection || !isSelected)
            {
                return;
            }

            _isSynchronizingTrainingSelection = true;
            for (int index = 0; index < TrainingCards.Count; index++)
            {
                MPMatchmakingItemVM item = TrainingCards[index];
                bool selected = ReferenceEquals(item, selectedItem);
                item.IsSelected = selected;
                if (selected)
                {
                    _selectedTrainingVariant = index;
                }
            }
            _isSynchronizingTrainingSelection = false;
            StatusText = selectedItem.Name;
        }
    }

    internal sealed class DikozemieArenaLobbyMenuLayer : GlobalLayer, IDisposable
    {
        private MPLobbyVM _lobby;
        private readonly GauntletLayer _gauntletLayer;
        private readonly DikozemieArenaLobbyMenuVM _viewModel;
        private GauntletMovieIdentifier _mapMovie;
        private DikozemieLobbyMapWidget _mapWidget;
        private Widget _homeCard;
        private bool _mapPressed;
        private float _mapDragDistance;
        private bool _isCapturingMouse;
        private bool _isDisposed;
        private bool _isArenaContext;

        internal DikozemieArenaLobbyMenuLayer(MPLobbyVM lobby)
        {
            _lobby = lobby;
            _viewModel = new DikozemieArenaLobbyMenuVM(
                SelectTopTab,
                () => StartPvpSearch(lobby),
                StartTrainingBattle,
                StartTournament,
                StartNavalBattle);
            _gauntletLayer = new GauntletLayer("DikozemieArenaLobbyMenu", 10000);
            _mapMovie = _gauntletLayer.LoadMovie("DikozemieLobbyMap", _viewModel);
            Widget mapRoot = _mapMovie?.Movie?.RootWidget;
            _mapWidget = mapRoot as DikozemieLobbyMapWidget ??
                mapRoot?.GetAllChildrenOfTypeRecursive<DikozemieLobbyMapWidget>()
                    .FirstOrDefault();
            _homeCard = mapRoot?.FindChild("HomeArenaCard", true);
            Layer = _gauntletLayer;
        }

        internal bool IsArenaContext => _isArenaContext;

        internal void SetVisible(bool isVisible)
        {
            _viewModel.IsVisible = isVisible;
            if (!isVisible)
            {
                _isArenaContext = false;
                _viewModel.IsArenaOpen = false;
                _viewModel.IsMapOpen = false;
                _viewModel.IsHeroesOpen = false;
                SetMouseCapture(false);
            }
        }

        internal void Tick(MPLobbyVM lobby)
        {
            YieldToQuestionBoxes();
            if (_isArenaContext &&
                lobby.CurrentPage != MPLobbyVM.LobbyPage.Matchmaking)
            {
                _isArenaContext = false;
            }
            if (_viewModel.IsMapOpen &&
                lobby.CurrentPage != MPLobbyVM.LobbyPage.Home)
            {
                _viewModel.IsMapOpen = false;
            }
            if (_viewModel.IsHeroesOpen &&
                lobby.CurrentPage != MPLobbyVM.LobbyPage.Armory)
            {
                _viewModel.IsHeroesOpen = false;
            }

            SynchronizeSelectedTopTab(lobby);

            if (_isArenaContext && _viewModel.IsPvpSection)
            {
                if (DikozemieArenaMatchSelection.MenuVenue ==
                    DikozemieBattleVenue.Naval)
                {
                    DikozemieNavalBattleDefinition battle =
                        DikozemieArenaMatchSelection.SelectedNavalBattle;
                    if (DikozemieNavalBattleLauncher.IsPending)
                    {
                        _viewModel.StatusText =
                            DikozemieNavalBattleLauncher.Status;
                        _viewModel.PlayButtonText = "ЗАГРУЖАЕМ WAR SAILS…";
                    }
                    else
                    {
                        _viewModel.StatusText = battle.DisplayName + " · " +
                            battle.CrewCapacity + "×" + battle.CrewCapacity +
                            " матросов · штатная физика War Sails";
                        _viewModel.PlayButtonText = "НАЧАТЬ МОРСКОЙ БОЙ";
                    }
                }
                else
                {
                    _viewModel.StatusText = DikozemiePvpSearchController.Status;
                    _viewModel.PlayButtonText =
                        DikozemiePvpSearchController.IsSearching
                            ? "ОТМЕНИТЬ ПОИСК"
                            : "ВОЙТИ НА АРЕНУ";
                }
            }

            TickLeaderboardWheel();

            Vec2 mousePosition = _gauntletLayer.Input.GetMousePositionPixel();
            bool isOverMapPanel = HandleMapInput(mousePosition);
            SetMouseCapture(
                _viewModel.IsVisible &&
                (_viewModel.IsTournamentAnnouncementVisible ||
                 _viewModel.IsArenaOpen || isOverMapPanel));
        }

        internal void SetMapVisible(bool isVisible)
        {
            _viewModel.IsMapOpen = isVisible;
            if (!isVisible)
            {
                SetMouseCapture(false);
                _viewModel.IsHomeCardVisible = false;
            }
        }

        internal void TickMapOverlay(MPLobbyVM lobby)
        {
            // The lobby returning from a battle can hand out a new view model
            // while the screen and this layer stay.
            _lobby = lobby;
            YieldToQuestionBoxes();
            Vec2 mousePosition = _gauntletLayer.Input.GetMousePositionPixel();
            bool isOverMapPanel = HandleMapInput(mousePosition);
            UpdateHomeCard();
            bool isOverHomeCard = _viewModel.IsHomeCardVisible &&
                _homeCard != null &&
                mousePosition.X >= _homeCard.GlobalPosition.X &&
                mousePosition.X <= _homeCard.GlobalPosition.X + _homeCard.Size.X &&
                mousePosition.Y >= _homeCard.GlobalPosition.Y &&
                mousePosition.Y <= _homeCard.GlobalPosition.Y + _homeCard.Size.Y;
            SetMouseCapture(
                _viewModel.IsTournamentAnnouncementVisible || isOverMapPanel ||
                isOverHomeCard);
        }

        // This layer lies above the game's question boxes (a road event, a
        // purchase): while one is open the map takes no clicks at all, or
        // its full-screen blocker swallows them before the box sees them.
        private void YieldToQuestionBoxes()
        {
            Widget root = _mapMovie?.Movie?.RootWidget;
            if (root == null)
            {
                return;
            }
            bool asking = InformationManager.IsAnyInquiryActive();
            if (root.DoNotAcceptEvents != asking)
            {
                root.DoNotAcceptEvents = asking;
                root.DoNotPassEventsToChildren = asking;
                if (asking)
                {
                    SetMouseCapture(false);
                }
            }
        }

        private bool HandleMapInput(Vec2 mousePosition)
        {
            // A road event or any other question box sits above the map: the
            // map must not take the mouse from its buttons (2026-11-07, the
            // «ПРИНЯТЬ БОЙ» button did not react).
            bool isOverMapPanel =
                _viewModel.IsMapOpen && mousePosition.Y >= 80f &&
                !InformationManager.IsAnyInquiryActive();
            if (!isOverMapPanel || _mapWidget == null)
            {
                _mapPressed = false;
                return isOverMapPanel;
            }
            float wheel = _gauntletLayer.Input.GetDeltaMouseScroll();
            if (Math.Abs(wheel) > 0.01f)
            {
                _mapWidget.Zoom(wheel, mousePosition);
            }
            if (_gauntletLayer.Input.IsKeyPressed(InputKey.LeftMouseButton))
            {
                _mapPressed = true;
                _mapDragDistance = 0f;
            }
            if (_gauntletLayer.Input.IsKeyDown(InputKey.LeftMouseButton))
            {
                float moveX = _gauntletLayer.Input.GetMouseMoveX();
                float moveY = _gauntletLayer.Input.GetMouseMoveY();
                _mapDragDistance += Math.Abs(moveX) + Math.Abs(moveY);
                _mapWidget.Pan(moveX, moveY);
            }
            // A click selects, a drag only moves the map.
            if (_gauntletLayer.Input.IsKeyReleased(InputKey.LeftMouseButton) &&
                _mapPressed)
            {
                _mapPressed = false;
                if (_mapDragDistance < 6f)
                {
                    _mapWidget.SelectMapPoint(mousePosition);
                }
            }
            if (_gauntletLayer.Input.IsKeyReleased(InputKey.MiddleMouseButton))
            {
                _mapWidget.ResetView();
            }
            if (_gauntletLayer.Input.IsKeyReleased(InputKey.RightMouseButton))
            {
                _mapWidget.SelectMapPoint(mousePosition);
            }
            return true;
        }

        private void UpdateHomeCard()
        {
            MPLobbyVM lobby = _lobby;
            // A local scene (a road fight, the town walk) draws under this
            // layer: the card must not hang over it.
            bool visible = lobby != null &&
                TaleWorlds.MountAndBlade.Mission.Current == null &&
                lobby.IsLoggedIn &&
                lobby.CurrentPage == MPLobbyVM.LobbyPage.Home &&
                !_viewModel.IsMapOpen &&
                !DikozemieLobbyShellIntegration.IsHeroContext &&
                !DikozemieLobbyShellIntegration.IsArenaContext &&
                !DikozemieHeroMenuController.IsOpen &&
                !DikozemieHeroAppearanceEditor.IsOpen;
            if (visible != _viewModel.IsHomeCardVisible)
            {
                DikozemieLobbyShellIntegration.TraceButtonAction(
                    "home card visible=" + visible +
                    " page=" + lobby?.CurrentPage +
                    " login=" + lobby?.IsLoggedIn +
                    " map=" + _viewModel.IsMapOpen +
                    " hero=" + DikozemieLobbyShellIntegration.IsHeroContext +
                    " arena=" + DikozemieLobbyShellIntegration.IsArenaContext +
                    " menu=" + DikozemieHeroMenuController.IsOpen +
                    " editor=" + DikozemieHeroAppearanceEditor.IsOpen);
            }
            _viewModel.IsHomeCardVisible = visible;
            if (visible && lobby.Home?.News != null)
            {
                lobby.Home.News.HasValidNews = false;
            }
        }

        internal void SelectTopTab(int tabIndex)
        {
            MPLobbyVM lobby = _lobby;
            if (lobby == null)
            {
                return;
            }

            _viewModel.IsArenaOpen = false;
            _viewModel.IsMapOpen = false;
            _viewModel.IsHeroesOpen = false;
            _isArenaContext = tabIndex == 5;
            switch (tabIndex)
            {
                case 0:
                    lobby.SetPage(MPLobbyVM.LobbyPage.Home);
                    break;
                case 1:
                    lobby.SetPage(MPLobbyVM.LobbyPage.Armory);
                    break;
                case 2:
                    lobby.SetPage(MPLobbyVM.LobbyPage.Matchmaking);
                    lobby.Matchmaking?.TrySetMatchmakingSubPage(
                        MPMatchmakingVM.MatchmakingSubPages.QuickPlay);
                    break;
                case 3:
                    lobby.SetPage(MPLobbyVM.LobbyPage.Home);
                    _viewModel.IsMapOpen = true;
                    break;
                case 4:
                    lobby.SetPage(MPLobbyVM.LobbyPage.Armory);
                    _viewModel.IsHeroesOpen = true;
                    break;
                case 5:
                    lobby.SetPage(
                        MPLobbyVM.LobbyPage.Matchmaking,
                        MPMatchmakingVM.MatchmakingSubPages.QuickPlay);
                    OpenArena(lobby);
                    break;
                case 6:
                    lobby.SetPage(MPLobbyVM.LobbyPage.Profile);
                    break;
                default:
                    return;
            }

            _viewModel.SelectedTopTabIndex = tabIndex;
            DikozemieLobbyTabButtonWidget.SetSelectedTab(tabIndex);
        }

        private void SynchronizeSelectedTopTab(MPLobbyVM lobby)
        {
            if (_isArenaContext)
            {
                _viewModel.SelectedTopTabIndex = 5;
                DikozemieLobbyTabButtonWidget.SetSelectedTab(5);
                return;
            }
            if (_viewModel.IsMapOpen)
            {
                _viewModel.SelectedTopTabIndex = 3;
                DikozemieLobbyTabButtonWidget.SetSelectedTab(3);
                return;
            }
            if (_viewModel.IsHeroesOpen)
            {
                _viewModel.SelectedTopTabIndex = 4;
                DikozemieLobbyTabButtonWidget.SetSelectedTab(4);
                return;
            }

            int tabIndex;
            switch (lobby.CurrentPage)
            {
                case MPLobbyVM.LobbyPage.Home:
                    tabIndex = 0;
                    break;
                case MPLobbyVM.LobbyPage.Armory:
                    tabIndex = 1;
                    break;
                case MPLobbyVM.LobbyPage.Matchmaking:
                    tabIndex = 2;
                    break;
                case MPLobbyVM.LobbyPage.Profile:
                    tabIndex = 6;
                    break;
                default:
                    return;
            }

            _viewModel.SelectedTopTabIndex = tabIndex;
            DikozemieLobbyTabButtonWidget.SetSelectedTab(tabIndex);
        }

        private void SetMouseCapture(bool shouldCapture)
        {
            if (_isCapturingMouse == shouldCapture)
            {
                return;
            }

            _isCapturingMouse = shouldCapture;
            if (shouldCapture)
            {
                _gauntletLayer.InputRestrictions.SetInputRestrictions(
                    true,
                    InputUsageMask.Mouse);
            }
            else
            {
                _gauntletLayer.InputRestrictions.ResetInputRestrictions();
            }
        }

        private void OpenArena(MPLobbyVM lobby)
        {
            if (lobby?.Matchmaking == null)
            {
                return;
            }

            lobby.Matchmaking.HasUnofficialModulesLoaded = false;
            lobby.Matchmaking.TrySetMatchmakingSubPage(
                MPMatchmakingVM.MatchmakingSubPages.QuickPlay);
            _isArenaContext = true;
        }

        private void StartTrainingBattle(int variant)
        {
            SetMouseCapture(false);
            if (!DikozemieArenaLobbyController.StartTrainingBattle(
                    variant,
                    out string errorMessage))
            {
                _viewModel.StatusText = errorMessage;
            }
        }

        private void StartTournament()
        {
            SetMouseCapture(false);
            if (!DikozemieArenaLobbyController.StartTournament(
                    out string errorMessage))
            {
                _viewModel.StatusText = errorMessage;
            }
        }

        private void StartNavalBattle()
        {
            SetMouseCapture(false);
            if (DikozemiePvpSearchController.IsSearching)
            {
                DikozemiePvpSearchController.Cancel();
                _viewModel.StatusText = DikozemiePvpSearchController.Status;
                _viewModel.PlayButtonText = "ВСТАТЬ МАТРОСОМ";
                return;
            }
            if (!DikozemieArenaLobbyController.TryStartNavalBattle(
                    out string errorMessage))
            {
                _viewModel.StatusText = errorMessage;
                return;
            }
            _viewModel.StatusText = DikozemiePvpSearchController.Status;
            _viewModel.PlayButtonText = "ОТМЕНИТЬ ПОИСК";
        }

        private void TickLeaderboardWheel()
        {
            if (!_isArenaContext || _viewModel.IsTournamentAnnouncementVisible)
            {
                return;
            }

            float wheel = _gauntletLayer.Input.GetDeltaMouseScroll();
            if (Math.Abs(wheel) > 0.01f)
            {
                DikozemieArenaLobbyLeaderboardWidget.HandleLeaderboardWheel(
                    wheel);
            }
        }

        internal void StartTrainingBattleFromNativeMenu()
        {
            int variant = 0;
            var modes = _lobby?.Matchmaking?.CustomGameTypes;
            if (modes != null)
            {
                for (int index = 0; index < modes.Count; index++)
                {
                    if (modes[index].IsSelected)
                    {
                        variant = Math.Min(index, 1);
                        break;
                    }
                }
            }
            StartTrainingBattle(variant);
        }

        private void StartPvpSearch(MPLobbyVM lobby)
        {
            if (DikozemiePvpSearchController.IsSearching)
            {
                DikozemiePvpSearchController.Cancel();
                _viewModel.StatusText = DikozemiePvpSearchController.Status;
                _viewModel.PlayButtonText = "НАЧАТЬ БОЙ";
                return;
            }

            LobbyClient client =
                (Game.Current?.GameStateManager?.ActiveState as LobbyState)?.LobbyClient;
            if (!DikozemiePvpSearchController.Start(client))
            {
                _viewModel.StatusText = DikozemiePvpSearchController.Status;
                return;
            }

            _viewModel.StatusText = DikozemiePvpSearchController.Status;
            _viewModel.PlayButtonText = "ОТМЕНИТЬ ПОИСК";
        }

        private void OpenServerBrowser(MPLobbyVM lobby)
        {
            if (lobby?.Matchmaking == null)
            {
                return;
            }

            _viewModel.IsArenaOpen = false;
            lobby.Matchmaking.SelectedSubPageIndex = 2;
            lobby.Matchmaking.TrySetMatchmakingSubPage(
                MPMatchmakingVM.MatchmakingSubPages.CustomGameList);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            SetMouseCapture(false);
            if (_mapMovie != null)
            {
                _gauntletLayer.ReleaseMovie(_mapMovie);
                _mapMovie = null;
            }
            _mapWidget = null;

            _viewModel.FinalizeViewModel();
        }
    }

    internal static class DikozemieArenaLobbyMenuController
    {
        private static DikozemieArenaLobbyMenuLayer _layer;

        internal static bool IsArenaContext => _layer?.IsArenaContext == true;

        internal static void Show(MPLobbyVM lobby)
        {
            Hide();
            _layer = new DikozemieArenaLobbyMenuLayer(lobby);
            ScreenManager.AddGlobalLayer(_layer, false);
        }

        internal static void Tick(MPLobbyVM lobby)
        {
            _layer?.SetVisible(lobby.IsLoggedIn);
            _layer?.Tick(lobby);
        }

        internal static void SelectTopTab(int tabIndex)
        {
            _layer?.SelectTopTab(tabIndex);
        }

        internal static void SetMapVisible(bool isVisible)
        {
            _layer?.SetMapVisible(isVisible);
        }

        internal static void TickMapOverlay(MPLobbyVM lobby)
        {
            _layer?.TickMapOverlay(lobby);
        }

        internal static void StartTrainingBattleFromNativeMenu()
        {
            _layer?.StartTrainingBattleFromNativeMenu();
        }

        internal static void Hide()
        {
            DikozemieArenaLobbyMenuLayer layer = _layer;
            if (layer == null)
            {
                return;
            }

            // Clear the shared reference first: removing a global layer can
            // synchronously notify the screen stack and re-enter this method.
            _layer = null;
            // Bannerlord 1.5.7 finalizes a removed global layer, so the movie
            // is released first (the native views do the same).
            try
            {
                layer.Dispose();
            }
            finally
            {
                ScreenManager.RemoveGlobalLayer(layer);
            }
        }

        internal static void AbandonForApplicationShutdown()
        {
            // Bannerlord's Final Cleanup already owns the registered layer.
            // Dropping only our reference prevents a second removal/disposal.
            _layer = null;
        }
    }
}
