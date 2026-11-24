using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets;
using TaleWorlds.ObjectSystem;

namespace Dikozemie.Online
{
    public sealed class DikozemieMapIconPeerTextureProvider : DikozemieMapIconProviderBase
    {
        public DikozemieMapIconPeerTextureProvider() : base("map-icon-peer.png") { }
    }

    public sealed class DikozemieMapIconPeerPartyTextureProvider : DikozemieMapIconProviderBase
    {
        public DikozemieMapIconPeerPartyTextureProvider() : base("map-icon-peer-party.png") { }
    }

    public sealed class DikozemieMapIconPeerOutlawTextureProvider : DikozemieMapIconProviderBase
    {
        public DikozemieMapIconPeerOutlawTextureProvider() : base("map-icon-peer-outlaw.png") { }
    }

    public sealed class DikozemieMapIconPeerBoatTextureProvider : DikozemieMapIconProviderBase
    {
        public DikozemieMapIconPeerBoatTextureProvider() : base("map-icon-boat-peer.png") { }
    }

    public sealed class DikozemieMapIconPeerBoatLeftTextureProvider : DikozemieMapIconProviderBase
    {
        public DikozemieMapIconPeerBoatLeftTextureProvider() : base("map-icon-boat-peer-left.png") { }
    }

    public sealed class DikozemieMapIconCaravanTextureProvider : DikozemieMapIconProviderBase
    {
        public DikozemieMapIconCaravanTextureProvider() : base("map-icon-caravan.png") { }
    }

    public sealed class DikozemieMapIconCaravanLeftTextureProvider : DikozemieMapIconProviderBase
    {
        public DikozemieMapIconCaravanLeftTextureProvider() : base("map-icon-caravan-left.png") { }
    }

    public sealed class DikozemieMapIconCogTextureProvider : DikozemieMapIconProviderBase
    {
        public DikozemieMapIconCogTextureProvider() : base("map-icon-cog.png") { }
    }

    public sealed class DikozemieMapIconCogLeftTextureProvider : DikozemieMapIconProviderBase
    {
        public DikozemieMapIconCogLeftTextureProvider() : base("map-icon-cog-left.png") { }
    }

    // Stage 5 on the map: the other heroes (pawns of the same board game),
    // caravans and ships on their ways, how many heroes stand in each town;
    // the card of whoever was clicked; meetings on the road and raids drawn
    // on the encounter screen of stage 2; a party walking behind its leader.
    public sealed partial class DikozemieLobbyMapWidget
    {
        private const int PeerMarkerCount = 24;
        private const int ConvoyMarkerCount = 24;
        private const int TownBadgeCount = 20;
        private const float PeerFigureSize = 46f;
        private const float PeerBoatSize = 62f;
        private const float ConvoyFigureSize = 44f;
        private const float ShipFigureSize = 64f;
        private const float PickRadius = 22f;
        // Atlas pixels within which a hero may stop another or a caravan.
        private const float AttackReach = 100f;
        private const float InviteReach = 150f;

        private sealed class WorldMarker
        {
            internal Widget Halo;
            internal TextureWidget Figure;
            internal TextWidget Label;
            internal string Provider = string.Empty;
            internal string Id = string.Empty;
            internal float X;
            internal float Y;
            internal bool Shown;
        }

        private sealed class TownBadge
        {
            internal Widget Disc;
            internal TextWidget Count;
        }

        private readonly List<WorldMarker> _peerMarkers = new List<WorldMarker>();
        private readonly List<WorldMarker> _convoyMarkers = new List<WorldMarker>();
        private readonly List<TownBadge> _townBadges = new List<TownBadge>();
        private ButtonWidget _worldAttack;
        private ButtonWidget _worldApproach;
        private ButtonWidget _worldInvite;
        private ButtonWidget _worldParty;
        private ButtonWidget _worldClan;
        // The watch: a complaint to the observer, or the observer's measures.
        private ButtonWidget _worldReport;
        private string _worldPick;
        private int _worldRevision = -1;
        private float _worldPanelRefresh;
        private string _worldNoteValue = string.Empty;
        private DateTime _worldNoteAt;
        private bool _worldBusy;

        // A word under the card (an answer of the server); it fades away.
        private string _worldNote
        {
            get => _worldNoteValue;
            set
            {
                _worldNoteValue = value ?? string.Empty;
                _worldNoteAt = DateTime.UtcNow;
            }
        }

        // The meeting or raid on the encounter screen.
        private bool _meetingOpen;
        private string _meetingId = string.Empty;
        private string _meetingShownState = string.Empty;
        private string _meetingDismissed = string.Empty;
        private string _meetingStartedFight = string.Empty;
        private DikozemieWorldNote _pendingCaptive;
        private bool _raidOpen;
        private DikozemieWorldConvoy _raidConvoy;
        private DikozemieWorldRaid _raid;
        private string _raidStartedFight = string.Empty;
        private readonly HashSet<string> _partyBattlesSeen = new HashSet<string>(StringComparer.Ordinal);
        private static string _raidFightId = string.Empty;

        private static readonly Color PeerInk = new Color(0.10f, 0.12f, 0.16f, 1f);
        private static readonly Color PartyInk = new Color(0.08f, 0.26f, 0.08f, 1f);
        private static readonly Color OutlawInk = new Color(0.45f, 0.05f, 0.03f, 1f);
        private static readonly Color ConvoyInk = new Color(0.30f, 0.17f, 0.05f, 1f);

        // ---- building --------------------------------------------------------

        private void BuildWorldLayer()
        {
            for (int index = 0; index < ConvoyMarkerCount; index++)
            {
                _convoyMarkers.Add(CreateWorldMarker("WorldConvoy" + index, new Color(0.16f, 0.09f, 0.03f, 0.55f)));
            }
            for (int index = 0; index < PeerMarkerCount; index++)
            {
                _peerMarkers.Add(CreateWorldMarker("WorldPeer" + index, new Color(0.05f, 0.06f, 0.09f, 0.55f)));
            }
            for (int index = 0; index < TownBadgeCount; index++)
            {
                var badge = new TownBadge
                {
                    Disc = new Widget(Context)
                    {
                        Id = "WorldTownBadge" + index,
                        WidthSizePolicy = SizePolicy.Fixed,
                        HeightSizePolicy = SizePolicy.Fixed,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        SuggestedWidth = 24f,
                        SuggestedHeight = 24f,
                        Sprite = Context.SpriteData.GetSprite("BlankWhiteCircle"),
                        Color = new Color(0.12f, 0.16f, 0.24f, 0.95f),
                        DoNotAcceptEvents = true,
                        IsVisible = false
                    }
                };
                AddChild(badge.Disc);
                badge.Count = CreateText("WorldTownBadgeText" + index, string.Empty, 15, new Color(0.92f, 0.95f, 1f, 1f));
                badge.Count.WidthSizePolicy = SizePolicy.StretchToParent;
                badge.Count.HeightSizePolicy = SizePolicy.StretchToParent;
                badge.Disc.AddChild(badge.Count);
                _townBadges.Add(badge);
            }
        }

        private WorldMarker CreateWorldMarker(string id, Color halo)
        {
            var marker = new WorldMarker
            {
                Halo = new Widget(Context)
                {
                    Id = id + "Halo",
                    WidthSizePolicy = SizePolicy.Fixed,
                    HeightSizePolicy = SizePolicy.Fixed,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Sprite = Context.SpriteData.GetSprite("BlankWhiteCircle"),
                    Color = halo,
                    DoNotAcceptEvents = true,
                    IsVisible = false
                },
                Figure = new TextureWidget(Context)
                {
                    Id = id + "Figure",
                    WidthSizePolicy = SizePolicy.Fixed,
                    HeightSizePolicy = SizePolicy.Fixed,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    DoNotAcceptEvents = true,
                    IsVisible = false,
                    // never the engine's ResourceTextureProvider (see DikozemieLobbyMapView tiles)
                    TextureProviderName = string.Empty
                }
            };
            AddChild(marker.Halo);
            AddChild(marker.Figure);
            marker.Label = CreateText(id + "Label", string.Empty, 15, PeerInk);
            marker.Label.WidthSizePolicy = SizePolicy.Fixed;
            marker.Label.HeightSizePolicy = SizePolicy.Fixed;
            marker.Label.HorizontalAlignment = HorizontalAlignment.Left;
            marker.Label.VerticalAlignment = VerticalAlignment.Top;
            marker.Label.SuggestedWidth = 260f;
            marker.Label.SuggestedHeight = 24f;
            marker.Label.Brush.DefaultStyle.TextGlowColor = new Color(0.98f, 0.94f, 0.82f, 1f);
            marker.Label.Brush.DefaultStyle.TextGlowRadius = 0.2f;
            marker.Label.Brush.DefaultStyle.TextBlur = 0.6f;
            marker.Label.IsVisible = false;
            AddChild(marker.Label);
            return marker;
        }

        private void BuildWorldButtons()
        {
            _worldAttack = CreatePanelButton("WorldAttack", "НАПАСТЬ", 128f);
            _worldAttack.ClickEventHandlers.Add(_ => OnWorldAttack());
            _worldApproach = CreatePanelButton("WorldApproach", "ПОДОЙТИ", 196f);
            _worldApproach.ClickEventHandlers.Add(_ => OnWorldApproach());
            _worldInvite = CreatePanelButton("WorldInvite", "ПОЗВАТЬ В ОТРЯД", 264f);
            _worldInvite.ClickEventHandlers.Add(_ => OnWorldInvite());
            _worldParty = CreatePanelButton("WorldParty", "ПОКИНУТЬ ОТРЯД", 332f);
            _worldParty.ClickEventHandlers.Add(_ => OnLeaveParty());
            _worldClan = CreatePanelButton("WorldClan", "ПОЗВАТЬ В КЛАН", 400f);
            _worldClan.ClickEventHandlers.Add(_ => OnWorldClanInvite());
            _worldReport = CreatePanelButton("WorldReport", "ПОЖАЛОВАТЬСЯ", 468f);
            _worldReport.ClickEventHandlers.Add(_ => OnWorldReport());
            BuildLandLayer();
            HideWorldButtons();
        }

