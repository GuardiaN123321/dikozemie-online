using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Multiplayer.View.MissionViews;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace Dikozemie.Online
{
    [ViewCreatorModule]
    public static class DikozemieArenaMissionView
    {
        [ViewMethod(DikozemieSubModule.ArenaGameType)]
        public static MissionView[] OpenArena(Mission mission)
        {
            var views = new List<MissionView>
            {
                MultiplayerViewCreator.CreateMissionServerStatusUIHandler(),
                MultiplayerViewCreator.CreateMissionMultiplayerPreloadView(mission),
                MultiplayerViewCreator.CreateMissionKillNotificationUIHandler(),
                ViewCreator.CreateMissionAgentStatusUIHandler(mission),
                ViewCreator.CreateMissionMainAgentEquipmentController(mission),
                ViewCreator.CreateMissionMainAgentCheerBarkControllerView(mission),
                MultiplayerViewCreator.CreateMissionMultiplayerEscapeMenu(
                    DikozemieSubModule.ArenaGameType),
                MultiplayerViewCreator.CreateMissionScoreBoardUIHandler(mission, false),
                MultiplayerViewCreator.CreateMultiplayerEndOfRoundUIHandler(),
                MultiplayerViewCreator.CreateMultiplayerEndOfBattleUIHandler(),
                MultiplayerViewCreator.CreateLobbyEquipmentUIHandler(),
                new DikozemieBattleCryView(),
                new DikozemieObserverView(),
                MultiplayerViewCreator.CreatePollProgressUIHandler(),
                MultiplayerViewCreator.CreateMissionFlagMarkerUIHandler(),
                MultiplayerViewCreator.CreateMultiplayerMissionDeathCardUIHandler(),
                ViewCreator.CreateOptionsUIHandler(),
                ViewCreator.CreateMissionMainAgentEquipDropView(mission),
                new MissionItemContourControllerView(),
                new MissionAgentContourControllerView(),
                new DikozemieArenaDuelStartView(),
                new MissionGauntletDikozemieArenaUI()
            };
            // The friendly banners cover a crowded deck: 64 fighters stand within
            // a few metres of the camera, and crews already differ by their dress.
            if (!DikozemieArenaSceneRules.IsNavalBoardingScene(
                MultiplayerOptions.OptionType.Map.GetStrValue()))
            {
                views.Add(ViewCreator.CreateMissionAgentLabelUIHandler(mission));
            }
            return views.ToArray();
        }

        [ViewMethod(DikozemieArenaLobbyController.LocalArenaMissionName)]
        public static MissionView[] OpenLocalArena(Mission mission)
        {
            return new MissionView[]
            {
                new MissionMainAgentController(),
                new MissionGauntletDikozemieTournamentUI()
            };
        }
    }
}
