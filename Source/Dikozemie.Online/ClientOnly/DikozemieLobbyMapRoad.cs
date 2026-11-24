using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets;
using TaleWorlds.TwoDimension;

namespace Dikozemie.Online
{
    // A road encounter on the map, as the singleplayer encounter menu: the
    // people who stopped the hero stand in the picture in their real gear,
    // the panel on the right says what is going on and what can be done.
    public sealed partial class DikozemieLobbyMapWidget
    {
        private const int RoadFigureCount = 4;

        private Widget _road;
        private Widget _roadView;
        private readonly List<Widget> _roadBands = new List<Widget>();
        private readonly Widget[] _roadFrame = new Widget[4];
        private readonly CharacterTableauWidget[] _roadFigures = new CharacterTableauWidget[RoadFigureCount];
        private TextWidget _roadTitle;
        private TextWidget _roadSubtitle;
        private TextWidget _roadCaption;
        private Widget _nightShade;
        private Widget _lairHalo;
        private Widget _lairDot;
        private TextWidget _lairLabel;
        private bool _roadOpen;
        private DikozemieRoadEncounter _roadCard;
        private DikozemieRoadOutcome _roadOutcome;
        private string _roadNote = string.Empty;
        private int _figureCount;
        private bool _centerOnHero;

        private static readonly Color RoadRed = new Color(0.93f, 0.42f, 0.32f, 1f);

        // The card the hero went to fight: the map widget may be built anew
        // while the field is open, the card must survive that.
        private static DikozemieRoadEncounter _fightCard;

        private void BuildRoadLayer()
        {
            _road = new Widget(Context)
            {
                Id = "RoadScreen",
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                ClipContents = true,
                DoNotAcceptEvents = true,
                IsVisible = false
            };
            AddChild(_road);
            _road.AddChild(CreateSolid("RoadDim", new Color(0.01f, 0.008f, 0.006f, 0.72f)));
            _roadView = new Widget(Context)
            {
                Id = "RoadView",
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                ClipContents = true,
                DoNotAcceptEvents = true
            };
            _road.AddChild(_roadView);
            // A dark stage, light falling from above: bands of colour make
            // the gradient Gauntlet does not have here.
            for (int index = 0; index < 14; index++)
            {
                Widget band = CreateSolid("RoadBand" + index, new Color(0.05f, 0.04f, 0.03f, 1f));
                band.WidthSizePolicy = SizePolicy.StretchToParent;
                band.HeightSizePolicy = SizePolicy.Fixed;
                band.VerticalAlignment = VerticalAlignment.Top;
                _roadView.AddChild(band);
                _roadBands.Add(band);
            }
            for (int index = 0; index < RoadFigureCount; index++)
            {
                var figure = new CharacterTableauWidget(Context)
                {
                    Id = "RoadFigure" + index,
                    WidthSizePolicy = SizePolicy.Fixed,
                    HeightSizePolicy = SizePolicy.Fixed,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    DoNotAcceptEvents = true,
                    IsVisible = false
                };
                _roadView.AddChild(figure);
                _roadFigures[index] = figure;
            }
            for (int index = 0; index < _roadFrame.Length; index++)
            {
                _roadFrame[index] = CreateSolid("RoadFrame" + index, new Color(0.72f, 0.47f, 0.17f, 0.9f));
                _roadFrame[index].WidthSizePolicy = SizePolicy.Fixed;
                _roadFrame[index].HeightSizePolicy = SizePolicy.Fixed;
                _roadFrame[index].HorizontalAlignment = HorizontalAlignment.Left;
                _roadFrame[index].VerticalAlignment = VerticalAlignment.Top;
                _roadView.AddChild(_roadFrame[index]);
            }
            _roadTitle = CreateRoadText("RoadTitle", 54, TownGold);
            _roadSubtitle = CreateRoadText("RoadSubtitle", 20, new Color(0.98f, 0.83f, 0.42f, 1f));
            _roadCaption = CreateRoadText("RoadCaption", 19, TownText);
        }