        private void HideWorldButtons()
        {
            foreach (ButtonWidget button in new[] { _worldAttack, _worldApproach, _worldInvite, _worldParty, _worldClan, _worldReport, _siegeButton })
            {
                if (button != null)
                {
                    button.IsVisible = false;
                }
            }
        }

        private static void SetButtonText(ButtonWidget button, string text, bool enabled = true)
        {
            if (button?.FindChild(button.Id + "Text", true) is TextWidget label)
            {
                label.Text = text;
                label.Brush.FontColor = enabled
                    ? new Color(0.98f, 0.83f, 0.42f, 1f)
                    : new Color(0.50f, 0.42f, 0.30f, 1f);
            }
            if (button?.FindChild(button.Id + "Frame", true) is Widget frame)
            {
                frame.Color = enabled ? new Color(0.72f, 0.47f, 0.17f, 1f) : new Color(0.40f, 0.30f, 0.18f, 0.6f);
            }
        }

        // Why the card's main action cannot be done now (shown when pressed).
        private string _worldBlocked = string.Empty;

        // Settlement markers are made late, on top of everything: the world's
        // pawns are raised above them, under the hero and the screens.
        private void RaiseWorldLayer()
        {
            foreach (WorldMarker marker in _convoyMarkers.Concat(_peerMarkers))
            {
                marker.Halo.SetSiblingIndex(ChildCount - 1);
                marker.Figure.SetSiblingIndex(ChildCount - 1);
                marker.Label.SetSiblingIndex(ChildCount - 1);
            }
            foreach (TownBadge badge in _townBadges)
            {
                badge.Disc.SetSiblingIndex(ChildCount - 1);
            }
        }

        // ---- every frame ------------------------------------------------------

        private void TickWorld(float dt)
        {
            if (_travel == null)
            {
                return;
            }
            bool onScreen = IsVisible && IsRecursivelyVisible();
            if (onScreen)
            {
                ReportToWorld();
            }
            if (!onScreen)
            {
                return;
            }
            if (DikozemieWorld.TryTakeCaptive(out DikozemieWorldNote captive))
            {
                // With the meeting's card on screen the cart waits for its
                // button; otherwise straight to the arena.
                if (DikozemieWorld.Meeting != null && DikozemieWorld.Meeting.Id != _meetingDismissed)
                {
                    _pendingCaptive = captive;
                    RefreshSidePanel();
                }
                else
                {
                    GoCaptiveTo(captive.Town, captive.Text);
                    return;
                }
            }
            _siegePulse += dt;
            TickFinishedWorldBattle();
            TickSieges();
            TickMeeting();
            TickPartyBattle();
            TickRaids();
            _worldPanelRefresh -= dt;
            bool changed = DikozemieWorld.Revision != _worldRevision;
            if (changed || _worldPanelRefresh <= 0f)
            {
                _worldRevision = DikozemieWorld.Revision;
                _worldPanelRefresh = 1f;
                if (_meetingOpen || _raidOpen || _worldPick != null ||
                    changed && !_roadOpen && !(LayerOpen && TradeSheetOpen) && !(_clanOpen && _clanSection == ClanSection.Banner))
                {
                    RefreshSidePanel();
                }
                if (!string.IsNullOrEmpty(_worldNote) && (DateTime.UtcNow - _worldNoteAt).TotalSeconds > 12)
                {
                    _worldNote = string.Empty;
                }
                if (_meetingOpen && changed)
                {
                    ShowMeetingFigures(DikozemieWorld.Meeting);
                }
            }
        }

        // Where the hero stands, for the others: on the map, a point two
        // seconds ahead on his way lets them draw him walking.
        private void ReportToWorld()
        {
            float headX = _travel.X;
            float headY = _travel.Y;
            float speed = 0f;
            bool moving = _travelling && _route != null && !DikozemieWorld.IsFollower;
            if (moving)
            {
                byte cell = DikozemieTravelTerrain.AtAtlas(_travel.X, _travel.Y);
                speed = DikozemieTravelRouter.SpeedOf(cell, _travel.Boat) * DikozemieRoads.SpeedFactor(_travel.Boat);
                float budget = speed * 2.5f;
                float x = _travel.X;
                float y = _travel.Y;
                for (int index = _routeIndex + 1; index < _route.Xs.Count && budget > 0f; index++)
                {
                    float d = DikozemieWorld.Distance(x, y, _route.Xs[index], _route.Ys[index]);
                    if (d >= budget)
                    {
                        x += (_route.Xs[index] - x) / Math.Max(0.01f, d) * budget;
                        y += (_route.Ys[index] - y) / Math.Max(0.01f, d) * budget;
                        budget = 0f;
                        break;
                    }
                    budget -= d;
                    x = _route.Xs[index];
                    y = _route.Ys[index];
                }
                headX = x;
                headY = y;
            }
            DikozemieWorldPlayer leader = DikozemieWorld.Leader;
            if (DikozemieWorld.IsFollower && leader != null && leader.Moving)
            {
                moving = true;
                headX = leader.Tx;
                headY = leader.Ty;
                speed = leader.Speed;
            }
            string place = _roadOpen ? "road" : _townOpen ? "town" : "map";
            string section = _townOpen ? _townSection.ToString().ToLowerInvariant() : string.Empty;
            DikozemieWorld.ReportMap(_travel.X, _travel.Y, moving, headX, headY, speed,
                _travelling ? string.Empty : _travel.NodeId, _travel.Boat, _travel.Vessel, VisionRadius(),
                place, section);
        }

        // ---- drawing -------------------------------------------------------------

        private void LayoutWorld(float mapX, float mapY, float mapWidth, float mapHeight,
            float viewportX, float viewportY, float viewportWidth, float viewportHeight)
        {
            if (_peerMarkers.Count == 0 || _travel == null)
            {
                return;
            }
            bool covered = LayerOpen || _roadOpen;
            float scaleX = mapWidth / SourceWidth;
            float scaleY = mapHeight / SourceHeight;
            float right = viewportX + viewportWidth;
            float bottom = viewportY + viewportHeight;
            float sight = VisionRadius() * 1.15f;
            string myParty = DikozemieWorld.Party?.Id ?? string.Empty;

            int used = 0;
            if (!covered)
            {
                foreach (DikozemieWorldPlayer p in DikozemieWorld.Players)
                {
                    if (used >= _peerMarkers.Count || !string.IsNullOrEmpty(p.In))
                    {
                        continue;
                    }
                    DikozemieWorld.Position(p, out float ax, out float ay);
                    bool mate = !string.IsNullOrEmpty(myParty) && p.Party == myParty;
                    if (!mate && _travelling && DikozemieWorld.Distance(ax, ay, _travel.X, _travel.Y) > sight)
                    {
                        continue;
                    }
                    float x = mapX + ax * scaleX;
                    float y = mapY + ay * scaleY;
                    if (x < viewportX - 40f || x > right + 40f || y < viewportY - 40f || y > bottom + 40f)
                    {
                        continue;
                    }
                    WorldMarker marker = _peerMarkers[used++];
                    string provider = p.Boat
                        ? (p.Tx < p.X - 1f ? "DikozemieMapIconPeerBoatLeftTextureProvider" : "DikozemieMapIconPeerBoatTextureProvider")
                        : mate ? "DikozemieMapIconPeerPartyTextureProvider"
                        : p.Outlaw ? "DikozemieMapIconPeerOutlawTextureProvider"
                        : "DikozemieMapIconPeerTextureProvider";
                    float size = p.Boat ? PeerBoatSize : PeerFigureSize;
                    bool picked = _worldPick == "p:" + p.Id;
                    bool foe = DikozemieClans.AtWarWith(p.Clan);
                    bool kin = !string.IsNullOrEmpty(p.Clan) && p.Clan == DikozemieClans.MyClanId;
                    string clanName = string.IsNullOrEmpty(p.Clan) ? string.Empty : DikozemieClans.ClanName(p.Clan);
                    PlaceMarker(marker, p.Id, provider, x, y, size, p.Boat ? 0.72f : 0.88f, picked,
                        p.Name + (clanName.Length > 0 ? " · «" + clanName + "»" : string.Empty) +
                        (p.Load > 0 ? " · обоз " + p.Load : string.Empty) + (p.Leader && mate ? " · ведёт" : string.Empty),
                        mate || kin ? PartyInk : p.Outlaw || foe ? OutlawInk : PeerInk, picked || mate || kin || foe || _zoom >= 1.6f);
                    marker.X = x;
                    marker.Y = y;
                }
            }
            for (int index = used; index < _peerMarkers.Count; index++)
            {
                HideMarker(_peerMarkers[index]);
            }

            used = 0;
            if (!covered)
            {
                foreach (DikozemieWorldConvoy c in DikozemieWorld.Convoys)
                {
                    if (used >= _convoyMarkers.Count || !DikozemieWorld.Position(c, out float ax, out float ay))
                    {
                        continue;
                    }
                    bool mine = c.Owner == DikozemieWorld.MyId;
                    if (!mine && DikozemieWorld.Distance(ax, ay, _travel.X, _travel.Y) > sight * (_travelling ? 1f : 1.6f))
                    {
                        continue;
                    }
                    float x = mapX + ax * scaleX;
                    float y = mapY + ay * scaleY;
                    if (x < viewportX - 40f || x > right + 40f || y < viewportY - 40f || y > bottom + 40f)
                    {
                        continue;
                    }
                    bool left = DikozemieWorld.HeadsLeft(c);
                    string provider = c.IsShip
                        ? (left ? "DikozemieMapIconCogLeftTextureProvider" : "DikozemieMapIconCogTextureProvider")
                        : (left ? "DikozemieMapIconCaravanTextureProvider" : "DikozemieMapIconCaravanLeftTextureProvider");
                    WorldMarker marker = _convoyMarkers[used++];
                    string label = mine ? "Ваш " + c.Name.ToLowerInvariant()
                        : c.Npc ? c.Name : c.Name + " · " + c.OwnerName;
                    PlaceMarker(marker, c.Id, provider, x, y, c.IsShip ? ShipFigureSize : ConvoyFigureSize,
                        c.IsShip ? 0.72f : 0.86f, _worldPick == "c:" + c.Id, label, mine ? PartyInk : ConvoyInk,
                        mine || _worldPick == "c:" + c.Id || _zoom >= 2.2f);
                    marker.X = x;
                    marker.Y = y;
                }
            }
            for (int index = used; index < _convoyMarkers.Count; index++)
            {
                HideMarker(_convoyMarkers[index]);
            }

            SpreadWorldLabels();

            used = 0;
            if (!covered && DikozemieWorld.View?.Towns != null)
            {
                foreach (KeyValuePair<string, int> town in DikozemieWorld.View.Towns)
                {
                    DikozemieLobbyMapNode node = DikozemieLobbyMapCatalog.Find(town.Key);
                    if (used >= _townBadges.Count || node?.Position == null || town.Value <= 0 || !IsNodeVisible(node))
                    {
                        continue;
                    }
                    float x = mapX + node.Position.X * scaleX;
                    float y = mapY + node.Position.Y * scaleY;
                    if (x < viewportX || x > right || y < viewportY || y > bottom)
                    {
                        continue;
                    }
                    TownBadge badge = _townBadges[used++];
                    badge.Count.Text = town.Value.ToString(CultureInfo.InvariantCulture);
                    badge.Disc.PositionXOffset = x - 26f;
                    badge.Disc.PositionYOffset = y - 30f;
                    badge.Disc.IsVisible = true;
                }
            }
            for (int index = used; index < _townBadges.Count; index++)
            {
                _townBadges[index].Disc.IsVisible = false;
            }
        }

