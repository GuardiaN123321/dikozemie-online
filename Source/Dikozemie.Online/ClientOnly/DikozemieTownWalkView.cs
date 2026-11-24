using System;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.ScreenSystem;

namespace Dikozemie.Online
{
    internal sealed class DikozemieWalkOptionVM : ViewModel
    {
        private readonly Action<int> _choose;
        private readonly int _index;

        internal DikozemieWalkOptionVM(int index, DikozemieWalkChoice choice, Action<int> choose)
        {
            _index = index;
            _choose = choose;
            Number = index < 9 ? (index + 1).ToString() : string.Empty;
            Label = choice.Label ?? string.Empty;
            Detail = choice.Detail ?? string.Empty;
        }

        [DataSourceProperty] public string Number { get; }
        [DataSourceProperty] public string Label { get; }
        [DataSourceProperty] public string Detail { get; }

        public void ExecuteChoose() => _choose?.Invoke(_index);
    }

    internal sealed class DikozemieTownWalkVM : ViewModel
    {
        private string _placeName = string.Empty;
        private string _placeSub = string.Empty;
        private string _hint = string.Empty;
        private string _doorPrompt = string.Empty;
        private bool _isDoorVisible;
        private string _waypoint = string.Empty;
        private bool _isWaypointVisible;
        private string _brawlStatus = string.Empty;
        private bool _isBrawlVisible;
        private bool _isTalkOpen;
        private string _talkName = string.Empty;
        private string _talkTitle = string.Empty;
        private bool _hasTalkTitle;
        private string _talkText = string.Empty;
        private bool _isHudVisible = true;
        private MBBindingList<DikozemieWalkOptionVM> _options = new MBBindingList<DikozemieWalkOptionVM>();

        [DataSourceProperty]
        public string PlaceName { get => _placeName; set { if (value != _placeName) { _placeName = value; OnPropertyChangedWithValue(value); } } }

        [DataSourceProperty]
        public string PlaceSub { get => _placeSub; set { if (value != _placeSub) { _placeSub = value; OnPropertyChangedWithValue(value); } } }

        [DataSourceProperty]
        public string Hint { get => _hint; set { if (value != _hint) { _hint = value; OnPropertyChangedWithValue(value); } } }

        [DataSourceProperty]
        public string DoorPrompt { get => _doorPrompt; set { if (value != _doorPrompt) { _doorPrompt = value; OnPropertyChangedWithValue(value); } } }

        [DataSourceProperty]
        public bool IsDoorVisible { get => _isDoorVisible; set { if (value != _isDoorVisible) { _isDoorVisible = value; OnPropertyChangedWithValue(value); } } }

        [DataSourceProperty]
        public string Waypoint { get => _waypoint; set { if (value != _waypoint) { _waypoint = value; OnPropertyChangedWithValue(value); } } }

        [DataSourceProperty]
        public bool IsWaypointVisible { get => _isWaypointVisible; set { if (value != _isWaypointVisible) { _isWaypointVisible = value; OnPropertyChangedWithValue(value); } } }

        [DataSourceProperty]
        public string BrawlStatus { get => _brawlStatus; set { if (value != _brawlStatus) { _brawlStatus = value; OnPropertyChangedWithValue(value); } } }

        [DataSourceProperty]
        public bool IsBrawlVisible { get => _isBrawlVisible; set { if (value != _isBrawlVisible) { _isBrawlVisible = value; OnPropertyChangedWithValue(value); } } }

        [DataSourceProperty]
        public bool IsTalkOpen { get => _isTalkOpen; set { if (value != _isTalkOpen) { _isTalkOpen = value; OnPropertyChangedWithValue(value); } } }

        [DataSourceProperty]
        public string TalkName { get => _talkName; set { if (value != _talkName) { _talkName = value; OnPropertyChangedWithValue(value); } } }

        [DataSourceProperty]
        public string TalkTitle { get => _talkTitle; set { if (value != _talkTitle) { _talkTitle = value; OnPropertyChangedWithValue(value); } } }

        [DataSourceProperty]
        public bool HasTalkTitle { get => _hasTalkTitle; set { if (value != _hasTalkTitle) { _hasTalkTitle = value; OnPropertyChangedWithValue(value); } } }

