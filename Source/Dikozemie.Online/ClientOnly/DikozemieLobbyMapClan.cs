using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets;
using TaleWorlds.TwoDimension;

namespace Dikozemie.Online
{
    // Stage 6 «Кланы и земля»: the clan screen. It stands where the town
    // screen stands (the painting of the clan's seat, the menu on the right)
    // with the clan's banner hanging over it; its tables are the ledger
    // board of the trade yard. «КЛАН» in the line under the header opens it
    // from anywhere; a clan is founded at a town's herald.
    public sealed partial class DikozemieLobbyMapWidget
    {
        private enum ClanSection
        {
            Main,
            Banner,
            Members,
            Treasury,
            Lands,
            Warband,
            Relations,
            Clans,
            Chronicle,
            // The observer's screen (DikozemieLobbyMapWatch).
            WatchPeople,
            WatchBattles,
            WatchComplaints,
            WatchFlags,
            WatchSanctions,
            WatchBook
        }

        private bool _clanOpen;
        private ClanSection _clanSection;
        private DikozemieLobbyMapNode _clanReturnTown;
        private string _clanNote = string.Empty;
        private bool _clanBusy;
        private int _clanRevision = -1;
        private string _clanSelected = string.Empty;
        private BannerTableauWidget _clanBanner;
        private ImageIdentifierWidget _placeBanner;
        private Widget _placeBannerFrame;

        // The banner board: a big banner and the parts to choose from.
        private bool _founding;
        private string _foundName = string.Empty;
        private string _foundMotto = string.Empty;
        private DikozemieBannerKit.Parts _parts = new DikozemieBannerKit.Parts();
        private Widget _bannerBoard;
        private BannerTableauWidget _bannerPreview;
        private TextWidget _bannerTitle;
        private TextWidget _bannerName;
        private TextWidget _bannerMotto;
        private readonly List<BannerGrid> _bannerGrids = new List<BannerGrid>();

        private bool LayerOpen => _townOpen || _clanOpen || _siegeOpen;

        private sealed class BannerTile
        {
            internal ButtonWidget Button;
            internal Widget Frame;
            internal ImageIdentifierWidget Image;
            internal Widget Swatch;
            internal int Value;
        }

        private sealed class BannerGrid
        {
            internal string Kind;
            internal string Label;
            internal TextWidget Title;
            internal ButtonWidget Prev;
            internal ButtonWidget Next;
            internal readonly List<BannerTile> Tiles = new List<BannerTile>();
            internal int Page;
            internal int Columns;
            internal int Rows;
            internal float Size;
            internal bool Swatches;
        }

        // ---- building ---------------------------------------------------------

        private void BuildClanLayer()
        {
            _clanBanner = new BannerTableauWidget(Context)
            {
                Id = "ClanBanner",
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                DoNotAcceptEvents = true,
                IsVisible = false
            };
            _clanBanner.IsNineGrid = false;
            _townView.AddChild(_clanBanner);
            // The defender's banner on the siege screen.
            _foeBanner = new BannerTableauWidget(Context)
            {
                Id = "SiegeFoeBanner",
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                DoNotAcceptEvents = true,
                IsVisible = false
            };
            _foeBanner.IsNineGrid = false;
            _townView.AddChild(_foeBanner);

            // The lord's banner over a town, as in the singleplayer menu.
            _placeBannerFrame = CreateSolid("PlaceBannerFrame", new Color(0.05f, 0.035f, 0.02f, 0.65f));
            _placeBannerFrame.WidthSizePolicy = SizePolicy.Fixed;
            _placeBannerFrame.HeightSizePolicy = SizePolicy.Fixed;
            _placeBannerFrame.HorizontalAlignment = HorizontalAlignment.Left;
            _placeBannerFrame.VerticalAlignment = VerticalAlignment.Top;
            _placeBannerFrame.IsVisible = false;
            _townView.AddChild(_placeBannerFrame);
            _placeBanner = new ImageIdentifierWidget(Context)
            {
                Id = "PlaceBanner",
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                DoNotAcceptEvents = true
            };
            _placeBanner.TextureProviderName = "BannerImageTextureProvider";
            _placeBanner.IsVisible = false;
            _townView.AddChild(_placeBanner);

            BuildBannerBoard();
        }

        private void BuildBannerBoard()
        {
            _bannerBoard = new Widget(Context)
            {
                Id = "BannerBoard",
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                ClipContents = true,
                IsVisible = false
            };
            _townView.AddChild(_bannerBoard);
            _bannerBoard.AddChild(CreateSolid("BannerBoardFill", new Color(0.055f, 0.040f, 0.026f, 1f)));
            for (int index = 0; index < 4; index++)
            {
                Widget line = CreateSolid("BannerBoardFrame" + index, new Color(0.72f, 0.47f, 0.17f, 0.75f));
                PlaceFixed(line);
                line.Id = "BannerBoardFrame" + index;
                _bannerBoard.AddChild(line);
            }
            _bannerPreview = new BannerTableauWidget(Context)
            {
                Id = "BannerPreview",
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                DoNotAcceptEvents = true
            };
            _bannerPreview.IsNineGrid = true;
            _bannerBoard.AddChild(_bannerPreview);
            _bannerTitle = BoardText("BannerTitle", 34, SheetGold);
            _bannerName = BoardText("BannerName", 30, new Color(1f, 0.92f, 0.70f, 1f));
            _bannerName.Brush.TextHorizontalAlignment = TextHorizontalAlignment.Center;
            _bannerMotto = BoardText("BannerMotto", 20, SheetDim);
            _bannerMotto.Brush.TextHorizontalAlignment = TextHorizontalAlignment.Center;
            _bannerMotto.Brush.TextVerticalAlignment = TextVerticalAlignment.Top;

            _bannerGrids.Add(BuildGrid("background", "УЗОР ПОЛЯ", 10, 1, 58f, false));
            _bannerGrids.Add(BuildGrid("field", "ЦВЕТ ПОЛЯ", 24, 2, 26f, true));
            _bannerGrids.Add(BuildGrid("pattern", "ЦВЕТ УЗОРА", 24, 1, 26f, true));
            _bannerGrids.Add(BuildGrid("icon", "ЗНАК", 10, 3, 58f, false));
            _bannerGrids.Add(BuildGrid("iconColor", "ЦВЕТ ЗНАКА", 24, 2, 26f, true));
        }

        private TextWidget BoardText(string id, int size, Color color)
        {
            TextWidget text = CreateText(id, string.Empty, size, color);
            PlaceFixed(text);
            text.Brush.TextHorizontalAlignment = TextHorizontalAlignment.Left;
            _bannerBoard.AddChild(text);
            return text;
        }

        private BannerGrid BuildGrid(string kind, string label, int columns, int rows, float size, bool swatches)
        {
            var grid = new BannerGrid { Kind = kind, Label = label, Columns = columns, Rows = rows, Size = size, Swatches = swatches };
            grid.Title = BoardText("BannerGridTitle_" + kind, 18, SheetDim);
            grid.Prev = BoardArrow("BannerGridPrev_" + kind, "‹", () => TurnGrid(grid, -1));
            grid.Next = BoardArrow("BannerGridNext_" + kind, "›", () => TurnGrid(grid, 1));
            for (int index = 0; index < columns * rows; index++)
            {
                var tile = new BannerTile();
                tile.Button = new ButtonWidget(Context)
                {
                    Id = "BannerTile_" + kind + index,
                    WidthSizePolicy = SizePolicy.Fixed,
                    HeightSizePolicy = SizePolicy.Fixed,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    SuggestedWidth = size,
                    SuggestedHeight = size,
                    DoNotPassEventsToChildren = true,
                    IsVisible = false
                };
                tile.Frame = CreateSolid("BannerTileFrame_" + kind + index, new Color(0.40f, 0.30f, 0.18f, 0.8f));
                tile.Button.AddChild(tile.Frame);
                if (swatches)
                {
                    tile.Swatch = CreateSolid("BannerTileSwatch_" + kind + index, Color.White);
                    tile.Swatch.MarginLeft = tile.Swatch.MarginRight = tile.Swatch.MarginTop = tile.Swatch.MarginBottom = 3f;
                    tile.Button.AddChild(tile.Swatch);
                }
                else
                {
                    Widget back = CreateSolid("BannerTileBack_" + kind + index, new Color(0.10f, 0.07f, 0.04f, 1f));
                    back.MarginLeft = back.MarginRight = back.MarginTop = back.MarginBottom = 3f;
                    tile.Button.AddChild(back);
                    tile.Image = new ImageIdentifierWidget(Context)
                    {
                        Id = "BannerTileImage_" + kind + index,
                        WidthSizePolicy = SizePolicy.StretchToParent,
                        HeightSizePolicy = SizePolicy.StretchToParent,
                        MarginLeft = 4f,
                        MarginRight = 4f,
                        MarginTop = 4f,
                        MarginBottom = 4f,
                        DoNotAcceptEvents = true
                    };
                    tile.Image.TextureProviderName = "BannerImageTextureProvider";
                    tile.Button.AddChild(tile.Image);
                }
                BannerTile captured = tile;
                tile.Button.ClickEventHandlers.Add(_ => PickPart(grid, captured.Value));
                _bannerBoard.AddChild(tile.Button);
                grid.Tiles.Add(tile);
            }
            return grid;
        }

