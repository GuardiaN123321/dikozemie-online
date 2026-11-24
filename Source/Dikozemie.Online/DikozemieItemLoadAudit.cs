using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using TaleWorlds.Core;
using TaleWorlds.ModuleManager;
using TaleWorlds.ObjectSystem;

namespace Dikozemie.Online
{
    /// <summary>
    /// Diagnostics for porting the dedicated server (DIKOZEMIE_TRACE_ITEMS=1):
    /// one exception in ItemObject.Deserialize stops MBObjectManager.LoadXml
    /// for every item after it, so the first item of the merged document that
    /// the object manager does not know is the one that broke the load.
    /// 2026-11-10: the native Linux server rejected heroes with «unknown item».
    /// </summary>
    internal static class DikozemieItemLoadAudit
    {
        private static bool _done;

        internal static void Run(Action<string> trace)
        {
            if (_done || Environment.GetEnvironmentVariable("DIKOZEMIE_TRACE_ITEMS") != "1")
            {
                return;
            }
            _done = true;
            try
            {
                var files = new List<string>();
                string native = Path.Combine(ModuleHelper.GetModuleFullPath("Native"), "ModuleData", "mpitems.xml");
                if (File.Exists(native))
                {
                    files.Add(native);
                }
                string folder = Path.Combine(ModuleHelper.GetModuleFullPath("DikozemieOnline"), "ModuleData",
                    "singleplayer_catalog", "items");
                if (Directory.Exists(folder))
                {
                    files.AddRange(Directory.GetFiles(folder, "*.xml").OrderBy(path => path, StringComparer.Ordinal));
                }
                string firstMissing = null;
                foreach (string file in files)
                {
                    var document = new XmlDocument();
                    document.Load(file);
                    int known = 0;
                    var missing = new List<string>();
                    foreach (XmlNode node in document.DocumentElement?.ChildNodes ?? (XmlNodeList)null)
                    {
                        string id = node.Attributes?["id"]?.Value;
                        if (node.NodeType != XmlNodeType.Element || string.IsNullOrEmpty(id))
                        {
                            continue;
                        }
                        if (MBObjectManager.Instance.GetObject<ItemObject>(id) != null)
                        {
                            known++;
                        }
                        else
                        {
                            missing.Add(node.Name + ":" + id);
                        }
                    }
                    if (firstMissing == null && missing.Count > 0)
                    {
                        firstMissing = Path.GetFileName(file) + " " + missing[0];
                    }
                    trace("item audit " + Path.GetFileName(file) + " known=" + known + " missing=" + missing.Count +
                        (missing.Count > 0 ? " first=" + string.Join(",", missing.Take(5)) : string.Empty));
                }
                trace("item audit first missing: " + (firstMissing ?? "none"));
            }
            catch (Exception error)
            {
                trace("item audit failed: " + error.GetType().Name + " " + error.Message);
            }
        }
    }
}
