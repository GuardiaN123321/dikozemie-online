using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.ModuleManager;

namespace Dikozemie.Online
{
    // Customer, 2026-11-05: «карта ... не будет вот этих разных вкладок ...
    // это всё будет встроено в контекстное меню города»: tavern, castle
    // «за 500 монет», the main townsfolk, an arena in every town and the port
    // in port towns. Stage 1 of the plan.

    // What the market of the place sells.
    internal enum DikozemieMarketKind
    {
        // Out on the map: nobody to trade with.
        None,
        // Everything an ordinary town sells.
        Town,
        // The lord's armoury behind the castle gate: knightly gear.
        Armory,
        // Food, beasts and simple things.
        Village,
        // A pedlar met on the road: small things, a quarter dearer.
        Peddler
    }

    // The screen the player looks at, for the location line above it.
    internal enum DikozemiePlace
    {
        Map,
        Town,
        Arena,
        Port,
        Market,
        Hero,
        Inventory
    }

    [DataContract]
    internal sealed class DikozemieTownInfo
    {
        [DataMember(Name = "lore")] public string Lore = string.Empty;
        [DataMember(Name = "clan")] public string Clan = string.Empty;
        [DataMember(Name = "kingdom")] public string Kingdom = string.Empty;
        [DataMember(Name = "bound")] public string Bound = string.Empty;
        // Stage 6: the banner of the singleplayer lord (a banner code).
        [DataMember(Name = "banner")] public string Banner = string.Empty;
        // The singleplayer 3D scenes of the place for the town walk.
        [DataMember(Name = "scenes")] public DikozemieTownScenes Scenes = new DikozemieTownScenes();
    }

    [DataContract]
    internal sealed class DikozemieTownScenes
    {
        // The streets of a town, the village green or the castle yard.
        [DataMember(Name = "center")] public string Center = string.Empty;
        [DataMember(Name = "tavern")] public string Tavern = string.Empty;
        [DataMember(Name = "arena")] public string Arena = string.Empty;
        [DataMember(Name = "hall")] public string Hall = string.Empty;
        [DataMember(Name = "port")] public string Port = string.Empty;
        // The level of the walls: the streets show the buildings of it.
        [DataMember(Name = "wall")] public int Wall;
    }

    [DataContract]
    internal sealed class DikozemieCultureNames
    {
        [DataMember(Name = "male")] public List<string> Male = new List<string>();
        [DataMember(Name = "female")] public List<string> Female = new List<string>();
    }

    [DataContract]
    internal sealed class DikozemieTownDocument
    {
        [DataMember(Name = "settlements")]
        public Dictionary<string, DikozemieTownInfo> Settlements =
            new Dictionary<string, DikozemieTownInfo>();

        [DataMember(Name = "names")]
        public Dictionary<string, DikozemieCultureNames> Names =
            new Dictionary<string, DikozemieCultureNames>();
    }

    // One of the main people of a town, as in the singleplayer town menu.
    internal sealed class DikozemieNotable
    {
        internal string Name;
        internal string Role;
        internal string Speech;
        internal bool Female;
    }

    // Descriptions, owners and names from the singleplayer data of the game,
    // gathered by Tools/build_town_catalog.py into dikozemie_towns.json.
    internal static class DikozemieTownCatalog
    {
        private static DikozemieTownDocument _document;

        internal static DikozemieTownInfo Get(string nodeId)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(nodeId) &&
                _document.Settlements.TryGetValue(nodeId, out DikozemieTownInfo info)
                    ? info
                    : new DikozemieTownInfo();
        }

        internal static IReadOnlyList<string> Names(string culture, bool female)
        {
            EnsureLoaded();
            string key = DikozemieTown.NormalizeCulture(culture);
            if (!_document.Names.TryGetValue(key, out DikozemieCultureNames names) ||
                (female ? names.Female : names.Male).Count == 0)
            {
                _document.Names.TryGetValue("empire", out names);
            }
            List<string> list = female ? names?.Female : names?.Male;
            return list != null && list.Count > 0
                ? (IReadOnlyList<string>)list
                : new[] { female ? "Алена" : "Андрос" };
        }

