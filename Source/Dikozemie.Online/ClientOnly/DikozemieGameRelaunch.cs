using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Path = System.IO.Path;

namespace Dikozemie.Online
{
    internal static class DikozemieGameRelaunch
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateProcess(IntPtr processHandle, uint exitCode);

        private const string ModuleList =
            "_MODULES_*Native*SandBoxCore*Sandbox*StoryMode*NavalDLC*" +
            "Multiplayer*DikozemieWarSails*DikozemieOnline*_MODULES_";

        private static string ArenaDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Dikozemie",
            "arena");

        private static string CampaignReturnPath => Path.Combine(
            ArenaDirectory,
            "campaign-return.txt");

        internal static void StartCampaignAndExit(string returnSave)
        {
            if (string.IsNullOrWhiteSpace(returnSave) || returnSave.Length > 128)
            {
                throw new InvalidOperationException("invalid campaign return save name");
            }

            Directory.CreateDirectory(ArenaDirectory);
            string pendingPath = CampaignReturnPath + ".pending";
            File.WriteAllText(pendingPath, returnSave.Trim(), new UTF8Encoding(false));
            if (File.Exists(CampaignReturnPath))
            {
                File.Delete(CampaignReturnPath);
            }
            File.Move(pendingPath, CampaignReturnPath);

            StartAndExit("singleplayer");
        }

        // The TaleWorlds launcher's Multiplayer tab starts the game with the
        // multiplayer modules only (Native, Multiplayer, this mod): War Sails,
        // SandBoxCore and the campaign modules the arena needs stay unloaded,
        // and a sea battle then crashes while its scene loads. Hand the start
        // over to the desktop shortcut's script, which loads the full set.
        internal static void RestartThroughArenaShortcutAndExit(
            string launchScriptPath)
        {
            if (string.IsNullOrWhiteSpace(launchScriptPath) ||
                !File.Exists(launchScriptPath))
            {
                throw new FileNotFoundException(
                    "arena launch script was not found", launchScriptPath);
            }

            int parentProcessId;
            using (Process current = Process.GetCurrentProcess())
            {
                parentProcessId = current.Id;
            }

            Directory.CreateDirectory(ArenaDirectory);
            string scriptPath = Path.Combine(
                ArenaDirectory,
                "relaunch-" + Guid.NewGuid().ToString("N") + ".cmd");
            string[] script =
            {
                "@echo off",
                "setlocal DisableDelayedExpansion",
                "set \"DZ_PARENT_PID=" + parentProcessId + "\"",
                "set /a DZ_WAITS=0",
                ":wait_for_parent",
                "tasklist /FI \"PID eq %DZ_PARENT_PID%\" /NH 2>NUL | find \"%DZ_PARENT_PID%\" >NUL",
                "if not errorlevel 1 (",
                "  set /a DZ_WAITS+=1",
                "  if %DZ_WAITS% GEQ 60 goto start_arena",
                "  ping 127.0.0.1 -n 2 >NUL",
                "  goto wait_for_parent",
                ")",
                ":start_arena",
                "set \"" + DikozemieLaunchGuard.RelaunchedVariable + "=1\"",
                "start \"\" wscript.exe \"" + EscapeBatchValue(launchScriptPath) + "\"",
                "del \"%~f0\"",
                "exit /b 0"
            };
            File.WriteAllLines(scriptPath, script, new UTF8Encoding(false));

            Process launcher = Process.Start(new ProcessStartInfo
            {
                FileName = scriptPath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = ArenaDirectory
            });
            if (launcher == null)
            {
                throw new InvalidOperationException("arena relaunch helper did not start");
            }

            Console.WriteLine(
                "DikozemieOnline: restarting through the arena shortcut after parent pid=" +
                parentProcessId);
            if (!TerminateProcess(GetCurrentProcess(), 0))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "failed to terminate the incomplete game process");
            }
        }

        private static void StartAndExit(string mode)
        {
            int parentProcessId;
            string executablePath;
            using (Process current = Process.GetCurrentProcess())
            {
                parentProcessId = current.Id;
                executablePath = current.MainModule?.FileName;
            }

            if (string.IsNullOrWhiteSpace(executablePath))
            {
                executablePath = Path.Combine(AppContext.BaseDirectory, "Bannerlord.exe");
            }
            if (!File.Exists(executablePath))
            {
                throw new FileNotFoundException(
                    "Bannerlord.exe was not found for campaign relaunch",
                    executablePath);
            }

            string workingDirectory = Path.GetDirectoryName(executablePath);
            string scriptPath = Path.Combine(
                ArenaDirectory,
                "relaunch-" + Guid.NewGuid().ToString("N") + ".cmd");
            string[] script =
            {
                "@echo off",
                "setlocal DisableDelayedExpansion",
                "set \"DZ_PARENT_PID=" + parentProcessId + "\"",
                ":wait_for_parent",
                "tasklist /FI \"PID eq %DZ_PARENT_PID%\" /NH 2>NUL | find \"%DZ_PARENT_PID%\" >NUL",
                "if not errorlevel 1 (",
                "  ping 127.0.0.1 -n 2 >NUL",
                "  goto wait_for_parent",
                ")",
                "set \"SteamAppId=261550\"",
                "start \"\" /D \"" + EscapeBatchValue(workingDirectory) + "\" \"" +
                    EscapeBatchValue(executablePath) + "\" /" + mode + " \"" +
                    ModuleList + "\"",
                "del \"%~f0\"",
                "exit /b 0"
            };
            File.WriteAllLines(scriptPath, script, new UTF8Encoding(false));

            Process launcher = Process.Start(new ProcessStartInfo
            {
                FileName = scriptPath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = workingDirectory
            });
            if (launcher == null)
            {
                throw new InvalidOperationException("campaign relaunch helper did not start");
            }

            Console.WriteLine(
                "DikozemieOnline: scheduled /" + mode +
                " after parent pid=" + parentProcessId);
            if (!TerminateProcess(GetCurrentProcess(), 0))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "failed to terminate multiplayer process after campaign launch");
            }
        }

        private static string EscapeBatchValue(string value)
        {
            if (value == null || value.IndexOf('"') >= 0 || value.IndexOf('\r') >= 0 ||
                value.IndexOf('\n') >= 0)
            {
                throw new InvalidOperationException("unsafe Bannerlord relaunch path");
            }

            return value.Replace("%", "%%");
        }
    }
}
