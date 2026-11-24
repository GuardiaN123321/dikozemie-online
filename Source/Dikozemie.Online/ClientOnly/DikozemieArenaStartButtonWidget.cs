using System;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.MountAndBlade.Multiplayer.ViewModelCollection.Lobby;

namespace Dikozemie.Online
{
    public sealed class DikozemieArenaStartButtonWidget : ButtonWidget
    {
        private TextWidget _labelWidget;
        private int _selectedSubPageIndex;

        public DikozemieArenaStartButtonWidget(UIContext context)
            : base(context)
        {
            Console.WriteLine("DikozemieOnline: Arena start button widget created");
        }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            bool isArena = DikozemieLobbyShellIntegration.IsArenaContext;
            if (string.Equals(
                    Id,
                    "ArenaOverlayStartButton",
                    StringComparison.Ordinal))
            {
                IsVisible = isArena;
            }
            else if (string.Equals(Id, "ArenaStartButton", StringComparison.Ordinal))
            {
                // Like the native find button: the custom server list and the
                // team games bring their own button (1.5.7 put the server
                // list's «ПРИСОЕДИНИТЬСЯ» right under this one).
                var page = (MPMatchmakingVM.MatchmakingSubPages)SelectedSubPageIndex;
                IsVisible = page != MPMatchmakingVM.MatchmakingSubPages.CustomGameList &&
                    page != MPMatchmakingVM.MatchmakingSubPages.PremadeMatchList;
            }
            if (isArena)
            {
                IsEnabled = true;
                DoNotAcceptEvents = false;
                if (LabelWidget != null)
                {
                    LabelWidget.Text = !DikozemieHeroProfile.IsCreated
                        ? "СОЗДАТЬ ГЕРОЯ"
                        : DikozemiePvpSearchController.IsSearching
                        ? "ОТМЕНИТЬ • " +
                          DikozemiePvpSearchController.SearchElapsedText
                        : "ВОЙТИ НА АРЕНУ";
                }
            }
        }

        [Editor(false)]
        public int SelectedSubPageIndex
        {
            get => _selectedSubPageIndex;
            set
            {
                if (_selectedSubPageIndex == value)
                {
                    return;
                }

                _selectedSubPageIndex = value;
                OnPropertyChanged(value, nameof(SelectedSubPageIndex));
            }
        }

        [Editor(false)]
        public TextWidget LabelWidget
        {
            get => _labelWidget;
            set
            {
                if (_labelWidget == value)
                {
                    return;
                }

                _labelWidget = value;
                OnPropertyChanged(value, nameof(LabelWidget));
            }
        }

        protected override void HandleClick()
        {
            Console.WriteLine("DikozemieOnline: Arena start button click received");
            if (DikozemieLobbyShellIntegration.TryHandleArenaStartButtonClick())
            {
                return;
            }

            base.HandleClick();
        }
    }

    internal static class DikozemieArenaSeriesSelection
    {
        private static int _battleCount = 1;

        internal static int BattleCount => _battleCount;

        internal static void Select(int battleCount)
        {
            if (battleCount == 1 || battleCount == 3)
            {
                _battleCount = battleCount;
            }
        }
    }

    public sealed class DikozemieArenaSeriesButtonWidget : ButtonWidget
    {
        private int _battleCount = 1;

        public DikozemieArenaSeriesButtonWidget(UIContext context)
            : base(context)
        {
        }

        [Editor(false)]
        public int BattleCount
        {
            get => _battleCount;
            set
            {
                if (_battleCount == value)
                {
                    return;
                }
                _battleCount = value;
                OnPropertyChanged(value, nameof(BattleCount));
            }
        }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            IsVisible = DikozemieLobbyShellIntegration.IsArenaContext;
            IsEnabled = DikozemieArenaSeriesSelection.BattleCount != BattleCount;
            DoNotAcceptEvents = !IsEnabled;
        }

        protected override void HandleClick()
        {
            DikozemieArenaSeriesSelection.Select(BattleCount);
            base.HandleClick();
        }
    }

    public sealed class DikozemieLobbyExitButtonWidget : ButtonWidget
    {
        public DikozemieLobbyExitButtonWidget(UIContext context)
            : base(context)
        {
        }

        protected override void HandleClick()
        {
            if (DikozemieLobbyShellIntegration.TryHandleLobbyExitButtonClick())
            {
                return;
            }

            base.HandleClick();
        }
    }
}
