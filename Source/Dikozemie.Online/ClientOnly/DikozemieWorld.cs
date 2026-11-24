using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Dikozemie.Online
{
    // Stage 5 of the plan, «Общий мир» (customer's plan 2026-11-05/08): other
    // heroes on the map, meetings on the roads, gangers and pirates, the
    // tavern's talk, parties. The trade server (EconomyServer) keeps who is
    // where and decides everything between players; this file tells it where
    // the hero stands and keeps its last answer for the map, the town and
    // the walk to draw.

    [DataContract]
    internal sealed class DikozemieWorldWalk
    {
        [DataMember(Name = "scene")] public string Scene = string.Empty;
        [DataMember(Name = "x")] public float X;
        [DataMember(Name = "y")] public float Y;
        [DataMember(Name = "z")] public float Z;
        [DataMember(Name = "dir")] public float Dir;
        [DataMember(Name = "act")] public int Act;
    }

    [DataContract]
    internal sealed class DikozemieWorldPlayer
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "x")] public float X;
        [DataMember(Name = "y")] public float Y;
        [DataMember(Name = "age")] public float Age;
        [DataMember(Name = "boat")] public bool Boat;
        [DataMember(Name = "vessel")] public int Vessel;
        [DataMember(Name = "mounted")] public bool Mounted;
        [DataMember(Name = "node")] public string Node = string.Empty;
        [DataMember(Name = "in")] public string In = string.Empty;
        [DataMember(Name = "place")] public string Place = string.Empty;
        [DataMember(Name = "section")] public string Section = string.Empty;
        [DataMember(Name = "moving")] public bool Moving;
        [DataMember(Name = "tx")] public float Tx;
        [DataMember(Name = "ty")] public float Ty;
        [DataMember(Name = "speed")] public float Speed;
        [DataMember(Name = "culture")] public string Culture = string.Empty;
        [DataMember(Name = "rating")] public int Rating;
        [DataMember(Name = "level")] public int Level;
        [DataMember(Name = "wound")] public int Wound;
        [DataMember(Name = "escort")] public int Escort;
        [DataMember(Name = "load")] public int Load;
        [DataMember(Name = "capacity")] public int Capacity;
        [DataMember(Name = "party")] public string Party = string.Empty;
        [DataMember(Name = "leader")] public bool Leader;
        [DataMember(Name = "look")] public string Look = string.Empty;
        [DataMember(Name = "outlaw")] public bool Outlaw;
        [DataMember(Name = "busy")] public bool Busy;
        [DataMember(Name = "guard")] public string Guard = string.Empty;
        [DataMember(Name = "walk")] public DikozemieWorldWalk Walk;
        // Stage 6: the hero's clan (its name and banner are in the view's clans).
        [DataMember(Name = "clan")] public string Clan = string.Empty;
        internal DateTime ReceivedUtc;
    }

    [DataContract]
    internal sealed class DikozemieWorldGoods
    {
        [DataMember(Name = "good")] public string Good = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "qty")] public int Qty;
    }

    [DataContract]
    internal sealed class DikozemieWorldConvoy
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "kind")] public string Kind = string.Empty;
        [DataMember(Name = "npc")] public bool Npc;
        [DataMember(Name = "owner")] public string Owner = string.Empty;
        [DataMember(Name = "ownerName")] public string OwnerName = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "master")] public string Master = string.Empty;
        [DataMember(Name = "guards")] public int Guards;
        [DataMember(Name = "from")] public string From = string.Empty;
        [DataMember(Name = "to")] public string To = string.Empty;
        [DataMember(Name = "leaves")] public double Leaves;
        [DataMember(Name = "travel")] public double Travel;
        [DataMember(Name = "cargo")] public List<DikozemieWorldGoods> Cargo = new List<DikozemieWorldGoods>();
        [DataMember(Name = "load")] public int Load;
        [DataMember(Name = "value")] public int Value;
        [DataMember(Name = "beasts")] public int Beasts;
        [DataMember(Name = "raided")] public bool Raided;
        [DataMember(Name = "x")] public float X;
        [DataMember(Name = "y")] public float Y;

        internal bool IsShip => Kind == "ship";
    }

    [DataContract]
    internal sealed class DikozemieWorldMember
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "fresh")] public bool Fresh;
        [DataMember(Name = "place")] public string Place = string.Empty;
        [DataMember(Name = "in")] public string In = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieWorldCard
    {
        [DataMember(Name = "kind")] public string Kind = string.Empty;
        [DataMember(Name = "seed")] public int Seed;
        [DataMember(Name = "culture")] public string Culture = string.Empty;
        [DataMember(Name = "forest")] public bool Forest;
        [DataMember(Name = "hills")] public bool Hills;
        [DataMember(Name = "coast")] public bool Coast;
        [DataMember(Name = "night")] public bool Night;
        [DataMember(Name = "title")] public string Title = string.Empty;
        [DataMember(Name = "foes")] public int Foes;
        [DataMember(Name = "x")] public float X;
        [DataMember(Name = "y")] public float Y;
    }

    [DataContract]
    internal sealed class DikozemieWorldPartyBattle
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "at")] public double At;
        [DataMember(Name = "members")] public List<string> Members = new List<string>();
        [DataMember(Name = "venue")] public string Venue = string.Empty;
        [DataMember(Name = "teamSize")] public int TeamSize;
        [DataMember(Name = "card")] public DikozemieWorldCard Card;
    }

    [DataContract]
    internal sealed class DikozemieWorldParty
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "leader")] public string Leader = string.Empty;
        [DataMember(Name = "since")] public double Since;
        [DataMember(Name = "members")] public List<DikozemieWorldMember> Members = new List<DikozemieWorldMember>();
        [DataMember(Name = "battle")] public DikozemieWorldPartyBattle Battle;
    }

    [DataContract]
    internal sealed class DikozemieWorldInvite
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "from")] public string From = string.Empty;
        [DataMember(Name = "fromName")] public string FromName = string.Empty;
        [DataMember(Name = "party")] public string Party = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieWorldPerson
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "look")] public string Look = string.Empty;
        [DataMember(Name = "rating")] public int Rating;
        [DataMember(Name = "mounted")] public bool Mounted;
        [DataMember(Name = "culture")] public string Culture = string.Empty;
        [DataMember(Name = "boat")] public bool Boat;
        [DataMember(Name = "vessel")] public int Vessel;
    }

    [DataContract]
    internal sealed class DikozemieWorldFight
    {
        [DataMember(Name = "mode")] public string Mode = string.Empty;
        [DataMember(Name = "guid")] public string Guid = string.Empty;
        [DataMember(Name = "size")] public int Size;
        [DataMember(Name = "humans")] public int Humans;
        [DataMember(Name = "opponent")] public string Opponent = string.Empty;
        [DataMember(Name = "sideLeader")] public string SideLeader = string.Empty;
        [DataMember(Name = "joinIn")] public float JoinIn;
        [DataMember(Name = "joined")] public bool Joined;
        [DataMember(Name = "port")] public int Port;
    }

    [DataContract]
    internal sealed class DikozemieWorldMeeting
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "state")] public string State = string.Empty;
        [DataMember(Name = "kind")] public string Kind = string.Empty;
        [DataMember(Name = "x")] public float X;
        [DataMember(Name = "y")] public float Y;
        [DataMember(Name = "side")] public string Side = string.Empty;
        [DataMember(Name = "leader")] public bool Leader;
        [DataMember(Name = "leaderName")] public string LeaderName = string.Empty;
        [DataMember(Name = "us")] public List<DikozemieWorldPerson> Us = new List<DikozemieWorldPerson>();
        [DataMember(Name = "them")] public List<DikozemieWorldPerson> Them = new List<DikozemieWorldPerson>();
        [DataMember(Name = "answerIn")] public float AnswerIn;
        [DataMember(Name = "demandCoins")] public int DemandCoins;
        [DataMember(Name = "demandCargo")] public List<DikozemieWorldGoods> DemandCargo = new List<DikozemieWorldGoods>();
        [DataMember(Name = "fleeChance")] public int FleeChance;
        [DataMember(Name = "cornered")] public bool Cornered;
        [DataMember(Name = "log")] public List<string> Log = new List<string>();
        [DataMember(Name = "fight")] public DikozemieWorldFight Fight;
        [DataMember(Name = "winner")] public string Winner = string.Empty;
        [DataMember(Name = "decideIn")] public float DecideIn;
        [DataMember(Name = "outcome")] public string Outcome = string.Empty;
        [DataMember(Name = "doneIn")] public float DoneIn;
        internal DateTime ReceivedUtc;

        internal bool IsAttacker => Side == "attacker";
        internal bool AtSea => Kind == "sea";
    }

    [DataContract]
    internal sealed class DikozemieWorldRaid
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "convoy")] public string Convoy = string.Empty;
        [DataMember(Name = "kind")] public string Kind = string.Empty;
        [DataMember(Name = "npc")] public bool Npc;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "master")] public string Master = string.Empty;
        [DataMember(Name = "guards")] public int Guards;
        [DataMember(Name = "owner")] public string Owner = string.Empty;
        [DataMember(Name = "ownerName")] public string OwnerName = string.Empty;
        [DataMember(Name = "mode")] public string Mode = string.Empty;
        [DataMember(Name = "state")] public string State = string.Empty;
        [DataMember(Name = "leader")] public string Leader = string.Empty;
        [DataMember(Name = "members")] public List<string> Members = new List<string>();
        [DataMember(Name = "groupSize")] public int GroupSize;
        [DataMember(Name = "teamSize")] public int TeamSize;
        [DataMember(Name = "text")] public string Text = string.Empty;
        [DataMember(Name = "coins")] public int Coins;
        [DataMember(Name = "goods")] public List<DikozemieWorldGoods> Goods = new List<DikozemieWorldGoods>();
        [DataMember(Name = "from")] public string From = string.Empty;
        [DataMember(Name = "to")] public string To = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieWorldLine
    {
        [DataMember(Name = "id")] public long Id;
        [DataMember(Name = "at")] public double At;
        [DataMember(Name = "player")] public string Player = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "text")] public string Text = string.Empty;
        [DataMember(Name = "kind")] public string Kind = string.Empty;

        internal bool IsSystem => Kind == "system";
    }

    [DataContract]
    internal sealed class DikozemieWorldChannel
    {
        [DataMember(Name = "channel")] public string Channel = string.Empty;
        [DataMember(Name = "title")] public string Title = string.Empty;
        [DataMember(Name = "last")] public long Last;
        [DataMember(Name = "lines")] public List<DikozemieWorldLine> Lines = new List<DikozemieWorldLine>();
    }

    [DataContract]
    internal sealed class DikozemieWorldNote
    {
        [DataMember(Name = "kind")] public string Kind = string.Empty;
        [DataMember(Name = "text")] public string Text = string.Empty;
        [DataMember(Name = "town")] public string Town = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieWorldMarks
    {
        [DataMember(Name = "outlaw")] public bool Outlaw;
        [DataMember(Name = "protected")] public bool Protected;
        [DataMember(Name = "cooldown")] public float Cooldown;
        [DataMember(Name = "raid")] public float Raid;
    }

    [DataContract]
    internal sealed class DikozemieWorldView
    {
        [DataMember(Name = "ok")] public bool Ok;
        [DataMember(Name = "code")] public string Code = string.Empty;
        [DataMember(Name = "message")] public string Message = string.Empty;
        [DataMember(Name = "serverTime")] public double ServerTime;
        [DataMember(Name = "players")] public List<DikozemieWorldPlayer> Players = new List<DikozemieWorldPlayer>();
        [DataMember(Name = "towns")] public Dictionary<string, int> Towns = new Dictionary<string, int>();
        [DataMember(Name = "online")] public int Online;
        [DataMember(Name = "convoys")] public List<DikozemieWorldConvoy> Convoys = new List<DikozemieWorldConvoy>();
        [DataMember(Name = "party")] public DikozemieWorldParty Party;
        [DataMember(Name = "invites")] public List<DikozemieWorldInvite> Invites = new List<DikozemieWorldInvite>();
        [DataMember(Name = "meeting")] public DikozemieWorldMeeting Meeting;
        [DataMember(Name = "raids")] public List<DikozemieWorldRaid> Raids = new List<DikozemieWorldRaid>();
        [DataMember(Name = "chat")] public List<DikozemieWorldChannel> Chat = new List<DikozemieWorldChannel>();
        [DataMember(Name = "notes")] public List<DikozemieWorldNote> Notes = new List<DikozemieWorldNote>();
        [DataMember(Name = "marks")] public DikozemieWorldMarks Marks = new DikozemieWorldMarks();
        [DataMember(Name = "lookKnown")] public bool LookKnown = true;
        // Stage 6: the hero's clan, the land of the clans, sieges seen.
        [DataMember(Name = "clan")] public DikozemieClanBrief Clan;
        [DataMember(Name = "landsRev")] public long LandsRev;
        [DataMember(Name = "lands")] public List<DikozemieLand> Lands = new List<DikozemieLand>();
        [DataMember(Name = "sieges")] public List<DikozemieSiege> Sieges = new List<DikozemieSiege>();
        [DataMember(Name = "clanInvites")] public List<DikozemieClanInvite> ClanInvites = new List<DikozemieClanInvite>();
        [DataMember(Name = "clans")] public Dictionary<string, DikozemieClanName> Clans = new Dictionary<string, DikozemieClanName>();
    }

    [DataContract]
    internal sealed class DikozemieWorldLook
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "hash")] public string Hash = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "body")] public string Body = string.Empty;
        [DataMember(Name = "female")] public bool Female;
        [DataMember(Name = "race")] public int Race;
        [DataMember(Name = "equip")] public string Equip = string.Empty;
        [DataMember(Name = "culture")] public string Culture = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieWorldLooks
    {
        [DataMember(Name = "ok")] public bool Ok;
        [DataMember(Name = "looks")] public List<DikozemieWorldLook> Looks = new List<DikozemieWorldLook>();
    }

    // The answer to an action: whatever it changed.
    [DataContract]
    internal sealed class DikozemieWorldAnswer
    {
        [DataMember(Name = "ok")] public bool Ok;
        [DataMember(Name = "code")] public string Code = string.Empty;
        [DataMember(Name = "message")] public string Message = string.Empty;
        [DataMember(Name = "meeting")] public DikozemieWorldMeeting Meeting;
        [DataMember(Name = "raid")] public DikozemieWorldRaid Raid;
        [DataMember(Name = "party")] public DikozemieWorldParty Party;
        [DataMember(Name = "chat")] public List<DikozemieWorldChannel> Chat;
    }

    internal static class DikozemieWorld
    {
        // ---- what the map, the town and the walk report -----------------

        private static float _mapX;
        private static float _mapY;
        private static float _headX;
        private static float _headY;
        private static float _speed;
        private static float _sight = 260f;
        private static bool _mapMoving;
        private static bool _mapBoat;
        private static int _mapVessel;
        private static string _mapNode = string.Empty;
        private static string _mapPlace = "map";
        private static string _mapSection = string.Empty;
        private static DateTime _mapReportedUtc = DateTime.MinValue;
        private static DikozemieWorldWalk _walk;
        private static DateTime _walkReportedUtc = DateTime.MinValue;

        // The map widget, every frame it is on screen. head: a point a few
        // seconds ahead on the way, for the others to draw the hero walking.
        internal static void ReportMap(float x, float y, bool moving, float headX, float headY, float speed,
            string node, bool boat, int vessel, float sight, string place, string section)
        {
            _mapX = x;
            _mapY = y;
            _mapMoving = moving;
            _headX = headX;
            _headY = headY;
            _speed = speed;
            _mapNode = node ?? string.Empty;
            _mapBoat = boat;
            _mapVessel = vessel;
            _sight = sight;
            _mapPlace = place ?? "map";
            _mapSection = section ?? string.Empty;
            _mapReportedUtc = DateTime.UtcNow;
        }

        internal static void ReportWalk(string scene, Vec3 position, float direction, int action)
        {
            _walk = new DikozemieWorldWalk
            {
                Scene = scene ?? string.Empty, X = position.x, Y = position.y, Z = position.z,
                Dir = direction, Act = action
            };
            _walkReportedUtc = DateTime.UtcNow;
        }

        internal static bool IsMapOnScreen => (DateTime.UtcNow - _mapReportedUtc).TotalSeconds < 1.0;

        // ---- the last answer ------------------------------------------------

        internal static DikozemieWorldView View { get; private set; }
        internal static int Revision { get; private set; }
        private static DateTime _lastOkUtc = DateTime.MinValue;
        private static bool _pulseInFlight;
        private static DateTime _pulseSentUtc;
        private static DateTime _nextPulse = DateTime.MinValue;
        private static int _failures;
        internal static string LastError { get; private set; } = string.Empty;

        internal static bool IsOnline => View != null && (DateTime.UtcNow - _lastOkUtc).TotalSeconds < 25;

        internal static string MyId => DikozemieEconomy.PlayerId;

        internal static IReadOnlyList<DikozemieWorldPlayer> Players =>
            (IReadOnlyList<DikozemieWorldPlayer>)View?.Players ?? Array.Empty<DikozemieWorldPlayer>();

        internal static IReadOnlyList<DikozemieWorldConvoy> Convoys =>
            (IReadOnlyList<DikozemieWorldConvoy>)View?.Convoys ?? Array.Empty<DikozemieWorldConvoy>();

        internal static DikozemieWorldParty Party => View?.Party;

        internal static DikozemieWorldMeeting Meeting => View?.Meeting;

        internal static bool InParty => Party != null && Party.Members.Count > 1;

        internal static bool IsPartyLeader => Party == null || Party.Leader == MyId;

        // A member of a party walks where its leader walks.
        internal static bool IsFollower => InParty && !IsPartyLeader;

        internal static DikozemieWorldPlayer Find(string id) =>
            string.IsNullOrEmpty(id) ? null : Players.FirstOrDefault(p => p.Id == id);

        internal static DikozemieWorldPlayer Leader => IsFollower ? Find(Party.Leader) : null;

        internal static int TownCount(string node) =>
            View?.Towns != null && node != null && View.Towns.TryGetValue(node, out int count) ? count : 0;

        internal static string PlaceName(string node) =>
            DikozemieTown.DisplayName(DikozemieLobbyMapCatalog.Find(node));

        // ---- smoothing ---------------------------------------------------------

        private sealed class Shown
        {
            internal float X;
            internal float Y;
            internal DateTime At;
        }

        private static readonly Dictionary<string, Shown> ShownAt = new Dictionary<string, Shown>(StringComparer.Ordinal);

        // Where to draw another hero now: his last word carried forward along
        // his way, eased so that a new word never makes him jump.
        internal static void Position(DikozemieWorldPlayer p, out float x, out float y)
        {
            float elapsed = (float)Math.Min(4.0, (DateTime.UtcNow - p.ReceivedUtc).TotalSeconds + p.Age);
            x = p.X;
            y = p.Y;
            if (p.Moving && p.Speed > 0f)
            {
                float dx = p.Tx - p.X;
                float dy = p.Ty - p.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                float step = Math.Min(dist, p.Speed * elapsed);
                if (dist > 0.01f)
                {
                    x += dx / dist * step;
                    y += dy / dist * step;
                }
            }
            DateTime now = DateTime.UtcNow;
            if (!ShownAt.TryGetValue(p.Id, out Shown shown) || (now - shown.At).TotalSeconds > 3)
            {
                ShownAt[p.Id] = new Shown { X = x, Y = y, At = now };
                return;
            }
            float dt = (float)(now - shown.At).TotalSeconds;
            float jump = Math.Abs(x - shown.X) + Math.Abs(y - shown.Y);
            float k = jump > 400f ? 1f : Math.Min(1f, dt * 6f);
            shown.X += (x - shown.X) * k;
            shown.Y += (y - shown.Y) * k;
            shown.At = now;
            x = shown.X;
            y = shown.Y;
        }

        // A caravan or ship on its way: where it is now along the road (or
        // the sea lane) between its two towns, as the router draws it.
        private static readonly Dictionary<string, DikozemieTravelRoute> ConvoyRoutes =
            new Dictionary<string, DikozemieTravelRoute>(StringComparer.Ordinal);

        internal static bool Position(DikozemieWorldConvoy c, out float x, out float y)
        {
            x = c.X;
            y = c.Y;
            DikozemieLobbyMapNode from = DikozemieLobbyMapCatalog.Find(c.From);
            DikozemieLobbyMapNode to = DikozemieLobbyMapCatalog.Find(c.To);
            if (from?.Position == null || to?.Position == null)
            {
                return false;
            }
            string key = c.From + ">" + c.To + (c.IsShip ? ":sea" : ":land");
            if (!ConvoyRoutes.TryGetValue(key, out DikozemieTravelRoute route))
            {
                route = DikozemieTravelRouter.Find(from.Position.X, from.Position.Y, to.Position.X, to.Position.Y, c.IsShip);
                ConvoyRoutes[key] = route;
            }
            double share = c.Travel <= 0 ? 1.0 :
                Math.Max(0.0, Math.Min(1.0, (DikozemieEconomy.ServerNow - c.Leaves) / c.Travel));
            if (route == null || route.Xs.Count < 2)
            {
                x = from.Position.X + (to.Position.X - from.Position.X) * (float)share;
                y = from.Position.Y + (to.Position.Y - from.Position.Y) * (float)share;
                return true;
            }
            float total = 0f;
            for (int index = 1; index < route.Xs.Count; index++)
            {
                total += Distance(route.Xs[index - 1], route.Ys[index - 1], route.Xs[index], route.Ys[index]);
            }
            float left = total * (float)share;
            for (int index = 1; index < route.Xs.Count; index++)
            {
                float segment = Distance(route.Xs[index - 1], route.Ys[index - 1], route.Xs[index], route.Ys[index]);
                if (left <= segment || index == route.Xs.Count - 1)
                {
                    float k = segment <= 0.01f ? 0f : Math.Min(1f, left / segment);
                    x = route.Xs[index - 1] + (route.Xs[index] - route.Xs[index - 1]) * k;
                    y = route.Ys[index - 1] + (route.Ys[index] - route.Ys[index - 1]) * k;
                    return true;
                }
                left -= segment;
            }
            return true;
        }

        // Which way a convoy heads (for the picture to face it).
        internal static bool HeadsLeft(DikozemieWorldConvoy c)
        {
            DikozemieLobbyMapNode from = DikozemieLobbyMapCatalog.Find(c.From);
            DikozemieLobbyMapNode to = DikozemieLobbyMapCatalog.Find(c.To);
            return from?.Position != null && to?.Position != null && to.Position.X < from.Position.X;
        }

        internal static float Distance(float ax, float ay, float bx, float by)
        {
            float dx = bx - ax;
            float dy = by - ay;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        // ---- the clock ------------------------------------------------------

        internal static void Tick()
        {
            if (!DikozemieEconomy.Enabled || !DikozemieHeroProfile.IsCreated || !DikozemieEconomy.CanAsk)
            {
                return;
            }
            DikozemieWorldBattle.Tick();
            DateTime now = DateTime.UtcNow;
            if (_pulseInFlight)
            {
                if ((now - _pulseSentUtc).TotalSeconds < 15)
                {
                    return;
                }
                _pulseInFlight = false;
            }
            if (now < _nextPulse)
            {
                return;
            }
            Pulse();
        }

        // Asked by a screen that waits for news (a meeting, an action done).
        internal static void PulseSoon(double seconds = 0.2)
        {
            DateTime when = DateTime.UtcNow.AddSeconds(seconds);
            if (when < _nextPulse)
            {
                _nextPulse = when;
            }
        }

        internal static bool ChatOpen { get; set; }

        private static double Interval()
        {
            if (Mission.Current != null)
            {
                return DikozemieTownWalk.IsRunning ? 0.5 : 3.0;
            }
            if (DikozemieWorldBattle.IsPending || View?.Meeting != null || ChatOpen ||
                View?.Raids?.Any(r => r.State == "fighting") == true)
            {
                return 1.0;
            }
            if (IsMapOnScreen)
            {
                return _mapMoving || Players.Any(p => p.Moving) ? 1.0 : 2.0;
            }
            return 5.0;
        }

        private static string Place()
        {
            if (Mission.Current != null)
            {
                return DikozemieTownWalk.IsRunning ? "walk" : DikozemieRoadFight.IsRunning ? "road" : "battle";
            }
            if (IsMapOnScreen)
            {
                return _mapPlace;
            }
            return DikozemieTown.IsTownScreenOpen ? "town" : "menu";
        }

        private static readonly Dictionary<string, long> ChatSince = new Dictionary<string, long>(StringComparer.Ordinal);
        private static string _lookSent = string.Empty;
        private static DateTime _lookSentUtc = DateTime.MinValue;

        private static void Pulse()
        {
            DikozemieTravelSave save = DikozemieTravelState.Current ?? DikozemieTravelState.EnsureLoadedForHero();
            if (save == null)
            {
                _nextPulse = DateTime.UtcNow.AddSeconds(3);
                return;
            }
            bool fromMap = IsMapOnScreen;
            string place = Place();
            float x = fromMap ? _mapX : save.X;
            float y = fromMap ? _mapY : save.Y;
            string node = fromMap ? _mapNode : save.NodeId ?? string.Empty;
            DikozemieLobbyMapNode walking = place == "walk" ? DikozemieTownWalk.Active?.Node : null;
            if (walking?.Position != null)
            {
                // A hero walking a town stands in that town, whatever the map says.
                node = walking.Id;
                x = walking.Position.X;
                y = walking.Position.Y;
            }
            var body = new StringBuilder("{");
            void Field(string name, string json)
            {
                if (body.Length > 1)
                {
                    body.Append(',');
                }
                body.Append('"').Append(name).Append("\":").Append(json);
            }
            string F(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
            Field("x", F(x));
            Field("y", F(y));
            Field("place", Json(place));
            Field("node", Json(node));
            Field("boat", fromMap ? (_mapBoat ? "true" : "false") : (save.Boat ? "true" : "false"));
            Field("vessel", (fromMap ? _mapVessel : save.Vessel).ToString(CultureInfo.InvariantCulture));
            bool moving = fromMap && _mapMoving;
            Field("moving", moving ? "true" : "false");
            Field("tx", F(moving ? _headX : x));
            Field("ty", F(moving ? _headY : y));
            Field("speed", F(moving ? _speed : 0f));
            Field("section", Json(fromMap ? _mapSection : string.Empty));
            Field("sight", F(_sight));
            Field("mounted", DikozemieRoads.IsMounted ? "true" : "false");
            Field("culture", Json(DikozemieHeroProfile.CultureId ?? "empire"));
            Field("rating", Math.Max(0, DikozemieArmoryProgression.PlayerRating).ToString(CultureInfo.InvariantCulture));
            Field("level", DikozemieHeroProfile.Level.ToString(CultureInfo.InvariantCulture));
            Field("wound", DikozemieRoads.Wound.ToString(CultureInfo.InvariantCulture));
            Field("escort", (DikozemieRoads.HasCompany ? Math.Max(1, save.FellowCount) : 0).ToString(CultureInfo.InvariantCulture));
            Field("skills", "{" + string.Join(",", new[] { "Riding", "Athletics", "Scouting", "Charm", "Roguery", "Leadership" }
                .Select(skill => "\"" + skill + "\":" + DikozemieHeroProfile.GetEffectiveSkill(skill, 0))) + "}");
            if (DikozemieWorldBattle.Active != null)
            {
                Field("meeting", Json(DikozemieWorldBattle.Active.Id));
                Field("port", DikozemiePvpSearchController.LastJoinedPort.ToString(CultureInfo.InvariantCulture));
            }
            if (place == "walk" && _walk != null && (DateTime.UtcNow - _walkReportedUtc).TotalSeconds < 3)
            {
                Field("walk", "{\"scene\":" + Json(_walk.Scene) + ",\"x\":" + F(_walk.X) + ",\"y\":" + F(_walk.Y) +
                    ",\"z\":" + F(_walk.Z) + ",\"dir\":" + _walk.Dir.ToString("0.###", CultureInfo.InvariantCulture) +
                    ",\"act\":" + _walk.Act + "}");
            }
            DikozemieWorldLook mine = OwnLook();
            if (mine != null && (mine.Hash != _lookSent || (DateTime.UtcNow - _lookSentUtc).TotalSeconds > 60))
            {
                Field("look", "{\"hash\":" + Json(mine.Hash) + ",\"body\":" + Json(mine.Body) +
                    ",\"female\":" + (mine.Female ? "true" : "false") + ",\"race\":" + mine.Race +
                    ",\"equip\":" + Json(mine.Equip) + ",\"culture\":" + Json(mine.Culture) + "}");
                _lookSent = mine.Hash;
                _lookSentUtc = DateTime.UtcNow;
            }
            Field("chat", "{" + string.Join(",", ChatSince.Select(pair =>
                Json(pair.Key) + ":" + pair.Value.ToString(CultureInfo.InvariantCulture))) + "}");
            body.Append('}');
            _pulseInFlight = true;
            _pulseSentUtc = DateTime.UtcNow;
            DikozemieEconomy.Request(HttpMethod.Post, "world/pulse", body.ToString(), (status, raw, error) =>
            {
                _pulseInFlight = false;
                DikozemieWorldView view = status == 200 ? DikozemieEconomy.ParseJson<DikozemieWorldView>(raw) : null;
                if (status == 200 && view == null && _failures < 3)
                {
                    DikozemieLobbyShellIntegration.TraceButtonAction("world pulse unreadable: " +
                        (raw ?? string.Empty).Substring(0, Math.Min(300, (raw ?? string.Empty).Length)));
                }
                if (view == null || !view.Ok)
                {
                    _failures++;
                    LastError = DikozemieEconomy.ParseJson<DikozemieWorldView>(raw)?.Message ?? error ?? string.Empty;
                    _nextPulse = DateTime.UtcNow.AddSeconds(Math.Min(30, 2 * _failures));
                    if (status == 403)
                    {
                        // An unconfirmed computer: the treasury says so already.
                        _lookSent = string.Empty;
                    }
                    return;
                }
                _failures = 0;
                LastError = string.Empty;
                Accept(view);
                _nextPulse = DateTime.UtcNow.AddSeconds(Interval());
            });
        }

        // ---- what came back ----------------------------------------------------

        private static readonly Dictionary<string, List<DikozemieWorldLine>> ChatLog =
            new Dictionary<string, List<DikozemieWorldLine>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> ChatTitles = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> Unread = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly HashSet<string> InvitesShown = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Queue<DikozemieWorldNote> Captives = new Queue<DikozemieWorldNote>();
        private static string _meetingState = string.Empty;
        private static string _raidsState = string.Empty;
        private static string _partyBattleSeen = string.Empty;
        internal static string ChatViewing { get; set; } = string.Empty;

        private static void Accept(DikozemieWorldView view)
        {
            DateTime now = DateTime.UtcNow;
            foreach (DikozemieWorldPlayer p in view.Players)
            {
                p.ReceivedUtc = now;
            }
            if (view.Meeting != null)
            {
                view.Meeting.ReceivedUtc = now;
            }
            foreach (DikozemieSiege siege in view.Sieges ?? new List<DikozemieSiege>())
            {
                siege.ReceivedUtc = now;
            }
            View = view;
            _lastOkUtc = now;
            if (!view.LookKnown)
            {
                // The server lost the hero's face (restarted): the next word brings it.
                _lookSent = string.Empty;
            }
            Revision++;
            MergeChat(view.Chat);
            foreach (DikozemieWorldNote note in view.Notes ?? new List<DikozemieWorldNote>())
            {
                if (note.Kind == "captive" && !string.IsNullOrEmpty(note.Town))
                {
                    Captives.Enqueue(note);
                }
                else
                {
                    Notify(note.Text, new Color(0.62f, 0.86f, 0.62f));
                }
            }
            string meetingState = view.Meeting == null ? string.Empty : view.Meeting.Id + ":" + view.Meeting.State;
            string raidsState = string.Join(",", view.Raids.Select(r => r.Id + ":" + r.State));
            bool newMeeting = view.Meeting != null && view.Meeting.State != "done" &&
                !_meetingState.StartsWith(view.Meeting.Id, StringComparison.Ordinal);
            bool partyCalls = view.Party?.Battle != null && view.Party.Leader != MyId &&
                view.Party.Battle.Members.Contains(MyId) && view.Party.Battle.Id != _partyBattleSeen;
            if ((newMeeting || partyCalls) && Mission.Current == null && !IsMapOnScreen &&
                !DikozemieNavalBattleLauncher.IsPending)
            {
                // Somebody stopped the hero on the road (or his party took a
                // fight) while he looked at another page: the map comes back.
                Notify(newMeeting ? "На дороге вас остановили!" : "Отряд вступает в бой!", new Color(0.95f, 0.45f, 0.35f));
                DikozemieLobbyShellIntegration.OpenMap(false);
            }
            if (view.Party?.Battle != null)
            {
                _partyBattleSeen = view.Party.Battle.Id;
            }
            if (meetingState != _meetingState || raidsState != _raidsState)
            {
                // Coins or cargo may have changed hands.
                DikozemieEconomy.RequestRefresh();
                DikozemieLobbyShellIntegration.TraceButtonAction("world meeting=" + meetingState + " raids=" + raidsState);
            }
            _meetingState = meetingState;
            _raidsState = raidsState;
            if (view.Meeting?.Fight?.Port > 0)
            {
                DikozemieWorldBattle.UpdatePort(view.Meeting.Fight.Port);
            }
            ShowInvites(view.Invites);
            DikozemieClans.OnPulse(view);
            FetchLooks(view.Players.Select(p => p.Id).Concat(view.Meeting?.Them?.Select(p => p.Id) ??
                Enumerable.Empty<string>()).Concat(view.Meeting?.Us?.Select(p => p.Id) ?? Enumerable.Empty<string>()),
                view);
        }

        // The winner of a meeting sold this hero to an arena: the map takes
        // him there, as the man-catchers do.
        internal static bool TryTakeCaptive(out DikozemieWorldNote note)
        {
            note = Captives.Count > 0 ? Captives.Dequeue() : null;
            return note != null;
        }

        private static void MergeChat(List<DikozemieWorldChannel> channels)
        {
            if (channels == null)
            {
                return;
            }
            foreach (DikozemieWorldChannel channel in channels)
            {
                bool first = !ChatSince.ContainsKey(channel.Channel);
                ChatTitles[channel.Channel] = channel.Title;
                if (!ChatLog.TryGetValue(channel.Channel, out List<DikozemieWorldLine> log))
                {
                    log = new List<DikozemieWorldLine>();
                    ChatLog[channel.Channel] = log;
                }
                long known = log.Count == 0 ? -1 : log[log.Count - 1].Id;
                foreach (DikozemieWorldLine line in channel.Lines)
                {
                    if (line.Id <= known)
                    {
                        continue;
                    }
                    log.Add(line);
                    if (!first && line.Player != MyId && !line.IsSystem)
                    {
                        if (ChatViewing != channel.Channel)
                        {
                            Unread[channel.Channel] = UnreadIn(channel.Channel) + 1;
                            bool party = channel.Channel.StartsWith("party:", StringComparison.Ordinal);
                            bool clan = channel.Channel.StartsWith("clan:", StringComparison.Ordinal);
                            Notify((party ? "[Отряд] " : clan ? "[Клан] " : "[Таверна] ") + line.Name + ": " + line.Text,
                                party ? new Color(0.62f, 0.86f, 0.62f)
                                    : clan ? new Color(0.62f, 0.80f, 0.98f) : new Color(0.95f, 0.82f, 0.52f));
                        }
                    }
                }
                if (log.Count > 200)
                {
                    log.RemoveRange(0, log.Count - 200);
                }
                ChatSince[channel.Channel] = Math.Max(channel.Last, known);
            }
            // Channels the hero no longer stands in are forgotten next time.
            var current = new HashSet<string>(channels.Select(c => c.Channel), StringComparer.Ordinal);
            foreach (string gone in ChatSince.Keys.Where(k => !current.Contains(k)).ToList())
            {
                ChatSince.Remove(gone);
                ChatLog.Remove(gone);
                Unread.Remove(gone);
            }
        }

        internal static IReadOnlyList<string> Channels => ChatLog.Keys.ToList();

        internal static string ChannelTitle(string channel) =>
            channel != null && ChatTitles.TryGetValue(channel, out string title) ? title : string.Empty;

        internal static IReadOnlyList<DikozemieWorldLine> Lines(string channel) =>
            channel != null && ChatLog.TryGetValue(channel, out List<DikozemieWorldLine> log)
                ? log
                : (IReadOnlyList<DikozemieWorldLine>)Array.Empty<DikozemieWorldLine>();

        internal static int UnreadIn(string channel) =>
            channel != null && Unread.TryGetValue(channel, out int count) ? count : 0;

        internal static void MarkRead(string channel)
        {
            if (channel != null)
            {
                Unread.Remove(channel);
            }
        }

        internal static string PlaceChannel => ChatLog.Keys.FirstOrDefault(k => k.StartsWith("place:", StringComparison.Ordinal));

        internal static string PartyChannel => ChatLog.Keys.FirstOrDefault(k => k.StartsWith("party:", StringComparison.Ordinal));

        internal static string ClanChannel => ChatLog.Keys.FirstOrDefault(k => k.StartsWith("clan:", StringComparison.Ordinal));

        private static void Notify(string text, Color color)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                InformationManager.DisplayMessage(new InformationMessage(text, color));
            }
        }

        // ---- invitations ------------------------------------------------------

        private static void ShowInvites(List<DikozemieWorldInvite> invites)
        {
            if (invites == null || InformationManager.IsAnyInquiryActive() || Mission.Current != null)
            {
                return;
            }
            DikozemieWorldInvite invite = invites.FirstOrDefault(i => !InvitesShown.Contains(i.Id));
            if (invite == null)
            {
                return;
            }
            InvitesShown.Add(invite.Id);
            string id = invite.Id;
            InformationManager.ShowInquiry(new InquiryData(
                "ПРИГЛАШЕНИЕ В ОТРЯД",
                invite.FromName + " зовёт вас идти вместе.\n\nОтряд ходит по карте за ведущим: куда он, туда и вы. " +
                "На дороге отряд бьётся одной командой — и против разбойников, и против других игроков. " +
                "В городе каждый сам по себе. Уйти из отряда можно в любой момент.",
                true, true, "ИДТИ ВМЕСТЕ", "ОТКАЗАТЬСЯ",
                () => Act("world/party/accept", "{\"invite\":" + Json(id) + "}", (answer, error) =>
                    Notify(error ?? "Вы идёте с отрядом " + invite.FromName + ".", new Color(0.62f, 0.86f, 0.62f))),
                () => Act("world/party/decline", "{\"invite\":" + Json(id) + "}", null)), true, true);
        }

        // ---- looks ------------------------------------------------------------

        private static readonly Dictionary<string, DikozemieWorldLook> Looks =
            new Dictionary<string, DikozemieWorldLook>(StringComparer.Ordinal);
        private static bool _looksInFlight;

        internal static DikozemieWorldLook LookOf(string id) =>
            id != null && Looks.TryGetValue(id, out DikozemieWorldLook look) ? look : null;

        private static DateTime _looksAskedUtc = DateTime.MinValue;

        private static void FetchLooks(IEnumerable<string> ids, DikozemieWorldView view)
        {
            // A lost answer must not stop faces from coming for ever.
            if (_looksInFlight && (DateTime.UtcNow - _looksAskedUtc).TotalSeconds < 12)
            {
                return;
            }
            _looksInFlight = false;
            var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (DikozemieWorldPlayer p in view.Players)
            {
                hashes[p.Id] = p.Look;
            }
            foreach (DikozemieWorldPerson p in (view.Meeting?.Them ?? new List<DikozemieWorldPerson>())
                         .Concat(view.Meeting?.Us ?? new List<DikozemieWorldPerson>()))
            {
                hashes[p.Id] = p.Look;
            }
            List<string> wanted = ids.Distinct().Where(id => id != MyId && hashes.TryGetValue(id, out string hash) &&
                !string.IsNullOrEmpty(hash) && (!Looks.TryGetValue(id, out DikozemieWorldLook known) || known.Hash != hash))
                .Take(16).ToList();
            if (wanted.Count == 0)
            {
                return;
            }
            _looksInFlight = true;
            _looksAskedUtc = DateTime.UtcNow;
            DikozemieLobbyShellIntegration.TraceButtonAction("world looks ask " + string.Join(",", wanted));
            DikozemieEconomy.Request(HttpMethod.Get, "world/looks?ids=" + Uri.EscapeDataString(string.Join(",", wanted)), null,
                (status, raw, error) =>
                {
                    _looksInFlight = false;
                    DikozemieWorldLooks looks = status == 200 ? DikozemieEconomy.ParseJson<DikozemieWorldLooks>(raw) : null;
                    if (looks?.Looks == null)
                    {
                        return;
                    }
                    foreach (DikozemieWorldLook look in looks.Looks)
                    {
                        Looks[look.Id] = look;
                    }
                    Revision++;
                });
        }

        private static DikozemieWorldLook _ownLook;
        private static int _ownLookRevision = -1;
        private static DateTime _ownLookAt = DateTime.MinValue;

        // What the others see of this hero: his face and what he wears.
        internal static DikozemieWorldLook OwnLook()
        {
            if (!DikozemieHeroProfile.IsCreated)
            {
                return null;
            }
            if (_ownLook != null && (DateTime.UtcNow - _ownLookAt).TotalSeconds < 20)
            {
                return _ownLook;
            }
            try
            {
                DikozemieHeroProfileState profile = DikozemieHeroProfile.Snapshot;
                BasicCharacterObject body = MBObjectManager.Instance.GetObject<BasicCharacterObject>(
                    DikozemieRoadFolk.FaceTroop(profile.CultureId));
                Equipment equipment = DikozemieHeroProfile.CreateDisplayEquipment(body);
                string equip = equipment?.CalculateEquipmentCode() ?? string.Empty;
                string face = string.IsNullOrEmpty(profile.BodyProperties)
                    ? body?.GetBodyProperties(equipment).ToString() ?? string.Empty
                    : profile.BodyProperties;
                string culture = DikozemieTown.NormalizeCulture(profile.CultureId);
                string hash;
                using (var sha = SHA1.Create())
                {
                    hash = string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(face + "|" + equip + "|" +
                        profile.IsFemale + "|" + profile.Race + "|" + culture)).Take(10).Select(b => b.ToString("x2")));
                }
                _ownLook = new DikozemieWorldLook
                {
                    Id = MyId, Hash = hash, Body = face, Equip = equip, Female = profile.IsFemale,
                    Race = Math.Max(0, profile.Race), Culture = culture, Name = profile.Name
                };
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: own look failed: " + error.Message);
            }
            _ownLookAt = DateTime.UtcNow;
            return _ownLook;
        }

        // ---- actions --------------------------------------------------------------

        internal static void Act(string path, string body, Action<DikozemieWorldAnswer, string> done)
        {
            if (!DikozemieEconomy.CanAsk)
            {
                done?.Invoke(null, DikozemieEconomy.StatusText);
                return;
            }
            DikozemieLobbyShellIntegration.TraceButtonAction("world act " + path);
            DikozemieEconomy.Request(HttpMethod.Post, path, body ?? "{}", (status, raw, error) =>
            {
                DikozemieWorldAnswer answer = DikozemieEconomy.ParseJson<DikozemieWorldAnswer>(raw);
                PulseSoon();
                if (status == 200 && answer != null && answer.Ok)
                {
                    if (answer.Chat != null)
                    {
                        MergeChat(answer.Chat);
                        Revision++;
                    }
                    if (answer.Meeting != null && View != null)
                    {
                        answer.Meeting.ReceivedUtc = DateTime.UtcNow;
                        View.Meeting = answer.Meeting;
                        Revision++;
                    }
                    if (answer.Raid != null && View != null)
                    {
                        View.Raids.RemoveAll(r => r.Id == answer.Raid.Id);
                        View.Raids.Add(answer.Raid);
                        Revision++;
                    }
                    DikozemieEconomy.RequestRefresh();
                    done?.Invoke(answer, null);
                    return;
                }
                string message = answer?.Message;
                if (string.IsNullOrEmpty(message))
                {
                    message = error == null ? "Сервер мира не ответил." : "Нет связи с сервером мира: " + error;
                }
                DikozemieLobbyShellIntegration.TraceButtonAction("world act failed " + path + " status=" + status + " " + message);
                done?.Invoke(null, message);
            });
        }

        internal static void Say(string channel, string text, Action<string> done)
        {
            string trimmed = (text ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                done?.Invoke(null);
                return;
            }
            Act("chat/say", "{\"channel\":" + Json(channel) + ",\"text\":" + Json(trimmed) + ",\"since\":{" +
                string.Join(",", ChatSince.Select(pair => Json(pair.Key) + ":" + pair.Value.ToString(CultureInfo.InvariantCulture))) +
                "}}", (answer, error) => done?.Invoke(error));
        }

        internal static string Json(string value) => DikozemieEconomy.Json(value);

        // ---- words --------------------------------------------------------------

        internal static string CultureName(string culture)
        {
            switch (DikozemieTown.NormalizeCulture(culture))
            {
                case "aserai": return "асераи";
                case "battania": return "баттания";
                case "khuzait": return "хузаиты";
                case "nord": return "норды";
                case "sturgia": return "стургия";
                case "vlandia": return "вландия";
                default: return "империя";
            }
        }

        internal static string Goods(IEnumerable<DikozemieWorldGoods> goods) =>
            string.Join(", ", (goods ?? Enumerable.Empty<DikozemieWorldGoods>())
                .Where(g => g.Qty > 0).Select(g => g.Name.ToLowerInvariant() + " " + g.Qty));

        internal static string People(int count)
        {
            int last = count % 10;
            int lastTwo = count % 100;
            string word = lastTwo >= 11 && lastTwo <= 14 ? "человек" : last >= 2 && last <= 4 ? "человека" : "человек";
            return count + " " + word;
        }

        internal static string Heroes(int count)
        {
            int last = count % 10;
            int lastTwo = count % 100;
            string word = lastTwo >= 11 && lastTwo <= 14 ? "героев" : last == 1 ? "герой" : last >= 2 && last <= 4 ? "героя" : "героев";
            return count + " " + word;
        }
    }
}
