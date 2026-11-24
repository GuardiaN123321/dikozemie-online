using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using TaleWorlds.Library;

namespace Dikozemie.Online
{
    /// <summary>
    /// Readable names for the leaderboard's bots. The server only knows raw
    /// character ids: the campaign catalog is not loaded in multiplayer.
    /// The table is built by Tools/build_arena_bot_names.py.
    /// </summary>
    internal static class DikozemieArenaBotNames
    {
        private const string FallbackName = "Боец арены";
        private static Dictionary<string, string> _names;

        internal static string Format(string characterId, string displayName)
        {
            EnsureLoaded();
            // A bot's displayName is its raw character id; a player's is a name.
            if (displayName != null &&
                _names.TryGetValue(displayName, out string name))
            {
                return name;
            }
            if (characterId != null && _names.TryGetValue(characterId, out name))
            {
                return name;
            }
            return string.IsNullOrWhiteSpace(displayName)
                ? FallbackName
                : displayName;
        }

        private static void EnsureLoaded()
        {
            if (_names != null)
            {
                return;
            }

            _names = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                string path = Path.Combine(
                    BasePath.Name,
                    "Modules",
                    "DikozemieOnline",
                    "ModuleData",
                    "dikozemie_arena_bot_names.json");
                var serializer = new DataContractJsonSerializer(
                    typeof(Dictionary<string, string>),
                    new DataContractJsonSerializerSettings
                    {
                        UseSimpleDictionaryFormat = true
                    });
                using (var stream = new MemoryStream(
                    Encoding.UTF8.GetBytes(File.ReadAllText(path, Encoding.UTF8))))
                {
                    _names = (Dictionary<string, string>)serializer.ReadObject(stream);
                }
            }
            catch (Exception error)
            {
                DikozemieLobbyShellIntegration.TraceButtonAction(
                    "bot names unavailable: " + error.Message);
            }
        }
    }
}
