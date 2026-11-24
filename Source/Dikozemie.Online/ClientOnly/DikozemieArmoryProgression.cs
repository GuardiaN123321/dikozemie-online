using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace Dikozemie.Online
{
    [DataContract]
    internal sealed class DikozemieArmoryProgressionState
    {
        [DataMember(Name = "schemaVersion")]
        public int SchemaVersion = 7;

        [DataMember(Name = "arenaWins")]
        public int ArenaWins;

        [DataMember(Name = "playerRating")]
        public int PlayerRating;

        [DataMember(Name = "trainingBotRating")]
        public int TrainingBotRating;

        [DataMember(Name = "heroResources")]
        public int HeroResources;

        [DataMember(Name = "botRatingWins")]
        public Dictionary<string, int> BotRatingWins =
            new Dictionary<string, int>(StringComparer.Ordinal);
    }

    internal static class DikozemieArmoryProgression
    {
        internal const int RatingUnit = 1;
        internal const int UpsetRatingDifferenceUnit = 1000;
        private const int BotRatingStep = 100;

        private static readonly object Gate = new object();
        private static DikozemieArmoryProgressionState _state;
        private static string _pendingTrainingResult;
        private static bool _pendingTrainingVictory;

        private static string StatePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Dikozemie",
            "arena",
            "armory-progression.json");

        internal static int ArenaWins
        {
            get
            {
                EnsureLoaded();
                lock (Gate)
                {
                    return _state.ArenaWins;
                }
            }
        }

        internal static int PlayerRating
        {
            get
            {
                EnsureLoaded();
                lock (Gate)
                {
                    return _state.PlayerRating;
                }
            }
        }

        internal static int TrainingBotRating
        {
            get
            {
                EnsureLoaded();
                lock (Gate)
                {
                    return _state.TrainingBotRating;
                }
            }
        }

        internal static int HeroResources
        {
            get
            {
                EnsureLoaded();
                lock (Gate)
                {
                    return _state.HeroResources;
                }
            }
        }

        // The coins of the old file, as they were before the trade server
        // took the money over: brought over once (DikozemieEconomy.Hello).
        internal static int LocalCoins => HeroResources;

        // The trade server's balance plus what is still on its way to it.
        internal static void SetFromServer(int coins)
        {
            EnsureLoaded();
            lock (Gate)
            {
                int safe = Math.Max(0, coins);
                if (_state.HeroResources == safe)
                {
                    return;
                }
                _state.HeroResources = safe;
                SaveLocked();
            }
        }

        internal static void Initialize()
        {
            EnsureLoaded();
        }

        internal static void ResetHeroProgression()
        {
            EnsureLoaded();
            lock (Gate)
            {
                // Bot rows belong to the persistent arena population. The
                // player's results burn with the hero; the coins stay on the
                // account (customer 2026-10-31: «деньги не сгорают»).
                _state.ArenaWins = 0;
                _state.PlayerRating = 0;
                _pendingTrainingResult = null;
                _pendingTrainingVictory = false;
                SaveLocked();
            }
        }

        internal static void ObserveServerWins(int arenaWins)
        {
            int sanitizedWins = Math.Max(0, arenaWins);
            EnsureLoaded();
            lock (Gate)
            {
                if (sanitizedWins <= _state.ArenaWins)
                {
                    return;
                }

                _state.ArenaWins = sanitizedWins;
                SaveLocked();
            }
        }

        internal static int ObserveArenaSeriesResult(
            int playerRating,
            int arenaWins,
            int earnedCoins,
            int coinPenalty)
        {
            EnsureLoaded();
            lock (Gate)
            {
                int safeCoins = Math.Max(0, earnedCoins);
                int safePenalty = Math.Min(
                    Math.Max(0, coinPenalty),
                    _state.HeroResources + safeCoins);
                int coinChange = safeCoins - safePenalty;
                _state.PlayerRating = Math.Max(0, playerRating);
                _state.ArenaWins = Math.Max(0, arenaWins);
                _state.HeroResources = Math.Max(
                    0,
                    _state.HeroResources + safeCoins - safePenalty);
                SaveLocked();
                // The battle server pays these coins into the treasury itself.
                DikozemieEconomy.Report("battle", coinChange, "бой на арене");
                return coinChange;
            }
        }

        internal static string RecordNavalResult(
            bool playerVictory,
            int personalKills,
            DikozemieNavalShipClass shipClass,
            string experienceSummary)
        {
            EnsureLoaded();
            lock (Gate)
            {
                int coinsBefore = _state.HeroResources;
                int safePersonalKills = Math.Max(0, personalKills);
                int personalKillReward =
                    DikozemieNavalRewardRules.CalculatePersonalKillReward(
                        safePersonalKills);
                _state.PlayerRating += personalKillReward;
                _state.HeroResources += personalKillReward;

                int lossPenalty = 0;
                if (playerVictory)
                {
                    _state.PlayerRating += RatingUnit;
                    _state.ArenaWins++;
                    _state.HeroResources +=
                        DikozemieNavalRewardRules.GetVictoryCoinReward(shipClass);
                }
                else
                {
                    lossPenalty =
                        DikozemieNavalRewardRules.CalculateLossCoinPenalty(
                            shipClass,
                            _state.HeroResources);
                    _state.HeroResources -= lossPenalty;
                }

                string killSummary = "Личные убийства: " +
                    safePersonalKills + ". За них: +" +
                    personalKillReward + " рейтинга и +" +
                    personalKillReward + " монет (x2).";
                int configuredLossPenalty =
                    DikozemieNavalRewardRules.GetLossCoinPenalty(shipClass);
                string result = playerVictory
                    ? "Морская победа: +1 рейтинг, +" +
                      DikozemieNavalRewardRules.GetVictoryCoinReward(shipClass) +
                      " монет."
                    : "Морское поражение: штрафа к рейтингу нет, " +
                      (lossPenalty == configuredLossPenalty
                          ? "-" + configuredLossPenalty + " монет."
                          : lossPenalty > 0
                              ? "списано " + lossPenalty +
                                " из " + configuredLossPenalty +
                                " монет — баланс не уходит ниже нуля."
                              : "монет не было — ниже нуля не списываем.");
                result += "\n" + killSummary;
                if (!string.IsNullOrWhiteSpace(experienceSummary))
                {
                    result += "\n" + experienceSummary;
                }
                result += "\nВаш рейтинг: " + _state.PlayerRating +
                    ". Монеты: " + _state.HeroResources + ".";

                _pendingTrainingResult = result;
                _pendingTrainingVictory = playerVictory;
                SaveLocked();
                int navalChange = _state.HeroResources - coinsBefore;
                DikozemieEconomy.Report(navalChange >= 0 ? "naval" : "penalty", navalChange,
                    playerVictory ? "морская победа" : "морское поражение");
                return result;
            }
        }

        internal static int GetBotRating(string botId, int startingRating)
        {
            EnsureLoaded();
            lock (Gate)
            {
                return NormalizeBotRating(startingRating);
            }
        }

        internal static int CalculateUpsetReward(
            int playerRating,
            int opponentRating)
        {
            int ratingDifference = Math.Max(
                0,
                Math.Max(0, opponentRating) - Math.Max(0, playerRating));
            return ratingDifference / UpsetRatingDifferenceUnit;
        }

        internal static string RecordTrainingResult(
            bool playerVictory,
            string botId,
            string botName,
            int botStartingRating,
            int opponentCount)
        {
            EnsureLoaded();
            lock (Gate)
            {
                string safeBotName = string.IsNullOrWhiteSpace(botName)
                    ? "Боец арены"
                    : botName;
                int botRatingBeforeFight = NormalizeBotRating(botStartingRating);
                if (playerVictory)
                {
                    int upsetReward = CalculateUpsetReward(
                        _state.PlayerRating,
                        botRatingBeforeFight);
                    _state.PlayerRating += upsetReward;
                    _state.HeroResources += upsetReward;
                    DikozemieEconomy.Report("training", upsetReward, "победа над " + safeBotName);
                    _pendingTrainingResult = upsetReward > 0
                        ? "Победа над более сильным соперником! +" +
                          upsetReward + " рейтинга и +" + upsetReward +
                          " монет."
                        : "Победа. Соперник не выше вас по рейтингу: " +
                          "бонус рейтинга и монет не начислен.";
                }
                else
                {
                    _pendingTrainingResult =
                        "Поражение. Рейтинг NPC фиксирован и не изменяется.";
                }

                int botRating = botRatingBeforeFight;
                _pendingTrainingResult +=
                    "\nВаш рейтинг: " + _state.PlayerRating +
                    ". Монеты: " + _state.HeroResources +
                    ". Противник: " + safeBotName +
                    ". Рейтинг противника: " + botRating +
                    ". Бойцов арены: " + Math.Max(1, opponentCount) + ".";

                _pendingTrainingVictory = playerVictory;
                SaveLocked();
                return _pendingTrainingResult;
            }
        }

        internal static int AwardPracticeResources(
            int defeatedOpponentCount,
            bool playerSurvived,
            out int totalResources)
        {
            int safeDefeatedCount = Math.Max(0, defeatedOpponentCount);
            int earnedResources = safeDefeatedCount / 10 +
                (playerSurvived ? 1 : 0);
            EnsureLoaded();
            lock (Gate)
            {
                _state.HeroResources += earnedResources;
                totalResources = _state.HeroResources;
                SaveLocked();
            }
            DikozemieEconomy.Report("practice", earnedResources, "тренировка");
            return earnedResources;
        }

        internal static int AwardTournamentVictory(out int totalResources)
        {
            EnsureLoaded();
            lock (Gate)
            {
                _state.HeroResources++;
                totalResources = _state.HeroResources;
                SaveLocked();
            }
            DikozemieEconomy.Report("tournament", 1, "победа в турнире");
            return 1;
        }

        internal static bool TryPlaceTournamentWager(
            int wager,
            out int totalResources)
        {
            EnsureLoaded();
            lock (Gate)
            {
                int safeWager = Math.Max(0, wager);
                if (safeWager > _state.HeroResources)
                {
                    totalResources = _state.HeroResources;
                    return false;
                }

                _state.HeroResources -= safeWager;
                totalResources = _state.HeroResources;
                SaveLocked();
            }
            DikozemieEconomy.Report("wager", -Math.Max(0, wager), "ставка на турнире");
            return true;
        }

        // kind: the reason as the trade server's ledger names it (rumor,
        // bed, healer, castle, passage, toll, fodder, ransom, escort, robbed,
        // bribe, fine, vessel, item-buy ...).
        internal static bool TrySpendHeroResources(
            int price,
            out int totalResources,
            string kind = "misc-spend",
            string note = "",
            string reference = "",
            string item = "")
        {
            EnsureLoaded();
            lock (Gate)
            {
                int safePrice = Math.Max(0, price);
                if (safePrice > _state.HeroResources)
                {
                    totalResources = _state.HeroResources;
                    return false;
                }

                _state.HeroResources -= safePrice;
                totalResources = _state.HeroResources;
                SaveLocked();
            }
            DikozemieEconomy.Report(kind, -Math.Max(0, price), note, reference, item);
            return true;
        }

        // kind: "refund" gives back a spend just made; anything else is
        // money earned (road, bounty, loot, item-sell ...).
        internal static void RefundHeroResources(
            int amount,
            out int totalResources,
            string kind = "refund",
            string note = "",
            string reference = "",
            string item = "")
        {
            EnsureLoaded();
            lock (Gate)
            {
                _state.HeroResources += Math.Max(0, amount);
                totalResources = _state.HeroResources;
                SaveLocked();
            }
            DikozemieEconomy.Report(kind, Math.Max(0, amount), note, reference, item);
        }

        internal static int AwardTournamentWager(
            int payout,
            out int totalResources)
        {
            int safePayout = Math.Max(0, payout);
            EnsureLoaded();
            lock (Gate)
            {
                _state.HeroResources += safePayout;
                totalResources = _state.HeroResources;
                SaveLocked();
            }
            DikozemieEconomy.Report("tournament", safePayout, "выигрыш ставки");
            return safePayout;
        }

        internal static bool TryConsumeTrainingResult(
            out string message,
            out bool playerVictory)
        {
            lock (Gate)
            {
                message = _pendingTrainingResult;
                playerVictory = _pendingTrainingVictory;
                _pendingTrainingResult = null;
                return !string.IsNullOrEmpty(message);
            }
        }

        private static void EnsureLoaded()
        {
            if (_state != null)
            {
                return;
            }

            lock (Gate)
            {
                if (_state != null)
                {
                    return;
                }

                if (!DikozemieArenaStateFiles.TryRead(
                        StatePath,
                        out DikozemieArmoryProgressionState loaded) ||
                    loaded == null)
                {
                    loaded = new DikozemieArmoryProgressionState();
                }

                int loadedSchemaVersion = loaded.SchemaVersion;
                if (loadedSchemaVersion < 5)
                {
                    loaded = new DikozemieArmoryProgressionState();
                }

                // Version 6 changes only the player's scale: a new hero starts
                // at zero. Preserve wins already earned in the live test by
                // removing the obsolete 1000-point base once.
                if (loadedSchemaVersion == 5)
                {
                    loaded.PlayerRating = Math.Max(0, loaded.PlayerRating - 1000);
                }
                loaded.SchemaVersion = 7;
                loaded.ArenaWins = Math.Max(0, loaded.ArenaWins);
                loaded.PlayerRating = Math.Max(0, loaded.PlayerRating);
                loaded.TrainingBotRating = 0;
                loaded.HeroResources = Math.Max(0, loaded.HeroResources);
                if (loaded.BotRatingWins == null)
                {
                    loaded.BotRatingWins =
                        new Dictionary<string, int>(StringComparer.Ordinal);
                }
                else
                {
                    loaded.BotRatingWins.Clear();
                }
                _state = loaded;
                if (loadedSchemaVersion < 7)
                {
                    SaveLocked();
                }
            }
        }

        private static void SaveLocked()
        {
            DikozemieArenaStateFiles.WriteAtomic(StatePath, _state);
        }

        private static int ScaleLegacyRating(int rating)
        {
            long scaledRating = Math.Max(0L, (long)rating) * RatingUnit;
            return (int)Math.Min(int.MaxValue, scaledRating);
        }

        private static int NormalizeBotRating(int startingRating)
        {
            int safeRating = Math.Max(BotRatingStep, startingRating);
            return safeRating - safeRating % BotRatingStep;
        }
    }
}