        private static void EnsureLoaded()
        {
            if (_document != null)
            {
                return;
            }
            _document = new DikozemieTownDocument();
            try
            {
                string path = Path.Combine(
                    ModuleHelper.GetModuleFullPath(DikozemieSubModule.ModuleId),
                    "ModuleData",
                    "dikozemie_towns.json");
                var serializer = new DataContractJsonSerializer(
                    typeof(DikozemieTownDocument),
                    new DataContractJsonSerializerSettings
                    {
                        UseSimpleDictionaryFormat = true
                    });
                using (var stream = new MemoryStream(
                    Encoding.UTF8.GetBytes(File.ReadAllText(path, Encoding.UTF8))))
                {
                    _document = (DikozemieTownDocument)serializer.ReadObject(stream) ??
                        new DikozemieTownDocument();
                }
                if (_document.Settlements == null)
                {
                    _document.Settlements = new Dictionary<string, DikozemieTownInfo>();
                }
                if (_document.Names == null)
                {
                    _document.Names = new Dictionary<string, DikozemieCultureNames>();
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: town catalog unavailable: " + error.Message);
                _document = new DikozemieTownDocument();
            }
        }
    }

    internal static class DikozemieTown
    {
        // «Можно и пройти в замок там за 500 монет» (customer, 2026-11-05):
        // about two pounds, a bribe the size of a merchant house's yearly rent.
        internal const int CastleEntryPrice = 500;
        // From ten pounds up a thing is knightly gear: the lord's armoury
        // sells it, the town market does not.
        internal const int ArmoryThreshold = 2400;
        // A village sells food, beasts and cheap things.
        internal const int VillageThreshold = 60;
        // Ale for the regulars: a gallon of good ale cost 1–2 pence.
        internal const int RumorPrice = 2;
        // Local work is cheaper, things brought from afar dearer.
        internal const float LocalFactor = 0.9f;
        internal const float ImportedFactor = 1.1f;

        private static bool _armoryOpen;

        // Where the location line says the player is.
        internal static DikozemiePlace Place { get; set; } = DikozemiePlace.Map;

        // The map is asked to show (true) or close (false) the town screen:
        // from the location line or when the Arena tab opens the map.
        private static bool? _townScreenRequest;

        internal static void RequestTownScreen(bool open) => _townScreenRequest = open;

        internal static bool? ConsumeTownScreenRequest()
        {
            bool? request = _townScreenRequest;
            _townScreenRequest = null;
            return request;
        }

        // The town screen is on (the map widget keeps this current).
        internal static bool IsTownScreenOpen { get; set; }

        // The settlement the hero stands in, or null on the road.
        internal static DikozemieLobbyMapNode Here
        {
            get
            {
                DikozemieTravelSave save = DikozemieTravelState.Current;
                if (save == null || save.HasTarget && string.IsNullOrEmpty(save.NodeId))
                {
                    return null;
                }
                return DikozemieLobbyMapCatalog.Find(save.NodeId);
            }
        }

        internal static bool IsTown(DikozemieLobbyMapNode node) =>
            string.Equals(node?.Kind, "town", StringComparison.OrdinalIgnoreCase);

        internal static bool IsCastle(DikozemieLobbyMapNode node) =>
            string.Equals(node?.Kind, "castle", StringComparison.OrdinalIgnoreCase);

        internal static bool IsVillage(DikozemieLobbyMapNode node) =>
            node != null && !IsTown(node) && !IsCastle(node);

        internal static string NormalizeCulture(string culture)
        {
            string value = (culture ?? string.Empty).Trim().ToLowerInvariant();
            if (value.StartsWith("culture.", StringComparison.Ordinal))
            {
                value = value.Substring("culture.".Length);
            }
            return value == "nords" ? "nord" : value;
        }

        internal static string DisplayName(DikozemieLobbyMapNode node)
        {
            if (node == null)
            {
                return string.Empty;
            }
            string name = !string.IsNullOrWhiteSpace(node.NameRu) ? node.NameRu : node.Name;
            return string.IsNullOrWhiteSpace(name) ? node.Id : name;
        }

        // ---- Market ----------------------------------------------------