        private ButtonWidget BoardArrow(string id, string text, Action click)
        {
            var button = new ButtonWidget(Context)
            {
                Id = id,
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                SuggestedWidth = 34f,
                SuggestedHeight = 30f,
                DoNotPassEventsToChildren = true
            };
            button.AddChild(CreateSolid(id + "Frame", new Color(0.72f, 0.47f, 0.17f, 0.9f)));
            Widget fill = CreateSolid(id + "Fill", OptionFill);
            fill.MarginLeft = fill.MarginRight = fill.MarginTop = fill.MarginBottom = 2f;
            button.AddChild(fill);
            TextWidget label = CreateText(id + "Text", text, 24, SheetGold);
            label.WidthSizePolicy = SizePolicy.StretchToParent;
            label.HeightSizePolicy = SizePolicy.StretchToParent;
            button.AddChild(label);
            button.ClickEventHandlers.Add(_ => click());
            _bannerBoard.AddChild(button);
            return button;
        }

        // ---- opening and closing ---------------------------------------------------

        // «КЛАН» in the line under the header.
        private void TickClanRequests()
        {
            if (_travel == null)
            {
                return;
            }
            if (DikozemieWatch.ConsumeScreenRequest())
            {
                DikozemieTown.ConsumeTownScreenRequest();
                OpenClan(ClanSection.WatchPeople);
                return;
            }
            if (!DikozemieClans.ConsumeScreenRequest())
            {
                return;
            }
            // The town request of the same click is for the map: drop it.
            DikozemieTown.ConsumeTownScreenRequest();
            OpenClan();
        }

        private void OpenClan(ClanSection section = ClanSection.Main)
        {
            if (_roadOpen)
            {
                return;
            }
            if (_townOpen)
            {
                _clanReturnTown = _townNode;
                CloseSheet();
                CloseChat();
                HideTownFigures();
                _townOpen = false;
                DikozemieTown.IsTownScreenOpen = false;
            }
            else if (!_clanOpen)
            {
                _clanReturnTown = null;
            }
            _siegeOpen = false;
            _passagePick = false;
            _worldPick = null;
            _clanOpen = true;
            DikozemieClans.IsScreenOpen = true;
            DikozemieClans.Refresh();
            ShowClanSection(section);
            DikozemieLobbyShellIntegration.TraceButtonAction("clan screen open clan=" + DikozemieClans.MyClanId +
                " return=" + (_clanReturnTown?.Id ?? "map"));
        }

        private void CloseClan()
        {
            if (!_clanOpen)
            {
                return;
            }
            CloseSheet();
            CloseChat();
            _clanOpen = false;
            _founding = false;
            DikozemieClans.IsScreenOpen = false;
            DikozemieWatch.IsScreenOpen = false;
            DikozemieLobbyMapNode back = _clanReturnTown;
            _clanReturnTown = null;
            if (back != null && NodeHere() != null && NodeHere().Id == back.Id)
            {
                OpenTown(back);
                return;
            }
            RefreshSidePanel();
            LayoutContent();
        }

        private void ShowClanSection(ClanSection section, string note = "")
        {
            CloseSheet();
            CloseChat();
            _clanSection = section;
            _clanNote = note ?? string.Empty;
            _clanSelected = string.Empty;
            DikozemieWatch.IsScreenOpen = false;
            if (OpenWatchSection(section))
            {
                SetClanPicture();
                RefreshSidePanel();
                LayoutContent();
                return;
            }
            switch (section)
            {
                case ClanSection.Members: OpenSheet(TradeSheet.ClanMembers); break;
                case ClanSection.Treasury: OpenSheet(TradeSheet.ClanTreasury); break;
                case ClanSection.Lands: OpenSheet(TradeSheet.ClanLands); break;
                case ClanSection.Warband: OpenSheet(TradeSheet.ClanWarband); break;
                case ClanSection.Relations: OpenSheet(TradeSheet.ClanRelations); break;
                case ClanSection.Clans:
                    DikozemieClans.RefreshList();
                    OpenSheet(TradeSheet.ClanList);
                    break;
                case ClanSection.Chronicle: OpenSheet(TradeSheet.ClanChronicle); break;
            }
            if (section == ClanSection.Banner)
            {
                RefreshBannerBoard();
            }
            SetClanPicture();
            RefreshSidePanel();
            LayoutContent();
        }

        // The painting of the clan's seat: its first castle or town, else a
        // castle of the culture of the town it was founded in.
        private void SetClanPicture()
        {
            DikozemieClanFull clan = DikozemieClans.Clan;
            DikozemieLobbyMapNode seat = null;
            if (clan != null)
            {
                seat = DikozemieLobbyMapCatalog.Find(clan.Holdings.FirstOrDefault()?.Node ?? string.Empty) ??
                    DikozemieLobbyMapCatalog.Find(clan.Home);
            }
            seat = seat ?? NodeHere() ?? _clanReturnTown;
            string culture = DikozemieTown.NormalizeCulture(seat?.Culture);
            if (string.IsNullOrEmpty(culture) || culture == "neutral_culture")
            {
                culture = "empire";
            }
            bool town = seat != null && clan != null && clan.Holdings.Any(h => h.Node == seat.Id && h.Kind == "town");
            DikozemieTownPictureTextureProvider.Requested = culture + "-" + (town ? (seat.HasPort ? "port" : "town") : "castle");
        }

        // ---- the side panel --------------------------------------------------------

