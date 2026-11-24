using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    // Who stands on this battle server (plan, «Античит»: the observer's
    // panel lists the battles and joins one). Every five seconds the server
    // writes battle-presence/<instance>.json next to the economy inbox; the
    // trade server reads the folder. The server knows its people from the
    // TaleWorlds lobby, so no client has to tell where it is.
    internal static class DikozemieBattlePresence
    {
        private const double WriteSeconds = 5.0;
        private static DateTime _nextWriteUtc = DateTime.MinValue;
        private static string _folder;
        private static bool _failed;

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
                _folder = Path.Combine(root, "battle-presence");
                return _folder;
            }
        }

        internal static void Tick(Func<NetworkCommunicator, bool> alive, Func<NetworkCommunicator, bool> observer)
        {
            if (!GameNetwork.IsDedicatedServer)
            {
                return;
            }
            DateTime now = DateTime.UtcNow;
            if (now < _nextWriteUtc)
            {
                return;
            }
            _nextWriteUtc = now.AddSeconds(WriteSeconds);
            string instance = Environment.GetEnvironmentVariable("DIKOZEMIE_ARENA_INSTANCE");
            string portText = Environment.GetEnvironmentVariable("DIKOZEMIE_ARENA_PORT");
            if (string.IsNullOrWhiteSpace(instance) || !int.TryParse(portText, out int port))
            {
                return;
            }
            try
            {
                string map = MultiplayerOptions.OptionType.Map.GetStrValue();
                var text = new StringBuilder("{");
                text.Append("\"server\":").Append(Json(instance.Trim()));
                text.Append(",\"port\":").Append(port.ToString(CultureInfo.InvariantCulture));
                text.Append(",\"name\":").Append(Json(MultiplayerOptions.OptionType.ServerName.GetStrValue()));
                text.Append(",\"naval\":").Append(DikozemieArenaSceneRules.IsNavalBoardingScene(map) ? "true" : "false");
                text.Append(",\"at\":").Append(((now - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds)
                    .ToString("0.0", CultureInfo.InvariantCulture));
                text.Append(",\"players\":[");
                bool first = true;
                foreach (NetworkCommunicator peer in GameNetwork.NetworkPeers.ToList())
                {
                    if (peer?.PlayerConnectionInfo == null || !peer.IsConnectionActive ||
                        !peer.PlayerConnectionInfo.PlayerID.IsValid)
                    {
                        continue;
                    }
                    if (!first)
                    {
                        text.Append(',');
                    }
                    first = false;
                    text.Append("{\"id\":").Append(Json(peer.PlayerConnectionInfo.PlayerID.ToString()));
                    text.Append(",\"name\":").Append(Json(peer.UserName ?? string.Empty));
                    text.Append(",\"alive\":").Append(alive(peer) ? "true" : "false");
                    text.Append(",\"observer\":").Append(observer(peer) ? "true" : "false");
                    text.Append('}');
                }
                text.Append("]}");
                Directory.CreateDirectory(Folder);
                string path = Path.Combine(Folder, instance.Trim() + ".json");
                string temp = path + ".tmp";
                File.WriteAllText(temp, text.ToString(), new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    File.Replace(temp, path, null);
                }
                else
                {
                    File.Move(temp, path);
                }
            }
            catch (Exception error)
            {
                if (!_failed)
                {
                    _failed = true;
                    Console.WriteLine("DikozemieOnline: battle presence not written: " + error.Message);
                }
            }
        }

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
