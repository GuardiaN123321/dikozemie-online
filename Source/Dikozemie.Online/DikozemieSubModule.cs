using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;

namespace Dikozemie.Online
{
    public sealed class DikozemieSubModule : MBSubModuleBase
    {
        public const string ModuleId = "DikozemieOnline";
        public const string ArenaGameType = "DikozemieArena";

#if !CLIENT
        private static readonly HashSet<NetworkCommunicator>
            StandalonePeersWithPublishedTeam =
                new HashSet<NetworkCommunicator>();
#endif

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TraceFirstChanceExceptions();
            Module.CurrentModule.AddMultiplayerGameMode(new MissionMultiplayerDikozemieArenaMode(ArenaGameType));
#if CLIENT
            // Started from the TaleWorlds launcher without War Sails and the
            // campaign modules: restart through the shortcut before anything
            // else loads (the process ends here in that case).
            DikozemieLaunchGuard.EnsureFullModuleSet();
            SkipLogoVideo();
            TrimClientLog();
            DikozemieArmoryProgression.Initialize();
            DikozemieHeroProfile.Initialize();
#endif
            Console.WriteLine("DikozemieOnline: multiplayer battle mode registered");
        }

#if CLIENT
        // The logo video plays once per launch; players come here to fight.
        private static void SkipLogoVideo()
        {
            try
            {
                typeof(Module)
                    .GetField(
                        "_splashScreenPlayed",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic)
                    ?.SetValue(Module.CurrentModule, true);
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: logo video stays: " + error.Message);
            }
        }

        // client-ui.log has no rotation and grew past a gigabyte on a tester's disk.
        private static void TrimClientLog()
        {
            try
            {
                string log = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Dikozemie",
                    "logs",
                    "client-ui.log");
                var info = new System.IO.FileInfo(log);
                if (info.Exists && info.Length > 8L * 1024L * 1024L)
                {
                    string previous = log + ".old";
                    System.IO.File.Delete(previous);
                    System.IO.File.Move(log, previous);
                    new System.IO.FileInfo(previous).Refresh();
                    using (var stream = new System.IO.FileStream(previous, System.IO.FileMode.Open, System.IO.FileAccess.Write))
                    {
                        stream.SetLength(Math.Min(stream.Length, 8L * 1024L * 1024L));
                    }
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: client log stays: " + error.Message);
            }
        }
#endif

        protected override void InitializeGameStarter(Game game, IGameStarter starterObject)
        {
            base.InitializeGameStarter(game, starterObject);
            // This module remains loaded while the native War Sails custom
            // game is active. Arena models are multiplayer-only: replacing
            // CustomGame's naval models corrupts MissionShip initialization
            // in native code (0xc0000005 after the first hull is created).
            if (game?.GameType is MultiplayerGame &&
                starterObject is BasicGameStarter multiplayerStarter)
            {
                multiplayerStarter.AddModel<AgentStatCalculateModel>(
                    new DikozemieArenaAgentStatCalculateModel());
                multiplayerStarter.AddModel<AgentApplyDamageModel>(
                    new DikozemieArenaAgentApplyDamageModel());
            }
        }

        // The items of a multiplayer game are known once its initialization
        // ends: write their registry (see DikozemieItemRegistry).
        public override void OnGameInitializationFinished(Game game)
        {
            base.OnGameInitializationFinished(game);
            if (game?.GameType is MultiplayerGame)
            {
#if SERVER
                DikozemieItemRegistry.Write(DikozemieItemRegistry.DefaultPath(), Console.WriteLine);
#else
                DikozemieItemRegistry.Write(
                    DikozemieItemRegistry.DefaultPath(),
                    message => TaleWorlds.Library.Debug.Print(message));
#endif
            }
        }

        protected override void OnSubModuleUnloaded()
        {
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
#if CLIENT
            DikozemiePvpCancelOverlayController.AbandonForApplicationShutdown();
            DikozemieDuelOfferController.AbandonForApplicationShutdown();
            DikozemiePvpSearchController.Reset();
            DikozemieRankedTransitionController.AbandonForApplicationShutdown();
            DikozemieHeroAppearanceEditor.AbandonForApplicationShutdown();
            DikozemieArmoryLobbyIntegration.Reset();
            DikozemieHeroMenuController.AbandonForApplicationShutdown();
            DikozemieLobbyShellIntegration.AbandonForApplicationShutdown();
#endif
            base.OnSubModuleUnloaded();
        }

        // Diagnostics for porting the dedicated server: with
        // DIKOZEMIE_TRACE_FIRST_CHANCE=1 the first exceptions the game throws
        // (and maybe catches itself) are written with their full call stack.
        // The native Linux server stopped loading items after a caught
        // exception the Windows build survives (2026-11-10).
        private static int _firstChanceTraced;