        private TextWidget CreateRoadText(string id, int size, Color color)
        {
            TextWidget text = CreateText(id, string.Empty, size, color);
            text.WidthSizePolicy = SizePolicy.Fixed;
            text.HeightSizePolicy = SizePolicy.Fixed;
            text.HorizontalAlignment = HorizontalAlignment.Left;
            text.VerticalAlignment = VerticalAlignment.Top;
            text.Brush.TextHorizontalAlignment = TextHorizontalAlignment.Left;
            text.Brush.TextVerticalAlignment = TextVerticalAlignment.Top;
            text.Brush.DefaultStyle.TextGlowColor = new Color(0f, 0f, 0f, 1f);
            text.Brush.DefaultStyle.TextOutlineColor = new Color(0f, 0f, 0f, 0.9f);
            text.Brush.DefaultStyle.TextOutlineAmount = 0.12f;
            _roadView.AddChild(text);
            return text;
        }

        // The darkness of the night over the map, under the names of places.
        private void BuildNightShade()
        {
            _nightShade = CreateSolid("MapNight", new Color(0.02f, 0.03f, 0.08f, 0f));
            _nightShade.WidthSizePolicy = SizePolicy.Fixed;
            _nightShade.HeightSizePolicy = SizePolicy.Fixed;
            _nightShade.HorizontalAlignment = HorizontalAlignment.Left;
            _nightShade.VerticalAlignment = VerticalAlignment.Top;
            _nightShade.IsVisible = false;
            AddChild(_nightShade);
        }

        // The hideout of a wanted man.
        private void BuildLairMarker()
        {
            _lairHalo = new Widget(Context)
            {
                Id = "LairHalo",
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                SuggestedWidth = 34f,
                SuggestedHeight = 34f,
                Sprite = Context.SpriteData.GetSprite("BlankWhiteCircle"),
                Color = new Color(0.55f, 0.06f, 0.04f, 0.45f),
                DoNotAcceptEvents = true,
                IsVisible = false
            };
            AddChild(_lairHalo);
            _lairDot = new Widget(Context)
            {
                Id = "LairDot",
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                SuggestedWidth = 14f,
                SuggestedHeight = 14f,
                Sprite = Context.SpriteData.GetSprite("BlankWhiteCircle"),
                Color = new Color(0.86f, 0.12f, 0.08f, 1f),
                DoNotAcceptEvents = true,
                IsVisible = false
            };
            AddChild(_lairDot);
            _lairLabel = CreateText("LairLabel", string.Empty, 17, new Color(0.45f, 0.04f, 0.02f, 1f));
            _lairLabel.WidthSizePolicy = SizePolicy.Fixed;
            _lairLabel.HeightSizePolicy = SizePolicy.Fixed;
            _lairLabel.HorizontalAlignment = HorizontalAlignment.Left;
            _lairLabel.VerticalAlignment = VerticalAlignment.Top;
            _lairLabel.SuggestedWidth = 260f;
            _lairLabel.SuggestedHeight = 26f;
            _lairLabel.Brush.TextHorizontalAlignment = TextHorizontalAlignment.Left;
            _lairLabel.Brush.DefaultStyle.TextGlowColor = new Color(0.98f, 0.92f, 0.78f, 1f);
            _lairLabel.Brush.DefaultStyle.TextGlowRadius = 0.2f;
            _lairLabel.Brush.DefaultStyle.TextBlur = 0.6f;
            _lairLabel.IsVisible = false;
            AddChild(_lairLabel);
        }

        private void LayoutNight(float mapX, float mapY, float mapWidth, float mapHeight,
            float viewportX, float viewportY, float viewportWidth, float viewportHeight)
        {
            if (_nightShade == null)
            {
                return;
            }
            float darkness = DikozemieTravelState.Current == null ? 0f : DikozemieRoads.Darkness;
            _nightShade.IsVisible = darkness > 0.01f;
            if (!_nightShade.IsVisible)
            {
                return;
            }
            float left = Math.Max(mapX, viewportX);
            float top = Math.Max(mapY, viewportY);
            float right = Math.Min(mapX + mapWidth, viewportX + viewportWidth);
            float bottom = Math.Min(mapY + mapHeight, viewportY + viewportHeight);
            _nightShade.PositionXOffset = left;
            _nightShade.PositionYOffset = top;
            _nightShade.SuggestedWidth = Math.Max(0f, right - left);
            _nightShade.SuggestedHeight = Math.Max(0f, bottom - top);
            _nightShade.Color = new Color(0.02f, 0.03f, 0.09f, 0.42f * darkness);
        }

