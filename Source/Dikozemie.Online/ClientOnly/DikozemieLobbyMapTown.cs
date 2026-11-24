using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets;
using TaleWorlds.TwoDimension;

namespace Dikozemie.Online
{
    // The town picture: one painting per culture and kind of place
    // (Tools/build_town_pictures.py). The widget asks for the current one.
    public sealed class DikozemieTownPictureTextureProvider : TextureProvider
    {
        internal static string Requested = "empire-town";
        private string _loadedName;
        private TaleWorlds.Engine.Texture _engine;
        private TaleWorlds.TwoDimension.Texture _texture;

        protected override TaleWorlds.TwoDimension.Texture OnGetTextureForRender(
            TwoDimensionContext twoDimensionContext,
            string name)
        {
            string wanted = Requested;
            if (string.Equals(wanted, _loadedName, StringComparison.Ordinal))
            {
                return _texture;
            }
            Release(true);
            _loadedName = wanted;
            try
            {
                string path = Path.Combine(
                    ModuleHelper.GetModuleFullPath(DikozemieSubModule.ModuleId),
                    "GUI", "Textures", "Towns", wanted + ".jpg");
                if (File.Exists(path))
                {
                    _engine = TaleWorlds.Engine.Texture.CreateFromMemory(File.ReadAllBytes(path));
                    if (_engine != null)
                    {
                        // Without its own name the engine hands back the first image.
                        _engine.Name = "dikozemie_town_" + wanted;
                        _texture = new TaleWorlds.TwoDimension.Texture(new EngineTexture(_engine));
                    }
                }
                else
                {
                    Console.WriteLine("DikozemieOnline: town picture is missing: " + path);
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: town picture failed: " + error.Message);
                _texture = null;
            }
            return _texture;
        }

        private void Release(bool nextFrame)
        {
            _texture = null;
            if (_engine != null && !_engine.IsReleased)
            {
                if (nextFrame)
                {
                    _engine.ReleaseAfterNumberOfFrames(2);
                }
                else
                {
                    _engine.Release();
                }
            }
            _engine = null;
            _loadedName = null;
        }

        public override void Clear(bool clearNextFrame)
        {
            Release(clearNextFrame);
            base.Clear(clearNextFrame);
        }
    }

    // The town screen of the map: the customer's «контекстное меню города»
    // (2026-11-05). A painting of the place with its name, owner and story,
    // and the menu on the right as in the singleplayer game.
    public sealed partial class DikozemieLobbyMapWidget
    {
        private enum TownSection
        {
            Main,
            Tavern,
            Castle,
            People,
            Port,
            Wanted,
            // The trade yard, the elder's or the lord's errands (stage 3).
            Trade,
            Commenda,
            // Stage 6: the tavern's mercenaries; a castle of one's clan.
            Mercs,
            Holding
        }

        private const int TownOptionCount = 10;
        private const float TownOptionHeight = 52f;
        private const float TownOptionStep = 60f;
        private const int TownShadeSteps = 12;

        private Widget _town;
        private Widget _townView;
        private TextureWidget _townPicture;
        private readonly List<Widget> _townShade = new List<Widget>();
        private readonly Widget[] _townFrame = new Widget[4];
        private TextWidget _townName;
        private TextWidget _townKind;
        private TextWidget _townOwner;
        private TextWidget _townLore;
        private readonly ButtonWidget[] _townOptions = new ButtonWidget[TownOptionCount];
        private readonly Widget[] _townOptionFills = new Widget[TownOptionCount];
        private readonly TextWidget[] _townOptionLabels = new TextWidget[TownOptionCount];
        private readonly TextWidget[] _townOptionDetails = new TextWidget[TownOptionCount];
        private readonly Action[] _townActions = new Action[TownOptionCount];
        private ButtonWidget _enterButton;
        private TextWidget _enterText;
        private bool _townOpen;
        private DikozemieLobbyMapNode _townNode;
        private TownSection _townSection;
        private string _townNote = string.Empty;
        private int _rumorIndex;
        private bool _passagePick;

        // The notables of «Горожане» standing on the town picture: the same
        // people the town walk puts in the streets (stage 4).
        private const int TownFigureCount = 5;
        private readonly CharacterTableauWidget[] _townFigures = new CharacterTableauWidget[TownFigureCount];
        private readonly TextWidget[] _townFigureRoles = new TextWidget[TownFigureCount];
        private readonly TextWidget[] _townFigureNames = new TextWidget[TownFigureCount];
        private int _townFigureCount;

        private static readonly Color TownGold = new Color(0.94f, 0.73f, 0.31f, 1f);
        private static readonly Color TownText = new Color(0.90f, 0.84f, 0.72f, 1f);
        private static readonly Color TownDim = new Color(0.74f, 0.62f, 0.42f, 1f);
        private static readonly Color OptionFill = new Color(0.12f, 0.08f, 0.04f, 1f);
        private static readonly Color OptionFillHover = new Color(0.27f, 0.17f, 0.06f, 1f);

        private void BuildTownLayer()
        {
            _town = new Widget(Context)
            {
                Id = "TownScreen",
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                ClipContents = true,
                DoNotAcceptEvents = true,
                IsVisible = false
            };
            AddChild(_town);
            // The town covers the whole map area, the zoomed map included.
            _town.AddChild(CreateSolid("TownBackdrop", new Color(0.025f, 0.022f, 0.018f, 1f)));
            _townView = new Widget(Context)
            {
                Id = "TownView",
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                ClipContents = true,
                DoNotAcceptEvents = true
            };
            _town.AddChild(_townView);
            _townView.AddChild(CreateSolid("TownViewBackdrop", new Color(0.03f, 0.025f, 0.02f, 1f)));
            _townPicture = new TextureWidget(Context)
            {
                Id = "TownPicture",
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                TextureProviderName = "DikozemieTownPictureTextureProvider",
                DoNotAcceptEvents = true
            };
            _townView.AddChild(_townPicture);
            // Gauntlet has no gradient here: thin bands of rising darkness
            // under the text read as one.
            for (int index = 0; index < TownShadeSteps; index++)
            {
                Widget band = CreateSolid("TownShade" + index, new Color(0.02f, 0.015f, 0.01f, 1f));
                band.WidthSizePolicy = SizePolicy.StretchToParent;
                band.HeightSizePolicy = SizePolicy.Fixed;
                band.VerticalAlignment = VerticalAlignment.Top;
                _townView.AddChild(band);
                _townShade.Add(band);
            }
            for (int index = 0; index < TownFigureCount; index++)
            {
                var figure = new CharacterTableauWidget(Context)
                {
                    Id = "TownFigure" + index,
                    WidthSizePolicy = SizePolicy.Fixed,
                    HeightSizePolicy = SizePolicy.Fixed,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    DoNotAcceptEvents = true,
                    IsVisible = false
                };
                _townView.AddChild(figure);
                _townFigures[index] = figure;
            }
            for (int index = 0; index < _townFrame.Length; index++)
            {
                _townFrame[index] = CreateSolid("TownFrame" + index, new Color(0.72f, 0.47f, 0.17f, 0.9f));
                _townFrame[index].WidthSizePolicy = SizePolicy.Fixed;
                _townFrame[index].HeightSizePolicy = SizePolicy.Fixed;
                _townFrame[index].HorizontalAlignment = HorizontalAlignment.Left;
                _townFrame[index].VerticalAlignment = VerticalAlignment.Top;
                _townView.AddChild(_townFrame[index]);
            }

            _townName = CreateTownText("TownName", 58, TownGold);
            _townKind = CreateTownText("TownKind", 21, new Color(0.98f, 0.83f, 0.42f, 1f));
            _townOwner = CreateTownText("TownOwner", 20, TownDim);
            _townLore = CreateTownText("TownLore", 19, TownText);
            _townLore.Brush.TextVerticalAlignment = TextVerticalAlignment.Top;
            for (int index = 0; index < TownFigureCount; index++)
            {
                _townFigureRoles[index] = CreateTownText("TownFigureRole" + index, 18, new Color(0.98f, 0.83f, 0.42f, 1f));
                _townFigureRoles[index].Brush.TextHorizontalAlignment = TextHorizontalAlignment.Center;
                _townFigureRoles[index].IsVisible = false;
                _townFigureNames[index] = CreateTownText("TownFigureName" + index, 22, TownText);
                _townFigureNames[index].Brush.TextHorizontalAlignment = TextHorizontalAlignment.Center;
                _townFigureNames[index].IsVisible = false;
            }

            for (int index = 0; index < TownOptionCount; index++)
            {
                int captured = index;
                var button = new ButtonWidget(Context)
                {
                    Id = "TownOption" + index,
                    WidthSizePolicy = SizePolicy.StretchToParent,
                    HeightSizePolicy = SizePolicy.Fixed,
                    SuggestedHeight = TownOptionHeight,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    MarginLeft = 30f,
                    MarginRight = 30f,
                    DoNotPassEventsToChildren = true,
                    IsVisible = false
                };
                button.AddChild(CreateSolid("TownOptionFrame" + index, new Color(0.72f, 0.47f, 0.17f, 1f)));
                Widget fill = CreateSolid("TownOptionFill" + index, OptionFill);
                fill.MarginLeft = 2f;
                fill.MarginRight = 2f;
                fill.MarginTop = 2f;
                fill.MarginBottom = 2f;
                button.AddChild(fill);
                TextWidget label = CreateText("TownOptionText" + index, string.Empty, 22,
                    new Color(0.98f, 0.83f, 0.42f, 1f));
                label.WidthSizePolicy = SizePolicy.StretchToParent;
                label.HeightSizePolicy = SizePolicy.StretchToParent;
                label.MarginLeft = 22f;
                label.MarginRight = 110f;
                label.Brush.TextHorizontalAlignment = TextHorizontalAlignment.Left;
                button.AddChild(label);
                TextWidget detail = CreateText("TownOptionDetail" + index, string.Empty, 18,
                    new Color(0.80f, 0.66f, 0.40f, 1f));
                detail.WidthSizePolicy = SizePolicy.StretchToParent;
                detail.HeightSizePolicy = SizePolicy.StretchToParent;
                detail.MarginRight = 20f;
                detail.Brush.TextHorizontalAlignment = TextHorizontalAlignment.Right;
                button.AddChild(detail);
                button.ClickEventHandlers.Add(_ => OnTownOption(captured));
                _sidePanel.AddChild(button);
                _townOptions[index] = button;
                _townOptionFills[index] = fill;
                _townOptionLabels[index] = label;
                _townOptionDetails[index] = detail;
            }

            BuildTradeLayer();
            BuildChatLayer();
            BuildClanLayer();
            _enterButton = CreatePanelButton("MapEnter", "ВОЙТИ В ГОРОД", 196f);
            _enterText = (TextWidget)_enterButton.FindChild("MapEnterText", true);
            _enterButton.ClickEventHandlers.Add(_ => OpenTown(NodeHere()));
        }

