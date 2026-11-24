using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;
using TaleWorlds.ObjectSystem;

namespace Dikozemie.Online
{
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class DikozemieArenaLeaderboardChunkMessage : GameNetworkMessage
    {
        public string TransferId { get; private set; }
        public int ChunkIndex { get; private set; }
        public int ChunkCount { get; private set; }
        public string Payload { get; private set; }

        public DikozemieArenaLeaderboardChunkMessage()
        {
        }

        internal DikozemieArenaLeaderboardChunkMessage(
            string transferId,
            int chunkIndex,
            int chunkCount,
            string payload)
        {
            TransferId = transferId;
            ChunkIndex = chunkIndex;
            ChunkCount = chunkCount;
            Payload = payload;
        }

        protected override void OnWrite()
        {
            WriteStringToPacket(TransferId ?? string.Empty);
            // PlayerCompressionInfo is one-based (1..60), while transfer chunks
            // are indexed from zero in memory.
            WriteIntToPacket(ChunkIndex + 1, CompressionBasic.PlayerCompressionInfo);
            WriteIntToPacket(ChunkCount, CompressionBasic.PlayerCompressionInfo);
            WriteStringToPacket(Payload ?? string.Empty);
        }

        protected override bool OnRead()
        {
            bool valid = true;
            TransferId = ReadStringFromPacket(ref valid);
            ChunkIndex = ReadIntFromPacket(CompressionBasic.PlayerCompressionInfo, ref valid) - 1;
            ChunkCount = ReadIntFromPacket(CompressionBasic.PlayerCompressionInfo, ref valid);
            Payload = ReadStringFromPacket(ref valid);
            return valid && Guid.TryParse(TransferId, out _) &&
                ChunkCount > 0 && ChunkCount <= 32 &&
                ChunkIndex >= 0 && ChunkIndex < ChunkCount &&
                (Payload?.Length ?? 0) <= 700;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter() =>
            MultiplayerMessageFilter.GameMode;

        protected override string OnGetLogFormat() =>
            "Dikozemie arena leaderboard chunk";
    }

    [DataContract]
    internal sealed class DikozemieArenaLeaderboardEnvelope
    {
        [DataMember(Name = "schemaVersion")]
        public int SchemaVersion = 1;

        [DataMember(Name = "updatedAt")]
        public string UpdatedAt = string.Empty;

        [DataMember(Name = "snapshotId")]
        public string SnapshotId = string.Empty;

        [DataMember(Name = "pageIndex")]
        public int PageIndex;

        [DataMember(Name = "pageCount")]
        public int PageCount = 1;

        [DataMember(Name = "entries")]
        public List<DikozemieArenaLeaderboardEntry> Entries =
            new List<DikozemieArenaLeaderboardEntry>();
    }

    [DataContract]
    internal sealed class DikozemieArenaLeaderboardEntry
    {
        [DataMember(Name = "rank")] public int Rank;
        [DataMember(Name = "characterId")] public string CharacterId = string.Empty;
        [DataMember(Name = "displayName")] public string DisplayName = string.Empty;
        [DataMember(Name = "rating")] public int Rating = 1000;
        [DataMember(Name = "wins")] public int Wins;
        [DataMember(Name = "losses")] public int Losses;
    }

    internal static class DikozemieArenaLeaderboardTransport
    {
        private const int ChunkSize = 600;
        private const int PageSize = 200;
        private const int MaximumChunks = 32;
        // Native Linux battle server, 2026-11-11: a team battle's result
        // queued the whole table (60-90 reliable chunks) and the first chunk
        // went out in the very next frame, one more every frame (64 a
        // second), behind the result and skill messages of the same moment.
        // Now and then the engine did not open the player's reliable packet
        // (BeginModuleEvent) and the first write of the chunk went through a
        // null or stale buffer: SIGSEGV in Write_bit_buffer::serial_op from
        // TickServer, 3 crashes in ~10 battles where the hero died first and
        // watched. The table now waits a moment after the result and goes
        // out at most 20 chunks a second, to synchronized players only.
        private static readonly TimeSpan FirstChunkDelay = TimeSpan.FromSeconds(1.5);
        private static readonly TimeSpan ChunkInterval = TimeSpan.FromMilliseconds(50);
        private static readonly Dictionary<NetworkCommunicator, Queue<DikozemieArenaLeaderboardChunkMessage>>
            Outgoing = new Dictionary<NetworkCommunicator, Queue<DikozemieArenaLeaderboardChunkMessage>>();
        private static readonly Dictionary<NetworkCommunicator, DateTime> NextChunkUtc =
            new Dictionary<NetworkCommunicator, DateTime>();
        private static IncomingTransfer _incoming;
        private static IncomingSnapshot _incomingSnapshot;
        private static IReadOnlyList<DikozemieArenaLeaderboardEntry> _latestEntries =
            Array.Empty<DikozemieArenaLeaderboardEntry>();
#if CLIENT
        private static bool _clientCatalogSeeded;
#endif

        internal static event Action LeaderboardUpdated;

        internal static IReadOnlyList<DikozemieArenaLeaderboardEntry> LatestEntries =>
            _latestEntries;

        private static string LeaderboardPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Dikozemie",
            "arena",
            "leaderboard.json");

        public static void Send(NetworkCommunicator peer)
        {
            if (!GameNetwork.IsServer || peer == null || !peer.IsConnectionActive)
            {
                return;
            }

            List<DikozemieArenaLeaderboardEntry> entries =
                DikozemieArenaRatingStore.GetLeaderboard(2048);
            string snapshotId = Guid.NewGuid().ToString();
            string updatedAt = DateTime.UtcNow.ToString("O");
            int pageCount = Math.Max(1, (entries.Count + PageSize - 1) / PageSize);
            var outgoing = new Queue<DikozemieArenaLeaderboardChunkMessage>();
            for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                var envelope = new DikozemieArenaLeaderboardEnvelope
                {
                    UpdatedAt = updatedAt,
                    SnapshotId = snapshotId,
                    PageIndex = pageIndex,
                    PageCount = pageCount,
                    Entries = entries.Skip(pageIndex * PageSize)
                        .Take(PageSize)
                        .ToList()
                };
                AppendEnvelope(outgoing, envelope);
            }
            // Replacing an older snapshot is intentional: the newest rating
            // state is the only one the client needs. Actual network writes are
            // spread over server ticks so a 1,620-row table cannot starve the
            // client's profile packet or freeze the mission-loading frame.
            Outgoing[peer] = outgoing;
            DateTime earliest = DateTime.UtcNow + FirstChunkDelay;
            if (!NextChunkUtc.TryGetValue(peer, out DateTime next) || next < earliest)
            {
                NextChunkUtc[peer] = earliest;
            }
        }