        // The lord's armoury is open while the hero stands at a castle he
        // may enter and asked for it from the castle menu.
        internal static void OpenArmory(bool open) => _armoryOpen = open;

        internal const int PeddlerThreshold = 240;
        internal const float PeddlerFactor = 1.25f;

        internal static DikozemieMarketKind MarketKind
        {
            get
            {
                if (DikozemieRoads.PeddlerOpen)
                {
                    return DikozemieMarketKind.Peddler;
                }
                if (DikozemieRoads.IsCaptive)
                {
                    return DikozemieMarketKind.None;
                }
                DikozemieLobbyMapNode here = Here;
                if (here == null)
                {
                    return DikozemieMarketKind.None;
                }
                if (_armoryOpen && HasCastlePass(here))
                {
                    return DikozemieMarketKind.Armory;
                }
                if (IsTown(here))
                {
                    return DikozemieMarketKind.Town;
                }
                return IsVillage(here) ? DikozemieMarketKind.Village : DikozemieMarketKind.None;
            }
        }

        internal static string MarketTitle
        {
            get
            {
                DikozemieLobbyMapNode here = Here;
                switch (MarketKind)
                {
                    case DikozemieMarketKind.Town:
                        return "РЫНОК · " + DisplayName(here).ToUpperInvariant();
                    case DikozemieMarketKind.Armory:
                        return "ОРУЖЕЙНАЯ · " + DisplayName(here).ToUpperInvariant();
                    case DikozemieMarketKind.Village:
                        return "СЕЛЬСКИЙ РЫНОК";
                    case DikozemieMarketKind.Peddler:
                        return "ТОРГОВЕЦ НА ДОРОГЕ";
                    default:
                        return "РЫНОК";
                }
            }
        }

        // What the market of the place has on sale.
        internal static bool IsSoldHere(ItemObject item, int basePrice)
        {
            if (item == null || basePrice <= 0)
            {
                return false;
            }
            bool goods = DikozemieItemStore.GetCategory(item) == DikozemieStoreCategory.Goods;
            switch (MarketKind)
            {
                case DikozemieMarketKind.Town:
                    return goods || basePrice < ArmoryThreshold;
                case DikozemieMarketKind.Armory:
                    return !goods && basePrice >= ArmoryThreshold;
                case DikozemieMarketKind.Village:
                    return goods || item.ItemType == ItemObject.ItemTypeEnum.Animal ||
                        basePrice <= VillageThreshold;
                case DikozemieMarketKind.Peddler:
                    // What fits on one mule: about one cheap thing in twenty,
                    // a different pack for every pedlar.
                    return basePrice <= PeddlerThreshold &&
                        item.ItemType != ItemObject.ItemTypeEnum.Horse &&
                        item.ItemType != ItemObject.ItemTypeEnum.Animal &&
                        item.ItemType != ItemObject.ItemTypeEnum.HorseHarness &&
                        (uint)(StableHash(item.StringId) ^ DikozemieRoads.PeddlerSeed) % 20u == 0u;
                default:
                    return false;
            }
        }

        // «Цены по культуре города: местное дешевле, привозное дороже».
        internal static float PriceFactor(ItemObject item)
        {
            if (MarketKind == DikozemieMarketKind.Peddler)
            {
                return PeddlerFactor;
            }
            DikozemieLobbyMapNode here = Here;
            if (item == null || here == null)
            {
                return 1f;
            }
            string itemCulture = NormalizeCulture(item.Culture?.StringId);
            if (string.IsNullOrEmpty(itemCulture) ||
                itemCulture == "neutral_culture" || itemCulture == "neutral")
            {
                return 1f;
            }
            return itemCulture == NormalizeCulture(here.Culture) ? LocalFactor : ImportedFactor;
        }

