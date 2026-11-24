using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace Dikozemie.Online
{
    /// <summary>Shows who is shouting: a label above the hero's head.</summary>
    public sealed class DikozemieBattleCryView : MissionView
    {
        private const float MarkerSeconds = 1.4f;
        private const float HeadClearance = 0.45f;

        private readonly Dictionary<Agent, DikozemieBattleCryMarkerVM> _markers =
            new Dictionary<Agent, DikozemieBattleCryMarkerVM>();
        private DikozemieBattleCryOverlayVM _dataSource;
        private GauntletLayer _layer;

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            _dataSource = new DikozemieBattleCryOverlayVM();
            _layer = new GauntletLayer("DikozemieBattleCry", 1);
            _layer.LoadMovie("DikozemieBattleCry", _dataSource);
            MissionScreen.AddLayer(_layer);
            DikozemieBattleCryBehavior.CryReceived += OnCryReceived;
        }

        public override void OnMissionScreenFinalize()
        {
            DikozemieBattleCryBehavior.CryReceived -= OnCryReceived;
            MissionScreen.RemoveLayer(_layer);
            _layer = null;
            _dataSource?.OnFinalize();
            _dataSource = null;
            base.OnMissionScreenFinalize();
        }

        private void OnCryReceived(Agent agent, DikozemieBattleCryLevel level)
        {
            if (!_markers.TryGetValue(agent, out DikozemieBattleCryMarkerVM marker))
            {
                marker = new DikozemieBattleCryMarkerVM();
                _markers[agent] = marker;
                _dataSource.Markers.Add(marker);
            }
            marker.Show(level, MarkerSeconds);
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            foreach (KeyValuePair<Agent, DikozemieBattleCryMarkerVM> pair in
                     _markers.ToArray())
            {
                DikozemieBattleCryMarkerVM marker = pair.Value;
                if (pair.Key.State != AgentState.Active || !marker.Tick(dt))
                {
                    _dataSource.Markers.Remove(marker);
                    _markers.Remove(pair.Key);
                    continue;
                }
                Vec3 head = pair.Key.GetEyeGlobalPosition();
                head.z += HeadClearance;
                float x = 0f;
                float y = 0f;
                float w = 0f;
                MBWindowManager.WorldToScreenInsideUsableArea(
                    MissionScreen.CombatCamera,
                    head,
                    ref x,
                    ref y,
                    ref w);
                marker.ScreenX = x;
                marker.ScreenY = y;
                marker.IsOnScreen = w > 0f;
            }
        }
    }

    public sealed class DikozemieBattleCryOverlayVM : ViewModel
    {
        private MBBindingList<DikozemieBattleCryMarkerVM> _markers =
            new MBBindingList<DikozemieBattleCryMarkerVM>();

        [DataSourceProperty]
        public MBBindingList<DikozemieBattleCryMarkerVM> Markers
        {
            get => _markers;
            set
            {
                if (value != _markers)
                {
                    _markers = value;
                    OnPropertyChangedWithValue(value, nameof(Markers));
                }
            }
        }
    }

    public sealed class DikozemieBattleCryMarkerVM : ViewModel
    {
        private string _text = string.Empty;
        private bool _isBerserk;
        private bool _isOnScreen;
        private float _screenX;
        private float _screenY;
        private float _remaining;

        internal void Show(DikozemieBattleCryLevel level, float seconds)
        {
            _remaining = seconds;
            IsBerserk = level == DikozemieBattleCryLevel.Berserk;
            Text = level == DikozemieBattleCryLevel.Berserk
                ? "БЕРСЕРК!"
                : level == DikozemieBattleCryLevel.Roar
                    ? "РЁВ!"
                    : "КРИК!";
        }

        internal bool Tick(float dt)
        {
            _remaining -= dt;
            return _remaining > 0f;
        }

        [DataSourceProperty]
        public string Text
        {
            get => _text;
            set
            {
                if (value != _text)
                {
                    _text = value;
                    OnPropertyChangedWithValue(value, nameof(Text));
                }
            }
        }

        [DataSourceProperty]
        public bool IsBerserk
        {
            get => _isBerserk;
            set
            {
                if (value != _isBerserk)
                {
                    _isBerserk = value;
                    OnPropertyChangedWithValue(value, nameof(IsBerserk));
                }
            }
        }

        [DataSourceProperty]
        public bool IsOnScreen
        {
            get => _isOnScreen;
            set
            {
                if (value != _isOnScreen)
                {
                    _isOnScreen = value;
                    OnPropertyChangedWithValue(value, nameof(IsOnScreen));
                }
            }
        }

        [DataSourceProperty]
        public float ScreenX
        {
            get => _screenX;
            set
            {
                if (value != _screenX)
                {
                    _screenX = value;
                    OnPropertyChangedWithValue(value, nameof(ScreenX));
                }
            }
        }

        [DataSourceProperty]
        public float ScreenY
        {
            get => _screenY;
            set
            {
                if (value != _screenY)
                {
                    _screenY = value;
                    OnPropertyChangedWithValue(value, nameof(ScreenY));
                }
            }
        }
    }

    public sealed class DikozemieBattleCryMarkerWidget : TextWidget
    {
        private static readonly Color CryColor = new Color(0.996f, 0.757f, 0.341f, 1f);
        private static readonly Color BerserkColor = new Color(0.95f, 0.22f, 0.15f, 1f);

        public DikozemieBattleCryMarkerWidget(UIContext context)
            : base(context)
        {
        }

        [Editor(false)]
        public float ScreenX { get; set; }

        [Editor(false)]
        public float ScreenY { get; set; }

        [Editor(false)]
        public bool IsBerserk { get; set; }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            ScaledPositionXOffset = ScreenX - (Size.X * 0.5f);
            ScaledPositionYOffset = ScreenY - Size.Y;
            Brush.FontColor = IsBerserk ? BerserkColor : CryColor;
        }
    }
}
