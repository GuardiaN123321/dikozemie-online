using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Diamond;
using TaleWorlds.MountAndBlade.Multiplayer;
using TaleWorlds.MountAndBlade.Multiplayer.Missions;
using TaleWorlds.MountAndBlade.Source.Missions;

namespace Dikozemie.Online
{
    public sealed class MissionMultiplayerDikozemieArenaMode : MissionBasedMultiplayerGameMode
    {
        public MissionMultiplayerDikozemieArenaMode(string name) : base(name)
        {
        }

        public override void JoinCustomGame(JoinGameData joinGameData)
        {
            // Keep TaleWorlds' lobby reservation and the actual endpoint coupled.
            // Replacing the advertised address with a LAN address lets UDP sync
            // start, but the lobby closes that mismatched custom-game session
            // after its 15-second join grace period.
            base.JoinCustomGame(joinGameData);
        }

        public override void StartMultiplayerGame(string scene)
        {
            MissionInitializerRecord record = new MissionInitializerRecord(scene);
#if CLIENT
            if (DikozemieArenaSceneRules.IsNavalBoardingScene(scene))
            {
                // What War Sails passes for its own battles: without it the sea
                // is a flat mirror, whatever the water strength says.
                record.PlayingInCampaignMode = true;
                record.DecalAtlasGroup = 2;
                record.AtmosphereOnCampaign = new TaleWorlds.Library.AtmosphereInfo
                {
                    TimeInfo = new TaleWorlds.Library.TimeInformation { Season = 1, TimeOfDay = 16 },
                    NauticalInfo = new TaleWorlds.Library.NauticalInformation
                    {
                        // The engine multiplies this by 30; the sky smears
                        // into streaks when the scene wind is overshot.
                        WindVector = new TaleWorlds.Library.Vec2(0f, 8.7f / 30f),
                        CanUseLowAltitudeAtmosphere = 1,
                        UsesNavalSimulatedWater = 1
                    }
                };
            }
#endif
            MissionState.OpenNew(
                DikozemieSubModule.ArenaGameType,
                record,
#if CLIENT
                missionController => GameNetwork.IsDedicatedServer || GameNetwork.IsServer
                    ? CreateServerBehaviors()
                    : CreateClientBehaviors(),
#else
                // The dedicated-server bootstrap opens the mission before the
                // runtime GameNetwork role flags are stable. A server build must
                // never fall through to the client-only behavior set.
                missionController => CreateServerBehaviors(),
#endif
                true,
                true);
        }

        private static MissionBehavior[] CreateServerBehaviors()
        {
            return new MissionBehavior[]
            {
                MissionLobbyComponent.CreateBehavior(),
                new MissionMultiplayerDikozemieArena(),
                new MissionMultiplayerDikozemieArenaClient(),
                new VoiceChatHandler(),
                new DikozemieBattleCryBehavior(),
                new MultiplayerTimerComponent(),
                new MultiplayerBattleMissionAgentInteractionLogic(),
                new DikozemieNavalBoardingBehavior(),
#if CLIENT
                new MultiplayerMissionAgentVisualSpawnComponent(),
                new ConsoleMatchStartEndHandler(),
#endif
                new DikozemieArenaSpawnComponent(new DikozemieArenaSpawnFrameBehavior(), new DikozemieArenaSpawningBehavior()),
                new MissionLobbyEquipmentNetworkComponent(),
                new MultiplayerTeamSelectComponent(),
                new MissionHardBorderPlacer(),
                new MissionBoundaryPlacer(),
                new MultiplayerPollComponent(),
                new MultiplayerAdminComponent(),
                new MultiplayerGameNotificationsComponent(),
                new MissionOptionsComponent(),
                new MissionScoreboardComponent(new DikozemieArenaScoreboardData()),
                new MissionAgentPanicHandler(),
                new AgentHumanAILogic(),
                new EquipmentControllerLeaveLogic(),
                new MultiplayerPreloadHelper()
            };
        }

        private static MissionBehavior[] CreateClientBehaviors()
        {
            return new MissionBehavior[]
            {
                MissionLobbyComponent.CreateBehavior(),
                new MissionMultiplayerDikozemieArenaClient(),
                new VoiceChatHandler(),
                new DikozemieBattleCryBehavior(),
                new MultiplayerAchievementComponent(),
                new MultiplayerTimerComponent(),
                new MultiplayerBattleMissionAgentInteractionLogic(),
                new DikozemieNavalBoardingBehavior(),
#if CLIENT
                new MultiplayerMissionAgentVisualSpawnComponent(),
                new ConsoleMatchStartEndHandler(),
#endif
                new MissionLobbyEquipmentNetworkComponent(),
                new MultiplayerTeamSelectComponent(),
                new MissionHardBorderPlacer(),
                new MissionBoundaryPlacer(),
                new MultiplayerPollComponent(),
                new MultiplayerAdminComponent(),
                new MultiplayerGameNotificationsComponent(),
                new MissionOptionsComponent(),
                new MissionScoreboardComponent(new DikozemieArenaScoreboardData()),
                new EquipmentControllerLeaveLogic(),
                new MissionRecentPlayersComponent(),
                new MultiplayerPreloadHelper()
            };
        }
    }
}
