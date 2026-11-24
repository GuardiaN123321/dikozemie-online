using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    // The observer's word on the battle servers (plan, «Античит»: «кнопки
    // предупредить, выгнать из боя, заблокировать … блокировка действует на
    // всех серверах и на казну»). The trade server writes
    // battle-sanctions.json next to its database; every battle server reads
    // it every three seconds from the mission tick (the main thread: it
    // writes network messages):
    //  * a banned player is not admitted and is put out of a battle;
    //  * a kick puts the player out of the battle he is in, once;
    //  * a warning is a chat line from the observer, once per server;
    //  * observers are listed for the observer's free camera.
    // No new network message: a chat line (ServerMessage) and the engine's
    // disconnect, so older clients understand all of it.
    internal static class DikozemieBattleSanctions
    {
        [DataContract]
        private sealed class Entry
        {
            [DataMember(Name = "id")]
            public long Id = 0;

            [DataMember(Name = "player")]
            public string Player = string.Empty;

            [DataMember(Name = "reason")]
            public string Reason = string.Empty;

            [DataMember(Name = "text")]
            public string Text = string.Empty;

            // Unix seconds of the trade server's clock.
            [DataMember(Name = "at")]
            public double At = 0;
        }

        [DataContract]
        private sealed class FileData
        {
            [DataMember(Name = "version")]
            public long Version = 0;

            [DataMember(Name = "banned")]
            public string[] Banned = Array.Empty<string>();

            [DataMember(Name = "observers")]
            public string[] Observers = Array.Empty<string>();

            [DataMember(Name = "kicks")]
            public Entry[] Kicks = Array.Empty<Entry>();

            [DataMember(Name = "warnings")]
            public Entry[] Warnings = Array.Empty<Entry>();
        }

        private const double CheckSeconds = 3.0;
        private const double KickDelaySeconds = 2.5;
        private static readonly object Gate = new object();
        private static HashSet<string> _banned = new HashSet<string>(StringComparer.Ordinal);
        private static HashSet<string> _observers = new HashSet<string>(StringComparer.Ordinal);
        private static Entry[] _kicks = Array.Empty<Entry>();
        private static Entry[] _warnings = Array.Empty<Entry>();
        private static readonly HashSet<long> DoneKicks = new HashSet<long>();
        private static readonly HashSet<long> DoneWarnings = new HashSet<long>();
        private static readonly Dictionary<NetworkCommunicator, DateTime> PendingDisconnects =
            new Dictionary<NetworkCommunicator, DateTime>();
        // When this server first saw each peer: a kick given before a player
        // came here belongs to another battle.
        private static readonly Dictionary<NetworkCommunicator, double> Arrived =
            new Dictionary<NetworkCommunicator, double>();
        private const double ClockSlackSeconds = 5.0;
        private const double UnsyncedBannedSeconds = 60.0;
        private static DateTime _nextCheckUtc = DateTime.MinValue;
        private static DateTime _fileWriteUtc = DateTime.MinValue;
        private static bool _reportedMissing;

        internal static string FilePath
        {
            get
            {
                string configured = Environment.GetEnvironmentVariable("DIKOZEMIE_SANCTIONS_FILE");
                if (!string.IsNullOrWhiteSpace(configured))
                {
                    return configured.Trim();
                }
                return Path.DirectorySeparatorChar == '/'
                    ? "/opt/bannerlord/economy/state/battle-sanctions.json"
                    : @"Z:\opt\bannerlord\economy\state\battle-sanctions.json";
            }
        }

        internal static bool IsBanned(string playerId)
        {
            Refresh(force: false);
            lock (Gate)
            {
                return !string.IsNullOrEmpty(playerId) && _banned.Contains(playerId);
            }
        }

        internal static bool IsObserver(string playerId)
        {
            Refresh(force: false);
            lock (Gate)
            {
                return !string.IsNullOrEmpty(playerId) && _observers.Contains(playerId);
            }
        }

        // Mission tick of the dedicated server. Nothing may escape into the
        // mission tick: a broken file or entry waits for the next check.
        internal static void Tick(Action<string> trace)
        {
            if (!GameNetwork.IsDedicatedServer)
            {
                return;
            }
            try
            {
                TickCore(trace);
            }
            catch (Exception error)
            {
                _nextCheckUtc = DateTime.UtcNow.AddSeconds(CheckSeconds);
                Console.WriteLine("DikozemieOnline: sanctions tick failed: " + error);
                trace?.Invoke("sanctions tick failed: " + error.GetType().Name + ": " + error.Message);
            }
        }

        // Put out and waiting for the disconnect (a moment after his chat line).
        internal static bool IsLeaving(NetworkCommunicator peer) =>
            peer != null && PendingDisconnects.ContainsKey(peer);

        // Another part of the server puts a player out the observer's way:
        // a chat line, then the disconnect a moment later (a synchronized
        // peer only: nothing is written to one still loading).
        internal static void PutOutPeer(NetworkCommunicator peer, string text, Action<string> trace, string why)
        {
            if (peer == null || !peer.IsConnectionActive || PendingDisconnects.ContainsKey(peer))
            {
                return;
            }
            if (!peer.IsSynchronized)
            {
                PendingDisconnects[peer] = DateTime.UtcNow;
                trace?.Invoke("puts out a loading player=" + peer.PlayerConnectionInfo?.PlayerID.ToString() +
                    " why=" + why);
                return;
            }
            PutOut(peer, text, DateTime.UtcNow, trace, why);
        }

        private static void TickCore(Action<string> trace)
        {
            DateTime now = DateTime.UtcNow;
            DisconnectDue(now, trace);
            if (now < _nextCheckUtc)
            {
                return;
            }
            _nextCheckUtc = now.AddSeconds(CheckSeconds);
            Refresh(force: true);

            double unixNow = (now - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            foreach (NetworkCommunicator gone in Arrived.Keys.Where(peer => peer == null || !peer.IsConnectionActive).ToList())
            {
                Arrived.Remove(gone);
            }
            foreach (NetworkCommunicator peer in GameNetwork.NetworkPeers)
            {
                if (peer != null && peer.IsConnectionActive && !Arrived.ContainsKey(peer))
                {
                    Arrived[peer] = unixNow;
                }
            }

            HashSet<string> banned;
            Entry[] kicks;
            Entry[] warnings;
            lock (Gate)
            {
                banned = _banned;
                kicks = _kicks;
                warnings = _warnings;
            }
            if (banned.Count == 0 && kicks.Length == 0 && warnings.Length == 0)
            {
                return;
            }
            foreach (NetworkCommunicator peer in GameNetwork.NetworkPeers.ToList())
            {
                if (peer?.PlayerConnectionInfo == null || !peer.IsConnectionActive ||
                    !peer.PlayerConnectionInfo.PlayerID.IsValid || PendingDisconnects.ContainsKey(peer))
                {
                    continue;
                }
                string id = peer.PlayerConnectionInfo.PlayerID.ToString();
                Arrived.TryGetValue(peer, out double arrived);
                if (!peer.IsSynchronized)
                {
                    // Still loading: a chat line now would be lost (and the
                    // engine's packet writer is not for peers mid-load, see
                    // DikozemieArenaLeaderboardTransport). A banned player who
                    // never finishes loading goes without a word.
                    if (banned.Contains(id) && arrived > 0 && unixNow - arrived > UnsyncedBannedSeconds)
                    {
                        PendingDisconnects[peer] = now;
                        trace?.Invoke("observer puts out a banned player still loading=" + id);
                    }
                    continue;
                }
                if (banned.Contains(id))
                {
                    Entry ban = kicks.LastOrDefault(kick => kick.Player == id);
                    PutOut(peer, "Наблюдатель закрыл вам вход в Арену" +
                        (ban != null && !string.IsNullOrWhiteSpace(ban.Reason) ? ": " + ban.Reason : "."),
                        now, trace, "banned");
                    if (ban != null)
                    {
                        DoneKicks.Add(ban.Id);
                    }
                    continue;
                }
                foreach (Entry kick in kicks)
                {
                    if (kick.Player == id && kick.At + ClockSlackSeconds >= arrived && DoneKicks.Add(kick.Id))
                    {
                        PutOut(peer, "Наблюдатель удалил вас из боя: " + kick.Reason, now, trace, "kick " + kick.Id);
                        break;
                    }
                }
                if (PendingDisconnects.ContainsKey(peer))
                {
                    continue;
                }
                foreach (Entry warning in warnings)
                {
                    // Given while he is on this server: a warning from before
                    // he came here was shown where he was (another server, or
                    // the lobby's window) — not again here or after a restart.
                    if (warning.Player == id && warning.At + ClockSlackSeconds >= arrived &&
                        DoneWarnings.Add(warning.Id))
                    {
                        Say(peer, "Наблюдатель: " + warning.Text);
                        trace?.Invoke("observer warning shown id=" + warning.Id + " player=" + id);
                    }
                }
            }
        }

        private static void PutOut(NetworkCommunicator peer, string text, DateTime now, Action<string> trace, string why)
        {
            Say(peer, text);
            PendingDisconnects[peer] = now.AddSeconds(KickDelaySeconds);
            trace?.Invoke("observer puts out player=" + peer.PlayerConnectionInfo?.PlayerID.ToString() + " why=" + why);
        }

        // The chat line goes first; the disconnect follows a moment later so
        // the player can read why.
        private static void DisconnectDue(DateTime now, Action<string> trace)
        {
            if (PendingDisconnects.Count == 0)
            {
                return;
            }
            foreach (KeyValuePair<NetworkCommunicator, DateTime> pending in PendingDisconnects.ToList())
            {
                if (pending.Value > now)
                {
                    continue;
                }
                PendingDisconnects.Remove(pending.Key);
                if (pending.Key != null && pending.Key.IsConnectionActive)
                {
                    trace?.Invoke("observer disconnects player=" +
                        pending.Key.PlayerConnectionInfo?.PlayerID.ToString());
                    GameNetwork.AddNetworkPeerToDisconnectAsServer(pending.Key);
                }
            }
        }

        private static void Say(NetworkCommunicator peer, string text)
        {
            try
            {
                GameNetwork.BeginModuleEventAsServer(peer);
                GameNetwork.WriteMessage(new NetworkMessages.FromServer.ServerMessage(text));
                GameNetwork.EndModuleEventAsServer();
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: observer message failed: " + error.Message);
            }
        }

        private static void Refresh(bool force)
        {
            if (!force && _fileWriteUtc != DateTime.MinValue)
            {
                return;
            }
            string path = FilePath;
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    if (!_reportedMissing)
                    {
                        _reportedMissing = true;
                        Console.WriteLine("DikozemieOnline: no sanctions file at " + path);
                    }
                    return;
                }
                if (info.LastWriteTimeUtc == _fileWriteUtc)
                {
                    return;
                }
                FileData data;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    data = (FileData)new DataContractJsonSerializer(typeof(FileData)).ReadObject(stream);
                }
                // A hand-edited file may hold nulls: they are skipped, never
                // dereferenced in the tick.
                lock (Gate)
                {
                    _banned = new HashSet<string>(
                        (data?.Banned ?? Array.Empty<string>()).Where(id => !string.IsNullOrEmpty(id)),
                        StringComparer.Ordinal);
                    _observers = new HashSet<string>(
                        (data?.Observers ?? Array.Empty<string>()).Where(id => !string.IsNullOrEmpty(id)),
                        StringComparer.Ordinal);
                    _kicks = (data?.Kicks ?? Array.Empty<Entry>())
                        .Where(entry => entry != null && !string.IsNullOrEmpty(entry.Player)).ToArray();
                    _warnings = (data?.Warnings ?? Array.Empty<Entry>())
                        .Where(entry => entry != null && !string.IsNullOrEmpty(entry.Player)).ToArray();
                }
                _fileWriteUtc = info.LastWriteTimeUtc;
            }
            catch (Exception error)
            {
                // A half-written file is replaced atomically by the trade
                // server; the next check reads the whole one.
                Console.WriteLine("DikozemieOnline: sanctions file unreadable: " + error.Message);
            }
        }
    }
}