        private TextWidget CreateTownText(string id, int size, Color color)
        {
            TextWidget text = CreateText(id, string.Empty, size, color);
            text.WidthSizePolicy = SizePolicy.Fixed;
            text.HeightSizePolicy = SizePolicy.Fixed;
            text.HorizontalAlignment = HorizontalAlignment.Left;
            text.VerticalAlignment = VerticalAlignment.Top;
            text.Brush.TextHorizontalAlignment = TextHorizontalAlignment.Left;
            text.Brush.DefaultStyle.TextGlowColor = new Color(0f, 0f, 0f, 1f);
            text.Brush.DefaultStyle.TextOutlineColor = new Color(0f, 0f, 0f, 0.9f);
            text.Brush.DefaultStyle.TextOutlineAmount = 0.12f;
            _townView.AddChild(text);
            return text;
        }

        internal bool IsTownOpen => _townOpen;

        // Requests from the location line above the screen and from the
        // shell (the Arena tab opens the town the hero stands in).
        private void TickTownRequests()
        {
            bool? request = DikozemieTown.ConsumeTownScreenRequest();
            if (request == null || _travel == null)
            {
                return;
            }
            if (request.Value)
            {
                DikozemieLobbyMapNode here = NodeHere();
                if (here != null && !_travelling)
                {
                    OpenTown(here);
                    ApplyWalkExit();
                }
            }
            else
            {
                CloseTown();
                // «КАРТА» in the line under the header: the map itself.
                if (_clanOpen)
                {
                    _clanReturnTown = null;
                    CloseClan();
                }
                if (_siegeOpen)
                {
                    _siegeOpen = false;
                    _siegeId = string.Empty;
                    RefreshSidePanel();
                    LayoutContent();
                }
            }
        }

        private void OpenTown(DikozemieLobbyMapNode node)
        {
            if (node == null)
            {
                return;
            }
            _passagePick = false;
            if (_clanOpen || _siegeOpen)
            {
                // The place's own screen replaces the clan's and the siege's.
                CloseSheet();
                CloseChat();
                _clanOpen = false;
                _siegeOpen = false;
                _siegeId = string.Empty;
                DikozemieClans.IsScreenOpen = false;
            }
            _townOpen = true;
            _townNode = node;
            _townSection = TownSection.Main;
            _townNote = string.Empty;
            DikozemieTown.IsTownScreenOpen = true;
            DikozemieTown.OpenArmory(false);
            string culture = DikozemieTown.NormalizeCulture(node.Culture);
            if (string.IsNullOrEmpty(culture) || culture == "neutral_culture")
            {
                culture = "empire";
            }
            string kind = DikozemieTown.IsCastle(node)
                ? "castle"
                : DikozemieTown.IsVillage(node)
                    ? "village"
                    : node.HasPort ? "port" : "town";
            DikozemieTownPictureTextureProvider.Requested = culture + "-" + kind;
            DikozemieTownInfo info = DikozemieTownCatalog.Get(node.Id);
            _townName.Text = DisplayNodeName(node).ToUpperInvariant();
            _townKind.Text = GetKindName(node) + "  ·  " + GetCultureName(node.Culture) +
                (node.HasPort ? "  ·  ПОРТ" : string.Empty);
            string owner = string.IsNullOrEmpty(info.Clan)
                ? string.Empty
                : (DikozemieTown.IsVillage(node) ? "Платит оброк клану " : "Владеет клан ") +
                    info.Clan + (string.IsNullOrEmpty(info.Kingdom) ? string.Empty : " · " + info.Kingdom);
            string clanOwner = DikozemieClans.OwnerLine(node);
            _townOwner.Text = string.IsNullOrEmpty(clanOwner) ? owner : clanOwner;
            _townLore.Text = string.IsNullOrEmpty(info.Lore) ? DefaultLore(node) : info.Lore;
            if (IsCaptiveHere(node))
            {
                _townKind.Text = "ВЫ В ПЛЕНУ  ·  АРЕНА " + DisplayNodeName(node).ToUpperInvariant();
                _townOwner.Text = "Пленники дерутся за свободу. Выход — через песок арены, выкуп или ночной побег.";
            }
            string note = DikozemieRoads.TakeNote();
            if (!string.IsNullOrEmpty(note))
            {
                _townNote = note;
            }
            DikozemieLobbyShellIntegration.TraceButtonAction(
                "town open node=" + node.Id + " kind=" + node.Kind + " picture=" +
                DikozemieTownPictureTextureProvider.Requested);
            RefreshSidePanel();
            LayoutContent();
        }

        private static bool IsCaptiveHere(DikozemieLobbyMapNode node) =>
            node != null && DikozemieRoads.IsCaptive &&
            string.Equals(DikozemieRoads.CaptiveTown?.Id, node.Id, StringComparison.OrdinalIgnoreCase);

        private void CloseTown()
        {
            CloseSheet();
            if (!_townOpen && !_passagePick)
            {
                return;
            }
            if (_townOpen && IsCaptiveHere(_townNode))
            {
                ShowSection(TownSection.Main, "Стражник у решётки ухмыляется: «Куда? Сначала песок.»");
                return;
            }
            _townOpen = false;
            _passagePick = false;
            CloseChat();
            HideTownFigures();
            DikozemieTown.IsTownScreenOpen = false;
            RefreshSidePanel();
            LayoutContent();
        }

        private static string DefaultLore(DikozemieLobbyMapNode node)
        {
            if (DikozemieTown.IsCastle(node))
            {
                return "Каменные стены, ров и ворота со стражей. Внутри живёт гарнизон лорда, " +
                    "а в оружейной хранится то, что простому люду не по карману.";
            }
            return DikozemieTown.IsVillage(node)
                ? "Несколько дворов, поля и выгон. Здесь пашут, пасут скот и платят оброк владельцу."
                : "Шумный город за стенами: рынок, мастерские, трактиры и арена.";
        }

