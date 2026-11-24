using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace Dikozemie.Online
{
    // Customer, 2026-11-05: «лошадь надо кормить, она требует у тебя расход.
    // Кстати, тоже как прикольная механика»; «коня — это нужно где-то
    // держать ... аренда, еда». Every day the hero plays, each of his mounts
    // eats oats and hay. Without coins for fodder the horse stays in the
    // stable: the hero goes to battle on foot until he can pay.
    internal static class DikozemieHorseUpkeep
    {
        private static DateTime _nextCheck = DateTime.MinValue;
        private static bool _hungryNoticeShown;

        // Coins a day (Docs/ARENA_PRICES_HISTORY_RU.md, «Содержание коня»):
        // London inns could ask up to 2d for a horse's hay for a day and a
        // night (1365), a bushel of oats cost up to 8d (1371), and a great
        // horse ate half to two thirds of a bushel a day. A pack horse lives
        // on hay; a riding horse needs hay and some oats; a war horse more
        // oats; a destrier the most.
        internal static int FodderPerDay(ItemObject item)
        {
            if (!IsMount(item))
            {
                return 0;
            }
            int price = DikozemieItemStore.GetBasePrice(item);
            if (price > 8400)
            {
                return 5;
            }
            if (price > 1200)
            {
                return 3;
            }
            return price > 480 ? 2 : 1;
        }

        internal static bool IsMount(ItemObject item) =>
            item != null && item.HasHorseComponent &&
            item.HorseComponent.IsMount && !item.HorseComponent.IsLiveStock;

        internal static int DailyCost
        {
            get
            {
                int total = 0;
                foreach (string itemId in DikozemieHeroProfile.Snapshot.OwnedItems ??
                             new List<string>())
                {
                    if (string.IsNullOrWhiteSpace(itemId))
                    {
                        continue;
                    }
                    total += FodderPerDay(MBObjectManager.Instance.GetObject<ItemObject>(itemId));
                }
                return total;
            }
        }

        internal static bool IsHungry =>
            DikozemieTravelState.Current?.HorseHungry == true && DailyCost > 0;

        internal static string Status
        {
            get
            {
                int cost = DailyCost;
                if (cost <= 0)
                {
                    return string.Empty;
                }
                return IsHungry
                    ? "Конь голоден: нужно " + cost + " мон. на корм. В бой — пешком."
                    : "Корм коню: " + cost + " мон. в день.";
            }
        }

        private static string Today => DateTime.Now.ToString("yyyy-MM-dd");

        // Called from the lobby tick; cheap and at most twice a second.
        internal static void Tick()
        {
            DateTime now = DateTime.UtcNow;
            if (now < _nextCheck)
            {
                return;
            }
            _nextCheck = now.AddSeconds(0.5);
            DikozemieTravelSave save = DikozemieTravelState.EnsureLoadedForHero();
            if (save == null)
            {
                return;
            }
            string today = Today;
            int cost = DailyCost;
            if (cost <= 0)
            {
                if (save.HorseHungry || save.FodderDay != today)
                {
                    save.HorseHungry = false;
                    save.FodderDay = today;
                    DikozemieTravelState.Save();
                }
                return;
            }
            if (save.FodderDay == today && !save.HorseHungry)
            {
                return;
            }
            if (DikozemieArmoryProgression.TrySpendHeroResources(cost, out int coins, "fodder", "корм коня"))
            {
                bool wasHungry = save.HorseHungry;
                save.FodderDay = today;
                save.HorseHungry = false;
                _hungryNoticeShown = false;
                DikozemieTravelState.Save();
                DikozemieLobbyShellIntegration.TraceButtonAction(
                    "horse fodder paid cost=" + cost + " coinsLeft=" + coins);
                Show((wasHungry ? "Конь наконец поел: -" : "Корм коню на сутки: -") + cost +
                    " мон. (овёс и сено). Осталось " + coins + ".",
                    new Color(0.95f, 0.80f, 0.45f));
                return;
            }
            if (!save.HorseHungry)
            {
                save.HorseHungry = true;
                DikozemieTravelState.Save();
                DikozemieLobbyShellIntegration.TraceButtonAction(
                    "horse hungry cost=" + cost + " coins=" + coins);
            }
            if (!_hungryNoticeShown)
            {
                _hungryNoticeShown = true;
                Show("Нечем кормить коня: нужно " + cost + " мон., у вас " + coins +
                    ". Голодный конь не повезёт в бой — пока не заработаете на корм.",
                    new Color(0.95f, 0.45f, 0.35f));
            }
        }

        private static void Show(string text, Color color)
        {
            try
            {
                InformationManager.DisplayMessage(new InformationMessage(text, color));
            }
            catch (Exception)
            {
                // Messages are a courtesy; the state above is what matters.
            }
        }
    }
}
