using System;
using System.IO;
using System.Linq;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Dikozemie.Online
{
    /// <summary>
    /// Network messages name items by their object id (MBGUID), which is the
    /// order in which the process registered them. A battle server and a
    /// client that register items in another order dress a hero in other
    /// items (Bannerlord 1.5.7, 2026-11-14: tunic and boots arrived as a
    /// lance and shoes). Both sides write their registry after the game
    /// starts, so the two files can be compared line by line.
    /// </summary>
    internal static class DikozemieItemRegistry
    {
        /// <summary>Items registered by this process (0 before the game starts).</summary>
        internal static int Count { get; private set; }

        /// <summary>FNV-1a hash of the registration order (id and name per item).</summary>
        internal static uint Fingerprint { get; private set; }

        internal static void Write(string path, Action<string> log)
        {
            try
            {
                var items = MBObjectManager.Instance?.GetObjectTypeList<ItemObject>();
                if (items == null)
                {
                    return;
                }

                var lines = items
                    .OrderBy(item => item.Id.InternalValue)
                    .Select(item => item.Id.InternalValue + "\t" + item.StringId +
                        (item.IsInitialized ? string.Empty : "\tuninitialized"))
                    .ToList();
                uint fingerprint = 2166136261;
                foreach (string line in lines)
                {
                    foreach (char c in line)
                    {
                        fingerprint = (fingerprint ^ c) * 16777619;
                    }
                }

                Count = lines.Count;
                Fingerprint = fingerprint;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllLines(path, lines, new UTF8Encoding(false));
                log("DikozemieOnline: item registry items=" + lines.Count +
                    " fingerprint=" + fingerprint.ToString("x8") + " file=" + path);
            }
            catch (Exception error)
            {
                log("DikozemieOnline: item registry not written: " + error.Message);
            }
        }

        internal static string DefaultPath()
        {
#if SERVER
            string serverLog = Environment.GetEnvironmentVariable("DIKOZEMIE_ARENA_SERVER_LOG");
            string folder = string.IsNullOrWhiteSpace(serverLog)
                ? Path.GetTempPath()
                : Path.GetDirectoryName(serverLog.Trim());
            return Path.Combine(folder, "item-registry.txt");
#else
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Dikozemie",
                "logs",
                "item-registry.txt");
#endif
        }
    }
}