        private void LayoutTown(float x, float y, float width, float height)
        {
            if (_town == null)
            {
                return;
            }
            _town.IsVisible = LayerOpen;
            if (!LayerOpen)
            {
                if (_clanBanner != null)
                {
                    _clanBanner.IsVisible = false;
                    _foeBanner.IsVisible = false;
                    _placeBanner.IsVisible = false;
                    _placeBannerFrame.IsVisible = false;
                    _bannerBoard.IsVisible = false;
                }
                return;
            }
            _town.PositionXOffset = 0f;
            _town.PositionYOffset = 0f;
            _town.SuggestedWidth = Math.Max(width + x, ViewWidth - SidePanelWidth - OuterMargin);
            _town.SuggestedHeight = ViewHeight;
            _townView.PositionXOffset = x;
            _townView.PositionYOffset = y;
            _townView.SuggestedWidth = width;
            _townView.SuggestedHeight = height;

            // The painting covers the whole frame, cut at the sides or top.
            float scale = Math.Max(width / 1600f, height / 900f);
            float pictureWidth = 1600f * scale;
            float pictureHeight = 900f * scale;
            _townPicture.SuggestedWidth = pictureWidth;
            _townPicture.SuggestedHeight = pictureHeight;
            _townPicture.PositionXOffset = (width - pictureWidth) * 0.5f;
            _townPicture.PositionYOffset = (height - pictureHeight) * 0.35f;

            float shadeTop = height * 0.44f;
            float bandHeight = (height - shadeTop) / TownShadeSteps + 1f;
            for (int index = 0; index < _townShade.Count; index++)
            {
                float t = (index + 1f) / TownShadeSteps;
                Widget band = _townShade[index];
                band.SuggestedHeight = bandHeight;
                band.PositionYOffset = shadeTop + index * (bandHeight - 1f);
                band.Color = new Color(0.02f, 0.015f, 0.01f, Math.Min(0.92f, 0.1f + t * t * 0.86f));
            }

            const float line = 2f;
            SetFrame(_townFrame[0], 0f, 0f, width, line);
            SetFrame(_townFrame[1], 0f, height - line, width, line);
            SetFrame(_townFrame[2], 0f, 0f, line, height);
            SetFrame(_townFrame[3], width - line, 0f, line, height);

            float left = 44f;
            float textWidth = Math.Max(300f, width - left * 2f);
            // The story sits at the bottom, as many lines as it needs
            // (about 0.5 of the font size per letter).
            int perLine = Math.Max(30, (int)(textWidth / (19f * 0.5f)));
            int lines = Math.Max(1, Math.Min(7, (_townLore.Text.Length + perLine - 1) / perLine));
            float loreHeight = lines * 25f + 6f;
            // The game's own messages appear at the bottom left: the story
            // stands above them.
            float loreTop = height - 110f - loreHeight;
            SetText(_townLore, left, loreTop, textWidth, loreHeight);
            SetText(_townOwner, left, loreTop - 38f, textWidth, 30f);
            SetText(_townKind, left, loreTop - 72f, textWidth, 30f);
            SetText(_townName, left, loreTop - 146f, textWidth, 72f);
            LayoutTradeSheet(width, height);
            LayoutChatSheet(width, height);
            LayoutTownFigures(width, height);
            LayoutClan(width, height);
            LayoutPlaceBanners(width, height);
            if (_bannerBoard.IsVisible)
            {
                _townName.IsVisible = false;
                _townKind.IsVisible = false;
                _townOwner.IsVisible = false;
                _townLore.IsVisible = false;
            }
        }

        private static void SetFrame(Widget widget, float x, float y, float width, float height)
        {
            widget.PositionXOffset = x;
            widget.PositionYOffset = y;
            widget.SuggestedWidth = width;
            widget.SuggestedHeight = height;
        }

        private static void SetText(TextWidget widget, float x, float y, float width, float height)
        {
            widget.PositionXOffset = x;
            widget.PositionYOffset = y;
            widget.SuggestedWidth = width;
            widget.SuggestedHeight = height;
        }

        // Buttons do not light up by themselves when built in code.
        private void TickTownHover()
        {
            for (int index = 0; index < TownOptionCount; index++)
            {
                ButtonWidget button = _townOptions[index];
                if (button == null || !button.IsVisible)
                {
                    continue;
                }
                _townOptionFills[index].Color = button.IsHovered ? OptionFillHover : OptionFill;
            }
        }

        private void SetMapPanelVisible(bool visible)
        {
            _zoomText.IsVisible = visible;
            _legend.IsVisible = visible;
            Widget footer = _sidePanel.FindChild("MapFooter", false);
            if (footer != null)
            {
                footer.IsVisible = visible;
            }
            if (!visible)
            {
                _arenaButton.IsVisible = false;
                _navalButton.IsVisible = false;
                _goButton.IsVisible = false;
                _boatButton.IsVisible = false;
                _enterButton.IsVisible = false;
                HideWorldButtons();
            }
        }

        private void HideTownOptions()
        {
            for (int index = 0; index < TownOptionCount; index++)
            {
                _townOptions[index].IsVisible = false;
                _townActions[index] = null;
            }
        }

        private int _optionCount;

        private void AddOption(string label, string detail, Action action)
        {
            if (_optionCount >= TownOptionCount)
            {
                return;
            }
            int index = _optionCount++;
            _townOptionLabels[index].Text = label;
            _townOptionDetails[index].Text = detail ?? string.Empty;
            _townActions[index] = action;
            _townOptions[index].IsVisible = true;
        }

        private void OnTownOption(int index)
        {
            Action action = index >= 0 && index < TownOptionCount ? _townActions[index] : null;
            if (action == null)
            {
                return;
            }
            DikozemieLobbyShellIntegration.TraceButtonAction(
                "town option " + _townOptionLabels[index].Text + " node=" + _townNode?.Id +
                " section=" + _townSection);
            action();
        }

        private void ShowSection(TownSection section, string note = "")
        {
            if (section != TownSection.Trade && section != TownSection.Commenda &&
                section != TownSection.Mercs && section != TownSection.Holding)
            {
                CloseSheet();
            }
            if (section != TownSection.People)
            {
                HideTownFigures();
            }
            if (section != TownSection.Tavern)
            {
                CloseChat();
            }
            _townSection = section;
            _townNote = note ?? string.Empty;
            RefreshSidePanel();
            LayoutContent();
        }