        private static void PlaceMarker(WorldMarker marker, string id, string provider, float x, float y, float size,
            float foot, bool picked, string label, Color ink, bool named)
        {
            if (marker.Provider != provider)
            {
                if (!string.IsNullOrEmpty(marker.Figure.TextureProviderName))
                {
                    marker.Figure.OnClearTextureProvider();
                }
                marker.Figure.TextureProviderName = provider;
                marker.Provider = provider;
            }
            marker.Id = id;
            marker.Shown = true;
            marker.Figure.SuggestedWidth = size;
            marker.Figure.SuggestedHeight = size;
            marker.Figure.PositionXOffset = x - size * 0.5f;
            marker.Figure.PositionYOffset = y - size * foot;
            marker.Figure.IsVisible = true;
            float halo = picked ? 34f : 22f;
            marker.Halo.SuggestedWidth = halo;
            marker.Halo.SuggestedHeight = halo * 0.5f;
            marker.Halo.PositionXOffset = x - halo * 0.5f;
            marker.Halo.PositionYOffset = y - halo * 0.25f;
            marker.Halo.Color = picked ? new Color(0.98f, 0.70f, 0.16f, 0.9f) : new Color(0.05f, 0.04f, 0.03f, 0.45f);
            marker.Halo.IsVisible = true;
            marker.Label.Text = label;
            marker.Label.Brush.FontColor = ink;
            marker.Label.SuggestedWidth = Math.Max(80f, label.Length * 15f * 0.5f + 12f);
            marker.Label.PositionXOffset = x - marker.Label.SuggestedWidth * 0.5f;
            // Under the base: the hero's own «Вы здесь» stays above his pawn.
            marker.Label.PositionYOffset = y + 4f;
            marker.Label.IsVisible = named;
        }

        // A label that would cover another moves down a line, then another.
        private void SpreadWorldLabels()
        {
            var placed = new List<WorldMarker>();
            foreach (WorldMarker marker in _peerMarkers.Concat(_convoyMarkers))
            {
                if (!marker.Shown || !marker.Label.IsVisible)
                {
                    continue;
                }
                for (int tries = 0; tries < 4; tries++)
                {
                    float left = marker.Label.PositionXOffset;
                    float top = marker.Label.PositionYOffset;
                    float right = left + marker.Label.SuggestedWidth;
                    bool clash = placed.Any(other =>
                        other.Label.PositionXOffset < right &&
                        other.Label.PositionXOffset + other.Label.SuggestedWidth > left &&
                        Math.Abs(other.Label.PositionYOffset - top) < 18f);
                    if (!clash)
                    {
                        break;
                    }
                    marker.Label.PositionYOffset = top + 18f;
                }
                placed.Add(marker);
            }
        }

        private static void HideMarker(WorldMarker marker)
        {
            if (!marker.Shown)
            {
                return;
            }
            marker.Shown = false;
            marker.Id = string.Empty;
            marker.Halo.IsVisible = false;
            marker.Figure.IsVisible = false;
            marker.Label.IsVisible = false;
        }

        // ---- picking --------------------------------------------------------------

        private bool TryPickWorld(Vec2 cursor)
        {
            WorldMarker best = null;
            float bestDistance = PickRadius * PickRadius;
            bool convoy = false;
            foreach (WorldMarker marker in _peerMarkers)
            {
                if (!marker.Shown)
                {
                    continue;
                }
                // The pawn stands above its point: its body counts too.
                float dx = cursor.X - marker.X;
                float dy = cursor.Y - (marker.Y - PeerFigureSize * 0.4f);
                float d = dx * dx + dy * dy * 0.5f;
                if (d < bestDistance)
                {
                    best = marker;
                    bestDistance = d;
                }
            }
            foreach (WorldMarker marker in _convoyMarkers)
            {
                if (!marker.Shown)
                {
                    continue;
                }
                float dx = cursor.X - marker.X;
                float dy = cursor.Y - (marker.Y - ConvoyFigureSize * 0.4f);
                float d = dx * dx + dy * dy * 0.5f;
                if (d < bestDistance)
                {
                    best = marker;
                    bestDistance = d;
                    convoy = true;
                }
            }
            if (best == null)
            {
                return false;
            }
            _worldPick = (convoy ? "c:" : "p:") + best.Id;
            _worldNote = string.Empty;
            DikozemieLobbyShellIntegration.TraceButtonAction("world pick " + _worldPick);
            RefreshSidePanel();
            LayoutContent();
            return true;
        }

        private DikozemieWorldPlayer PickedPeer =>
            _worldPick != null && _worldPick.StartsWith("p:", StringComparison.Ordinal)
                ? DikozemieWorld.Find(_worldPick.Substring(2))
                : null;

        private DikozemieWorldConvoy PickedConvoy =>
            _worldPick != null && _worldPick.StartsWith("c:", StringComparison.Ordinal)
                ? DikozemieWorld.Convoys.FirstOrDefault(c => c.Id == _worldPick.Substring(2))
                : null;

        // ---- the card of a hero or a convoy ------------------------------------------

        // The side panel for what was clicked on the map; false: nothing to show.
        private bool RefreshWorldCard()
        {
            DikozemieWorldPlayer peer = PickedPeer;
            DikozemieWorldConvoy convoy = PickedConvoy;
            if (peer == null && convoy == null)
            {
                _worldPick = null;
                return false;
            }
            HideTownOptions();
            SetMapPanelVisible(true);
            _legend.IsVisible = false;
            _enterButton.IsVisible = false;
            _boatButton.IsVisible = false;
            _goButton.IsVisible = _travelling && !DikozemieWorld.IsFollower;
            _goText.Text = "ОСТАНОВИТЬСЯ";
            HideWorldButtons();
            float stack = 128f;
            if (peer != null)
            {
                FillPeerCard(peer, ref stack);
            }
            else
            {
                FillConvoyCard(convoy, ref stack);
            }
            if (_goButton.IsVisible)
            {
                _goButton.MarginBottom = stack;
            }
            return true;
        }

        private void FillPeerCard(DikozemieWorldPlayer p, ref float stack)
        {
            DikozemieWorld.Position(p, out float px, out float py);
            float distance = DikozemieWorld.Distance(px, py, _travel.X, _travel.Y);
            bool mate = DikozemieWorld.Party != null && p.Party == DikozemieWorld.Party.Id;
            _title.Text = p.Name.ToUpperInvariant();
            var lines = new List<string>
            {
                "Герой · " + DikozemieWorld.CultureName(p.Culture) + " · слава " + FormatCoins(p.Rating) +
                    " · ур. " + Math.Max(1, p.Level),
                (p.Boat ? (p.Vessel == DikozemieTravelState.Ship ? "В ладье" : "В лодке") : p.Mounted ? "Верхом" : "Пешком") +
                    (p.Load > 0
                        ? " · обоз " + p.Load + " из " + Math.Max(p.Load, p.Capacity) + ": вьюки видно издалека"
                        : " · налегке") +
                    (p.Escort > 0 ? " · охрана " + p.Escort : string.Empty) +
                    (p.Wound > 0 ? " · ранен" : string.Empty)
            };
            if (!string.IsNullOrEmpty(p.Party))
            {
                lines.Add(mate ? "Идёт с вашим отрядом." + (p.Leader ? " Ведёт отряд." : string.Empty)
                    : p.Leader ? "Ведёт свой отряд." : "Идёт с чужим отрядом.");
            }
            if (!string.IsNullOrEmpty(p.Clan))
            {
                // Stage 6: whose banner he follows.
                bool ours = p.Clan == DikozemieClans.MyClanId;
                lines.Add("Клан «" + DikozemieClans.ClanName(p.Clan) + "»" + (ours ? " — ваш клан." :
                    DikozemieClans.AtWarWith(p.Clan) ? " — вы воюете: бой с ним не разбой." : "."));
            }
            if (p.Outlaw)
            {
                lines.Add("Нападал на путников: держитесь подальше или берите охрану.");
            }
            lines.Add((p.Moving ? "Идёт по дороге, " : "Стоит, ") + Steps(distance) + " от вас.");
            string attackWhy = AttackBlock(p, distance, mate);
            if (!string.IsNullOrEmpty(attackWhy) && !mate)
            {
                lines.Add(attackWhy);
            }
            if (!string.IsNullOrEmpty(_worldNote))
            {
                lines.Add(string.Empty);
                lines.Add(_worldNote);
            }
            _description.Text = string.Join("\n", lines);

            bool follower = DikozemieWorld.IsFollower;
            if (!mate && !follower)
            {
                _worldBlocked = attackWhy;
                SetButtonText(_worldAttack, p.Boat ? "НА АБОРДАЖ" : "ПРЕГРАДИТЬ ПУТЬ",
                    string.IsNullOrEmpty(attackWhy) && !_worldBusy);
                _worldAttack.IsVisible = true;
                _worldAttack.MarginBottom = stack;
                stack += 68f;
            }
            if (!follower && !_travel.IsPassenger)
            {
                SetButtonText(_worldApproach, p.Moving ? "ДОГНАТЬ" : "ПОДОЙТИ", distance > 30f);
                _worldApproach.IsVisible = true;
                _worldApproach.MarginBottom = stack;
                stack += 68f;
            }
            bool canInvite = !mate && string.IsNullOrEmpty(p.Party) && DikozemieWorld.IsPartyLeader &&
                distance <= InviteReach && !p.Busy;
            if (canInvite)
            {
                SetButtonText(_worldInvite, "ПОЗВАТЬ В ОТРЯД");
                _worldInvite.IsVisible = true;
                _worldInvite.MarginBottom = stack;
                stack += 68f;
            }
            if (string.IsNullOrEmpty(p.Clan) && DikozemieClans.InClan && DikozemieClans.Can("invite"))
            {
                SetButtonText(_worldClan, "ПОЗВАТЬ В КЛАН", !_worldBusy);
                _worldClan.IsVisible = true;
                _worldClan.MarginBottom = stack;
                stack += 68f;
            }
            // Plan, «Античит»: players complain, the observer acts.
            SetButtonText(_worldReport, DikozemieWatch.IsObserver ? "МЕРЫ НАБЛЮДАТЕЛЯ" : "ПОЖАЛОВАТЬСЯ",
                DikozemieEconomy.CanAsk);
            _worldReport.IsVisible = true;
            _worldReport.MarginBottom = stack;
            stack += 68f;
        }