        private void LayoutLair(float mapX, float mapY, float mapWidth, float mapHeight,
            float viewportX, float viewportY, float viewportWidth, float viewportHeight)
        {
            if (_lairDot == null)
            {
                return;
            }
            DikozemieTravelSave save = DikozemieTravelState.Current;
            bool show = save != null && DikozemieRoads.HasBounty && !_townOpen && !_roadOpen;
            float x = 0f;
            float y = 0f;
            if (show)
            {
                x = mapX + save.BountyX / SourceWidth * mapWidth;
                y = mapY + save.BountyY / SourceHeight * mapHeight;
                show = x >= viewportX && y >= viewportY &&
                    x <= viewportX + viewportWidth && y <= viewportY + viewportHeight;
            }
            _lairHalo.IsVisible = show;
            _lairDot.IsVisible = show;
            _lairLabel.IsVisible = show;
            if (!show)
            {
                return;
            }
            _lairHalo.PositionXOffset = x - 17f;
            _lairHalo.PositionYOffset = y - 17f;
            _lairDot.PositionXOffset = x - 7f;
            _lairDot.PositionYOffset = y - 7f;
            _lairLabel.Text = "Логово: " + save.BountyName;
            _lairLabel.PositionXOffset = x + 14f;
            _lairLabel.PositionYOffset = y - 13f;
        }

        // ---- Showing an encounter --------------------------------------

        private void ShowEncounter(DikozemieRoadEncounter card)
        {
            if (card == null)
            {
                return;
            }
            StopWalking();
            DikozemieTravelState.Save();
            CloseLayersForRoad();
            _townOpen = false;
            _passagePick = false;
            DikozemieTown.IsTownScreenOpen = false;
            _roadOpen = true;
            _roadCard = card;
            _roadOutcome = null;
            _roadNote = string.Empty;
            DikozemieLobbyShellIntegration.TraceButtonAction("road encounter " + card.Kind + " foes=" +
                card.Foes.Count + " night=" + card.Night + " spotted=" + card.Spotted);
            SetFigures(card.Foes.Count > 0 ? card.Foes : card.Figures);
            RefreshRoadScene();
            RefreshSidePanel();
            LayoutContent();
        }

