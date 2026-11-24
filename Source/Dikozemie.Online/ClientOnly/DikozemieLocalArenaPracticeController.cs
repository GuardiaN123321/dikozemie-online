using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    public sealed class DikozemieLocalArenaPracticeController : MissionLogic
    {
        private const int TotalOpponentCount = 30;
        private const int MaxAliveOpponentCount = 6;

        private readonly BasicCharacterObject _fighter;
        private readonly List<Team> _opponentTeams = new List<Team>();
        private readonly List<Agent> _activeOpponents = new List<Agent>();
        private IReadOnlyList<DikozemieArenaOpponent> _opponents;
        private List<GameEntity> _initialSpawnPoints;
        private List<GameEntity> _respawnPoints;
        private Team _playerTeam;
        private Agent _playerAgent;
        private int _spawnedOpponentCount;
        private int _aliveOpponentCount;
        private int _opponentsDefeatedByPlayer;
        private float _nextSpawnTime;
        private bool _practiceCompleted;

        public DikozemieLocalArenaPracticeController(
            BasicCharacterObject fighter)
        {
            _fighter = fighter;
        }

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            _playerTeam = Mission.Teams.Add(
                BattleSideEnum.Defender,
                0xffa96a2d,
                0xffffffff,
                null,
                true,
                false,
                true);
            Mission.PlayerTeam = _playerTeam;

            for (int index = 0; index < MaxAliveOpponentCount; index++)
            {
                Team team = Mission.Teams.Add(
                    BattleSideEnum.Attacker,
                    0xff6d1717,
                    0xffffffff,
                    null,
                    false,
                    false,
                    true);
                team.SetIsEnemyOf(_playerTeam, true);
                foreach (Team existing in _opponentTeams)
                {
                    team.SetIsEnemyOf(existing, true);
                }
                _opponentTeams.Add(team);
            }

            Debug.Print(
                "DikozemieOnline: local single-player-style practice initialized",
                0,
                Debug.DebugColor.Green);
        }

        public override void AfterStart()
        {
            base.AfterStart();
            _opponents = DikozemieArenaOpponentCatalog.Build(_fighter);
            _initialSpawnPoints = FindSpawnPoints("sp_arena");
            _respawnPoints = FindSpawnPoints("sp_arena_respawn");
            if (_initialSpawnPoints.Count == 0)
            {
                _initialSpawnPoints = FindSpawnPoints("mp_spawnpoint");
            }
            if (_initialSpawnPoints.Count == 0)
            {
                throw new InvalidOperationException(
                    "The imperial arena does not contain practice spawn points.");
            }
            if (_respawnPoints.Count == 0)
            {
                _respawnPoints = _initialSpawnPoints;
            }

            StartPracticeFight();
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (_practiceCompleted)
            {
                return;
            }

            if (_playerAgent == null || !_playerAgent.IsActive())
            {
                CompletePractice(false);
                return;
            }

            if (_spawnedOpponentCount >= TotalOpponentCount)
            {
                if (_aliveOpponentCount == 0)
                {
                    CompletePractice(true);
                }
                return;
            }

            if (_aliveOpponentCount < MaxAliveOpponentCount &&
                (_aliveOpponentCount <= 2 || Mission.CurrentTime >= _nextSpawnTime))
            {
                SpawnOpponent(false);
                ScheduleNextSpawn();
            }
        }

        public override void OnAgentRemoved(
            Agent victim,
            Agent killer,
            AgentState state,
            KillingBlow blow)
        {
            base.OnAgentRemoved(victim, killer, state, blow);
            if (_practiceCompleted ||
                (state != AgentState.Unconscious && state != AgentState.Killed))
            {
                return;
            }

            if (victim == _playerAgent)
            {
                CompletePractice(false);
                return;
            }

            if (!_activeOpponents.Remove(victim))
            {
                return;
            }

            _aliveOpponentCount = Math.Max(0, _aliveOpponentCount - 1);
            if (killer == _playerAgent)
            {
                _opponentsDefeatedByPlayer++;
            }

            if (_spawnedOpponentCount >= TotalOpponentCount &&
                _aliveOpponentCount == 0)
            {
                CompletePractice(true);
            }
        }

        private void StartPracticeFight()
        {
            Mission.ClearCorpses(false);
            Mission.RemoveSpawnedItemsAndMissiles();

            if (_playerAgent == null || !_playerAgent.IsActive())
            {
                _playerAgent = Mission.SpawnAgent(
                    CreateAgentBuildData(
                        _fighter,
                        _playerTeam,
                        AgentControllerType.Player,
                        GetSpawnPoint(0, false)),
                    false);
                DikozemieHeroProfile.ApplyHealthBonus(_playerAgent);
            }
            else
            {
                ResetAgent(
                    _playerAgent,
                    AgentControllerType.Player,
                    GetSpawnPoint(0, false));
            }
            Mission.MainAgent = _playerAgent;

            List<Agent> reusableOpponents = _activeOpponents
                .Where(agent => agent != null && agent.IsActive())
                .Take(MaxAliveOpponentCount)
                .ToList();
            _activeOpponents.Clear();
            _spawnedOpponentCount = 0;
            _aliveOpponentCount = 0;
            _opponentsDefeatedByPlayer = 0;
            _practiceCompleted = false;

            foreach (Agent opponent in reusableOpponents)
            {
                ResetAgent(
                    opponent,
                    AgentControllerType.AI,
                    GetSpawnPoint(_spawnedOpponentCount + 1, false));
                opponent.SetWatchState(Agent.WatchState.Alarmed);
                _activeOpponents.Add(opponent);
                _spawnedOpponentCount++;
                _aliveOpponentCount++;
            }

            while (_aliveOpponentCount < MaxAliveOpponentCount)
            {
                SpawnOpponent(true);
            }
            ScheduleNextSpawn();

            Debug.Print(
                "DikozemieOnline: arena practice fight started, opponents=" +
                TotalOpponentCount + ", simultaneously=" +
                MaxAliveOpponentCount,
                0,
                Debug.DebugColor.Green);
        }

        private void SpawnOpponent(bool initialSpawn)
        {
            int catalogIndex = _spawnedOpponentCount % _opponents.Count;
            DikozemieArenaOpponent opponentEntry = _opponents[catalogIndex];
            BasicCharacterObject character =
                DikozemieArenaOpponentCatalog.ResolveCharacter(
                    opponentEntry,
                    _fighter);
            Team team = _opponentTeams[
                _spawnedOpponentCount % _opponentTeams.Count];
            Agent agent = Mission.SpawnAgent(
                CreateAgentBuildData(
                    character,
                    team,
                    AgentControllerType.AI,
                    GetSpawnPoint(
                        _spawnedOpponentCount + 1,
                        !initialSpawn)),
                false);
            agent.SetWatchState(Agent.WatchState.Alarmed);
            _activeOpponents.Add(agent);
            _spawnedOpponentCount++;
            _aliveOpponentCount++;
        }

        private void ScheduleNextSpawn()
        {
            _nextSpawnTime = Mission.CurrentTime +
                Math.Max(4f, 14f - _spawnedOpponentCount / 3f);
        }

        private void CompletePractice(bool playerSurvived)
        {
            if (_practiceCompleted)
            {
                return;
            }

            _practiceCompleted = true;
            if (_playerAgent?.IsActive() == true)
            {
                _playerAgent.Controller = AgentControllerType.None;
            }
            foreach (Agent opponent in _activeOpponents)
            {
                if (opponent?.IsActive() == true)
                {
                    opponent.Controller = AgentControllerType.None;
                }
            }

            string result = playerSurvived
                ? "Вы одолели всех бойцов арены."
                : "Повержено противников: " +
                    _opponentsDefeatedByPlayer + " из " +
                    TotalOpponentCount + ".";
            int earnedResources =
                DikozemieArmoryProgression.AwardPracticeResources(
                    _opponentsDefeatedByPlayer,
                    playerSurvived,
                    out int totalResources);
            result += "\nМонеты: +" + earnedResources +
                " (по одной за каждые 10 убийств" +
                (playerSurvived ? " и +1 за выживание" : string.Empty) +
                "). Всего: " + totalResources + ".";
            result += "\n" + DikozemieHeroProfile.RecordBattleResult(
                playerSurvived,
                _opponentsDefeatedByPlayer);
            Debug.Print(
                "DikozemieOnline: arena practice completed, survived=" +
                playerSurvived + ", defeated=" +
                _opponentsDefeatedByPlayer,
                0,
                playerSurvived
                    ? Debug.DebugColor.Green
                    : Debug.DebugColor.Red);

            InformationManager.ShowInquiry(
                new InquiryData(
                    playerSurvived
                        ? "ТРЕНИРОВОЧНЫЙ БОЙ ПРОЙДЕН"
                        : "ТРЕНИРОВОЧНЫЙ БОЙ ЗАВЕРШЁН",
                    result,
                    true,
                    true,
                    "СРАЗИТЬСЯ ЕЩЁ РАЗ",
                    "ВЫЙТИ С АРЕНЫ",
                    StartPracticeFight,
                    ExitArena),
                true,
                true);
        }

        private void ExitArena()
        {
            DikozemieLobbyShellIntegration.PrepareForTrainingExit();
            if (!Mission.MissionEnded && !Mission.IsMissionEnding)
            {
                Mission.EndMission();
            }
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

        private static void ResetAgent(
            Agent agent,
            AgentControllerType controller,
            GameEntity spawnPoint)
        {
            agent.Controller = AgentControllerType.None;
            BasicCharacterObject character = agent.Character;
            agent.UpdateSpawnEquipmentAndRefreshVisuals(
                character.FirstBattleEquipment ?? character.Equipment);
            agent.Health = agent.HealthLimit;
            agent.TeleportToPosition(spawnPoint.GlobalPosition);
            agent.Controller = controller;
        }

        private GameEntity GetSpawnPoint(int index, bool useRespawnPoint)
        {
            List<GameEntity> spawnPoints = useRespawnPoint
                ? _respawnPoints
                : _initialSpawnPoints;
            return spawnPoints[index % spawnPoints.Count];
        }

        private List<GameEntity> FindSpawnPoints(string tag)
        {
            return Mission.Scene.FindEntitiesWithTag(tag).ToList();
        }
    }
}
