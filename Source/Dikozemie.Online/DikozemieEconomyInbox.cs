using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    // The battle server's side of the treasury (stage 3, 2026-11-08). The
    // trade server (EconomyServer/dikozemie_economy.py) runs on the same
    // machine and reads these lines; a battle server never talks to it over
    // the network, and a slow trade server never holds a battle.
    //
    // Two kinds of lines, one JSON object each:
    //   bind  — the player came through the TaleWorlds lobby with this key
    //           hash: the computer that sent it may use the treasury;
    //   arena / training — the coins a finished series earned or lost,
    //           counted here, not by the client.
    internal static class DikozemieEconomyInbox
    {
        private static readonly object Gate = new object();
        private static string _folder;
        // The client sends its profile again every two seconds until it
        // controls an agent (up to twelve times; an observer never does).
        // The same bind or equip line for the same player within ten
        // minutes says nothing new.
        private static readonly Dictionary<string, KeyValuePair<string, DateTime>> LastLines =
            new Dictionary<string, KeyValuePair<string, DateTime>>(StringComparer.Ordinal);
        private static readonly TimeSpan RepeatWindow = TimeSpan.FromMinutes(10);

        private static bool IsRepeat(string slot, string signature)
        {
            DateTime now = DateTime.UtcNow;
            lock (Gate)
            {
                if (LastLines.TryGetValue(slot, out KeyValuePair<string, DateTime> last) &&
                    last.Key == signature && now - last.Value < RepeatWindow)
                {
                    return true;
                }
                if (LastLines.Count > 2000)
                {
                    foreach (string old in LastLines.Where(pair => now - pair.Value.Value >= RepeatWindow)
                                 .Select(pair => pair.Key).ToList())
                    {
                        LastLines.Remove(old);
                    }
                }
                LastLines[slot] = new KeyValuePair<string, DateTime>(signature, now);
                return false;
            }
        }

        private static string Folder
        {
            get
            {
                if (_folder != null)
                {
                    return _folder;
                }
                string configured = Environment.GetEnvironmentVariable("DIKOZEMIE_ARENA_STATE_DIR");
                string root = string.IsNullOrWhiteSpace(configured)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Dikozemie", "server")
                    : configured.Trim();
                _folder = Path.Combine(root, "economy-inbox");
                return _folder;
            }
        }

        internal static void Bind(string playerId, string keyHash)
        {
            if (!GameNetwork.IsDedicatedServer || string.IsNullOrWhiteSpace(playerId) || !IsHash(keyHash) ||
                IsRepeat("bind|" + playerId, keyHash.ToLowerInvariant()))
            {
                return;
            }
            Write("{\"id\":" + Json(Guid.NewGuid().ToString("N")) +
                ",\"kind\":\"bind\",\"playerId\":" + Json(playerId) +
                ",\"keyHash\":" + Json(keyHash.ToLowerInvariant()) +
                ",\"at\":" + Now() + "}", durable: false);
        }

        // What a hero carries into a battle, as the battle server accepted
        // it (plan, «Античит»: «сверка снаряжения в бою с тем, что куплено»).
        // The trade server compares it with what he bought or started with
        // and flags the rest for the observer.
        internal static void Equip(string playerId, IEnumerable<string> items)
        {
            if (!GameNetwork.IsDedicatedServer || string.IsNullOrWhiteSpace(playerId) || items == null)
            {
                return;
            }
            string[] list = items.Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal).Take(16).ToArray();
            if (list.Length == 0 ||
                IsRepeat("equip|" + playerId, string.Join(",", list.OrderBy(item => item, StringComparer.Ordinal))))
            {
                return;
            }
            string server = Environment.GetEnvironmentVariable("DIKOZEMIE_ARENA_INSTANCE");
            Write("{\"id\":" + Json(Guid.NewGuid().ToString("N")) +
                ",\"kind\":\"equip\",\"playerId\":" + Json(playerId) +
                ",\"server\":" + Json(string.IsNullOrWhiteSpace(server) ? "battle" : server.Trim()) +
                ",\"items\":[" + string.Join(",", list.Select(Json)) + "]" +
                ",\"at\":" + Now() + "}", durable: false);
        }

        internal static void Result(NetworkCommunicator peer, string kind, int coins, int penalty, string note)
        {
            if (!GameNetwork.IsDedicatedServer || peer?.PlayerConnectionInfo == null ||
                !peer.PlayerConnectionInfo.PlayerID.IsValid || coins == 0 && penalty == 0)
            {
                return;
            }
            Write("{\"id\":" + Json(Guid.NewGuid().ToString("N")) +
                ",\"kind\":" + Json(kind) +
                ",\"playerId\":" + Json(peer.PlayerConnectionInfo.PlayerID.ToString()) +
                ",\"coins\":" + Math.Max(0, coins) +
                ",\"penalty\":" + Math.Max(0, penalty) +
                ",\"note\":" + Json(note ?? string.Empty) +
                ",\"at\":" + Now() + "}");
        }

        // durable: coins (results) are flushed to the disk before the battle
        // goes on; a lost bind or equip line is only sent again next time.
        private static void Write(string line, bool durable = true)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Folder);
                    string path = Path.Combine(Folder,
                        "battle-" + Process.GetCurrentProcess().Id + "-" +
                        DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".jsonl");
                    byte[] bytes = Encoding.UTF8.GetBytes(line + "\n");
                    using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(durable);
                    }
                }
                Console.WriteLine("DikozemieOnline: economy inbox " + line);
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: economy inbox write failed: " + error.Message);
            }
        }

        private static bool IsHash(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64)
            {
                return false;
            }
            foreach (char symbol in value)
            {
                if (!Uri.IsHexDigit(symbol))
                {
                    return false;
                }
            }
            return true;
        }

        private static string Now() =>
            ((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds)
                .ToString("0.000", CultureInfo.InvariantCulture);

        private static string Json(string value)
        {
            var text = new StringBuilder("\"");
            foreach (char symbol in value ?? string.Empty)
            {
                if (symbol == '"' || symbol == '\\')
                {
                    text.Append('\\').Append(symbol);
                }
                else if (symbol < 0x20)
                {
                    text.Append("\\u").Append(((int)symbol).ToString("x4", CultureInfo.InvariantCulture));
                }
                else
                {
                    text.Append(symbol);
                }
            }
            return text.Append('"').ToString();
        }
    }
}