        private void SetFigures(List<DikozemieRoadFighter> fighters)
        {
            _figureCount = 0;
            for (int index = 0; index < RoadFigureCount; index++)
            {
                CharacterTableauWidget widget = _roadFigures[index];
                DikozemieRoadFighter fighter = fighters != null && index < fighters.Count ? fighters[index] : null;
                if (fighter == null || fighter.Equipment == null)
                {
                    widget.IsVisible = false;
                    continue;
                }
                try
                {
                    BasicCharacterObject body = fighter.FaceCharacter();
                    var visual = new CharacterViewModel();
                    visual.FillFrom(body, fighter.Seed, string.Empty);
                    visual.SetEquipment(fighter.Equipment);
                    visual.BodyProperties = body.GetBodyProperties(fighter.Equipment, fighter.Seed).ToString();
                    visual.IsFemale = false;
                    widget.BodyProperties = visual.BodyProperties;
                    widget.EquipmentCode = visual.EquipmentCode;
                    widget.CharStringId = visual.CharStringId;
                    widget.IsFemale = false;
                    widget.Race = visual.Race;
                    widget.StanceIndex = visual.StanceIndex;
                    // Outlaws wear their rags as they are; the colours of a
                    // people belong to its guards, escorts and merchants.
                    bool people = fighter.Role.StartsWith("guard_", StringComparison.Ordinal) ||
                        fighter.Role.StartsWith("escort_", StringComparison.Ordinal) ||
                        fighter.Role.StartsWith("merchant_", StringComparison.Ordinal) ||
                        fighter.Role.StartsWith("pilgrim_", StringComparison.Ordinal);
                    widget.ArmorColor1 = people ? visual.ArmorColor1 : uint.MaxValue;
                    widget.ArmorColor2 = people ? visual.ArmorColor2 : uint.MaxValue;
                    widget.BannerCodeText = visual.BannerCodeText;
                    ItemObject horse = fighter.Equipment[EquipmentIndex.Horse].Item;
                    widget.MountCreationKey = horse == null
                        ? string.Empty
                        : MountCreationKey.GetRandomMountKeyString(horse, fighter.Seed);
                    widget.IdleAction = visual.IdleAction;
                    widget.IsTableauEnabled = true;
                    widget.IsVisible = true;
                    _figureCount++;
                    DikozemieLobbyShellIntegration.TraceButtonAction("road figure " + index + " " + fighter.Role +
                        " seed=" + fighter.Seed + " body=" + visual.BodyProperties.GetHashCode() +
                        " gear=" + visual.EquipmentCode?.GetHashCode());
                }
                catch (Exception error)
                {
                    widget.IsVisible = false;
                    Console.WriteLine("DikozemieOnline: road figure failed: " + error.Message);
                }
            }
        }

        private void SetHeroFigure()
        {
            HideFigures();
            if (!DikozemieHeroProfile.IsCreated)
            {
                return;
            }
            try
            {
                DikozemieHeroProfileState profile = DikozemieHeroProfile.Snapshot;
                BasicCharacterObject body = TaleWorlds.ObjectSystem.MBObjectManager.Instance
                    .GetObject<BasicCharacterObject>(DikozemieRoadFolk.FaceTroop(profile.CultureId));
                Equipment equipment = DikozemieHeroProfile.CreateDisplayEquipment(body);
                if (body == null || equipment == null)
                {
                    return;
                }
                var visual = new CharacterViewModel();
                visual.FillFrom(body, -1, string.Empty);
                visual.SetEquipment(equipment);
                CharacterTableauWidget widget = _roadFigures[0];
                widget.BodyProperties = string.IsNullOrEmpty(profile.BodyProperties)
                    ? visual.BodyProperties
                    : profile.BodyProperties;
                widget.EquipmentCode = visual.EquipmentCode;
                widget.CharStringId = visual.CharStringId;
                widget.IsFemale = profile.IsFemale;
                widget.Race = Math.Max(0, profile.Race);
                widget.StanceIndex = visual.StanceIndex;
                widget.ArmorColor1 = uint.MaxValue;
                widget.ArmorColor2 = uint.MaxValue;
                widget.BannerCodeText = visual.BannerCodeText;
                ItemObject horse = equipment[EquipmentIndex.Horse].Item;
                widget.MountCreationKey = horse == null
                    ? string.Empty
                    : MountCreationKey.GetRandomMountKeyString(horse, 7);
                widget.IdleAction = visual.IdleAction;
                widget.IsTableauEnabled = true;
                widget.IsVisible = true;
                _figureCount = 1;
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: road hero figure failed: " + error.Message);
            }
        }

        private void HideFigures()
        {
            foreach (CharacterTableauWidget widget in _roadFigures)
            {
                if (widget != null)
                {
                    widget.IsVisible = false;
                }
            }
            _figureCount = 0;
        }