        [DataSourceProperty]
        public string TalkText { get => _talkText; set { if (value != _talkText) { _talkText = value; OnPropertyChangedWithValue(value); } } }

        [DataSourceProperty]
        public bool IsHudVisible { get => _isHudVisible; set { if (value != _isHudVisible) { _isHudVisible = value; OnPropertyChangedWithValue(value); } } }

        private bool _isHintVisible = true;

        [DataSourceProperty]
        public bool IsHintVisible { get => _isHintVisible; set { if (value != _isHintVisible) { _isHintVisible = value; OnPropertyChangedWithValue(value); } } }

        [DataSourceProperty]
        public MBBindingList<DikozemieWalkOptionVM> Options { get => _options; set { if (value != _options) { _options = value; OnPropertyChangedWithValue(value); } } }
    }

    // The screen of the walk: what is around, the doors, the conversation.
    public sealed class DikozemieTownWalkView : MissionView
    {
        private DikozemieTownWalkController _controller;
        private DikozemieTownWalkVM _dataSource;
        private GauntletLayer _layer;
        private int _shownTalk = -1;
        private bool _focused;
        private bool _asking;
        private float _hudTick;

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            _controller = Mission.GetMissionBehavior<DikozemieTownWalkController>();
            _dataSource = new DikozemieTownWalkVM();
            _layer = new GauntletLayer("DikozemieTownWalk", 50);
            _layer.LoadMovie("DikozemieTownWalk", _dataSource);
            MissionScreen.AddLayer(_layer);
        }

        public override void OnMissionScreenFinalize()
        {
            base.OnMissionScreenFinalize();
            SetFocus(false);
            if (_layer != null)
            {
                MissionScreen.RemoveLayer(_layer);
                _layer = null;
            }
            _dataSource?.OnFinalize();
            _dataSource = null;
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            if (_controller == null || _dataSource == null)
            {
                return;
            }
            RefreshTalk();
            _hudTick -= dt;
            if (_hudTick <= 0f)
            {
                _hudTick = 0.25f;
                RefreshHud();
            }
            if (_controller.IsLeaving || _asking)
            {
                return;
            }
            if (_controller.IsTalking)
            {
                TickTalkKeys();
                return;
            }
            // As in the singleplayer game: a person in sight comes first, the
            // door behind him waits.
            DikozemieWalkDoor door = _controller.NearDoor;
            if (door != null && Mission.GetMissionBehavior<MissionMainAgentController>()?.InteractionComponent
                    .CurrentFocusedObject is Agent)
            {
                door = null;
            }
            _dataSource.IsDoorVisible = door != null;
            _dataSource.DoorPrompt = door?.Label ?? string.Empty;
            if (door != null && Input.IsGameKeyPressed(13))
            {
                _controller.UseDoor();
                return;
            }
            if (!_controller.InBrawl && (Input.IsKeyReleased(InputKey.Tab) || Input.IsKeyReleased(InputKey.Escape)))
            {
                AskLeave();
                return;
            }
            if (Input.IsKeyReleased(InputKey.Enter) || Input.IsKeyReleased(InputKey.NumpadEnter))
            {
                AskSay();
            }
        }