        private void RefreshClanPanel()
        {
            SetMapPanelVisible(false);
            HideTownOptions();
            HideWorldButtons();
            _optionCount = 0;
            DikozemieClanState state = DikozemieClans.State;
            DikozemieClanFull clan = state?.Clan;
            if (clan == null && _clanSection != ClanSection.Banner && _clanSection != ClanSection.Clans &&
                !IsWatchSection)
            {
                _clanSection = ClanSection.Main;
            }
            FillClanTexts(clan);
            var text = new List<string>();
            if (IsWatchSection)
            {
                FillWatchTexts();
            }
            switch (_clanSection)
            {
                case ClanSection.WatchPeople:
                case ClanSection.WatchBattles:
                case ClanSection.WatchComplaints:
                case ClanSection.WatchFlags:
                case ClanSection.WatchSanctions:
                case ClanSection.WatchBook:
                    FillWatchPanel(text);
                    break;
                case ClanSection.Banner:
                    FillBannerPanel(clan, text);
                    break;
                case ClanSection.Members:
                    _title.Text = "ЛЮДИ КЛАНА";
                    text.Add(MembersSummary(clan));
                    if (clan.Applications.Count > 0 && DikozemieClans.Can("admit"))
                    {
                        text.Add("Просятся в клан: " + clan.Applications.Count + ". Ответ — в таблице.");
                    }
                    if (DikozemieClans.Can("invite"))
                    {
                        text.Add("Позвать в клан можно героя на карте: в его карточке «ПОЗВАТЬ В КЛАН».");
                    }
                    AddOption("ПОКИНУТЬ КЛАН", clan.Rank == "leader" && clan.Members.Count > 1 ? "главенство перейдёт" : string.Empty,
                        LeaveClan);
                    if (DikozemieClans.Can("disband"))
                    {
                        AddOption("РАСПУСТИТЬ КЛАН", "казна — главе", DisbandClan);
                    }
                    AddOption("НАЗАД", string.Empty, () => ShowClanSection(ClanSection.Main));
                    break;
                case ClanSection.Treasury:
                    _title.Text = "КАЗНА КЛАНА";
                    text.Add("В казне " + FormatCoins(clan.Coins) + " мон. В день: доход земли +" +
                        FormatCoins((int)Math.Round(clan.IncomeDay)) + ", жалованье людям -" + FormatCoins((int)Math.Round(clan.WagesDay)) + ".");
                    text.Add("Пошлины рынков, мостов и мастерских приходят сюда сами. Из казны платят наёмникам; пуста — они уходят.");
                    AddOption("ВНЕСТИ В КАЗНУ", "ваши монеты: " + FormatCoins(DikozemieArmoryProgression.HeroResources),
                        () => AskAmount("ВНЕСТИ В КАЗНУ КЛАНА", "Сколько монет внести? У вас " +
                            FormatCoins(DikozemieArmoryProgression.HeroResources) + ".", DikozemieArmoryProgression.HeroResources,
                            amount => ClanAct("deposit", "{\"amount\":" + amount + "}", "Внесено " + amount + " мон.")));
                    if (DikozemieClans.Can("withdraw"))
                    {
                        AddOption("ВЗЯТЬ ИЗ КАЗНЫ", "только глава", () => AskAmount("ВЗЯТЬ ИЗ КАЗНЫ КЛАНА",
                            "Сколько взять? В казне " + FormatCoins(clan.Coins) + ".", clan.Coins,
                            amount => ClanAct("withdraw", "{\"amount\":" + amount + "}", "Взято " + amount + " мон.")));
                    }
                    AddOption("НАЗАД", string.Empty, () => ShowClanSection(ClanSection.Main));
                    break;
                case ClanSection.Lands:
                    _title.Text = "ЗЕМЛИ КЛАНА";
                    text.Add(clan.Holdings.Count == 0
                        ? "У клана нет крепостей. Подойдите с людьми к стенам замка и нажмите на него на карте: «ОСАДИТЬ»."
                        : "Крепостей: " + clan.Holdings.Count + " из " + clan.MaxHoldings + ". Гарнизон ставят на месте, " +
                          "в замке: «ГАРНИЗОН». Пошлины меняют отсюда.");
                    text.Add("Без единого человека в гарнизоне крепость через " + (int)DikozemieClans.Rules.AbandonMinutes +
                        " мин. вернётся к прежнему владельцу.");
                    AddOption("НАЗАД", string.Empty, () => ShowClanSection(ClanSection.Main));
                    break;
                case ClanSection.Warband:
                    _title.Text = "ДРУЖИНА";
                    text.Add("Наёмники клана: " + DikozemieClans.TroopsWords(clan.Troops) + ".");
                    text.Add("Их нанимают в тавернах городов: «ТАВЕРНА → НАЁМНИКИ». Дружина идёт на штурм с тем, кто поставил лагерь; " +
                        "в гарнизон её ставят в самой крепости.");
                    AddOption("НАЗАД", string.Empty, () => ShowClanSection(ClanSection.Main));
                    break;
                case ClanSection.Relations:
                    _title.Text = "ВОЙНА И МИР";
                    text.Add("С кланом в войне можно биться на дорогах — это не разбой — и осаждать его крепости. " +
                        "Мир заключают оба главы; после него " + DikozemieClans.Rules.TruceHours.ToString("0", CultureInfo.InvariantCulture) +
                        " ч. перемирия.");
                    if (!DikozemieClans.Can("war"))
                    {
                        text.Add("Объявляет войну и мир глава клана.");
                    }
                    AddOption("НАЗАД", string.Empty, () => ShowClanSection(ClanSection.Main));
                    break;
                case ClanSection.Clans:
                    _title.Text = "КЛАНЫ ДИКОЗЕМЬЯ";
                    text.Add(clan == null
                        ? "Попроситесь в клан — глава или воевода ответят. Или основайте свой."
                        : "Все кланы мира, их земли и люди.");
                    AddOption("НАЗАД", string.Empty, () => ShowClanSection(ClanSection.Main));
                    break;
                case ClanSection.Chronicle:
                    _title.Text = "ЛЕТОПИСЬ";
                    text.Add("Всё, что было с кланом: кто пришёл и ушёл, что взято и что потеряно.");
                    AddOption("НАЗАД", string.Empty, () => ShowClanSection(ClanSection.Main));
                    break;
                default:
                    FillClanMain(state, clan, text);
                    break;
            }
            if (!string.IsNullOrEmpty(_clanNote))
            {
                text.Add(string.Empty);
                text.Add(_clanNote);
            }
            else if (!DikozemieEconomy.IsOnline)
            {
                text.Add(string.Empty);
                text.Add(DikozemieEconomy.StatusText);
            }
            _description.Text = string.Join("\n", text.Where(t => t != null));
            StackOptions();
        }

        private void FillClanMain(DikozemieClanState state, DikozemieClanFull clan, List<string> text)
        {
            if (clan == null)
            {
                _title.Text = "КЛАНЫ";
                text.Add("Клан — это общее знамя, общая казна и общие крепости. Клан берёт замки и города, собирает пошлины " +
                    "с рынков, мостов и мастерских своей земли, нанимает воинов и воюет с другими кланами.");
                DikozemieLobbyMapNode here = NodeHere();
                bool town = here != null && DikozemieTown.IsTown(here);
                int cost = DikozemieClans.Rules.FoundCost;
                AddOption("ОСНОВАТЬ КЛАН", town ? "герольд · " + cost + " МОН." : "только в городе", () =>
                {
                    if (!town)
                    {
                        _clanNote = "Грамоту клана пишет и знамя расписывает герольд, а он сидит в городе. Придите в любой город.";
                        RefreshSidePanel();
                        return;
                    }
                    StartFounding();
                });
                AddOption("КЛАНЫ ДИКОЗЕМЬЯ", (DikozemieClans.List?.Clans.Count ?? 0) > 0
                    ? DikozemieClans.List.Clans.Count + " · попроситься" : "попроситься в клан",
                    () => ShowClanSection(ClanSection.Clans));
                AddOption("НАЗАД", string.Empty, CloseClan);
                return;
            }
            _title.Text = "КЛАН";
            text.Add(clan.RankName + " клана «" + clan.Name + "». Глава — " + clan.LeaderName + ".");
            int online = clan.Members.Count(m => m.Online);
            DikozemieSiege siege = DikozemieClans.MySiege;
            AddOption("ЛЮДИ КЛАНА", clan.Members.Count + " · в мире " + online +
                (clan.Applications.Count > 0 && DikozemieClans.Can("admit") ? " · просятся " + clan.Applications.Count : string.Empty),
                () => ShowClanSection(ClanSection.Members));
            AddOption("КАЗНА КЛАНА", FormatCoins(clan.Coins) + " МОН.", () => ShowClanSection(ClanSection.Treasury));
            AddOption("ЗЕМЛИ", clan.Holdings.Count + " ИЗ " + clan.MaxHoldings, () => ShowClanSection(ClanSection.Lands));
            AddOption("ДРУЖИНА", DikozemieClans.Total(clan.Troops) + " · " + FormatCoins((int)Math.Round(clan.WagesDay)) + " мон. в день",
                () => ShowClanSection(ClanSection.Warband));
            int wars = clan.Relations.Count(r => r.State == "war");
            int offers = clan.Relations.Count(r => r.Offer == "theirs");
            AddOption("ВОЙНА И МИР", wars > 0 ? "войн: " + wars + (offers > 0 ? " · мир предложен" : string.Empty) : "мир со всеми",
                () => ShowClanSection(ClanSection.Relations));
            if (siege != null)
            {
                AddOption("ОСАДА", siege.Name + " · " + (siege.State == "battle" ? "ШТУРМ" : DikozemieClans.Minutes(siege.AssaultLeft)),
                    () => OpenSiege(siege.Id));
            }
            if (DikozemieClans.Can("banner"))
            {
                AddOption("ЗНАМЯ", DikozemieClans.Rules.BannerCost + " мон. из казны", () =>
                {
                    _founding = false;
                    _parts = DikozemieBannerKit.Read(clan.Banner);
                    ShowClanSection(ClanSection.Banner);
                });
            }
            AddOption("РАЗГОВОР КЛАНА", DikozemieWorld.UnreadIn(DikozemieWorld.ClanChannel) > 0
                ? "новых: " + DikozemieWorld.UnreadIn(DikozemieWorld.ClanChannel) : string.Empty, () =>
                {
                    _clanSection = ClanSection.Main;
                    OpenChat(DikozemieWorld.ClanChannel);
                });
            AddOption("ЛЕТОПИСЬ", string.Empty, () => ShowClanSection(ClanSection.Chronicle));
            AddOption("НАЗАД", string.Empty, CloseClan);
        }

