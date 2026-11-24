using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Dikozemie.Online
{
    // Stage 6 of the plan, «Кланы и земля» (customer, 2026-11-08: «военные
    // кланы, которые будут бороться за территорию»). The trade server owns
    // all of it: who is in which clan, the clan's coins, who holds which
    // castle, the mercenaries, wars and sieges. The world pulse brings the
    // short part (the hero's clan, the land of the clans, the sieges seen);
    // the clan screen asks for the rest.

    [DataContract]
    internal sealed class DikozemieClanBrief
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "banner")] public string Banner = string.Empty;
        [DataMember(Name = "rank")] public string Rank = string.Empty;
        [DataMember(Name = "rev")] public long Rev;
        [DataMember(Name = "wars")] public List<string> Wars = new List<string>();
    }

    [DataContract]
    internal sealed class DikozemieClanName
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "banner")] public string Banner = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieLand
    {
        [DataMember(Name = "node")] public string Node = string.Empty;
        [DataMember(Name = "clan")] public string Clan = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "banner")] public string Banner = string.Empty;
        [DataMember(Name = "market")] public int Market;
        [DataMember(Name = "workshop")] public int Workshop;
        [DataMember(Name = "bridge")] public int Bridge;
        [DataMember(Name = "patrol")] public bool Patrol;
        [DataMember(Name = "since")] public double Since;
        [DataMember(Name = "siege")] public bool Siege;
    }

    [DataContract]
    internal sealed class DikozemieSiegePerson
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "fresh")] public bool Fresh;
    }

    [DataContract]
    internal sealed class DikozemieSiegeFight
    {
        [DataMember(Name = "guid")] public string Guid = string.Empty;
        [DataMember(Name = "humans")] public int Humans;
        [DataMember(Name = "sideLeader")] public string SideLeader = string.Empty;
        [DataMember(Name = "sideSize")] public int SideSize;
        [DataMember(Name = "sideRating")] public int SideRating;
        [DataMember(Name = "foeSize")] public int FoeSize;
        [DataMember(Name = "foeRating")] public int FoeRating;
        [DataMember(Name = "joinIn")] public double JoinIn;
        [DataMember(Name = "joined")] public bool Joined;
        [DataMember(Name = "inBattle")] public bool InBattle;
        [DataMember(Name = "port")] public int Port;
    }

    [DataContract]
    internal sealed class DikozemieSiege
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "node")] public string Node = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "kind")] public string Kind = string.Empty;
        [DataMember(Name = "state")] public string State = string.Empty;
        // "attack", "defense" or "" (somebody else's war).
        [DataMember(Name = "side")] public string Side = string.Empty;
        [DataMember(Name = "leader")] public string Leader = string.Empty;
        [DataMember(Name = "leaderName")] public string LeaderName = string.Empty;
        [DataMember(Name = "attacker")] public DikozemieClanName Attacker = new DikozemieClanName();
        [DataMember(Name = "defender")] public DikozemieClanName Defender = new DikozemieClanName();
        [DataMember(Name = "npc")] public bool Npc;
        [DataMember(Name = "kingdom")] public string Kingdom = string.Empty;
        [DataMember(Name = "assaultIn")] public double AssaultIn;
        [DataMember(Name = "attackers")] public List<DikozemieSiegePerson> Attackers = new List<DikozemieSiegePerson>();
        [DataMember(Name = "defenders")] public List<DikozemieSiegePerson> Defenders = new List<DikozemieSiegePerson>();
        [DataMember(Name = "joinedMe")] public bool JoinedMe;
        [DataMember(Name = "log")] public List<string> Log = new List<string>();
        [DataMember(Name = "outcome")] public string Outcome = string.Empty;
        [DataMember(Name = "warband")] public int Warband;
        [DataMember(Name = "garrison")] public int Garrison;
        [DataMember(Name = "fight")] public DikozemieSiegeFight Fight;
        internal DateTime ReceivedUtc = DateTime.UtcNow;

        internal bool Active => State == "camp" || State == "battle";
        internal bool Mine => !string.IsNullOrEmpty(Side);
        internal bool Attacking => Side == "attack";
        internal int AssaultLeft => Math.Max(0, (int)Math.Ceiling(AssaultIn - (DateTime.UtcNow - ReceivedUtc).TotalSeconds));
    }

    [DataContract]
    internal sealed class DikozemieClanInvite
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "clan")] public string Clan = string.Empty;
        [DataMember(Name = "clanName")] public string ClanName = string.Empty;
        [DataMember(Name = "banner")] public string Banner = string.Empty;
        [DataMember(Name = "from")] public string From = string.Empty;
        [DataMember(Name = "fromName")] public string FromName = string.Empty;
        [DataMember(Name = "at")] public double At;
    }

    [DataContract]
    internal sealed class DikozemieClanMember
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "rank")] public string Rank = string.Empty;
        [DataMember(Name = "rankName")] public string RankName = string.Empty;
        [DataMember(Name = "joined")] public double Joined;
        [DataMember(Name = "given")] public int Given;
        [DataMember(Name = "online")] public bool Online;
        [DataMember(Name = "where")] public string Where = string.Empty;
        [DataMember(Name = "rating")] public int Rating;
        [DataMember(Name = "seen")] public double Seen;
    }

    [DataContract]
    internal sealed class DikozemieClanApplication
    {
        [DataMember(Name = "player")] public string Player = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "at")] public double At;
        [DataMember(Name = "text")] public string Text = string.Empty;
        [DataMember(Name = "rating")] public int Rating;
    }

    [DataContract]
    internal sealed class DikozemieLandRates
    {
        [DataMember(Name = "market")] public int Market;
        [DataMember(Name = "workshop")] public int Workshop;
        [DataMember(Name = "bridge")] public int Bridge;
        [DataMember(Name = "patrol")] public bool Patrol;
    }

    [DataContract]
    internal sealed class DikozemieLandStats
    {
        [DataMember(Name = "today")] public int Today;
        [DataMember(Name = "yesterday")] public int Yesterday;
        [DataMember(Name = "total")] public int Total;
    }

    [DataContract]
    internal sealed class DikozemieClanHolding
    {
        [DataMember(Name = "node")] public string Node = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "kind")] public string Kind = string.Empty;
        [DataMember(Name = "since")] public double Since;
        [DataMember(Name = "garrison")] public Dictionary<string, int> Garrison = new Dictionary<string, int>();
        [DataMember(Name = "men")] public int Men;
        [DataMember(Name = "rates")] public DikozemieLandRates Rates = new DikozemieLandRates();
        [DataMember(Name = "patrol")] public bool Patrol;
        [DataMember(Name = "villages")] public int Villages;
        [DataMember(Name = "incomeDay")] public double IncomeDay;
        [DataMember(Name = "stats")] public DikozemieLandStats Stats = new DikozemieLandStats();
        [DataMember(Name = "calmIn")] public double CalmIn;
        [DataMember(Name = "emptyIn")] public double EmptyIn;
        [DataMember(Name = "siege")] public bool Siege;
        [DataMember(Name = "taken")] public string Taken = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieClanRelation
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "banner")] public string Banner = string.Empty;
        [DataMember(Name = "state")] public string State = string.Empty;
        [DataMember(Name = "until")] public double Until;
        [DataMember(Name = "since")] public double Since;
        [DataMember(Name = "offer")] public string Offer = string.Empty;
        [DataMember(Name = "members")] public int Members;
        [DataMember(Name = "holdings")] public int Holdings;
    }

    [DataContract]
    internal sealed class DikozemieClanLedgerRow
    {
        [DataMember(Name = "id")] public long Id;
        [DataMember(Name = "at")] public double At;
        [DataMember(Name = "amount")] public int Amount;
        [DataMember(Name = "balance")] public int Balance;
        [DataMember(Name = "kind")] public string Kind = string.Empty;
        [DataMember(Name = "title")] public string Title = string.Empty;
        [DataMember(Name = "note")] public string Note = string.Empty;
        [DataMember(Name = "player")] public string Player = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieClanLogLine
    {
        [DataMember(Name = "at")] public double At;
        [DataMember(Name = "text")] public string Text = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieClanFull
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "banner")] public string Banner = string.Empty;
        [DataMember(Name = "motto")] public string Motto = string.Empty;
        [DataMember(Name = "created")] public double Created;
        [DataMember(Name = "leader")] public string Leader = string.Empty;
        [DataMember(Name = "leaderName")] public string LeaderName = string.Empty;
        [DataMember(Name = "home")] public string Home = string.Empty;
        [DataMember(Name = "coins")] public int Coins;
        [DataMember(Name = "rank")] public string Rank = string.Empty;
        [DataMember(Name = "rankName")] public string RankName = string.Empty;
        [DataMember(Name = "rights")] public List<string> Rights = new List<string>();
        [DataMember(Name = "members")] public List<DikozemieClanMember> Members = new List<DikozemieClanMember>();
        [DataMember(Name = "applications")] public List<DikozemieClanApplication> Applications = new List<DikozemieClanApplication>();
        [DataMember(Name = "troops")] public Dictionary<string, int> Troops = new Dictionary<string, int>();
        [DataMember(Name = "holdings")] public List<DikozemieClanHolding> Holdings = new List<DikozemieClanHolding>();
        [DataMember(Name = "maxHoldings")] public int MaxHoldings;
        [DataMember(Name = "wagesDay")] public double WagesDay;
        [DataMember(Name = "incomeDay")] public double IncomeDay;
        [DataMember(Name = "relations")] public List<DikozemieClanRelation> Relations = new List<DikozemieClanRelation>();
        [DataMember(Name = "ledger")] public List<DikozemieClanLedgerRow> Ledger = new List<DikozemieClanLedgerRow>();
        [DataMember(Name = "log")] public List<DikozemieClanLogLine> Log = new List<DikozemieClanLogLine>();
    }

    [DataContract]
    internal sealed class DikozemieTroopType
    {
        [DataMember(Name = "type")] public string Type = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "plural")] public string Plural = string.Empty;
        [DataMember(Name = "wage")] public int Wage;
        [DataMember(Name = "fame")] public int Fame;
        [DataMember(Name = "about")] public string About = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieClanRules
    {
        [DataMember(Name = "foundCost")] public int FoundCost = 100;
        [DataMember(Name = "bannerCost")] public int BannerCost = 20;
        [DataMember(Name = "siegeCost")] public int SiegeCost = 20;
        [DataMember(Name = "maxMembers")] public int MaxMembers = 30;
        [DataMember(Name = "marketMax")] public int MarketMax = 10;
        [DataMember(Name = "workshopMax")] public int WorkshopMax = 20;
        [DataMember(Name = "bridgeMax")] public int BridgeMax = 3;
        [DataMember(Name = "patrolCost")] public int PatrolCost = 4;
        [DataMember(Name = "truceHours")] public double TruceHours = 2;
        [DataMember(Name = "garrisonMax")] public int GarrisonMax = 20;
        [DataMember(Name = "warbandMax")] public int WarbandMax = 30;
        [DataMember(Name = "hireDays")] public double HireDays = 2;
        [DataMember(Name = "abandonMinutes")] public double AbandonMinutes = 60;
    }

    [DataContract]
    internal sealed class DikozemieClanState
    {
        [DataMember(Name = "ok")] public bool Ok;
        [DataMember(Name = "code")] public string Code = string.Empty;
        [DataMember(Name = "message")] public string Message = string.Empty;
        [DataMember(Name = "serverTime")] public double ServerTime;
        [DataMember(Name = "coins")] public int Coins;
        [DataMember(Name = "clan")] public DikozemieClanFull Clan;
        [DataMember(Name = "invites")] public List<DikozemieClanInvite> Invites = new List<DikozemieClanInvite>();
        [DataMember(Name = "applied")] public List<string> Applied = new List<string>();
        [DataMember(Name = "troopTypes")] public List<DikozemieTroopType> TroopTypes = new List<DikozemieTroopType>();
        [DataMember(Name = "rankNames")] public Dictionary<string, string> RankNames = new Dictionary<string, string>();
        [DataMember(Name = "rules")] public DikozemieClanRules Rules = new DikozemieClanRules();
        [DataMember(Name = "rev")] public long Rev;
        // The answer of a siege action.
        [DataMember(Name = "siege")] public DikozemieSiege Siege;

        internal bool IsFull => TroopTypes != null && TroopTypes.Count > 0;
    }

    [DataContract]
    internal sealed class DikozemieClanListRow
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "banner")] public string Banner = string.Empty;
        [DataMember(Name = "motto")] public string Motto = string.Empty;
        [DataMember(Name = "leaderName")] public string LeaderName = string.Empty;
        [DataMember(Name = "members")] public int Members;
        [DataMember(Name = "online")] public int Online;
        [DataMember(Name = "holdings")] public List<string> Holdings = new List<string>();
        [DataMember(Name = "created")] public double Created;
        [DataMember(Name = "relation")] public string Relation = string.Empty;
        [DataMember(Name = "applied")] public bool Applied;
    }

    [DataContract]
    internal sealed class DikozemieClanList
    {
        [DataMember(Name = "ok")] public bool Ok;
        [DataMember(Name = "clans")] public List<DikozemieClanListRow> Clans = new List<DikozemieClanListRow>();
        [DataMember(Name = "mine")] public string Mine = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieMercRow
    {
        [DataMember(Name = "type")] public string Type = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "plural")] public string Plural = string.Empty;
        [DataMember(Name = "wage")] public int Wage;
        [DataMember(Name = "fame")] public int Fame;
        [DataMember(Name = "left")] public int Left;
        [DataMember(Name = "about")] public string About = string.Empty;
        [DataMember(Name = "advance")] public int Advance;
        [DataMember(Name = "allowed")] public bool Allowed;
    }

    [DataContract]
    internal sealed class DikozemieMercs
    {
        [DataMember(Name = "ok")] public bool Ok;
        [DataMember(Name = "message")] public string Message = string.Empty;
        [DataMember(Name = "town")] public string Town = string.Empty;
        [DataMember(Name = "rows")] public List<DikozemieMercRow> Rows = new List<DikozemieMercRow>();
        [DataMember(Name = "rating")] public int Rating;
        [DataMember(Name = "coins")] public int Coins;
        [DataMember(Name = "clan")] public string Clan = string.Empty;
        [DataMember(Name = "warband")] public Dictionary<string, int> Warband = new Dictionary<string, int>();
        [DataMember(Name = "max")] public int Max;
    }

    [DataContract]
    internal sealed class DikozemieLandView
    {
        [DataMember(Name = "ok")] public bool Ok;
        [DataMember(Name = "message")] public string Message = string.Empty;
        [DataMember(Name = "node")] public string Node = string.Empty;
        [DataMember(Name = "seat")] public string Seat = string.Empty;
        [DataMember(Name = "seatName")] public string SeatName = string.Empty;
        [DataMember(Name = "kind")] public string Kind = string.Empty;
        [DataMember(Name = "seatKind")] public string SeatKind = string.Empty;
        [DataMember(Name = "owner")] public DikozemieClanName Owner;
        [DataMember(Name = "lord")] public string Lord = string.Empty;
        [DataMember(Name = "kingdom")] public string Kingdom = string.Empty;
        [DataMember(Name = "rates")] public DikozemieLandRates Rates = new DikozemieLandRates();
        [DataMember(Name = "villages")] public int Villages;
        [DataMember(Name = "incomeDay")] public double IncomeDay;
        [DataMember(Name = "since")] public double Since;
        [DataMember(Name = "taken")] public string Taken = string.Empty;
        [DataMember(Name = "calmIn")] public double CalmIn;
        [DataMember(Name = "garrison")] public int Garrison;
        [DataMember(Name = "mine")] public bool Mine;
        [DataMember(Name = "troops")] public Dictionary<string, int> Troops = new Dictionary<string, int>();
        [DataMember(Name = "stats")] public DikozemieLandStats Stats = new DikozemieLandStats();
        [DataMember(Name = "emptyIn")] public double EmptyIn;
        [DataMember(Name = "relation")] public string Relation = string.Empty;
        [DataMember(Name = "siege")] public DikozemieSiege Siege;
        [DataMember(Name = "siegeBlock")] public string SiegeBlock = string.Empty;
        [DataMember(Name = "siegeRule")] public string SiegeRule = string.Empty;
        internal DateTime ReceivedUtc = DateTime.UtcNow;
    }

    internal static class DikozemieClans
    {
        // ---- what the world pulse brought --------------------------------

        private static readonly List<DikozemieLand> NoLands = new List<DikozemieLand>();
        private static readonly List<DikozemieSiege> NoSieges = new List<DikozemieSiege>();

        internal static DikozemieClanBrief Mine => DikozemieWorld.View?.Clan;

        internal static string MyClanId => Mine?.Id ?? string.Empty;

        internal static bool InClan => !string.IsNullOrEmpty(MyClanId);

        internal static IReadOnlyList<DikozemieLand> Lands =>
            (IReadOnlyList<DikozemieLand>)DikozemieWorld.View?.Lands ?? NoLands;

        internal static IReadOnlyList<DikozemieSiege> Sieges =>
            (IReadOnlyList<DikozemieSiege>)DikozemieWorld.View?.Sieges ?? NoSieges;

        internal static DikozemieLand LandOf(string seat) =>
            string.IsNullOrEmpty(seat) ? null : Lands.FirstOrDefault(l => l.Node == seat);

        // Whose land a settlement is: a village gives its castle or town.
        internal static string SeatOf(DikozemieLobbyMapNode node)
        {
            if (node == null)
            {
                return string.Empty;
            }
            if (DikozemieTown.IsVillage(node))
            {
                string bound = DikozemieTownCatalog.Get(node.Id).Bound;
                return string.IsNullOrEmpty(bound) ? node.Id : bound;
            }
            return node.Id;
        }

        // The castle or town holding the land at an atlas point.
        internal static string SeatAt(float x, float y) =>
            SeatOf(DikozemieLobbyMapCatalog.FindNearestNode(x, y));

        internal static DikozemieLand LandAt(float x, float y) => LandOf(SeatAt(x, y));

        internal static bool IsMyLand(DikozemieLand land) =>
            land != null && InClan && land.Clan == MyClanId;

        internal static bool AtWarWith(string clan) =>
            !string.IsNullOrEmpty(clan) && Mine?.Wars?.Contains(clan) == true;

        internal static string ClanName(string clan) =>
            !string.IsNullOrEmpty(clan) && DikozemieWorld.View?.Clans != null &&
            DikozemieWorld.View.Clans.TryGetValue(clan, out DikozemieClanName name) ? name.Name : string.Empty;

        internal static string ClanBanner(string clan) =>
            !string.IsNullOrEmpty(clan) && DikozemieWorld.View?.Clans != null &&
            DikozemieWorld.View.Clans.TryGetValue(clan, out DikozemieClanName name) ? name.Banner : string.Empty;

        internal static DikozemieSiege SiegeAt(string seat) =>
            Sieges.FirstOrDefault(s => s.Node == seat && s.Active);

        // The siege the hero takes part in or his clan wages or suffers.
        internal static DikozemieSiege MySiege =>
            Sieges.Where(s => s.Mine && s.Active).OrderByDescending(s => s.State == "battle")
                .ThenByDescending(s => s.JoinedMe).FirstOrDefault();

        // The banner over a settlement: its clan's, or its singleplayer lord's.
        internal static string BannerOf(DikozemieLobbyMapNode node)
        {
            DikozemieLand land = LandOf(SeatOf(node));
            if (land != null)
            {
                return land.Banner;
            }
            return node == null ? string.Empty : DikozemieTownCatalog.Get(node.Id).Banner ?? string.Empty;
        }

        internal static string OwnerLine(DikozemieLobbyMapNode node)
        {
            string seat = SeatOf(node);
            DikozemieLand land = LandOf(seat);
            if (land == null)
            {
                return string.Empty;
            }
            bool village = DikozemieTown.IsVillage(node);
            return (village ? "Платит оброк клану игроков «" : "Владеет клан игроков «") + land.Name + "»" +
                (village ? " · " + DikozemieWorld.PlaceName(seat) : string.Empty) +
                (IsMyLand(land) ? " — ваш клан" : AtWarWith(land.Clan) ? " — вы воюете" : string.Empty) +
                (land.Siege ? " · ОСАДА" : string.Empty);
        }

        // ---- the full state of the hero's clan -------------------------------

        internal static DikozemieClanState State { get; private set; }
        internal static int Revision { get; private set; }
        internal static DikozemieClanList List { get; private set; }
        internal static string LastError { get; private set; } = string.Empty;
        private static long _seenRev = -1;
        private static string _seenClan = string.Empty;
        private static bool _stateInFlight;
        private static DateTime _stateAskedUtc = DateTime.MinValue;
        private static readonly HashSet<string> InvitesShown = new HashSet<string>(StringComparer.Ordinal);

        internal static DikozemieClanFull Clan => State?.Clan;

        internal static DikozemieClanRules Rules => State?.Rules ?? new DikozemieClanRules();

        internal static bool Can(string right) => Clan?.Rights?.Contains(right) == true;

        internal static IReadOnlyList<DikozemieTroopType> TroopTypes =>
            (IReadOnlyList<DikozemieTroopType>)State?.TroopTypes ?? Array.Empty<DikozemieTroopType>();

        internal static DikozemieTroopType Troop(string type) =>
            TroopTypes.FirstOrDefault(t => t.Type == type);

        internal static string TroopsWords(Dictionary<string, int> troops)
        {
            if (troops == null)
            {
                return "никого";
            }
            var parts = new List<string>();
            foreach (DikozemieTroopType type in TroopTypes)
            {
                if (troops.TryGetValue(type.Type, out int count) && count > 0)
                {
                    parts.Add(type.Plural + " " + count);
                }
            }
            return parts.Count == 0 ? "никого" : string.Join(", ", parts);
        }

        internal static int Total(Dictionary<string, int> troops) =>
            troops == null ? 0 : troops.Values.Where(v => v > 0).Sum();

        // Every world pulse: the clan changed on the server, ask for it.
        internal static void OnPulse(DikozemieWorldView view)
        {
            string clan = view?.Clan?.Id ?? string.Empty;
            long rev = view?.Clan?.Rev ?? -1;
            if (clan != _seenClan || clan.Length > 0 && rev != _seenRev || State == null && IsScreenOpen)
            {
                _seenClan = clan;
                _seenRev = rev;
                Refresh();
            }
            ShowInvites(view?.ClanInvites);
        }

        internal static void Refresh()
        {
            if (!DikozemieEconomy.CanAsk)
            {
                return;
            }
            if (_stateInFlight && (DateTime.UtcNow - _stateAskedUtc).TotalSeconds < 12)
            {
                return;
            }
            _stateInFlight = true;
            _stateAskedUtc = DateTime.UtcNow;
            DikozemieEconomy.Request(HttpMethod.Get, "clan", null, (status, raw, error) =>
            {
                _stateInFlight = false;
                DikozemieClanState state = status == 200 ? DikozemieEconomy.ParseJson<DikozemieClanState>(raw) : null;
                if (state == null || !state.Ok)
                {
                    LastError = DikozemieEconomy.ParseJson<DikozemieClanState>(raw)?.Message ?? error ?? string.Empty;
                    return;
                }
                Accept(state);
            });
        }

        private static void Accept(DikozemieClanState state)
        {
            State = state;
            LastError = string.Empty;
            Revision++;
        }

        internal static void RefreshList(Action done = null)
        {
            if (!DikozemieEconomy.CanAsk)
            {
                return;
            }
            DikozemieEconomy.Request(HttpMethod.Get, "clan/list", null, (status, raw, error) =>
            {
                DikozemieClanList list = status == 200 ? DikozemieEconomy.ParseJson<DikozemieClanList>(raw) : null;
                if (list != null && list.Ok)
                {
                    List = list;
                    Revision++;
                }
                done?.Invoke();
            });
        }

        // A clan action: the answer is the clan's new state (or a siege).
        internal static void Act(string path, string body, Action<DikozemieClanState, string> done)
        {
            if (!DikozemieEconomy.CanAsk)
            {
                done?.Invoke(null, DikozemieEconomy.StatusText);
                return;
            }
            DikozemieLobbyShellIntegration.TraceButtonAction("clan act " + path);
            DikozemieEconomy.Request(HttpMethod.Post, "clan/" + path, body ?? "{}", (status, raw, error) =>
            {
                DikozemieClanState answer = DikozemieEconomy.ParseJson<DikozemieClanState>(raw);
                DikozemieWorld.PulseSoon();
                if (status == 200 && answer != null && answer.Ok)
                {
                    if (answer.IsFull)
                    {
                        Accept(answer);
                    }
                    else
                    {
                        Refresh();
                    }
                    // Coins of the hero may have moved (a charter, a deposit).
                    DikozemieEconomy.RequestRefresh();
                    done?.Invoke(answer, null);
                    return;
                }
                string message = answer?.Message;
                if (string.IsNullOrEmpty(message))
                {
                    message = error == null ? "Казна не ответила." : "Нет связи с казной: " + error;
                }
                DikozemieLobbyShellIntegration.TraceButtonAction("clan act failed " + path + " status=" + status + " " + message);
                done?.Invoke(null, message);
            });
        }

        internal static void Land(string node, Action<DikozemieLandView, string> done)
        {
            if (!DikozemieEconomy.CanAsk || string.IsNullOrEmpty(node))
            {
                done?.Invoke(null, DikozemieEconomy.StatusText);
                return;
            }
            DikozemieEconomy.Request(HttpMethod.Get, "land?node=" + Uri.EscapeDataString(node), null, (status, raw, error) =>
            {
                DikozemieLandView view = DikozemieEconomy.ParseJson<DikozemieLandView>(raw);
                if (status == 200 && view != null && view.Ok)
                {
                    view.ReceivedUtc = DateTime.UtcNow;
                    done?.Invoke(view, null);
                    return;
                }
                done?.Invoke(null, view?.Message ?? error ?? "Казна не ответила.");
            });
        }

        internal static void Mercs(string town, Action<DikozemieMercs, string> done)
        {
            if (!DikozemieEconomy.CanAsk)
            {
                done?.Invoke(null, DikozemieEconomy.StatusText);
                return;
            }
            DikozemieEconomy.Request(HttpMethod.Get, "clan/mercs?at=" + Uri.EscapeDataString(town ?? string.Empty), null,
                (status, raw, error) =>
                {
                    DikozemieMercs mercs = DikozemieEconomy.ParseJson<DikozemieMercs>(raw);
                    if (status == 200 && mercs != null && mercs.Ok)
                    {
                        done?.Invoke(mercs, null);
                        return;
                    }
                    done?.Invoke(null, mercs?.Message ?? error ?? "Казна не ответила.");
                });
        }

        // ---- the clan screen ---------------------------------------------------

        private static bool _screenRequest;

        internal static bool IsScreenOpen { get; set; }

        internal static void RequestScreen()
        {
            _screenRequest = true;
            DikozemieLobbyShellIntegration.OpenMap(false);
        }

        internal static bool ConsumeScreenRequest()
        {
            bool request = _screenRequest;
            _screenRequest = false;
            return request;
        }

        // ---- invitations ----------------------------------------------------------

        private static void ShowInvites(List<DikozemieClanInvite> invites)
        {
            if (invites == null || InformationManager.IsAnyInquiryActive() ||
                TaleWorlds.MountAndBlade.Mission.Current != null)
            {
                return;
            }
            DikozemieClanInvite invite = invites.FirstOrDefault(i => !InvitesShown.Contains(i.Id));
            if (invite == null)
            {
                return;
            }
            InvitesShown.Add(invite.Id);
            string id = invite.Id;
            InformationManager.ShowInquiry(new InquiryData(
                "ПРИГЛАШЕНИЕ В КЛАН",
                invite.FromName + " зовёт вас в клан «" + invite.ClanName + "».\n\n" +
                "Клан — это общее знамя, общая казна и общие крепости. Его люди держат землю, берут пошлины, " +
                "нанимают воинов и идут на штурм вместе. Новичок клана вносит монеты в казну и встаёт в осадный " +
                "лагерь; звание даёт глава.",
                true, true, "ВСТУПИТЬ", "ОТКАЗАТЬСЯ",
                () => Act("accept", "{\"invite\":" + DikozemieEconomy.Json(id) + "}", (state, error) =>
                    InformationManager.DisplayMessage(new InformationMessage(
                        error ?? "Вы теперь в клане «" + invite.ClanName + "».", new Color(0.62f, 0.86f, 0.62f)))),
                () => Act("decline", "{\"invite\":" + DikozemieEconomy.Json(id) + "}", null)), true, true);
        }

        // ---- words -----------------------------------------------------------------

        internal static string RelationWord(string state)
        {
            switch (state)
            {
                case "war": return "ВОЙНА";
                case "truce": return "перемирие";
                case "own": return "ваш клан";
                default: return "мир";
            }
        }

        internal static string Minutes(double seconds)
        {
            int total = Math.Max(0, (int)Math.Ceiling(seconds));
            return total >= 60
                ? (total / 60) + ":" + (total % 60).ToString("00", CultureInfo.InvariantCulture)
                : total + " с";
        }

        internal static string ClansWord(int count)
        {
            int last = count % 10;
            int lastTwo = count % 100;
            string word = lastTwo >= 11 && lastTwo <= 14 ? "кланов" : last == 1 ? "клан" : last >= 2 && last <= 4 ? "клана" : "кланов";
            return count + " " + word;
        }

        internal static string Date(double at)
        {
            DateTime when = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(at).ToLocalTime();
            return when.ToString("dd.MM HH:mm", CultureInfo.InvariantCulture);
        }
    }

    // Banners as the game draws them: a field, a pattern and a charge, all
    // from the game's own banner catalogue (BannerManager), so a clan's banner
    // looks like a lord's of the singleplayer game. Codes are the game's own.
    internal static class DikozemieBannerKit
    {
        internal sealed class Parts
        {
            internal int Background = 11;
            internal int FieldColor = 116;
            internal int PatternColor = 116;
            internal int Icon = 133;
            internal int IconColor = 121;
            internal int IconSize = 560;
        }

        private static List<int> _backgrounds;
        private static List<int> _icons;
        private static List<int> _fieldColors;
        private static List<int> _iconColors;
        private static int _plain = 11;
        private static readonly Dictionary<int, uint> ColorValues = new Dictionary<int, uint>();

        // A sample from the singleplayer clans: used if the game's catalogue
        // is not there in the multiplayer client.
        private static readonly int[] FallbackBackgrounds = { 11, 24, 35, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 13, 14, 15, 16 };
        private static readonly int[] FallbackIcons = { 133, 161, 212, 446, 309, 510, 101, 102, 103, 104, 105, 106, 107,
            108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118 };
        private static readonly int[] FallbackColors = { 1, 4, 6, 9, 12, 14, 19, 22, 40, 75, 116, 118, 119, 120, 121,
            156, 163, 166, 171, 193 };

        private static void Load()
        {
            if (_backgrounds != null)
            {
                return;
            }
            _backgrounds = new List<int>();
            _icons = new List<int>();
            _fieldColors = new List<int>();
            _iconColors = new List<int>();
            try
            {
                BannerManager manager = BannerManager.Instance;
                if (manager != null)
                {
                    foreach (BannerIconGroup group in manager.BannerIconGroups)
                    {
                        if (group.IsPattern)
                        {
                            _backgrounds.AddRange(group.AllBackgrounds.Keys);
                        }
                        else
                        {
                            _icons.AddRange(group.AvailableIcons.Keys);
                        }
                    }
                    _plain = manager.BaseBackgroundId > 0 ? manager.BaseBackgroundId : _plain;
                    // The whole palette, one entry a colour: the game lets a
                    // player choose from eight, a clan of this world from all.
                    var seen = new HashSet<uint>();
                    foreach (KeyValuePair<int, BannerColor> pair in manager.ReadOnlyColorPalette.OrderBy(p => p.Key))
                    {
                        ColorValues[pair.Key] = pair.Value.Color;
                        if (seen.Add(pair.Value.Color & 0xFFFFFF))
                        {
                            _fieldColors.Add(pair.Key);
                            _iconColors.Add(pair.Key);
                        }
                    }
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: banner catalogue failed: " + error.Message);
            }
            if (_backgrounds.Count == 0)
            {
                _backgrounds.AddRange(FallbackBackgrounds);
            }
            if (_icons.Count == 0)
            {
                _icons.AddRange(FallbackIcons);
            }
            if (_fieldColors.Count == 0)
            {
                _fieldColors.AddRange(FallbackColors);
            }
            if (_iconColors.Count == 0)
            {
                _iconColors.AddRange(FallbackColors);
            }
            _backgrounds = _backgrounds.Distinct().ToList();
            _icons = _icons.Distinct().ToList();
            _fieldColors = Distinct(_fieldColors).OrderBy(Hue).ToList();
            _iconColors = Distinct(_iconColors).OrderBy(Hue).ToList();
            DikozemieLobbyShellIntegration.TraceButtonAction("banner kit backgrounds=" + _backgrounds.Count +
                " icons=" + _icons.Count + " fieldColors=" + _fieldColors.Count + " iconColors=" + _iconColors.Count +
                " plain=" + _plain);
        }

        // The palette has dozens of whites a shade apart: keep the colours
        // an eye tells apart.
        private static List<int> Distinct(List<int> ids)
        {
            var kept = new List<int>();
            foreach (int id in ids.Distinct())
            {
                Color c = ColorOf(id);
                bool near = kept.Any(other =>
                {
                    Color o = ColorOf(other);
                    float dr = c.Red - o.Red;
                    float dg = c.Green - o.Green;
                    float db = c.Blue - o.Blue;
                    return dr * dr * 0.3f + dg * dg * 0.59f + db * db * 0.11f < 0.0035f;
                });
                if (!near)
                {
                    kept.Add(id);
                }
            }
            return kept;
        }

        // Greys first (dark to light), then the colours round the wheel.
        private static double Hue(int id)
        {
            Color c = ColorOf(id);
            float max = Math.Max(c.Red, Math.Max(c.Green, c.Blue));
            float min = Math.Min(c.Red, Math.Min(c.Green, c.Blue));
            float light = (max + min) * 0.5f;
            if (max - min < 0.08f)
            {
                return light;
            }
            float hue;
            if (max == c.Red)
            {
                hue = ((c.Green - c.Blue) / (max - min) + 6f) % 6f;
            }
            else if (max == c.Green)
            {
                hue = (c.Blue - c.Red) / (max - min) + 2f;
            }
            else
            {
                hue = (c.Red - c.Green) / (max - min) + 4f;
            }
            return 1.0 + Math.Floor(hue * 4f) + light;
        }

        internal static float Lightness(int id)
        {
            Color c = ColorOf(id);
            return 0.3f * c.Red + 0.59f * c.Green + 0.11f * c.Blue;
        }

        // A plain field: the picker draws the charges on it.
        internal static int Plain { get { Load(); return _plain; } }

        internal static IReadOnlyList<int> Backgrounds { get { Load(); return _backgrounds; } }
        internal static IReadOnlyList<int> Icons { get { Load(); return _icons; } }
        internal static IReadOnlyList<int> FieldColors { get { Load(); return _fieldColors; } }
        internal static IReadOnlyList<int> IconColors { get { Load(); return _iconColors; } }

        // The colour of a palette entry, for the swatches (0xAARRGGBB).
        internal static Color ColorOf(int id)
        {
            Load();
            if (!ColorValues.TryGetValue(id, out uint value))
            {
                return new Color(0.5f, 0.5f, 0.5f, 1f);
            }
            return new Color(((value >> 16) & 0xFF) / 255f, ((value >> 8) & 0xFF) / 255f, (value & 0xFF) / 255f, 1f);
        }

        internal static string Code(Parts parts) =>
            string.Join(".", new[]
            {
                parts.Background, parts.FieldColor, parts.PatternColor, 1528, 1528, 764, 764, 1, 0, 0,
                parts.Icon, parts.IconColor, parts.IconColor, parts.IconSize, parts.IconSize, 764, 764, 0, 0, 0
            }.Select(n => n.ToString(CultureInfo.InvariantCulture)));

        // A field alone, with no charge.
        internal static string FieldCode(int background, int field, int pattern) =>
            string.Join(".", new[] { background, field, pattern, 1528, 1528, 764, 764, 1, 0, 0 }
                .Select(n => n.ToString(CultureInfo.InvariantCulture)));

        internal static Parts Read(string code)
        {
            var parts = new Parts();
            string[] numbers = (code ?? string.Empty).Split('.');
            int Get(int index, int fallback) =>
                index < numbers.Length && int.TryParse(numbers[index], out int value) ? value : fallback;
            parts.Background = Get(0, parts.Background);
            parts.FieldColor = Get(1, parts.FieldColor);
            parts.PatternColor = Get(2, parts.PatternColor);
            parts.Icon = Get(10, parts.Icon);
            parts.IconColor = Get(11, parts.IconColor);
            parts.IconSize = Math.Max(300, Math.Min(700, Get(13, parts.IconSize)));
            return parts;
        }

        internal static Parts Roll(int seed)
        {
            Load();
            var random = new Random(seed);
            var parts = new Parts
            {
                // Half of the banners are plain, as most lords' are.
                Background = random.Next(2) == 0 ? _plain : _backgrounds[random.Next(Math.Min(_backgrounds.Count, 40))],
                Icon = _icons[random.Next(_icons.Count)],
                IconSize = 560
            };
            parts.FieldColor = _fieldColors[random.Next(_fieldColors.Count)];
            parts.PatternColor = _fieldColors[random.Next(_fieldColors.Count)];
            // A charge that stands out from the field.
            for (int tries = 0; tries < 40; tries++)
            {
                parts.IconColor = _iconColors[random.Next(_iconColors.Count)];
                if (Math.Abs(Lightness(parts.IconColor) - Lightness(parts.FieldColor)) > 0.35f)
                {
                    break;
                }
            }
            return parts;
        }
    }
}
