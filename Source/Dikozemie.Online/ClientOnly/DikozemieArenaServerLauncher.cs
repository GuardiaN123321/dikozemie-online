using System;
using System.Diagnostics;
using System.IO;

namespace Dikozemie.Online
{
    internal enum DikozemieArenaServerLaunchResult
    {
        Started,
        AlreadyRunning,
        MissingToken,
        NotInstalled,
        Failed
    }

    internal static class DikozemieArenaServerLauncher
    {
        private const string ModuleList =
            "_MODULES_*Native*SandBoxCore*Multiplayer*DikozemieOnline*_MODULES_";
        private const string ConfigFileName = "ds_config_dikozemie_arena.txt";

        internal static DikozemieArenaServerLaunchResult Start()
        {
            try
            {
                if (IsRunning())
                {
                    return DikozemieArenaServerLaunchResult.AlreadyRunning;
                }

                if (!HasLocalToken())
                {
                    return DikozemieArenaServerLaunchResult.MissingToken;
                }

                string serverRoot = FindServerRoot();
                if (serverRoot == null)
                {
                    return DikozemieArenaServerLaunchResult.NotInstalled;
                }

                string serverBin = Path.Combine(serverRoot, "bin", "Win64_Shipping_Server");
                string starter = Path.Combine(serverBin, "DedicatedCustomServer.Starter.exe");
                string moduleConfig = Path.Combine(
                    serverRoot,
                    "Modules",
                    "DikozemieOnline",
                    "Server",
                    ConfigFileName);
                string nativeConfig = Path.Combine(
                    serverRoot,
                    "Modules",
                    "Native",
                    ConfigFileName);
                if (!File.Exists(starter) || !File.Exists(moduleConfig))
                {
                    return DikozemieArenaServerLaunchResult.NotInstalled;
                }

                File.Copy(moduleConfig, nativeConfig, true);
                Process process = Process.Start(new ProcessStartInfo
                {
                    FileName = starter,
                    Arguments = "\"" + ModuleList + "\" " +
                        "/dedicatedcustomserverconfigfile " + ConfigFileName + " " +
                        "/port 7210",
                    WorkingDirectory = serverBin,
                    UseShellExecute = false,
                    CreateNoWindow = false
                });
                if (process == null)
                {
                    return DikozemieArenaServerLaunchResult.Failed;
                }

                Console.WriteLine(
                    "DikozemieOnline: dedicated test server requested pid=" + process.Id);
                return DikozemieArenaServerLaunchResult.Started;
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "DikozemieOnline: failed to start dedicated server: " + exception);
                return DikozemieArenaServerLaunchResult.Failed;
            }
        }

        private static bool IsRunning()
        {
            Process[] processes = Process.GetProcessesByName("DedicatedCustomServer.Starter");
            try
            {
                return processes.Length > 0;
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }
        }

        private static bool HasLocalToken()
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string tokenDirectory = Path.Combine(
                documents,
                "Mount & Blade II Bannerlord",
                "Tokens");
            return Directory.Exists(tokenDirectory) &&
                Directory.GetFiles(tokenDirectory).Length > 0;
        }

        private static string FindServerRoot()
        {
            string configured = Environment.GetEnvironmentVariable("BANNERLORD_SERVER_DIR");
            string[] candidates =
            {
                configured,
                @"D:\SteamLibrary\steamapps\common\Mount & Blade II Dedicated Server",
                @"C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Dedicated Server"
            };
            foreach (string candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                string starter = Path.Combine(
                    candidate,
                    "bin",
                    "Win64_Shipping_Server",
                    "DedicatedCustomServer.Starter.exe");
                if (File.Exists(starter))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
