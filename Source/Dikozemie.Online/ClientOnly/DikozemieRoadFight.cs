using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.ObjectSystem;

namespace Dikozemie.Online
{
    // Customer, 2026-11-05: «видишь банда каких-то ребят нехороших, там их
    // два-три человека, уже очень опасно ... один отвлекает внимание, а другой
    // уже сложно». The arena server only pairs equal teams, so a road fight is
    // fought here, on the player's computer, on a real singleplayer field of
    // the region: the hero (and whoever walks with him) against two or three.
    internal sealed class DikozemieRoadFightSetup
    {
        internal string Title = "ЗАСАДА";
        internal string Scene = "battle_terrain_i";
        internal string Season = "summer";
        internal int Hour = 12;
        // The foes came out of the bushes around the hero: no time to form up.
        internal bool Ambush;
        // Wounds of an earlier defeat: the hero starts with less health.
        internal float HeroHealth = 1f;
        internal List<DikozemieRoadFighter> Foes = new List<DikozemieRoadFighter>();
        internal List<DikozemieRoadFighter> Allies = new List<DikozemieRoadFighter>();
        // What the map does with the outcome (DikozemieRoads).
        internal string Purpose = string.Empty;
    }

    internal sealed class DikozemieRoadFightResult
    {
        internal DikozemieRoadFightSetup Setup;
        internal bool Victory;
        // The hero ran from the field (Esc): a defeat without the last blow.
        internal bool Fled;
        internal int FoesDown;
        internal int AlliesDown;
        internal float HeroHealthLeft = 1f;
        internal string Progress = string.Empty;
    }

    internal static class DikozemieRoadFight
    {
        internal const string MissionName = "DikozemieRoadFight";
        private const string HeroFallbackId = "mp_heavy_infantry_empire_troop";

        private static readonly Dictionary<int, Dictionary<string, int>> AgentSkills =
            new Dictionary<int, Dictionary<string, int>>();
        private static DikozemieRoadFightResult _result;

        internal static DikozemieRoadFightSetup Active { get; private set; }

        internal static bool IsRunning => Active != null && Mission.Current != null &&
            Mission.Current.SceneName == Active.Scene;

        internal static bool TryStart(DikozemieRoadFightSetup setup, out string error)
        {
            error = string.Empty;
            if (setup == null || setup.Foes.Count == 0)
            {
                error = "Не с кем драться.";
                return false;
            }
            if (Mission.Current != null)
            {
                error = "Сначала завершите текущий бой.";
                return false;
            }
            if (DikozemiePvpSearchController.IsSearching)
            {
                DikozemiePvpSearchController.Cancel();
            }
            // The multiplayer stat model looks up the troop class of every
            // agent's character: the hero's own «mp_character» has none and
            // threw inside SpawnAgent, which then crashed the engine
            // (2026-11-07). The hero takes the body of a multiplayer soldier
            // of his people; face, equipment and skills stay his own.
            BasicCharacterObject hero =
                MBObjectManager.Instance.GetObject<BasicCharacterObject>(
                    DikozemieRoadFolk.FaceTroop(DikozemieHeroProfile.CultureId)) ??
                MBObjectManager.Instance.GetObject<BasicCharacterObject>(HeroFallbackId);
            if (hero == null)
            {
                error = "Нет данных героя для боя.";
                return false;
            }
            try
            {
                Active = setup;
                _result = null;
                AgentSkills.Clear();
                Game.Current.PlayerTroop = hero;
                // The map and the location line are a global layer: they would
                // stay drawn over the field.
                DikozemieArenaLobbyMenuController.SetMapVisible(false);
                var record = new MissionInitializerRecord(setup.Scene)
                {
                    DoNotUseLoadingScreen = false,
                    PlayingInCampaignMode = false,
                    SceneLevels = string.Empty,
                    DecalAtlasGroup = 2,
                    AtmosphereOnCampaign = BannerlordMissions.CreateAtmosphereInfoForMission(
                        setup.Season, setup.Hour)
                };
                DikozemieLobbyShellIntegration.TraceButtonAction(
                    "road fight start scene=" + setup.Scene + " foes=" + setup.Foes.Count +
                    " allies=" + setup.Allies.Count + " hour=" + setup.Hour +
                    " ambush=" + setup.Ambush + " purpose=" + setup.Purpose);
                MissionState.OpenNew(
                    MissionName,
                    record,
                    mission => Behaviors(hero, setup),
                    true,
                    true);
                return true;
            }
            catch (Exception exception)
            {
                Active = null;
                Console.WriteLine("DikozemieOnline: road fight failed to open: " + exception);
                error = "Не удалось открыть поле боя.";
                return false;
            }
        }