        private static string MembersSummary(DikozemieClanFull clan)
        {
            int online = clan.Members.Count(m => m.Online);
            return "В клане " + DikozemieWorld.People(clan.Members.Count) + ", в мире сейчас " + online + ".";
        }

        // The texts over the painting: the clan's name, rank and story.
        private void FillClanTexts(DikozemieClanFull clan)
        {
            if (clan == null)
            {
                _townName.Text = "КЛАНЫ ДИКОЗЕМЬЯ";
                int count = DikozemieClans.List?.Clans.Count ?? 0;
                _townKind.Text = (count == 0 ? "КЛАНОВ ИГРОКОВ ЕЩЁ НЕТ" : DikozemieClans.ClansWord(count).ToUpperInvariant()) +
                    "  ·  ЗАМКИ И ГОРОДА ЖДУТ ХОЗЯЕВ";
                _townOwner.Text = "Вы ни в одном клане.";
                _townLore.Text = "Замки и города Дикоземья держат кланы одиночной игры. Клан игроков может взять любой из них " +
                    "штурмом: гарнизон бьётся у ворот, а победитель поднимает над стенами своё знамя и собирает пошлины " +
                    "с рынков, мостов, мастерских и деревень этой земли. Другой клан может её отбить.";
                return;
            }
            _townName.Text = clan.Name.ToUpperInvariant();
            int castles = clan.Holdings.Count(h => h.Kind == "castle");
            int towns = clan.Holdings.Count(h => h.Kind == "town");
            _townKind.Text = "КЛАН  ·  " + DikozemieWorld.People(clan.Members.Count).ToUpperInvariant() +
                (castles > 0 ? "  ·  ЗАМКОВ " + castles : string.Empty) +
                (towns > 0 ? "  ·  ГОРОДОВ " + towns : string.Empty) +
                (clan.Holdings.Count == 0 ? "  ·  БЕЗ ЗЕМЛИ" : string.Empty);
            _townOwner.Text = string.IsNullOrEmpty(clan.Motto) ? "Основан " + DikozemieClans.Date(clan.Created) : "«" + clan.Motto + "»";
            var lore = new List<string>
            {
                "Глава — " + clan.LeaderName + ". Казна: " + FormatCoins(clan.Coins) + " мон. Дружина: " +
                    DikozemieClans.TroopsWords(clan.Troops) + "."
            };
            if (clan.Holdings.Count > 0)
            {
                lore.Add("Земли: " + string.Join(", ", clan.Holdings.Select(h => h.Name + " (гарнизон " + h.Men + ")")) +
                    ". В день: +" + FormatCoins((int)Math.Round(clan.IncomeDay)) + " с земли, -" +
                    FormatCoins((int)Math.Round(clan.WagesDay)) + " жалованье.");
            }
            List<string> wars = clan.Relations.Where(r => r.State == "war").Select(r => "«" + r.Name + "»").ToList();
            if (wars.Count > 0)
            {
                lore.Add("Война: " + string.Join(", ", wars) + ".");
            }
            _townLore.Text = string.Join(" ", lore);
        }

        private void LayoutClan(float width, float height)
        {
            bool board = _clanOpen && _clanSection == ClanSection.Banner;
            _bannerBoard.IsVisible = board;
            bool picture = _clanOpen && !TradeSheetOpen && !_chatOpen && !board;
            string banner = DikozemieClans.Clan?.Banner ?? string.Empty;
            _clanBanner.IsVisible = picture && banner.Length > 0;
            if (_clanBanner.IsVisible)
            {
                if (_clanBanner.BannerCodeText != banner)
                {
                    _clanBanner.BannerCodeText = banner;
                }
                _clanBanner.SuggestedWidth = 260f;
                _clanBanner.SuggestedHeight = 390f;
                _clanBanner.PositionXOffset = 30f;
                _clanBanner.PositionYOffset = 0f;
            }
            if (board)
            {
                LayoutBannerBoard(width, height);
            }
        }

        // ---- the banner board --------------------------------------------------------

        private void StartFounding()
        {
            _founding = true;
            _foundName = string.Empty;
            _foundMotto = string.Empty;
            _parts = DikozemieBannerKit.Roll(Environment.TickCount);
            ShowClanSection(ClanSection.Banner);
        }

        private void FillBannerPanel(DikozemieClanFull clan, List<string> text)
        {
            if (_founding)
            {
                int cost = DikozemieClans.Rules.FoundCost;
                _title.Text = "ГЕРОЛЬД";
                text.Add("«Имя, знамя и девиз — и клан записан в грамоту. За роспись знамени и грамоту — " + cost + " монет.»");
                text.Add("Слева — узор, цвета и знак. Знамя будет над вашими крепостями и в списке кланов.");
                int purse = DikozemieArmoryProgression.HeroResources;
                if (purse < cost)
                {
                    text.Add("У вас " + purse + " мон.: на грамоту не хватает " + (cost - purse) +
                        ". Знамя можно расписать уже сейчас — оно дождётся денег.");
                }
                AddOption("ИМЯ КЛАНА", string.IsNullOrEmpty(_foundName) ? "не названо" : _foundName, AskClanName);
                AddOption("ДЕВИЗ", string.IsNullOrEmpty(_foundMotto) ? "по желанию" : "задан", AskMotto);
                AddOption("СЛУЧАЙНОЕ ЗНАМЯ", string.Empty, () =>
                {
                    _parts = DikozemieBannerKit.Roll(Environment.TickCount);
                    RefreshBannerBoard();
                });
                AddOption("ОСНОВАТЬ КЛАН", purse < cost ? "нужно " + cost + " МОН." : cost + " МОН.", FoundClan);
                AddOption("НАЗАД", string.Empty, () =>
                {
                    _founding = false;
                    ShowClanSection(ClanSection.Main);
                });
                return;
            }
            _title.Text = "ЗНАМЯ КЛАНА";
            text.Add("Новая роспись знамени — " + DikozemieClans.Rules.BannerCost + " монет из казны клана.");
            AddOption("ДЕВИЗ", string.IsNullOrEmpty(clan?.Motto) ? "нет" : "сменить", () => AskText("ДЕВИЗ КЛАНА",
                "Слова под знаменем (до 80 букв).", clan?.Motto ?? string.Empty, 80,
                motto => ClanAct("motto", "{\"motto\":" + DikozemieEconomy.Json(motto) + "}", "Девиз записан.")));
            AddOption("СЛУЧАЙНОЕ ЗНАМЯ", string.Empty, () =>
            {
                _parts = DikozemieBannerKit.Roll(Environment.TickCount);
                RefreshBannerBoard();
            });
            AddOption("ПОДНЯТЬ ЗНАМЯ", DikozemieClans.Rules.BannerCost + " МОН.", () => ClanAct("banner",
                "{\"banner\":" + DikozemieEconomy.Json(DikozemieBannerKit.Code(_parts)) + "}", "Клан поднял новое знамя."));
            AddOption("НАЗАД", string.Empty, () => ShowClanSection(ClanSection.Main));
        }

        private void AskClanName()
        {
            AskText("ИМЯ КЛАНА", "Как будут звать ваш клан? От трёх букв, до 28 знаков.", _foundName, 28, name =>
            {
                _foundName = name;
                RefreshBannerBoard();
                RefreshSidePanel();
            });
        }