        // The side panel while the town screen is open.
        private void RefreshTownPanel()
        {
            SetMapPanelVisible(false);
            HideTownOptions();
            _optionCount = 0;
            DikozemieLobbyMapNode node = _townNode;
            if (node == null)
            {
                return;
            }
            if (IsCaptiveHere(node))
            {
                RefreshCaptivePanel(node);
                return;
            }
            bool town = DikozemieTown.IsTown(node);
            bool castle = DikozemieTown.IsCastle(node);
            int coins = DikozemieArmoryProgression.HeroResources;
            string purse = "Ваши монеты: " + coins + "." +
                (string.IsNullOrEmpty(DikozemieHorseUpkeep.Status)
                    ? string.Empty
                    : "\n" + DikozemieHorseUpkeep.Status);
            string text;
            switch (_townSection)
            {
                case TownSection.Tavern:
                    if (_chatOpen)
                    {
                        FillChatPanel(out text);
                        break;
                    }
                    _title.Text = "ТАВЕРНА";
                    text = "Дым, пиво и разговоры. Трактирщик знает, где в городе платят, " +
                        "а завсегдатаи за кружку расскажут, что слышали.";
                    // Stage 5: the other heroes of the town sit here too.
                    AddOption("ЗА СТОЛ С ГЕРОЯМИ", TableDetail(node), () => OpenChat());
                    if (!string.IsNullOrEmpty(DikozemieTownWalk.SceneFor(node, DikozemieWalkPlace.Tavern)))
                    {
                        AddOption("ЗАЙТИ ВНУТРЬ", "пешком", () => StartWalk(node, DikozemieWalkPlace.Tavern));
                    }
                    AddOption("ТРАКТИРЩИК", "как заработать", () => Inquiry(
                        "ТРАКТИРЩИК", DikozemieTown.TavernKeeper, "ПОНЯТНО", string.Empty, null, null));
                    AddOption("УГОСТИТЬ ЗАВСЕГДАТАЕВ", DikozemieTown.RumorPrice + " МОН.", BuyRumor);
                    AddOption("НОЧЛЕГ", RestDetail(), () => Rest(TownSection.Tavern));
                    AddOption("НАНЯТЬ ОХРАНУ", DikozemieRoads.EscortPrice + " МОН. В ДЕНЬ", () =>
                    {
                        DikozemieRoads.TryHireEscort(node, out string hired);
                        ShowSection(TownSection.Tavern, hired);
                    });
                    // Stage 6: men for a clan's warband and garrisons.
                    if (town)
                    {
                        AddOption("НАЁМНИКИ", DikozemieClans.InClan ? "в дружину клана" : "для кланов", () =>
                        {
                            ShowSection(TownSection.Mercs);
                            OpenMercs(node);
                        });
                    }
                    AddOption("НАЗАД", string.Empty, () => ShowSection(TownSection.Main));
                    break;
                case TownSection.Mercs:
                    _title.Text = "НАЁМНИКИ";
                    text = "У стойки сидят люди с оружием: ищут, кому служить. Наёмник идёт в дружину клана, " +
                        "встаёт в гарнизон его крепостей и идёт с кланом на штурм.";
                    AddOption("НАЗАД", string.Empty, () => ShowSection(TownSection.Tavern));
                    break;
                case TownSection.Holding:
                {
                    DikozemieClanHolding held = MyHolding(DikozemieClans.SeatOf(node));
                    _title.Text = castle ? "ВАШ ЗАМОК" : "ВАШ ГОРОД";
                    text = held == null
                        ? "Клан больше не держит эту крепость."
                        : "Над воротами — знамя вашего клана. Гарнизон: " + DikozemieClans.TroopsWords(held.Garrison) +
                          ". Доход земли ~" + FormatCoins((int)Math.Round(held.IncomeDay)) + " в день, вчера " +
                          FormatCoins(held.Stats.Yesterday) + "." +
                          (held.EmptyIn > 0 ? "\nВ крепости никого! Через " + DikozemieClans.Minutes(held.EmptyIn) +
                              " её займёт прежний владелец." : string.Empty);
                    if (held != null)
                    {
                        string seat = held.Node;
                        AddOption("ГАРНИЗОН", held.Men + " ЧЕЛ.", () => OpenGarrison(seat));
                        AddOption("ПОШЛИНЫ И ДОЗОРЫ", held.Patrol ? "дозоры есть" : "дозоров нет", () =>
                        {
                            _landNode = seat;
                            OpenSheet(TradeSheet.LandRates);
                        });
                        AddOption("КАЗНА КЛАНА", FormatCoins(DikozemieClans.Clan?.Coins ?? 0) + " МОН.", () => OpenClan(ClanSection.Treasury));
                    }
                    AddOption("НАЗАД", string.Empty, () => ShowSection(castle ? TownSection.Castle : TownSection.Main));
                    break;
                }
                case TownSection.Wanted:
                    _title.Text = "КАПИТАН СТРАЖИ";
                    text = WantedText(node);
                    AddOption("НАЗАД", string.Empty, () => ShowSection(TownSection.Main));
                    break;
                case TownSection.Castle:
                    _title.Text = castle ? "ЗАМОК" : "ЗАМОК ГОРОДА";
                    bool ownSeat = DikozemieClans.IsMyLand(DikozemieClans.LandOf(DikozemieClans.SeatOf(node)));
                    DikozemieSiege siegeHere = DikozemieClans.SiegeAt(DikozemieClans.SeatOf(node));
                    if (siegeHere != null && siegeHere.Mine)
                    {
                        AddOption(siegeHere.Attacking ? "ОСАДНЫЙ ЛАГЕРЬ" : "НА СТЕНЫ!", siegeHere.State == "battle"
                            ? "ШТУРМ" : DikozemieClans.Minutes(siegeHere.AssaultLeft), () => OpenSiege(siegeHere.Id));
                    }
                    // Stage 6: a clan's officer at the gate of a castle may lay siege.
                    DikozemieLandView walls = !ownSeat && castle && DikozemieClans.InClan ? LandFor(node) : null;
                    if (walls != null && string.IsNullOrEmpty(walls.SiegeBlock) && (siegeHere == null || !siegeHere.Mine))
                    {
                        AddOption("ОСАДИТЬ ЗАМОК", DikozemieClans.Rules.SiegeCost + " МОН. ИЗ КАЗНЫ", () => OnSiegeAt(node));
                    }
                    if (ownSeat)
                    {
                        text = "Стража салютует знамени вашего клана и распахивает ворота.";
                        AddOption(castle ? "ВАШ ЗАМОК" : "ВАШ ГОРОД", "гарнизон, пошлины", () => ShowSection(TownSection.Holding));
                        if (castle)
                        {
                            AddWalkOption(node, "пешком");
                        }
                        AddOption("ОРУЖЕЙНАЯ ЗНАТИ", "от " + DikozemieTown.ArmoryThreshold + " мон.", () =>
                        {
                            DikozemieTown.OpenArmory(true);
                            DikozemieLobbyShellIntegration.OpenMarketFromTown();
                        });
                        if (!string.IsNullOrEmpty(DikozemieTownWalk.SceneFor(node, DikozemieWalkPlace.Hall)))
                        {
                            AddOption("ВОЙТИ В ЗАЛ", "пешком", () => StartWalk(node, DikozemieWalkPlace.Hall));
                        }
                    }
                    else if (!DikozemieTown.HasCastlePass(node))
                    {
                        text = "Стража у ворот скрещивает копья: «Без дела не пускаем.» " +
                            "Десятник косится на ваш кошель: " + DikozemieTown.CastleEntryPrice +
                            " монет — и вас запомнят.\n\nВнутри — зал лорда и оружейная: " +
                            "доспехи и кони знати, которых нет на городском рынке.";
                        AddOption("ЗАПЛАТИТЬ СТРАЖЕ", DikozemieTown.CastleEntryPrice + " МОН.", BuyCastlePass);
                    }
                    else
                    {
                        text = "Стража вас знает и пропускает без слов. Во дворе куют, " +
                            "в оружейной пахнет маслом и кожей.";
                        if (castle)
                        {
                            AddWalkOption(node, "пешком");
                        }
                        AddOption("ОРУЖЕЙНАЯ ЗНАТИ", "от " + DikozemieTown.ArmoryThreshold + " мон.", () =>
                        {
                            DikozemieTown.OpenArmory(true);
                            DikozemieLobbyShellIntegration.OpenMarketFromTown();
                        });
                        AddOption("ЗАЛ ЛОРДА", "дела лорда", () =>
                        {
                            ShowSection(TownSection.Trade);
                            OpenSheet(TradeSheet.Contracts);
                        });
                        if (!string.IsNullOrEmpty(DikozemieTownWalk.SceneFor(node, DikozemieWalkPlace.Hall)))
                        {
                            AddOption("ВОЙТИ В ЗАЛ", "пешком", () => StartWalk(node, DikozemieWalkPlace.Hall));
                        }
                    }
                    AddOption(castle ? "УЙТИ НА КАРТУ" : "НАЗАД", string.Empty, () =>
                    {
                        if (castle)
                        {
                            CloseTown();
                        }
                        else
                        {
                            ShowSection(TownSection.Main);
                        }
                    });
                    break;
                case TownSection.People:
                    ShowTownFigures(node);
                    _title.Text = DikozemieTown.IsVillage(node) ? "ДЕРЕВНЯ" : "ГОРОЖАНЕ";
                    text = "С кем поговорить?";
                    foreach (DikozemieNotable notable in DikozemieTown.Notables(node))
                    {
                        DikozemieNotable captured = notable;
                        AddOption(notable.Role.ToUpperInvariant(), notable.Name, () => ShowSection(
                            TownSection.People,
                            captured.Role + " " + captured.Name + ":\n" + captured.Speech));
                    }
                    if (town)
                    {
                        AddOption("МАСТЕРСКИЕ", "купить, открыть дело", () =>
                        {
                            ShowSection(TownSection.Trade);
                            OpenSheet(TradeSheet.Workshops);
                        });
                    }
                    AddOption("НАЗАД", string.Empty, () => ShowSection(TownSection.Main));
                    break;
                case TownSection.Port:
                    _title.Text = "ПОРТ";
                    BuildPortOptions(out text);
                    break;
                case TownSection.Trade:
                    BuildTradeOptions(node, out text);
                    break;
                case TownSection.Commenda:
                    _title.Text = "ПОРТ";
                    text = "Купеческие корабли уходят за море. Шкипер берёт деньги в долю: " +
                        "три четверти прибыли — вкладчику.\n\n" + TradeSummary();
                    AddOption("КНИГА КАЗНЫ", string.Empty, () => OpenSheet(TradeSheet.Ledger));
                    AddOption("КУПЕЧЕСКИЙ КОРАБЛЬ", "вклады", () => OpenSheet(TradeSheet.Commenda));
                    AddOption("НАЗАД", string.Empty, () => ShowSection(TownSection.Port));
                    break;
                default:
                    _title.Text = castle ? "ЗАМОК" : town ? "ГОРОД" : "ДЕРЕВНЯ";
                    text = town
                        ? "Чем заняться?"
                        : castle ? "У ворот замка." : "Тихо: собаки, куры, дым из труб.";
                    DikozemieSiege townSiege = town ? DikozemieClans.SiegeAt(node.Id) : null;
                    if (townSiege != null && townSiege.Mine)
                    {
                        AddOption(townSiege.Attacking ? "ОСАДНЫЙ ЛАГЕРЬ" : "ОСАДА! НА СТЕНЫ", townSiege.State == "battle"
                            ? "ШТУРМ" : DikozemieClans.Minutes(townSiege.AssaultLeft), () => OpenSiege(townSiege.Id));
                    }
                    if (town)
                    {
                        AddWalkOption(node, "пешком");
                        AddOption("АРЕНА", "бои за монеты", () => DikozemieLobbyShellIntegration.OpenVenueFromTown(
                            DikozemieBattleVenue.Arena));
                        AddOption("ТАВЕРНА", TavernDetail(node), () => ShowSection(TownSection.Tavern));
                        AddOption("РЫНОК", "оружие, одежда, кони", () =>
                        {
                            DikozemieTown.OpenArmory(false);
                            DikozemieLobbyShellIntegration.OpenMarketFromTown();
                        });
                        AddOption("ТОРГОВЫЙ ДВОР", TradeYardDetail(), () =>
                        {
                            ShowSection(TownSection.Trade);
                            OpenSheet(TradeSheet.Exchange);
                        });
                        AddOption("ЗАМОК", DikozemieClans.IsMyLand(DikozemieClans.LandOf(node.Id))
                            ? "ваш клан"
                            : DikozemieTown.HasCastlePass(node)
                                ? "вход открыт"
                                : DikozemieTown.CastleEntryPrice + " МОН.", () => ShowSection(TownSection.Castle));
                        AddOption("ГОРОЖАНЕ", DikozemieTown.Notables(node).Count + " человека",
                            () => ShowSection(TownSection.People));
                        if (node.HasPort)
                        {
                            AddOption("ПОРТ", "корабли, матросы", () => ShowSection(TownSection.Port));
                        }
                        if (townSiege == null || !townSiege.Mine)
                        {
                            AddOption("РОЗЫСК", DikozemieRoads.HasPrisoner
                                ? "сдать пленника"
                                : DikozemieRoads.HasBounty ? "вы взялись" : "награды за разбойников",
                                () => ShowSection(TownSection.Wanted));
                        }
                        AddOption("ВЫЙТИ НА КАРТУ", string.Empty, CloseTown);
                    }
                    else if (castle)
                    {
                        _townSection = TownSection.Castle;
                        RefreshTownPanel();
                        return;
                    }
                    else
                    {
                        AddWalkOption(node, "пешком");
                        AddOption("СТАРОСТА", string.Empty, () => ShowSection(TownSection.People));
                        AddOption("ПОРУЧЕНИЕ СТАРОСТЫ", "деревне нужно", () =>
                        {
                            ShowSection(TownSection.Trade);
                            OpenSheet(TradeSheet.Contracts);
                        });
                        AddOption("ЗНАХАРКА", DikozemieRoads.Wound > 0
                            ? "перевязать: " + DikozemieRoads.HealerPrice + " МОН."
                            : "вы здоровы", Heal);
                        AddOption("СЕЛЬСКИЙ РЫНОК", string.Empty, () =>
                        {
                            DikozemieTown.OpenArmory(false);
                            DikozemieLobbyShellIntegration.OpenMarketFromTown();
                        });
                        AddOption("НОЧЛЕГ", RestDetail(), () => Rest(TownSection.Main));
                        AddOption("ВЫЙТИ НА КАРТУ", string.Empty, CloseTown);
                    }
                    break;
            }
            if (_townSection == TownSection.Wanted && DikozemieRoads.HasBounty && !string.IsNullOrEmpty(_townNote))
            {
                // Just taken: the captain's words say it all.
                text = string.Empty;
            }
            // The trade screens say the purse themselves.
            bool trade = _townSection == TownSection.Trade || _townSection == TownSection.Commenda ||
                _townSection == TownSection.Mercs || _townSection == TownSection.Holding;
            string tail = string.IsNullOrEmpty(_townNote) ? (trade ? TownStatus().TrimStart('\n') : purse + TownStatus()) : _townNote;
            _description.Text = string.IsNullOrEmpty(text) ? tail : text + "\n\n" + tail;
            StackOptions();
        }

