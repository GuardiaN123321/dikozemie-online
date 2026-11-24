using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    // Stage 3 of the plan (customer, 2026-11-08): the trade server keeps the
    // money. Coins, cargo, workshops, caravans and voyages live on the server
    // (EconomyServer/dikozemie_economy.py); every coin is written into its
    // ledger with a reason. The client only asks and shows what it is told.
    //
    // Old code keeps changing the local coin count at once, as before, and
    // reports every change here with its reason. The server answers with the
    // real balance; the local count is set to it plus what is still on its
    // way. A doctored file brings nothing: the next answer overwrites it.

    internal enum DikozemieEconomyStatus
    {
        Off,
        Waiting,
        Connecting,
        Online,
        Offline,
        DeviceUnconfirmed,
        Banned
    }

    [DataContract]
    internal sealed class DikozemieEconomyOp
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "kind")] public string Kind = string.Empty;
        [DataMember(Name = "amount")] public int Amount;
        [DataMember(Name = "note")] public string Note = string.Empty;
        [DataMember(Name = "ref")] public string Ref = string.Empty;
        [DataMember(Name = "item")] public string Item = string.Empty;
        [DataMember(Name = "items")] public List<string> Items;
        [DataMember(Name = "share")] public double Share;
        // What the change did to the local count: + earned, - spent.
        [DataMember(Name = "delta")] public int Delta;
    }

    [DataContract]
    internal sealed class DikozemieEconomyOpResult
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "ok")] public bool Ok;
        [DataMember(Name = "code")] public string Code = string.Empty;
        [DataMember(Name = "cut")] public int Cut;
    }

    [DataContract]
    internal sealed class DikozemieLedgerRow
    {
        [DataMember(Name = "id")] public long Id;
        [DataMember(Name = "at")] public double At;
        [DataMember(Name = "amount")] public int Amount;
        [DataMember(Name = "balance")] public int Balance;
        [DataMember(Name = "kind")] public string Kind = string.Empty;
        [DataMember(Name = "title")] public string Title = string.Empty;
        [DataMember(Name = "note")] public string Note = string.Empty;
        [DataMember(Name = "ref")] public string Ref = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieWorkshopInfo
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "town")] public string Town = string.Empty;
        [DataMember(Name = "townName")] public string TownName = string.Empty;
        [DataMember(Name = "slot")] public int Slot;
        [DataMember(Name = "type")] public string Type = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "owner")] public string Owner = string.Empty;
        [DataMember(Name = "ownerName")] public string OwnerName = string.Empty;
        [DataMember(Name = "npc")] public string Npc = string.Empty;
        [DataMember(Name = "price")] public int Price;
        [DataMember(Name = "work")] public double Work;
        [DataMember(Name = "today")] public double Today;
        [DataMember(Name = "yesterday")] public double Yesterday;
        [DataMember(Name = "total")] public double Total;
        [DataMember(Name = "since")] public double Since;
        [DataMember(Name = "estimate")] public double Estimate;
    }

    [DataContract]
    internal sealed class DikozemieLogLine
    {
        [DataMember(Name = "at")] public double At;
        [DataMember(Name = "text")] public string Text = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieCaravanInfo
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "size")] public string Size = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "mode")] public string Mode = string.Empty;
        [DataMember(Name = "capacity")] public int Capacity;
        [DataMember(Name = "guards")] public int Guards;
        [DataMember(Name = "purse")] public double Purse;
        [DataMember(Name = "stake")] public double Stake;
        [DataMember(Name = "from")] public string From = string.Empty;
        [DataMember(Name = "to")] public string To = string.Empty;
        [DataMember(Name = "fromName")] public string FromName = string.Empty;
        [DataMember(Name = "toName")] public string ToName = string.Empty;
        [DataMember(Name = "arrives")] public double Arrives;
        [DataMember(Name = "leaves")] public double Leaves;
        [DataMember(Name = "earned")] public double Earned;
        [DataMember(Name = "trips")] public int Trips;
        [DataMember(Name = "state")] public string State = string.Empty;
        [DataMember(Name = "master")] public string Master = string.Empty;
        [DataMember(Name = "cargo")] public Dictionary<string, int> Cargo = new Dictionary<string, int>();
        [DataMember(Name = "log")] public List<DikozemieLogLine> Log = new List<DikozemieLogLine>();
    }

    [DataContract]
    internal sealed class DikozemieCommendaInfo
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "fromName")] public string FromName = string.Empty;
        [DataMember(Name = "toName")] public string ToName = string.Empty;
        [DataMember(Name = "amount")] public int Amount;
        [DataMember(Name = "left")] public double Left;
        [DataMember(Name = "returns")] public double Returns;
        [DataMember(Name = "captain")] public string Captain = string.Empty;
        [DataMember(Name = "ship")] public string Ship = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieContractInfo
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "kind")] public string Kind = string.Empty;
        [DataMember(Name = "at")] public string At = string.Empty;
        [DataMember(Name = "from")] public string From = string.Empty;
        [DataMember(Name = "good")] public string Good = string.Empty;
        [DataMember(Name = "goodName")] public string GoodName = string.Empty;
        [DataMember(Name = "qty")] public int Qty;
        [DataMember(Name = "reward")] public int Reward;
        [DataMember(Name = "due")] public double Due;
        [DataMember(Name = "taker")] public string Taker = string.Empty;
        [DataMember(Name = "giver")] public string Giver = string.Empty;
        [DataMember(Name = "title")] public string Title = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieStoryInfo
    {
        [DataMember(Name = "step")] public int Step;
        [DataMember(Name = "done")] public bool Done;
        [DataMember(Name = "title")] public string Title = string.Empty;
        [DataMember(Name = "letter")] public string Letter = string.Empty;
        [DataMember(Name = "task")] public string Task = string.Empty;
        [DataMember(Name = "reward")] public int Reward;
    }

    [DataContract]
    internal sealed class DikozemieGoodRow
    {
        [DataMember(Name = "good")] public string Good = string.Empty;
        [DataMember(Name = "stock")] public int Stock;
        [DataMember(Name = "level")] public double Level;
        [DataMember(Name = "buy")] public int Buy;
        [DataMember(Name = "sell")] public int Sell;
        [DataMember(Name = "price")] public double Price;
        [DataMember(Name = "base")] public int Base;
        [DataMember(Name = "trend")] public double Trend;
        [DataMember(Name = "made")] public double Made;
        [DataMember(Name = "used")] public double Used;
    }

    [DataContract]
    internal sealed class DikozemieMarketInfo
    {
        [DataMember(Name = "town")] public string Town = string.Empty;
        [DataMember(Name = "node")] public string Node = string.Empty;
        [DataMember(Name = "serverTime")] public double ServerTime;
        [DataMember(Name = "goods")] public List<DikozemieGoodRow> Goods = new List<DikozemieGoodRow>();
        [DataMember(Name = "workshops")] public List<DikozemieWorkshopInfo> Workshops = new List<DikozemieWorkshopInfo>();
        [DataMember(Name = "freeSlots")] public List<int> FreeSlots = new List<int>();
        [DataMember(Name = "contracts")] public List<DikozemieContractInfo> Contracts = new List<DikozemieContractInfo>();
        [DataMember(Name = "port")] public bool Port;
        // Stage 6: the market's toll and whose it is.
        [DataMember(Name = "tax")] public double Tax = 5;
        [DataMember(Name = "owner")] public DikozemieClanName Owner;
        [DataMember(Name = "lord")] public string Lord = string.Empty;
        // When the client got it.
        internal DateTime ReceivedUtc;
    }

    [DataContract]
    internal sealed class DikozemieEconomyState
    {
        [DataMember(Name = "ok")] public bool Ok;
        [DataMember(Name = "code")] public string Code = string.Empty;
        [DataMember(Name = "message")] public string Message = string.Empty;
        [DataMember(Name = "serverTime")] public double ServerTime;
        [DataMember(Name = "player")] public string Player = string.Empty;
        [DataMember(Name = "coins")] public int Coins;
        [DataMember(Name = "cargo")] public Dictionary<string, int> Cargo = new Dictionary<string, int>();
        [DataMember(Name = "basis")] public Dictionary<string, double> Basis = new Dictionary<string, double>();
        [DataMember(Name = "load")] public int Load;
        [DataMember(Name = "capacity")] public int Capacity = 1;
        [DataMember(Name = "workshops")] public List<DikozemieWorkshopInfo> Workshops = new List<DikozemieWorkshopInfo>();
        [DataMember(Name = "workshopsYesterday")] public double WorkshopsYesterday;
        [DataMember(Name = "caravans")] public List<DikozemieCaravanInfo> Caravans = new List<DikozemieCaravanInfo>();
        [DataMember(Name = "commendas")] public List<DikozemieCommendaInfo> Commendas = new List<DikozemieCommendaInfo>();
        [DataMember(Name = "contracts")] public List<DikozemieContractInfo> Contracts = new List<DikozemieContractInfo>();
        [DataMember(Name = "story")] public DikozemieStoryInfo Story;
        [DataMember(Name = "ledger")] public List<DikozemieLedgerRow> Ledger = new List<DikozemieLedgerRow>();
        [DataMember(Name = "news")] public List<DikozemieLogLine> News = new List<DikozemieLogLine>();
        // The watch (plan, «Античит»): the observer's warnings to this hero
        // and whether he himself keeps watch.
        [DataMember(Name = "notices")] public List<DikozemieNotice> Notices = new List<DikozemieNotice>();
        [DataMember(Name = "observer")] public bool Observer;
        // An observer whose key no battle server has vouched for yet.
        [DataMember(Name = "observerPending")] public bool ObserverPending;
        [DataMember(Name = "results")] public List<DikozemieEconomyOpResult> Results;
        [DataMember(Name = "market")] public DikozemieMarketInfo Market;
        [DataMember(Name = "offers")] public List<DikozemieContractInfo> Offers;
        [DataMember(Name = "nextDay")] public double NextDay;
        [DataMember(Name = "bought")] public int Bought;
        [DataMember(Name = "paid")] public int Paid;
        [DataMember(Name = "sold")] public int Sold;
        [DataMember(Name = "got")] public int Got;
        [DataMember(Name = "profit")] public double Profit;
    }

    [DataContract]
    internal sealed class DikozemieKnownPrice
    {
        [DataMember(Name = "buy")] public int Buy;
        [DataMember(Name = "sell")] public int Sell;
        [DataMember(Name = "at")] public long At;
    }

    // Goods, towns and workshops as the server knows them, from the same
    // file the server runs on (Tools/build_economy_world.py).
    [DataContract]
    internal sealed class DikozemieEconomyGood
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "unit")] public string Unit = string.Empty;
        [DataMember(Name = "group")] public string Group = string.Empty;
        [DataMember(Name = "base")] public int Base;
        [DataMember(Name = "about")] public string About = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieEconomyWorkshopType
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "price")] public int Price;
        [DataMember(Name = "inputs")] public Dictionary<string, double> Inputs = new Dictionary<string, double>();
        [DataMember(Name = "outputs")] public Dictionary<string, double> Outputs = new Dictionary<string, double>();
        [DataMember(Name = "wages")] public double Wages;
        [DataMember(Name = "about")] public string About = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieEconomyTown
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "culture")] public string Culture = string.Empty;
        [DataMember(Name = "port")] public bool Port;
        [DataMember(Name = "x")] public double X;
        [DataMember(Name = "y")] public double Y;
        [DataMember(Name = "produce")] public Dictionary<string, double> Produce = new Dictionary<string, double>();
        [DataMember(Name = "workshops")] public List<string> Workshops = new List<string>();
    }

    [DataContract]
    internal sealed class DikozemieEconomyPlace
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "town")] public string Town = string.Empty;
        [DataMember(Name = "kind")] public string Kind = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieEconomyWorld
    {
        [DataMember(Name = "policy")] public string Policy = string.Empty;
        [DataMember(Name = "goods")] public List<DikozemieEconomyGood> Goods = new List<DikozemieEconomyGood>();
        [DataMember(Name = "workshopTypes")] public List<DikozemieEconomyWorkshopType> WorkshopTypes =
            new List<DikozemieEconomyWorkshopType>();
        [DataMember(Name = "towns")] public List<DikozemieEconomyTown> Towns = new List<DikozemieEconomyTown>();
        [DataMember(Name = "villages")] public List<DikozemieEconomyPlace> Villages = new List<DikozemieEconomyPlace>();
        [DataMember(Name = "castles")] public List<DikozemieEconomyPlace> Castles = new List<DikozemieEconomyPlace>();
        [DataMember(Name = "packAnimals")] public List<string> PackAnimals = new List<string>();
    }

    internal static class DikozemieEconomy
    {
        private const string DefaultUrl = "https://136-234-5-140.sslip.io/api/v1/";
        private const int MaxOutbox = 300;

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        private static readonly ConcurrentQueue<Action> Completed = new ConcurrentQueue<Action>();
        private static readonly object Gate = new object();
        private static readonly List<DikozemieEconomyOp> Outbox = new List<DikozemieEconomyOp>();
        private static readonly List<KeyValuePair<DateTime, int>> PendingBattleCredits =
            new List<KeyValuePair<DateTime, int>>();
        private static readonly Dictionary<string, DikozemieMarketInfo> Markets =
            new Dictionary<string, DikozemieMarketInfo>(StringComparer.Ordinal);
        private static Dictionary<string, Dictionary<string, DikozemieKnownPrice>> _known;
        private static DikozemieEconomyWorld _world;
        private static Dictionary<string, DikozemieEconomyGood> _goods;
        private static Dictionary<string, DikozemieEconomyTown> _towns;
        private static string _key;
        private static string _playerId;
        private static string _playerName;
        private static bool _loaded;
        private static bool _busy;
        private static bool _opsInFlight;
        private static DateTime _nextHello;
        private static DateTime _nextFlush;
        private static DateTime _nextRefresh;
        private static int _failures;
        private static double _serverOffset;

        internal static DikozemieEconomyStatus Status { get; private set; } =
            DikozemieEconomyStatus.Waiting;
        internal static DikozemieEconomyState State { get; private set; }
        internal static string LastError { get; private set; } = string.Empty;

        // The observer's word when the treasury refuses this hero.
        internal static string BanText { get; private set; } = string.Empty;

        // Kept while the ban is asked again (Connecting) or the treasury is
        // out of reach (Offline): only an answer that lets him in lifts it.
        private static bool _banned;

        internal static bool IsBanned => _banned;
        internal static int Revision { get; private set; }

        internal static bool Enabled =>
            !string.Equals(Environment.GetEnvironmentVariable("DIKOZEMIE_ECONOMY"), "0", StringComparison.Ordinal);

        internal static bool IsOnline => Status == DikozemieEconomyStatus.Online && State != null;

        private static string Folder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dikozemie", "arena");

        // Files that belong to one trade server (the queue, the opening
        // mark, the prices seen): a test server never touches the real one's.
        private static string ServerFolder
        {
            get
            {
                string host;
                try
                {
                    var uri = new Uri(BaseUrl);
                    host = uri.Host + (uri.IsDefaultPort ? string.Empty : "-" + uri.Port);
                }
                catch (Exception)
                {
                    host = "default";
                }
                var safe = new StringBuilder();
                foreach (char symbol in host)
                {
                    safe.Append(char.IsLetterOrDigit(symbol) || symbol == '-' || symbol == '.' ? symbol : '-');
                }
                return Path.Combine(Folder, "economy", safe.ToString());
            }
        }

        // The download page of the mod is on this host.
        internal static string SiteBaseUrl => BaseUrl;

        private static string BaseUrl
        {
            get
            {
                string configured = Environment.GetEnvironmentVariable("DIKOZEMIE_ECONOMY_URL");
                if (string.IsNullOrWhiteSpace(configured))
                {
                    try
                    {
                        string path = Path.Combine(Folder, "economy-url.txt");
                        if (File.Exists(path))
                        {
                            configured = File.ReadAllText(path).Trim();
                        }
                    }
                    catch (Exception)
                    {
                        configured = null;
                    }
                }
                string url = string.IsNullOrWhiteSpace(configured) ? DefaultUrl : configured.Trim();
                return url.EndsWith("/", StringComparison.Ordinal) ? url : url + "/";
            }
        }

        // ---- status line ----------------------------------------------------

        internal static string StatusText
        {
            get
            {
                switch (Status)
                {
                    case DikozemieEconomyStatus.Online:
                        return "Казна на сервере: связь есть.";
                    case DikozemieEconomyStatus.Connecting:
                    case DikozemieEconomyStatus.Waiting:
                        return "Казна: связываемся с сервером…";
                    case DikozemieEconomyStatus.DeviceUnconfirmed:
                        return "Казна закрыта: этот компьютер ещё не подтверждён. Сыграйте один бой на арене — " +
                            "сервер боя узнает вас и откроет казну.";
                    case DikozemieEconomyStatus.Banned:
                        return string.IsNullOrEmpty(BanText) ? "Казна закрыта наблюдателем." : BanText;
                    case DikozemieEconomyStatus.Off:
                        return "Казна отключена.";
                    default:
                        return "Казна: нет связи с сервером. Торговый двор, мастерские и караваны ждут связи; " +
                            "бои и дорога идут, расчёт придёт позже.";
                }
            }
        }

        internal static double ServerNow =>
            (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds + _serverOffset;

        // ---- the world file -------------------------------------------------

        internal static DikozemieEconomyWorld World
        {
            get
            {
                if (_world != null)
                {
                    return _world;
                }
                _world = new DikozemieEconomyWorld();
                try
                {
                    string path = Path.Combine(
                        ModuleHelper.GetModuleFullPath(DikozemieSubModule.ModuleId),
                        "ModuleData", "dikozemie_economy.json");
                    _world = Parse<DikozemieEconomyWorld>(File.ReadAllText(path, Encoding.UTF8)) ??
                        new DikozemieEconomyWorld();
                }
                catch (Exception error)
                {
                    Console.WriteLine("DikozemieOnline: economy world unavailable: " + error.Message);
                }
                _goods = _world.Goods.ToDictionary(g => g.Id, StringComparer.Ordinal);
                _towns = _world.Towns.ToDictionary(t => t.Id, StringComparer.Ordinal);
                return _world;
            }
        }

        internal static DikozemieEconomyGood Good(string id)
        {
            _ = World;
            return id != null && _goods.TryGetValue(id, out DikozemieEconomyGood good) ? good : null;
        }

        internal static string GoodName(string id) => Good(id)?.Name ?? id ?? string.Empty;

        internal static DikozemieEconomyTown Town(string id)
        {
            _ = World;
            return id != null && _towns.TryGetValue(id, out DikozemieEconomyTown town) ? town : null;
        }

        internal static DikozemieEconomyWorkshopType WorkshopType(string id) =>
            World.WorkshopTypes.FirstOrDefault(w => w.Id == id);

        internal static bool IsPackAnimal(string itemId) =>
            !string.IsNullOrEmpty(itemId) && World.PackAnimals.Contains(itemId);

        // ---- reporting coin changes ---------------------------------------

        // Called by DikozemieArmoryProgression for every change of the local
        // count. Kinds are the server's (see LEDGER_NAMES there).
        internal static void Report(string kind, int delta, string note, string reference = "", string item = "")
        {
            if (!Enabled || delta == 0 && kind != "item-sell")
            {
                return;
            }
            EnsureLoaded();
            lock (Gate)
            {
                if (kind == "battle")
                {
                    // Arena coins are paid by the battle server itself; the
                    // local count shows them until the ledger has them.
                    PendingBattleCredits.Add(new KeyValuePair<DateTime, int>(DateTime.UtcNow, delta));
                    _nextRefresh = DateTime.UtcNow.AddSeconds(4);
                    return;
                }
                if (kind == "refund")
                {
                    // Undo a spend that has not left yet; otherwise the server
                    // counts it as a small claim.
                    DikozemieEconomyOp spent = Outbox.LastOrDefault(op => op.Delta == -delta && !_sending.Contains(op.Id));
                    if (spent != null)
                    {
                        Outbox.Remove(spent);
                        SaveOutbox();
                        return;
                    }
                    kind = "misc";
                }
                Outbox.Add(new DikozemieEconomyOp
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Kind = kind,
                    Amount = Math.Abs(delta),
                    Delta = delta,
                    Note = note ?? string.Empty,
                    Ref = reference ?? string.Empty,
                    Item = item ?? string.Empty
                });
                while (Outbox.Count > MaxOutbox)
                {
                    Outbox.RemoveAt(0);
                }
                SaveOutbox();
                _nextFlush = DateTime.UtcNow.AddMilliseconds(400);
            }
        }

        // Not a coin change: the list of things the hero owns started over
        // (a new hero), or robbers took part of the cargo.
        internal static void ReportItemsReset(IEnumerable<string> items)
        {
            if (!Enabled)
            {
                return;
            }
            EnsureLoaded();
            lock (Gate)
            {
                Outbox.Add(new DikozemieEconomyOp
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Kind = "items-reset",
                    Items = items?.ToList() ?? new List<string>()
                });
                SaveOutbox();
            }
        }

        internal static void ReportCargoLost(double share, string note)
        {
            if (!Enabled || State == null || State.Load <= 0)
            {
                return;
            }
            EnsureLoaded();
            lock (Gate)
            {
                Outbox.Add(new DikozemieEconomyOp
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Kind = "cargo-lost",
                    Share = Math.Max(0.0, Math.Min(1.0, share)),
                    Note = note ?? string.Empty
                });
                SaveOutbox();
                // Show it at once; the server's answer confirms.
                foreach (string good in State.Cargo.Keys.ToList())
                {
                    int take = (int)Math.Ceiling(State.Cargo[good] * share);
                    State.Cargo[good] -= take;
                    State.Load -= take;
                    if (State.Cargo[good] <= 0)
                    {
                        State.Cargo.Remove(good);
                    }
                }
                Revision++;
                _nextFlush = DateTime.UtcNow;
            }
        }

        // Pack weight for the travel map: 0 empty, 1 full.
        internal static float LoadShare =>
            State == null || State.Capacity <= 0 ? 0f : Math.Min(1f, State.Load / (float)State.Capacity);

        internal static int CargoValue
        {
            get
            {
                if (State == null)
                {
                    return 0;
                }
                return State.Cargo.Sum(pair => (Good(pair.Key)?.Base ?? 0) * pair.Value);
            }
        }

        // ---- the clock ------------------------------------------------------

        internal static void Tick()
        {
            if (!Enabled)
            {
                Status = DikozemieEconomyStatus.Off;
                return;
            }
            while (Completed.TryDequeue(out Action action))
            {
                try
                {
                    action();
                }
                catch (Exception error)
                {
                    Console.WriteLine("DikozemieOnline: economy callback failed: " + error);
                }
            }
            if (_playerId == null)
            {
                var client = NetworkMain.GameClient;
                if (client == null || !client.PlayerID.IsValid || !client.LoggedIn)
                {
                    return;
                }
                _playerId = client.PlayerID.ToString();
                _playerName = client.Name ?? string.Empty;
                _nextHello = DateTime.UtcNow;
            }
            EnsureLoaded();
            DateTime now = DateTime.UtcNow;
            if (Status != DikozemieEconomyStatus.Online)
            {
                if (!_busy && now >= _nextHello)
                {
                    Hello();
                }
                return;
            }
            lock (Gate)
            {
                if (!_opsInFlight && Outbox.Count > 0 && now >= _nextFlush)
                {
                    FlushOps();
                    return;
                }
            }
            if (!_busy && now >= _nextRefresh)
            {
                Refresh();
            }
        }

        // After a battle the server may have confirmed this computer.
        internal static void NoteBattlePlayed()
        {
            if (Status == DikozemieEconomyStatus.DeviceUnconfirmed)
            {
                _nextHello = DateTime.UtcNow.AddSeconds(3);
            }
            _nextRefresh = DateTime.UtcNow.AddSeconds(3);
        }

        internal static void RequestRefresh() => _nextRefresh = DateTime.UtcNow;

        // ---- requests -------------------------------------------------------

        private static readonly HashSet<string> _sending = new HashSet<string>(StringComparer.Ordinal);

        private static void Hello()
        {
            _busy = true;
            Status = Status == DikozemieEconomyStatus.DeviceUnconfirmed || Status == DikozemieEconomyStatus.Banned
                ? Status
                : DikozemieEconomyStatus.Connecting;
            bool firstTime = !File.Exists(Path.Combine(ServerFolder, "opened.txt"));
            var body = new StringBuilder();
            body.Append("{\"playerId\":").Append(Json(_playerId))
                .Append(",\"key\":").Append(Json(Key))
                .Append(",\"name\":").Append(Json(_playerName))
                .Append(",\"at\":").Append(Json(DikozemieTown.Here?.Id ?? string.Empty));
            if (firstTime)
            {
                // What the old file says the hero has: brought over once.
                body.Append(",\"opening\":{\"coins\":").Append(DikozemieArmoryProgression.LocalCoins)
                    .Append(",\"items\":[")
                    .Append(string.Join(",", DikozemieHeroProfile.OwnedItemsSnapshot().Select(Json)))
                    .Append("],\"castles\":[")
                    .Append(string.Join(",", (DikozemieTravelState.Current?.CastlePasses ?? new List<string>()).Select(Json)))
                    .Append("]}");
            }
            body.Append('}');
            Send(HttpMethod.Post, "hello", body.ToString(), (state, status, error) =>
            {
                _busy = false;
                if (state != null && state.Ok)
                {
                    if (firstTime)
                    {
                        try
                        {
                            Directory.CreateDirectory(ServerFolder);
                            File.WriteAllText(Path.Combine(ServerFolder, "opened.txt"),
                                DateTime.UtcNow.ToString("o") + " " + BaseUrl);
                        }
                        catch (Exception)
                        {
                        }
                        lock (Gate)
                        {
                            // Anything spent before the first answer is
                            // already inside the coins brought over.
                            Outbox.RemoveAll(op => op.Kind != "items-reset");
                            SaveOutbox();
                        }
                    }
                    _failures = 0;
                    Status = DikozemieEconomyStatus.Online;
                    Accept(state);
                    Trace("economy hello ok coins=" + state.Coins + " capacity=" + state.Capacity +
                        " opening=" + firstTime);
                    return;
                }
                string code = state?.Code ?? string.Empty;
                if (code == "device-unconfirmed")
                {
                    Status = DikozemieEconomyStatus.DeviceUnconfirmed;
                    _nextHello = DateTime.UtcNow.AddSeconds(60);
                }
                else if (code == "banned")
                {
                    Status = DikozemieEconomyStatus.Banned;
                    _banned = true;
                    BanText = state?.Message ?? string.Empty;
                    _nextHello = DateTime.UtcNow.AddSeconds(120);
                }
                else if (_banned)
                {
                    // The treasury is out of reach: the ban stands until it
                    // says otherwise.
                    Status = DikozemieEconomyStatus.Banned;
                    _nextHello = DateTime.UtcNow.AddSeconds(120);
                }
                else
                {
                    Status = DikozemieEconomyStatus.Offline;
                    _failures++;
                    _nextHello = DateTime.UtcNow.AddSeconds(Math.Min(120, 10 * _failures));
                }
                LastError = state?.Message ?? error ?? string.Empty;
                Trace("economy hello failed status=" + status + " code=" + code + " " + LastError);
            });
        }

        private static void FlushOps()
        {
            List<DikozemieEconomyOp> batch = Outbox.Take(30).ToList();
            foreach (DikozemieEconomyOp op in batch)
            {
                _sending.Add(op.Id);
            }
            _opsInFlight = true;
            var body = new StringBuilder("{\"ops\":[");
            for (int index = 0; index < batch.Count; index++)
            {
                DikozemieEconomyOp op = batch[index];
                if (index > 0)
                {
                    body.Append(',');
                }
                body.Append("{\"id\":").Append(Json(op.Id))
                    .Append(",\"kind\":").Append(Json(op.Kind))
                    .Append(",\"amount\":").Append(op.Amount)
                    .Append(",\"note\":").Append(Json(op.Note))
                    .Append(",\"ref\":").Append(Json(op.Ref))
                    .Append(",\"item\":").Append(Json(op.Item))
                    .Append(",\"share\":").Append(op.Share.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
                if (op.Items != null)
                {
                    body.Append(",\"items\":[").Append(string.Join(",", op.Items.Select(Json))).Append(']');
                }
                body.Append('}');
            }
            body.Append("]}");
            Send(HttpMethod.Post, "ops", body.ToString(), (state, status, error) =>
            {
                lock (Gate)
                {
                    _opsInFlight = false;
                    foreach (DikozemieEconomyOp op in batch)
                    {
                        _sending.Remove(op.Id);
                    }
                    if (state == null || !state.Ok)
                    {
                        _nextFlush = DateTime.UtcNow.AddSeconds(15);
                        if (status == 0 || status >= 500)
                        {
                            Status = DikozemieEconomyStatus.Offline;
                            _nextHello = DateTime.UtcNow.AddSeconds(15);
                        }
                        return;
                    }
                    var answered = new HashSet<string>(
                        (state.Results ?? new List<DikozemieEconomyOpResult>()).Select(r => r.Id),
                        StringComparer.Ordinal);
                    foreach (DikozemieEconomyOpResult result in state.Results ?? new List<DikozemieEconomyOpResult>())
                    {
                        DikozemieEconomyOp op = batch.FirstOrDefault(o => o.Id == result.Id);
                        if (op == null)
                        {
                            continue;
                        }
                        if (!result.Ok && result.Code == "no-coins")
                        {
                            Notify("Казна не подтвердила расход: " + Describe(op) + " — не хватило монет на сервере.");
                        }
                        else if (!result.Ok && result.Code == "not-owned")
                        {
                            Notify("Казна не приняла продажу: " + Describe(op) + " — этой вещи у героя на сервере нет.");
                        }
                        else if (result.Cut > 0)
                        {
                            Notify("Казна: из " + op.Amount + " монет за «" + Describe(op) + "» зачтено " +
                                (op.Amount - result.Cut) + " — предел заработка за час.");
                        }
                    }
                    Outbox.RemoveAll(op => answered.Contains(op.Id));
                    SaveOutbox();
                }
                Accept(state);
            });
        }

        private static void Refresh()
        {
            _busy = true;
            _nextRefresh = DateTime.UtcNow.AddSeconds(45);
            Send(HttpMethod.Get, "state", null, (state, status, error) =>
            {
                _busy = false;
                if (state != null && state.Ok)
                {
                    Accept(state);
                }
                else if (status == 403 && state?.Code == "device-unconfirmed")
                {
                    Status = DikozemieEconomyStatus.DeviceUnconfirmed;
                }
                else if (status == 403 && state?.Code == "banned")
                {
                    // Banned in the middle of a session: the Arena closes now,
                    // the treasury is asked again in two minutes.
                    Status = DikozemieEconomyStatus.Banned;
                    _banned = true;
                    BanText = state.Message ?? string.Empty;
                    _nextHello = DateTime.UtcNow.AddSeconds(120);
                    Revision++;
                }
                else if (status == 0 || status >= 500)
                {
                    Status = DikozemieEconomyStatus.Offline;
                    _nextHello = DateTime.UtcNow.AddSeconds(15);
                }
            });
        }

        // A screen asks: the answer comes on the main thread.
        internal static void Market(string node, Action<DikozemieMarketInfo, string> done)
        {
            if (!IsOnline)
            {
                done(null, StatusText);
                return;
            }
            Send(HttpMethod.Get, "market?at=" + Uri.EscapeDataString(node ?? string.Empty), null,
                (state, status, error) =>
                {
                    if (state == null || !state.Ok)
                    {
                        done(null, state?.Message ?? error ?? "Нет ответа.");
                        return;
                    }
                    // The market answer is the market itself, not a player state.
                    done(null, "unexpected");
                }, raw =>
                {
                    DikozemieMarketInfo market = Parse<DikozemieMarketInfo>(raw);
                    if (market != null)
                    {
                        market.ReceivedUtc = DateTime.UtcNow;
                        Markets[market.Node] = market;
                        Remember(market);
                    }
                    return market;
                }, done);
        }

        internal static DikozemieMarketInfo CachedMarket(string node) =>
            node != null && Markets.TryGetValue(node, out DikozemieMarketInfo market) ? market : null;

        internal static void Offers(string node, Action<List<DikozemieContractInfo>, string> done)
        {
            if (!IsOnline)
            {
                done(null, StatusText);
                return;
            }
            Send(HttpMethod.Get, "offers?at=" + Uri.EscapeDataString(node ?? string.Empty), null,
                (state, status, error) => done(state?.Offers, state?.Ok == true ? null : state?.Message ?? error));
        }

        // Every action that changes what the hero has. The answer is the new
        // state (and for trade the new market).
        internal static void Act(string path, string body, Action<DikozemieEconomyState, string> done)
        {
            if (!IsOnline)
            {
                done?.Invoke(null, StatusText);
                return;
            }
            Send(HttpMethod.Post, path, body, (state, status, error) =>
            {
                if (state != null && state.Ok)
                {
                    Accept(state);
                    if (state.Market != null)
                    {
                        state.Market.ReceivedUtc = DateTime.UtcNow;
                        Markets[state.Market.Node] = state.Market;
                        Remember(state.Market);
                    }
                    done?.Invoke(state, null);
                    return;
                }
                if (status == 0 || status >= 500)
                {
                    Status = DikozemieEconomyStatus.Offline;
                    _nextHello = DateTime.UtcNow.AddSeconds(10);
                }
                done?.Invoke(null, state?.Message ?? error ?? "Нет ответа от казны.");
            });
        }

        internal static void Trade(string node, string good, int qty, Action<DikozemieEconomyState, string> done) =>
            Act("trade", "{\"at\":" + Json(node) + ",\"good\":" + Json(good) + ",\"qty\":" + qty + "}", done);

        // ---- answers ----------------------------------------------------------

        private static void Accept(DikozemieEconomyState state)
        {
            // An answer that lets him in: no ban (any more).
            _banned = false;
            BanText = string.Empty;
            State = state;
            DikozemieWatch.OnState(state);
            _serverOffset = state.ServerTime -
                (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            Status = DikozemieEconomyStatus.Online;
            Revision++;
            int pending;
            lock (Gate)
            {
                // Battle coins the ledger already has are no longer pending.
                DateTime now = DateTime.UtcNow;
                PendingBattleCredits.RemoveAll(credit => (now - credit.Key).TotalSeconds > 180);
                foreach (DikozemieLedgerRow row in state.Ledger.Where(r => r.Kind == "arena" || r.Kind == "training" || r.Kind == "naval"))
                {
                    DateTime at = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(row.At - _serverOffset);
                    int index = PendingBattleCredits.FindIndex(credit =>
                        credit.Value == row.Amount && at >= credit.Key.AddSeconds(-90));
                    if (index >= 0 && !_matchedLedger.Contains(row.Id))
                    {
                        _matchedLedger.Add(row.Id);
                        PendingBattleCredits.RemoveAt(index);
                    }
                }
                pending = Outbox.Where(op => op.Kind != "items-reset" && op.Kind != "cargo-lost").Sum(op => op.Delta) +
                    PendingBattleCredits.Sum(credit => credit.Value);
            }
            DikozemieArmoryProgression.SetFromServer(Math.Max(0, state.Coins + pending));
            if (state.News != null && state.News.Count > 0)
            {
                foreach (DikozemieLogLine line in state.News)
                {
                    Notify(line.Text);
                }
                Send(HttpMethod.Post, "news/read", "{}", (s, status, error) => { });
            }
        }

        private static readonly HashSet<long> _matchedLedger = new HashSet<long>();

        private static void Notify(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }
            InformationManager.DisplayMessage(new InformationMessage(text, new Color(0.95f, 0.80f, 0.45f)));
        }

        private static string Describe(DikozemieEconomyOp op) =>
            string.IsNullOrEmpty(op.Note) ? op.Kind : op.Note;

        // ---- what the hero has seen ------------------------------------------

        private static void Remember(DikozemieMarketInfo market)
        {
            if (market == null || string.IsNullOrEmpty(market.Town))
            {
                return;
            }
            LoadKnown();
            long at = (long)ServerNow;
            var row = new Dictionary<string, DikozemieKnownPrice>(StringComparer.Ordinal);
            foreach (DikozemieGoodRow good in market.Goods)
            {
                row[good.Good] = new DikozemieKnownPrice { Buy = good.Buy, Sell = good.Sell, At = at };
            }
            _known[market.Town] = row;
            try
            {
                Directory.CreateDirectory(ServerFolder);
                File.WriteAllText(Path.Combine(ServerFolder, "prices.json"), Write(_known), Encoding.UTF8);
            }
            catch (Exception)
            {
            }
        }

        // Where the hero last saw a good dearer or cheaper than here.
        internal static IEnumerable<KeyValuePair<string, DikozemieKnownPrice>> Known(string good)
        {
            LoadKnown();
            foreach (KeyValuePair<string, Dictionary<string, DikozemieKnownPrice>> town in _known)
            {
                if (town.Value.TryGetValue(good, out DikozemieKnownPrice price))
                {
                    yield return new KeyValuePair<string, DikozemieKnownPrice>(town.Key, price);
                }
            }
        }

        private static void LoadKnown()
        {
            if (_known != null)
            {
                return;
            }
            _known = new Dictionary<string, Dictionary<string, DikozemieKnownPrice>>(StringComparer.Ordinal);
            try
            {
                string path = Path.Combine(ServerFolder, "prices.json");
                if (File.Exists(path))
                {
                    _known = Parse<Dictionary<string, Dictionary<string, DikozemieKnownPrice>>>(
                        File.ReadAllText(path, Encoding.UTF8)) ?? _known;
                }
            }
            catch (Exception)
            {
            }
        }

        // ---- key ------------------------------------------------------------

        internal static string Key
        {
            get
            {
                if (_key != null)
                {
                    return _key;
                }
                string path = Path.Combine(Folder, "economy-key.txt");
                try
                {
                    if (File.Exists(path))
                    {
                        string saved = File.ReadAllText(path).Trim();
                        if (saved.Length >= 32)
                        {
                            _key = saved;
                            return _key;
                        }
                    }
                    byte[] bytes = new byte[32];
                    using (var random = RandomNumberGenerator.Create())
                    {
                        random.GetBytes(bytes);
                    }
                    _key = string.Concat(bytes.Select(b => b.ToString("x2")));
                    Directory.CreateDirectory(Folder);
                    File.WriteAllText(path, _key);
                }
                catch (Exception error)
                {
                    Console.WriteLine("DikozemieOnline: economy key unavailable: " + error.Message);
                    _key = _key ?? Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                }
                return _key;
            }
        }

        // What the battle server passes on to confirm this computer: the
        // key itself never leaves for the battle server.
        internal static string KeyHash
        {
            get
            {
                if (!Enabled)
                {
                    return string.Empty;
                }
                using (var sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes("dikozemie-economy:" + Key));
                    return string.Concat(hash.Select(b => b.ToString("x2")));
                }
            }
        }

        // ---- plumbing -------------------------------------------------------

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }
            _loaded = true;
            try
            {
                string path = Path.Combine(ServerFolder, "outbox.json");
                if (File.Exists(path))
                {
                    List<DikozemieEconomyOp> saved = Parse<List<DikozemieEconomyOp>>(File.ReadAllText(path, Encoding.UTF8));
                    if (saved != null)
                    {
                        Outbox.AddRange(saved.Where(op => op != null && !string.IsNullOrEmpty(op.Id)));
                    }
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: economy outbox unreadable: " + error.Message);
            }
        }

        private static void SaveOutbox()
        {
            try
            {
                Directory.CreateDirectory(ServerFolder);
                string path = Path.Combine(ServerFolder, "outbox.json");
                string temp = path + ".tmp";
                File.WriteAllText(temp, Write(Outbox), Encoding.UTF8);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                File.Move(temp, path);
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: economy outbox not saved: " + error.Message);
            }
        }

        // Stage 5: the shared world (DikozemieWorld) talks to the same trade
        // server with the same key. The answer comes raw, on the main thread.
        internal static bool CanAsk => Enabled && _playerId != null &&
            (Status == DikozemieEconomyStatus.Online || Status == DikozemieEconomyStatus.Offline);

        internal static string PlayerId => _playerId ?? string.Empty;

        internal static void Request(HttpMethod method, string path, string body, Action<int, string, string> done)
        {
            string url = BaseUrl + path;
            string auth = _playerId == null ? null : "Dikozemie " + _playerId + ":" + Key;
            Task.Run(async () =>
            {
                int status = 0;
                string raw = null;
                string error = null;
                try
                {
                    using (var request = new HttpRequestMessage(method, url))
                    {
                        if (auth != null)
                        {
                            request.Headers.TryAddWithoutValidation("Authorization", auth);
                        }
                        if (body != null)
                        {
                            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                        }
                        using (HttpResponseMessage response = await Http.SendAsync(request).ConfigureAwait(false))
                        {
                            status = (int)response.StatusCode;
                            raw = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception failure)
                {
                    error = failure.GetBaseException().Message;
                }
                Completed.Enqueue(() => done(status, raw, error));
            });
        }

        internal static T ParseJson<T>(string raw) where T : class => raw == null ? null : Parse<T>(raw);

        private static void Send(HttpMethod method, string path, string body,
            Action<DikozemieEconomyState, int, string> done)
        {
            Send<object>(method, path, body, done, null, null);
        }

        // One request. The callback runs on the main thread in Tick().
        private static void Send<T>(HttpMethod method, string path, string body,
            Action<DikozemieEconomyState, int, string> done,
            Func<string, T> parseOther, Action<T, string> doneOther) where T : class
        {
            string url = BaseUrl + path;
            string auth = _playerId == null ? null : "Dikozemie " + _playerId + ":" + Key;
            Task.Run(async () =>
            {
                int status = 0;
                string raw = null;
                string error = null;
                try
                {
                    using (var request = new HttpRequestMessage(method, url))
                    {
                        if (auth != null)
                        {
                            request.Headers.TryAddWithoutValidation("Authorization", auth);
                        }
                        if (body != null)
                        {
                            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                        }
                        using (HttpResponseMessage response = await Http.SendAsync(request).ConfigureAwait(false))
                        {
                            status = (int)response.StatusCode;
                            raw = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception failure)
                {
                    error = "нет связи с казной (" + failure.GetBaseException().Message + ")";
                }
                Completed.Enqueue(() =>
                {
                    if (parseOther != null && status == 200 && raw != null)
                    {
                        T other = parseOther(raw);
                        doneOther?.Invoke(other, other == null ? "Ответ казны не читается." : null);
                        return;
                    }
                    DikozemieEconomyState state = raw == null ? null : Parse<DikozemieEconomyState>(raw);
                    if (parseOther != null)
                    {
                        doneOther?.Invoke(null, state?.Message ?? error ?? "Нет ответа от казны.");
                        return;
                    }
                    done(state, status, error);
                });
            });
        }

        internal static string Json(string value)
        {
            var text = new StringBuilder("\"");
            foreach (char symbol in value ?? string.Empty)
            {
                switch (symbol)
                {
                    case '"': text.Append("\\\""); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': text.Append("\\r"); break;
                    case '\t': text.Append("\\t"); break;
                    default:
                        if (symbol < 0x20)
                        {
                            text.Append("\\u").Append(((int)symbol).ToString("x4"));
                        }
                        else
                        {
                            text.Append(symbol);
                        }
                        break;
                }
            }
            return text.Append('"').ToString();
        }

        private static T Parse<T>(string raw) where T : class
        {
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(T),
                    new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(raw)))
                {
                    return serializer.ReadObject(stream) as T;
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: economy answer unreadable: " + error.Message);
                return null;
            }
        }

        private static string Write<T>(T value)
        {
            var serializer = new DataContractJsonSerializer(typeof(T),
                new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static void Trace(string line) => DikozemieLobbyShellIntegration.TraceButtonAction(line);
    }
}