        private void AskMotto()
        {
            AskText("ДЕВИЗ", "Слова под знаменем (до 80 букв). Можно оставить пустым.", _foundMotto, 80, motto =>
            {
                _foundMotto = motto;
                RefreshBannerBoard();
                RefreshSidePanel();
            });
        }

        private void FoundClan()
        {
            // Money first: asking for a name the herald will not write down
            // anyway only wastes the player's time.
            int cost = DikozemieClans.Rules.FoundCost;
            int purse = DikozemieArmoryProgression.HeroResources;
            if (purse < cost)
            {
                _clanNote = "Герольд качает головой: «" + cost + " монет, не меньше». Заработать можно на арене, " +
                    "в дорожных передрягах и торговлей.";
                RefreshSidePanel();
                return;
            }
            if (string.IsNullOrWhiteSpace(_foundName))
            {
                AskClanName();
                return;
            }
            DikozemieLobbyMapNode here = NodeHere();
            if (here == null || !DikozemieTown.IsTown(here))
            {
                _clanNote = "Герольд остался в городе: вернитесь туда.";
                RefreshSidePanel();
                return;
            }
            string body = "{\"name\":" + DikozemieEconomy.Json(_foundName) + ",\"motto\":" + DikozemieEconomy.Json(_foundMotto) +
                ",\"banner\":" + DikozemieEconomy.Json(DikozemieBannerKit.Code(_parts)) + ",\"at\":" + DikozemieEconomy.Json(here.Id) + "}";
            ClanAct("found", body, null, state =>
            {
                _founding = false;
                ShowClanSection(ClanSection.Main, "Герольд скрепил грамоту печатью. Клан «" + state.Clan?.Name +
                    "» записан, знамя расписано. Позовите людей и наймите воинов в таверне.");
            });
        }

        private void RefreshBannerBoard()
        {
            if (_bannerBoard == null)
            {
                return;
            }
            string code = DikozemieBannerKit.Code(_parts);
            _bannerPreview.BannerCodeText = code;
            _bannerTitle.Text = _founding ? "ГРАМОТА КЛАНА" : "ЗНАМЯ КЛАНА";
            _bannerName.Text = _founding
                ? (string.IsNullOrEmpty(_foundName) ? "ИМЯ НЕ НАЗВАНО" : _foundName.ToUpperInvariant())
                : (DikozemieClans.Clan?.Name ?? string.Empty).ToUpperInvariant();
            string motto = _founding ? _foundMotto : DikozemieClans.Clan?.Motto;
            _bannerMotto.Text = string.IsNullOrEmpty(motto) ? string.Empty : "«" + motto + "»";
            foreach (BannerGrid grid in _bannerGrids)
            {
                FillGrid(grid);
            }
        }

        private IReadOnlyList<int> GridValues(BannerGrid grid)
        {
            switch (grid.Kind)
            {
                case "background": return DikozemieBannerKit.Backgrounds;
                case "field":
                case "pattern": return DikozemieBannerKit.FieldColors;
                case "icon": return DikozemieBannerKit.Icons;
                default: return DikozemieBannerKit.IconColors;
            }
        }

        private int GridCurrent(BannerGrid grid)
        {
            switch (grid.Kind)
            {
                case "background": return _parts.Background;
                case "field": return _parts.FieldColor;
                case "pattern": return _parts.PatternColor;
                case "icon": return _parts.Icon;
                default: return _parts.IconColor;
            }
        }

        private void FillGrid(BannerGrid grid)
        {
            IReadOnlyList<int> values = GridValues(grid);
            int per = grid.Tiles.Count;
            int pages = Math.Max(1, (values.Count + per - 1) / per);
            grid.Page = Math.Max(0, Math.Min(pages - 1, grid.Page));
            grid.Title.Text = grid.Label + (pages > 1 ? "  ·  " + (grid.Page + 1) + " / " + pages : string.Empty);
            grid.Prev.IsVisible = pages > 1;
            grid.Next.IsVisible = pages > 1;
            int current = GridCurrent(grid);
            for (int index = 0; index < per; index++)
            {
                BannerTile tile = grid.Tiles[index];
                int at = grid.Page * per + index;
                bool shown = at < values.Count;
                tile.Button.IsVisible = shown;
                if (!shown)
                {
                    continue;
                }
                tile.Value = values[at];
                bool chosen = tile.Value == current;
                tile.Frame.Color = chosen ? new Color(1f, 0.82f, 0.36f, 1f) : new Color(0.40f, 0.30f, 0.18f, 0.8f);
                if (tile.Swatch != null)
                {
                    tile.Swatch.Color = DikozemieBannerKit.ColorOf(tile.Value);
                }
                else
                {
                    // Patterns in the chosen field colours; charges in the chosen
                    // colour on a plain field, as large as the tile allows.
                    string code = grid.Kind == "background"
                        ? DikozemieBannerKit.FieldCode(tile.Value, _parts.FieldColor, _parts.PatternColor)
                        : DikozemieBannerKit.Code(new DikozemieBannerKit.Parts
                        {
                            Background = DikozemieBannerKit.Plain,
                            FieldColor = _parts.FieldColor,
                            PatternColor = _parts.FieldColor,
                            Icon = tile.Value,
                            IconColor = _parts.IconColor,
                            IconSize = 1100
                        });
                    if (tile.Image.ImageId != code)
                    {
                        tile.Image.ImageId = code;
                    }
                }
            }
        }

        private void TurnGrid(BannerGrid grid, int step)
        {
            int pages = Math.Max(1, (GridValues(grid).Count + grid.Tiles.Count - 1) / grid.Tiles.Count);
            grid.Page = (grid.Page + step + pages) % pages;
            FillGrid(grid);
        }

        private void PickPart(BannerGrid grid, int value)
        {
            switch (grid.Kind)
            {
                case "background": _parts.Background = value; break;
                case "field": _parts.FieldColor = value; break;
                case "pattern": _parts.PatternColor = value; break;
                case "icon": _parts.Icon = value; break;
                default: _parts.IconColor = value; break;
            }
            RefreshBannerBoard();
        }

        private void LayoutBannerBoard(float width, float height)
        {
            const float margin = 18f;
            float w = Math.Max(600f, width - margin * 2f);
            float h = Math.Max(400f, height - margin * 2f);
            _bannerBoard.PositionXOffset = margin;
            _bannerBoard.PositionYOffset = margin;
            _bannerBoard.SuggestedWidth = w;
            _bannerBoard.SuggestedHeight = h;
            const float line = 2f;
            SetFrame((Widget)_bannerBoard.FindChild("BannerBoardFrame0", false), 0f, 0f, w, line);
            SetFrame((Widget)_bannerBoard.FindChild("BannerBoardFrame1", false), 0f, h - line, w, line);
            SetFrame((Widget)_bannerBoard.FindChild("BannerBoardFrame2", false), 0f, 0f, line, h);
            SetFrame((Widget)_bannerBoard.FindChild("BannerBoardFrame3", false), w - line, 0f, line, h);
            SetText(_bannerTitle, SheetPad, 20f, w * 0.6f, 48f);
            // The banner on the left, as large as the board allows.
            float previewSize = Math.Min(380f, Math.Max(240f, h - 230f));
            float previewX = SheetPad;
            _bannerPreview.SuggestedWidth = previewSize;
            _bannerPreview.SuggestedHeight = previewSize;
            _bannerPreview.PositionXOffset = previewX;
            _bannerPreview.PositionYOffset = 80f;
            SetText(_bannerName, previewX - 20f, 90f + previewSize, previewSize + 40f, 40f);
            SetText(_bannerMotto, previewX - 20f, 132f + previewSize, previewSize + 40f, 70f);
            // The parts on the right.
            float left = previewX + previewSize + 46f;
            float right = w - SheetPad;
            float y = 84f;
            foreach (BannerGrid grid in _bannerGrids)
            {
                float gap = grid.Swatches ? 4f : 8f;
                int columns = Math.Max(4, Math.Min(grid.Columns, (int)((right - left + gap) / (grid.Size + gap))));
                SetText(grid.Title, left, y, right - left - 90f, 26f);
                grid.Next.PositionXOffset = right - 34f;
                grid.Next.PositionYOffset = y - 2f;
                grid.Prev.PositionXOffset = right - 74f;
                grid.Prev.PositionYOffset = y - 2f;
                y += 32f;
                for (int index = 0; index < grid.Tiles.Count; index++)
                {
                    BannerTile tile = grid.Tiles[index];
                    int column = index % grid.Columns;
                    int row = index / grid.Columns;
                    if (column >= columns)
                    {
                        tile.Button.IsVisible = false;
                        continue;
                    }
                    tile.Button.PositionXOffset = left + column * (grid.Size + gap);
                    tile.Button.PositionYOffset = y + row * (grid.Size + gap);
                }
                y += grid.Rows * (grid.Size + gap) + 18f;
            }
        }

