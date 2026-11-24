using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    public sealed class DikozemieLocalArenaController : MissionLogic
    {
        private const float ResultPresentationDelaySeconds = 3f;

        private readonly BasicCharacterObject _fighter;
        private readonly int _variant;
        private IReadOnlyList<DikozemieArenaOpponent> _opponents;
        private DikozemieArenaOpponent _currentOpponent;
        private BasicCharacterObject _currentOpponentCharacter;
        private Team _playerTeam;
        private Team _enemyTeam;
        private GameEntity _playerSpawn;
        private GameEntity _enemySpawn;
        private float _targetAssignmentDelay;
        private int _trainingFightNumber;
        private bool _targetAssigned;
        private bool _duelCompleted;
        private bool _resultPending;
        private float _resultPresentationTime;
        private readonly DikozemieArenaKnockoutWindow _knockoutWindow =
            new DikozemieArenaKnockoutWindow();
        private static readonly DateTimeOffset MissionClockEpoch =
            new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private DateTimeOffset KnockoutTime =>
            MissionClockEpoch.AddSeconds(Mission.CurrentTime);

        public DikozemieLocalArenaController()
        {
        }

        public DikozemieLocalArenaController(
            BasicCharacterObject fighter,
            int variant)
        {
            _fighter = fighter;
            _variant = variant;
        }

        public Agent PlayerAgent { get; private set; }
        public Agent BotAgent { get; private set; }

        public bool RequestDuel()
        {
            return PlayerAgent != null && BotAgent != null;
        }

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            Debug.Print(
                "DikozemieOnline: pure local arena initialized",
                0,
                Debug.DebugColor.Green);

            if (_fighter == null)
            {
                return;
            }

            _playerTeam = Mission.Teams.Add(
                BattleSideEnum.Attacker,
                0xffa96a2d,
                0xffffffff,
                null,
                true,
                false,
                true);
            _enemyTeam = Mission.Teams.Add(
                BattleSideEnum.Defender,
                0xff6d1717,
                0xffffffff,
                null,
                false,
                false,
                true);

        }

        public override void AfterStart()
        {
            base.AfterStart();
            if (_fighter == null)
            {
                return;
            }

            _playerSpawn = FindSpawn(
                "mp_spawnpoint_attacker",
                "sp_arena_1");
            _enemySpawn = FindSpawn(
                "mp_spawnpoint_defender",
                "sp_arena_2");
            if (_playerSpawn == null || _enemySpawn == null)
            {
                throw new InvalidOperationException(
                    "The duel scene does not contain attacker and defender spawn points.");
            }

            _opponents = DikozemieArenaOpponentCatalog.Build(_fighter);
            StartNextDuel();

            Debug.Print(
                "DikozemieOnline: local duel agents spawned, variant=" + _variant,
                0,
                Debug.DebugColor.Green);
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (_resultPending)
            {
                if (Mission.CurrentTime >= _resultPresentationTime &&
                    _knockoutWindow.TryResolve(KnockoutTime,
                        out DikozemieArenaDuelOutcome outcome))
                {
                    _resultPending = false;
                    CompleteLocalDuel(outcome);
                }
                return;
            }
            if (_duelCompleted) return;

            if (_targetAssigned || BotAgent == null || PlayerAgent == null)
            {
                return;
            }

            _targetAssignmentDelay += dt;
            if (_targetAssignmentDelay < 0.75f ||
                !BotAgent.IsActive() ||
                !PlayerAgent.IsActive())
            {
                return;
            }

            BotAgent.SetWatchState(Agent.WatchState.Alarmed);
            BotAgent.SetAutomaticTargetSelection(false);
            BotAgent.SetTargetAgent(PlayerAgent);
            _targetAssigned = true;
            Debug.Print(
                "DikozemieOnline: local duel bot target assigned",
                0,
                Debug.DebugColor.Green);
        }

        public override void OnAgentRemoved(
            Agent victim,
            Agent killer,
            AgentState state,
            KillingBlow blow)
        {
            base.OnAgentRemoved(victim, killer, state, blow);
            if (_duelCompleted ||
                (state != AgentState.Unconscious && state != AgentState.Killed))
            {
                return;
            }

            bool? playerVictory = null;
            if (victim == BotAgent)
            {
                playerVictory = true;
            }
            else if (victim == PlayerAgent)
            {
                playerVictory = false;
            }

            if (!playerVictory.HasValue)
            {
                return;
            }

            _knockoutWindow.RegisterDeath(!playerVictory.Value, KnockoutTime,
                TimeSpan.FromSeconds(ResultPresentationDelaySeconds));
            if (!_resultPending)
            {
                _resultPresentationTime = Mission.CurrentTime +
                    ResultPresentationDelaySeconds;
                _resultPending = true;
            }
        }

        private void ShowResultWindow(bool playerVictory, string result, bool draw = false)
        {
            InformationManager.ShowInquiry(
                new InquiryData(
                    "ТРЕНИРОВОЧНЫЙ БОЙ №" + _trainingFightNumber +
                    (draw ? " — НИЧЬЯ" : playerVictory ? " — ПОБЕДА" : " — ПОРАЖЕНИЕ"),
                    result,
                    true,
                    true,
                    "СРАЗИТЬСЯ ЕЩЁ РАЗ",
                    "ВЫЙТИ С АРЕНЫ",
                    StartNextDuel,
                    ExitArena),
                true,
                true);
        }

        private void CompleteLocalDuel(DikozemieArenaDuelOutcome outcome)
        {
            bool playerVictory = outcome == DikozemieArenaDuelOutcome.AttackerVictory;
            bool draw = outcome == DikozemieArenaDuelOutcome.Draw;
            _duelCompleted = true;

            string opponentId = _currentOpponent?.CharacterId ??
                "training_bot";
            string opponentName = _currentOpponentCharacter?.Name?.ToString() ??
                "Боец арены";
            int startingRating = _currentOpponent?.StartingRating ?? 1;
            string result = draw
                ? "Оба бойца погибли. Ничья: рейтинг, победы и золото не изменились."
                : DikozemieArmoryProgression.RecordTrainingResult(
                playerVictory,
                opponentId,
                opponentName,
                startingRating,
                _opponents?.Count ?? 1);
            result += "\n" + DikozemieHeroProfile.RecordBattleResult(
                playerVictory);
            _targetAssigned = false;
            if (!draw && playerVictory && PlayerAgent?.IsActive() == true)
            {
                PlayerAgent.Controller = AgentControllerType.None;
            }
            else if (!draw && !playerVictory && BotAgent?.IsActive() == true)
            {
                BotAgent.Controller = AgentControllerType.None;
            }
            Debug.Print(
                "DikozemieOnline: local duel completed, victory=" +
                playerVictory + ", draw=" + draw + ", fight=" +
                _trainingFightNumber + ", opponentRank=" +
                startingRating + ", result=" + result,
                0,
                playerVictory
                    ? Debug.DebugColor.Green
                    : Debug.DebugColor.Red);

            DikozemieArmoryProgression.TryConsumeTrainingResult(
                out _,
                out _);
            ShowResultWindow(playerVictory, result, draw);
        }

        private void StartNextDuel()
        {
            _resultPending = false;
            _knockoutWindow.Reset();
            Mission.ClearCorpses(false);
            Mission.RemoveSpawnedItemsAndMissiles();

            _trainingFightNumber++;
            _currentOpponent = DikozemieArenaOpponentCatalog.SelectForPlayer(
                _opponents,
                DikozemieArmoryProgression.PlayerRating,
                _currentOpponent?.CharacterId);
            BasicCharacterObject opponent =
                DikozemieArenaOpponentCatalog.ResolveCharacter(
                    _currentOpponent,
                    _fighter);
            _currentOpponentCharacter = opponent;

            if (PlayerAgent == null || !PlayerAgent.IsActive())
            {
                PlayerAgent = Mission.SpawnAgent(
                    CreateAgentBuildData(
                        _fighter,
                        _playerTeam,
                        AgentControllerType.Player,
                        _playerSpawn),
                    false);
                DikozemieHeroProfile.ApplyHealthBonus(PlayerAgent);
            }
            else
            {
                ResetAgent(
                    PlayerAgent,
                    _fighter,
                    AgentControllerType.Player,
                    _playerSpawn);
            }
            Mission.MainAgent = PlayerAgent;

            if (BotAgent?.IsActive() == true)
            {
                BotAgent.Controller = AgentControllerType.None;
                BotAgent.FadeOut(hideInstantly: true, hideMount: true);
            }
            BotAgent = Mission.SpawnAgent(
                CreateAgentBuildData(
                    opponent,
                    _enemyTeam,
                    AgentControllerType.AI,
                    _enemySpawn),
                false);

            InformationManager.DisplayMessage(
                new InformationMessage(
                    "Тренировочный бой №" + _trainingFightNumber +
                    ". Рейтинг соперника: " +
                    DikozemieArenaOpponentCatalog.GetCurrentRating(
                        _currentOpponent) +
                    " — " + (opponent.Name?.ToString() ?? "Боец арены") +
                    ". Рейтинг героя: " +
                    DikozemieArmoryProgression.PlayerRating + ".",
                    Colors.Yellow));

            _duelCompleted = false;
            _targetAssigned = false;
            _targetAssignmentDelay = 0f;
            Debug.Print(
                "DikozemieOnline: next local duel started, opponent=" +
                opponent.StringId + ", fight=" +
                _trainingFightNumber + ", playerRating=" +
                DikozemieArmoryProgression.PlayerRating + ", rank=" +
                DikozemieArenaOpponentCatalog.GetCurrentRating(
                    _currentOpponent) + ", total=" +
                (_opponents?.Count ?? 1),
                0,
                Debug.DebugColor.Green);
        }

        private void ExitArena()
        {
            DikozemieLobbyShellIntegration.PrepareForTrainingExit();
            if (!Mission.MissionEnded && !Mission.IsMissionEnding)
            {
                Mission.EndMission();
            }
        }

        private static void ResetAgent(
            Agent agent,
            BasicCharacterObject character,
            AgentControllerType controller,
            GameEntity spawnPoint)
        {
            agent.Controller = AgentControllerType.None;
            agent.UpdateSpawnEquipmentAndRefreshVisuals(
                controller == AgentControllerType.Player
                    ? DikozemieHeroProfile.CreateBattleEquipment(character)
                    : character.FirstBattleEquipment ?? character.Equipment);
            agent.Health = agent.HealthLimit;
            agent.TeleportToPosition(spawnPoint.GlobalPosition);
            agent.Controller = controller;
        }

        private AgentBuildData CreateAgentBuildData(
            BasicCharacterObject character,
            Team team,
            AgentControllerType controller,
            GameEntity spawnPoint)
        {
            AgentBuildData buildData = new AgentBuildData(new BasicBattleAgentOrigin(character))
                .Team(team)
                .Controller(controller)
                .Equipment(character.FirstBattleEquipment ?? character.Equipment)
                .FixedEquipment(true)
                .InitialFrameFromSpawnPointEntity(spawnPoint);
            return controller == AgentControllerType.Player
                ? DikozemieHeroProfile.ApplyToPlayerBuild(buildData, character)
                : buildData;
        }

        private GameEntity FindSpawn(string primaryTag, string arenaTag)
        {
            return Mission.Scene.FindEntityWithTag(primaryTag) ??
                Mission.Scene.FindEntityWithTag(arenaTag) ??
                Mission.Scene.FindEntityWithTag("mp_spawnpoint") ??
                Mission.Scene.FindEntityWithTag("spawnpoint") ??
                Mission.Scene.FindEntityWithTag("sp_arena");
        }
    }
}