        private static void AppendEnvelope(
            Queue<DikozemieArenaLeaderboardChunkMessage> outgoing,
            DikozemieArenaLeaderboardEnvelope envelope)
        {
            string encoded = Encode(envelope);
            int count = Math.Max(1, (encoded.Length + ChunkSize - 1) / ChunkSize);
            if (count > MaximumChunks)
            {
                Console.WriteLine(
                    "DikozemieOnline: leaderboard page exceeds network limit page=" +
                    envelope.PageIndex);
                return;
            }
            string transferId = Guid.NewGuid().ToString();
            for (int index = 0; index < count; index++)
            {
                int start = index * ChunkSize;
                string payload = encoded.Substring(
                    start,
                    Math.Min(ChunkSize, encoded.Length - start));
                outgoing.Enqueue(new DikozemieArenaLeaderboardChunkMessage(
                    transferId,
                    index,
                    count,
                    payload));
            }
        }

        public static void TickServer()
        {
            if (!GameNetwork.IsServer || Outgoing.Count == 0)
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            foreach (NetworkCommunicator peer in Outgoing.Keys.ToArray())
            {
                if (peer == null || !peer.IsConnectionActive ||
                    !Outgoing.TryGetValue(peer, out Queue<DikozemieArenaLeaderboardChunkMessage> queue))
                {
                    Outgoing.Remove(peer);
                    NextChunkUtc.Remove(peer);
                    continue;
                }
                if (!peer.IsSynchronized ||
                    (NextChunkUtc.TryGetValue(peer, out DateTime next) && now < next))
                {
                    continue;
                }

                if (queue.Count > 0)
                {
                    GameNetwork.BeginModuleEventAsServer(peer);
                    GameNetwork.WriteMessage(queue.Dequeue());
                    GameNetwork.EndModuleEventAsServer();
                    NextChunkUtc[peer] = now + ChunkInterval;
                }
                if (queue.Count == 0)
                {
                    Outgoing.Remove(peer);
                    NextChunkUtc.Remove(peer);
                }
            }
        }

        public static void Broadcast(IEnumerable<NetworkCommunicator> peers)
        {
            if (peers == null)
            {
                return;
            }
            foreach (NetworkCommunicator peer in peers)
            {
                Send(peer);
            }
        }