        private static void TraceFirstChanceExceptions()
        {
            if (Environment.GetEnvironmentVariable("DIKOZEMIE_TRACE_FIRST_CHANCE") != "1")
            {
                return;
            }
            AppDomain.CurrentDomain.FirstChanceException += (sender, args) =>
            {
                if (System.Threading.Interlocked.Increment(ref _firstChanceTraced) > 40)
                {
                    return;
                }
                try
                {
                    Console.WriteLine("DikozemieOnline: first-chance " + args.Exception.GetType().FullName + ": " +
                        args.Exception.Message + Environment.NewLine + new System.Diagnostics.StackTrace(1, false));
                }
                catch
                {
                    // Diagnostics only.
                }
            };
        }

        private static void OnUnhandledException(
            object sender,
            UnhandledExceptionEventArgs eventArgs)
        {
            string details = eventArgs.ExceptionObject?.ToString() ??
                "Unknown unhandled exception";
            Console.WriteLine("DikozemieOnline: UNHANDLED " + details);
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Dikozemie",
                    "logs");
                Directory.CreateDirectory(folder);
                File.WriteAllText(
                    Path.Combine(folder, "arena-unhandled.txt"),
                    DateTimeOffset.Now.ToString("O") + Environment.NewLine + details);
            }
            catch
            {
                // The original exception must remain the useful failure signal.
            }
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
#if CLIENT
            // The launcher owns the MultiplayerGame -> no Game -> native
            // War Sails CustomGame transition, so it must keep ticking on
            // both sides of the multiplayer-only guard below.
            DikozemieNavalBattleLauncher.Tick();
            DikozemieNavalAutoboardingController.Tick();
            DikozemieDuelOfferController.Tick();
            if (!(Game.Current?.GameType is MultiplayerGame))
            {
                return;
            }
#endif
            DikozemieArenaServerRegistration.Tick();
#if !CLIENT
            PublishStandalonePeerTeamsAfterSynchronization();
            MissionMultiplayerDikozemieArena.TickStandaloneAdmissionFromSubModule();
#endif
#if CLIENT
            DikozemieHeroProfile.TickMigrations();
            DikozemieItemStatsDump.Tick(dt);
            DikozemieHeroAppearanceEditor.Tick(dt);
            DikozemiePvpCancelOverlayController.Tick();
            DikozemieHeroMenuController.Tick();
            DikozemieLobbyShellIntegration.Tick();
            DikozemieArmoryLobbyIntegration.Tick();
            DikozemieEconomy.Tick();
            DikozemieWorld.Tick();
            DikozemieWatch.Tick();
#endif
        }


#if !CLIENT
        private static void PublishStandalonePeerTeamsAfterSynchronization()
        {
            if (DikozemieArenaServerRegistration.UsesExternalMatchmaker ||
                Mission.Current == null ||
                (!GameNetwork.IsServerOrRecorder && !GameNetwork.IsDedicatedServer))
            {
                StandalonePeersWithPublishedTeam.Clear();
                return;
            }

            Mission mission = Mission.Current;
            MultiplayerTeamSelectComponent teamSelect =
                mission.GetMissionBehavior<MultiplayerTeamSelectComponent>();
            Team attacker = mission.Teams?.Attacker;
            if (teamSelect == null || attacker == null)
            {
                return;
            }

            StandalonePeersWithPublishedTeam.RemoveWhere(peer =>
                peer == null || !peer.IsConnectionActive);

            foreach (NetworkCommunicator peer in GameNetwork.NetworkPeers.ToArray())
            {
                if (peer == null || peer.IsServerPeer ||
                    !peer.IsConnectionActive || !peer.IsSynchronized ||
                    StandalonePeersWithPublishedTeam.Contains(peer))
                {
                    continue;
                }

                MissionPeer missionPeer = peer.GetComponent<MissionPeer>();
                if (missionPeer == null)
                {
                    continue;
                }

                // The native lobby accepts the UDP connection before the peer is
                // synchronized. A team message sent in that interval is not echoed
                // to the joining client, which then remains on team selection and
                // quits the custom game after the native 15-second grace period.
                // Publish once from the application tick after the authoritative
                // synchronized flag is true, independent of game-mode callbacks.
                teamSelect.ChangeTeamServer(peer, attacker);
                missionPeer.HasSpawnedAgentVisuals = false;
                missionPeer.SpawnTimer.Reset(mission.CurrentTime, 0.1f);
                StandalonePeersWithPublishedTeam.Add(peer);
                Console.WriteLine(
                    "DikozemieOnline: published synchronized team for " +
                    peer.UserName);
            }
        }
#endif
    }
}