        // How many heroes stand in the place now, and unread talk.
        private static string TableDetail(DikozemieLobbyMapNode node)
        {
            int here = DikozemieWorld.Players.Count(p => p.In == node?.Id);
            int unread = DikozemieWorld.UnreadIn(DikozemieWorld.PlaceChannel);
            if (!DikozemieWorld.IsOnline)
            {
                return "нет связи";
            }
            return (here == 0 ? "никого" : "героев: " + here) + (unread > 0 ? " · новых: " + unread : string.Empty);
        }

        private static string TavernDetail(DikozemieLobbyMapNode node)
        {
            int here = DikozemieWorld.Players.Count(p => p.In == node?.Id);
            int unread = DikozemieWorld.UnreadIn(DikozemieWorld.PlaceChannel);
            return here > 0 || unread > 0
                ? "героев: " + here + (unread > 0 ? " · новых: " + unread : string.Empty)
                : "слухи, советы";
        }

        // ---- The town walk (stage 4) ------------------------------------

        private void AddWalkOption(DikozemieLobbyMapNode node, string detail)
        {
            if (!DikozemieTownWalk.CanWalk(node))
            {
                return;
            }
            AddOption(DikozemieTownWalk.MenuLabel(node), detail, () => StartWalk(node));
        }

        private void StartWalk(DikozemieLobbyMapNode node, DikozemieWalkPlace place = DikozemieWalkPlace.Streets)
        {
            if (!DikozemieTownWalk.TryStart(node, place, null, out string error))
            {
                ShowSection(_townSection, error);
            }
        }

        // Back from the walk: the section the hero went for.
        private void ApplyWalkExit()
        {
            if (!DikozemieTownWalk.TryConsumeExit(out DikozemieWalkExit exit, out string note) || !_townOpen)
            {
                return;
            }
            DikozemieLobbyShellIntegration.TraceButtonAction("town walk exit applied " + exit);
            switch (exit)
            {
                case DikozemieWalkExit.Tavern:
                    ShowSection(TownSection.Tavern, note);
                    break;
                case DikozemieWalkExit.Market:
                    DikozemieTown.OpenArmory(false);
                    DikozemieLobbyShellIntegration.OpenMarketFromTown();
                    break;
                case DikozemieWalkExit.Armory:
                    DikozemieTown.OpenArmory(true);
                    DikozemieLobbyShellIntegration.OpenMarketFromTown();
                    break;
                case DikozemieWalkExit.Exchange:
                    ShowSection(TownSection.Trade);
                    OpenSheet(TradeSheet.Exchange);
                    break;
                case DikozemieWalkExit.Contracts:
                    ShowSection(TownSection.Trade);
                    OpenSheet(TradeSheet.Contracts);
                    break;
                case DikozemieWalkExit.Workshops:
                    ShowSection(TownSection.Trade);
                    OpenSheet(TradeSheet.Workshops);
                    break;
                case DikozemieWalkExit.People:
                    ShowSection(TownSection.People, note);
                    break;
                case DikozemieWalkExit.ArenaFights:
                    DikozemieLobbyShellIntegration.OpenVenueFromTown(DikozemieBattleVenue.Arena);
                    break;
                case DikozemieWalkExit.Wanted:
                    ShowSection(TownSection.Wanted, note);
                    break;
                case DikozemieWalkExit.Port:
                    ShowSection(_townNode?.HasPort == true ? TownSection.Port : TownSection.Main, note);
                    break;
                case DikozemieWalkExit.Castle:
                    ShowSection(TownSection.Castle, note);
                    break;
                default:
                    ShowSection(DikozemieTown.IsCastle(_townNode) ? TownSection.Castle : TownSection.Main, note);
                    break;
            }
        }

        // ---- Portraits of the notables ---------------------------------

        private string _townFiguresFor = string.Empty;