        private void RefreshRoadScene()
        {
            DikozemieRoadEncounter card = _roadCard;
            if (card == null)
            {
                return;
            }
            DikozemieRoadOutcome outcome = _roadOutcome;
            _roadTitle.Text = outcome != null && !string.IsNullOrEmpty(outcome.Title)
                ? outcome.Title.ToUpperInvariant()
                : card.Title;
            var place = new List<string>();
            DikozemieTravelSave save = _travel;
            if (save != null)
            {
                byte cell = DikozemieTravelTerrain.AtAtlas(save.X, save.Y);
                place.Add((cell & DikozemieTravelTerrain.Road) != 0 ? "ДОРОГА" :
                    (cell & DikozemieTravelTerrain.Forest) != 0 ? "ЛЕС" : "БЕЗДОРОЖЬЕ");
            }
            place.Add(GetCultureName(card.Culture));
            place.Add(DikozemieRoads.ClockText.ToUpperInvariant());
            if (card.Spotted && outcome == null)
            {
                place.Add("ВЫ ЗАМЕТИЛИ ИХ ПЕРВЫМИ");
            }
            _roadSubtitle.Text = string.Join("  ·  ", place);
            bool foes = card.Foes.Count > 0;
            var who = (foes ? card.Foes : card.Figures)
                .Where(fighter => fighter != null)
                .Select(fighter => fighter.Name +
                    (!foes || string.IsNullOrEmpty(fighter.Arms) ? string.Empty : " — " + fighter.Arms))
                .ToList();
            _roadCaption.Text = outcome == null ? string.Join("     ", who) : string.Empty;
        }

        private void LayoutRoad(float x, float y, float width, float height)
        {
            if (_road == null)
            {
                return;
            }
            _road.IsVisible = _roadOpen;
            if (!_roadOpen)
            {
                return;
            }
            _road.PositionXOffset = 0f;
            _road.PositionYOffset = 0f;
            _road.SuggestedWidth = Math.Max(width + x, ViewWidth - SidePanelWidth - OuterMargin);
            _road.SuggestedHeight = ViewHeight;
            _roadView.PositionXOffset = x;
            _roadView.PositionYOffset = y;
            _roadView.SuggestedWidth = width;
            _roadView.SuggestedHeight = height;

            bool night = DikozemieRoads.Darkness > 0.5f;
            float bandHeight = height / _roadBands.Count + 1f;
            for (int index = 0; index < _roadBands.Count; index++)
            {
                float t = (float)index / (_roadBands.Count - 1);
                Widget band = _roadBands[index];
                band.SuggestedHeight = bandHeight;
                band.PositionYOffset = index * (bandHeight - 1f);
                // Warm dusk above, the dark ground below; bluish at night.
                float glow = (float)Math.Sin(Math.PI * Math.Min(1f, t * 1.4f)) * 0.08f;
                band.Color = night
                    ? new Color(0.03f + glow * 0.2f, 0.04f + glow * 0.35f, 0.08f + glow * 0.7f, 1f)
                    : new Color(0.08f + glow, 0.055f + glow * 0.6f, 0.035f + glow * 0.3f, 1f);
            }

            const float line = 2f;
            SetFrame(_roadFrame[0], 0f, 0f, width, line);
            SetFrame(_roadFrame[1], 0f, height - line, width, line);
            SetFrame(_roadFrame[2], 0f, 0f, line, height);
            SetFrame(_roadFrame[3], width - line, 0f, line, height);

            // The figures stand side by side, a little overlapping, the
            // leader in the middle.
            float figureHeight = Math.Min(height * 0.82f, 760f);
            float figureWidth = figureHeight * 0.62f;
            float step = Math.Min(figureWidth * 0.72f, (width - 120f) / Math.Max(1, _figureCount));
            float total = step * (_figureCount - 1) + figureWidth;
            float startX = (width - total) * 0.5f;
            int shown = 0;
            for (int index = 0; index < RoadFigureCount; index++)
            {
                CharacterTableauWidget widget = _roadFigures[index];
                if (!widget.IsVisible)
                {
                    continue;
                }
                widget.SuggestedWidth = figureWidth;
                widget.SuggestedHeight = figureHeight;
                widget.PositionXOffset = startX + shown * step;
                widget.PositionYOffset = height - figureHeight - 150f;
                shown++;
            }

            float left = 44f;
            float textWidth = Math.Max(300f, width - left * 2f);
            // Above the game's own messages at the bottom left.
            SetText(_roadCaption, left, height - 136f, textWidth, 30f);
            SetText(_roadSubtitle, left, height - 172f, textWidth, 30f);
            SetText(_roadTitle, left, height - 240f, textWidth, 70f);
        }

