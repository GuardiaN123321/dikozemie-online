using System;
using System.Collections.Generic;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace Dikozemie.Online
{
    // Stage 1 of the plan (customer, 2026-11-05: «не будет вот этих разных
    // вкладок "наняться матросом" и прочее»). The line under the header says
    // where the player is — «КАРТА › САРГОТ › АРЕНА» — and each step leads
    // back. «ГЕРОЙ» and «ИНВЕНТАРЬ» stay at hand on the right, as the
    // buttons of the singleplayer map. Used by the map, the arena page and
    // the hero screens, so it reads the shared state itself.
    public sealed class DikozemieLocationBarWidget : Widget
    {
        private const int MaxCrumbs = 3;
        private const float BarWidth = 1480f;
        private const int FontSize = 24;

        private enum Target
        {
            Map,
            Town,
            Here,
            Hero,
            Inventory,
            // Stage 6: the hero's clan, or how to found one.
            Clan,
            // The observer's screen.
            Watch
        }

        private sealed class Crumb
        {
            internal ButtonWidget Button;
            internal TextWidget Label;
            internal Widget Underline;
            internal TextWidget Separator;
            internal Target Target;
            internal bool Current;
        }

        private readonly Crumb[] _crumbs = new Crumb[MaxCrumbs];
        private Crumb _hero;
        private Crumb _inventory;
        private Crumb _clan;
        // The observer of the watch: his screen (only observers see it).
        private Crumb _watch;
        private TextWidget _coins;
        private Widget _coinIcon;
        private bool _built;
        private string _signature = string.Empty;
        private float _refreshDelay;

        private static readonly Color Bright = new Color(0.98f, 0.83f, 0.42f, 1f);
        private static readonly Color Dim = new Color(0.74f, 0.64f, 0.48f, 1f);
        private static readonly Color Hover = new Color(1f, 0.93f, 0.70f, 1f);

        public DikozemieLocationBarWidget(UIContext context)
            : base(context)
        {
        }

        protected override void OnConnectedToRoot()
        {
            base.OnConnectedToRoot();
            Build();
        }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            Build();
            _refreshDelay -= dt;
            if (_refreshDelay <= 0f)
            {
                _refreshDelay = 0.2f;
                Refresh();
            }
            foreach (Crumb crumb in AllCrumbs())
            {
                if (crumb?.Button == null || !crumb.Button.IsVisible)
                {
                    continue;
                }
                crumb.Label.Brush.FontColor = crumb.Current
                    ? Bright
                    : crumb.Button.IsHovered ? Hover : Dim;
            }
        }

        private IEnumerable<Crumb> AllCrumbs()
        {
            foreach (Crumb crumb in _crumbs)
            {
                yield return crumb;
            }
            yield return _hero;
            yield return _inventory;
            yield return _clan;
            yield return _watch;
        }

        private void Build()
        {
            if (_built || Context == null)
            {
                return;
            }
            _built = true;
            for (int index = 0; index < MaxCrumbs; index++)
            {
                _crumbs[index] = CreateCrumb("LocationCrumb" + index, index > 0);
            }
            _hero = CreateCrumb("LocationHero", false);
            _hero.Target = Target.Hero;
            _inventory = CreateCrumb("LocationInventory", false);
            _inventory.Target = Target.Inventory;
            _clan = CreateCrumb("LocationClan", false);
            _clan.Target = Target.Clan;
            _watch = CreateCrumb("LocationWatch", false);
            _watch.Target = Target.Watch;
            _coinIcon = new Widget(Context)
            {
                Id = "LocationCoinIcon",
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                SuggestedWidth = 28f,
                SuggestedHeight = 27f,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Sprite = Context.SpriteData.GetSprite("Inventory\\icon_gold"),
                DoNotAcceptEvents = true
            };
            AddChild(_coinIcon);
            _coins = CreateLabel("LocationCoins", 22);
            _coins.Brush.TextHorizontalAlignment = TextHorizontalAlignment.Right;
            _coins.Brush.FontColor = Bright;
            AddChild(_coins);
            Refresh();
        }

        private Crumb CreateCrumb(string id, bool withSeparator)
        {
            var crumb = new Crumb();
            if (withSeparator)
            {
                crumb.Separator = CreateLabel(id + "Separator", 26);
                crumb.Separator.Text = "›";
                crumb.Separator.Brush.FontColor = new Color(0.62f, 0.46f, 0.24f, 1f);
                crumb.Separator.SuggestedWidth = 30f;
                AddChild(crumb.Separator);
            }
            crumb.Button = new ButtonWidget(Context)
            {
                Id = id,
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.StretchToParent,
                HorizontalAlignment = HorizontalAlignment.Left,
                DoNotPassEventsToChildren = true
            };
            crumb.Label = CreateLabel(id + "Text", FontSize);
            crumb.Label.WidthSizePolicy = SizePolicy.StretchToParent;
            crumb.Button.AddChild(crumb.Label);
            crumb.Underline = new Widget(Context)
            {
                Id = id + "Underline",
                WidthSizePolicy = SizePolicy.StretchToParent,
                HeightSizePolicy = SizePolicy.Fixed,
                SuggestedHeight = 3f,
                VerticalAlignment = VerticalAlignment.Bottom,
                MarginLeft = 10f,
                MarginRight = 10f,
                MarginBottom = 4f,
                Sprite = Context.SpriteData.GetSprite("BlankWhiteSquare_9"),
                Color = new Color(0.88f, 0.62f, 0.28f, 1f),
                DoNotAcceptEvents = true
            };
            crumb.Button.AddChild(crumb.Underline);
            crumb.Button.ClickEventHandlers.Add(_ => Navigate(crumb.Target));
            AddChild(crumb.Button);
            return crumb;
        }

        private TextWidget CreateLabel(string id, int size)
        {
            var label = new TextWidget(Context)
            {
                Id = id,
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.StretchToParent,
                HorizontalAlignment = HorizontalAlignment.Left,
                DoNotAcceptEvents = true
            };
            Brush brush = Context.GetBrush("MPLobby.Matchmaking.LeftMenuText");
            if (brush != null)
            {
                label.Brush = brush.Clone();
            }
            else
            {
                label.Brush.Font = Context.FontFactory.GetMappedFontForLocalization("Galahad");
            }
            label.Brush.FontSize = size;
            label.Brush.FontColor = Dim;
            label.Brush.TextHorizontalAlignment = TextHorizontalAlignment.Center;
            label.Brush.TextVerticalAlignment = TextVerticalAlignment.Center;
            return label;
        }

        // What the line shows: the steps from the map to the screen on view.
        private List<Tuple<string, Target>> Steps(out int current, out bool heroSelected,
            out bool inventorySelected)
        {
            var steps = new List<Tuple<string, Target>>();
            int tab = DikozemieLobbyTabButtonWidget.SelectedTabIndex;
            heroSelected = tab == DikozemieLobbyTabButtonWidget.HeroSectionIndex;
            inventorySelected = false;
            if (!DikozemieHeroProfile.IsCreated)
            {
                steps.Add(Tuple.Create("СОЗДАНИЕ ГЕРОЯ", Target.Hero));
                current = 0;
                return steps;
            }
            DikozemieTravelState.EnsureLoadedForHero();
            DikozemieLobbyMapNode here = DikozemieTown.Here;
            string place = here == null ? null : DikozemieTown.DisplayName(here).ToUpperInvariant();
            steps.Add(Tuple.Create("КАРТА", Target.Map));
            switch (tab)
            {
                case DikozemieLobbyTabButtonWidget.ArenaTabIndex:
                {
                    if (place != null)
                    {
                        steps.Add(Tuple.Create(place, Target.Town));
                    }
                    bool naval = DikozemieArenaMatchSelection.MenuVenue == DikozemieBattleVenue.Naval;
                    steps.Add(Tuple.Create(
                        naval ? "ПОРТ · МАТРОСЫ" : place == null ? "БОЙ НА ДОРОГЕ" : "АРЕНА",
                        Target.Here));
                    break;
                }
                case DikozemieLobbyTabButtonWidget.InventorySectionIndex:
                {
                    DikozemieMarketKind market = DikozemieTown.MarketKind;
                    if (market == DikozemieMarketKind.Peddler)
                    {
                        steps.Add(Tuple.Create("ТОРГОВЕЦ НА ДОРОГЕ", Target.Here));
                    }
                    else if (market != DikozemieMarketKind.None && place != null)
                    {
                        steps.Add(Tuple.Create(place, Target.Town));
                        steps.Add(Tuple.Create(
                            market == DikozemieMarketKind.Armory ? "ОРУЖЕЙНАЯ" : "РЫНОК",
                            Target.Here));
                    }
                    else
                    {
                        inventorySelected = true;
                    }
                    break;
                }
                case DikozemieLobbyTabButtonWidget.MapSectionIndex:
                    if (DikozemieClans.IsScreenOpen)
                    {
                        break;
                    }
                    if (DikozemieTown.IsTownScreenOpen && place != null)
                    {
                        steps.Add(Tuple.Create(place, Target.Town));
                    }
                    break;
            }
            bool clanScreen = tab == DikozemieLobbyTabButtonWidget.MapSectionIndex && DikozemieClans.IsScreenOpen;
            current = heroSelected || inventorySelected || clanScreen ? -1 : steps.Count - 1;
            return steps;
        }

        private void Refresh()
        {
            if (!_built)
            {
                return;
            }
            List<Tuple<string, Target>> steps = Steps(
                out int current, out bool heroSelected, out bool inventorySelected);
            int coins = DikozemieHeroProfile.IsCreated ? DikozemieArmoryProgression.HeroResources : -1;
            DikozemieEconomyState economy = DikozemieEconomy.IsOnline ? DikozemieEconomy.State : null;
            string pack = economy != null && economy.Load > 0
                ? "ОБОЗ " + economy.Load + " ИЗ " + economy.Capacity + "   ·   "
                : string.Empty;
            bool watchSelected = DikozemieLobbyTabButtonWidget.SelectedTabIndex ==
                DikozemieLobbyTabButtonWidget.MapSectionIndex && DikozemieWatch.IsScreenOpen;
            bool clanSelected = DikozemieLobbyTabButtonWidget.SelectedTabIndex ==
                DikozemieLobbyTabButtonWidget.MapSectionIndex && DikozemieClans.IsScreenOpen && !watchSelected;
            bool observer = DikozemieWatch.IsObserver;
            string clanWord = DikozemieClans.InClan ? "КЛАН" : "КЛАНЫ";
            string signature = string.Join("|", steps.ConvertAll(step => step.Item1)) + "#" +
                current + heroSelected + inventorySelected + clanSelected + clanWord + coins + pack +
                watchSelected + observer;
            if (signature == _signature)
            {
                return;
            }
            _signature = signature;

            float x = 22f;
            for (int index = 0; index < MaxCrumbs; index++)
            {
                Crumb crumb = _crumbs[index];
                bool visible = index < steps.Count;
                crumb.Button.IsVisible = visible;
                if (crumb.Separator != null)
                {
                    crumb.Separator.IsVisible = visible;
                }
                if (!visible)
                {
                    continue;
                }
                if (crumb.Separator != null)
                {
                    crumb.Separator.PositionXOffset = x;
                    x += crumb.Separator.SuggestedWidth;
                }
                crumb.Target = steps[index].Item2;
                crumb.Current = index == current;
                crumb.Label.Text = steps[index].Item1;
                float width = TextWidth(steps[index].Item1, FontSize);
                crumb.Button.SuggestedWidth = width;
                crumb.Button.PositionXOffset = x;
                crumb.Underline.IsVisible = crumb.Current;
                x += width;
            }

            bool created = DikozemieHeroProfile.IsCreated;
            float right = BarWidth - 18f;
            _coins.IsVisible = created;
            _coinIcon.IsVisible = created;
            if (created)
            {
                _coins.Text = pack + "МОНЕТЫ: " + DikozemieLobbyMapWidget.FormatCoins(Math.Max(0, coins));
                float coinWidth = TextWidth(_coins.Text, 22) - 20f;
                _coins.SuggestedWidth = coinWidth;
                // The coin sprite is not loaded on every screen of the
                // line: the word says it everywhere alike.
                _coinIcon.IsVisible = false;
                _coins.PositionXOffset = right - coinWidth;
                right = _coins.PositionXOffset - 26f;
            }
            PlaceRight(_inventory, "ИНВЕНТАРЬ", inventorySelected, created, ref right);
            PlaceRight(_hero, "ГЕРОЙ", heroSelected, created, ref right);
            PlaceRight(_clan, clanWord, clanSelected, created, ref right);
            PlaceRight(_watch, "НАБЛЮДЕНИЕ", watchSelected, created && observer, ref right);
        }

        private void PlaceRight(Crumb crumb, string text, bool selected, bool visible, ref float right)
        {
            crumb.Button.IsVisible = visible;
            if (!visible)
            {
                return;
            }
            float width = TextWidth(text, FontSize);
            crumb.Label.Text = text;
            crumb.Current = selected;
            crumb.Underline.IsVisible = selected;
            crumb.Button.SuggestedWidth = width;
            crumb.Button.PositionXOffset = right - width;
            right -= width + 8f;
        }

        // Condensed capitals of the header font: about 0.56 of the size each.
        private static float TextWidth(string text, int size) =>
            (text?.Length ?? 0) * size * 0.56f + 44f;

        private static void Navigate(Target target)
        {
            if (target == Target.Clan)
            {
                if (DikozemieHeroAppearanceEditor.IsOpen)
                {
                    return;
                }
                DikozemieClans.RequestScreen();
                return;
            }
            if (target == Target.Watch)
            {
                if (DikozemieHeroAppearanceEditor.IsOpen)
                {
                    return;
                }
                DikozemieWatch.RequestScreen();
                return;
            }
            int tab;
            switch (target)
            {
                case Target.Map:
                case Target.Town:
                    tab = DikozemieLobbyTabButtonWidget.MapSectionIndex;
                    break;
                case Target.Hero:
                    tab = DikozemieLobbyTabButtonWidget.HeroSectionIndex;
                    break;
                case Target.Inventory:
                    tab = DikozemieLobbyTabButtonWidget.InventorySectionIndex;
                    break;
                default:
                    return;
            }
            if (DikozemieHeroAppearanceEditor.IsOpen)
            {
                DikozemieHeroAppearanceEditor.RequestNavigateToTab(tab);
                return;
            }
            DikozemieLobbyShellIntegration.NavigateFromLocationBar(
                tab, target == Target.Town);
        }
    }
}