        private void RefreshHud()
        {
            DikozemieTownWalkSetup setup = _controller.Setup;
            string town = setup?.Node == null ? string.Empty : DikozemieLobbyMapWidget.DisplayNodeName(setup.Node).ToUpperInvariant();
            _dataSource.PlaceName = town;
            // Stage 5: the other heroes walking here.
            string peers = string.Join(", ", _controller.PeerNames);
            _dataSource.PlaceSub = DikozemieTownWalk.PlaceName(setup) + "  ·  " + DikozemieRoads.ClockText +
                (peers.Length > 0 ? "  ·  здесь: " + peers : string.Empty);
            bool brawl = _controller.InBrawl;
            _dataSource.IsBrawlVisible = brawl;
            _dataSource.BrawlStatus = _controller.BrawlStatus;
            DikozemieWalkDoor way = _controller.Waypoint;
            if (way != null && _controller.StepsTo(way) <= 4)
            {
                _controller.ClearWaypoint();
                way = null;
            }
            _dataSource.IsWaypointVisible = way != null && !brawl;
            _dataSource.Waypoint = way == null
                ? string.Empty
                : way.Label + "  ·  " + _controller.StepsTo(way) + " ШАГОВ  ·  " + _controller.SideOf(way).ToUpperInvariant();
            // The conversation panel says its own keys.
            _dataSource.IsHintVisible = !_controller.IsTalking;
            if (brawl)
            {
                _dataSource.Hint = "ДЕРИТЕСЬ!   ·   ВЫЙТИ ИЗ ДРАКИ НЕЛЬЗЯ";
            }
            else
            {
                string exit = setup == null || setup.Place == DikozemieWalkPlace.Streets
                    ? (setup?.IsVillage == true ? "УЙТИ ИЗ ДЕРЕВНИ" : setup?.IsCastle == true ? "УЙТИ ИЗ ЗАМКА" : "УЙТИ ИЗ ГОРОДА")
                    : "В МЕНЮ ГОРОДА";
                _dataSource.Hint = "F — ГОВОРИТЬ, ВОЙТИ   ·   ENTER — СКАЗАТЬ ВСЛУХ   ·   TAB — " + exit;
            }
            _dataSource.IsHudVisible = true;
        }

        private void RefreshTalk()
        {
            DikozemieWalkTalk talk = _controller.Talk;
            int version = _controller.TalkVersion;
            if (version == _shownTalk)
            {
                return;
            }
            _shownTalk = version;
            _dataSource.IsTalkOpen = talk != null;
            SetFocus(talk != null);
            SetConversationCamera(talk?.Agent);
            if (talk == null)
            {
                _dataSource.Options = new MBBindingList<DikozemieWalkOptionVM>();
                return;
            }
            _dataSource.IsDoorVisible = false;
            _dataSource.TalkName = (talk.Name ?? string.Empty).ToUpperInvariant();
            _dataSource.TalkTitle = talk.Title ?? string.Empty;
            _dataSource.HasTalkTitle = !string.IsNullOrEmpty(talk.Title);
            _dataSource.TalkText = talk.Text ?? string.Empty;
            var options = new MBBindingList<DikozemieWalkOptionVM>();
            for (int index = 0; index < talk.Choices.Count; index++)
            {
                options.Add(new DikozemieWalkOptionVM(index, talk.Choices[index], Choose));
            }
            _dataSource.Options = options;
        }

        private Agent _cameraOn;

        // The game's own conversation camera: it turns to the speaker's face
        // and closes in, as in the singleplayer game.
        private void SetConversationCamera(Agent speaker)
        {
            if (speaker == _cameraOn)
            {
                return;
            }
            _cameraOn = speaker;
            try
            {
                if (speaker != null && speaker.IsActive())
                {
                    Agent hero = _controller.Hero;
                    if (hero != null && hero.IsActive())
                    {
                        Vec3 toward = speaker.Position - hero.Position;
                        if (toward.AsVec2.LengthSquared > 0.01f)
                        {
                            Vec2 flat = toward.AsVec2.Normalized();
                            hero.LookDirection = flat.ToVec3();
                            hero.SetMovementDirection(flat);
                        }
                    }
                    Mission.GetMissionBehavior<MissionMainAgentController>()?.InteractionComponent
                        .SetCurrentFocusedObject(speaker, null, -1, true);
                    if (Mission.Mode != MissionMode.Conversation)
                    {
                        Mission.SetMissionMode(MissionMode.Conversation, false);
                    }
                }
                else if (Mission.Mode == MissionMode.Conversation && !_controller.InBrawl)
                {
                    Mission.SetMissionMode(MissionMode.StartUp, false);
                }
            }
            catch (Exception error)
            {
                Debug.Print("DikozemieOnline: town walk conversation camera: " + error.Message, 0, Debug.DebugColor.Yellow);
            }
        }

        private void Choose(int index)
        {
            _controller?.Choose(index);
        }

        private void TickTalkKeys()
        {
            InputContext input = _layer?.Input;
            if (input == null)
            {
                return;
            }
            DikozemieWalkTalk talk = _controller.Talk;
            if (talk == null)
            {
                return;
            }
            for (int number = 0; number < Math.Min(9, talk.Choices.Count); number++)
            {
                if (input.IsKeyReleased(InputKey.D1 + number) || input.IsKeyReleased(InputKey.Numpad1 + number))
                {
                    Choose(number);
                    return;
                }
            }
            if (input.IsKeyReleased(InputKey.Escape))
            {
                // The last choice says goodbye (or goes on after a result).
                Choose(talk.Choices.Count - 1);
            }
        }