        private static IEnumerable<MissionBehavior> Behaviors(
            BasicCharacterObject hero, DikozemieRoadFightSetup setup)
        {
            return new MissionBehavior[]
            {
                new AgentHumanAILogic(),
                new MissionHardBorderPlacer(),
                new MissionBoundaryPlacer(),
                new MissionBoundaryCrossingHandler(),
                new DikozemieRoadFightController(hero, setup)
            };
        }

        internal static void RegisterSkills(Agent agent, Dictionary<string, int> skills)
        {
            if (agent != null && skills != null)
            {
                AgentSkills[agent.Index] = skills;
            }
        }

        // Bandits fight with their own singleplayer skills, not the skills of
        // the multiplayer troop whose body they borrow.
        internal static bool TryGetSkill(Agent agent, string skillId, out int value)
        {
            value = 0;
            if (agent == null || Active == null || AgentSkills.Count == 0 ||
                !AgentSkills.TryGetValue(agent.Index, out Dictionary<string, int> skills) ||
                Mission.Current == null || Mission.Current.MainAgent == agent)
            {
                return false;
            }
            return skills.TryGetValue(skillId, out value);
        }

        internal static void Finish(DikozemieRoadFightResult result)
        {
            _result = result;
            AgentSkills.Clear();
            DikozemieLobbyShellIntegration.PrepareForRoadFightExit();
        }

        internal static bool TryConsumeResult(out DikozemieRoadFightResult result)
        {
            result = _result;
            if (result == null || Mission.Current != null)
            {
                return false;
            }
            _result = null;
            Active = null;
            return true;
        }
    }

    public sealed class DikozemieRoadFightController : MissionLogic
    {
        private const float ResultDelay = 3f;
        private const float RetargetEvery = 1f;
        // Tests on a machine without a player at the keyboard: the hero
        // fights by himself.
        private static readonly bool Autoplay =
            Environment.GetEnvironmentVariable("DIKOZEMIE_TEST_ROAD_AUTOPLAY") == "1";

        private readonly BasicCharacterObject _hero;
        private readonly DikozemieRoadFightSetup _setup;
        private readonly List<Agent> _foes = new List<Agent>();
        private readonly List<Agent> _allies = new List<Agent>();
        private Team _heroTeam;
        private Team _foeTeam;
        private Agent _heroAgent;
        // The first orders wait until every fighter stands in the world.
        private float _retarget = 1.5f;
        private bool _firstTick = true;
        private float _resultAt = -1f;
        private bool _victory;
        private bool _fled;
        private bool _finished;
        private bool _resultShown;
        private bool _targetsLogged;

        internal DikozemieRoadFightController(BasicCharacterObject hero, DikozemieRoadFightSetup setup)
        {
            _hero = hero;
            _setup = setup;
        }

        internal bool IsOver => _resultAt >= 0f || _finished;

        internal int FoesStanding => _foes.Count(IsStanding);

        internal int FoeCount => _foes.Count;

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            _heroTeam = Mission.Teams.Add(BattleSideEnum.Defender, 0xff2d5a8c, 0xffd8c9a0, null, true, false, true);
            _foeTeam = Mission.Teams.Add(BattleSideEnum.Attacker, 0xff6d1717, 0xff2b2015, null, false, false, true);
            _foeTeam.SetIsEnemyOf(_heroTeam, true);
            Mission.PlayerTeam = _heroTeam;
        }

