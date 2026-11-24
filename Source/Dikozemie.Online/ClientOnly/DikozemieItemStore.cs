using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.ModuleManager;
using TaleWorlds.ObjectSystem;

namespace Dikozemie.Online
{
    internal enum DikozemieStoreCategory
    {
        All,
        Weapons,
        Shields,
        Armor,
        Mounts,
        Goods
    }

    /// <summary>
    /// Reads the multiplayer-safe copy of the installed SandBoxCore catalogue.
    /// The preparation script bakes campaign-style prices into a sidecar table
    /// before Bannerlord loads the XML. Multiplayer can replace ItemObject.Value
    /// with 1 for imported campaign equipment, so that property is only a
    /// fallback. Runtime price calculation must stay deliberately shallow:
    /// walking weapon components of all imported items can crash the native
    /// multiplayer client when Inventory opens.
    /// </summary>
    internal static class DikozemieItemStore
    {
        internal const string GrainItemId = "grain";
        internal const int GrainBuyPrice = 12;
        internal const int GrainSellPrice = 6;
        private const string PreparedPriceFileName = "arena-prices.tsv";

        private static float _catalogTraceDelay;
        private static bool _catalogTraceCompleted;
        private static readonly object PreparedPriceGate = new object();
        private static readonly Dictionary<string, int> PreparedPrices =
            new Dictionary<string, int>(StringComparer.Ordinal);
        // A 0 in the prepared table: never sold (siege ammunition, TaleWorlds'
        // AI test weapons and dummies; Tools/build_arena_price_table.py).
        private static readonly HashSet<string> WithdrawnItems =
            new HashSet<string>(StringComparer.Ordinal);
        private static bool _preparedPricesLoaded;

        internal static void TickCatalogTrace(float dt)
        {
            if (_catalogTraceCompleted)
            {
                return;
            }

            _catalogTraceDelay += Math.Max(0f, dt);
            if (_catalogTraceDelay < 8f)
            {
                return;
            }

            IReadOnlyList<ItemObject> catalog = GetCatalog();
            string categories = string.Join(",", catalog
                .GroupBy(GetCategory)
                .OrderBy(group => group.Key)
                .Select(group => group.Key + "=" + group.Count()));
            string categoryPrices = string.Join(",", catalog
                .GroupBy(GetCategory)
                .OrderBy(group => group.Key)
                .Select(group => group.Key + "=" +
                    group.Min(GetBuyPrice) + "-" + group.Max(GetBuyPrice)));
            DikozemieHeroMenuController.TraceLifecycle(
                "item store catalog ready count=" + catalog.Count +
                " categories=" + categories +
                " prices=" + catalog.Min(GetBuyPrice) + "-" +
                catalog.Max(GetBuyPrice) +
                " distinctPrices=" + catalog.Select(GetBuyPrice).Distinct().Count() +
                " categoryPrices=" + categoryPrices +
                " topPrices=" + string.Join(",", catalog
                    .OrderByDescending(GetBuyPrice)
                    .Take(8)
                    .Select(item => item.StringId + ":" + GetBuyPrice(item))));
            _catalogTraceCompleted = true;
        }