        // ---- actions and questions -----------------------------------------------------

        private void ClanAct(string path, string body, string done, Action<DikozemieClanState> then = null)
        {
            if (_clanBusy)
            {
                return;
            }
            _clanBusy = true;
            _clanNote = "…";
            _sheetNote = string.Empty;
            RefreshSidePanel();
            DikozemieClans.Act(path, body, (state, error) =>
            {
                _clanBusy = false;
                _clanNote = error ?? done ?? string.Empty;
                _sheetNote = error ?? done ?? string.Empty;
                _sheetRevision = -1;
                if (state != null)
                {
                    then?.Invoke(state);
                }
                RefreshSidePanel();
                LayoutContent();
            });
        }

        private static void AskText(string title, string text, string current, int limit, Action<string> done)
        {
            InformationManager.ShowTextInquiry(new TextInquiryData(title, text, true, true, "ГОТОВО", "ОТМЕНА",
                value => done((value ?? string.Empty).Trim()), null, false,
                value => (value ?? string.Empty).Trim().Length <= limit
                    ? new Tuple<bool, string>(true, string.Empty)
                    : new Tuple<bool, string>(false, "Не длиннее " + limit + " знаков."),
                string.Empty, current ?? string.Empty), true);
        }

        private static void AskAmount(string title, string text, int most, Action<int> done)
        {
            InformationManager.ShowTextInquiry(new TextInquiryData(title, text, true, true, "ГОТОВО", "ОТМЕНА",
                value =>
                {
                    if (int.TryParse((value ?? string.Empty).Trim(), out int amount) && amount > 0)
                    {
                        done(amount);
                    }
                }, null, false,
                value => int.TryParse((value ?? string.Empty).Trim(), out int amount) && amount > 0 && amount <= Math.Max(1, most)
                    ? new Tuple<bool, string>(true, string.Empty)
                    : new Tuple<bool, string>(false, "Число от 1 до " + Math.Max(1, most) + "."),
                string.Empty, string.Empty), true);
        }

        private void LeaveClan()
        {
            DikozemieClanFull clan = DikozemieClans.Clan;
            if (clan == null)
            {
                return;
            }
            InquiryConfirm("ПОКИНУТЬ КЛАН", "Уйти из клана «" + clan.Name + "»? Вклады в казну остаются клану." +
                (clan.Rank == "leader" && clan.Members.Count > 1 ? " Клан поведёт следующий по званию." : string.Empty),
                () => ClanAct("leave", "{}", "Вы ушли из клана.", _ => ShowClanSection(ClanSection.Main, "Вы ушли из клана.")));
        }

        private void DisbandClan()
        {
            DikozemieClanFull clan = DikozemieClans.Clan;
            if (clan == null)
            {
                return;
            }
            InquiryConfirm("РАСПУСТИТЬ КЛАН", "Распустить клан «" + clan.Name + "»? Крепости вернутся прежним владельцам, " +
                "наёмники разойдутся, казна (" + FormatCoins(clan.Coins) + " мон.) — вам.",
                () => ClanAct("disband", "{}", "Клан распущен.", _ => ShowClanSection(ClanSection.Main, "Клан распущен.")));
        }

        // ---- the tables ----------------------------------------------------------------

        private bool RenderClanSheet()
        {
            if (RenderWatchSheet())
            {
                return true;
            }
            DikozemieClanState state = DikozemieClans.State;
            DikozemieClanFull clan = state?.Clan;
            switch (_sheetMode)
            {
                case TradeSheet.ClanMembers:
                    if (clan != null) RenderClanMembers(clan);
                    return true;
                case TradeSheet.ClanTreasury:
                    if (clan != null) RenderClanTreasury(clan);
                    return true;
                case TradeSheet.ClanLands:
                    if (clan != null) RenderClanLands(clan);
                    return true;
                case TradeSheet.ClanWarband:
                    if (clan != null) RenderClanWarband(clan);
                    return true;
                case TradeSheet.ClanRelations:
                    if (clan != null) RenderClanRelations(clan);
                    return true;
                case TradeSheet.ClanList:
                    RenderClanList(clan);
                    return true;
                case TradeSheet.ClanChronicle:
                    if (clan != null) RenderClanChronicle(clan);
                    return true;
                case TradeSheet.Mercs:
                    RenderMercs();
                    return true;
                case TradeSheet.Garrison:
                    RenderGarrison();
                    return true;
                case TradeSheet.LandRates:
                    RenderLandRates();
                    return true;
            }
            return false;
        }

        private void ClanSheetHeader(string title)
        {
            _sheetTitle.Text = title;
            DikozemieClanFull clan = DikozemieClans.Clan;
            _sheetStatus.Text = clan == null
                ? "МОНЕТЫ: " + FormatCoins(DikozemieArmoryProgression.HeroResources)
                : "КАЗНА КЛАНА: " + FormatCoins(clan.Coins) + "   ·   ВАШИ: " + FormatCoins(DikozemieArmoryProgression.HeroResources);
        }

        private static readonly string[] RankOrder = { "recruit", "member", "officer", "leader" };

        private void RenderClanMembers(DikozemieClanFull clan)
        {
            ClanSheetHeader("ЛЮДИ КЛАНА · " + clan.Name.ToUpperInvariant());
            SetColumns(3.2f, 1.6f, 2.6f, 1.2f, 1.2f);
            SetAlign(TextHorizontalAlignment.Left, TextHorizontalAlignment.Left, TextHorizontalAlignment.Left,
                TextHorizontalAlignment.Right, TextHorizontalAlignment.Right);
            SetHeads("ИМЯ", "ЗВАНИЕ", "ГДЕ", "СЛАВА", "ВНЁС");
            int row = 0;
            bool leader = clan.Rank == "leader";
            bool officer = DikozemieClans.Can("rank");
            foreach (DikozemieClanApplication app in clan.Applications.Take(4))
            {
                if (!DikozemieClans.Can("admit"))
                {
                    break;
                }
                string pid = app.Player;
                SetRow(row++, new[] { app.Name, "просится", string.IsNullOrEmpty(app.Text) ? "—" : "«" + app.Text + "»",
                        FormatCoins(app.Rating), string.Empty },
                    new[] { SheetWarn, SheetWarn, SheetDim, default(Color), default(Color) }, false, null,
                    ("ПРИНЯТЬ", !_clanBusy, () => ClanAct("admit", "{\"player\":" + DikozemieEconomy.Json(pid) + "}",
                        app.Name + " принят в клан.")),
                    ("ОТКАЗАТЬ", !_clanBusy, () => ClanAct("admit", "{\"player\":" + DikozemieEconomy.Json(pid) + ",\"no\":true}",
                        "Просьба отклонена.")));
            }
            foreach (DikozemieClanMember m in clan.Members)
            {
                if (row >= SheetRows)
                {
                    break;
                }
                string pid = m.Id;
                bool me = pid == DikozemieWorld.MyId;
                string where = m.Online ? (string.IsNullOrEmpty(m.Where) ? "в мире" : m.Where) : "был " + DikozemieClans.Date(m.Seen);
                Color nameColor = me ? SheetGold : m.Online ? SheetInk : SheetDim;
                var actions = new List<(string, bool, Action)>();
                if (!me && officer && (leader || m.Rank == "recruit" || m.Rank == "member"))
                {
                    int at = Array.IndexOf(RankOrder, m.Rank);
                    string up = at >= 0 && at + 1 < RankOrder.Length ? RankOrder[at + 1] : string.Empty;
                    bool canUp = up.Length > 0 && (leader ? up != "leader" || true : up == "member");
                    if (canUp && up == "leader")
                    {
                        actions.Add(("В ГЛАВЫ", !_clanBusy, () => InquiryConfirm("ПЕРЕДАТЬ ГЛАВЕНСТВО",
                            "Передать главенство клана: " + m.Name + "? Вы станете воеводой.",
                            () => ClanAct("rank", "{\"player\":" + DikozemieEconomy.Json(pid) + ",\"rank\":\"leader\"}",
                                m.Name + " — глава клана."))));
                    }
                    else if (canUp)
                    {
                        actions.Add(("ПОВЫСИТЬ", !_clanBusy, () => ClanAct("rank", "{\"player\":" + DikozemieEconomy.Json(pid) +
                            ",\"rank\":\"" + up + "\"}", m.Name + " повышен.")));
                    }
                    actions.Add(("ИЗГНАТЬ", !_clanBusy, () => InquiryConfirm("ИЗГНАТЬ ИЗ КЛАНА", "Изгнать " + m.Name + " из клана?",
                        () => ClanAct("kick", "{\"player\":" + DikozemieEconomy.Json(pid) + "}", m.Name + " изгнан."))));
                }
                SetRow(row++, new[] { m.Name + (me ? " (вы)" : string.Empty), m.RankName.ToLowerInvariant(), where,
                        FormatCoins(m.Rating), FormatCoins(m.Given) },
                    new[] { nameColor, m.Rank == "leader" ? SheetGold : default(Color), m.Online ? SheetGood : SheetDim,
                        default(Color), default(Color) },
                    false, null, actions.Take(SheetActions).ToArray());
            }
            SetDetail("Звания: глава решает всё; воевода зовёт и принимает людей, нанимает воинов, ставит гарнизоны, меняет " +
                "пошлины и объявляет осаду; дружинник и новик вносят в казну и идут в лагерь. Войну, мир и казну держит глава." +
                (string.IsNullOrEmpty(_sheetNote) ? string.Empty : "\n\n" + _sheetNote));
        }