        public override void AfterStart()
        {
            base.AfterStart();
            FindField(out Vec3 center, out Vec2 forward);
            Vec2 side = forward.LeftVec();
            float apart = _setup.Ambush ? 9f : 22f;

            Vec3 heroPosition = OnGround(center - (forward * (apart * 0.5f)).ToVec3());
            _heroAgent = Spawn(_hero, null, _heroTeam, AgentControllerType.Player, heroPosition, forward);
            Mission.MainAgent = _heroAgent;
            DikozemieHeroProfile.ApplyHealthBonus(_heroAgent);
            if (_heroAgent != null && _setup.HeroHealth < 0.999f)
            {
                _heroAgent.Health = Math.Max(1f, _heroAgent.HealthLimit * _setup.HeroHealth);
            }

            for (int index = 0; index < _setup.Allies.Count; index++)
            {
                float offset = (index % 2 == 0 ? 1f : -1f) * (2.5f + index);
                Vec3 position = OnGround(heroPosition + (side * offset - forward * 1.5f).ToVec3());
                Agent ally = Spawn(null, _setup.Allies[index], _heroTeam, AgentControllerType.AI, position, forward);
                if (ally != null)
                {
                    _allies.Add(ally);
                }
            }

            int count = _setup.Foes.Count;
            for (int index = 0; index < count; index++)
            {
                Vec3 position;
                Vec2 facing;
                if (_setup.Ambush)
                {
                    // Out of the bushes on every side: one in front, the others
                    // at the flanks and behind.
                    float angle = (float)(Math.PI * 2.0 * index / Math.Max(1, count)) + 0.35f;
                    Vec2 around = forward;
                    around.RotateCCW(angle);
                    position = OnGround(heroPosition + (around * 8.5f).ToVec3());
                    facing = -around;
                }
                else
                {
                    float offset = (index - (count - 1) * 0.5f) * 3.2f;
                    position = OnGround(center + (forward * (apart * 0.5f) + side * offset).ToVec3());
                    facing = -forward;
                }
                Agent foe = Spawn(null, _setup.Foes[index], _foeTeam, AgentControllerType.AI, position, facing);
                if (foe != null)
                {
                    _foes.Add(foe);
                }
            }
            InformationManager.DisplayMessage(new InformationMessage(
                _setup.Title + ": против вас " + _foes.Count + ". Esc — бежать с поля боя.",
                Colors.Yellow));
            Debug.Print("DikozemieOnline: road fight spawned foes=" + _foes.Count +
                " allies=" + _allies.Count + " scene=" + _setup.Scene, 0, Debug.DebugColor.Green);
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (_firstTick)
            {
                _firstTick = false;
                Debug.Print("DikozemieOnline: road fight first tick", 0, Debug.DebugColor.Green);
            }
            if (_finished)
            {
                return;
            }
            if (_resultAt >= 0f)
            {
                if (!_resultShown && Mission.CurrentTime >= _resultAt)
                {
                    _resultShown = true;
                    ShowResult();
                }
                return;
            }
            _retarget -= dt;
            if (_retarget <= 0f)
            {
                _retarget = RetargetEvery;
                AssignTargets(_foes, Defenders());
                AssignTargets(_allies, _foes);
                if (!_targetsLogged)
                {
                    _targetsLogged = true;
                    Debug.Print("DikozemieOnline: road fight targets assigned", 0, Debug.DebugColor.Green);
                }
                if (Autoplay && _heroAgent != null && IsStanding(_heroAgent))
                {
                    if (_heroAgent.Controller != AgentControllerType.AI)
                    {
                        _heroAgent.Controller = AgentControllerType.AI;
                    }
                    AssignTargets(new[] { _heroAgent }, _foes);
                }
            }
            if (_foes.Count > 0 && FoesStanding == 0)
            {
                End(true, false);
            }
            else if (_heroAgent == null || !IsStanding(_heroAgent))
            {
                End(false, false);
            }
        }

        // Esc on the field: run away. It counts as a defeat.
        internal void Flee()
        {
            if (IsOver)
            {
                return;
            }
            End(false, true);
            _resultAt = Mission.CurrentTime;
        }

        private void End(bool victory, bool fled)
        {
            _victory = victory;
            _fled = fled;
            _resultAt = Mission.CurrentTime + ResultDelay;
            foreach (Agent agent in _foes.Concat(_allies))
            {
                if (IsStanding(agent))
                {
                    agent.SetAutomaticTargetSelection(false);
                    agent.SetTargetAgent(null);
                }
            }
            Debug.Print("DikozemieOnline: road fight over victory=" + victory + " fled=" + fled +
                " foesDown=" + (_foes.Count - FoesStanding), 0, Debug.DebugColor.Green);
        }

        private void ShowResult()
        {
            int down = _foes.Count - FoesStanding;
            string text;
            if (_victory)
            {
                text = down == 1
                    ? "Противник лежит в дорожной пыли."
                    : "Все " + down + " лежат в дорожной пыли.";
            }
            else if (_fled)
            {
                text = "Вы бросились прочь, пока вас не добили.";
            }
            else
            {
                text = "Удар сбил вас с ног. Очнулись вы уже " +
                    DikozemieRoads.G("связанным или обобранным.", "связанной или обобранной.");
            }
            InformationManager.ShowInquiry(
                new InquiryData(
                    _victory ? "ПОБЕДА" : _fled ? "БЕГСТВО" : "ПОРАЖЕНИЕ",
                    text,
                    true,
                    false,
                    "НА КАРТУ",
                    string.Empty,
                    Leave,
                    null),
                true,
                true);
        }