        private void OnWorldReport()
        {
            DikozemieWorldPlayer p = PickedPeer;
            if (p == null || !DikozemieEconomy.CanAsk)
            {
                return;
            }
            if (DikozemieWatch.IsObserver)
            {
                DikozemieWatch.AskMeasure(p.Id, p.Name);
                return;
            }
            DikozemieLobbyMapNode here = NodeHere();
            DikozemieWatch.AskReport(p.Id, p.Name, here != null ? "у " + DikozemieTown.DisplayName(here) : "на карте");
        }

        private void OnWorldClanInvite()
        {
            DikozemieWorldPlayer p = PickedPeer;
            if (p == null || _worldBusy)
            {
                return;
            }
            _worldBusy = true;
            DikozemieClans.Act("invite", "{\"target\":" + DikozemieWorld.Json(p.Id) + "}", (state, error) =>
            {
                _worldBusy = false;
                _worldNote = error ?? p.Name + " получит приглашение в клан «" + DikozemieClans.Mine?.Name + "».";
                RefreshSidePanel();
            });
        }

        private static string Steps(float distance)
        {
            int count = Math.Max(1, (int)Math.Round(distance));
            int last = count % 10;
            int lastTwo = count % 100;
            return count + (lastTwo >= 11 && lastTwo <= 14 ? " шагов" : last == 1 ? " шаг" :
                last >= 2 && last <= 4 ? " шага" : " шагов");
        }

        // Why this hero cannot be stopped now (empty: he can).
        private string AttackBlock(DikozemieWorldPlayer p, float distance, bool mate)
        {
            if (mate)
            {
                return string.Empty;
            }
            if (DikozemieWorld.IsFollower)
            {
                return "Решает тот, кто ведёт ваш отряд.";
            }
            if (!string.IsNullOrEmpty(p.Guard))
            {
                return "Напасть нельзя: " + p.Guard + ".";
            }
            if (p.Boat != _travel.Boat)
            {
                return p.Boat ? "Он на воде: с берега не достать." : "Вы на воде: на берег не прыгнуть.";
            }
            if (!_travelling && !string.IsNullOrEmpty(_travel.NodeId) && NodeHere() != null)
            {
                return "Из поселения не нападают: выйдите на дорогу.";
            }
            float cooldown = DikozemieWorld.View?.Marks?.Cooldown ?? 0f;
            if (cooldown > 0f)
            {
                return "Ваши люди ещё не отдышались: " + (int)Math.Ceiling(cooldown) + " с.";
            }
            if (distance > AttackReach)
            {
                return "Чтобы преградить путь, подойдите ближе.";
            }
            return string.Empty;
        }

        private void FillConvoyCard(DikozemieWorldConvoy c, ref float stack)
        {
            DikozemieWorld.Position(c, out float cx, out float cy);
            float distance = DikozemieWorld.Distance(cx, cy, _travel.X, _travel.Y);
            bool mine = c.Owner == DikozemieWorld.MyId;
            bool ours = !mine && DikozemieWorld.Party != null &&
                DikozemieWorld.Party.Members.Any(m => m.Id == c.Owner);
            _title.Text = (mine ? "ВАШ " + c.Name : c.Name).ToUpperInvariant();
            var lines = new List<string>();
            string from = DikozemieWorld.PlaceName(c.From);
            string to = DikozemieWorld.PlaceName(c.To);
            if (c.IsShip)
            {
                lines.Add((c.Npc ? "Купеческий корабль" : "Корабль с вкладом: " + c.OwnerName) + ". Шкипер " + c.Master + ".");
                lines.Add("Идёт: " + from + " → " + to + ".");
                lines.Add(c.Npc
                    ? "Трюм: " + (c.Cargo.Count > 0 ? DikozemieWorld.Goods(c.Cargo) : "не видно") + "."
                    : "В деле " + c.Value + " мон. вкладчиков.");
            }
            else
            {
                lines.Add((c.Npc ? "Обоз городских купцов" : mine ? "Ваш караван" : "Караван игрока " + c.OwnerName) +
                    ". Караванщик " + c.Master + ", охрана " + c.Guards + ", вьючных лошадей " + c.Beasts + ".");
                lines.Add("Дорога: " + from + " → " + to + ".");
                lines.Add(c.Load > 0 ? "Везут: " + DikozemieWorld.Goods(c.Cargo) + "." : "Идут порожняком.");
            }
            lines.Add(Steps(distance) + " от вас.");
            string why = RaidBlock(c, distance, mine || ours);
            if (!string.IsNullOrEmpty(why))
            {
                lines.Add(why);
            }
            if (!string.IsNullOrEmpty(_worldNote))
            {
                lines.Add(string.Empty);
                lines.Add(_worldNote);
            }
            _description.Text = string.Join("\n", lines);
            if (!mine && !ours)
            {
                _worldBlocked = why;
                SetButtonText(_worldAttack, c.IsShip ? "НА АБОРДАЖ" : "НАПАСТЬ НА ОБОЗ",
                    string.IsNullOrEmpty(why) && !_worldBusy);
                _worldAttack.IsVisible = true;
                _worldAttack.MarginBottom = stack;
                stack += 68f;
            }
            if (!DikozemieWorld.IsFollower && !_travel.IsPassenger)
            {
                SetButtonText(_worldApproach, "ДОГНАТЬ", distance > 30f);
                _worldApproach.IsVisible = true;
                _worldApproach.MarginBottom = stack;
                stack += 68f;
            }
        }

        private string RaidBlock(DikozemieWorldConvoy c, float distance, bool own)
        {
            if (own)
            {
                return string.Empty;
            }
            if (DikozemieWorld.IsFollower)
            {
                return "Нападать решает тот, кто ведёт ваш отряд.";
            }
            if (c.Raided)
            {
                return "Его только что грабили: охрана настороже.";
            }
            if (c.IsShip != _travel.Boat)
            {
                return c.IsShip ? "До корабля нужно доплыть." : "С воды обоз не достать.";
            }
            if (c.IsShip && !DikozemieLaunchGuard.IsNavalAvailable)
            {
                return DikozemieLaunchGuard.NavalUnavailableText;
            }
            float cooldown = DikozemieWorld.View?.Marks?.Raid ?? 0f;
            if (cooldown > 0f)
            {
                return "После набега люди отдыхают: " + (int)Math.Ceiling(cooldown) + " с.";
            }
            if (distance > AttackReach * 1.4f)
            {
                return "Чтобы напасть, подойдите ближе.";
            }
            return string.Empty;
        }

        // ---- the card's buttons ------------------------------------------------------

        private void OnWorldApproach()
        {
            DikozemieWorldPlayer near = PickedPeer;
            DikozemieWorldConvoy nearConvoy = PickedConvoy;
            float gap = float.MaxValue;
            if (near != null)
            {
                DikozemieWorld.Position(near, out float nx, out float ny);
                gap = DikozemieWorld.Distance(nx, ny, _travel.X, _travel.Y);
            }
            else if (nearConvoy != null && DikozemieWorld.Position(nearConvoy, out float cx, out float cy))
            {
                gap = DikozemieWorld.Distance(cx, cy, _travel.X, _travel.Y);
            }
            if (gap <= 30f)
            {
                _worldNote = "Вы уже рядом.";
                RefreshSidePanel();
                return;
            }
            float x;
            float y;
            DikozemieWorldPlayer peer = PickedPeer;
            DikozemieWorldConvoy convoy = PickedConvoy;
            if (peer != null)
            {
                DikozemieWorld.Position(peer, out x, out y);
                if (peer.Moving)
                {
                    // Cut him off a little ahead.
                    x = peer.Tx;
                    y = peer.Ty;
                }
            }
            else if (convoy != null)
            {
                DikozemieWorld.Position(convoy, out x, out y);
            }
            else
            {
                return;
            }
            string keep = _worldPick;
            SelectPoint(x, y);
            _worldPick = keep;
            if (_route != null && _route.Xs.Count >= 2)
            {
                ToggleTravel();
            }
            RefreshSidePanel();
        }

        private void OnWorldAttack()
        {
            if (_worldBusy)
            {
                return;
            }
            if (!string.IsNullOrEmpty(_worldBlocked))
            {
                _worldNote = _worldBlocked;
                RefreshSidePanel();
                return;
            }
            DikozemieWorldPlayer peer = PickedPeer;
            DikozemieWorldConvoy convoy = PickedConvoy;
            if (peer != null)
            {
                string name = peer.Name;
                Inquiry(peer.Boat ? "НА АБОРДАЖ?" : "ПРЕГРАДИТЬ ПУТЬ?",
                    "Навстречу идёт " + name + ". Вы выходите на " + (peer.Boat ? "курс" : "дорогу") +
                    " и требуете дань: пятую часть кошеля и половину вьюков. " +
                    "Путник может заплатить, бежать, сдаться или принять бой" +
                    (peer.Party.Length > 0 ? " — вместе со своим отрядом." : ".") +
                    "\n\nНа полчаса вас запомнят как разбойника: это видно всем на карте. Проигравший бой — ваша добыча, " +
                    "но и вы — его, если проиграете.",
                    "НАПАСТЬ", "НЕ СЕЙЧАС", () => DoAttack(peer.Id, name), null);
                return;
            }
            if (convoy != null)
            {
                ShowConvoyEncounter(convoy);
            }
        }