        private void RenderClanTreasury(DikozemieClanFull clan)
        {
            ClanSheetHeader("КАЗНА КЛАНА · " + clan.Name.ToUpperInvariant());
            SetColumns(1.6f, 2.6f, 4.2f, 1.2f, 1.3f);
            SetAlign(TextHorizontalAlignment.Left, TextHorizontalAlignment.Left, TextHorizontalAlignment.Left,
                TextHorizontalAlignment.Right, TextHorizontalAlignment.Right);
            SetHeads("КОГДА", "СТАТЬЯ", "ПОДРОБНО", "СУММА", "ОСТАТОК");
            int row = 0;
            foreach (DikozemieClanLedgerRow r in clan.Ledger.Take(SheetRows))
            {
                SetRow(row++, new[] { DikozemieClans.Date(r.At), r.Title, r.Note, (r.Amount > 0 ? "+" : string.Empty) + FormatCoins(r.Amount),
                        FormatCoins(r.Balance) },
                    new[] { SheetDim, default(Color), SheetDim, r.Amount >= 0 ? SheetGood : SheetBad, default(Color) },
                    false, null);
            }
            if (row == 0)
            {
                SetRow(0, new[] { string.Empty, "Казна пуста", "Внесите монеты: справа «ВНЕСТИ В КАЗНУ»." }, null, false, null);
            }
            SetDetail("Доход земли за день: +" + FormatCoins((int)Math.Round(clan.IncomeDay)) + " (оброк деревень, поля и суд, рынок города), " +
                "плюс пошлины с проданного на рынке, мостовщина и налог мастерских. Жалованье дружине и гарнизонам: -" +
                FormatCoins((int)Math.Round(clan.WagesDay)) + " в день. Торговые сутки — час." +
                (string.IsNullOrEmpty(_sheetNote) ? string.Empty : "\n\n" + _sheetNote));
        }

        private void RenderClanLands(DikozemieClanFull clan)
        {
            ClanSheetHeader("ЗЕМЛИ КЛАНА · " + clan.Holdings.Count + " ИЗ " + clan.MaxHoldings);
            SetColumns(2.6f, 1.1f, 1.1f, 2.2f, 1.2f, 1.2f);
            SetAlign(TextHorizontalAlignment.Left, TextHorizontalAlignment.Left, TextHorizontalAlignment.Right,
                TextHorizontalAlignment.Left, TextHorizontalAlignment.Right, TextHorizontalAlignment.Right);
            SetHeads("КРЕПОСТЬ", "ЧТО", "ГАРНИЗОН", "ПОШЛИНЫ", "ДОХОД/ДЕНЬ", "ВЧЕРА");
            int row = 0;
            foreach (DikozemieClanHolding h in clan.Holdings.Take(SheetRows))
            {
                string node = h.Node;
                string rates = (h.Kind == "town" ? "рынок " + h.Rates.Market + "% · мастерские " + h.Rates.Workshop + "% · " : string.Empty) +
                    "мост " + h.Rates.Bridge + (h.Patrol ? " · дозоры" : string.Empty);
                string status = h.Siege ? " · ОСАДА" : h.EmptyIn > 0 ? " · ПУСТО" : string.Empty;
                bool selected = _clanSelected == node;
                SetRow(row++, new[] { h.Name + status, h.Kind == "town" ? "город" : "замок", h.Men.ToString(CultureInfo.InvariantCulture),
                        rates, "+" + FormatCoins((int)Math.Round(h.IncomeDay)), FormatCoins(h.Stats.Yesterday) },
                    new[] { h.Siege || h.EmptyIn > 0 ? SheetBad : SheetInk, SheetDim, h.Men == 0 ? SheetBad : default(Color), SheetDim,
                        SheetGood, default(Color) },
                    selected, () =>
                    {
                        _clanSelected = node;
                        _sheetRevision = -1;
                    },
                    ("ПОШЛИНЫ", DikozemieClans.Can("rates") && !_clanBusy, () =>
                    {
                        _landNode = node;
                        OpenSheet(TradeSheet.LandRates);
                    }));
            }
            if (row == 0)
            {
                SetRow(0, new[] { "Земли нет", string.Empty, string.Empty, "Замок берут штурмом: подойдите к стенам с людьми." },
                    new[] { SheetDim }, false, null);
            }
            DikozemieClanHolding pick = clan.Holdings.FirstOrDefault(h => h.Node == _clanSelected) ?? clan.Holdings.FirstOrDefault();
            string detail = pick == null
                ? "Взятая крепость приносит оброк своих деревень, доход с полей и суда; город — ещё и рыночную пошлину и налог мастерских."
                : pick.Name + ": с " + DikozemieClans.Date(pick.Since) + (string.IsNullOrEmpty(pick.Taken) ? string.Empty : ", взята у клана " + pick.Taken) +
                  ". Деревень: " + pick.Villages + ". Гарнизон: " + DikozemieClans.TroopsWords(pick.Garrison) + ". Всего принесла " +
                  FormatCoins(pick.Stats.Total) + " мон." +
                  (pick.CalmIn > 0 ? " Стены полны людей: штурмовать её нельзя ещё " + DikozemieClans.Minutes(pick.CalmIn) + "." : string.Empty) +
                  (pick.EmptyIn > 0 ? " В крепости никого: через " + DikozemieClans.Minutes(pick.EmptyIn) +
                      " её займёт прежний владелец. Поставьте гарнизон на месте." : string.Empty);
            SetDetail(detail + (string.IsNullOrEmpty(_sheetNote) ? string.Empty : "\n\n" + _sheetNote));
        }