        internal static IReadOnlyList<ItemObject> GetCatalog(
            DikozemieStoreCategory category = DikozemieStoreCategory.All,
            string searchText = null)
        {
            string query = (searchText ?? string.Empty).Trim();
            return MBObjectManager.Instance
                .GetObjectTypeList<ItemObject>()
                .Where(item => item != null &&
                    IsArenaMerchandise(item) &&
                    GetBuyPrice(item) > 0 &&
                    !string.IsNullOrWhiteSpace(item.StringId) &&
                    MatchesCategory(item, category) &&
                    MatchesSearch(item, query))
                .GroupBy(item => item.StringId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(GetBuyPrice)
                .ThenBy(item => item.Name?.ToString() ?? item.StringId,
                    StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        // The price of the market where the hero stands: the table's price
        // with the town's discount for local work or mark-up for imports.
        internal static int GetBuyPrice(ItemObject item)
        {
            int basePrice = GetBasePrice(item);
            if (basePrice <= 0)
            {
                return basePrice;
            }
            float factor = DikozemieTown.PriceFactor(item);
            return Math.Abs(factor - 1f) < 0.001f
                ? basePrice
                : Math.Max(1, (int)Math.Round(basePrice * factor));
        }

        // The price list's own price, the same in every town.
        internal static int GetBasePrice(ItemObject item)
        {
            if (item == null)
            {
                return 0;
            }

            if (string.Equals(item.StringId, GrainItemId, StringComparison.Ordinal))
            {
                return GrainBuyPrice;
            }

            if (TryGetPreparedPrice(item.StringId, out int preparedPrice))
            {
                return preparedPrice;
            }

            return Math.Max(1, item.Value);
        }

        private static bool TryGetPreparedPrice(string itemId, out int price)
        {
            EnsurePreparedPricesLoaded();
            price = 0;
            return !string.IsNullOrWhiteSpace(itemId) &&
                PreparedPrices.TryGetValue(itemId, out price);
        }

        private static bool IsArenaMerchandise(ItemObject item)
        {
            if (item == null)
            {
                return false;
            }

            // Native deliberately marks multiplayer class equipment as
            // non-merchandise because the vanilla mode has no shop. Arena's
            // generated price table is the explicit allow-list for those
            // battle-tested objects, so they belong in our market.
            EnsurePreparedPricesLoaded();
            if (WithdrawnItems.Contains(item.StringId ?? string.Empty))
            {
                return false;
            }
            return !item.NotMerchandise ||
                TryGetPreparedPrice(item.StringId, out int price) && price > 0;
        }

        private static void EnsurePreparedPricesLoaded()
        {
            if (_preparedPricesLoaded)
            {
                return;
            }

            lock (PreparedPriceGate)
            {
                if (_preparedPricesLoaded)
                {
                    return;
                }

                string path = Path.Combine(
                    ModuleHelper.GetModuleFullPath(DikozemieSubModule.ModuleId),
                    "ModuleData",
                    "singleplayer_catalog",
                    PreparedPriceFileName);
                try
                {
                    foreach (string line in File.ReadAllLines(path))
                    {
                        if (string.IsNullOrWhiteSpace(line) || line[0] == '#')
                        {
                            continue;
                        }

                        string[] columns = line.Split('\t');
                        if (columns.Length == 2 &&
                            !string.IsNullOrWhiteSpace(columns[0]) &&
                            int.TryParse(columns[1], out int withdrawnPrice) &&
                            withdrawnPrice == 0)
                        {
                            WithdrawnItems.Add(columns[0]);
                            continue;
                        }
                        if (columns.Length == 2 &&
                            !string.IsNullOrWhiteSpace(columns[0]) &&
                            int.TryParse(columns[1], out int parsedPrice) &&
                            parsedPrice > 0)
                        {
                            if (PreparedPrices.TryGetValue(
                                    columns[0],
                                    out int existingPrice))
                            {
                                PreparedPrices[columns[0]] = Math.Max(
                                    existingPrice,
                                    parsedPrice);
                            }
                            else
                            {
                                PreparedPrices[columns[0]] = parsedPrice;
                            }
                        }
                    }
                    DikozemieHeroMenuController.TraceLifecycle(
                        "item store prepared prices loaded count=" +
                        PreparedPrices.Count + " withdrawn=" + WithdrawnItems.Count);
                }
                catch (Exception error)
                {
                    DikozemieHeroMenuController.TraceLifecycle(
                        "item store prepared prices failed: " + error.Message);
                }
                finally
                {
                    _preparedPricesLoaded = true;
                }
            }
        }

        internal static int GetSellPrice(ItemObject item)
        {
            return item == null ? 0 : Math.Max(1, GetBuyPrice(item) / 2);
        }

        internal static DikozemieStoreCategory GetCategory(ItemObject item)
        {
            string type = item?.ItemType.ToString() ?? string.Empty;
            switch (type)
            {
                case "OneHandedWeapon":
                case "TwoHandedWeapon":
                case "Polearm":
                case "Bow":
                case "Crossbow":
                case "Thrown":
                case "Arrows":
                case "Bolts":
                    return DikozemieStoreCategory.Weapons;
                case "Shield":
                    return DikozemieStoreCategory.Shields;
                case "HeadArmor":
                case "BodyArmor":
                case "LegArmor":
                case "HandArmor":
                case "Cape":
                    return DikozemieStoreCategory.Armor;
                case "Horse":
                case "HorseHarness":
                case "Animal":
                    return DikozemieStoreCategory.Mounts;
                default:
                    return DikozemieStoreCategory.Goods;
            }
        }

        internal static bool CanEquip(ItemObject item)
        {
            if (item == null)
            {
                return false;
            }

            for (int slot = 0; slot < 12; slot++)
            {
                if (Equipment.IsItemFitsToSlot((EquipmentIndex)slot, item))
                {
                    return true;
                }
            }
            return false;
        }

        internal static string GetTypeName(ItemObject item)
        {
            string type = item?.ItemType.ToString() ?? string.Empty;
            switch (type)
            {
                case "OneHandedWeapon": return "Одноручное";
                case "TwoHandedWeapon": return "Двуручное";
                case "Polearm": return "Древковое";
                case "Bow": return "Лук";
                case "Crossbow": return "Арбалет";
                case "Thrown": return "Метательное";
                case "Arrows": return "Стрелы";
                case "Bolts": return "Болты";
                case "Shield": return "Щит";
                case "HeadArmor": return "Шлем";
                case "BodyArmor": return "Доспех";
                case "LegArmor": return "Обувь";
                case "HandArmor": return "Перчатки";
                case "Cape": return "Накидка";
                case "Horse": return "Конь";
                case "HorseHarness": return "Сбруя";
                case "Animal": return "Животное";
                case "Banner": return "Знамя";
                case "Goods": return "Товар";
                default: return string.IsNullOrWhiteSpace(type) ? "Предмет" : type;
            }
        }

        internal static bool TryPurchase(
            string itemId,
            out int remainingGold,
            out string message)
        {
            remainingGold = DikozemieArmoryProgression.HeroResources;
            ItemObject item = ResolveMerchandise(itemId);
            if (item == null)
            {
                message = "Предмет недоступен в магазине.";
                return false;
            }

            if (!DikozemieTown.IsSoldHere(item, GetBasePrice(item)))
            {
                message = DikozemieTown.MarketKind == DikozemieMarketKind.None
                    ? "Здесь не торгуют: рынок есть в городах и деревнях."
                    : "Здесь это не продают. " + DikozemieTown.MarketHint;
                return false;
            }

            int price = GetBuyPrice(item);
            if (!DikozemieArmoryProgression.TrySpendHeroResources(
                    price,
                    out remainingGold,
                    "item-buy",
                    item.Name?.ToString() ?? itemId,
                    itemId,
                    itemId))
            {
                message = "Не хватает золота: нужно " + price +
                    ", у вас " + remainingGold + ".";
                return false;
            }

            if (!DikozemieHeroProfile.TryAddOwnedItem(itemId))
            {
                DikozemieArmoryProgression.RefundHeroResources(
                    price,
                    out remainingGold);
                message = "Покупку не удалось сохранить. Золото возвращено.";
                return false;
            }

            message = (item.Name?.ToString() ?? "Предмет") +
                " куплен за " + price + ". Осталось золота: " +
                remainingGold + ".";
            return true;
        }

        internal static bool TrySell(
            string itemId,
            out int remainingGold,
            out string message)
        {
            remainingGold = DikozemieArmoryProgression.HeroResources;
            if (DikozemieTown.MarketKind == DikozemieMarketKind.None)
            {
                message = "Продать можно только на рынке: зайдите в город или деревню.";
                return false;
            }
            if (string.Equals(itemId, GrainItemId, StringComparison.Ordinal))
            {
                if (!DikozemieHeroProfile.OwnsItem(itemId) ||
                    !DikozemieHeroProfile.TryRemoveOwnedItem(itemId))
                {
                    message = "Зерна нет в инвентаре.";
                    return false;
                }
                DikozemieArmoryProgression.RefundHeroResources(
                    GrainSellPrice,
                    out remainingGold,
                    "item-sell",
                    "Зерно",
                    itemId,
                    itemId);
                message = "Зерно продано за " + GrainSellPrice +
                    ". Всего золота: " + remainingGold + ".";
                return true;
            }

            ItemObject item = ResolveMerchandise(itemId);
            if (item == null || !DikozemieHeroProfile.OwnsItem(itemId))
            {
                message = "Этого предмета нет в инвентаре.";
                return false;
            }

            if (!DikozemieHeroProfile.TryRemoveOwnedItem(itemId))
            {
                message = "Продажу не удалось сохранить.";
                return false;
            }

            int price = GetSellPrice(item);
            DikozemieArmoryProgression.RefundHeroResources(
                price,
                out remainingGold,
                "item-sell",
                item.Name?.ToString() ?? itemId,
                itemId,
                itemId);
            message = (item.Name?.ToString() ?? "Предмет") +
                " продан за " + price + ". Всего золота: " +
                remainingGold + ".";
            return true;
        }

        private static bool MatchesCategory(
            ItemObject item,
            DikozemieStoreCategory category)
        {
            return category == DikozemieStoreCategory.All ||
                GetCategory(item) == category;
        }

        private static bool MatchesSearch(ItemObject item, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            return (item.Name?.ToString() ?? string.Empty).IndexOf(
                       query,
                       StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                item.StringId.IndexOf(
                    query,
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static ItemObject ResolveMerchandise(string itemId)
        {
            ItemObject item = string.IsNullOrWhiteSpace(itemId)
                ? null
                : MBObjectManager.Instance.GetObject<ItemObject>(itemId);
            return item == null || !IsArenaMerchandise(item) ||
                GetBuyPrice(item) <= 0
                ? null
                : item;
        }
    }
}