        private void DoAttack(string target, string name)
        {
            _worldBusy = true;
            _worldNote = "Выходите на дорогу…";
            StopWalking();
            RefreshSidePanel();
            DikozemieWorld.Act("world/attack", "{\"target\":" + DikozemieWorld.Json(target) + "}", (answer, error) =>
            {
                _worldBusy = false;
                _worldNote = error ?? string.Empty;
                if (answer?.Meeting != null)
                {
                    _worldPick = null;
                    ShowMeeting(answer.Meeting);
                }
                RefreshSidePanel();
            });
        }

        private void OnWorldInvite()
        {
            DikozemieWorldPlayer peer = PickedPeer;
            if (peer == null || _worldBusy)
            {
                return;
            }
            _worldBusy = true;
            DikozemieWorld.Act("world/party/invite", "{\"target\":" + DikozemieWorld.Json(peer.Id) + "}", (answer, error) =>
            {
                _worldBusy = false;
                _worldNote = error ?? peer.Name + " получил приглашение идти с вами.";
                RefreshSidePanel();
            });
        }

        private void OnLeaveParty()
        {
            Inquiry("ПОКИНУТЬ ОТРЯД?", "Дальше вы пойдёте один. Отряд продолжит путь без вас.", "ПОКИНУТЬ", "ОСТАТЬСЯ",
                () => DikozemieWorld.Act("world/party/leave", "{}", (answer, error) =>
                {
                    _travelNote = error ?? "Вы покинули отряд и идёте своей дорогой.";
                    RefreshSidePanel();
                }), null);
        }

        // ---- the map's own lines ---------------------------------------------------------

        // A few words under the clock: who is around, the party, the marks.
        private string WorldLines()
        {
            if (!DikozemieWorld.IsOnline)
            {
                return string.Empty;
            }
            var lines = new List<string>();
            DikozemieWorldView view = DikozemieWorld.View;
            int near = DikozemieWorld.Players.Count(p => string.IsNullOrEmpty(p.In) &&
                (DikozemieWorld.Party == null || p.Party != DikozemieWorld.Party.Id));
            lines.Add("В мире " + DikozemieWorld.Heroes(Math.Max(1, view.Online)) +
                (near > 0 ? ", рядом с вами: " + near + "." : "."));
            DikozemieWorldParty party = DikozemieWorld.Party;
            if (party != null && party.Members.Count > 1)
            {
                string leader = party.Members.FirstOrDefault(m => m.Id == party.Leader)?.Name ?? "?";
                lines.Add("Отряд: " + string.Join(", ", party.Members.Select(m => m.Name +
                    (m.Id == party.Leader ? " (ведёт)" : string.Empty) + (m.Fresh ? string.Empty : " — отстал"))) + ".");
                if (DikozemieWorld.IsFollower)
                {
                    lines.Add("Куда " + leader + ", туда и вы.");
                }
            }
            if (view.Marks?.Outlaw == true)
            {
                lines.Add("Вас видят разбойником: вы нападали на путников.");
            }
            if (view.Marks?.Protected == true)
            {
                lines.Add("После поражения вас никто не тронет ещё несколько минут.");
            }
            return "\n" + string.Join("\n", lines);
        }

        // ---- a party walks behind its leader ---------------------------------------------

        // True: the hero followed his leader this frame and does not walk himself.
        private bool TickFollow(float dt)
        {
            if (!DikozemieWorld.IsFollower || _travel == null)
            {
                // The party broke up at sea: a member with no vessel of his
                // own is set ashore instead of drifting in someone's boat.
                if (_travel != null && !_travelling && DikozemieTravelState.PutAshoreIfStranded(_travel))
                {
                    _route = null;
                    _hasSelectedPoint = false;
                    _travelNote = "Отряда больше нет. Лодочник высадил вас на ближайший берег.";
                    RefreshSidePanel();
                }
                return false;
            }
            DikozemieWorldPlayer leader = DikozemieWorld.Leader;
            if (leader == null)
            {
                return true;
            }
            if (_travelling)
            {
                StopWalking();
                _route = null;
                _hasSelectedPoint = false;
            }
            if (!string.IsNullOrEmpty(leader.In))
            {
                // The leader went into a place: the party is there with him.
                DikozemieLobbyMapNode node = DikozemieLobbyMapCatalog.Find(leader.In);
                if (node?.Position != null && _travel.NodeId != node.Id)
                {
                    _travel.X = node.Position.X;
                    _travel.Y = node.Position.Y;
                    _travel.NodeId = node.Id;
                    _travel.Boat = false;
                    DikozemieTravelState.Save();
                    if (!_roadOpen && !_meetingOpen)
                    {
                        _worldPick = null;
                        OpenTown(node);
                        _townNote = "Отряд вошёл в " + DisplayNodeName(node) + ".";
                        RefreshSidePanel();
                    }
                }
                return true;
            }
            if (_townOpen && !_roadOpen)
            {
                // The leader set out: the party leaves the town behind him.
                CloseTown();
                _travelNote = leader.Name + " выводит отряд в путь — вы идёте следом.";
            }
            DikozemieWorld.Position(leader, out float lx, out float ly);
            float hx = leader.Moving ? leader.Tx - leader.X : 1f;
            float hy = leader.Moving ? leader.Ty - leader.Y : 0f;
            float heading = Math.Max(0.01f, (float)Math.Sqrt(hx * hx + hy * hy));
            int place = Math.Max(1, (DikozemieWorld.Party?.Members.FindIndex(m => m.Id == DikozemieWorld.MyId) ?? 1));
            lx -= hx / heading * 14f * place;
            ly -= hy / heading * 14f * place;
            float k = Math.Min(1f, dt * 4f);
            _travel.X += (lx - _travel.X) * k;
            _travel.Y += (ly - _travel.Y) * k;
            _travel.Boat = leader.Boat;
            _travel.NodeId = string.Empty;
            _saveCountdown -= dt;
            if (_saveCountdown <= 0f)
            {
                _saveCountdown = 5f;
                DikozemieTravelState.Save();
            }
            if (leader.Moving && _zoom > MinZoom && _untilFollow <= 0f)
            {
                _centerOnHero = true;
            }
            return true;
        }

        // ---- meetings on the road --------------------------------------------------------

        private void TickMeeting()
        {
            DikozemieWorldMeeting m = DikozemieWorld.Meeting;
            if (m == null)
            {
                if (_meetingOpen && !string.IsNullOrEmpty(_meetingId))
                {
                    // The server forgot it (done and archived): close quietly.
                    CloseWorldScreen(false);
                }
                return;
            }
            if (m.Id == _meetingDismissed)
            {
                return;
            }
            if (!_meetingOpen || _meetingId != m.Id || _meetingShownState != m.State)
            {
                ShowMeeting(m);
            }
            if (m.State == "fighting" && m.Fight != null && _meetingStartedFight != m.Id &&
                TaleWorlds.MountAndBlade.Mission.Current == null)
            {
                StartMeetingBattle(m);
            }
        }

        private void ShowMeeting(DikozemieWorldMeeting m)
        {
            if (m == null)
            {
                return;
            }
            bool fresh = _meetingId != m.Id;
            StopWalking();
            DikozemieTravelState.Save();
            if (_townOpen)
            {
                _townOpen = false;
                DikozemieTown.IsTownScreenOpen = false;
                HideTownFigures();
            }
            CloseLayersForRoad();
            _passagePick = false;
            _roadOpen = true;
            _meetingOpen = true;
            _raidOpen = false;
            _roadCard = null;
            _roadOutcome = null;
            _meetingId = m.Id;
            _meetingShownState = m.State;
            _worldPick = null;
            if (fresh)
            {
                _roadNote = string.Empty;
            }
            DikozemieLobbyShellIntegration.TraceButtonAction("world meeting show id=" + m.Id + " state=" + m.State +
                " side=" + m.Side + " leader=" + m.Leader);
            ShowMeetingFigures(m);
            RefreshSidePanel();
            LayoutContent();
        }

        private void ShowMeetingFigures(DikozemieWorldMeeting m)
        {
            if (m == null)
            {
                return;
            }
            // The other side stands in the picture; after the fight, the winners.
            List<DikozemieWorldPerson> people = (m.State == "won" || m.State == "done") && m.Winner == "us" ? m.Us : m.Them;
            HideFigures();
            int index = 0;
            foreach (DikozemieWorldPerson person in people.Take(RoadFigureCount))
            {
                if (person.Id == DikozemieWorld.MyId)
                {
                    SetHeroFigureAt(index);
                }
                else
                {
                    SetLookFigure(index, DikozemieWorld.LookOf(person.Id), person.Culture);
                }
                index++;
            }
            _figureCount = index;
            string place = m.AtSea ? "МОРЕ" : "ДОРОГА";
            _roadTitle.Text = MeetingTitle(m);
            _roadSubtitle.Text = place + "  ·  " + DikozemieRoads.ClockText.ToUpperInvariant() +
                (m.Fight != null ? "  ·  " + DikozemieWorldBattle.ModeText(m.Fight.Mode, m.Fight.Size).ToUpperInvariant() : string.Empty);
            _roadCaption.Text = string.Join("     ", people.Select(p => p.Name + " — слава " + FormatCoins(p.Rating) +
                (p.Mounted ? ", верхом" : string.Empty)));
        }

        private static string MeetingTitle(DikozemieWorldMeeting m)
        {
            switch (m.State)
            {
                case "pending":
                    return m.IsAttacker ? "ВЫ ПРЕГРАДИЛИ ПУТЬ" : "ВАМ ПРЕГРАДИЛИ ПУТЬ";
                case "fighting":
                    return m.AtSea ? "АБОРДАЖ" : "БОЙ";
                case "won":
                    return m.Winner == "us" ? "ПОБЕДА" : "ПОРАЖЕНИЕ";
                default:
                    return m.Winner == "us" ? "ПОБЕДА" : m.Winner == "them" ? "ПОРАЖЕНИЕ" : "ВСТРЕЧА НА ДОРОГЕ";
            }
        }