        public static void Receive(DikozemieArenaLeaderboardChunkMessage message)
        {
            if (!GameNetwork.IsClient || message == null ||
                !Guid.TryParse(message.TransferId, out _) ||
                message.ChunkCount <= 0 || message.ChunkCount > MaximumChunks ||
                message.ChunkIndex < 0 || message.ChunkIndex >= message.ChunkCount ||
                (message.Payload?.Length ?? 0) > 700)
            {
                return;
            }

            if (_incoming == null ||
                !string.Equals(
                    _incoming.TransferId,
                    message.TransferId,
                    StringComparison.Ordinal) ||
                _incoming.Chunks.Length != message.ChunkCount)
            {
                _incoming = new IncomingTransfer(message.TransferId, message.ChunkCount);
            }

            _incoming.Chunks[message.ChunkIndex] = message.Payload ?? string.Empty;
            foreach (string chunk in _incoming.Chunks)
            {
                if (chunk == null)
                {
                    return;
                }
            }

            try
            {
                var joined = new StringBuilder();
                foreach (string chunk in _incoming.Chunks)
                {
                    joined.Append(chunk);
                }
                DikozemieArenaLeaderboardEnvelope envelope =
                    Deserialize(Decode(joined.ToString()));
                ReceivePage(envelope);
            }
            catch (Exception error)
            {
                Console.WriteLine(
                    "DikozemieOnline: leaderboard receive failed: " + error.Message);
            }
            finally
            {
                _incoming = null;
            }
        }

#if CLIENT
        internal static void EnsureClientCatalogSeed()
        {
            if (_clientCatalogSeeded)
            {
                return;
            }

            _clientCatalogSeeded = true;
            var entries = DikozemieArenaOpponentCatalog.BuildLeaderboardSeed();
            DikozemieHeroProfile.Initialize();
            DikozemieHeroProfileState hero = DikozemieHeroProfile.Snapshot;

            // The last table the server sent survives a restart: without it
            // the lobby showed the hero with 0 losses and no other players
            // until the next battle (2026-11-04). Bot ratings still come from
            // the current catalog; wins, losses and live players from the cache.
            Dictionary<string, DikozemieArenaLeaderboardEntry> cached =
                TryLoadCachedEntries();
            int cachedLosses = 0;
            if (cached != null)
            {
                foreach (DikozemieArenaLeaderboardEntry seed in entries)
                {
                    if (cached.TryGetValue(seed.CharacterId, out DikozemieArenaLeaderboardEntry row))
                    {
                        seed.Wins = row.Wins;
                        seed.Losses = row.Losses;
                    }
                }
                foreach (DikozemieArenaLeaderboardEntry row in cached.Values)
                {
                    if (row.CharacterId.StartsWith("bot:", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (hero != null && string.Equals(
                            row.CharacterId, hero.HeroId, StringComparison.Ordinal))
                    {
                        cachedLosses = row.Losses;
                        continue;
                    }
                    entries.Add(row);
                }
            }
            if (hero != null && !string.IsNullOrWhiteSpace(hero.HeroId))
            {
                entries.Add(new DikozemieArenaLeaderboardEntry
                {
                    CharacterId = hero.HeroId,
                    DisplayName = string.IsNullOrWhiteSpace(hero.Name)
                        ? GameNetwork.MyPeer?.UserName ?? "Герой"
                        : hero.Name,
                    Rating = DikozemieArmoryProgression.PlayerRating,
                    Wins = DikozemieArmoryProgression.ArenaWins,
                    Losses = cachedLosses
                });
            }
            PublishClientEntries(entries);
        }

        private static Dictionary<string, DikozemieArenaLeaderboardEntry> TryLoadCachedEntries()
        {
            try
            {
                if (!File.Exists(LeaderboardPath))
                {
                    return null;
                }
                DikozemieArenaLeaderboardEnvelope envelope =
                    Deserialize(File.ReadAllBytes(LeaderboardPath));
                if (envelope?.Entries == null || envelope.Entries.Count == 0)
                {
                    return null;
                }
                var rows = new Dictionary<string, DikozemieArenaLeaderboardEntry>(
                    StringComparer.Ordinal);
                foreach (DikozemieArenaLeaderboardEntry row in envelope.Entries)
                {
                    if (row != null && !string.IsNullOrWhiteSpace(row.CharacterId))
                    {
                        rows[row.CharacterId] = row;
                    }
                }
                return rows;
            }
            catch
            {
                // A damaged cache only costs the stale numbers.
                return null;
            }
        }

        internal static void ApplyTrainingResult(
            DikozemieArenaTrainingResultMessage message)
        {
            if (message == null)
            {
                return;
            }
            EnsureClientCatalogSeed();
            var entries = _latestEntries.ToList();
            // A team battle against live players only has no bot to name.
            if (!string.IsNullOrEmpty(message.OpponentCharacterId))
            {
                BasicCharacterObject opponent = MBObjectManager.Instance
                    .GetObject<BasicCharacterObject>(message.OpponentCharacterId);
                Upsert(
                    entries,
                    "bot:" + message.OpponentCharacterId,
                    opponent?.Name?.ToString() ?? message.OpponentCharacterId,
                    message.OpponentRating,
                    message.OpponentWins,
                    message.OpponentLosses);
            }

            DikozemieHeroProfileState hero = DikozemieHeroProfile.Snapshot;
            Upsert(
                entries,
                hero?.HeroId ?? GameNetwork.MyPeer?.PlayerConnectionInfo?.PlayerID.ToString(),
                hero?.Name ?? GameNetwork.MyPeer?.UserName ?? "Герой",
                message.PlayerRating,
                message.PlayerWins,
                message.PlayerLosses);
            PublishClientEntries(entries);
        }

        internal static void ApplyMatchResult(DikozemieArenaMatchResultMessage message)
        {
            if (message == null)
            {
                return;
            }
            EnsureClientCatalogSeed();
            var entries = _latestEntries.ToList();
            UpsertPeer(
                entries,
                message.Winner,
                message.WinnerRating,
                message.WinnerWins,
                message.WinnerLosses);
            UpsertPeer(
                entries,
                message.Loser,
                message.LoserRating,
                message.LoserWins,
                message.LoserLosses);
            PublishClientEntries(entries);
        }

        private static void UpsertPeer(
            List<DikozemieArenaLeaderboardEntry> entries,
            NetworkCommunicator peer,
            int rating,
            int wins,
            int losses)
        {
            DikozemieHeroProfileState hero = DikozemieHeroProfile.Snapshot;
            bool local = peer == GameNetwork.MyPeer;
            string id = local && !string.IsNullOrWhiteSpace(hero?.HeroId)
                ? hero.HeroId
                : peer?.PlayerConnectionInfo?.PlayerID.ToString();
            string name = local && !string.IsNullOrWhiteSpace(hero?.Name)
                ? hero.Name
                : peer?.UserName ?? "Боец арены";
            Upsert(entries, id, name, rating, wins, losses);
        }

        private static void Upsert(
            List<DikozemieArenaLeaderboardEntry> entries,
            string characterId,
            string displayName,
            int rating,
            int wins,
            int losses)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return;
            }
            DikozemieArenaLeaderboardEntry entry = entries.FirstOrDefault(candidate =>
                string.Equals(candidate.CharacterId, characterId, StringComparison.Ordinal));
            if (entry == null)
            {
                entry = new DikozemieArenaLeaderboardEntry { CharacterId = characterId };
                entries.Add(entry);
            }
            entry.DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? characterId
                : displayName;
            entry.Rating = Math.Max(0, rating);
            entry.Wins = Math.Max(0, wins);
            entry.Losses = Math.Max(0, losses);
        }

