using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    internal sealed class DikozemieArenaRatingEntry
    {
        public string PlayerId;
        public string DisplayName;
        public int Rating;
        public int Wins;
        public int Losses;
        public string ProgressionResetToken;
    }

    internal static class DikozemieArenaRatingStore
    {
        private const int InitialRating = 0;
        private const int BotRatingStep = 100;
        internal const int VictoryRatingReward = 1;
        internal const int HigherOpponentRewardStep = 1000;
        private const string StateDirectoryEnvironmentVariable =
            "DIKOZEMIE_ARENA_STATE_DIR";
        private static readonly Dictionary<string, DikozemieArenaRatingEntry> Entries =
            new Dictionary<string, DikozemieArenaRatingEntry>(StringComparer.Ordinal);
        private static bool _loaded;

        private static string FolderPath
        {
            get
            {
                string configured = Environment.GetEnvironmentVariable(
                    StateDirectoryEnvironmentVariable);
                return string.IsNullOrWhiteSpace(configured)
                    ? Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.LocalApplicationData),
                        "Dikozemie",
                        "server")
                    : configured.Trim();
            }
        }

        private static string RatingPath => Path.Combine(FolderPath, "ranked-1v1-ratings.tsv");
        private static string LockPath => Path.Combine(FolderPath, "ranked-1v1-ratings.lock");

        // Reads used to take the cross-process file lock and re-parse the
        // whole 1,600-row table every time. The spawning behaviour asks for a
        // waiting player's rating on every server frame, so under Wine a
        // single waiting or dead player held the arena at 7-9 FPS
        // (2026-11-04 measurements). Reads now come from memory and the table
        // is re-read from disk at most every ReadCacheSeconds; every write
        // still locks, reloads and saves, so no result is ever lost.
        private const double ReadCacheSeconds = 2.0;
        private static DateTime _lastDiskSyncUtc = DateTime.MinValue;

        public static DikozemieArenaRatingEntry Get(NetworkCommunicator peer)
        {
            EnsureLoaded();
            if ((DateTime.UtcNow - _lastDiskSyncUtc).TotalSeconds < ReadCacheSeconds)
            {
                return GetUnsafe(peer);
            }
            DikozemieArenaRatingEntry result = null;
            ExecuteExclusive(() => result = GetUnsafe(peer), false);
            return result;
        }

        private static DikozemieArenaRatingEntry GetUnsafe(NetworkCommunicator peer)
        {
            string playerId = GetPlayerId(peer);
            if (!Entries.TryGetValue(playerId, out DikozemieArenaRatingEntry entry))
            {
                DikozemieArenaServerRegistration.TryGetCharacterProfile(
                    peer,
                    out DikozemieArenaCharacterProfile profile);
                entry = new DikozemieArenaRatingEntry
                {
                    PlayerId = playerId,
                    DisplayName = string.IsNullOrWhiteSpace(profile?.DisplayName)
                        ? peer?.UserName ?? "Unknown"
                        : profile.DisplayName,
                    Rating = InitialRating
                };
                Entries.Add(playerId, entry);
            }
            else if (!string.IsNullOrWhiteSpace(peer?.UserName))
            {
                entry.DisplayName = peer.UserName;
            }

            return entry;
        }

        public static int RecordResult(NetworkCommunicator winnerPeer, NetworkCommunicator loserPeer)
        {
            int reward = 0;
            ExecuteExclusive(() =>
            {
                DikozemieArenaRatingEntry winner = GetUnsafe(winnerPeer);
                DikozemieArenaRatingEntry loser = GetUnsafe(loserPeer);
                reward = CalculateVictoryReward(winner.Rating, loser.Rating);
                winner.Rating += reward;
                winner.Wins++;
                loser.Losses++;
            }, true);
            return reward;
        }

        public static DikozemieArenaRatingEntry GetBot(
            string characterId,
            string displayName,
            int startingRating)
        {
            EnsureLoaded();
            return GetBotUnsafe(characterId, displayName, startingRating);
        }

        private static DikozemieArenaRatingEntry GetBotUnsafe(
            string characterId,
            string displayName,
            int startingRating)
        {
            string safeCharacterId = string.IsNullOrWhiteSpace(characterId)
                ? "unknown"
                : characterId.Trim();
            string botId = "bot:" + safeCharacterId;
            int fixedRating = NormalizeBotRating(startingRating);
            if (!Entries.TryGetValue(botId, out DikozemieArenaRatingEntry entry))
            {
                entry = new DikozemieArenaRatingEntry
                {
                    PlayerId = botId,
                    DisplayName = string.IsNullOrWhiteSpace(displayName)
                        ? safeCharacterId
                        : displayName,
                    Rating = fixedRating
                };
                Entries.Add(botId, entry);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    entry.DisplayName = displayName;
                }
                // NPC ratings are immutable catalog tiers. Re-seeding also
                // repairs legacy rows that gained points from arena wins.
                entry.Rating = fixedRating;
            }
            return entry;
        }

        public static int GetBotRating(string characterId, int startingRating)
        {
            EnsureLoaded();
            string botId = "bot:" + (string.IsNullOrWhiteSpace(characterId)
                ? "unknown"
                : characterId.Trim());
            return NormalizeBotRating(startingRating);
        }

        public static int RecordTrainingResult(
            NetworkCommunicator playerPeer,
            string botCharacterId,
            string botDisplayName,
            int botStartingRating,
            bool playerVictory,
            out DikozemieArenaRatingEntry player,
            out DikozemieArenaRatingEntry bot,
            bool draw = false)
        {
            DikozemieArenaRatingEntry resolvedPlayer = null;
            DikozemieArenaRatingEntry resolvedBot = null;
            int reward = 0;
            if (draw)
            {
                ExecuteExclusive(() =>
                {
                    resolvedPlayer = GetUnsafe(playerPeer);
                    resolvedBot = GetBotUnsafe(
                        botCharacterId,
                        botDisplayName,
                        botStartingRating);
                }, false);
                player = resolvedPlayer;
                bot = resolvedBot;
                return 0;
            }

            ExecuteExclusive(() =>
            {
                resolvedPlayer = GetUnsafe(playerPeer);
                resolvedBot = GetBotUnsafe(
                    botCharacterId,
                    botDisplayName,
                    botStartingRating);
                if (playerVictory)
                {
                    reward = CalculateVictoryReward(
                        resolvedPlayer.Rating,
                        resolvedBot.Rating);
                    resolvedPlayer.Rating += reward;
                    resolvedPlayer.Wins++;
                    resolvedBot.Losses++;
                }
                else
                {
                    // The player climbs a fixed 100-point NPC ladder. Bots
                    // keep their catalog rating even when they win.
                    resolvedBot.Wins++;
                    resolvedPlayer.Losses++;
                }
            }, true);
            player = resolvedPlayer;
            bot = resolvedBot;
            return reward;
        }

        public static int RecordTeamKill(
            NetworkCommunicator playerPeer,
            string botCharacterId,
            string botDisplayName,
            int botStartingRating,
            out DikozemieArenaRatingEntry player,
            out DikozemieArenaRatingEntry bot)
        {
            return RecordTeamKill(
                playerPeer,
                botCharacterId,
                botDisplayName,
                botStartingRating,
                1,
                out player,
                out bot);
        }

        public static int RecordTeamKill(
            NetworkCommunicator playerPeer,
            string botCharacterId,
            string botDisplayName,
            int botStartingRating,
            int rewardMultiplier,
            out DikozemieArenaRatingEntry player,
            out DikozemieArenaRatingEntry bot)
        {
            DikozemieArenaRatingEntry resolvedPlayer = null;
            DikozemieArenaRatingEntry resolvedBot = null;
            int reward = 0;
            ExecuteExclusive(() =>
            {
                resolvedPlayer = GetUnsafe(playerPeer);
                resolvedBot = GetBotUnsafe(
                    botCharacterId,
                    botDisplayName,
                    botStartingRating);
                reward = CalculateVictoryReward(
                    resolvedPlayer.Rating,
                    resolvedBot.Rating) *
                    Math.Max(1, Math.Min(2, rewardMultiplier));
                resolvedPlayer.Rating += reward;
                resolvedBot.Losses++;
            }, true);
            player = resolvedPlayer;
            bot = resolvedBot;
            return reward;
        }

        // A live enemy in a team battle is worth exactly what a bot of the same
        // rating is worth. The victim keeps its rating; its win/loss is
        // recorded once by the team outcome, not per death.
        public static int RecordTeamPlayerKill(
            NetworkCommunicator killerPeer,
            NetworkCommunicator victimPeer,
            int rewardMultiplier,
            out DikozemieArenaRatingEntry killer,
            out DikozemieArenaRatingEntry victim)
        {
            DikozemieArenaRatingEntry resolvedKiller = null;
            DikozemieArenaRatingEntry resolvedVictim = null;
            int reward = 0;
            ExecuteExclusive(() =>
            {
                resolvedKiller = GetUnsafe(killerPeer);
                resolvedVictim = GetUnsafe(victimPeer);
                reward = CalculateVictoryReward(
                    resolvedKiller.Rating,
                    resolvedVictim.Rating) *
                    Math.Max(1, Math.Min(2, rewardMultiplier));
                resolvedKiller.Rating += reward;
            }, true);
            killer = resolvedKiller;
            victim = resolvedVictim;
            return reward;
        }

        public static void RecordTeamBattleOutcome(
            NetworkCommunicator playerPeer,
            bool victory,
            bool draw,
            out DikozemieArenaRatingEntry player,
            int victoryRating = 0)
        {
            DikozemieArenaRatingEntry resolvedPlayer = null;
            if (draw)
            {
                ExecuteExclusive(
                    () => resolvedPlayer = GetUnsafe(playerPeer),
                    false);
                player = resolvedPlayer;
                return;
            }

            ExecuteExclusive(() =>
            {
                resolvedPlayer = GetUnsafe(playerPeer);
                if (victory)
                {
                    resolvedPlayer.Wins++;
                    resolvedPlayer.Rating += Math.Max(0, victoryRating);
                }
                else
                {
                    resolvedPlayer.Losses++;
                }
            }, true);
            player = resolvedPlayer;
        }

        internal static int CalculateVictoryReward(
            int winnerRating,
            int opponentRating)
        {
            int ratingGap = Math.Max(0, opponentRating - winnerRating);
            return VictoryRatingReward +
                ratingGap / HigherOpponentRewardStep;
        }

        public static List<DikozemieArenaLeaderboardEntry> GetLeaderboard(int maximumEntries)
        {
            List<DikozemieArenaLeaderboardEntry> result = null;
            ExecuteExclusive(() =>
            {
                int rank = 0;
                result = Entries.Values
                    .OrderByDescending(entry => entry.Rating)
                    .ThenByDescending(entry => entry.Wins)
                    .ThenBy(entry => entry.DisplayName)
                    .Take(Math.Max(1, Math.Min(2048, maximumEntries)))
                    .Select(entry => new DikozemieArenaLeaderboardEntry
                    {
                        Rank = ++rank,
                        CharacterId = entry.PlayerId ?? string.Empty,
                        DisplayName = entry.DisplayName ?? "Боец Дикоземья",
                        Rating = entry.Rating,
                        Wins = entry.Wins,
                        Losses = entry.Losses
                    })
                    .ToList();
            }, false);
            return result;
        }

        internal static void Flush()
        {
            EnsureLoaded();
            DikozemieArenaRatingEntry[] snapshot = Entries.Values
                .Select(Clone)
                .ToArray();
            HashSet<string> seededBotIds = new HashSet<string>(
                snapshot
                    .Where(entry => entry.PlayerId.StartsWith(
                        "bot:",
                        StringComparison.Ordinal))
                    .Select(entry => entry.PlayerId),
                StringComparer.Ordinal);

            ExecuteExclusive(() =>
            {
                string[] staleBots = Entries.Keys.Where(playerId =>
                    playerId.StartsWith("bot:", StringComparison.Ordinal) &&
                    !seededBotIds.Contains(playerId)).ToArray();
                foreach (string playerId in staleBots)
                {
                    Entries.Remove(playerId);
                }

                foreach (DikozemieArenaRatingEntry candidate in snapshot)
                {
                    if (!Entries.TryGetValue(
                            candidate.PlayerId,
                            out DikozemieArenaRatingEntry current))
                    {
                        Entries[candidate.PlayerId] = Clone(candidate);
                    }
                    else if (candidate.PlayerId.StartsWith(
                        "bot:",
                        StringComparison.Ordinal))
                    {
                        current.DisplayName = candidate.DisplayName;
                        current.Rating = candidate.Rating;
                    }
                }
            }, true);
        }

        internal static int PruneBotsExcept(ISet<string> allowedBotIds)
        {
            EnsureLoaded();
            if (allowedBotIds == null)
            {
                return 0;
            }

            string[] stale = Entries.Keys.Where(playerId =>
                playerId.StartsWith("bot:", StringComparison.Ordinal) &&
                !allowedBotIds.Contains(playerId)).ToArray();
            foreach (string playerId in stale)
            {
                Entries.Remove(playerId);
            }
            return stale.Length;
        }

        internal static void RemoveLegacyPlatformEntry(
            string platformPlayerId,
            string characterId)
        {
            if (string.IsNullOrWhiteSpace(platformPlayerId) ||
                string.IsNullOrWhiteSpace(characterId) ||
                string.Equals(
                    platformPlayerId,
                    characterId,
                    StringComparison.Ordinal))
            {
                return;
            }

            ExecuteExclusive(
                () => Entries.Remove(platformPlayerId),
                true);
        }

        internal static bool ApplyHeroReset(
            NetworkCommunicator peer,
            string progressionResetToken)
        {
            if (!Guid.TryParse(progressionResetToken, out Guid parsedToken) ||
                parsedToken == Guid.Empty)
            {
                return false;
            }

            string normalizedToken = parsedToken.ToString();
            bool applied = false;
            ExecuteExclusive(() =>
            {
                DikozemieArenaRatingEntry entry = GetUnsafe(peer);
                if (string.Equals(
                        entry.ProgressionResetToken,
                        normalizedToken,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                entry.Rating = InitialRating;
                entry.Wins = 0;
                entry.Losses = 0;
                entry.ProgressionResetToken = normalizedToken;
                applied = true;
            }, true);
            return applied;
        }

        internal static string GetPlayerId(NetworkCommunicator peer)
        {
            if (DikozemieArenaServerRegistration.TryGetCharacterProfile(
                peer,
                out DikozemieArenaCharacterProfile profile) &&
                Guid.TryParse(profile.CharacterId, out Guid characterId) &&
                characterId != Guid.Empty)
            {
                return characterId.ToString();
            }

            string playerId = peer?.PlayerConnectionInfo?.PlayerID.ToString();
            if (!string.IsNullOrWhiteSpace(playerId))
            {
                return playerId;
            }

            return "name:" + (peer?.UserName ?? "unknown");
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(FolderPath);
                using (FileStream ignored = AcquireStoreLock())
                {
                    ReloadFromDisk();
                    StartCleanTestSeasonOnce();
                    MigratePlayersToZeroBaseOnce();
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: rating load failed: " + error.Message);
            }
            finally
            {
                _loaded = true;
            }
        }

        private static void ReloadFromDisk()
        {
            if (!File.Exists(RatingPath))
            {
                return;
            }

            Dictionary<string, DikozemieArenaRatingEntry> loaded =
                new Dictionary<string, DikozemieArenaRatingEntry>(
                    StringComparer.Ordinal);
            foreach (string line in File.ReadAllLines(RatingPath))
            {
                string[] values = line.Split('\t');
                if (values.Length < 5 || string.IsNullOrWhiteSpace(values[0]))
                {
                    continue;
                }

                loaded[values[0]] = new DikozemieArenaRatingEntry
                {
                    PlayerId = values[0],
                    DisplayName = values[1].Replace("\\t", " "),
                    Rating = Parse(values[2], InitialRating),
                    Wins = Parse(values[3], 0),
                    Losses = Parse(values[4], 0),
                    ProgressionResetToken = values.Length >= 6
                        ? NormalizeResetToken(values[5])
                        : string.Empty
                };
            }

            string[] removed = Entries.Keys
                .Where(playerId => !loaded.ContainsKey(playerId))
                .ToArray();
            foreach (string playerId in removed)
            {
                Entries.Remove(playerId);
            }

            foreach (KeyValuePair<string, DikozemieArenaRatingEntry> pair in loaded)
            {
                if (!Entries.TryGetValue(
                        pair.Key,
                        out DikozemieArenaRatingEntry current))
                {
                    Entries[pair.Key] = pair.Value;
                    continue;
                }

                current.DisplayName = pair.Value.DisplayName;
                current.Rating = pair.Value.Rating;
                current.Wins = pair.Value.Wins;
                current.Losses = pair.Value.Losses;
                current.ProgressionResetToken = pair.Value.ProgressionResetToken;
            }
        }

        private static void ExecuteExclusive(Action action, bool save)
        {
            EnsureLoaded();
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            Directory.CreateDirectory(FolderPath);
            using (FileStream ignored = AcquireStoreLock())
            {
                ReloadFromDisk();
                action();
                if (save)
                {
                    Save();
                }
                _lastDiskSyncUtc = DateTime.UtcNow;
            }
            DikozemieServerPerf.Add(
                DikozemieServerPerf.Section.RatingStore,
                System.Diagnostics.Stopwatch.GetTimestamp() - started);
        }

        private static FileStream AcquireStoreLock()
        {
            const int attempts = 320;
            const int delayMilliseconds = 25;
            IOException lastError = null;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                try
                {
                    return new FileStream(
                        LockPath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None);
                }
                catch (IOException error)
                {
                    lastError = error;
                    Thread.Sleep(delayMilliseconds);
                }
            }

            throw new IOException(
                "Timed out waiting for the shared arena rating store lock.",
                lastError);
        }

        private static DikozemieArenaRatingEntry Clone(
            DikozemieArenaRatingEntry source)
        {
            return new DikozemieArenaRatingEntry
            {
                PlayerId = source.PlayerId,
                DisplayName = source.DisplayName,
                Rating = source.Rating,
                Wins = source.Wins,
                Losses = source.Losses,
                ProgressionResetToken = source.ProgressionResetToken
            };
        }

        private static void StartCleanTestSeasonOnce()
        {
            string markerPath = Path.Combine(
                FolderPath,
                "ranked-1v1-clean-slate-v2.marker");
            if (File.Exists(markerPath))
            {
                return;
            }

            // The test season is intentionally new. Removing every old row
            // prevents abandoned local hero GUIDs and previous bot results
            // from inflating the physical 1,620-bot table. The catalog seeds
            // the exact bot roster again immediately after this one-time reset.
            Entries.Clear();
            Save();
            Directory.CreateDirectory(FolderPath);
            File.WriteAllText(markerPath, "clean test season v2");
        }

        private static void MigratePlayersToZeroBaseOnce()
        {
            string markerPath = Path.Combine(
                FolderPath,
                "ranked-1v1-zero-base-v1.marker");
            if (File.Exists(markerPath))
            {
                return;
            }

            foreach (DikozemieArenaRatingEntry entry in Entries.Values)
            {
                if (entry != null &&
                    !entry.PlayerId.StartsWith("bot:", StringComparison.Ordinal))
                {
                    entry.Rating = Math.Max(0, entry.Rating - 1000);
                }
            }
            Save();
            Directory.CreateDirectory(FolderPath);
            File.WriteAllText(markerPath, "player ratings start at zero");
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(FolderPath);
                string[] lines = Entries.Values
                    .OrderByDescending(entry => entry.Rating)
                    .Select(entry => string.Join("\t", new[]
                    {
                        entry.PlayerId,
                        (entry.DisplayName ?? string.Empty).Replace("\t", " ").Replace("\r", " ").Replace("\n", " "),
                        entry.Rating.ToString(CultureInfo.InvariantCulture),
                        entry.Wins.ToString(CultureInfo.InvariantCulture),
                        entry.Losses.ToString(CultureInfo.InvariantCulture),
                        NormalizeResetToken(entry.ProgressionResetToken)
                    }))
                    .ToArray();
                File.WriteAllLines(RatingPath, lines);
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: rating save failed: " + error.Message);
            }
        }

        private static int Parse(string value, int fallback)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : fallback;
        }

        private static int NormalizeBotRating(int startingRating)
        {
            int safeRating = Math.Max(BotRatingStep, startingRating);
            return safeRating - safeRating % BotRatingStep;
        }

        private static string NormalizeResetToken(string value)
        {
            return Guid.TryParse(value, out Guid parsed) && parsed != Guid.Empty
                ? parsed.ToString()
                : string.Empty;
        }
    }
}