        private void RefreshMeetingPanel()
        {
            SetMapPanelVisible(false);
            HideTownOptions();
            HideWorldButtons();
            _optionCount = 0;
            DikozemieWorldMeeting m = DikozemieWorld.Meeting;
            if (m == null || m.Id != _meetingId)
            {
                CloseWorldScreen(false);
                return;
            }
            _title.Text = MeetingTitle(m);
            string them = string.Join(", ", m.Them.Select(p => p.Name));
            string us = string.Join(", ", m.Us.Select(p => p.Name));
            float since = (float)(DateTime.UtcNow - m.ReceivedUtc).TotalSeconds;
            var text = new List<string>();
            switch (m.State)
            {
                case "pending":
                {
                    int left = Math.Max(0, (int)Math.Ceiling(m.AnswerIn - since));
                    string demand = m.DemandCoins + " мон." +
                        (m.DemandCargo.Count > 0 ? " и половину вьюков: " + DikozemieWorld.Goods(m.DemandCargo) : string.Empty);
                    if (m.IsAttacker)
                    {
                        text.Add("Вы вышли на " + (m.AtSea ? "курс" : "дорогу") + ". Перед вами — " + them + ". Ваше слово: «" +
                            (m.DemandCargo.Count > 0 ? "Вьюки и кошелёк" : "Кошелёк") + " — или останешься тут».");
                        text.Add("Требуете: " + demand + ".");
                        text.Add(m.Leader
                            ? (m.Them.Count > 1 ? "Они решают" : "Путник решает") +
                              ": платить, бежать, сдаться или драться. Ответ — через " + left + " с; кто молчит, тот бежит."
                            : "Ведёт отряд " + m.LeaderName + ". Ждите ответа: " + left + " с.");
                        if (m.Leader)
                        {
                            AddOption("ОТСТУПИТЬ", "уйти с дороги", () => MeetingAct("world/withdraw", null));
                        }
                    }
                    else
                    {
                        text.Add((m.Them.Count > 1 ? "Дорогу перегородили: " : "Дорогу перегородил путник: ") + them +
                            (m.AtSea ? ", на воде" : string.Empty) + ". «" +
                            (m.DemandCargo.Count > 0 ? "Вьюки и кошелёк" : "Кошелёк") + " — или останешься тут».");
                        text.Add("Требуют: " + demand + ".");
                        if (m.Leader)
                        {
                            text.Add("Не ответите за " + left + " с — побежите сами, и это хуже.");
                            int humans = m.Us.Count + m.Them.Count;
                            AddOption("ПРИНЯТЬ БОЙ", m.Us.Count + " ПРОТИВ " + m.Them.Count +
                                (humans == 2 && !m.AtSea ? " · НА МЕЧАХ" : string.Empty), () => MeetingAnswer("fight"));
                            AddOption("ЗАПЛАТИТЬ ДАНЬ", m.DemandCoins + " МОН." + (m.DemandCargo.Count > 0 ? " + ВЬЮКИ" : string.Empty),
                                () => MeetingAnswer("pay"));
                            AddOption(m.AtSea ? "УЙТИ ПОД ПАРУСОМ" : "БЕЖАТЬ", m.FleeChance + "%", () => MeetingAnswer("flee"));
                            AddOption("СДАТЬСЯ", "отдать обоз и часть кошеля", () => MeetingAnswer("surrender"));
                        }
                        else
                        {
                            text.Add("Решает " + m.LeaderName + ": " + left + " с.");
                        }
                    }
                    break;
                }
                case "fighting":
                {
                    string mode = DikozemieWorldBattle.ModeText(m.Fight?.Mode, m.Fight?.Size ?? 1);
                    text.Add((m.Cornered ? "Уйти не вышло: вас окружили. " : string.Empty) + "Будет " + mode + ".");
                    text.Add("Сервер боя сводит вас: " + us + " против " + them + ". Победитель решит судьбу побеждённых.");
                    if (m.Fight?.Mode == "duel")
                    {
                        text.Add("Схватка идёт пешими, на мечах. Кто не придёт на бой за четыре минуты, тот бежал и бросил обоз.");
                    }
                    if (DikozemieWorldBattle.IsPending)
                    {
                        text.Add(DikozemiePvpSearchController.IsSearching ? DikozemiePvpSearchController.Status : "Идём в бой…");
                    }
                    else
                    {
                        AddOption("В БОЙ", "войти на сервер боя", () => StartMeetingBattle(m));
                    }
                    break;
                }
                case "won":
                {
                    int left = Math.Max(0, (int)Math.Ceiling(m.DecideIn - since));
                    if (m.Winner == "us")
                    {
                        if (m.Leader)
                        {
                            text.Add("Побеждённые лежат в пыли: " + them + ". Их судьба — ваша. Не решите за " + left +
                                " с — люди обчистят их сами.");
                            AddOption("ОБОБРАТЬ", "кошель и вьюки", () => MeetingAct("world/decide", "rob"));
                            AddOption("ПРОДАТЬ НА АРЕНУ", "обобрать и сдать распорядителю", () => MeetingAct("world/decide", "sell"));
                            AddOption("ОТПУСТИТЬ", "с миром", () => MeetingAct("world/decide", "release"));
                        }
                        else
                        {
                            text.Add("Победа! Что делать с побеждёнными, решает " + m.LeaderName + ".");
                        }
                    }
                    else
                    {
                        text.Add("Вы проиграли. " + them + (m.Them.Count > 1 ? " решают" : " решает") +
                            ", что с вами будет: обобрать, отпустить или продать на арену. " + left + " с.");
                    }
                    break;
                }
                default:
                    text.Add(string.IsNullOrEmpty(m.Outcome) ? "Встреча окончена." : m.Outcome);
                    if (_pendingCaptive != null)
                    {
                        AddOption("В ПОВОЗКУ", "на арену", () =>
                        {
                            _meetingDismissed = m.Id;
                            DikozemieWorldNote captive = _pendingCaptive;
                            _pendingCaptive = null;
                            GoCaptiveTo(captive.Town, captive.Text);
                        });
                        break;
                    }
                    AddOption(m.AtSea ? "ПЛЫТЬ ДАЛЬШЕ" : "ИДТИ ДАЛЬШЕ", string.Empty, () =>
                    {
                        _meetingDismissed = m.Id;
                        CloseWorldScreen(true);
                    });
                    break;
            }
            if (m.Log.Count > 0 && m.State != "done")
            {
                text.Add(string.Empty);
                text.Add(string.Join(" ", m.Log.Skip(Math.Max(0, m.Log.Count - 3))));
            }
            if (!string.IsNullOrEmpty(_roadNote))
            {
                text.Add(string.Empty);
                text.Add(_roadNote);
            }
            text.Add(string.Empty);
            text.Add(HeroStatus());
            _description.Text = string.Join("\n", text);
            StackOptions();
        }

        private void MeetingAnswer(string choice)
        {
            MeetingAct("world/answer", choice);
        }

        private void MeetingAct(string path, string choice)
        {
            if (_worldBusy)
            {
                return;
            }
            _worldBusy = true;
            string body = "{\"meeting\":" + DikozemieWorld.Json(_meetingId) +
                (choice == null ? string.Empty : ",\"choice\":" + DikozemieWorld.Json(choice)) + "}";
            DikozemieWorld.Act(path, body, (answer, error) =>
            {
                _worldBusy = false;
                _roadNote = error ?? string.Empty;
                if (answer?.Meeting != null)
                {
                    ShowMeeting(answer.Meeting);
                }
                RefreshSidePanel();
            });
        }

        private void StartMeetingBattle(DikozemieWorldMeeting m)
        {
            if (m?.Fight == null)
            {
                return;
            }
            _meetingStartedFight = m.Id;
            var plan = new DikozemieWorldBattlePlan
            {
                Kind = "meeting",
                Id = m.Fight.Guid,
                Mode = m.Fight.Mode,
                Opponent = m.Fight.Opponent,
                GroupSize = Math.Max(2, m.Fight.Humans),
                SideLeader = m.Fight.SideLeader,
                TeamSize = Math.Max(1, m.Fight.Size),
                PreferredPort = m.Fight.Port,
                Title = MeetingTitle(m),
                // The one who was stopped goes second: two duel servers, and
                // the pair must stand on the same one.
                SearchNotBeforeUtc = m.Fight.Mode == "duel" && !m.IsAttacker
                    ? DateTime.UtcNow.AddSeconds(25)
                    : DateTime.UtcNow
            };
            if (!DikozemieWorldBattle.Start(plan, out string error))
            {
                _meetingStartedFight = string.Empty;
                _roadNote = error;
            }
            RefreshSidePanel();
        }

        // ---- the party's fight on the road ------------------------------------------------

        // The leader took a fight with bandits: with live members around, it
        // becomes one online battle, everybody on one side.
        private bool TryPartyRoadFight(DikozemieRoadEncounter card)
        {
            DikozemieWorldParty party = DikozemieWorld.Party;
            if (party == null || party.Members.Count(m => m.Fresh) < 2 || !DikozemieWorld.IsPartyLeader)
            {
                return false;
            }
            int members = party.Members.Count(m => m.Fresh);
            int teamSize = Math.Max(members, Math.Max(2, card.Foes.Count));
            byte cell = DikozemieTravelTerrain.AtAtlas(_travel.X, _travel.Y);
            string body = "{\"teamSize\":" + teamSize + ",\"venue\":\"arena\",\"card\":{\"kind\":" +
                DikozemieWorld.Json(card.Kind.ToString()) + ",\"seed\":" + card.Seed + ",\"culture\":" +
                DikozemieWorld.Json(card.Culture) + ",\"forest\":" + (card.Forest ? "true" : "false") +
                ",\"hills\":" + (DikozemieTravelTerrain.Near(_travel.X, _travel.Y, DikozemieTravelTerrain.Mountain, 6) ? "true" : "false") +
                ",\"coast\":" + (DikozemieTravelTerrain.Near(_travel.X, _travel.Y, DikozemieTravelTerrain.Water, 8) ? "true" : "false") +
                ",\"night\":" + (card.Night ? "true" : "false") + ",\"title\":" + DikozemieWorld.Json(card.Title) +
                ",\"foes\":" + card.Foes.Count + ",\"x\":" + _travel.X.ToString("0.#", CultureInfo.InvariantCulture) +
                ",\"y\":" + _travel.Y.ToString("0.#", CultureInfo.InvariantCulture) + "}}";
            _fightCard = card;
            _roadNote = "Отряд берётся за оружие…";
            RefreshSidePanel();
            DikozemieWorld.Act("world/party/order", body, (answer, error) =>
            {
                DikozemieWorldPartyBattle battle = answer?.Party?.Battle;
                if (battle == null)
                {
                    // Nobody to call after all: the hero fights alone.
                    _roadNote = error ?? string.Empty;
                    StartLocalRoadFight(card, card.Cornered);
                    return;
                }
                _partyBattlesSeen.Add(battle.Id);
                StartPartyBattle(battle, card.Title);
            });
            return true;
        }

