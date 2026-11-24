using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaleWorlds.ModuleManager;

namespace Dikozemie.Online
{
    // Players kept starting the arena from the TaleWorlds launcher's
    // Multiplayer tab (customer, 2026-11-02 and 2026-11-04). That tab loads
    // only multiplayer modules: without War Sails the sea battle crashed while
    // its scene loaded, and without SandBoxCore and the campaign modules the
    // bots lose their characters. The desktop shortcut passes the full set.
    // When this module finds itself in an incomplete game it restarts the
    // game through that shortcut's script, once.
    internal static class DikozemieLaunchGuard
    {
        internal const string RelaunchedVariable = "DIKOZEMIE_ARENA_RELAUNCHED";

        // The exact set and order of launch_arena.js.
        private static readonly string[] RequiredModules =
        {
            "Native",
            "SandBoxCore",
            "CustomBattle",
            "Sandbox",
            "StoryMode",
            "NavalDLC",
            "Multiplayer",
            DikozemieSubModule.ModuleId
        };

        private static bool _checked;
        private static bool _navalLoaded = true;
        private static string _missingModules = string.Empty;

        // False when War Sails is not part of this game session: online sea
        // battles must not be entered then.
        internal static bool IsNavalAvailable
        {
            get
            {
                Check();
                return _navalLoaded;
            }
        }

        internal static string NavalUnavailableText =>
            "Морской бой недоступен: игра запущена без дополнения War Sails. " +
            "Закройте игру и запустите её ярлыком «Арена Дикоземья» на рабочем столе.";

        internal static string MissingModules
        {
            get
            {
                Check();
                return _missingModules;
            }
        }

        // Called from OnSubModuleLoad, before any lobby or mission exists.
        internal static void EnsureFullModuleSet()
        {
            Check();
            if (string.IsNullOrEmpty(_missingModules))
            {
                return;
            }

            if (string.Equals(
                    Environment.GetEnvironmentVariable(RelaunchedVariable),
                    "1",
                    StringComparison.Ordinal))
            {
                // Already restarted once and still incomplete: never loop.
                // The lobby keeps working; sea battles stay locked.
                Trace("launch-guard still incomplete after relaunch missing=" +
                    _missingModules);
                return;
            }

            string launchScript = FindLaunchScript();
            string gameRoot = FindGameRoot();
            bool navalInstalled = gameRoot != null && File.Exists(
                Path.Combine(gameRoot, "Modules", "NavalDLC", "SubModule.xml"));
            if (launchScript == null || !navalInstalled)
            {
                Trace("launch-guard cannot restart missing=" + _missingModules +
                    " script=" + (launchScript ?? "none") +
                    " navalInstalled=" + navalInstalled);
                return;
            }

            Trace("launch-guard restarting through the arena shortcut missing=" +
                _missingModules);
            try
            {
                DikozemieGameRelaunch.RestartThroughArenaShortcutAndExit(
                    launchScript);
            }
            catch (Exception error)
            {
                Trace("launch-guard restart failed=" + error.Message);
            }
        }

        private static void Check()
        {
            if (_checked)
            {
                return;
            }
            _checked = true;
            try
            {
                var loaded = new HashSet<string>(
                    TaleWorlds.Engine.Utilities.GetModulesNames() ??
                        Array.Empty<string>(),
                    StringComparer.OrdinalIgnoreCase);
                _navalLoaded = loaded.Contains("NavalDLC");
                _missingModules = string.Join(
                    ",",
                    RequiredModules.Where(id => !loaded.Contains(id)));
                Trace("launch-guard modules=" + string.Join(",", loaded) +
                    " missing=" + (_missingModules.Length == 0 ? "none" : _missingModules));
            }
            catch (Exception error)
            {
                // Never block a start on a failed check.
                _navalLoaded = true;
                _missingModules = string.Empty;
                Trace("launch-guard check failed=" + error.Message);
            }
        }

        private static string FindLaunchScript()
        {
            try
            {
                string moduleRoot = ModuleHelper.GetModuleFullPath(
                    DikozemieSubModule.ModuleId);
                if (string.IsNullOrWhiteSpace(moduleRoot))
                {
                    return null;
                }
                string script = Path.GetFullPath(Path.Combine(
                    moduleRoot, "Launcher", "launch_arena.js"));
                return File.Exists(script) ? script : null;
            }
            catch
            {
                return null;
            }
        }

        private static string FindGameRoot()
        {
            try
            {
                string moduleRoot = ModuleHelper.GetModuleFullPath(
                    DikozemieSubModule.ModuleId);
                if (string.IsNullOrWhiteSpace(moduleRoot))
                {
                    return null;
                }
                // <game>\Modules\DikozemieOnline\ -> <game>
                DirectoryInfo module = new DirectoryInfo(
                    Path.GetFullPath(moduleRoot).TrimEnd('\\', '/'));
                return module.Parent?.Parent?.FullName;
            }
            catch
            {
                return null;
            }
        }

        private static void Trace(string message)
        {
            Console.WriteLine("DikozemieOnline: " + message);
            DikozemieLobbyShellIntegration.TraceButtonAction(message);
        }
    }
}