        private void Leave()
        {
            if (_finished)
            {
                return;
            }
            _finished = true;
            int down = _foes.Count - FoesStanding;
            var result = new DikozemieRoadFightResult
            {
                Setup = _setup,
                Victory = _victory,
                Fled = _fled,
                FoesDown = down,
                AlliesDown = _allies.Count(agent => !IsStanding(agent)),
                HeroHealthLeft = _heroAgent != null && IsStanding(_heroAgent)
                    ? _heroAgent.Health / Math.Max(1f, _heroAgent.HealthLimit)
                    : 0f
            };
            result.Progress = DikozemieHeroProfile.RecordBattleResult(_victory, Math.Max(1, down));
            DikozemieRoadFight.Finish(result);
            if (!Mission.MissionEnded && !Mission.IsMissionEnding)
            {
                Mission.EndMission();
            }
        }

        private IEnumerable<Agent> Defenders()
        {
            if (_heroAgent != null)
            {
                yield return _heroAgent;
            }
            foreach (Agent ally in _allies)
            {
                yield return ally;
            }
        }

        private static void AssignTargets(IEnumerable<Agent> hunters, IEnumerable<Agent> prey)
        {
            List<Agent> targets = prey.Where(IsStanding).ToList();
            foreach (Agent hunter in hunters)
            {
                if (!IsStanding(hunter))
                {
                    continue;
                }
                Agent current = hunter.GetTargetAgent();
                if (current != null && IsStanding(current))
                {
                    continue;
                }
                Agent nearest = null;
                float best = float.MaxValue;
                foreach (Agent target in targets)
                {
                    float distance = target.Position.DistanceSquared(hunter.Position);
                    if (distance < best)
                    {
                        best = distance;
                        nearest = target;
                    }
                }
                hunter.SetWatchState(Agent.WatchState.Alarmed);
                hunter.SetAutomaticTargetSelection(false);
                hunter.SetTargetAgent(nearest);
            }
        }

        private static bool IsStanding(Agent agent) =>
            agent != null && agent.IsActive() && agent.State == AgentState.Active;

        private Agent Spawn(
            BasicCharacterObject heroCharacter,
            DikozemieRoadFighter fighter,
            Team team,
            AgentControllerType controller,
            Vec3 position,
            Vec2 facing)
        {
            try
            {
                BasicCharacterObject character = heroCharacter ?? fighter.FaceCharacter();
                Vec2 direction = facing.Normalized();
                var data = new AgentBuildData(new BasicBattleAgentOrigin(character))
                    .Team(team)
                    .Controller(controller)
                    .InitialPosition(in position)
                    .InitialDirection(in direction)
                    .FixedEquipment(true)
                    .CanSpawnOutsideOfMissionBoundary(true);
                if (heroCharacter != null)
                {
                    data = DikozemieHeroProfile.ApplyToPlayerBuild(data, heroCharacter);
                }
                else
                {
                    data = data.Equipment(fighter.Equipment)
                        .BodyProperties(character.GetBodyProperties(fighter.Equipment, fighter.Seed))
                        .IsFemale(false)
                        .EquipmentSeed(fighter.Seed);
                }
                Agent agent = Mission.SpawnAgent(data, false);
                if (fighter != null)
                {
                    DikozemieRoadFight.RegisterSkills(agent, fighter.Skills);
                    if (fighter.Health < 0.999f)
                    {
                        agent.Health = Math.Max(1f, agent.HealthLimit * fighter.Health);
                    }
                }
                Debug.Print("DikozemieOnline: road fighter spawned " +
                    (fighter == null ? "hero" : fighter.Role + " seed=" + fighter.Seed) +
                    " at " + position.x.ToString("0.0") + "," + position.y.ToString("0.0") + "," +
                    position.z.ToString("0.0") + " items=" + DescribeItems(fighter?.Equipment),
                    0, Debug.DebugColor.Green);
                return agent;
            }
            catch (Exception exception)
            {
                Debug.Print("DikozemieOnline: road fighter not spawned " +
                    (fighter == null ? "hero" : fighter.Role) + ": " + exception, 0, Debug.DebugColor.Red);
                return null;
            }
        }