        private void StartPartyBattle(DikozemieWorldPartyBattle battle, string title)
        {
            var plan = new DikozemieWorldBattlePlan
            {
                Kind = "party",
                Id = battle.Id,
                Mode = battle.Venue == "naval" ? "naval" : "team",
                GroupSize = Math.Max(2, battle.Members.Count),
                SideLeader = DikozemieWorld.Party?.Leader ?? DikozemieWorld.MyId,
                TeamSize = Math.Max(2, battle.TeamSize),
                Title = title,
                Card = battle.Card
            };
            if (!DikozemieWorldBattle.Start(plan, out string error))
            {
                _roadNote = error;
                RefreshSidePanel();
            }
        }

        // A member sees the fight his leader took and goes into it.
        private void TickPartyBattle()
        {
            DikozemieWorldPartyBattle battle = DikozemieWorld.Party?.Battle;
            if (battle == null || _partyBattlesSeen.Contains(battle.Id) || DikozemieWorld.IsPartyLeader ||
                !battle.Members.Contains(DikozemieWorld.MyId) || TaleWorlds.MountAndBlade.Mission.Current != null)
            {
                return;
            }
            _partyBattlesSeen.Add(battle.Id);
            if (DikozemieEconomy.ServerNow - battle.At > 180)
            {
                return;
            }
            DikozemieRoadEncounter card = CardOf(battle.Card);
            if (card != null)
            {
                _fightCard = card;
                ShowEncounter(card);
                _roadNote = (DikozemieWorld.Party?.Members.FirstOrDefault(m => m.Id == DikozemieWorld.Party.Leader)?.Name ?? "Ведущий") +
                    " ведёт отряд в бой. Вы идёте с ним.";
                RefreshSidePanel();
            }
            StartPartyBattle(battle, card?.Title ?? "БОЙ НА ДОРОГЕ");
        }

        private static DikozemieRoadEncounter CardOf(DikozemieWorldCard data)
        {
            if (data == null || !Enum.TryParse(data.Kind, out DikozemieRoadKind kind))
            {
                return null;
            }
            DikozemieRoadEncounter card = DikozemieRoads.Build(kind, data.Culture, data.Forest, data.Hills, data.Coast,
                data.Night, data.Seed, data.X, data.Y);
            if (card != null && !string.IsNullOrEmpty(data.Title))
            {
                card.Title = data.Title;
            }
            return card;
        }

        // Back from a battle of the shared world: a party's road fight ends
        // with the outcome card of that road for every member.
        private void TickFinishedWorldBattle()
        {
            if (!DikozemieWorldBattle.TryTakeFinished(out DikozemieWorldBattlePlan plan) || plan.Kind != "party")
            {
                return;
            }
            DikozemieRoadEncounter card = _fightCard ?? CardOf(plan.Card);
            _fightCard = null;
            if (card == null)
            {
                return;
            }
            _townOpen = false;
            _roadOpen = true;
            _meetingOpen = false;
            _raidOpen = false;
            _roadCard = card;
            if (!plan.ResultSeen)
            {
                ShowOutcome(new DikozemieRoadOutcome
                {
                    Title = card.Title,
                    Text = "Бой не состоялся: отряд разминулся с сервером боя. Разбойники ушли в лес.",
                    Good = true
                });
                return;
            }
            ShowOutcome(DikozemieRoads.ApplyFightResult(new DikozemieRoadFightResult
            {
                Victory = plan.Victory,
                Fled = false,
                FoesDown = plan.Victory ? card.Foes.Count : 0
            }, card));
        }

        // ---- raids on caravans and ships ---------------------------------------------------

        private void ShowConvoyEncounter(DikozemieWorldConvoy c)
        {
            StopWalking();
            DikozemieTravelState.Save();
            _roadOpen = true;
            _raidOpen = true;
            _meetingOpen = false;
            _roadCard = null;
            _roadOutcome = null;
            _raidConvoy = c;
            _raid = null;
            _roadNote = string.Empty;
            _worldPick = null;
            List<DikozemieRoadFighter> guards = ConvoyGuards(c);
            SetFigures(guards);
            _roadTitle.Text = c.Name.ToUpperInvariant();
            _roadSubtitle.Text = (c.IsShip ? "МОРЕ" : "ДОРОГА") + "  ·  " + DikozemieWorld.PlaceName(c.From).ToUpperInvariant() +
                " → " + DikozemieWorld.PlaceName(c.To).ToUpperInvariant() + "  ·  " + DikozemieRoads.ClockText.ToUpperInvariant();
            _roadCaption.Text = string.Join("     ", guards.Select(g => g.Name + (string.IsNullOrEmpty(g.Arms) ? string.Empty : " — " + g.Arms)));
            DikozemieLobbyShellIntegration.TraceButtonAction("world convoy card " + c.Id + " guards=" + c.Guards);
            RefreshSidePanel();
            LayoutContent();
        }

        private static List<DikozemieRoadFighter> ConvoyGuards(DikozemieWorldConvoy c)
        {
            string culture = DikozemieRoadFolk.FolkCulture(DikozemieLobbyMapCatalog.Find(c.From)?.Culture);
            int seed = Math.Abs((c.Id ?? string.Empty).GetHashCode());
            var list = new List<DikozemieRoadFighter>();
            DikozemieRoadFighter master = DikozemieRoadFolk.Create("escort_" + culture, culture, seed,
                (c.IsShip ? "Шкипер " : "Караванщик ") + c.Master);
            if (master != null)
            {
                master.IsLeader = true;
                list.Add(master);
            }
            for (int index = 0; index < Math.Min(5, Math.Max(1, c.Guards)); index++)
            {
                DikozemieRoadFighter guard = DikozemieRoadFolk.Create("escort_" + culture, culture, seed + 31 * (index + 1),
                    c.IsShip ? "Матрос" : "Охранник");
                if (guard != null)
                {
                    list.Add(guard);
                }
            }
            return list;
        }

        private void RefreshRaidPanel()
        {
            SetMapPanelVisible(false);
            HideTownOptions();
            HideWorldButtons();
            _optionCount = 0;
            DikozemieWorldConvoy c = _raidConvoy;
            if (c == null)
            {
                CloseWorldScreen(false);
                return;
            }
            DikozemieWorldRaid raid = _raid == null ? null : DikozemieWorld.View?.Raids?.FirstOrDefault(r => r.Id == _raid.Id) ?? _raid;
            var text = new List<string>();
            if (raid == null)
            {
                _title.Text = c.IsShip ? "КОРАБЛЬ" : "ОБОЗ";
                text.Add(c.IsShip
                    ? c.Name + " идёт тяжело, низко сидит в воде. На борту шкипер " + c.Master + " и матросы с топорами."
                    : "Караванщик " + c.Master + " ведёт " + c.Beasts + " вьючных лошадей. Охрана — " + c.Guards +
                      ": сразу видно, что за товар они будут драться.");
                if (!c.IsShip)
                {
                    text.Add(c.Load > 0 ? "Во вьюках: " + DikozemieWorld.Goods(c.Cargo) + "." : "Вьюки тощие: идут порожняком.");
                }
                text.Add(c.Npc ? "Это обоз городских купцов." : "Караван принадлежит игроку " + c.OwnerName + ": он узнает, кто его ограбил.");
                int party = DikozemieWorld.Party?.Members.Count(m => m.Fresh) ?? 1;
                text.Add(c.IsShip
                    ? "Абордаж идёт на сервере морских боёв: ваша лодка против корабля и его команды."
                    : party >= 2
                        ? "Отряд из " + DikozemieWorld.People(party) + " бьётся с охраной вместе, на сервере боя."
                        : "Бой с охраной — здесь же, у дороги. Добыча — сколько унесёте: вьючные места решают.");
                AddOption(c.IsShip ? "НА АБОРДАЖ" : "НАПАСТЬ", (c.IsShip ? "КОМАНДА " : (party >= 2 ? party + " ПРОТИВ " : "1 ПРОТИВ ")) +
                    (c.Guards + 1), () => StartRaid(c));
                AddOption("ПРОПУСТИТЬ", string.Empty, () => CloseWorldScreen(true));
            }
            else
            {
                switch (raid.State)
                {
                    case "fighting":
                        _title.Text = c.IsShip ? "АБОРДАЖ" : "НАБЕГ";
                        text.Add(raid.Mode == "local" ? "Охрана берётся за оружие…" :
                            "Сервер боя собирает ваших людей: " + DikozemieWorldBattle.ModeText(raid.Mode, raid.TeamSize) + ".");
                        if (raid.Mode != "local" && !DikozemieWorldBattle.IsPending && _raidStartedFight != raid.Id)
                        {
                            AddOption("В БОЙ", string.Empty, () => StartRaidBattle(raid));
                        }
                        break;
                    case "won":
                        _title.Text = "ДОБЫЧА";
                        text.Add(raid.Text);
                        AddOption("ИДТИ ДАЛЬШЕ", string.Empty, () => CloseWorldScreen(true));
                        break;
                    case "lost":
                        _title.Text = "НЕУДАЧА";
                        text.Add(string.IsNullOrEmpty(raid.Text) ? "Охрана отбилась." : raid.Text);
                        AddOption("ИДТИ ДАЛЬШЕ", string.Empty, () => CloseWorldScreen(true));
                        break;
                    default:
                        _title.Text = c.Name.ToUpperInvariant();
                        text.Add(string.IsNullOrEmpty(raid.Text) ? "Набег не состоялся." : raid.Text);
                        AddOption("ИДТИ ДАЛЬШЕ", string.Empty, () => CloseWorldScreen(true));
                        break;
                }
            }
            if (!string.IsNullOrEmpty(_roadNote))
            {
                text.Add(string.Empty);
                text.Add(_roadNote);
            }
            text.Add(string.Empty);
            text.Add(HeroStatus());
            _description.Text = string.Join("\n", text);
            StackOptions();
        }