        private void SetFocus(bool focused)
        {
            if (_layer == null || focused == _focused)
            {
                return;
            }
            _focused = focused;
            if (focused)
            {
                _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
                _layer.IsFocusLayer = true;
                ScreenManager.TrySetFocus(_layer);
            }
            else
            {
                _layer.InputRestrictions.ResetInputRestrictions();
                _layer.IsFocusLayer = false;
                ScreenManager.TryLoseFocus(_layer);
            }
        }

        // Stage 5: a line said aloud is heard by every hero in the town
        // (the tavern's talk of stage 5).
        private void AskSay()
        {
            string channel = DikozemieWorld.PlaceChannel;
            if (string.IsNullOrEmpty(channel) || InformationManager.IsAnyInquiryActive())
            {
                if (string.IsNullOrEmpty(channel))
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Нет связи с сервером мира: вас никто не услышит."));
                }
                return;
            }
            _asking = true;
            InformationManager.ShowTextInquiry(new TextInquiryData(
                "СКАЗАТЬ ВСЛУХ",
                "Услышат все герои, кто сейчас в этом городе: в таверне, на улицах и в меню города.",
                true, true, "СКАЗАТЬ", "ПРОМОЛЧАТЬ",
                text =>
                {
                    _asking = false;
                    DikozemieWorld.Say(channel, text, error =>
                    {
                        if (error != null)
                        {
                            InformationManager.DisplayMessage(new InformationMessage(error));
                        }
                    });
                },
                () => _asking = false,
                false,
                text => new Tuple<bool, string>(text != null && text.Length <= 240, "Не длиннее 240 знаков.")), true, false);
        }

        private void AskLeave()
        {
            DikozemieTownWalkSetup setup = _controller.Setup;
            bool streets = setup == null || setup.Place == DikozemieWalkPlace.Streets;
            _asking = true;
            InformationManager.ShowInquiry(
                new InquiryData(
                    streets ? "ЗАКОНЧИТЬ ПРОГУЛКУ?" : "ВЕРНУТЬСЯ В МЕНЮ ГОРОДА?",
                    streets
                        ? "Вы вернётесь в меню " + (setup?.IsVillage == true ? "деревни" : setup?.IsCastle == true ? "замка" : "города") + "."
                        : "Можно выйти через дверь — и оказаться на улице. Или сразу вернуться в меню города.",
                    true,
                    true,
                    "В МЕНЮ",
                    "ОСТАТЬСЯ",
                    () =>
                    {
                        _asking = false;
                        _controller.LeaveWalk(setup?.Place == DikozemieWalkPlace.Tavern
                            ? DikozemieWalkExit.Tavern
                            : DikozemieWalkExit.Town, string.Empty);
                    },
                    () => _asking = false),
                true,
                true);
        }
    }

    [ViewCreatorModule]
    public static class DikozemieTownWalkViews
    {
        [ViewMethod(DikozemieTownWalk.MissionName)]
        public static MissionView[] OpenTownWalk(Mission mission)
        {
            // The same views as a road fight (no options screen: it brought
            // the multiplayer client down), and the walk's own screen.
            var views = new System.Collections.Generic.List<MissionView>
            {
                new MissionMainAgentController(),
                ViewCreator.CreateMissionAgentStatusUIHandler(mission),
                ViewCreator.CreateMissionMainAgentEquipmentController(mission),
                ViewCreator.CreateMissionMainAgentEquipDropView(mission),
                new MissionItemContourControllerView(),
                new MissionAgentContourControllerView(),
                new DikozemieTownWalkView()
            };
            // «Вы покидаете местность» needs the boundary handler, which the
            // arena does not have (its boundary is the sand): without the
            // handler the view threw and the game froze (2026-11-09).
            if (DikozemieTownWalk.Active?.Place != DikozemieWalkPlace.Arena)
            {
                views.Insert(4, ViewCreator.CreateMissionBoundaryCrossingView());
            }
            return views.ToArray();
        }
    }
}