        private static void PublishClientEntries(
            IEnumerable<DikozemieArenaLeaderboardEntry> source)
        {
            int rank = 0;
            List<DikozemieArenaLeaderboardEntry> entries = source
                .Where(entry => entry != null &&
                    !string.IsNullOrWhiteSpace(entry.CharacterId))
                .GroupBy(entry => entry.CharacterId, StringComparer.Ordinal)
                .Select(group => group.Last())
                .OrderByDescending(entry => entry.Rating)
                .ThenByDescending(entry => entry.Wins)
                .ThenBy(entry => entry.DisplayName)
                .Select(entry =>
                {
                    entry.Rank = ++rank;
                    return entry;
                })
                .ToList();
            _latestEntries = entries;
            var complete = new DikozemieArenaLeaderboardEnvelope
            {
                UpdatedAt = DateTime.UtcNow.ToString("O"),
                SnapshotId = Guid.NewGuid().ToString(),
                Entries = entries
            };
            WriteAtomic(LeaderboardPath, Serialize(complete));
            LeaderboardUpdated?.Invoke();
        }
#endif

        private static void ReceivePage(DikozemieArenaLeaderboardEnvelope envelope)
        {
            if (envelope == null ||
                !Guid.TryParse(envelope.SnapshotId, out _) ||
                envelope.PageCount <= 0 || envelope.PageCount > 32 ||
                envelope.PageIndex < 0 || envelope.PageIndex >= envelope.PageCount ||
                envelope.Entries == null || envelope.Entries.Count > PageSize)
            {
                throw new InvalidOperationException("invalid leaderboard page");
            }

            if (_incomingSnapshot == null ||
                !string.Equals(
                    _incomingSnapshot.SnapshotId,
                    envelope.SnapshotId,
                    StringComparison.Ordinal) ||
                _incomingSnapshot.Pages.Length != envelope.PageCount)
            {
                _incomingSnapshot = new IncomingSnapshot(
                    envelope.SnapshotId,
                    envelope.UpdatedAt,
                    envelope.PageCount);
            }

            _incomingSnapshot.Pages[envelope.PageIndex] = envelope.Entries;
            for (int index = 0; index < _incomingSnapshot.Pages.Length; index++)
            {
                if (_incomingSnapshot.Pages[index] == null)
                {
                    return;
                }
            }

            List<DikozemieArenaLeaderboardEntry> entries = _incomingSnapshot.Pages
                .SelectMany(page => page)
                .OrderBy(entry => entry.Rank)
                .ToList();
            var complete = new DikozemieArenaLeaderboardEnvelope
            {
                UpdatedAt = _incomingSnapshot.UpdatedAt,
                SnapshotId = _incomingSnapshot.SnapshotId,
                PageIndex = 0,
                PageCount = 1,
                Entries = entries
            };
            WriteAtomic(LeaderboardPath, Serialize(complete));
            _latestEntries = entries;
            _incomingSnapshot = null;
            LeaderboardUpdated?.Invoke();
        }