        private void ShowTownFigures(DikozemieLobbyMapNode node)
        {
            if (node != null && _townFigureCount > 0 && _townFiguresFor == node.Id)
            {
                return;
            }
            HideTownFigures();
            if (node == null)
            {
                return;
            }
            _townFiguresFor = node.Id;
            foreach (DikozemieNotable notable in DikozemieTown.Notables(node))
            {
                if (_townFigureCount >= TownFigureCount)
                {
                    break;
                }
                DikozemieTownPerson person = DikozemieTownFolk.NotablePerson(node, notable);
                if (person == null || person.Equipment == null)
                {
                    continue;
                }
                try
                {
                    BasicCharacterObject body = person.Body();
                    var visual = new CharacterViewModel();
                    visual.FillFrom(body, person.Seed, string.Empty);
                    visual.SetEquipment(person.Equipment);
                    CharacterTableauWidget widget = _townFigures[_townFigureCount];
                    widget.BodyProperties = DikozemieTownFolk.FaceOf(person).ToString();
                    widget.EquipmentCode = visual.EquipmentCode;
                    widget.CharStringId = visual.CharStringId;
                    widget.IsFemale = person.Female;
                    widget.Race = visual.Race;
                    widget.StanceIndex = visual.StanceIndex;
                    widget.ArmorColor1 = uint.MaxValue;
                    widget.ArmorColor2 = uint.MaxValue;
                    widget.BannerCodeText = visual.BannerCodeText;
                    widget.MountCreationKey = string.Empty;
                    widget.IdleAction = visual.IdleAction;
                    widget.IsTableauEnabled = true;
                    widget.IsVisible = true;
                    _townFigureRoles[_townFigureCount].Text = notable.Role.ToUpperInvariant();
                    _townFigureNames[_townFigureCount].Text = notable.Name;
                    _townFigureRoles[_townFigureCount].IsVisible = true;
                    _townFigureNames[_townFigureCount].IsVisible = true;
                    _townFigureCount++;
                }
                catch (Exception error)
                {
                    Console.WriteLine("DikozemieOnline: town figure failed: " + error.Message);
                }
            }
        }

        private void HideTownFigures()
        {
            for (int index = 0; index < TownFigureCount; index++)
            {
                if (_townFigures[index] != null)
                {
                    _townFigures[index].IsVisible = false;
                    _townFigures[index].IsTableauEnabled = false;
                }
                if (_townFigureRoles[index] != null)
                {
                    _townFigureRoles[index].IsVisible = false;
                    _townFigureNames[index].IsVisible = false;
                }
            }
            _townFigureCount = 0;
        }

        // The notables stand side by side on the right of the picture; the
        // name of the place stays readable on the left.
        private void LayoutTownFigures(float width, float height)
        {
            bool shown = _townFigureCount > 0;
            if (!shown)
            {
                return;
            }
            // The story of the place gives way to its people.
            _townLore.IsVisible = false;
            _townOwner.IsVisible = false;
            float figureHeight = Math.Min(height * 0.74f, 700f);
            float figureWidth = figureHeight * 0.5f;
            float area = width * 0.62f;
            float step = Math.Min(figureWidth * 0.9f, (area - figureWidth) / Math.Max(1, _townFigureCount - 1));
            float total = step * (_townFigureCount - 1) + figureWidth;
            float startX = width - 40f - total;
            float top = height - figureHeight - 92f;
            for (int index = 0; index < _townFigureCount; index++)
            {
                float x = startX + index * step;
                SetFrame(_townFigures[index], x, top, figureWidth, figureHeight);
                SetText(_townFigureRoles[index], x - 30f, height - 88f, figureWidth + 60f, 26f);
                SetText(_townFigureNames[index], x - 30f, height - 62f, figureWidth + 60f, 30f);
            }
        }

        private static string TownStatus()
        {
            var lines = new List<string>();
            foreach (string line in new[]
                     {
                         DikozemieRoads.ClockText + ".", DikozemieRoads.WoundText, DikozemieRoads.CompanyText,
                         DikozemieRoads.BountyText
                     })
            {
                if (!string.IsNullOrEmpty(line))
                {
                    lines.Add(line);
                }
            }
            return "\n" + string.Join("\n", lines);
        }

        // ---- Captivity, bounties, rest ----------------------------------

        private void RefreshCaptivePanel(DikozemieLobbyMapNode node)
        {
            _title.Text = "ПЛЕННИК АРЕНЫ";
            AddOption("ВЫЙТИ НА ПЕСОК", "бой за свободу", () =>
            {
                DikozemieRoads.PrepareCaptiveFight();
                DikozemieLobbyShellIntegration.TraceButtonAction("road captive fight node=" + node.Id);
                DikozemieLobbyShellIntegration.StartRoadBattle(DikozemieBattleVenue.Arena, 1);
            });
            AddOption("ВЫКУПИТЬСЯ", DikozemieRoads.RansomPrice + " МОН.", () =>
            {
                bool free = DikozemieRoads.TryRansom(out string message);
                ShowSection(TownSection.Main, message);
                if (free)
                {
                    OpenTown(node);
                    _townNote = message;
                    RefreshSidePanel();
                }
            });
            if (DikozemieRoads.EscapeTriedToday)
            {
                AddOption("БЕЖАТЬ НОЧЬЮ", "завтра", () => ShowSection(TownSection.Main,
                    "После вчерашнего за вами смотрят в оба. Попробуйте в другую ночь."));
            }
            else
            {
                AddOption("БЕЖАТЬ НОЧЬЮ", DikozemieRoads.EscapeChance + "% · Плутовство", () =>
                {
                    bool free = DikozemieRoads.TryEscape(out string message);
                    if (free)
                    {
                        OpenTown(node);
                    }
                    ShowSection(TownSection.Main, message);
                });
            }
            string seller = DikozemieRoads.CaptiveBy;
            string text = (string.IsNullOrEmpty(seller) ? "Ловцы продали вас распорядителю арены." :
                "Вас продал распорядителю арены победитель на дороге — " + seller + ".") +
                " Здесь пленники дерутся за свободу: " +
                "победите — отпустят и дадут долю ставок (+" + DikozemieRoads.FreedomPurse + " мон.); " +
                "проиграете — заберут треть монет и вышвырнут за ворота.";
            _description.Text = text + "\n\n" +
                (string.IsNullOrEmpty(_townNote)
                    ? "Ваши монеты: " + DikozemieArmoryProgression.HeroResources + "." + TownStatus()
                    : _townNote);
            StackOptions();
        }

        private string WantedText(DikozemieLobbyMapNode node)
        {
            if (DikozemieRoads.HasPrisoner)
            {
                int reward = DikozemieRoads.HandInReward(node);
                AddOption("СДАТЬ ПЛЕННИКА", "+" + reward + " МОН.", () =>
                    ShowSection(TownSection.Wanted, DikozemieRoads.HandIn(node)));
                return "Капитан разглядывает вашего пленника: «" + DikozemieTravelState.Current?.PrisonerName +
                    "? Знаю такого. Сдаёшь — арена его примет, а ты получишь награду.»";
            }
            if (DikozemieRoads.HasBounty)
            {
                return "«Ты уже " + DikozemieRoads.G("взялся", "взялась") + " за " +
                    DikozemieTravelState.Current?.BountyName + ". Логово отмечено " +
                    "на твоей карте. Привези его живым.»";
            }
            DikozemieRoads.Bounty offer = DikozemieRoads.OfferAt(node);
            if (offer == null)
            {
                return "«Сегодня никого не ищем. Загляни завтра.»";
            }

            AddOption("ВЗЯТЬСЯ", "+" + offer.Reward + " МОН.", () =>
            {
                DikozemieRoads.Accept(offer);
                ShowSection(TownSection.Wanted, "«По рукам. Его логово у " + offer.Near +
                    " — я отметил на твоей карте. Ночью они спят, днём в дозоре. Не ходи " + DikozemieRoads.G("один", "одна") + ".»");
            });
            return "Капитан стражи стучит пальцем по листу с рожей: «" + offer.Name + ". С ним " +
                DikozemieRoads.Plural(offer.GangRole, offer.Gang) +
                ". Грабят путников у " + offer.Near + ". Живым — " + offer.Reward + " монет.»\n\n" +
                "Атаман — бывалый рубака. В одиночку это верная смерть: наймите охрану в таверне " +
                "или идите ночью, когда шайка спит.";
        }

        private static string RestDetail()
        {
            int coins = DikozemieArmoryProgression.HeroResources;
            if (coins < DikozemieRoads.TavernBedPrice)
            {
                return "в конюшне, даром";
            }
            return DikozemieRoads.TavernBedPrice + " МОН." + (DikozemieRoads.Wound > 0 ? " · раны" : " · до утра");
        }

