using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Dikozemie.Online
{
    [DataContract]
    internal sealed class DikozemieArenaHandoffState
    {
        [DataMember(Name = "schemaVersion")]
        public int SchemaVersion = 0;

        [DataMember(Name = "handoffId")]
        public string HandoffId = null;

        [DataMember(Name = "ticketId")]
        public string TicketId = null;

        [DataMember(Name = "matchId")]
        public string MatchId = null;

        [DataMember(Name = "battleId")]
        public string BattleId = null;

        [DataMember(Name = "playerId")]
        public string PlayerId = null;

        [DataMember(Name = "characterId")]
        public string CharacterId = null;

        [DataMember(Name = "characterProfile")]
        public DikozemieArenaCharacterProfile CharacterProfile = null;

        [DataMember(Name = "playerToken")]
        public string PlayerToken = null;

        [DataMember(Name = "returnSave")]
        public string ReturnSave = null;

        [DataMember(Name = "matchmakerUrl")]
        public string MatchmakerUrl = null;

        [DataMember(Name = "createdAt")]
        public string CreatedAt = null;

        [DataMember(Name = "expiresAt")]
        public string ExpiresAt = null;
    }

    [DataContract]
    public sealed class DikozemieArenaPendingResult
    {
        [DataMember(Name = "handoffId")]
        public string HandoffId { get; internal set; }

        [DataMember(Name = "matchId")]
        public string MatchId { get; internal set; }

        [DataMember(Name = "playerId")]
        public string PlayerId { get; internal set; }

        [DataMember(Name = "characterId")]
        public string CharacterId { get; internal set; }

        [DataMember(Name = "matchmakerUrl")]
        public string MatchmakerUrl { get; internal set; }

        [DataMember(Name = "playerToken")]
        public string PlayerToken { get; internal set; }

        [DataMember(Name = "returnSave")]
        public string ReturnSave { get; internal set; }

        [DataMember(Name = "createdAt")]
        public string CreatedAt { get; internal set; }

        [DataMember(Name = "consumed")]
        public bool Consumed { get; internal set; }

        [DataMember(Name = "directResult")]
        public DikozemieArenaDirectResult DirectResult { get; internal set; }
    }

    [DataContract]
    public sealed class DikozemieArenaDirectResult
    {
        [DataMember(Name = "resultId")]
        public string ResultId { get; internal set; }

        [DataMember(Name = "matchId")]
        public string MatchId { get; internal set; }

        [DataMember(Name = "characterId")]
        public string CharacterId { get; internal set; }

        [DataMember(Name = "victory")]
        public bool Victory { get; internal set; }

        [DataMember(Name = "delta")]
        public int Delta { get; internal set; }

        [DataMember(Name = "rewardGold")]
        public int RewardGold { get; internal set; }

        [DataMember(Name = "rating")]
        public int Rating { get; internal set; }

        [DataMember(Name = "wins")]
        public int Wins { get; internal set; }

        [DataMember(Name = "losses")]
        public int Losses { get; internal set; }
    }

    [DataContract]
    internal sealed class DikozemieArenaPendingResultEnvelope
    {
        [DataMember(Name = "schemaVersion")]
        public int SchemaVersion = 1;

        [DataMember(Name = "items")]
        public List<DikozemieArenaPendingResult> Items = new List<DikozemieArenaPendingResult>();
    }

    /// <summary>
    /// Durable bridge between the multiplayer process and the campaign process.
    /// The campaign can read a pending match, apply its rewards once, then mark it
    /// consumed. The matchmaker acknowledgement is intentionally a separate step.
    /// </summary>
    public static class DikozemieArenaResultInbox
    {
        private static readonly object Gate = new object();

        public static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Dikozemie",
            "arena",
            "pending-results.json");

        public static IReadOnlyList<DikozemieArenaPendingResult> ReadPending()
        {
            lock (Gate)
            {
                DikozemieArenaPendingResultEnvelope envelope = ReadEnvelope();
                return envelope.Items.FindAll(item => item != null && !item.Consumed).AsReadOnly();
            }
        }

        public static bool MarkConsumed(string handoffId)
        {
            if (string.IsNullOrWhiteSpace(handoffId))
            {
                return false;
            }

            lock (Gate)
            {
                DikozemieArenaPendingResultEnvelope envelope = ReadEnvelope();
                DikozemieArenaPendingResult item = envelope.Items.Find(candidate =>
                    candidate != null && string.Equals(candidate.HandoffId, handoffId, StringComparison.Ordinal));
                if (item == null || item.Consumed)
                {
                    return false;
                }

                item.Consumed = true;
                DikozemieArenaStateFiles.WriteAtomic(FilePath, envelope);
                return true;
            }
        }

        internal static void Stage(
            DikozemieArenaHandoffState handoff,
            DikozemieArenaDirectResult directResult = null)
        {
            if (handoff == null || string.IsNullOrWhiteSpace(handoff.MatchId))
            {
                return;
            }

            bool hasRemoteResult = !string.IsNullOrWhiteSpace(handoff.PlayerToken) &&
                !string.IsNullOrWhiteSpace(handoff.MatchmakerUrl);
            if (directResult == null && !hasRemoteResult)
            {
                return;
            }

            lock (Gate)
            {
                DikozemieArenaPendingResultEnvelope envelope = ReadEnvelope();
                bool alreadyStaged = envelope.Items.Exists(item => item != null &&
                    ((!string.IsNullOrWhiteSpace(handoff.HandoffId) &&
                      string.Equals(item.HandoffId, handoff.HandoffId, StringComparison.Ordinal)) ||
                     string.Equals(item.MatchId, handoff.MatchId, StringComparison.Ordinal)));
                if (alreadyStaged)
                {
                    return;
                }

                envelope.Items.Add(new DikozemieArenaPendingResult
                {
                    HandoffId = string.IsNullOrWhiteSpace(handoff.HandoffId)
                        ? handoff.MatchId
                        : handoff.HandoffId,
                    MatchId = handoff.MatchId,
                    PlayerId = handoff.PlayerId ?? string.Empty,
                    CharacterId = handoff.CharacterId ?? string.Empty,
                    PlayerToken = handoff.PlayerToken ?? string.Empty,
                    MatchmakerUrl = handoff.MatchmakerUrl ?? string.Empty,
                    ReturnSave = handoff.ReturnSave ?? string.Empty,
                    CreatedAt = DateTime.UtcNow.ToString("O"),
                    Consumed = false,
                    DirectResult = directResult
                });
                DikozemieArenaStateFiles.WriteAtomic(FilePath, envelope);
            }
        }

        private static DikozemieArenaPendingResultEnvelope ReadEnvelope()
        {
            if (DikozemieArenaStateFiles.TryRead(
                FilePath,
                out DikozemieArenaPendingResultEnvelope envelope) && envelope != null)
            {
                if (envelope.Items == null)
                {
                    envelope.Items = new List<DikozemieArenaPendingResult>();
                }
                return envelope;
            }

            return new DikozemieArenaPendingResultEnvelope();
        }
    }

    internal static class DikozemieArenaStateFiles
    {
        public static bool TryRead<T>(string path, out T value) where T : class
        {
            value = null;
            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var serializer = new DataContractJsonSerializer(typeof(T));
                    value = serializer.ReadObject(stream) as T;
                    return value != null;
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: JSON state read failed: " + error.Message);
                return false;
            }
        }

        public static void WriteAtomic<T>(string path, T value) where T : class
        {
            string folder = Path.GetDirectoryName(path);
            Directory.CreateDirectory(folder);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                using (var stream = new FileStream(
                    temporary,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    var serializer = new DataContractJsonSerializer(typeof(T));
                    serializer.WriteObject(stream, value);
                    stream.Flush(true);
                }

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
                TryDelete(temporary);
            }
        }

        public static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: state cleanup failed: " + error.Message);
            }
        }
    }
}
