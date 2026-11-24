using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using TaleWorlds.Localization;
using TaleWorlds.ModuleManager;

namespace Dikozemie.Online
{
    /// <summary>
    /// Character-creation texts are native campaign strings whose source
    /// carries only an id ("{=MFrIHJZM}"); the words come from the tables
    /// ArenaSetup copies into ModuleData/Languages. Only the Russian table
    /// has them, so in a game set to English every description was empty and
    /// the hero menu refused to open (customer, 2026-10-29). The arena UI is
    /// Russian, so any other game language reads the module's Russian table.
    /// </summary>
    internal static class DikozemieCreationText
    {
        private const string RussianLanguageId = "Русский";
        private static readonly Regex SourceId = new Regex(@"^\{=([^}]+)\}");
        private static readonly Regex GrammarTag = new Regex(@"\{\.[^}]*\}");
        private static Dictionary<string, string> _russian;

        internal static string Localize(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return string.Empty;
            }

            if (!string.Equals(
                    MBTextManager.ActiveTextLanguage,
                    RussianLanguageId,
                    StringComparison.Ordinal))
            {
                Match id = SourceId.Match(source);
                if (id.Success &&
                    RussianTable.TryGetValue(id.Groups[1].Value, out string text))
                {
                    // Russian grammar tags ({.f}, {.n}) are understood only by
                    // the Russian text processor.
                    return new TextObject(GrammarTag.Replace(text, string.Empty))
                        .ToString();
                }
            }
            return new TextObject(source).ToString();
        }

        private static Dictionary<string, string> RussianTable =>
            _russian ?? (_russian = LoadRussianTable());

        // The module's copies come first; stage titles and a few parent
        // names live only in the game's own Native/SandBox/NavalDLC tables.
        private static Dictionary<string, string> LoadRussianTable()
        {
            var table = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                string module = ModuleHelper.GetModuleFullPath(DikozemieSubModule.ModuleId);
                string modules = Path.GetDirectoryName(module.TrimEnd('/', '\\'));
                ReadRussianFolder(table, Path.Combine(module, "ModuleData", "Languages", "RU"), "dikozemie_creation_*.xml");
                foreach (string native in new[] { "Native", "SandBox", "NavalDLC" })
                {
                    ReadRussianFolder(table, Path.Combine(modules, native, "ModuleData", "Languages", "RU"), "*.xml");
                }
            }
            catch (Exception error)
            {
                DikozemieHeroMenuController.TraceLifecycle(
                    "russian creation texts failed to load: " + error.Message);
            }
            DikozemieHeroMenuController.TraceLifecycle(
                "russian creation texts loaded for " + MBTextManager.ActiveTextLanguage +
                ": " + table.Count);
            return table;
        }

        private static void ReadRussianFolder(
            Dictionary<string, string> table,
            string folder,
            string pattern)
        {
            if (!Directory.Exists(folder))
            {
                return;
            }

            foreach (string file in Directory.GetFiles(folder, pattern))
            {
                try
                {
                    using (XmlReader reader = XmlReader.Create(file))
                    {
                        while (reader.ReadToFollowing("string"))
                        {
                            string id = reader.GetAttribute("id");
                            string text = reader.GetAttribute("text");
                            if (!string.IsNullOrEmpty(id) &&
                                !string.IsNullOrWhiteSpace(text) &&
                                !table.ContainsKey(id))
                            {
                                table.Add(id, text);
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    DikozemieHeroMenuController.TraceLifecycle(
                        "russian table skipped " + Path.GetFileName(file) + ": " + error.Message);
                }
            }
        }
    }
}