        // ---- The panel ----------------------------------------------------

        private void RefreshRoadPanel()
        {
            if (_meetingOpen)
            {
                RefreshMeetingPanel();
                return;
            }
            if (_raidOpen)
            {
                RefreshRaidPanel();
                return;
            }
            SetMapPanelVisible(false);
            HideTownOptions();
            _optionCount = 0;
            DikozemieRoadEncounter card = _roadCard;
            if (card == null)
            {
                CloseRoad(false);
                return;
            }
            DikozemieRoadOutcome outcome = _roadOutcome;
            string status = HeroStatus();
            if (outcome != null)
            {
                _title.Text = string.IsNullOrEmpty(outcome.Title) ? card.Title : outcome.Title;
                if (outcome.CanBind)
                {
                    AddOption("СВЯЗАТЬ " + outcome.BindName.ToUpperInvariant(), "+" + outcome.BindReward + " МОН. НА АРЕНЕ",
                        () =>
                        {
                            DikozemieRoads.Bind(outcome);
                            _roadNote = "Пленник идёт за вами на верёвке. Сдайте его на арену любого города: " +
                                "«Розыск» в городском меню.";
                            RefreshSidePanel();
                        });
                }
                if (outcome.Captured)
                {
                    AddOption("В ПОВОЗКУ", "на арену ближайшего города", GoCaptive);
                }
                else
                {
                    AddOption("ИДТИ ДАЛЬШЕ", string.Empty, () => CloseRoad(true));
                }
                _description.Text = outcome.Text +
                    (string.IsNullOrEmpty(_roadNote) ? string.Empty : "\n\n" + _roadNote) +
                    "\n\n" + status;
                StackOptions();
                return;
            }
            _title.Text = card.Title;
            foreach (DikozemieRoadChoice choice in DikozemieRoads.Choices(card))
            {
                DikozemieRoadAction action = choice.Action;
                AddOption(choice.Label, choice.Detail, () => OnRoadChoice(action));
            }
            string factors = DikozemieRoads.FactorsText(card);
            _description.Text = card.Text +
                (string.IsNullOrEmpty(card.Threat) ? string.Empty : "\n\n" + card.Threat) +
                (string.IsNullOrEmpty(factors) ? string.Empty : "\n" + factors) +
                (string.IsNullOrEmpty(_roadNote) ? string.Empty : "\n\n" + _roadNote) +
                "\n\n" + status;
            StackOptions();
        }

        private static string HeroStatus()
        {
            var lines = new List<string> { "Монеты: " + DikozemieArmoryProgression.HeroResources + "." };
            if (!string.IsNullOrEmpty(DikozemieRoads.WoundText))
            {
                lines.Add(DikozemieRoads.WoundText);
            }
            if (!string.IsNullOrEmpty(DikozemieRoads.CompanyText))
            {
                lines.Add(DikozemieRoads.CompanyText);
            }
            return string.Join(" ", lines);
        }

        private void OnRoadChoice(DikozemieRoadAction action)
        {
            DikozemieRoadEncounter card = _roadCard;
            if (card == null)
            {
                return;
            }
            switch (action)
            {
                case DikozemieRoadAction.Fight:
                case DikozemieRoadAction.Attack:
                    StartRoadFight(card, card.Cornered);
                    return;
                case DikozemieRoadAction.Trade:
                    DikozemieRoads.Resolve(card, action);
                    CloseRoad(false);
                    DikozemieLobbyShellIntegration.OpenMarketFromTown();
                    return;
                case DikozemieRoadAction.Pass:
                    // Nothing happens: no second screen to say so.
                    DikozemieLobbyShellIntegration.TraceButtonAction("road choice " + card.Kind + " Pass");
                    CloseRoad(true);
                    return;
            }
            DikozemieRoadOutcome outcome = DikozemieRoads.Resolve(card, action);
            if (string.IsNullOrEmpty(outcome.Title))
            {
                // The attempt failed and the encounter goes on.
                _roadNote = outcome.Text;
                RefreshSidePanel();
                return;
            }
            ShowOutcome(outcome);
        }