        private void Rest(TownSection back)
        {
            bool paid = DikozemieArmoryProgression.TrySpendHeroResources(DikozemieRoads.TavernBedPrice, out _, "bed", "ночлег в таверне");
            float hours = DikozemieRoads.IsNight || DikozemieRoads.HourOfDay >= 17f
                ? DikozemieRoads.HoursToMorning()
                : 8f;
            int before = DikozemieRoads.Wound;
            DikozemieRoads.Advance(hours);
            if (paid || DikozemieRoads.Wound == 1)
            {
                DikozemieRoads.HealOneLevel();
            }
            DikozemieTravelState.Save();
            string wounds = before == 0
                ? string.Empty
                : DikozemieRoads.Wound == 0 ? " Раны затянулись." : " Раны ещё болят, но легче.";
            ShowSection(back, (paid
                ? "Вы заплатили за угол и тюфяк (-" + DikozemieRoads.TavernBedPrice + ") и проспали "
                : "Хозяин пустил вас спать в конюшню. Вы проспали ") +
                (int)Math.Round(hours) + " ч." + wounds + "\n" + DikozemieRoads.ClockText + ".");
            DikozemieLobbyShellIntegration.TraceButtonAction("town rest hours=" + hours + " paid=" + paid +
                " wound=" + DikozemieRoads.Wound);
        }

        private void Heal()
        {
            if (DikozemieRoads.Wound <= 0)
            {
                ShowSection(TownSection.Main, "Знахарка оглядела вас: «" +
                    DikozemieRoads.G("Здоров как бык", "Здорова как ломовая лошадь") + ". Иди, не мешай.»");
                return;
            }
            if (!DikozemieArmoryProgression.TrySpendHeroResources(DikozemieRoads.HealerPrice, out int coins, "healer", "перевязка у знахарки"))
            {
                ShowSection(TownSection.Main, "«Травы денег стоят.» Нужно " + DikozemieRoads.HealerPrice +
                    " мон., у вас " + coins + ".");
                return;
            }
            DikozemieRoads.HealOneLevel();
            DikozemieRoads.Advance(1f);
            DikozemieTravelState.Save();
            ShowSection(TownSection.Main, "Знахарка промыла раны отваром, наложила мазь и тугую повязку (-" +
                DikozemieRoads.HealerPrice + "). " + (DikozemieRoads.Wound == 0
                    ? "Через час вы на ногах."
                    : "Ещё болит, но идти уже можно."));
        }

        // The menu stands at the bottom of the panel, first option on top.
        private void StackOptions()
        {
            // Nine or ten choices close up a little to leave room for the text.
            float step = _optionCount > 8 ? TownOptionStep - 7f : TownOptionStep;
            for (int index = 0; index < _optionCount; index++)
            {
                _townOptions[index].SuggestedHeight = _optionCount > 8 ? TownOptionHeight - 6f : TownOptionHeight;
                _townOptions[index].MarginBottom = 30f + (_optionCount - 1 - index) * step;
            }
        }

        // The trade yard of a town; the elder's and the lord's errands
        // elsewhere. The table itself is on the left (DikozemieLobbyMapTrade).
        private void BuildTradeOptions(DikozemieLobbyMapNode node, out string text)
        {
            bool town = DikozemieTown.IsTown(node);
            bool castle = DikozemieTown.IsCastle(node);
            _title.Text = town ? "ТОРГОВЫЙ ДВОР" : castle ? "ЗАЛ ЛОРДА" : "СТАРОСТА";
            text = TradeSummary();
            if (town)
            {
                AddOption("БИРЖА", "товары города", () => OpenSheet(TradeSheet.Exchange));
                AddOption("ПОРУЧЕНИЯ КУПЦОВ", ContractsDetail(node), () => OpenSheet(TradeSheet.Contracts));
                AddOption("МАСТЕРСКИЕ", WorkshopsDetail(), () => OpenSheet(TradeSheet.Workshops));
                AddOption("КАРАВАНЫ", CaravansDetail(), () => OpenSheet(TradeSheet.Caravans));
            }
            else
            {
                AddOption(castle ? "ДЕЛА ЛОРДА" : "ПОРУЧЕНИЕ", string.Empty, () => OpenSheet(TradeSheet.Contracts));
            }
            AddOption("КНИГА КАЗНЫ", string.Empty, () => OpenSheet(TradeSheet.Ledger));
            DikozemieStoryInfo story = DikozemieEconomy.State?.Story;
            if (story != null && !story.Done && story.Step > 0)
            {
                AddOption("ПИСЬМО ОТЦА", story.Title.ToLowerInvariant(), () => OpenSheet(TradeSheet.Letter));
            }
            AddOption("НАЗАД", string.Empty, () => ShowSection(castle ? TownSection.Castle : TownSection.Main));
        }

        private static string TradeYardDetail()
        {
            DikozemieEconomyState state = DikozemieEconomy.State;
            if (!DikozemieEconomy.IsOnline || state == null)
            {
                return "биржа, купцы";
            }
            return state.Load > 0 ? "обоз " + state.Load + " из " + state.Capacity : "биржа, купцы";
        }

        private static string ContractsDetail(DikozemieLobbyMapNode node)
        {
            DikozemieEconomyState state = DikozemieEconomy.State;
            int mine = state?.Contracts.Count ?? 0;
            int here = DikozemieEconomy.CachedMarket(node?.Id)?.Contracts.Count ?? 0;
            return mine > 0 ? "ваших: " + mine : here > 0 ? "есть: " + here : string.Empty;
        }

        private static string WorkshopsDetail()
        {
            int mine = DikozemieEconomy.State?.Workshops.Count ?? 0;
            return mine > 0 ? "ваших: " + mine : "от 2 400 мон.";
        }

        private static string CaravansDetail()
        {
            int mine = DikozemieEconomy.State?.Caravans.Count ?? 0;
            return mine > 0 ? "ваших: " + mine : "от 1 500 мон.";
        }

        // Purse, pack and treasury under the title of a trade screen.
        private static string TradeSummary()
        {
            DikozemieEconomyState state = DikozemieEconomy.State;
            if (!DikozemieEconomy.IsOnline || state == null)
            {
                return DikozemieEconomy.StatusText;
            }
            var lines = new List<string>
            {
                "Монеты: " + FormatCoins(DikozemieArmoryProgression.HeroResources) + ".",
                "Обоз: " + state.Load + " из " + state.Capacity + " мест" +
                    (state.Load > 0 ? " — " + string.Join(", ", state.Cargo.Where(c => c.Value > 0)
                        .Select(c => DikozemieEconomy.GoodName(c.Key).ToLowerInvariant() + " " + c.Value)) : string.Empty) + "."
            };
            if (state.Capacity <= 2)
            {
                lines.Add("Пешком много не унести: купите на рынке вьючную лошадь или мула — ещё четыре места.");
            }
            if (state.Contracts.Count > 0)
            {
                lines.Add("Поручений: " + state.Contracts.Count + ".");
            }
            return string.Join("\n", lines);
        }

        private void BuildPortOptions(out string text)
        {
            DikozemieTravelSave save = _travel;
            int vessel = save?.Vessel ?? DikozemieTravelState.NoVessel;
            text = "Скрип снастей, запах смолы и рыбы. Здесь нанимают матросов, " +
                "продают места на кораблях и сами корабли.";
            AddOption("НАНЯТЬСЯ МАТРОСОМ", "морской бой", () =>
                DikozemieLobbyShellIntegration.OpenVenueFromTown(DikozemieBattleVenue.Naval));
            AddOption("МЕСТО НА КОРАБЛЕ", "выбрать порт", StartPassagePick);
            AddOption("КУПЕЧЕСКИЙ КОРАБЛЬ", "вклады", () =>
            {
                ShowSection(TownSection.Commenda);
                OpenSheet(TradeSheet.Commenda);
            });
            if (vessel != DikozemieTravelState.NoVessel)
            {
                AddOption("СЕСТЬ В " + (vessel == DikozemieTravelState.Ship ? "ЛАДЬЮ" : "ЧЕЛН"),
                    "свой", BoardOwnVessel);
            }
            if (vessel == DikozemieTravelState.NoVessel)
            {
                AddOption("КУПИТЬ ЧЕЛН", DikozemieTravelState.SmallBoatPrice + " МОН.",
                    () => ConfirmVessel(DikozemieTravelState.SmallBoat));
            }
            if (vessel != DikozemieTravelState.Ship)
            {
                AddOption("КУПИТЬ ЛАДЬЮ", FormatCoins(DikozemieTravelState.ShipPrice) + " МОН.",
                    () => ConfirmVessel(DikozemieTravelState.Ship));
            }
            AddOption("НАЗАД", string.Empty, () => ShowSection(TownSection.Main));
        }

        internal static string FormatCoins(int coins)
        {
            string digits = coins.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var result = new System.Text.StringBuilder();
            for (int index = 0; index < digits.Length; index++)
            {
                if (index > 0 && (digits.Length - index) % 3 == 0)
                {
                    result.Append(' ');
                }
                result.Append(digits[index]);
            }
            return result.ToString();
        }

