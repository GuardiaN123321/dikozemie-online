using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets;

namespace Dikozemie.Online
{
    public sealed class DikozemieLobbyTabButtonWidget : ToggleStateButtonWidget
    {
        internal const int GameCreationTabIndex = 2;
        internal const int ArenaTabIndex = 5;
        internal const int MapSectionIndex = 3;
        internal const int HeroSectionIndex = 4;
        internal const int InventorySectionIndex = 7;
        // The two venues of the arena page, shown as sections of the Arena tab.
        internal const int NavalSectionIndex = 8;
        internal const int ArenaSectionIndex = 9;
        internal const string NativeGameCreationId = "GameCreationButton";

        private static int _selectedTabIndex;
        private static bool _gameCreationTabVisible = true;
        private int _tabIndex;

        public DikozemieLobbyTabButtonWidget(UIContext context)
            : base(context)
        {
        }

        [Editor(false)]
        public int TabIndex
        {
            get => _tabIndex;
            set
            {
                if (_tabIndex == value)
                {
                    return;
                }

                _tabIndex = value;
                OnPropertyChanged(value, nameof(TabIndex));
            }
        }

        internal static void SetSelectedTab(int tabIndex)
        {
            _selectedTabIndex = tabIndex;
        }

        internal static int SelectedTabIndex => _selectedTabIndex;

        internal static void SetGameCreationTabVisible(bool visible)
        {
            _gameCreationTabVisible = visible;
        }

        private bool IsCurrentTab()
        {
            switch (TabIndex)
            {
                case ArenaTabIndex:
                    return _selectedTabIndex == ArenaTabIndex ||
                        _selectedTabIndex == MapSectionIndex ||
                        _selectedTabIndex == HeroSectionIndex ||
                        _selectedTabIndex == InventorySectionIndex;
                case ArenaSectionIndex:
                    return _selectedTabIndex == ArenaTabIndex &&
                        DikozemieArenaMatchSelection.MenuVenue ==
                            DikozemieBattleVenue.Arena;
                case NavalSectionIndex:
                    return _selectedTabIndex == ArenaTabIndex &&
                        DikozemieArenaMatchSelection.MenuVenue ==
                            DikozemieBattleVenue.Naval;
                default:
                    return TabIndex == _selectedTabIndex;
            }
        }

        // The Arena tab stays open before the hero exists: it leads to creation.
        private bool RequiresCreatedHero()
        {
            return TabIndex == 3 ||
                TabIndex == InventorySectionIndex ||
                TabIndex == NavalSectionIndex ||
                TabIndex == ArenaSectionIndex;
        }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            IsSelected = IsCurrentTab();
            IsEnabled = !RequiresCreatedHero() || DikozemieHeroProfile.IsCreated;
            // The native header hides «Создание игры» while the TaleWorlds
            // lobby offers no matchmaking. The hero menu and FaceGen draw
            // their own copy of the header over it; with the extra tab still
            // shown there, every label sat between two native ones and a
            // click on the next tab opened native matchmaking (2026-10-29).
            if (TabIndex == GameCreationTabIndex &&
                !string.Equals(Id, NativeGameCreationId, System.StringComparison.Ordinal))
            {
                IsVisible = _gameCreationTabVisible;
            }
        }

        protected override void HandleClick()
        {
            if (RequiresCreatedHero() && !DikozemieHeroProfile.IsCreated)
            {
                return;
            }
            base.HandleClick();
            if (DikozemieHeroAppearanceEditor.IsOpen)
            {
                DikozemieHeroAppearanceEditor.RequestNavigateToTab(TabIndex);
                return;
            }
            DikozemieLobbyShellIntegration.TryHandleTopTabClick(TabIndex);
        }
    }
}