        private void ShowOutcome(DikozemieRoadOutcome outcome)
        {
            _roadOutcome = outcome;
            _roadNote = string.Empty;
            HideFigures();
            // The winner stands in the picture: the hero after a victory,
            // the men who beat him after a defeat.
            if (outcome.Good)
            {
                SetHeroFigure();
            }
            else if (_roadCard != null && _roadCard.Foes.Count > 0)
            {
                SetFigures(_roadCard.Foes);
            }
            RefreshRoadScene();
            RefreshSidePanel();
            LayoutContent();
        }

        private void StartRoadFight(DikozemieRoadEncounter card, bool ambushed)
        {
            // Stage 5: a party with live members around fights as one team,
            // online; alone, the fight stays on the road field here.
            if (TryPartyRoadFight(card))
            {
                return;
            }
            StartLocalRoadFight(card, ambushed);
        }

        private void StartLocalRoadFight(DikozemieRoadEncounter card, bool ambushed)
        {
            DikozemieRoadFightSetup setup = DikozemieRoads.BuildFight(card, ambushed);
            _fightCard = card;
            if (!DikozemieRoadFight.TryStart(setup, out string error))
            {
                _fightCard = null;
                _roadNote = error;
                RefreshSidePanel();
            }
        }

        // Back from the field: the outcome waits on the map.
        private void TickRoadReturn()
        {
            if (!IsVisible || _travel == null || !DikozemieRoadFight.TryConsumeResult(out DikozemieRoadFightResult result))
            {
                return;
            }
            if (TryFinishRaidFight(result))
            {
                return;
            }
            DikozemieRoadEncounter card = _fightCard ?? new DikozemieRoadEncounter
            {
                Kind = DikozemieRoadKind.Bandits,
                Title = result.Setup?.Title ?? "БОЙ НА ДОРОГЕ"
            };
            _fightCard = null;
            if (result.Setup != null && card.Foes.Count == 0)
            {
                card.Foes.AddRange(result.Setup.Foes);
            }
            _townOpen = false;
            _roadOpen = true;
            _roadCard = card;
            ShowOutcome(DikozemieRoads.ApplyFightResult(result, card));
        }

        private void GoCaptive()
        {
            DikozemieLobbyMapNode town = DikozemieRoads.Capture();
            _roadOpen = false;
            _roadCard = null;
            _roadOutcome = null;
            HideFigures();
            _travelling = false;
            _route = null;
            _hasSelectedPoint = false;
            _selectedNode = null;
            _travelNote = string.Empty;
            if (town == null)
            {
                RefreshSidePanel();
                LayoutContent();
                return;
            }
            OpenTown(town);
            _townNote = "Сутки вас везли в повозке. Потом — ворота, двор, решётка. Ловцы получили за вас " +
                "кошель у распорядителя арены " + DisplayNodeName(town) + ".";
            RefreshSidePanel();
        }

        // Left the lair (in any way): no new encounter there until the hero
        // has walked away from it.
        private static bool _lairCooldown;

        private void CloseRoad(bool resume)
        {
            if (_roadCard?.Kind == DikozemieRoadKind.Lair && _travel != null)
            {
                _lairCooldown = true;
                resume = false;
                _travel.HasTarget = false;
                _route = null;
                _hasSelectedPoint = false;
                _selectedNode = null;
                DikozemieTravelState.Save();
            }
            _roadOpen = false;
            _meetingOpen = false;
            _raidOpen = false;
            _roadCard = null;
            _roadOutcome = null;
            _roadNote = string.Empty;
            HideFigures();
            _travelNote = string.Empty;
            _centerOnHero = true;
            RefreshSidePanel();
            LayoutContent();
            if (resume && _travel != null && _travel.HasTarget && _route != null && !IsAtSelected())
            {
                ToggleTravel();
            }
        }

        internal bool IsRoadOpen => _roadOpen;
    }
}