        private void BuyRumor()
        {
            if (!DikozemieArmoryProgression.TrySpendHeroResources(DikozemieTown.RumorPrice, out int coins, "rumor", "угостил завсегдатаев"))
            {
                ShowSection(TownSection.Tavern, "Без монет никто не разговорится: нужно " +
                    DikozemieTown.RumorPrice + ", у вас " + coins + ".");
                return;
            }
            string rumor = DikozemieTown.Rumor(_townNode, _rumorIndex++);
            DikozemieLobbyShellIntegration.TraceButtonAction("town rumor coinsLeft=" + coins);
            ShowSection(TownSection.Tavern, "Вы угостили завсегдатаев (-" + DikozemieTown.RumorPrice +
                "). Один наклоняется к вам:\n" + rumor);
        }

        private void BuyCastlePass()
        {
            DikozemieTown.TryBuyCastlePass(_townNode, out string message);
            ShowSection(TownSection.Castle, message);
        }

        private void ConfirmVessel(int vessel)
        {
            int price = vessel == DikozemieTravelState.Ship
                ? DikozemieTravelState.ShipPrice
                : DikozemieTravelState.SmallBoatPrice;
            int coins = DikozemieArmoryProgression.HeroResources;
            string what = vessel == DikozemieTravelState.Ship
                ? "Ладья: киль, двадцать пар вёсел, парус и снасти. Вдвое быстрее челна. " +
                    "Столько стоит поместье с землёй: простому человеку не накопить за всю жизнь."
                : "Челн: лодка из нескольких досок с вёслами и маленьким парусом. " +
                    "Медленная, но своя: плыть можно куда и когда угодно.";
            if (coins < price)
            {
                ShowSection(TownSection.Port, what + "\n\nЦена " + FormatCoins(price) +
                    " монет, у вас " + coins + ".");
                return;
            }
            Inquiry(
                vessel == DikozemieTravelState.Ship ? "КУПИТЬ ЛАДЬЮ" : "КУПИТЬ ЧЕЛН",
                what + "\n\nЦена " + FormatCoins(price) + " монет, у вас " + coins + ".",
                "КУПИТЬ",
                "НЕТ",
                () => BuyVessel(vessel, price),
                null);
        }

        private void BuyVessel(int vessel, int price)
        {
            if (_travel == null)
            {
                return;
            }
            if (!DikozemieArmoryProgression.TrySpendHeroResources(price, out int coins, "vessel",
                    vessel == DikozemieTravelState.Ship ? "ладья" : "челн"))
            {
                ShowSection(TownSection.Port, "Не хватает монет: нужно " + FormatCoins(price) +
                    ", у вас " + coins + ".");
                return;
            }
            _travel.Vessel = vessel;
            _travel.OwnsBoat = true;
            _travel.VesselPaid = true;
            DikozemieTravelState.Save();
            DikozemieLobbyShellIntegration.TraceButtonAction(
                "map-vessel bought kind=" + vessel + " price=" + price + " coinsLeft=" + coins +
                " node=" + _travel.NodeId);
            ShowSection(TownSection.Port, (vessel == DikozemieTravelState.Ship ? "Ладья" : "Челн") +
                " ваш. Садитесь в любом порту: «Порт» → «Сесть».");
        }

        private void BoardOwnVessel()
        {
            if (_travel == null || _travel.Vessel == DikozemieTravelState.NoVessel)
            {
                return;
            }
            CloseTown();
            _travel.PassengerTo = string.Empty;
            ToggleBoat();
        }

        // «Место на корабле»: the map opens, the player picks the port.
        private void StartPassagePick()
        {
            _townOpen = false;
            DikozemieTown.IsTownScreenOpen = false;
            _passagePick = true;
            _hasSelectedPoint = false;
            _selectedNode = null;
            _route = null;
            _travelNote = string.Empty;
            // Ships go only to the ports of the same water: a lake town
            // sees the towns of its lake, a sea port those of the sea.
            _passagePorts.Clear();
            _passagePortNames.Clear();
            DikozemieLobbyMapNode here = NodeHere();
            int body = _travel == null ? 0 : DikozemieTravelTerrain.WaterBodyNear(_travel.X, _travel.Y);
            if (body != 0)
            {
                foreach (DikozemieLobbyMapNode node in DikozemieLobbyMapCatalog.Nodes)
                {
                    if (node?.Position == null || !node.HasPort || !DikozemieTown.IsTown(node) ||
                        ReferenceEquals(node, here) ||
                        DikozemieTravelTerrain.WaterBodyNear(node.Position.X, node.Position.Y) != body)
                    {
                        continue;
                    }
                    _passagePorts.Add(node.Id);
                    _passagePortNames.Add(DisplayNodeName(node));
                }
            }
            _passagePortNames.Sort(StringComparer.CurrentCulture);
            DikozemieLobbyShellIntegration.TraceButtonAction(
                "passage pick node=" + _travel?.NodeId + " ports=" + _passagePorts.Count);
            RefreshSidePanel();
            LayoutContent();
        }

        private readonly HashSet<string> _passagePorts =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _passagePortNames = new List<string>();

        internal bool IsPassagePort(DikozemieLobbyMapNode node) =>
            _passagePick && node != null && _passagePorts.Contains(node.Id);

        private string PassagePortList()
        {
            return _passagePortNames.Count == 0
                ? "Отсюда корабли никуда не ходят: эта вода ни с чем не связана."
                : "Отсюда корабли ходят в: " + string.Join(", ", _passagePortNames) +
                    ". Эти порты отмечены на карте голубым.";
        }

        // The side panel while the player chooses the port of a passage.
        private void RefreshPassagePanel()
        {
            SetMapPanelVisible(false);
            HideTownOptions();
            _optionCount = 0;
            _title.Text = "МЕСТО НА КОРАБЛЕ";
            DikozemieLobbyMapNode target = _selectedNode;
            string text;
            if (target == null || !target.HasPort || !DikozemieTown.IsTown(target))
            {
                text = (target != null ? DisplayNodeName(target) + " — не порт.\n\n" : string.Empty) +
                    "Нажмите на портовый город на карте.\n\n" + PassagePortList();
            }
            else if (ReferenceEquals(target, NodeHere()))
            {
                text = "Вы уже здесь. Выберите другой порт.";
            }
            else
            {
                DikozemieTravelRoute route = DikozemieTravelRouter.Find(
                    _travel.X, _travel.Y, target.Position.X, target.Position.Y, true);
                if (route == null)
                {
                    text = "В " + DisplayNodeName(target) + " корабли отсюда не ходят: другая вода.\n\n" +
                        PassagePortList();
                }
                else
                {
                    bool horse = DikozemieHorseUpkeep.DailyCost > 0;
                    int fare = DikozemieTravelState.PassageFare(route.Length, horse);
                    float seconds = route.Length / DikozemieTravelState.PassageSpeed;
                    text = DisplayNodeName(target) + ": " + FormatTime(seconds) + " пути, место на корабле — " +
                        fare + " мон." + (horse ? " С конём вчетверо дороже: его тоже везут." : string.Empty) +
                        "\nВаши монеты: " + DikozemieArmoryProgression.HeroResources + ".";
                    DikozemieTravelRoute captured = route;
                    AddOption("ОПЛАТИТЬ И ОТПЛЫТЬ", fare + " МОН.", () => StartPassage(target, captured, fare));
                }
            }
            AddOption("ОТМЕНА", string.Empty, () =>
            {
                _passagePick = false;
                _hasSelectedPoint = false;
                _selectedNode = null;
                _route = null;
                OpenTown(NodeHere());
            });
            _description.Text = text;
            StackOptions();
        }

        private void StartPassage(DikozemieLobbyMapNode target, DikozemieTravelRoute route, int fare)
        {
            if (_travel == null || target == null || route == null)
            {
                return;
            }
            if (!DikozemieArmoryProgression.TrySpendHeroResources(fare, out int coins, "passage",
                    "место на корабле до " + DikozemieTown.DisplayName(target)))
            {
                _description.Text = "Не хватает монет на место: нужно " + fare + ", у вас " + coins + ".";
                return;
            }
            _passagePick = false;
            _travel.PassengerTo = target.Id;
            _travel.Boat = true;
            _selectedNode = target;
            _hasSelectedPoint = true;
            _selectedX = target.Position.X;
            _selectedY = target.Position.Y;
            _route = route;
            _routeIndex = 0;
            DikozemieTravelState.Save();
            DikozemieLobbyShellIntegration.TraceButtonAction(
                "passage paid to=" + target.Id + " fare=" + fare + " coinsLeft=" + coins);
            ToggleTravel();
            _travelNote = "Вы на борту. Корабль идёт в " + DisplayNodeName(target) + ".";
            RefreshSidePanel();
        }
    }
}