        internal static string MarketHint
        {
            get
            {
                DikozemieLobbyMapNode here = Here;
                switch (MarketKind)
                {
                    case DikozemieMarketKind.Town:
                        return "Местное дешевле на 10%, привозное дороже на 10%. " +
                            "Снаряжение знати дороже " + ArmoryThreshold +
                            " — в оружейной замка.";
                    case DikozemieMarketKind.Armory:
                        return "Оружейная лорда: снаряжение знати от " + ArmoryThreshold + " монет.";
                    case DikozemieMarketKind.Village:
                        return "В деревне продают еду, скот и простые вещи до " +
                            VillageThreshold + " монет.";
                    case DikozemieMarketKind.Peddler:
                        return "Торговец везёт мелочь до " + PeddlerThreshold +
                            " монет — на четверть дороже, чем в городе. Коней у него нет.";
                    default:
                        if (DikozemieRoads.IsCaptive)
                        {
                            return "Вы в плену: пленникам не торгуют.";
                        }
                        return here != null && IsCastle(here)
                            ? "В замке торгует только оружейная: сначала пройдите ворота."
                            : "Вы в пути: рынок есть в каждом городе и деревне.";
                }
            }
        }

        // ---- Castle ----------------------------------------------------

        internal static bool HasCastlePass(DikozemieLobbyMapNode node)
        {
            DikozemieTravelSave save = DikozemieTravelState.Current;
            return node != null && save?.CastlePasses != null &&
                save.CastlePasses.Contains(node.Id, StringComparer.OrdinalIgnoreCase);
        }

        internal static bool TryBuyCastlePass(DikozemieLobbyMapNode node, out string message)
        {
            DikozemieTravelSave save = DikozemieTravelState.Current;
            if (node == null || save == null)
            {
                message = "Вы не у ворот замка.";
                return false;
            }
            if (HasCastlePass(node))
            {
                message = "Стража вас знает: проходите.";
                return true;
            }
            if (!DikozemieArmoryProgression.TrySpendHeroResources(CastleEntryPrice, out int coins, "castle",
                    "стража замка " + DisplayName(node), node.Id))
            {
                message = "Стража не пускает без дела: нужно " + CastleEntryPrice +
                    " монет, у вас " + coins + ".";
                return false;
            }
            if (save.CastlePasses == null)
            {
                save.CastlePasses = new List<string>();
            }
            save.CastlePasses.Add(node.Id);
            DikozemieTravelState.Save();
            DikozemieLobbyShellIntegration.TraceButtonAction(
                "town castle pass bought node=" + node.Id + " coinsLeft=" + coins);
            message = "Вы заплатили страже " + CastleEntryPrice +
                " монет. Теперь вас пускают в замок в любой день.";
            return true;
        }

        // ---- People ----------------------------------------------------

        // Three or four townsfolk, the same every visit: the town's id picks them.
        internal static IReadOnlyList<DikozemieNotable> Notables(DikozemieLobbyMapNode node)
        {
            var result = new List<DikozemieNotable>();
            if (node == null)
            {
                return result;
            }
            int seed = StableHash(node.Id);
            string culture = NormalizeCulture(node.Culture);
            string Pick(bool female, int salt)
            {
                IReadOnlyList<string> names = DikozemieTownCatalog.Names(culture, female);
                return names[(int)((uint)(seed * 31 + salt * 977) % (uint)names.Count)];
            }
            if (IsVillage(node))
            {
                result.Add(new DikozemieNotable
                {
                    Name = Pick(false, 1),
                    Role = "Староста",
                    Speech = "«Коня у нас держат двое: я да мельник. Овёс, сено, конюшня — " +
                        "конь ест каждый день, а пашет не всякий. Корова нужнее.»"
                });
                result.Add(new DikozemieNotable
                {
                    Name = Pick(true, 2),
                    Female = true,
                    Role = "Знахарка",
                    Speech = "«Бойцы с арены приходят ко мне латать головы. " +
                        "Двое на одного — и самый сильный ляжет. Не ходите по дорогам в одиночку.»"
                });
                return result;
            }
            string workshop = Workshops[(int)((uint)seed % (uint)Workshops.Length)];
            result.Add(new DikozemieNotable
            {
                Name = Pick(false, 3),
                Role = "Мастер цеха",
                Speech = "«Моя " + workshop + " кормит три семьи. Хотите своё дело — копите: " +
                    "мастерская с домом стоит 2400–3600 монет и окупается за неделю-другую, если сырья в " +
                    "городе вдоволь. Мастерские продаются здесь же: спросите меня.»"
            });
            result.Add(new DikozemieNotable
            {
                Name = Pick(seed % 2 == 0, 4),
                Female = seed % 2 == 0,
                Role = seed % 2 == 0 ? "Купчиха" : "Купец",
                Speech = "«" + LocalGoods(culture) + " Привозное везут издалека — переплатите на десятую часть. " +
                    "Зерно мерим бушелем: мешок килограммов на 25, человеку хлеба недели на три. " +
                    "Хотите торговать — идите на торговый двор: там видно, чего в городе мало, а чего завались.»"
            });
            result.Add(new DikozemieNotable
            {
                Name = Pick(false, 5),
                Role = "Главарь шайки",
                Speech = "«Думаете, конь и меч делают вас хозяином дороги? " +
                    "Двое-трое моих ребят с дубинами — и вы в канаве. " +
                    "Ходите с попутчиками или платите, когда просят.»"
            });
            if (node.HasPort)
            {
                result.Add(new DikozemieNotable
                {
                    Name = Pick(false, 6),
                    Role = "Корабельщик",
                    Speech = "«Ладья — это не лодка из трёх досок. Лес, железо, парус, полгода работы двадцати мастеров. " +
                        "Простому человеку за всю жизнь не накопить. Хотите за море — наймитесь матросом или купите место на корабле.»"
                });
            }
            return result;
        }