        private static string Encode(DikozemieArenaLeaderboardEnvelope envelope)
        {
            using (var json = new MemoryStream(Serialize(envelope)))
            {
                using (var compressed = new MemoryStream())
                {
                    using (var deflate = new DeflateStream(
                        compressed,
                        CompressionLevel.Fastest,
                        leaveOpen: true))
                    {
                        json.CopyTo(deflate);
                    }
                    return Convert.ToBase64String(compressed.ToArray());
                }
            }
        }

        private static byte[] Serialize(DikozemieArenaLeaderboardEnvelope envelope)
        {
            using (var json = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(DikozemieArenaLeaderboardEnvelope))
                    .WriteObject(json, envelope);
                return json.ToArray();
            }
        }

        private static DikozemieArenaLeaderboardEnvelope Deserialize(byte[] json)
        {
            using (var stream = new MemoryStream(json))
            {
                return new DataContractJsonSerializer(
                    typeof(DikozemieArenaLeaderboardEnvelope))
                    .ReadObject(stream) as DikozemieArenaLeaderboardEnvelope;
            }
        }

        private static byte[] Decode(string encoded)
        {
            byte[] compressedBytes = Convert.FromBase64String(encoded ?? string.Empty);
            if (compressedBytes.Length > 32768)
            {
                throw new InvalidOperationException("compressed leaderboard exceeds limit");
            }

            using (var compressed = new MemoryStream(compressedBytes))
            using (var deflate = new DeflateStream(compressed, CompressionMode.Decompress))
            using (var json = new MemoryStream())
            {
                deflate.CopyTo(json);
                if (json.Length > 262144)
                {
                    throw new InvalidOperationException("leaderboard exceeds limit");
                }
                return json.ToArray();
            }
        }

        private static void WriteAtomic(string path, byte[] bytes)
        {
            string folder = Path.GetDirectoryName(path);
            Directory.CreateDirectory(folder);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, bytes);
                if (File.Exists(path))
                {
                    File.Replace(temporary, path, null, true);
                }
                else
                {
                    File.Move(temporary, path);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        private sealed class IncomingTransfer
        {
            public IncomingTransfer(string transferId, int chunkCount)
            {
                TransferId = transferId;
                Chunks = new string[chunkCount];
            }

            public string TransferId { get; }
            public string[] Chunks { get; }
        }

        private sealed class IncomingSnapshot
        {
            public IncomingSnapshot(
                string snapshotId,
                string updatedAt,
                int pageCount)
            {
                SnapshotId = snapshotId;
                UpdatedAt = updatedAt ?? string.Empty;
                Pages = new List<DikozemieArenaLeaderboardEntry>[pageCount];
            }

            public string SnapshotId { get; }
            public string UpdatedAt { get; }
            public List<DikozemieArenaLeaderboardEntry>[] Pages { get; }
        }
    }
}
