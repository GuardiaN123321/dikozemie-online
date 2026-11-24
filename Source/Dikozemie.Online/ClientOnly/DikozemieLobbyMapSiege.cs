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
    // Stage 6 on the map and in the towns: the banners of the clans over the
    // places they hold, siege camps under the walls, the siege screen and the
    // storm (a battle on the battle servers with unequal sides), a clan's own
    // castle (garrison, tolls, patrols) and the mercenaries of a tavern.
    public sealed partial class DikozemieLobbyMapWidget
    {
        private const int LandFlagCount = 24;
        private const int SiegeMarkerCount = 6;

        private sealed class LandFlag
        {
            internal Widget Pole;
            internal ImageIdentifierWidget Banner;
            internal string Code = string.Empty;
        }

        private sealed class SiegeMarker
        {
            internal Widget Ring;
            internal TextWidget Label;
        }

        private readonly List<LandFlag> _landFlags = new List<LandFlag>();
        private readonly List<SiegeMarker> _siegeMarkers = new List<SiegeMarker>();
        private ButtonWidget _siegeButton;
        private float _siegePulse;

        // The server's word about a place (cached a few seconds); asks again
        // when it is old.
        private DikozemieLandView LandFor(DikozemieLobbyMapNode node)
        {
            if (node == null || !DikozemieEconomy.IsOnline)
            {
                return null;
            }
            if ((_landAsked != node.Id || (DateTime.UtcNow - _landAskedUtc).TotalSeconds > 6) && !_landLoading)
            {
                _landAsked = node.Id;
                _landAskedUtc = DateTime.UtcNow;
                _landLoading = true;
                DikozemieClans.Land(node.Id, (view, error) =>
                {
                    _landLoading = false;
                    _landView = view;
                    RefreshSidePanel();
                });
            }
            return _landView != null && _landView.Node == node.Id ? _landView : null;
        }

        // The settlement card's word from the server: whose it is, what it takes.
        private DikozemieLandView _landView;
        private string _landAsked = string.Empty;
        private DateTime _landAskedUtc = DateTime.MinValue;
        private bool _landLoading;

        // The castle being managed (garrison, tolls) or whose tavern hires.
        private string _landNode = string.Empty;
        private DikozemieMercs _mercs;

        // The siege screen.
        private bool _siegeOpen;
        private string _siegeId = string.Empty;
        private string _siegeShownState = string.Empty;
        private string _siegeStartedFight = string.Empty;
        private string _siegeNote = string.Empty;
        private readonly HashSet<string> _siegesAnnounced = new HashSet<string>(StringComparer.Ordinal);
        // A camp just set: its screen opens when the world brings it.
        private string _siegePending = string.Empty;
        private DateTime _siegePendingUtc;
        private BannerTableauWidget _foeBanner;

        // ---- building ---------------------------------------------------------

        private void BuildLandLayer()
        {
            for (int index = 0; index < SiegeMarkerCount; index++)
            {
                var marker = new SiegeMarker
                {
                    Ring = new Widget(Context)
                    {
                        Id = "SiegeRing" + index,
                        WidthSizePolicy = SizePolicy.Fixed,
                        HeightSizePolicy = SizePolicy.Fixed,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        Sprite = Context.SpriteData.GetSprite("BlankWhiteCircle"),
                        Color = new Color(0.85f, 0.15f, 0.08f, 0.45f),
                        DoNotAcceptEvents = true,
                        IsVisible = false
                    }
                };
                AddChild(marker.Ring);
                marker.Label = CreateText("SiegeLabel" + index, string.Empty, 16, new Color(0.45f, 0.04f, 0.02f, 1f));
                marker.Label.WidthSizePolicy = SizePolicy.Fixed;
                marker.Label.HeightSizePolicy = SizePolicy.Fixed;
                marker.Label.HorizontalAlignment = HorizontalAlignment.Left;
                marker.Label.VerticalAlignment = VerticalAlignment.Top;
                marker.Label.SuggestedWidth = 220f;
                marker.Label.SuggestedHeight = 24f;
                marker.Label.Brush.DefaultStyle.TextGlowColor = new Color(0.98f, 0.94f, 0.82f, 1f);
                marker.Label.Brush.DefaultStyle.TextGlowRadius = 0.2f;
                marker.Label.Brush.DefaultStyle.TextBlur = 0.6f;
                marker.Label.IsVisible = false;
                AddChild(marker.Label);
                _siegeMarkers.Add(marker);
            }
            for (int index = 0; index < LandFlagCount; index++)
            {
                var flag = new LandFlag
                {
                    Pole = new Widget(Context)
                    {
                        Id = "LandFlagPole" + index,
                        WidthSizePolicy = SizePolicy.Fixed,
                        HeightSizePolicy = SizePolicy.Fixed,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        Sprite = Context.SpriteData.GetSprite("BlankWhiteSquare_9"),
                        Color = new Color(0.20f, 0.12f, 0.05f, 1f),
                        DoNotAcceptEvents = true,
                        IsVisible = false
                    },
                    Banner = new ImageIdentifierWidget(Context)
                    {
                        Id = "LandFlagBanner" + index,
                        WidthSizePolicy = SizePolicy.Fixed,
                        HeightSizePolicy = SizePolicy.Fixed,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        DoNotAcceptEvents = true
                    }
                };
                flag.Banner.TextureProviderName = "BannerImageTextureProvider";
                flag.Banner.IsVisible = false;
                AddChild(flag.Pole);
                AddChild(flag.Banner);
                _landFlags.Add(flag);
            }
            _siegeButton = CreatePanelButton("MapSiege", "ОСАДИТЬ", 264f);
            _siegeButton.ClickEventHandlers.Add(_ => OnSiegeButton());
            _siegeButton.IsVisible = false;
        }

        private void RaiseLandLayer()
        {
            foreach (SiegeMarker marker in _siegeMarkers)
            {
                marker.Ring.SetSiblingIndex(ChildCount - 1);
                marker.Label.SetSiblingIndex(ChildCount - 1);
            }
            foreach (LandFlag flag in _landFlags)
            {
                flag.Pole.SetSiblingIndex(ChildCount - 1);
                flag.Banner.SetSiblingIndex(ChildCount - 1);
            }
        }

        // ---- the map: banners over the clans' places, siege camps ----------------------

        private void LayoutLands(float mapX, float mapY, float mapWidth, float mapHeight,
            float viewportX, float viewportY, float viewportWidth, float viewportHeight)
        {
            if (_landFlags.Count == 0)
            {
                return;
            }
            bool covered = LayerOpen || _roadOpen;
            float scaleX = mapWidth / SourceWidth;
            float scaleY = mapHeight / SourceHeight;
            float right = viewportX + viewportWidth;
            float bottom = viewportY + viewportHeight;
            int used = 0;
            if (!covered)
            {
                foreach (DikozemieLand land in DikozemieClans.Lands)
                {
                    // A clan's banner shows at every zoom: it is the news of the map.
                    DikozemieLobbyMapNode node = DikozemieLobbyMapCatalog.Find(land.Node);
                    if (used >= _landFlags.Count || node?.Position == null || string.IsNullOrEmpty(land.Banner))
                    {
                        continue;
                    }
                    float x = mapX + node.Position.X * scaleX;
                    float y = mapY + node.Position.Y * scaleY;
                    if (x < viewportX - 30f || x > right + 30f || y < viewportY - 60f || y > bottom + 10f)
                    {
                        continue;
                    }
                    LandFlag flag = _landFlags[used++];
                    float size = Math.Min(46f, 26f + _zoom * 4f);
                    if (flag.Code != land.Banner)
                    {
                        flag.Code = land.Banner;
                        flag.Banner.ImageId = land.Banner;
                    }
                    // A pole from the place up and to the left, the banner on it:
                    // the hero's pawn standing there does not hide it.
                    float poleX = x - size * 0.55f;
                    float top = y - size * 1.45f;
                    flag.Pole.SuggestedWidth = 3f;
                    flag.Pole.SuggestedHeight = y - top;
                    flag.Pole.PositionXOffset = poleX - 1.5f;
                    flag.Pole.PositionYOffset = top;
                    flag.Pole.IsVisible = true;
                    flag.Banner.SuggestedWidth = size;
                    flag.Banner.SuggestedHeight = size;
                    flag.Banner.PositionXOffset = poleX - size;
                    flag.Banner.PositionYOffset = top;
                    flag.Banner.IsVisible = true;
                }
            }
            for (int index = used; index < _landFlags.Count; index++)
            {
                _landFlags[index].Pole.IsVisible = false;
                _landFlags[index].Banner.IsVisible = false;
            }

            used = 0;
            if (!covered)
            {
                foreach (DikozemieSiege siege in DikozemieClans.Sieges.Where(s => s.Active))
                {
                    DikozemieLobbyMapNode node = DikozemieLobbyMapCatalog.Find(siege.Node);
                    if (used >= _siegeMarkers.Count || node?.Position == null)
                    {
                        continue;
                    }
                    float x = mapX + node.Position.X * scaleX;
                    float y = mapY + node.Position.Y * scaleY;
                    if (x < viewportX - 60f || x > right + 60f || y < viewportY - 60f || y > bottom + 60f)
                    {
                        continue;
                    }
                    SiegeMarker marker = _siegeMarkers[used++];
                    float beat = 0.5f + 0.5f * (float)Math.Sin(_siegePulse * 4f);
                    float ring = 34f + 10f * beat;
                    marker.Ring.SuggestedWidth = ring;
                    marker.Ring.SuggestedHeight = ring;
                    marker.Ring.PositionXOffset = x - ring * 0.5f;
                    marker.Ring.PositionYOffset = y - ring * 0.5f;
                    marker.Ring.Color = new Color(0.85f, 0.15f, 0.08f, 0.25f + 0.25f * beat);
                    marker.Ring.IsVisible = true;
                    marker.Label.Text = (siege.State == "battle" ? "ШТУРМ · " : "ОСАДА · " + DikozemieClans.Minutes(siege.AssaultLeft) + " · ") +
                        siege.Attacker.Name;
                    marker.Label.SuggestedWidth = Math.Max(120f, marker.Label.Text.Length * 16f * 0.5f + 12f);
                    marker.Label.PositionXOffset = x - marker.Label.SuggestedWidth * 0.5f;
                    marker.Label.PositionYOffset = y + 16f;
                    marker.Label.IsVisible = true;
                }
            }
            for (int index = used; index < _siegeMarkers.Count; index++)
            {
                _siegeMarkers[index].Ring.IsVisible = false;
                _siegeMarkers[index].Label.IsVisible = false;
            }
        }

        // ---- the settlement card ----------------------------------------------------

        // Lines for the card of a castle or town picked on the map; asks the
        // server for the rest when the pick changes.
        private string LandCardLines(DikozemieLobbyMapNode node)
        {
            if (node == null || !DikozemieEconomy.IsOnline)
            {
                return string.Empty;
            }
            string seat = DikozemieClans.SeatOf(node);
            DikozemieLandView land = LandFor(node);
            DikozemieLand held = DikozemieClans.LandOf(seat);
            var lines = new List<string>();
            if (held != null)
            {
                lines.Add((DikozemieTown.IsVillage(node) ? "Оброк платит клану «" : "Держит клан «") + held.Name + "»" +
                    (DikozemieClans.IsMyLand(held) ? " — ваш клан." : DikozemieClans.AtWarWith(held.Clan) ? " — вы воюете." : "."));
                lines.Add((DikozemieTown.IsTown(DikozemieLobbyMapCatalog.Find(seat)) ? "Рынок " + held.Market + "%, мастерские " +
                    held.Workshop + "%, " : string.Empty) + "мост " + held.Bridge + " мон." + (held.Patrol ? " Дороги под дозором." : string.Empty));
            }
            else if (land != null && !string.IsNullOrEmpty(land.Lord))
            {
                lines.Add("Держит клан " + land.Lord + (string.IsNullOrEmpty(land.Kingdom) ? "." : " · " + land.Kingdom + "."));
            }
            if (land != null && (land.Kind == "town" || land.Kind == "castle"))
            {
                lines.Add("Гарнизон: " + (land.Mine ? DikozemieClans.TroopsWords(land.Troops) : "около " + land.Garrison + " человек") +
                    ". Деревень: " + land.Villages + ", доход земли ~" + FormatCoins((int)Math.Round(land.IncomeDay)) + " в день.");
                if (land.Siege != null && land.Siege.Active)
                {
                    lines.Add("ОСАДА: лагерь клана «" + land.Siege.Attacker.Name + "»" +
                        (land.Siege.State == "battle" ? ", идёт штурм." : ", штурм через " + DikozemieClans.Minutes(land.Siege.AssaultLeft) + "."));
                }
                else if (land.CalmIn > 0)
                {
                    lines.Add("Стены только что взяты: штурмовать нельзя ещё " + DikozemieClans.Minutes(land.CalmIn) + ".");
                }
                else if (!land.Mine && DikozemieClans.InClan && !string.IsNullOrEmpty(land.SiegeBlock) && land.SiegeRule == land.SiegeBlock)
                {
                    lines.Add(land.SiegeBlock);
                }
            }
            return lines.Count == 0 ? string.Empty : "\n" + string.Join("\n", lines);
        }

        // The siege button of the map panel; returns whether it is shown.
        private bool PlaceSiegeButton(DikozemieLobbyMapNode node, ref float stack)
        {
            _siegeButton.IsVisible = false;
            if (node == null || !DikozemieEconomy.IsOnline || _travel == null || _travel.Boat ||
                !(DikozemieTown.IsTown(node) || DikozemieTown.IsCastle(node)))
            {
                return false;
            }
            DikozemieSiege siege = DikozemieClans.SiegeAt(node.Id);
            DikozemieLandView land = LandFor(node);
            if (siege != null && siege.Mine)
            {
                SetButtonText(_siegeButton, siege.Attacking ? "К ОСАДНОМУ ЛАГЕРЮ" : "К ОСАДЕ: НА СТЕНЫ");
            }
            else if (land != null && string.IsNullOrEmpty(land.SiegeBlock))
            {
                SetButtonText(_siegeButton, "ОСАДИТЬ · " + DikozemieClans.Rules.SiegeCost + " МОН.");
            }
            else
            {
                return false;
            }
            _siegeButton.IsVisible = true;
            _siegeButton.MarginBottom = stack;
            stack += 68f;
            return true;
        }

        private void OnSiegeButton()
        {
            OnSiegeAt(_selectedNode ?? NodeHere());
        }

        private void OnSiegeAt(DikozemieLobbyMapNode node)
        {
            if (node == null)
            {
                return;
            }
            DikozemieSiege siege = DikozemieClans.SiegeAt(node.Id);
            if (siege != null && siege.Mine)
            {
                OpenSiege(siege.Id);
                return;
            }
            DikozemieLandView land = _landView != null && _landView.Node == node.Id ? _landView : null;
            string owner = land?.Owner != null ? "клан «" + land.Owner.Name + "»" : "клан " + (land?.Lord ?? "лорда");
            int garrison = land?.Garrison ?? 0;
            int mine = DikozemieClans.Total(DikozemieClans.Clan?.Troops);
            Inquiry("ОСАДА · " + DisplayNodeName(node).ToUpperInvariant(),
                "Поставить осадный лагерь под стенами? Крепость держит " + owner + ", гарнизон около " + garrison + " человек.\n\n" +
                "Ваша дружина: " + mine + ". На штурм идут все люди клана из лагеря и вся дружина — у ворот до десяти на сторону. " +
                (land?.Owner != null
                    ? "Защитникам дают десять минут прийти на стены."
                    : "Штурм — через пару минут, или раньше по вашему слову.") +
                "\n\nЛестницы и таран — " + DikozemieClans.Rules.SiegeCost + " монет из казны клана.",
                "В ОСАДУ", "НЕТ", () => DikozemieClans.Act("siege/start", "{\"node\":" + DikozemieEconomy.Json(node.Id) + "}",
                    (state, error) =>
                    {
                        if (state?.Siege != null)
                        {
                            OpenSiege(state.Siege.Id);
                            return;
                        }
                        _travelNote = error ?? string.Empty;
                        RefreshSidePanel();
                    }), null);
        }

        // ---- the siege screen ---------------------------------------------------------

        private DikozemieSiege CurrentSiege =>
            string.IsNullOrEmpty(_siegeId) ? null : DikozemieClans.Sieges.FirstOrDefault(s => s.Id == _siegeId);

        private void OpenSiege(string id)
        {
            DikozemieSiege siege = DikozemieClans.Sieges.FirstOrDefault(s => s.Id == id);
            if (siege == null)
            {
                _siegePending = id ?? string.Empty;
                _siegePendingUtc = DateTime.UtcNow;
                DikozemieWorld.PulseSoon();
                return;
            }
            _siegePending = string.Empty;
            if (_clanOpen)
            {
                CloseSheet();
                CloseChat();
                _clanOpen = false;
                DikozemieClans.IsScreenOpen = false;
            }
            if (_townOpen)
            {
                CloseSheet();
                CloseChat();
                HideTownFigures();
                _townOpen = false;
                DikozemieTown.IsTownScreenOpen = false;
            }
            _worldPick = null;
            _passagePick = false;
            _siegeOpen = true;
            _siegeId = id;
            _siegeShownState = siege.State;
            _siegeNote = string.Empty;
            DikozemieLobbyMapNode node = DikozemieLobbyMapCatalog.Find(siege.Node);
            string culture = DikozemieTown.NormalizeCulture(node?.Culture);
            if (string.IsNullOrEmpty(culture) || culture == "neutral_culture")
            {
                culture = "empire";
            }
            DikozemieTownPictureTextureProvider.Requested = culture + "-" +
                (node != null && DikozemieTown.IsCastle(node) ? "castle" : node?.HasPort == true ? "port" : "town");
            DikozemieLobbyShellIntegration.TraceButtonAction("siege screen open id=" + id + " node=" + siege.Node +
                " state=" + siege.State + " side=" + siege.Side);
            RefreshSidePanel();
            LayoutContent();
        }

        // Somebody stopped the hero on the road: the clan and siege screens give way.
        private void CloseLayersForRoad()
        {
            if (_clanOpen || _siegeOpen)
            {
                CloseSheet();
                CloseChat();
            }
            _clanOpen = false;
            _siegeOpen = false;
            _siegeId = string.Empty;
            DikozemieClans.IsScreenOpen = false;
        }

        private void CloseSiege()
        {
            string node = CurrentSiege?.Node ?? string.Empty;
            _siegeOpen = false;
            _siegeId = string.Empty;
            // Standing at those walls: back to the place's own screen.
            DikozemieLobbyMapNode here = NodeHere();
            if (here != null && here.Id == node && !_travelling)
            {
                OpenTown(here);
                return;
            }
            RefreshSidePanel();
            LayoutContent();
        }

        // Every frame on the map: a storm of the hero's clan pulls him in.
        private void TickSieges()
        {
            if (_siegePending.Length > 0)
            {
                if (DikozemieClans.Sieges.Any(s => s.Id == _siegePending))
                {
                    OpenSiege(_siegePending);
                }
                else if ((DateTime.UtcNow - _siegePendingUtc).TotalSeconds > 10)
                {
                    _siegePending = string.Empty;
                }
            }
            foreach (DikozemieSiege siege in DikozemieClans.Sieges)
            {
                if (!siege.Mine || !siege.Active || _siegesAnnounced.Contains(siege.Id + siege.State))
                {
                    continue;
                }
                _siegesAnnounced.Add(siege.Id + siege.State);
                bool called = siege.State == "battle" && siege.Fight != null && siege.Fight.InBattle;
                if (called && !_roadOpen && TaleWorlds.MountAndBlade.Mission.Current == null)
                {
                    OpenSiege(siege.Id);
                }
            }
            // The end of a siege the hero fought in: its outcome on screen once.
            foreach (DikozemieSiege siege in DikozemieClans.Sieges)
            {
                if (!siege.Mine || siege.Active || !siege.JoinedMe || _siegesAnnounced.Contains(siege.Id + siege.State))
                {
                    continue;
                }
                _siegesAnnounced.Add(siege.Id + siege.State);
                if (!_roadOpen && TaleWorlds.MountAndBlade.Mission.Current == null && !_siegeOpen)
                {
                    OpenSiege(siege.Id);
                }
            }
            DikozemieSiege current = CurrentSiege;
            if (_siegeOpen && current == null && !string.IsNullOrEmpty(_siegeId))
            {
                // Archived on the server: back to the map.
                CloseSiege();
                return;
            }
            if (current == null)
            {
                return;
            }
            if (_siegeOpen && current.State != _siegeShownState)
            {
                _siegeShownState = current.State;
                RefreshSidePanel();
            }
            if (current.State == "battle" && current.Fight != null && current.Fight.InBattle &&
                _siegeStartedFight != current.Fight.Guid && TaleWorlds.MountAndBlade.Mission.Current == null &&
                !DikozemieWorldBattle.IsPending)
            {
                StartSiegeBattle(current);
            }
        }

        private void StartSiegeBattle(DikozemieSiege siege)
        {
            DikozemieSiegeFight fight = siege?.Fight;
            if (fight == null)
            {
                return;
            }
            _siegeStartedFight = fight.Guid;
            bool leader = fight.SideLeader == DikozemieWorld.MyId;
            var plan = new DikozemieWorldBattlePlan
            {
                Kind = "siege",
                Id = fight.Guid,
                Mode = "team",
                GroupSize = Math.Max(1, fight.Humans),
                SideLeader = fight.SideLeader,
                TeamSize = Math.Max(2, Math.Max(fight.SideSize, fight.FoeSize)),
                PreferredPort = fight.Port,
                Title = "ШТУРМ · " + siege.Name.ToUpperInvariant(),
                Attacking = siege.Attacking,
                SideSize = fight.SideSize,
                SideRating = fight.SideRating,
                FoeSize = fight.FoeSize,
                FoeRating = fight.FoeRating,
                // The leader of the storm picks the server; the others follow.
                SearchNotBeforeUtc = leader && siege.Attacking ? DateTime.UtcNow : DateTime.UtcNow.AddSeconds(20)
            };
            if (!DikozemieWorldBattle.Start(plan, out string error))
            {
                _siegeStartedFight = string.Empty;
                _siegeNote = error;
            }
            RefreshSidePanel();
        }

        private void SiegeAct(string path, string done)
        {
            DikozemieSiege siege = CurrentSiege;
            if (siege == null || _worldBusy)
            {
                return;
            }
            _worldBusy = true;
            DikozemieClans.Act(path, "{\"siege\":" + DikozemieEconomy.Json(siege.Id) + "}", (state, error) =>
            {
                _worldBusy = false;
                _siegeNote = error ?? done ?? string.Empty;
                RefreshSidePanel();
            });
        }

        private void RefreshSiegePanel()
        {
            SetMapPanelVisible(false);
            HideTownOptions();
            HideWorldButtons();
            _optionCount = 0;
            DikozemieSiege siege = CurrentSiege;
            if (siege == null)
            {
                _siegeOpen = false;
                RefreshSidePanel();
                return;
            }
            string defender = siege.Npc ? "клан " + siege.Defender.Name + (string.IsNullOrEmpty(siege.Kingdom) ? string.Empty : " (" + siege.Kingdom + ")")
                : "клан «" + siege.Defender.Name + "»";
            string kind = siege.Kind == "town" ? "ГОРОД" : "ЗАМОК";
            _townName.Text = siege.Name.ToUpperInvariant();
            _townKind.Text = (siege.State == "battle" ? "ШТУРМ" : siege.Active ? "ОСАДА" : "ОСАДА ОКОНЧЕНА") + "  ·  " + kind;
            _townOwner.Text = "Клан «" + siege.Attacker.Name + "» против: " + defender + ".";
            var lore = new List<string>();
            lore.Add("В лагере: " + (siege.Attackers.Count == 0 ? "никого" : string.Join(", ", siege.Attackers.Select(p => p.Name + (p.Fresh ? string.Empty : " (отошёл)")))) +
                (siege.Attacking && siege.Warband > 0 ? ", с ними дружина: " + siege.Warband + "." : "."));
            lore.Add("На стенах: гарнизон около " + siege.Garrison + (siege.Defenders.Count > 0 ? ", люди клана: " +
                string.Join(", ", siege.Defenders.Select(p => p.Name)) : string.Empty) + ".");
            if (siege.Log.Count > 0)
            {
                lore.Add(string.Join(" ", siege.Log.Skip(Math.Max(0, siege.Log.Count - 3))));
            }
            _townLore.Text = string.Join(" ", lore);

            _title.Text = siege.State == "battle" ? "ШТУРМ" : siege.Active ? "ОСАДА"
                : siege.Side == "defense"
                    ? siege.State == "won" ? "КРЕПОСТЬ ПАЛА" : siege.State == "lost" ? "УСТОЯЛА" : "ОСАДА СНЯТА"
                    : siege.State == "won" ? "ВЗЯТА" : siege.State == "lost" ? "ОТБИТО" : "ОСАДА СНЯТА";
            var text = new List<string>();
            switch (siege.State)
            {
                case "camp":
                {
                    int left = siege.AssaultLeft;
                    if (siege.Attacking)
                    {
                        text.Add(siege.JoinedMe
                            ? "Вы в осадном лагере. Штурм через " + DikozemieClans.Minutes(left) + ": уходить далеко нельзя, кто отошёл — не в бою."
                            : "Ваш клан осаждает " + siege.Name + ". Встаньте в лагерь у стен, чтобы идти на штурм.");
                        if (!siege.JoinedMe)
                        {
                            AddOption("В ОСАДНЫЙ ЛАГЕРЬ", "у стен", () => SiegeAct("siege/join", "Вы в лагере."));
                        }
                        if (siege.Leader == DikozemieWorld.MyId && siege.Npc)
                        {
                            AddOption("НА ШТУРМ", "не ждать", () => SiegeAct("siege/assault", "На штурм!"));
                        }
                        if (siege.Leader == DikozemieWorld.MyId)
                        {
                            AddOption("СНЯТЬ ОСАДУ", "лестницы пропадут", () => InquiryConfirm("СНЯТЬ ОСАДУ",
                                "Свернуть лагерь? Монеты за лестницы не вернуть.", () => SiegeAct("siege/leave", "Осада снята.")));
                        }
                        else if (siege.JoinedMe)
                        {
                            AddOption("ПОКИНУТЬ ЛАГЕРЬ", string.Empty, () => SiegeAct("siege/leave", "Вы ушли из лагеря."));
                        }
                    }
                    else if (siege.Side == "defense")
                    {
                        text.Add("Клан «" + siege.Attacker.Name + "» стоит лагерем под стенами вашей крепости. Штурм через " +
                            DikozemieClans.Minutes(left) + ". Защитники встают на стены изнутри или у ворот.");
                        if (!siege.JoinedMe)
                        {
                            AddOption("НА СТЕНЫ", "защищать", () => SiegeAct("siege/join", "Вы на стенах."));
                        }
                        else
                        {
                            AddOption("ПОКИНУТЬ СТЕНЫ", string.Empty, () => SiegeAct("siege/leave", "Вы ушли со стен."));
                        }
                    }
                    else
                    {
                        text.Add("Чужая война: клан «" + siege.Attacker.Name + "» осаждает " + siege.Name + ".");
                    }
                    break;
                }
                case "battle":
                {
                    DikozemieSiegeFight fight = siege.Fight;
                    if (fight != null && fight.InBattle)
                    {
                        text.Add("Штурм ворот: " + fight.SideSize + " против " + fight.FoeSize + ". Сервер боя сводит всех, кто был в лагере и на стенах; " +
                            "остальные места занимают наёмники и гарнизон.");
                        if (DikozemieWorldBattle.IsPending)
                        {
                            text.Add(DikozemiePvpSearchController.IsSearching ? DikozemiePvpSearchController.Status : "Идём к воротам…");
                        }
                        else
                        {
                            AddOption("В БОЙ", "к воротам", () => StartSiegeBattle(siege));
                        }
                    }
                    else
                    {
                        text.Add("Идёт штурм. Вас не было ни в лагере, ни на стенах: исход узнаете здесь.");
                    }
                    break;
                }
                default:
                    text.Add(string.IsNullOrEmpty(siege.Outcome) ? "Осада окончена." : siege.Outcome);
                    if (siege.State == "won" && siege.Attacking)
                    {
                        text.Add("Поставьте в крепости гарнизон: «ЗАМОК → ВАШ ЗАМОК → ГАРНИЗОН». Без людей она уйдёт прежнему владельцу.");
                        DikozemieLobbyMapNode walls = DikozemieLobbyMapCatalog.Find(siege.Node);
                        if (walls != null && NodeHere()?.Id == walls.Id)
                        {
                            AddOption(siege.Kind == "town" ? "ВОЙТИ В ГОРОД" : "ВОЙТИ В ЗАМОК", "теперь ваш", () =>
                            {
                                _siegeOpen = false;
                                _siegeId = string.Empty;
                                OpenTown(walls);
                                ShowSection(DikozemieTown.IsCastle(walls) ? TownSection.Castle : TownSection.Main);
                            });
                        }
                    }
                    break;
            }
            if (!string.IsNullOrEmpty(_siegeNote))
            {
                text.Add(string.Empty);
                text.Add(_siegeNote);
            }
            AddOption("К КАРТЕ", string.Empty, CloseSiege);
            _description.Text = string.Join("\n", text);
            StackOptions();
        }

        // Banners over the pictures of the town and siege screens.
        private void LayoutPlaceBanners(float width, float height)
        {
            if (_placeBanner == null)
            {
                return;
            }
            string left = string.Empty;
            string right = string.Empty;
            bool picture = !TradeSheetOpen && !_chatOpen;
            if (_siegeOpen)
            {
                DikozemieSiege siege = CurrentSiege;
                left = siege?.Attacker.Banner ?? string.Empty;
                right = siege == null ? string.Empty
                    : siege.Npc ? DikozemieTownCatalog.Get(siege.Node).Banner ?? string.Empty : siege.Defender.Banner;
            }
            else if (_townOpen && _townNode != null && _townSection != TownSection.People)
            {
                right = DikozemieClans.BannerOf(_townNode);
            }
            bool showLeft = picture && _siegeOpen && left.Length > 0;
            if (showLeft)
            {
                if (_clanBanner.BannerCodeText != left)
                {
                    _clanBanner.BannerCodeText = left;
                }
                _clanBanner.SuggestedWidth = 220f;
                _clanBanner.SuggestedHeight = 330f;
                _clanBanner.PositionXOffset = 30f;
                _clanBanner.PositionYOffset = 0f;
                _clanBanner.IsVisible = true;
            }
            else if (!_clanOpen)
            {
                _clanBanner.IsVisible = false;
            }
            bool showFoe = picture && _siegeOpen && right.Length > 0;
            _foeBanner.IsVisible = showFoe;
            if (showFoe)
            {
                if (_foeBanner.BannerCodeText != right)
                {
                    _foeBanner.BannerCodeText = right;
                }
                _foeBanner.SuggestedWidth = 220f;
                _foeBanner.SuggestedHeight = 330f;
                _foeBanner.PositionXOffset = width - 250f;
                _foeBanner.PositionYOffset = 0f;
            }
            bool showRight = picture && _townOpen && right.Length > 0;
            if (showRight && _placeBanner.ImageId != right)
            {
                _placeBanner.ImageId = right;
            }
            float size = _siegeOpen ? 150f : 104f;
            _placeBanner.SuggestedWidth = size;
            _placeBanner.SuggestedHeight = size;
            _placeBanner.PositionXOffset = width - size - 36f;
            _placeBanner.PositionYOffset = 28f;
            _placeBanner.IsVisible = showRight;
            _placeBannerFrame.SuggestedWidth = size + 12f;
            _placeBannerFrame.SuggestedHeight = size + 12f;
            _placeBannerFrame.PositionXOffset = width - size - 42f;
            _placeBannerFrame.PositionYOffset = 22f;
            _placeBannerFrame.IsVisible = showRight;
        }

        // ---- a clan's own castle: garrison, tolls, patrols --------------------------------

        private DikozemieClanHolding MyHolding(string node) =>
            DikozemieClans.Clan?.Holdings.FirstOrDefault(h => h.Node == node);

        private void OpenGarrison(string node)
        {
            _landNode = node;
            OpenSheet(TradeSheet.Garrison);
        }

        private void RenderGarrison()
        {
            DikozemieClanFull clan = DikozemieClans.Clan;
            DikozemieClanHolding held = MyHolding(_landNode);
            _sheetTitle.Text = "ГАРНИЗОН · " + (held?.Name ?? DikozemieWorld.PlaceName(_landNode)).ToUpperInvariant();
            _sheetStatus.Text = "КАЗНА КЛАНА: " + FormatCoins(clan?.Coins ?? 0) + "   ·   ЖАЛОВАНЬЕ: " +
                FormatCoins((int)Math.Round(clan?.WagesDay ?? 0)) + " В ДЕНЬ";
            if (clan == null || held == null)
            {
                SetColumns(1f);
                SetDetail("Это не ваша крепость.");
                return;
            }
            SetColumns(2.4f, 1.3f, 1.3f, 1.6f);
            SetAlign(TextHorizontalAlignment.Left, TextHorizontalAlignment.Right, TextHorizontalAlignment.Right,
                TextHorizontalAlignment.Right);
            SetHeads("КТО", "НА СТЕНАХ", "В ДРУЖИНЕ", "ЖАЛОВАНЬЕ");
            bool can = DikozemieClans.Can("garrison") && !_clanBusy;
            int row = 0;
            foreach (DikozemieTroopType type in DikozemieClans.TroopTypes)
            {
                string kind = type.Type;
                held.Garrison.TryGetValue(kind, out int there);
                clan.Troops.TryGetValue(kind, out int band);
                string node = _landNode;
                SetRow(row++, new[] { Capital(type.Plural), there.ToString(CultureInfo.InvariantCulture), band.ToString(CultureInfo.InvariantCulture),
                        type.Wage + " мон./день" },
                    new[] { default(Color), there > 0 ? SheetGold : SheetDim, band > 0 ? SheetInk : SheetDim, SheetDim },
                    false, null,
                    ("НА СТЕНЫ +1", can && band > 0, () => ClanAct("garrison", "{\"node\":" + DikozemieEconomy.Json(node) +
                        ",\"type\":\"" + kind + "\",\"count\":1}", type.Name + " встал на стены.")),
                    ("В ДРУЖИНУ", can && there > 0, () => ClanAct("garrison", "{\"node\":" + DikozemieEconomy.Json(node) +
                        ",\"type\":\"" + kind + "\",\"count\":-1}", type.Name + " ушёл в дружину.")));
            }
            SetDetail("На стенах: " + DikozemieClans.TroopsWords(held.Garrison) + ". Гарнизон защищает крепость при штурме: у ворот " +
                "встаёт до десяти человек, сильнейшие первыми. Пустая крепость через " + (int)DikozemieClans.Rules.AbandonMinutes +
                " мин. уходит к прежнему владельцу. Нанять людей — в таверне любого города." +
                (string.IsNullOrEmpty(_sheetNote) ? string.Empty : "\n\n" + _sheetNote));
        }

        private void RenderLandRates()
        {
            DikozemieClanFull clan = DikozemieClans.Clan;
            DikozemieClanHolding held = MyHolding(_landNode);
            _sheetTitle.Text = "ПОШЛИНЫ · " + (held?.Name ?? DikozemieWorld.PlaceName(_landNode)).ToUpperInvariant();
            _sheetStatus.Text = "КАЗНА КЛАНА: " + FormatCoins(clan?.Coins ?? 0);
            if (clan == null || held == null)
            {
                SetColumns(1f);
                SetDetail("Это не ваша земля.");
                return;
            }
            DikozemieClanRules rules = DikozemieClans.Rules;
            SetColumns(2.6f, 1.2f, 5f);
            SetAlign(TextHorizontalAlignment.Left, TextHorizontalAlignment.Right, TextHorizontalAlignment.Left);
            SetHeads("ПОШЛИНА", "СЕЙЧАС", "ЧТО ДАЁТ");
            bool can = DikozemieClans.Can("rates") && !_clanBusy;
            string node = _landNode;
            int row = 0;
            void Rate(string key, string title, int value, int most, string unit, string about)
            {
                SetRow(row++, new[] { title, value + unit, about }, new[] { default(Color), SheetGold, SheetDim }, false, null,
                    ("МЕНЬШЕ", can && value > 0, () => ClanAct("rates", "{\"node\":" + DikozemieEconomy.Json(node) + ",\"" + key + "\":" +
                        (value - 1) + "}", title + ": " + (value - 1) + unit + ".")),
                    ("БОЛЬШЕ", can && value < most, () => ClanAct("rates", "{\"node\":" + DikozemieEconomy.Json(node) + ",\"" + key + "\":" +
                        (value + 1) + "}", title + ": " + (value + 1) + unit + ".")));
            }
            if (held.Kind == "town")
            {
                Rate("market", "Рыночная пошлина", held.Rates.Market, rules.MarketMax, "%",
                    "С каждой продажи на торговом дворе. Дешевле — торговцы везут сюда, дороже — объезжают.");
                Rate("workshop", "Налог мастерских", held.Rates.Workshop, rules.WorkshopMax, "%",
                    "С прибыли мастерских города: горожан и игроков.");
            }
            Rate("bridge", "Мостовщина", held.Rates.Bridge, rules.BridgeMax, " мон.",
                "С пешего на мостах этой земли; с конного вдвое. Ноль — мытарей нет.");
            int patrolCost = rules.PatrolCost * (held.Kind == "town" ? 2 : 1);
            SetRow(row++, new[] { "Дозоры на дорогах", held.Patrol ? "есть" : "нет",
                    "Разбойников на дорогах этой земли втрое меньше. " + patrolCost + " мон. в день из казны." },
                new[] { default(Color), held.Patrol ? SheetGood : SheetDim, SheetDim }, false, null,
                (held.Patrol ? "СНЯТЬ" : "ВЫСЛАТЬ", can, () => ClanAct("rates", "{\"node\":" + DikozemieEconomy.Json(node) +
                    ",\"patrol\":" + (held.Patrol ? "false" : "true") + "}", held.Patrol ? "Дозоры сняты." : "Дозоры высланы.")));
            SetDetail("Пошлины земли идут в казну клана сами. Торговцы видят их на карте и в торговом дворе и выбирают, чья земля " +
                "дешевле и безопаснее. Люди вашего клана по своей земле ходят без пошлин." +
                (string.IsNullOrEmpty(_sheetNote) ? string.Empty : "\n\n" + _sheetNote));
        }

        // ---- the tavern: mercenaries ----------------------------------------------------

        private void OpenMercs(DikozemieLobbyMapNode town)
        {
            _landNode = town?.Id ?? string.Empty;
            _mercs = null;
            OpenSheet(TradeSheet.Mercs);
            LoadMercs();
        }

        private void LoadMercs()
        {
            string town = _landNode;
            DikozemieClans.Mercs(town, (mercs, error) =>
            {
                if (_landNode != town)
                {
                    return;
                }
                _mercs = mercs;
                if (mercs == null)
                {
                    _sheetNote = error ?? string.Empty;
                }
                _sheetRevision = -1;
            });
        }

        private void RenderMercs()
        {
            DikozemieClanFull clan = DikozemieClans.Clan;
            _sheetTitle.Text = "НАЁМНИКИ · " + DikozemieWorld.PlaceName(_landNode).ToUpperInvariant();
            _sheetStatus.Text = clan == null
                ? "ВЫ НЕ В КЛАНЕ"
                : "КАЗНА КЛАНА: " + FormatCoins(clan.Coins) + "   ·   ДРУЖИНА: " + DikozemieClans.Total(clan.Troops) +
                  " ИЗ " + DikozemieClans.Rules.WarbandMax;
            SetColumns(2.2f, 1f, 1.2f, 1.2f, 1.2f, 3.4f);
            SetAlign(TextHorizontalAlignment.Left, TextHorizontalAlignment.Right, TextHorizontalAlignment.Right,
                TextHorizontalAlignment.Right, TextHorizontalAlignment.Right, TextHorizontalAlignment.Left);
            SetHeads("КТО", "ЕСТЬ", "В ДЕНЬ", "ЗАДАТОК", "СЛАВА", "КАКИЕ ОНИ");
            if (_mercs == null)
            {
                SetRow(0, new[] { "Расспрашиваем трактирщика…" }, new[] { SheetDim }, false, null);
                SetDetail(_sheetNote);
                return;
            }
            bool can = clan != null && DikozemieClans.Can("hire") && !_clanBusy;
            string town = _landNode;
            int row = 0;
            foreach (DikozemieMercRow r in _mercs.Rows)
            {
                string kind = r.Type;
                bool allowed = can && r.Allowed && r.Left > 0;
                Color fame = r.Allowed ? SheetDim : SheetBad;
                Action hire(int count) => () =>
                {
                    ClanAct("hire", "{\"type\":\"" + kind + "\",\"count\":" + count + ",\"at\":" + DikozemieEconomy.Json(town) + "}",
                        "Наняты: " + r.Plural + " " + count + ". Они в дружине клана.", _ => LoadMercs());
                };
                SetRow(row++, new[] { Capital(r.Plural), r.Left.ToString(CultureInfo.InvariantCulture), r.Wage + " мон.", r.Advance + " мон.",
                        r.Fame > 0 ? FormatCoins(r.Fame) : "—", r.About },
                    new[] { default(Color), r.Left > 0 ? SheetInk : SheetDim, default(Color), default(Color), fame, SheetDim },
                    false, null,
                    ("НАНЯТЬ 1", allowed, hire(1)),
                    (r.Left >= 2 ? "НАНЯТЬ " + Math.Min(5, r.Left) : string.Empty, allowed && r.Left >= 2,
                        hire(Math.Min(5, Math.Max(2, r.Left)))));
            }
            string why = clan == null
                ? "Наёмники служат кланам: основайте клан или вступите в него."
                : !DikozemieClans.Can("hire")
                    ? "Нанимают глава и воеводы клана."
                    : "Ваша слава — " + FormatCoins(_mercs.Rating) + ". Задаток — из казны клана; потом жалованье каждый день, " +
                      "пока казна не опустеет. Новые люди приходят в таверну каждый день.";
            SetDetail(why + (string.IsNullOrEmpty(_sheetNote) ? string.Empty : "\n\n" + _sheetNote));
        }
    }
}
