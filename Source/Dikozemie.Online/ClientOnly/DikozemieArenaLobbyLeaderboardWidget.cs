using System;
using System.Collections.Generic;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace Dikozemie.Online
{
    /// <summary>
    /// Presents the Arena as a native tournament ledger: battle selection on
    /// the left, the persistent rating table on the right and the normal lobby
    /// navigation above it.  The widget lives inside the native matchmaking
    /// movie so it cannot steal the top navigation from the lobby screen.
    /// </summary>
    public sealed class DikozemieArenaLobbyLeaderboardWidget : Widget
    {
        private const int PageSize = 13;
        private const float RefreshInterval = 1f;
        private const string TitleFont = "Galahad";
        private const string BodyFont = "FiraSansExtraCondensed-Regular";

        // The native lobby palette (see MPLobby.Matchmaking.* brushes).
        private static readonly Color GoldText = new Color(0.996f, 0.757f, 0.341f, 1f);
        private static readonly Color AmberText = new Color(0.878f, 0.620f, 0.282f, 1f);
        private static readonly Color BodyText = new Color(0.953f, 0.878f, 0.749f, 1f);
        private static readonly Color MutedText = new Color(0.631f, 0.502f, 0.318f, 1f);
        private static readonly Color DisabledText = new Color(0.37f, 0.33f, 0.28f, 1f);
        private static readonly Color AmberLine = new Color(0.878f, 0.620f, 0.282f, 0.55f);
        private static readonly Color SectionFill = new Color(0f, 0f, 0f, 0.62f);
        private static readonly Color RowEven = new Color(1f, 0.85f, 0.6f, 0.05f);
        private static readonly Color RowOdd = new Color(0f, 0f, 0f, 0.18f);
        private static readonly Color RowPlayer = new Color(0.878f, 0.620f, 0.282f, 0.34f);
        private static readonly Color ButtonFrame = new Color(0.43f, 0.30f, 0.16f, 1f);
        private static readonly Color ButtonFill = new Color(0.07f, 0.05f, 0.03f, 1f);
        private static readonly Color ButtonSelected = new Color(0.42f, 0.27f, 0.09f, 1f);
        private static readonly Color ButtonDisabled = new Color(0.05f, 0.04f, 0.03f, 1f);
        private static readonly Color ButtonHover = new Color(0.878f, 0.620f, 0.282f, 0.16f);
        private static readonly string[] NativeMatchmakingParts =
        {
            "NativeLeftMenu",
            "CenterPanelParent",
            "NativeGameDetails"
        };

        private sealed class RowWidgets
        {
            public Widget Background;
            public TextWidget Rank;
            public TextWidget Name;
            public TextWidget Rating;
            public TextWidget Wins;
            public TextWidget Losses;
        }

        private sealed class ChoiceWidgets
        {
            public ButtonWidget Button;
            public Widget Background;
            public TextWidget Label;
            public int Value;
            // Rotation switches draw a check box on the left.
            public Widget BoxFill;
        }

        private readonly List<ChoiceWidgets> _rotationSizeToggles =
            new List<ChoiceWidgets>();
        private readonly List<ChoiceWidgets> _rotationStyleToggles =
            new List<ChoiceWidgets>();
        private readonly List<ChoiceWidgets> _rotationMixToggles =
            new List<ChoiceWidgets>();
        private readonly List<ChoiceWidgets> _rotationShipToggles =
            new List<ChoiceWidgets>();
        private TextWidget _rotationNotice;
        private float _rotationNoticeSeconds;
        private static readonly Color BoxFrame = new Color(0.878f, 0.620f, 0.282f, 0.9f);
        private static readonly Color BoxOn = new Color(0.996f, 0.757f, 0.341f, 1f);
        private static readonly Color BoxOff = new Color(0.03f, 0.02f, 0.01f, 1f);

        private readonly List<RowWidgets> _rows = new List<RowWidgets>();
        private readonly List<ChoiceWidgets> _teamSizeButtons =
            new List<ChoiceWidgets>();
        private readonly List<ChoiceWidgets> _disciplineButtons =
            new List<ChoiceWidgets>();
        private readonly List<ChoiceWidgets> _compositionButtons =
            new List<ChoiceWidgets>();
        private readonly List<ChoiceWidgets> _navalClassButtons =
            new List<ChoiceWidgets>();
        private readonly List<KeyValuePair<ButtonWidget, Widget>> _hoverables =
            new List<KeyValuePair<ButtonWidget, Widget>>();
        private static DikozemieArenaLobbyLeaderboardWidget _activeWidget;
        private Widget _arenaSelectionPanel;
        private Widget _navalSelectionPanel;
        private TextureWidget _arenaBackdrop;
        private TextureWidget _navalBackdrop;
        private TextWidget _title;
        private TextWidget _tableTitle;
        private TextWidget _navalName;
        private TextWidget _navalDetails;
        private TextWidget _navalIndex;
        private TextWidget _summary;
        private TextWidget _selectionSummary;
        private TextWidget _loadoutHint;
        private TextWidget _pageText;
        private ButtonWidget _previousButton;
        private ButtonWidget _nextButton;
        private ButtonWidget _mineButton;
        private Widget _tablePanel;
        private Widget _tableLine;
        private readonly List<TextWidget> _tableHeaders = new List<TextWidget>();
        private bool _tableNarrow;
        private bool _tableLaidOut;
        private int _firstEntry;
        private bool _centeredOnPlayer = true;
        private float _refreshElapsed;
        private bool _built;
        private bool _wasArenaVisible;

        public DikozemieArenaLobbyLeaderboardWidget(UIContext context)
            : base(context)
        {
            ClipContents = true;
        }

        protected override void OnConnectedToRoot()
        {
            base.OnConnectedToRoot();
            _activeWidget = this;
            BuildContent();
        }

        protected override void OnDisconnectedFromRoot()
        {
            if (ReferenceEquals(_activeWidget, this))
            {
                _activeWidget = null;
            }
            base.OnDisconnectedFromRoot();
        }

        internal static void HandleLeaderboardWheel(float wheelDelta)
        {
            if (Math.Abs(wheelDelta) <= 0.01f)
            {
                return;
            }

            _activeWidget?.ChangePageByRows(wheelDelta > 0f ? -3 : 3);
        }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            bool isArenaVisible = DikozemieLobbyShellIntegration.IsArenaContext;
            IsVisible = isArenaVisible;
            SyncSiblings(isArenaVisible);
            if (!isArenaVisible)
            {
                _wasArenaVisible = false;
                return;
            }

            if (!_built)
            {
                BuildContent();
            }

            LayoutTable(DikozemieLobbyShellIntegration.IsFriendsListOpen);
            foreach (KeyValuePair<ButtonWidget, Widget> hoverable in _hoverables)
            {
                hoverable.Value.IsVisible =
                    hoverable.Key.IsHovered && hoverable.Key.IsEnabled;
            }

            if (_rotationNoticeSeconds > 0f)
            {
                _rotationNoticeSeconds -= Math.Max(0f, dt);
                if (_rotationNoticeSeconds <= 0f && _rotationNotice != null)
                {
                    _rotationNotice.Text =
                        "НАЖМИТЕ НА ВИД БОЯ, ЧТОБЫ ВКЛЮЧИТЬ ИЛИ ВЫКЛЮЧИТЬ ЕГО\n" +
                        "БОИ ИДУТ ПО КРУГУ: ПОСЛЕ ТРЁХ БОЁВ НА АРЕНЕ — МОРСКОЙ";
                    _rotationNotice.Brush.FontColor = MutedText;
                }
            }
            _refreshElapsed += Math.Max(0f, dt);
            if (!_wasArenaVisible)
            {
                DikozemieArenaLeaderboardTransport.EnsureClientCatalogSeed();
                MoveToPlayer();
                RefreshRows();
                _refreshElapsed = 0f;
                _wasArenaVisible = true;
            }
            else if (_refreshElapsed >= RefreshInterval)
            {
                RefreshRows();
                _refreshElapsed = 0f;
            }
        }

        // The arena shows the native lobby background, so the mode cards of
        // «Создание игры» must be gone rather than painted over.
        private void SyncSiblings(bool isArenaVisible)
        {
            if (ParentWidget == null)
            {
                return;
            }
            foreach (string id in NativeMatchmakingParts)
            {
                Widget part = ParentWidget.FindChild(id);
                if (part != null)
                {
                    part.IsVisible = !isArenaVisible;
                }
            }
            Widget sections = ParentWidget.FindChild("ArenaSubTabs");
            if (sections != null)
            {
                sections.IsVisible = isArenaVisible;
            }
        }

        private void BuildContent()
        {
            if (_built || Context == null)
            {
                return;
            }

            _built = true;
            // Consume events before they can reach the hidden native mode
            // cards.  The top lobby bar is outside this matchmaking widget.
            AddChild(new ButtonWidget(Context)
            {
                Id = "ArenaLeaderboardEventBlocker",
                WidthSizePolicy = SizePolicy.StretchToParent,
                HeightSizePolicy = SizePolicy.StretchToParent,
                DoNotPassEventsToChildren = true
            });

            // The native information panel, on the native lobby background.
            Widget panel = new Widget(Context)
            {
                Id = "ArenaLeaderboardPanel",
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                SuggestedWidth = 1880f,
                SuggestedHeight = 716f,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                MarginTop = 64f,
                Sprite = Context.SpriteData.GetSprite("information_panel_body_9"),
                Color = new Color(0f, 0f, 0f, 0.82f)
            };
            AddChild(panel);

            _arenaBackdrop = new TextureWidget(Context)
            {
                Id = "ArenaMenuBackdrop",
                WidthSizePolicy = SizePolicy.StretchToParent,
                HeightSizePolicy = SizePolicy.StretchToParent,
                TextureProviderName = "DikozemieArenaMenuTextureProvider",
                AlphaFactor = 0.10f,
                DoNotAcceptEvents = true
            };
            panel.AddChild(_arenaBackdrop);
            _navalBackdrop = new TextureWidget(Context)
            {
                Id = "NavalMenuBackdrop",
                WidthSizePolicy = SizePolicy.StretchToParent,
                HeightSizePolicy = SizePolicy.StretchToParent,
                TextureProviderName = "DikozemieNavalMenuTextureProvider",
                AlphaFactor = 0.14f,
                IsVisible = false,
                DoNotAcceptEvents = true
            };
            panel.AddChild(_navalBackdrop);
            // The texture provider ignores AlphaFactor, so dim the art here.
            panel.AddChild(CreateSolid(
                "ArenaMenuBackdropShade",
                new Color(0f, 0f, 0f, 0.55f)));

            Widget headerOrnament = CreateSprite(
                "ArenaLeaderboardHeaderOrnament",
                "MPLobby\\Matchmaking\\information_panel_header");
            headerOrnament.WidthSizePolicy = SizePolicy.StretchToParent;
            headerOrnament.HeightSizePolicy = SizePolicy.Fixed;
            headerOrnament.SuggestedHeight = 14f;
            panel.AddChild(headerOrnament);

            _title = AddLabel(
                panel,
                "ArenaLeaderboardTitle",
                "АРЕНА ДИКОЗЕМЬЯ",
                440f,
                10f,
                1000f,
                46f,
                38,
                GoldText);
            _summary = AddLabel(
                panel,
                "ArenaLeaderboardSummary",
                string.Empty,
                40f,
                54f,
                1800f,
                30f,
                20,
                BodyText,
                TextHorizontalAlignment.Center,
                BodyFont);
            AddAmberLine(panel, "ArenaLeaderboardHeaderLine", 24f, 88f, 1832f);

            BuildArenaSelection(panel);
            BuildNavalSelection(panel);
            BuildTable(panel);
        }

        private void BuildArenaSelection(Widget panel)
        {
            _arenaSelectionPanel = AddSection(
                panel,
                "ArenaBattleSelectionPanel",
                24f,
                100f,
                545f,
                604f);
            Widget section = _arenaSelectionPanel;

            // Every kind starts switched on; a click switches it off or on.
            // «Войти на арену» plays the enabled kinds in turn (customer,
            // 2026-11-04: «по умолчанию все включены... чекбоксами игрок может
            // отключить режимы» and battle types that keep rotating).
            AddLabel(
                section,
                "ArenaBattleSelectionTitle",
                "ВИДЫ БОЁВ В ПОДБОРЕ",
                20f,
                6f,
                505f,
                44f,
                28,
                AmberText);
            AddAmberLine(section, "ArenaBattleSelectionLine", 20f, 50f, 505f);

            AddCaption(section, "ArenaTeamSizeLabel", "ЧИСЛО БОЙЦОВ", 54f);
            int[] sizeValues = DikozemieBattleRotation.TeamSizes;
            for (int index = 0; index < sizeValues.Length; index++)
            {
                int teamSize = sizeValues[index];
                _rotationSizeToggles.Add(AddToggleButton(
                    section,
                    "ArenaRotationSize" + teamSize,
                    teamSize + "×" + teamSize,
                    20f + ((index % 4) * 127f),
                    82f + ((index / 4) * 40f),
                    124f,
                    teamSize,
                    () => ApplyToggle(DikozemieBattleRotation.ToggleSize(
                        teamSize, out string refusal), refusal)));
            }

            AddCaption(section, "ArenaDisciplineLabel", "СТИЛЬ БОЯ", 242f);
            string[] disciplineLabels = { "ПЕШИЙ", "КОННЫЙ", "ОТКРЫТЫЙ" };
            for (int index = 0; index < disciplineLabels.Length; index++)
            {
                var discipline = (DikozemieArenaDiscipline)index;
                _rotationStyleToggles.Add(AddToggleButton(
                    section,
                    "ArenaRotationStyle" + index,
                    disciplineLabels[index],
                    20f + (index * 170f),
                    270f,
                    165f,
                    index,
                    () => ApplyToggle(DikozemieBattleRotation.ToggleDiscipline(
                        discipline, out string refusal), refusal)));
            }

            // Throwing weapons count as melee kit: a javelin hero still gets
            // «ближний бой», as the old lobby explained under the buttons.
            AddCaption(
                section,
                "ArenaCompositionLabel",
                "СОСТАВ ВОЙСК · МЕТАТЕЛЬНОЕ ОРУЖИЕ РАЗРЕШЕНО",
                312f);
            string[] compositionLabels = { "СМЕШАННЫЙ", "БЛИЖНИЙ БОЙ", "ДАЛЬНИЙ БОЙ" };
            for (int index = 0; index < compositionLabels.Length; index++)
            {
                var composition = (DikozemieArenaComposition)index;
                _rotationMixToggles.Add(AddToggleButton(
                    section,
                    "ArenaRotationMix" + index,
                    compositionLabels[index],
                    20f + (index * 170f),
                    340f,
                    165f,
                    index,
                    () => ApplyToggle(DikozemieBattleRotation.ToggleComposition(
                        composition, out string refusal), refusal)));
            }

            AddCaption(section, "ArenaShipLabel", "МОРСКИЕ БОИ", 382f);
            string[] shipLabels = { "ЛЁГКИЙ", "СРЕДНИЙ", "ТЯЖЁЛЫЙ" };
            for (int index = 0; index < shipLabels.Length; index++)
            {
                var shipClass = (DikozemieNavalShipClass)index;
                _rotationShipToggles.Add(AddToggleButton(
                    section,
                    "ArenaRotationShip" + index,
                    shipLabels[index],
                    20f + (index * 170f),
                    410f,
                    165f,
                    index,
                    () => ApplyToggle(DikozemieBattleRotation.ToggleShip(
                        shipClass, out string refusal), refusal)));
            }

            AddAmberLine(section, "ArenaSelectionLine", 20f, 456f, 505f);
            _selectionSummary = AddLabel(
                section,
                "ArenaSelectionSummary",
                string.Empty,
                20f,
                462f,
                505f,
                56f,
                21,
                GoldText);
            _loadoutHint = AddLabel(
                section,
                "ArenaLoadoutHint",
                string.Empty,
                20f,
                518f,
                505f,
                40f,
                15,
                MutedText,
                TextHorizontalAlignment.Center,
                BodyFont);
            _rotationNotice = AddLabel(
                section,
                "ArenaRotationNotice",
                "НАЖМИТЕ НА ВИД БОЯ, ЧТОБЫ ВКЛЮЧИТЬ ИЛИ ВЫКЛЮЧИТЬ ЕГО\n" +
                "БОИ ИДУТ ПО КРУГУ: ПОСЛЕ ТРЁХ БОЁВ НА АРЕНЕ — МОРСКОЙ",
                20f,
                558f,
                505f,
                42f,
                15,
                MutedText,
                TextHorizontalAlignment.Center,
                BodyFont);
        }

        private void ApplyToggle(bool changed, string refusal)
        {
            if (!changed && !string.IsNullOrEmpty(refusal))
            {
                _rotationNotice.Text = refusal.ToUpperInvariant();
                _rotationNotice.Brush.FontColor = new Color(0.94f, 0.38f, 0.27f, 1f);
                _rotationNoticeSeconds = 3f;
            }
            RefreshRows();
        }

        // A switch: frame, fill, a check box on the left and the label.
        private ChoiceWidgets AddToggleButton(
            Widget panel,
            string id,
            string text,
            float x,
            float y,
            float width,
            int value,
            Action toggle)
        {
            ButtonWidget button = CreateFramedButton(
                id,
                x,
                y,
                width,
                36f,
                out Widget background);
            Widget box = CreateSolid(id + "Box", BoxFrame);
            box.WidthSizePolicy = SizePolicy.Fixed;
            box.HeightSizePolicy = SizePolicy.Fixed;
            box.SuggestedWidth = 16f;
            box.SuggestedHeight = 16f;
            box.HorizontalAlignment = HorizontalAlignment.Left;
            box.VerticalAlignment = VerticalAlignment.Center;
            box.MarginLeft = 10f;
            button.AddChild(box);
            Widget boxFill = CreateSolid(id + "BoxFill", BoxOn);
            boxFill.MarginLeft = 2f;
            boxFill.MarginRight = 2f;
            boxFill.MarginTop = 2f;
            boxFill.MarginBottom = 2f;
            box.AddChild(boxFill);
            TextWidget label = CreateText(id + "Text", text, 17, BodyText);
            label.WidthSizePolicy = SizePolicy.StretchToParent;
            label.HeightSizePolicy = SizePolicy.StretchToParent;
            label.MarginLeft = 22f;
            button.AddChild(label);
            button.ClickEventHandlers.Add(_ => toggle?.Invoke());
            panel.AddChild(button);
            return new ChoiceWidgets
            {
                Button = button,
                Background = background,
                Label = label,
                Value = value,
                BoxFill = boxFill
            };
        }

        // On: amber fill and a filled box. Playable but skipped for the
        // hero's gear (a horse missing for «конный»): dimmed label.
        private static void ApplyToggleState(
            ChoiceWidgets toggle,
            bool enabled,
            bool usable)
        {
            toggle.Button.IsEnabled = true;
            toggle.Button.DoNotAcceptEvents = false;
            toggle.Background.Color = enabled ? ButtonSelected : ButtonFill;
            toggle.BoxFill.Color = enabled ? BoxOn : BoxOff;
            toggle.Label.Brush.FontColor = !usable
                ? DisabledText
                : enabled
                    ? GoldText
                    : BodyText;
        }

        private void BuildNavalSelection(Widget panel)
        {
            _navalSelectionPanel = AddSection(
                panel,
                "NavalBattleSelectionPanel",
                24f,
                100f,
                545f,
                604f);
            _navalSelectionPanel.IsVisible = false;
            Widget section = _navalSelectionPanel;

            AddLabel(
                section,
                "NavalBattleSelectionTitle",
                "ВЫБЕРИТЕ КОРАБЛЬ",
                20f,
                8f,
                505f,
                44f,
                30,
                AmberText);
            AddAmberLine(section, "NavalBattleSelectionLine", 20f, 54f, 505f);

            string[] navalClassLabels = { "ЛЁГКИЙ", "СРЕДНИЙ", "ТЯЖЁЛЫЙ" };
            for (int index = 0; index < navalClassLabels.Length; index++)
            {
                int shipClass = index;
                _navalClassButtons.Add(AddChoiceButton(
                    section,
                    "NavalShipClass" + index,
                    navalClassLabels[index],
                    20f + index * 170f,
                    70f,
                    165f,
                    shipClass,
                    () =>
                    {
                        DikozemieArenaMatchSelection.SelectNavalClass(
                            (DikozemieNavalShipClass)shipClass);
                        RefreshRows();
                    }));
            }

            _navalName = AddLabel(
                section,
                "NavalSelectedShipName",
                string.Empty,
                20f,
                128f,
                505f,
                52f,
                32,
                GoldText);
            _navalDetails = AddLabel(
                section,
                "NavalSelectedShipDetails",
                string.Empty,
                20f,
                182f,
                505f,
                68f,
                21,
                BodyText,
                TextHorizontalAlignment.Center,
                BodyFont);

            ButtonWidget previousHull = AddButton(
                section,
                "NavalPreviousHull",
                "ПРЕДЫДУЩИЙ",
                20f,
                262f,
                245f);
            previousHull.ClickEventHandlers.Add(_ =>
            {
                DikozemieArenaMatchSelection.SelectNextNavalHull(-1);
                RefreshRows();
            });
            ButtonWidget nextHull = AddButton(
                section,
                "NavalNextHull",
                "СЛЕДУЮЩИЙ",
                280f,
                262f,
                245f);
            nextHull.ClickEventHandlers.Add(_ =>
            {
                DikozemieArenaMatchSelection.SelectNextNavalHull(1);
                RefreshRows();
            });

            _navalIndex = AddLabel(
                section,
                "NavalSelectedShipIndex",
                string.Empty,
                20f,
                314f,
                505f,
                30f,
                17,
                MutedText,
                TextHorizontalAlignment.Center,
                BodyFont);
            AddAmberLine(section, "NavalSelectionLine", 20f, 352f, 505f);
            AddLabel(
                section,
                "NavalRosterHint",
                "КОРАБЛИ САМИ СХОДЯТСЯ И СЦЕПЛЯЮТСЯ КОШКАМИ\n" +
                "ВЫ ЗАНИМАЕТЕ МЕСТО В ЭКИПАЖЕ И ТОЛЬКО ДЕРЁТЕСЬ",
                20f,
                366f,
                505f,
                76f,
                20,
                BodyText,
                TextHorizontalAlignment.Center,
                BodyFont);
            ButtonWidget localBattle = AddButton(
                section,
                "NavalLocalBattle",
                "ОДИН ПРОТИВ БОТОВ · WAR SAILS",
                20f,
                470f,
                505f);
            localBattle.ClickEventHandlers.Add(_ =>
                DikozemieLobbyShellIntegration.StartLocalNavalBattle());
            AddLabel(
                section,
                "NavalLifeHint",
                "ОДНА ЖИЗНЬ · ОБЩИЙ РЕЙТИНГ · ЗЕРКАЛЬНЫЕ УСЛОВИЯ",
                20f,
                560f,
                505f,
                30f,
                16,
                MutedText,
                TextHorizontalAlignment.Center,
                BodyFont);
        }

        private void BuildTable(Widget panel)
        {
            Widget table = _tablePanel = AddSection(
                panel,
                "ArenaLeaderboardTablePanel",
                584f,
                100f,
                1272f,
                604f);

            _tableTitle = AddLabel(
                table,
                "ArenaLeaderboardTableTitle",
                "ТАБЛИЦА ЛИДЕРОВ",
                20f,
                8f,
                1232f,
                44f,
                30,
                AmberText);
            AddHeader(table, "№", 18f, 66f, 54f);
            AddHeader(table, "БОЕЦ", 90f, 640f, 54f, TextHorizontalAlignment.Left);
            AddHeader(table, "РЕЙТИНГ", 734f, 160f, 54f);
            AddHeader(table, "ПОБЕДЫ", 894f, 160f, 54f);
            AddHeader(table, "ПОРАЖЕНИЯ", 1054f, 200f, 54f);
            AddAmberLine(table, "ArenaLeaderboardColumnsLine", 12f, 86f, 1248f);
            _tableLine = table.FindChild("ArenaLeaderboardColumnsLine");

            for (int index = 0; index < PageSize; index++)
            {
                Widget row = CreateSolid(
                    "ArenaLeaderboardRow" + index,
                    index % 2 == 0 ? RowEven : RowOdd);
                row.WidthSizePolicy = SizePolicy.StretchToParent;
                row.HeightSizePolicy = SizePolicy.Fixed;
                row.SuggestedHeight = 32f;
                row.MarginLeft = 12f;
                row.MarginRight = 12f;
                row.PositionYOffset = 90f + (index * 33f);
                table.AddChild(row);

                _rows.Add(new RowWidgets
                {
                    Background = row,
                    Rank = AddCell(row, "Rank", 6f, 66f),
                    Name = AddCell(
                        row,
                        "Name",
                        78f,
                        640f,
                        TextHorizontalAlignment.Left),
                    Rating = AddCell(row, "Rating", 722f, 160f),
                    Wins = AddCell(row, "Wins", 882f, 160f),
                    Losses = AddCell(row, "Losses", 1042f, 200f)
                });
            }

            _pageText = AddLabel(
                table,
                "ArenaLeaderboardPage",
                string.Empty,
                240f,
                522f,
                792f,
                30f,
                18,
                MutedText,
                TextHorizontalAlignment.Center,
                BodyFont);

            _previousButton = AddButton(
                table,
                "ArenaLeaderboardPrevious",
                "НАЗАД",
                300f,
                554f);
            _previousButton.ClickEventHandlers.Add(_ => ChangePage(-1));
            ButtonWidget mineButton = _mineButton = AddButton(
                table,
                "ArenaLeaderboardMine",
                "МОЯ СТРОКА",
                511f,
                554f,
                250f);
            mineButton.ClickEventHandlers.Add(_ =>
            {
                MoveToPlayer();
                RefreshRows();
            });
            _nextButton = AddButton(
                table,
                "ArenaLeaderboardNext",
                "ДАЛЕЕ",
                792f,
                554f);
            _nextButton.ClickEventHandlers.Add(_ => ChangePage(1));
        }

        private Widget AddSection(
            Widget parent,
            string id,
            float x,
            float y,
            float width,
            float height)
        {
            Widget section = CreateSolid(id, SectionFill);
            section.WidthSizePolicy = SizePolicy.Fixed;
            section.HeightSizePolicy = SizePolicy.Fixed;
            section.SuggestedWidth = width;
            section.SuggestedHeight = height;
            section.PositionXOffset = x;
            section.PositionYOffset = y;
            section.DoNotAcceptEvents = false;
            parent.AddChild(section);
            return section;
        }

        private void AddAmberLine(
            Widget parent,
            string id,
            float x,
            float y,
            float width)
        {
            Widget line = CreateSolid(id, AmberLine);
            line.WidthSizePolicy = SizePolicy.Fixed;
            line.HeightSizePolicy = SizePolicy.Fixed;
            line.SuggestedWidth = width;
            line.SuggestedHeight = 2f;
            line.PositionXOffset = x;
            line.PositionYOffset = y;
            parent.AddChild(line);
        }

        private void AddCaption(
            Widget parent,
            string id,
            string text,
            float y)
        {
            AddLabel(
                parent,
                id,
                text,
                20f,
                y,
                505f,
                28f,
                18,
                MutedText,
                TextHorizontalAlignment.Left);
        }

        private TextWidget AddLabel(
            Widget parent,
            string id,
            string text,
            float x,
            float y,
            float width,
            float height,
            int size,
            Color color,
            TextHorizontalAlignment alignment = TextHorizontalAlignment.Center,
            string font = TitleFont)
        {
            TextWidget label = CreateText(id, text, size, color, font);
            label.WidthSizePolicy = SizePolicy.Fixed;
            label.HeightSizePolicy = SizePolicy.Fixed;
            label.SuggestedWidth = width;
            label.SuggestedHeight = height;
            label.PositionXOffset = x;
            label.PositionYOffset = y;
            label.Brush.TextHorizontalAlignment = alignment;
            parent.AddChild(label);
            return label;
        }

        private void AddHeader(
            Widget panel,
            string text,
            float x,
            float width,
            float y,
            TextHorizontalAlignment alignment = TextHorizontalAlignment.Center)
        {
            _tableHeaders.Add(AddLabel(
                panel,
                "ArenaLeaderboardHeader" + x,
                text,
                x,
                y,
                width,
                30f,
                18,
                MutedText,
                alignment));
        }

        // The native friends panel takes the right edge of the screen: with it
        // open the table narrows instead of hiding its columns under the panel.
        private void LayoutTable(bool narrow)
        {
            if (_tablePanel == null || (_tableLaidOut && narrow == _tableNarrow))
            {
                return;
            }
            _tableLaidOut = true;
            _tableNarrow = narrow;
            float[] x = narrow
                ? new[] { 18f, 90f, 500f, 610f, 720f }
                : new[] { 18f, 90f, 734f, 894f, 1054f };
            float[] width = narrow
                ? new[] { 66f, 400f, 110f, 110f, 150f }
                : new[] { 66f, 640f, 160f, 160f, 200f };
            float tableWidth = narrow ? 886f : 1272f;
            _tablePanel.SuggestedWidth = tableWidth;
            _tableTitle.SuggestedWidth = tableWidth - 40f;
            _tableLine.SuggestedWidth = tableWidth - 24f;
            for (int column = 0; column < _tableHeaders.Count; column++)
            {
                _tableHeaders[column].PositionXOffset = x[column];
                _tableHeaders[column].SuggestedWidth = width[column];
            }
            foreach (RowWidgets row in _rows)
            {
                TextWidget[] cells = { row.Rank, row.Name, row.Rating, row.Wins, row.Losses };
                for (int column = 0; column < cells.Length; column++)
                {
                    cells[column].PositionXOffset = x[column] - 12f;
                    cells[column].SuggestedWidth = width[column];
                }
            }
            _pageText.PositionXOffset = 40f;
            _pageText.SuggestedWidth = tableWidth - 80f;
            float middle = tableWidth * 0.5f;
            _previousButton.PositionXOffset = middle - 125f - 21f - 180f;
            _mineButton.PositionXOffset = middle - 125f;
            _nextButton.PositionXOffset = middle + 125f + 21f;
        }

        private TextWidget AddCell(
            Widget row,
            string suffix,
            float x,
            float width,
            TextHorizontalAlignment alignment = TextHorizontalAlignment.Center)
        {
            TextWidget cell = CreateText(
                row.Id + suffix,
                string.Empty,
                20,
                BodyText,
                BodyFont);
            cell.WidthSizePolicy = SizePolicy.Fixed;
            cell.HeightSizePolicy = SizePolicy.StretchToParent;
            cell.SuggestedWidth = width;
            cell.PositionXOffset = x;
            cell.Brush.TextHorizontalAlignment = alignment;
            row.AddChild(cell);
            return cell;
        }

        // A thin amber frame around a dark fill, as on the native lobby lists.
        private ButtonWidget CreateFramedButton(
            string id,
            float x,
            float y,
            float width,
            float height,
            out Widget fill)
        {
            var button = new ButtonWidget(Context)
            {
                Id = id,
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                SuggestedWidth = width,
                SuggestedHeight = height,
                PositionXOffset = x,
                PositionYOffset = y,
                DoNotPassEventsToChildren = true
            };
            button.AddChild(CreateSolid(id + "Frame", ButtonFrame));
            fill = CreateSolid(id + "Background", ButtonFill);
            fill.MarginLeft = 1f;
            fill.MarginRight = 1f;
            fill.MarginTop = 1f;
            fill.MarginBottom = 1f;
            button.AddChild(fill);
            Widget hover = CreateSolid(id + "Hover", ButtonHover);
            hover.IsVisible = false;
            button.AddChild(hover);
            _hoverables.Add(new KeyValuePair<ButtonWidget, Widget>(button, hover));
            return button;
        }

        private ButtonWidget AddButton(
            Widget panel,
            string id,
            string text,
            float x,
            float y,
            float width = 180f)
        {
            ButtonWidget button = CreateFramedButton(id, x, y, width, 40f, out _);
            TextWidget label = CreateText(id + "Text", text, 19, BodyText);
            label.WidthSizePolicy = SizePolicy.StretchToParent;
            label.HeightSizePolicy = SizePolicy.StretchToParent;
            button.AddChild(label);
            panel.AddChild(button);
            return button;
        }

        private ChoiceWidgets AddChoiceButton(
            Widget panel,
            string id,
            string text,
            float x,
            float y,
            float width,
            int value,
            Action select)
        {
            ButtonWidget button = CreateFramedButton(
                id,
                x,
                y,
                width,
                38f,
                out Widget background);
            TextWidget label = CreateText(id + "Text", text, 18, BodyText);
            label.WidthSizePolicy = SizePolicy.StretchToParent;
            label.HeightSizePolicy = SizePolicy.StretchToParent;
            button.AddChild(label);
            button.ClickEventHandlers.Add(_ => select?.Invoke());
            panel.AddChild(button);
            return new ChoiceWidgets
            {
                Button = button,
                Background = background,
                Label = label,
                Value = value
            };
        }

        private static void ApplyChoiceState(
            ChoiceWidgets choice,
            bool selected,
            bool available)
        {
            choice.Button.IsEnabled = available;
            choice.Button.DoNotAcceptEvents = !available;
            choice.Background.Color = !available
                ? ButtonDisabled
                : selected
                    ? ButtonSelected
                    : ButtonFill;
            choice.Label.Brush.FontColor = !available
                ? DisabledText
                : selected
                    ? GoldText
                    : BodyText;
        }

        private void ChangePage(int delta)
        {
            int entryCount =
                DikozemieArenaLeaderboardTransport.LatestEntries.Count;
            int maximumFirst = Math.Max(0, entryCount - PageSize);
            _centeredOnPlayer = false;
            _firstEntry = Math.Max(
                0,
                Math.Min(
                    maximumFirst,
                    _firstEntry + delta * PageSize));
            RefreshRows();
        }

        private void ChangePageByRows(int rowDelta)
        {
            int entryCount =
                DikozemieArenaLeaderboardTransport.LatestEntries.Count;
            int maximumFirst = Math.Max(0, entryCount - PageSize);
            int nextFirst = Math.Max(
                0,
                Math.Min(maximumFirst, _firstEntry + rowDelta));
            if (nextFirst == _firstEntry)
            {
                return;
            }

            _centeredOnPlayer = false;
            _firstEntry = nextFirst;
            RefreshRows();
        }

        private void MoveToPlayer()
        {
            _centeredOnPlayer = true;
            string heroId = DikozemieHeroProfile.Snapshot?.HeroId;
            if (string.IsNullOrWhiteSpace(heroId))
            {
                _firstEntry = 0;
                return;
            }

            IReadOnlyList<DikozemieArenaLeaderboardEntry> entries =
                DikozemieArenaLeaderboardTransport.LatestEntries;
            int playerIndex = -1;
            for (int index = 0; index < entries.Count; index++)
            {
                if (string.Equals(
                        entries[index]?.CharacterId,
                        heroId,
                        StringComparison.Ordinal))
                {
                    playerIndex = index;
                    break;
                }
            }
            _firstEntry = playerIndex >= 0
                ? Math.Max(
                    0,
                    Math.Min(
                        entries.Count - PageSize,
                        playerIndex - PageSize / 2))
                : 0;
        }

        private void RefreshRows()
        {
            if (!_built)
            {
                return;
            }

            bool isNaval = DikozemieArenaMatchSelection.MenuVenue ==
                DikozemieBattleVenue.Naval;
            _arenaSelectionPanel.IsVisible = !isNaval;
            _navalSelectionPanel.IsVisible = isNaval;
            _arenaBackdrop.IsVisible = !isNaval;
            _navalBackdrop.IsVisible = isNaval;
            // Every town has its arena (customer, 2026-11-05); fighters of
            // all towns still meet in one ring while there are few players.
            DikozemieLobbyMapNode town = DikozemieTown.Here;
            string townName = town != null && DikozemieTown.IsTown(town)
                ? DikozemieTown.DisplayName(town).ToUpperInvariant()
                : null;
            _title.Text = isNaval
                ? townName != null ? "ПОРТ · " + townName : "НАНЯТЬСЯ МАТРОСОМ"
                : townName != null ? "АРЕНА · " + townName : "АРЕНА ДИКОЗЕМЬЯ";
            _tableTitle.Text = isNaval
                ? "ОБЩАЯ ТАБЛИЦА ЛИДЕРОВ"
                : "ТАБЛИЦА ЛИДЕРОВ";

            DikozemieNavalBattleDefinition navalBattle =
                DikozemieArenaMatchSelection.SelectedNavalBattle;
            foreach (ChoiceWidgets choice in _navalClassButtons)
            {
                ApplyChoiceState(
                    choice,
                    choice.Value == (int)navalBattle.ShipClass,
                    available: true);
            }
            IReadOnlyList<DikozemieNavalBattleDefinition> navalClassBattles =
                DikozemieNavalBattleCatalog.FindByClass(
                    navalBattle.ShipClass);
            int navalClassIndex = 0;
            for (int index = 0; index < navalClassBattles.Count; index++)
            {
                if (string.Equals(
                        navalClassBattles[index].HullId,
                        navalBattle.HullId,
                        StringComparison.Ordinal))
                {
                    navalClassIndex = index;
                    break;
                }
            }
            string navalClassName;
            switch (navalBattle.ShipClass)
            {
                case DikozemieNavalShipClass.Medium:
                    navalClassName = "СРЕДНИЙ КОРАБЛЬ";
                    break;
                case DikozemieNavalShipClass.Heavy:
                    navalClassName = "ТЯЖЁЛЫЙ КОРАБЛЬ";
                    break;
                default:
                    navalClassName = "ЛЁГКИЙ КОРАБЛЬ";
                    break;
            }
            _navalName.Text = navalBattle.DisplayName.ToUpperInvariant();
            _navalDetails.Text = navalClassName +
                "\nЭКИПАЖ: " + navalBattle.CrewCapacity + " ПРОТИВ " +
                navalBattle.CrewCapacity + " · ВСЕГО " +
                navalBattle.TotalCombatants;
            _navalIndex.Text = "КОРАБЛЬ " + (navalClassIndex + 1) +
                " ИЗ " + navalClassBattles.Count;

            DikozemieArenaMatchSelection.ReconcileWithLoadout();
            IReadOnlyList<DikozemieArenaLeaderboardEntry> entries =
                DikozemieArenaLeaderboardTransport.LatestEntries;
            if (_centeredOnPlayer)
            {
                // Keep following the hero when a post-fight rating update
                // changes their position in the ladder.
                MoveToPlayer();
            }
            _firstEntry = Math.Max(
                0,
                Math.Min(
                    Math.Max(0, entries.Count - PageSize),
                    _firstEntry));
            foreach (ChoiceWidgets toggle in _rotationSizeToggles)
            {
                ApplyToggleState(
                    toggle,
                    DikozemieBattleRotation.IsSizeEnabled(toggle.Value),
                    usable: true);
            }
            foreach (ChoiceWidgets toggle in _rotationStyleToggles)
            {
                var discipline = (DikozemieArenaDiscipline)toggle.Value;
                ApplyToggleState(
                    toggle,
                    DikozemieBattleRotation.IsDisciplineEnabled(discipline),
                    DikozemieArenaMatchSelection.IsDisciplineAvailable(discipline, out _));
            }
            foreach (ChoiceWidgets toggle in _rotationMixToggles)
            {
                var composition = (DikozemieArenaComposition)toggle.Value;
                bool usable = false;
                foreach (DikozemieArenaDiscipline discipline in DikozemieBattleRotation.Disciplines)
                {
                    usable |= DikozemieArenaMatchSelection.IsCompositionPlayable(
                        discipline,
                        composition);
                }
                ApplyToggleState(
                    toggle,
                    DikozemieBattleRotation.IsCompositionEnabled(composition),
                    usable);
            }
            foreach (ChoiceWidgets toggle in _rotationShipToggles)
            {
                ApplyToggleState(
                    toggle,
                    DikozemieBattleRotation.IsShipEnabled(
                        (DikozemieNavalShipClass)toggle.Value),
                    DikozemieLaunchGuard.IsNavalAvailable);
            }
            string heroId = DikozemieHeroProfile.Snapshot?.HeroId;

            for (int rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
            {
                int entryIndex = _firstEntry + rowIndex;
                RowWidgets row = _rows[rowIndex];
                if (entryIndex >= entries.Count)
                {
                    row.Background.IsVisible = false;
                    continue;
                }

                DikozemieArenaLeaderboardEntry entry = entries[entryIndex];
                bool isPlayer = !string.IsNullOrWhiteSpace(heroId) &&
                    string.Equals(entry.CharacterId, heroId, StringComparison.Ordinal);
                row.Background.IsVisible = true;
                row.Background.Color = isPlayer
                    ? RowPlayer
                    : rowIndex % 2 == 0
                        ? RowEven
                        : RowOdd;
                row.Rank.Text = entry.Rank.ToString();
                row.Name.Text = (isPlayer ? "★ " : string.Empty) +
                    DikozemieArenaBotNames.Format(
                        entry.CharacterId,
                        entry.DisplayName);
                row.Rating.Text = entry.Rating.ToString();
                row.Wins.Text = entry.Wins.ToString();
                row.Losses.Text = entry.Losses.ToString();
                Color cellColor = isPlayer ? GoldText : BodyText;
                row.Rank.Brush.FontColor = cellColor;
                row.Name.Brush.FontColor = cellColor;
                row.Rating.Brush.FontColor = cellColor;
                row.Wins.Brush.FontColor = cellColor;
                row.Losses.Brush.FontColor = cellColor;
            }

            _summary.Text = "ВАШ РЕЙТИНГ: " +
                DikozemieArmoryProgression.PlayerRating +
                "   •   ВАШИ МОНЕТЫ: " +
                DikozemieArmoryProgression.HeroResources +
                "   •   УЧАСТНИКОВ: " + entries.Count;
            _loadoutHint.Text = DikozemieLaunchGuard.IsNavalAvailable
                ? DikozemieArenaMatchSelection.BuildLoadoutAvailabilityText()
                : "МОРСКИЕ БОИ ПРОПУСКАЮТСЯ: ИГРА ЗАПУЩЕНА БЕЗ WAR SAILS";
            DikozemieBattlePlan next = DikozemieBattleRotation.PreviewNext();
            _loadoutHint.Brush.FontColor = next == null
                ? new Color(0.94f, 0.38f, 0.27f, 1f)
                : MutedText;
            _selectionSummary.Text = next == null
                ? "НЕТ ПОДХОДЯЩЕГО БОЯ\nВКЛЮЧИТЕ ДРУГИЕ ВИДЫ ИЛИ СМЕНИТЕ СНАРЯЖЕНИЕ"
                : next.DescribeForPanel();
            if (DikozemieEconomy.IsBanned)
            {
                // The observer closed the Arena to this hero: the search does
                // not start, and the page says why and for how long.
                _selectionSummary.Text = "АРЕНА ЗАКРЫТА НАБЛЮДАТЕЛЕМ";
                _loadoutHint.Text = DikozemieEconomy.StatusText;
                _loadoutHint.Brush.FontColor = new Color(0.94f, 0.38f, 0.27f, 1f);
            }
            int lastEntry = Math.Min(
                entries.Count,
                _firstEntry + PageSize);
            _pageText.Text = "МЕСТА " +
                (entries.Count > 0 ? _firstEntry + 1 : 0) + "–" +
                lastEntry + " ИЗ " + entries.Count +
                " · КОЛЕСО: ПРОКРУТКА";
            _previousButton.IsEnabled = _firstEntry > 0;
            _previousButton.DoNotAcceptEvents = !_previousButton.IsEnabled;
            _nextButton.IsEnabled = _firstEntry + PageSize < entries.Count;
            _nextButton.DoNotAcceptEvents = !_nextButton.IsEnabled;
        }

        private Widget CreateSolid(string id, Color color)
        {
            return new Widget(Context)
            {
                Id = id,
                WidthSizePolicy = SizePolicy.StretchToParent,
                HeightSizePolicy = SizePolicy.StretchToParent,
                Sprite = Context.SpriteData.GetSprite("BlankWhiteSquare_9"),
                Color = color,
                DoNotAcceptEvents = true
            };
        }

        private Widget CreateSprite(string id, string spriteName)
        {
            return new Widget(Context)
            {
                Id = id,
                Sprite = Context.SpriteData.GetSprite(spriteName),
                Color = new Color(1f, 1f, 1f, 0.92f),
                DoNotAcceptEvents = true
            };
        }

        private TextWidget CreateText(
            string id,
            string text,
            int size,
            Color color,
            string font = TitleFont)
        {
            var widget = new TextWidget(Context)
            {
                Id = id,
                Text = text,
                DoNotAcceptEvents = true
            };
            widget.Brush.Font = Context.FontFactory.GetMappedFontForLocalization(font);
            widget.Brush.FontSize = size;
            widget.Brush.FontColor = color;
            widget.Brush.TextHorizontalAlignment = TextHorizontalAlignment.Center;
            widget.Brush.TextVerticalAlignment = TextVerticalAlignment.Center;
            return widget;
        }
    }
}