        // The middle of the scene's first spawn path: the open ground where the
        // singleplayer game lines up its armies.
        private void FindField(out Vec3 center, out Vec2 forward)
        {
            Scene scene = Mission.Scene;
            Path path = null;
            foreach (string name in new[] { "spawn_path_01", "spawn_path_02", "spawn_path_03" })
            {
                path = scene.GetPathWithName(name);
                if (path != null && path.NumberOfPoints > 1)
                {
                    break;
                }
                path = null;
            }
            if (path != null)
            {
                float length = path.GetTotalLength();
                float at = length * 0.5f;
                Vec3 a = path.GetFrameForDistance(Math.Max(0f, at - 6f)).origin;
                Vec3 b = path.GetFrameForDistance(Math.Min(length, at + 6f)).origin;
                Vec2 along = (b - a).AsVec2;
                forward = along.LengthSquared > 0.01f ? along.Normalized() : new Vec2(0f, 1f);
                center = path.GetFrameForDistance(at).origin;
                return;
            }
            scene.GetSceneLimits(out Vec3 min, out Vec3 max);
            center = (min + max) * 0.5f;
            forward = new Vec2(0f, 1f);
        }

        // A point of the navigation mesh near the wanted one: no fighter may
        // stand in a tree, in water or in the air.
        private Vec3 OnGround(Vec3 wanted)
        {
            Scene scene = Mission.Scene;
            for (int ring = 0; ring <= 6; ring++)
            {
                int steps = ring == 0 ? 1 : 8;
                for (int step = 0; step < steps; step++)
                {
                    float angle = (float)(Math.PI * 2.0 * step / steps);
                    float radius = ring * 1.5f;
                    var probe = new Vec3(
                        wanted.x + (float)Math.Cos(angle) * radius,
                        wanted.y + (float)Math.Sin(angle) * radius,
                        0f);
                    probe.z = scene.GetTerrainHeight(probe.AsVec2);
                    var world = new WorldPosition(scene, UIntPtr.Zero, probe, false);
                    if (world.GetNavMesh() == UIntPtr.Zero)
                    {
                        continue;
                    }
                    Vec3 ground = world.GetGroundVec3();
                    if (!float.IsNaN(ground.z) && Math.Abs(ground.z - probe.z) < 8f)
                    {
                        return ground;
                    }
                }
            }
            return new Vec3(wanted.x, wanted.y, scene.GetTerrainHeight(wanted.AsVec2));
        }

        private static string DescribeItems(Equipment equipment)
        {
            if (equipment == null)
            {
                return "hero";
            }
            var ids = new List<string>();
            for (int slot = 0; slot < 12; slot++)
            {
                if (equipment[slot].Item != null)
                {
                    ids.Add(equipment[slot].Item.StringId);
                }
            }
            return string.Join("+", ids);
        }
    }

    // The player's side of a road fight: health and weapons as in the
    // singleplayer game, Esc to run from the field.
    public sealed class DikozemieRoadFightView : MissionView
    {
        private DikozemieRoadFightController _controller;
        private bool _asking;

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            _controller = Mission.GetMissionBehavior<DikozemieRoadFightController>();
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            if (_controller == null || _controller.IsOver || _asking)
            {
                return;
            }
            if (Input.IsKeyReleased(InputKey.Escape))
            {
                _asking = true;
                InformationManager.ShowInquiry(
                    new InquiryData(
                        "БЕЖАТЬ С ПОЛЯ БОЯ?",
                        "Это поражение: враги заберут своё. Зато вы уйдёте " + DikozemieRoads.G("живым.", "живой."),
                        true,
                        true,
                        "БЕЖАТЬ",
                        "ДРАТЬСЯ",
                        () =>
                        {
                            _asking = false;
                            _controller.Flee();
                        },
                        () => _asking = false),
                    true,
                    true);
            }
        }
    }

    [ViewCreatorModule]
    public static class DikozemieRoadFightViews
    {
        [ViewMethod(DikozemieRoadFight.MissionName)]
        public static MissionView[] OpenRoadFight(Mission mission)
        {
            // No ViewCreator.CreateOptionsUIHandler: in the multiplayer client
            // it brought the engine down right after the field loaded
            // (2026-11-07, found by switching the views off one by one).
            return new MissionView[]
            {
                new MissionMainAgentController(),
                ViewCreator.CreateMissionAgentStatusUIHandler(mission),
                ViewCreator.CreateMissionMainAgentEquipmentController(mission),
                ViewCreator.CreateMissionMainAgentEquipDropView(mission),
                ViewCreator.CreateMissionAgentLabelUIHandler(mission),
                ViewCreator.CreateMissionBoundaryCrossingView(),
                new MissionItemContourControllerView(),
                new MissionAgentContourControllerView(),
                new DikozemieRoadFightView()
            };
        }
    }
}
