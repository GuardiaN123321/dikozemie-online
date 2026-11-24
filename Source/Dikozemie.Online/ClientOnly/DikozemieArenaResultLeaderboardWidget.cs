using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace Dikozemie.Online
{
    /// <summary>
    /// Keeps the player's row in the middle when the result screen opens.
    /// </summary>
    public sealed class DikozemieArenaResultLeaderboardWidget : ScrollablePanel
    {
        private int _focusToken;
        private bool _resultVisible;
        private bool _focusPending;
        private int _layoutWaitFrames;

        public DikozemieArenaResultLeaderboardWidget(UIContext context)
            : base(context)
        {
        }

        [Editor(false)]
        public int FocusToken
        {
            get => _focusToken;
            set
            {
                if (_focusToken == value)
                {
                    return;
                }
                _focusToken = value;
                RequestPlayerFocus();
                OnPropertyChanged(value, nameof(FocusToken));
            }
        }

        [Editor(false)]
        public bool ResultVisible
        {
            get => _resultVisible;
            set
            {
                if (_resultVisible == value)
                {
                    return;
                }
                _resultVisible = value;
                if (value)
                {
                    RequestPlayerFocus();
                }
                OnPropertyChanged(value, nameof(ResultVisible));
            }
        }

        protected override void OnLateUpdate(float dt)
        {
            base.OnLateUpdate(dt);
            if (!_resultVisible || !_focusPending || InnerPanel == null ||
                ClipRect == null)
            {
                return;
            }

            // Gauntlet creates data-source rows before their final dimensions
            // are measured. Give layout two frames, then center the hero row.
            if (_layoutWaitFrames++ < 2)
            {
                return;
            }

            DikozemieArenaLeaderboardRowWidget playerRow =
                FindPlayerRow(InnerPanel);
            if (playerRow == null || playerRow.Size.Y <= 0f)
            {
                return;
            }

            if (VerticalScrollbar != null)
            {
                ScrollToChild(
                    playerRow,
                    new AutoScrollParameters
                    {
                        TopOffset = 12f,
                        BottomOffset = 12f,
                        VerticalScrollTarget = 0.5f,
                        InterpolationTime = 0.18f
                    });
            }
            _focusPending = false;
        }

        private void RequestPlayerFocus()
        {
            _focusPending = true;
            _layoutWaitFrames = 0;
        }

        private static DikozemieArenaLeaderboardRowWidget FindPlayerRow(
            Widget root)
        {
            if (root is DikozemieArenaLeaderboardRowWidget row && row.IsPlayer)
            {
                return row;
            }
            for (int index = 0; index < root.ChildCount; index++)
            {
                DikozemieArenaLeaderboardRowWidget found =
                    FindPlayerRow(root.GetChild(index));
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }
    }

    public sealed class DikozemieArenaLeaderboardRowWidget : Widget
    {
        private bool _isPlayer;

        public DikozemieArenaLeaderboardRowWidget(UIContext context)
            : base(context)
        {
        }

        [Editor(false)]
        public bool IsPlayer
        {
            get => _isPlayer;
            set
            {
                if (_isPlayer == value)
                {
                    return;
                }
                _isPlayer = value;
                OnPropertyChanged(value, nameof(IsPlayer));
            }
        }
    }
}