        private static readonly string[] Workshops =
        {
            "кузня", "пивоварня", "гончарня", "дубильня", "ткацкая", "столярная",
            "маслобойня", "давильня"
        };

        private static string LocalGoods(string culture)
        {
            switch (culture)
            {
                case "khuzait": return "Кони у нас дешевле всех: степь растит их сама. Луки тоже свои.";
                case "aserai": return "Верблюды свои, дешёвые. А сталь возим издалека — мечи дорогие.";
                case "battania": return "Луки и дротики делаем сами — берите здесь. Кони привозные, дороже.";
                case "sturgia": return "Топоры, копья и щиты у нас свои и дешёвые.";
                case "nord": return "Топоры и щиты свои. Коней везут морем — дорого.";
                case "vlandia": return "Арбалеты и болты у нас лучшие и дешевле, чем где-либо.";
                default: return "Доспехи и шлемы имперской работы здесь дешевле всего.";
            }
        }

        // ---- Tavern ----------------------------------------------------

        internal static string TavernKeeper =>
            "Трактирщик вытирает кружку: «Деньги? Их тут добывают так.»\n\n" +
            DikozemieArenaEarnings.Text +
            "\n\nДля сравнения: подёнщик получает 1–2 монеты в день, латник — 12, рыцарь — 24.";

        internal static string Rumor(DikozemieLobbyMapNode here, int index)
        {
            var rumors = new List<string>
            {
                "«Двое-трое разбойников — верная смерть для одиночки. Сколько ни учись, двое всегда сильнее.»",
                "«Кони дешевле всего у хузаитов. Но конь ест каждый день: овёс, сено и место в конюшне.»",
                "«В замок пускают за " + CastleEntryPrice + " монет страже. Зато там оружейная: доспехи знати в городе не купишь.»",
                "«Ладья стоит как поместье — " + DikozemieTravelState.ShipPrice +
                    " монет. Челн из трёх досок — " + DikozemieTravelState.SmallBoatPrice + ". Матросом же плывёшь даром и получаешь плату.»",
                "«Место на корабле до соседнего порта — несколько монет. Простому человеку другого пути за море нет.»",
                "«На арене сходятся бойцы со всех городов: круг один, а входов много.»",
                "«Подёнщик зарабатывает полторы монеты в день. Боец на арене — больше, если выживет.»",
                "«Знать платит за отделку вдвое: золото на рукояти режет не лучше железа.»"
            };
            return rumors[Math.Abs(index) % rumors.Count];
        }

        internal static int StableHash(string text)
        {
            unchecked
            {
                int hash = 23;
                foreach (char symbol in text ?? string.Empty)
                {
                    hash = hash * 31 + symbol;
                }
                return hash & 0x7FFFFFFF;
            }
        }
    }
}