        private void RenderClanWarband(DikozemieClanFull clan)
        {
            ClanSheetHeader("ДРУЖИНА · " + DikozemieClans.Total(clan.Troops) + " ЧЕЛ.");
            SetColumns(2.4f, 1.2f, 1.4f, 1.6f, 4f);
            SetAlign(TextHorizontalAlignment.Left, TextHorizontalAlignment.Right, TextHorizontalAlignment.Right,
                TextHorizontalAlignment.Right, TextHorizontalAlignment.Left);
            SetHeads("КТО", "В ДРУЖИНЕ", "В КРЕПОСТЯХ", "ЖАЛОВАНЬЕ", "КАКИЕ ОНИ");
            int row = 0;
            foreach (DikozemieTroopType type in DikozemieClans.TroopTypes)
            {
                string kind = type.Type;
                clan.Troops.TryGetValue(kind, out int band);
                int held = clan.Holdings.Sum(h => h.Garrison != null && h.Garrison.TryGetValue(kind, out int n) ? n : 0);
                SetRow(row++, new[] { type.Plural.Substring(0, 1).ToUpperInvariant() + type.Plural.Substring(1), band.ToString(CultureInfo.InvariantCulture),
                        held.ToString(CultureInfo.InvariantCulture), type.Wage + " мон./день", type.About },
                    new[] { default(Color), band > 0 ? SheetGold : SheetDim, held > 0 ? SheetInk : SheetDim, SheetDim, SheetDim },
                    false, null,
                    ("РАСПУСТИТЬ", band > 0 && DikozemieClans.Can("hire") && !_clanBusy, () => ClanAct("dismiss",
                        "{\"type\":\"" + kind + "\",\"count\":1}", type.Name + " отпущен.")));
            }
            SetDetail("Нанимают в тавернах городов, задаток — " + DikozemieClans.Rules.HireDays.ToString("0", CultureInfo.InvariantCulture) +
                " дня жалованья из казны клана. Копейщики и лучники не пойдут за безвестным, латник — только за прославленным. " +
                "На штурм дружина идёт вся (у ворот до десяти человек на сторону); после взятия часть остаётся на стенах." +
                (string.IsNullOrEmpty(_sheetNote) ? string.Empty : "\n\n" + _sheetNote));
        }

        private void RenderClanRelations(DikozemieClanFull clan)
        {
            ClanSheetHeader("ВОЙНА И МИР · " + clan.Name.ToUpperInvariant());
            SetColumns(3f, 1f, 1f, 2.6f);
            SetAlign(TextHorizontalAlignment.Left, TextHorizontalAlignment.Right, TextHorizontalAlignment.Right,
                TextHorizontalAlignment.Left);
            SetHeads("КЛАН", "ЛЮДЕЙ", "ЗЕМЕЛЬ", "ОТНОШЕНИЯ");
            int row = 0;
            bool can = DikozemieClans.Can("war");
            foreach (DikozemieClanRelation r in clan.Relations.OrderByDescending(r => r.State == "war").Take(SheetRows))
            {
                string id = r.Id;
                string state = r.State == "war"
                    ? "ВОЙНА с " + DikozemieClans.Date(r.Since) + (r.Offer == "theirs" ? " · они просят мира" : r.Offer == "ours" ? " · вы предложили мир" : string.Empty)
                    : r.State == "truce" ? "перемирие до " + DikozemieClans.Date(r.Until) : "мир";
                var actions = new List<(string, bool, Action)>();
                if (r.State == "war")
                {
                    actions.Add((r.Offer == "theirs" ? "ПРИНЯТЬ МИР" : "МИР", can && r.Offer != "ours" && !_clanBusy, () =>
                        ClanAct("peace", "{\"clan\":" + DikozemieEconomy.Json(id) + "}",
                            r.Offer == "theirs" ? "Мир с кланом «" + r.Name + "»." : "Мир предложен.")));
                    if (r.Offer == "theirs")
                    {
                        actions.Add(("ОТКАЗАТЬ", can && !_clanBusy, () => ClanAct("peace",
                            "{\"clan\":" + DikozemieEconomy.Json(id) + ",\"no\":true}", "Мир отвергнут.")));
                    }
                }
                else
                {
                    actions.Add(("ВОЙНА", can && r.State != "truce" && !_clanBusy, () => InquiryConfirm("ОБЪЯВИТЬ ВОЙНУ",
                        "Объявить войну клану «" + r.Name + "»? Его люди станут вам врагами на дорогах, его крепости можно будет осаждать. " +
                        "Мир — только по согласию обоих.",
                        () => ClanAct("war", "{\"clan\":" + DikozemieEconomy.Json(id) + "}", "Война объявлена."))));
                }
                SetRow(row++, new[] { r.Name, r.Members.ToString(CultureInfo.InvariantCulture), r.Holdings.ToString(CultureInfo.InvariantCulture), state },
                    new[] { default(Color), default(Color), default(Color), r.State == "war" ? SheetBad : r.State == "truce" ? SheetWarn : SheetGood },
                    false, null, actions.Take(SheetActions).ToArray());
            }
            if (row == 0)
            {
                SetRow(0, new[] { "Других кланов пока нет" }, new[] { SheetDim }, false, null);
            }
            SetDetail("Война идёт до мира: один глава предлагает, другой принимает. На войне люди кланов бьются на дорогах без " +
                "клейма разбойника, а крепости друг друга берут штурмом. Владельцу дают время прийти на стены: лагерь стоит десять минут." +
                (string.IsNullOrEmpty(_sheetNote) ? string.Empty : "\n\n" + _sheetNote));
        }

        private void RenderClanList(DikozemieClanFull clan)
        {
            ClanSheetHeader("КЛАНЫ ДИКОЗЕМЬЯ");
            SetColumns(2.8f, 2f, 0.9f, 3.2f, 1.2f);
            SetAlign(TextHorizontalAlignment.Left, TextHorizontalAlignment.Left, TextHorizontalAlignment.Right,
                TextHorizontalAlignment.Left, TextHorizontalAlignment.Left);
            SetHeads("КЛАН", "ГЛАВА", "ЛЮДЕЙ", "ЗЕМЛИ", "ВАМ");
            int row = 0;
            List<DikozemieClanListRow> rows = DikozemieClans.List?.Clans ?? new List<DikozemieClanListRow>();
            foreach (DikozemieClanListRow c in rows.Take(SheetRows))
            {
                string id = c.Id;
                bool mine = clan != null && c.Id == clan.Id;
                var actions = new List<(string, bool, Action)>();
                if (clan == null)
                {
                    actions.Add((c.Applied ? "ЖДЁТЕ" : "ПОПРОСИТЬСЯ", !c.Applied && !_clanBusy, () => AskText("ПРОСЬБА В КЛАН",
                        "Пара слов главе клана «" + c.Name + "» (можно пусто).", string.Empty, 120,
                        text => ClanAct("apply", "{\"clan\":" + DikozemieEconomy.Json(id) + ",\"text\":" + DikozemieEconomy.Json(text) + "}",
                            "Просьба отправлена. Ответ придёт, когда глава или воевода её прочтут.", _ => DikozemieClans.RefreshList()))));
                }
                SetRow(row++, new[] { c.Name + (string.IsNullOrEmpty(c.Motto) ? string.Empty : " · «" + c.Motto + "»"), c.LeaderName,
                        c.Members + (c.Online > 0 ? " (" + c.Online + ")" : string.Empty),
                        c.Holdings.Count == 0 ? "нет" : string.Join(", ", c.Holdings), mine ? "ваш" : DikozemieClans.RelationWord(c.Relation) },
                    new[] { mine ? SheetGold : default(Color), SheetDim, default(Color), SheetDim,
                        c.Relation == "war" ? SheetBad : mine ? SheetGold : SheetGood },
                    false, null, actions.ToArray());
            }
            if (row == 0)
            {
                SetRow(0, new[] { DikozemieClans.List == null ? "Спрашиваем казну…" : "Кланов ещё нет: станьте первым." },
                    new[] { SheetDim }, false, null);
            }
            SetDetail("В скобках — сколько людей клана сейчас в мире. Просьбу в клан читают глава и воеводы." +
                (string.IsNullOrEmpty(_sheetNote) ? string.Empty : "\n\n" + _sheetNote));
        }

        private void RenderClanChronicle(DikozemieClanFull clan)
        {
            ClanSheetHeader("ЛЕТОПИСЬ · " + clan.Name.ToUpperInvariant());
            SetColumns(1.4f, 8.6f);
            SetAlign(TextHorizontalAlignment.Left, TextHorizontalAlignment.Left);
            SetHeads("КОГДА", "ЧТО БЫЛО");
            int row = 0;
            foreach (DikozemieClanLogLine line in clan.Log.Take(SheetRows))
            {
                SetRow(row++, new[] { DikozemieClans.Date(line.At), line.Text }, new[] { SheetDim, default(Color) }, false, null);
            }
            SetDetail("Новые записи — сверху.");
        }
    }
}
