using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Multiplayer.Missions;
using TaleWorlds.ObjectSystem;

namespace Dikozemie.Online
{
    internal static class DikozemieLocalArenaMission
    {
        private const string FighterId = "mp_heavy_infantry_empire_troop";
        private const string ImperialArenaScene = "arena_empire_a";

        internal static Mission Open(string scene)
        {
            BasicCharacterObject fighter =
                MBObjectManager.Instance.GetObject<BasicCharacterObject>(FighterId);
            if (fighter == null)
            {
                throw new InvalidOperationException(
                    "Empire multiplayer fighter data is unavailable.");
            }

            fighter = DikozemieHeroProfile.ResolveCharacter(fighter);

            Game.Current.PlayerTroop = fighter;
            var initializer = new MissionInitializerRecord(scene)
            {
                DoNotUseLoadingScreen = false,
                PlayingInCampaignMode = false,
                SceneLevels = string.Empty,
                DecalAtlasGroup = 2
            };
            return MissionState.OpenNew(
                DikozemieArenaLobbyController.LocalArenaMissionName,
                initializer,
                missionController => CreateBehaviors(),
                true,
                true);
        }

        internal static Mission OpenTrainingBattle(int variant)
        {
            BasicCharacterObject fighter =
                MBObjectManager.Instance.GetObject<BasicCharacterObject>(FighterId);
            if (fighter == null)
            {
                throw new InvalidOperationException(
                    "Empire multiplayer training data is unavailable.");
            }

            fighter = DikozemieHeroProfile.ResolveCharacter(fighter);

            Game.Current.PlayerTroop = fighter;
            var initializer = new MissionInitializerRecord(ImperialArenaScene)
            {
                DoNotUseLoadingScreen = false,
                PlayingInCampaignMode = false,
                SceneLevels = string.Empty,
                DecalAtlasGroup = 2
            };
            return MissionState.OpenNew(
                DikozemieArenaLobbyController.LocalArenaMissionName,
                initializer,
                mission => CreateTrainingBehaviors(fighter, variant),
                true,
                true);
        }

        internal static Mission OpenTournament()
        {
            BasicCharacterObject fallbackFighter =
                MBObjectManager.Instance.GetObject<BasicCharacterObject>(FighterId);
            BasicCharacterObject fighter = DikozemieHeroProfile.ResolveCharacter(
                Game.Current?.PlayerTroop ?? fallbackFighter);
            if (fighter == null)
            {
                throw new InvalidOperationException(
                    "Empire multiplayer tournament data is unavailable.");
            }

            Game.Current.PlayerTroop = fighter;
            var initializer = new MissionInitializerRecord(ImperialArenaScene)
            {
                DoNotUseLoadingScreen = false,
                PlayingInCampaignMode = false,
                SceneLevels = string.Empty,
                DecalAtlasGroup = 2
            };
            return MissionState.OpenNew(
                DikozemieArenaLobbyController.LocalArenaMissionName,
                initializer,
                mission => CreateTournamentBehaviors(fighter),
                true,
                true);
        }

        private static IEnumerable<MissionBehavior> CreateTrainingBehaviors(
            BasicCharacterObject fighter,
            int variant)
        {
            MissionLogic fightController = variant == 1
                ? (MissionLogic)new DikozemieLocalArenaPracticeController(fighter)
                : new DikozemieLocalArenaController(fighter, 0);
            return new MissionBehavior[]
            {
                new AgentHumanAILogic(),
                new MultiplayerPracticeMissionComponent(),
                fightController
            };
        }

        private static IEnumerable<MissionBehavior> CreateTournamentBehaviors(
            BasicCharacterObject fighter)
        {
            return new MissionBehavior[]
            {
                new AgentHumanAILogic(),
                new MultiplayerPracticeMissionComponent(),
                new DikozemieLocalArenaTournamentController(fighter)
            };
        }

        private static IEnumerable<MissionBehavior> CreateBehaviors()
        {
            return new MissionBehavior[]
            {
                new DikozemieLocalArenaController()
            };
        }
    }
}