        private void StartRaid(DikozemieWorldConvoy c)
        {
            if (_worldBusy)
            {
                return;
            }
            _worldBusy = true;
            _roadNote = "Подбираетесь ближе…";
            RefreshSidePanel();
            DikozemieWorld.Act("world/raid/start", "{\"id\":" + DikozemieWorld.Json(c.Id) + "}", (answer, error) =>
            {
                _worldBusy = false;
                if (answer?.Raid == null)
                {
                    _roadNote = error ?? "Не вышло.";
                    RefreshSidePanel();
                    return;
                }
                _raid = answer.Raid;
                _roadNote = string.Empty;
                if (_raid.Mode == "local")
                {
                    StartLocalRaidFight(c, _raid);
                }
                else
                {
                    StartRaidBattle(_raid);
                }
                RefreshSidePanel();
            });
        }

        private void StartLocalRaidFight(DikozemieWorldConvoy c, DikozemieWorldRaid raid)
        {
            string culture = DikozemieRoadFolk.FolkCulture(DikozemieLobbyMapCatalog.Find(c.From)?.Culture);
            byte cell = DikozemieTravelTerrain.AtAtlas(_travel.X, _travel.Y);
            bool forest = (cell & DikozemieTravelTerrain.Forest) != 0;
            int seed = Math.Abs(raid.Id.GetHashCode());
            var setup = new DikozemieRoadFightSetup
            {
                Title = c.Name.ToUpperInvariant(),
                Scene = DikozemieRoadScenes.Pick(culture, forest, seed),
                Season = DikozemieRoadScenes.Season(culture),
                Hour = DikozemieRoadScenes.AtmosphereHour(DikozemieRoads.HourOfDay),
                Ambush = false,
                HeroHealth = DikozemieRoads.HealthAtStart,
                Purpose = "Raid"
            };
            setup.Foes.AddRange(ConvoyGuards(c));
            setup.Allies.AddRange(DikozemieRoads.CompanyFighters());
            _raidFightId = raid.Id;
            if (!DikozemieRoadFight.TryStart(setup, out string error))
            {
                _raidFightId = string.Empty;
                _roadNote = error;
                DikozemieWorld.Act("world/raid/finish", "{\"raid\":" + DikozemieWorld.Json(raid.Id) + ",\"victory\":false}", null);
            }
        }

        private void StartRaidBattle(DikozemieWorldRaid raid)
        {
            _raidStartedFight = raid.Id;
            var plan = new DikozemieWorldBattlePlan
            {
                Kind = "raid",
                Id = raid.Id,
                Mode = raid.Mode,
                GroupSize = Math.Max(1, raid.GroupSize),
                SideLeader = raid.Leader,
                TeamSize = Math.Max(1, raid.TeamSize),
                Title = raid.Name
            };
            if (!DikozemieWorldBattle.Start(plan, out string error))
            {
                _raidStartedFight = string.Empty;
                _roadNote = error;
            }
        }

        // A raid of the party (its members go too) or one decided online:
        // the screen follows the server's word.
        private void TickRaids()
        {
            List<DikozemieWorldRaid> raids = DikozemieWorld.View?.Raids;
            if (raids == null)
            {
                return;
            }
            foreach (DikozemieWorldRaid raid in raids)
            {
                if (raid.State == "fighting" && raid.Mode != "local" && raid.Leader != DikozemieWorld.MyId &&
                    _raidStartedFight != raid.Id && TaleWorlds.MountAndBlade.Mission.Current == null)
                {
                    // A member of the raiding party: into the same battle.
                    _raidStartedFight = raid.Id;
                    _raid = raid;
                    _raidConvoy = _raidConvoy ?? new DikozemieWorldConvoy
                    {
                        Id = raid.Convoy, Kind = raid.Kind, Name = raid.Name, Master = raid.Master, Guards = raid.Guards,
                        From = raid.From, To = raid.To, Npc = raid.Npc, OwnerName = raid.OwnerName
                    };
                    ShowConvoyEncounter(_raidConvoy);
                    _raid = raid;
                    StartRaidBattle(raid);
                }
                if (_raid != null && raid.Id == _raid.Id && raid.State != _raid.State)
                {
                    _raid = raid;
                    if (!_raidOpen && raid.State != "fighting")
                    {
                        ShowConvoyEncounter(_raidConvoy ?? new DikozemieWorldConvoy { Name = raid.Name, Kind = raid.Kind, From = raid.From, To = raid.To });
                        _raid = raid;
                    }
                    RefreshSidePanel();
                }
            }
        }

        // Back from the road field of a raid: the trade server decides the spoils.
        private bool TryFinishRaidFight(DikozemieRoadFightResult result)
        {
            if (result?.Setup?.Purpose != "Raid" || string.IsNullOrEmpty(_raidFightId))
            {
                return false;
            }
            string raidId = _raidFightId;
            _raidFightId = string.Empty;
            _roadOpen = true;
            _raidOpen = true;
            _townOpen = false;
            if (!result.Victory)
            {
                DikozemieRoads.Hurt(result.Fled ? 1 : 2);
            }
            _roadNote = result.Victory ? "Считаете добычу…" : string.Empty;
            if (result.Victory)
            {
                SetHeroFigure();
            }
            DikozemieWorld.Act("world/raid/finish", "{\"raid\":" + DikozemieWorld.Json(raidId) + ",\"victory\":" +
                (result.Victory ? "true" : "false") + "}", (answer, error) =>
            {
                _roadNote = error ?? string.Empty;
                if (answer?.Raid != null)
                {
                    _raid = answer.Raid;
                    if (!result.Victory)
                    {
                        _raid.Text = (result.Fled ? "Вы ушли с поля, бросив затею." : "Охрана отбилась и намяла вам бока.") +
                            " Раны заживут в таверне или у знахарки." +
                            (string.IsNullOrEmpty(result.Progress) ? string.Empty : "\n\n" + result.Progress);
                    }
                    else if (!string.IsNullOrEmpty(result.Progress))
                    {
                        _raid.Text += "\n\n" + result.Progress;
                    }
                }
                RefreshSidePanel();
            });
            if (_raid == null)
            {
                _raid = new DikozemieWorldRaid { Id = raidId, State = "fighting", Mode = "local" };
            }
            RefreshSidePanel();
            LayoutContent();
            return true;
        }

        // ---- closing -------------------------------------------------------------------

        private void CloseWorldScreen(bool resume)
        {
            _meetingOpen = false;
            _raidOpen = false;
            _raidConvoy = null;
            _raid = null;
            _meetingId = string.Empty;
            _meetingShownState = string.Empty;
            CloseRoad(resume);
        }

        private void GoCaptiveTo(string townId, string text)
        {
            DikozemieWorldMeeting meeting = DikozemieWorld.Meeting;
            string by = meeting != null && meeting.Them.Count > 0 ? meeting.Them[0].Name : "Победитель";
            DikozemieLobbyMapNode town = DikozemieRoads.CaptureIn(DikozemieLobbyMapCatalog.Find(townId), by);
            _meetingOpen = false;
            _raidOpen = false;
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
                return;
            }
            OpenTown(town);
            _townNote = text + " Сутки в повозке, потом ворота, двор, решётка. Выход — через песок арены, выкуп или ночной побег.";
            RefreshSidePanel();
        }

        // ---- figures from looks ----------------------------------------------------------

        private void SetLookFigure(int index, DikozemieWorldLook look, string culture)
        {
            if (index < 0 || index >= RoadFigureCount)
            {
                return;
            }
            CharacterTableauWidget widget = _roadFigures[index];
            try
            {
                BasicCharacterObject body = MBObjectManager.Instance.GetObject<BasicCharacterObject>(
                    DikozemieRoadFolk.FaceTroop(look?.Culture ?? culture)) ??
                    MBObjectManager.Instance.GetObject<BasicCharacterObject>("mp_character");
                var visual = new CharacterViewModel();
                visual.FillFrom(body, -1, string.Empty);
                Equipment equipment = null;
                if (!string.IsNullOrEmpty(look?.Equip))
                {
                    try
                    {
                        equipment = Equipment.CreateFromEquipmentCode(look.Equip);
                    }
                    catch (Exception)
                    {
                        equipment = null;
                    }
                }
                if (equipment != null)
                {
                    visual.SetEquipment(equipment);
                }
                widget.BodyProperties = string.IsNullOrEmpty(look?.Body) ? visual.BodyProperties : look.Body;
                widget.EquipmentCode = visual.EquipmentCode;
                widget.CharStringId = visual.CharStringId;
                widget.IsFemale = look?.Female ?? false;
                widget.Race = Math.Max(0, look?.Race ?? 0);
                widget.StanceIndex = visual.StanceIndex;
                widget.ArmorColor1 = uint.MaxValue;
                widget.ArmorColor2 = uint.MaxValue;
                widget.BannerCodeText = visual.BannerCodeText;
                ItemObject horse = equipment?[EquipmentIndex.Horse].Item;
                widget.MountCreationKey = horse == null ? string.Empty : MountCreationKey.GetRandomMountKeyString(horse, 7);
                widget.IdleAction = visual.IdleAction;
                widget.IsTableauEnabled = true;
                widget.IsVisible = true;
            }
            catch (Exception error)
            {
                widget.IsVisible = false;
                Console.WriteLine("DikozemieOnline: world figure failed: " + error.Message);
            }
        }

        // The hero himself in one of the places of the stage.
        private void SetHeroFigureAt(int index)
        {
            DikozemieWorldLook own = DikozemieWorld.OwnLook();
            SetLookFigure(index, own, DikozemieHeroProfile.CultureId);
        }
    }
}
