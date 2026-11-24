using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SandBox.Objects;
using SandBox.Objects.AnimationPoints;
using SandBox.Objects.Usables;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    // What a person of the walk does.
    internal enum DikozemieWalkerKind
    {
        // Stands at his own place all the time: a trader at the stall, the
        // tavern keeper behind the bar, a guard at the gate.
        Fixed,
        // Walks from one gathering place to another and lingers there.
        Wanderer,
        // Walks with the hero: his escort and fellow travellers.
        Follower,
        // The gang's men of a street fight.
        Thug
    }

    internal sealed class DikozemieWalker
    {
        internal Agent Agent;
        internal DikozemieTownPerson Person;
        internal DikozemieWalkerKind Kind;
        // The place he stands at (a market stall, a bench, a spot by a wall).
        internal UsableMachine Machine;
        internal StandingPoint Point;
        internal bool AtPoint;
        internal float WaitUntil = -1f;
        internal float NextThink;
        internal float LeaveAt = -1f;
        internal bool Talkable = true;
        internal bool IsGuard;
        internal bool IsBrawler;
        // A line the person says when greeted, picked once.
        internal int Mood;
    }

    internal sealed class DikozemieWalkDoor
    {
        internal string To = string.Empty;
        internal bool Exit;
        internal Vec3 Position;
        internal Vec2 Facing;
        internal string Label = string.Empty;
    }

    // The walk itself: the scene, its people and its doors.
    public sealed partial class DikozemieTownWalkController : MissionLogic
    {
        private const float ThinkEvery = 0.5f;
        private const float DoorReach = 2.4f;
        private static readonly FieldInfo AgentNameField =
            typeof(Agent).GetField("_name", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly BasicCharacterObject _hero;
        private readonly DikozemieTownWalkSetup _setup;
        private readonly Dictionary<string, List<UsableMachine>> _points =
            new Dictionary<string, List<UsableMachine>>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<StandingPoint> _taken = new HashSet<StandingPoint>();
        private readonly List<DikozemieWalkDoor> _doors = new List<DikozemieWalkDoor>();
        private readonly Dictionary<Agent, DikozemieWalker> _walkers = new Dictionary<Agent, DikozemieWalker>();
        private readonly Random _dice;
        private Team _heroTeam;
        private Team _rivalTeam;
        private Agent _heroAgent;
        private float _think;
        private bool _started;
        private bool _leaving;
        private int _seedBase;

        internal DikozemieTownWalkController(BasicCharacterObject hero, DikozemieTownWalkSetup setup)
        {
            _hero = hero;
            _setup = setup;
            _seedBase = DikozemieTown.StableHash(setup.Node?.Id ?? "walk") ^ (DikozemieRoads.Day * 7919);
            _dice = new Random(Environment.TickCount);
        }

        internal DikozemieTownWalkSetup Setup => _setup;

        internal Agent Hero => _heroAgent;

        // The door the hero stands at, for the prompt and the key.
        internal DikozemieWalkDoor NearDoor { get; private set; }

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            _heroTeam = Mission.Teams.Add(BattleSideEnum.Defender, 0xff2d5a8c, 0xffd8c9a0, null, true, false, true);
            _rivalTeam = Mission.Teams.Add(BattleSideEnum.Attacker, 0xff6d1717, 0xff2b2015, null, false, false, true);
            _rivalTeam.SetIsEnemyOf(_heroTeam, true);
            Mission.PlayerTeam = _heroTeam;
        }

        public override void AfterStart()
        {
            base.AfterStart();
            try
            {
                // People one can talk to; weapons stay in the sheath.
                Mission.SetMissionMode(MissionMode.StartUp, true);
                OpenGates();
                CollectPoints();
                CollectDoors();
                SpawnHero();
                SpawnPeople();
                SpawnCompany();
                SpawnAnimals();
                _started = true;
                LogCensus();
            }
            catch (Exception error)
            {
                Debug.Print("DikozemieOnline: town walk start failed: " + error, 0, Debug.DebugColor.Red);
                InformationManager.DisplayMessage(new InformationMessage(
                    "Город не удалось заселить: " + error.Message, Colors.Red));
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (!_started || _leaving)
            {
                return;
            }
            _think -= dt;
            if (_think <= 0f)
            {
                _think = ThinkEvery;
                Think();
            }
            TickBrawl(dt);
            TickRobbers(dt);
            TickPeers(dt);
            NearDoor = FindNearDoor();
            TickTest();
            TickTestTalks();
        }

        // Tests: DIKOZEMIE_TEST_TOWN_DOOR=<tavern|arena|lordshall|exit|...>
        // puts the hero in front of that door; DIKOZEMIE_TEST_TOWN_TALK=<role>
        // puts him before the first person of the role and starts talking.
        private bool _testDone;

        private void TickTest()
        {
            if (_testDone || Mission.CurrentTime < 4f || _heroAgent == null)
            {
                return;
            }
            _testDone = true;
            string door = Environment.GetEnvironmentVariable("DIKOZEMIE_TEST_TOWN_DOOR");
            if (!string.IsNullOrEmpty(door) && _setup.From == null)
            {
                DikozemieWalkDoor target = _doors.FirstOrDefault(d => d.To == door);
                if (target != null)
                {
                    Vec3 spot = OnGround(target.Position - (target.Facing * 1.0f).ToVec3());
                    _heroAgent.TeleportToPosition(spot);
                    _heroAgent.LookDirection = target.Facing.ToVec3();
                    _heroAgent.SetMovementDirection(target.Facing);
                    Debug.Print("DikozemieOnline: town walk test hero at door " + door, 0, Debug.DebugColor.Green);
                }
            }
            // DIKOZEMIE_TEST_TOWN_CRIME=1: the watch catches the hero as if he
            // had struck the nearest townsman (the automation cannot aim a blow).
            if (Environment.GetEnvironmentVariable("DIKOZEMIE_TEST_TOWN_CRIME") == "1" &&
                _setup.Place == DikozemieWalkPlace.Streets)
            {
                DikozemieWalker victim = _walkers.Values
                    .Where(w => w.Kind == DikozemieWalkerKind.Wanderer && w.Agent.IsActive())
                    .OrderBy(w => w.Agent.Position.DistanceSquared(_heroAgent.Position))
                    .FirstOrDefault();
                if (victim != null)
                {
                    Crime(victim);
                }
            }
            string roles = Environment.GetEnvironmentVariable("DIKOZEMIE_TEST_TOWN_TALK");
            if (!string.IsNullOrEmpty(roles))
            {
                _testTalks = new Queue<string>(roles.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            }
        }

        private Queue<string> _testTalks;
        private float _testNextTalk;

        // The next person of the test list, once the last talk is closed.
        private void TickTestTalks()
        {
            if (_testTalks == null || _testTalks.Count == 0 || _talk != null || InBrawl || _heroAgent == null)
            {
                if (_talk != null)
                {
                    _testNextTalk = Mission.CurrentTime + 2.5f;
                }
                return;
            }
            if (Mission.CurrentTime < _testNextTalk)
            {
                return;
            }
            string role = _testTalks.Dequeue();
            {
                DikozemieWalker walker = _walkers.Values.FirstOrDefault(w =>
                    (w.Person.Role == role || w.Person.Title == role) && w.Agent.IsActive());
                Debug.Print("DikozemieOnline: town walk test next talk " + role + " found=" + (walker != null),
                    0, Debug.DebugColor.Green);
                if (walker != null)
                {
                    Vec2 facing = walker.Agent.LookDirection.AsVec2.Normalized();
                    Vec3 spot = OnGround(walker.Agent.Position + (facing * 1.6f).ToVec3());
                    _heroAgent.TeleportToPosition(spot);
                    _heroAgent.LookDirection = (-facing).ToVec3();
                    _heroAgent.SetMovementDirection(-facing);
                    if (walker.Talkable)
                    {
                        OpenTalk(walker);
                    }
                    Debug.Print("DikozemieOnline: town walk test talk " + role, 0, Debug.DebugColor.Green);
                }
            }
        }

        public override void OnRemoveBehavior()
        {
            base.OnRemoveBehavior();
            DikozemieTownWalk.OnMissionGone();
        }

        // ---- The scene ----------------------------------------------------

        private void OpenGates()
        {
            foreach (CastleGate gate in Mission.ActiveMissionObjects.FindAllWithType<CastleGate>().ToList())
            {
                try
                {
                    gate.OpenDoorAndDisableGateForCivilianMission();
                }
                catch (Exception error)
                {
                    Debug.Print("DikozemieOnline: town gate not opened: " + error.Message, 0, Debug.DebugColor.Yellow);
                }
            }
        }

        // The places people stand at, by their tag, as the singleplayer game
        // gathers them. A singleplayer person «uses» such a place; here the
        // walk puts him there and plays its animation itself: the place's own
        // logic asks the campaign about conversations, and there is no
        // campaign in the multiplayer game.
        private void CollectPoints()
        {
            bool night = _setup.Night;
            foreach (UsableMachine machine in Mission.MissionObjects.FindAllWithType<UsableMachine>())
            {
                if (machine == null || machine.IsDeactivated || machine is Passage || machine is CastleGate)
                {
                    continue;
                }
                WeakGameEntity entity = machine.GameEntity;
                if (!entity.IsValid)
                {
                    continue;
                }
                if (night && entity.HasTag("disable_at_night") || !night && entity.HasTag("enable_at_night"))
                {
                    continue;
                }
                foreach (string tag in entity.Tags)
                {
                    if (!_points.TryGetValue(tag, out List<UsableMachine> list))
                    {
                        list = new List<UsableMachine>();
                        _points[tag] = list;
                    }
                    list.Add(machine);
                }
            }
            // The places keep their own logic away from our people.
            foreach (AnimationPoint point in Mission.MissionObjects.FindAllWithType<AnimationPoint>())
            {
                point.PairEntity = null;
            }
            foreach (TeleportUsePoint point in Mission.MissionObjects.FindAllWithType<TeleportUsePoint>())
            {
                point.IsDeactivated = true;
            }
        }

        private void CollectDoors()
        {
            foreach (PassageUsePoint passage in Mission.MissionObjects.FindAllWithType<PassageUsePoint>())
            {
                if (passage == null)
                {
                    continue;
                }
                // The singleplayer door asks the campaign where it leads: the
                // walk opens it itself.
                passage.IsDeactivated = true;
                string to = (passage.ToLocationId ?? string.Empty).Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(to) && !passage.IsMissionExit)
                {
                    continue;
                }
                MatrixFrame frame = passage.GameEntity.GetGlobalFrame();
                frame.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
                var door = new DikozemieWalkDoor
                {
                    To = passage.IsMissionExit ? "exit" : to,
                    Exit = passage.IsMissionExit,
                    Position = frame.origin,
                    Facing = frame.rotation.f.AsVec2.Normalized()
                };
                door.Label = DoorLabel(door);
                if (!string.IsNullOrEmpty(door.Label))
                {
                    _doors.Add(door);
                }
            }
        }

        private string DoorLabel(DikozemieWalkDoor door)
        {
            switch (door.To)
            {
                case "exit":
                    return _setup.Place == DikozemieWalkPlace.Streets
                        ? (_setup.IsVillage ? "УЙТИ ИЗ ДЕРЕВНИ" : _setup.IsCastle ? "ВЫЙТИ ЗА ВОРОТА" : "ВЫЙТИ ИЗ ГОРОДА")
                        : "ВЫЙТИ НА УЛИЦУ";
                case "center":
                case "village_center":
                    return _setup.IsCastle ? "ВЫЙТИ ВО ДВОР" : "ВЫЙТИ НА УЛИЦУ";
                case "tavern":
                    return string.IsNullOrEmpty(DikozemieTownWalk.SceneFor(_setup.Node, DikozemieWalkPlace.Tavern))
                        ? string.Empty
                        : "ВОЙТИ В ТАВЕРНУ";
                case "arena":
                    return string.IsNullOrEmpty(DikozemieTownWalk.SceneFor(_setup.Node, DikozemieWalkPlace.Arena))
                        ? string.Empty
                        : "ВОЙТИ НА АРЕНУ";
                case "lordshall":
                    return string.IsNullOrEmpty(DikozemieTownWalk.SceneFor(_setup.Node, DikozemieWalkPlace.Hall))
                        ? string.Empty
                        : "ВОЙТИ В ЗАЛ ЛОРДА";
                case "port":
                    return _setup.Node?.HasPort == true ? "ПРОЙТИ В ПОРТ" : string.Empty;
                case "prison":
                    return "ТЕМНИЦА";
                default:
                    return door.To.StartsWith("house", StringComparison.Ordinal) ? "ДВЕРЬ ДОМА" : string.Empty;
            }
        }

        private DikozemieWalkDoor FindNearDoor()
        {
            if (_heroAgent == null || !_heroAgent.IsActive() || IsTalking || InBrawl)
            {
                return null;
            }
            Vec3 at = _heroAgent.Position;
            DikozemieWalkDoor best = null;
            float bestDistance = DoorReach * DoorReach;
            foreach (DikozemieWalkDoor door in _doors)
            {
                float distance = door.Position.AsVec2.DistanceSquared(at.AsVec2);
                if (distance < bestDistance && Math.Abs(door.Position.z - at.z) < 2.5f)
                {
                    bestDistance = distance;
                    best = door;
                }
            }
            return best;
        }

        // F at a door.
        internal void UseDoor()
        {
            DikozemieWalkDoor door = NearDoor;
            if (door == null || _leaving)
            {
                return;
            }
            switch (door.To)
            {
                case "exit":
                    if (_setup.Place == DikozemieWalkPlace.Streets)
                    {
                        LeaveWalk(DikozemieWalkExit.Town, string.Empty);
                    }
                    else
                    {
                        GoTo(DikozemieWalkPlace.Streets);
                    }
                    return;
                case "center":
                case "village_center":
                    GoTo(DikozemieWalkPlace.Streets);
                    return;
                case "tavern":
                    GoTo(DikozemieWalkPlace.Tavern);
                    return;
                case "arena":
                    GoTo(DikozemieWalkPlace.Arena);
                    return;
                case "lordshall":
                    if (!DikozemieTown.HasCastlePass(_setup.Node))
                    {
                        OpenHallGuardTalk();
                        return;
                    }
                    GoTo(DikozemieWalkPlace.Hall);
                    return;
                case "port":
                    LeaveWalk(DikozemieWalkExit.Port, string.Empty);
                    return;
                case "prison":
                    Say("Стражник у решётки: «Туда вам рано. Или поздно — как посмотреть.»");
                    return;
                default:
                    Say("Заперто. Из-за двери тянет дымом и щами.");
                    return;
            }
        }

        private void GoTo(DikozemieWalkPlace place)
        {
            if (_leaving)
            {
                return;
            }
            _leaving = true;
            DikozemieRoads.Advance(0.1f);
            DikozemieTravelState.Save();
            DikozemieTownWalk.GoThrough(place);
        }

        internal void LeaveWalk(DikozemieWalkExit exit, string note)
        {
            if (_leaving)
            {
                return;
            }
            _leaving = true;
            DikozemieRoads.Advance(0.25f);
            DikozemieTravelState.Save();
            DikozemieTownWalk.Leave(exit, note);
        }

        internal bool IsLeaving => _leaving;

        // ---- The hero -----------------------------------------------------

        private void SpawnHero()
        {
            MatrixFrame frame = HeroStart();
            Vec2 direction = frame.rotation.f.AsVec2.Normalized();
            Vec3 position = frame.origin;
            var data = new AgentBuildData(new BasicBattleAgentOrigin(_hero))
                .Team(_heroTeam)
                .Controller(AgentControllerType.Player)
                .InitialPosition(in position)
                .InitialDirection(in direction)
                .FixedEquipment(true)
                .NoHorses(true)
                .CanSpawnOutsideOfMissionBoundary(true);
            data = DikozemieHeroProfile.ApplyToPlayerBuild(data, _hero);
            _heroAgent = Mission.SpawnAgent(data, false);
            Mission.MainAgent = _heroAgent;
            DikozemieHeroProfile.ApplyHealthBonus(_heroAgent);
            float health = DikozemieRoads.HealthAtStart;
            if (health < 0.999f)
            {
                _heroAgent.Health = Math.Max(1f, _heroAgent.HealthLimit * health);
            }
            // A street fight knocks the hero down; it does not kill him.
            _heroAgent.SetMortalityState(Agent.MortalityState.Immortal);
            Debug.Print("DikozemieOnline: town walk hero at " + Describe(position) + " place=" + _setup.Place +
                " from=" + (_setup.From?.ToString() ?? "menu"), 0, Debug.DebugColor.Green);
        }

        private MatrixFrame HeroStart()
        {
            // Out of a door: in front of the door that leads back there.
            if (_setup.From != null)
            {
                string back = DoorIdOf(_setup.From.Value);
                DikozemieWalkDoor door = _doors.FirstOrDefault(d => d.To == back) ??
                    (_setup.From == DikozemieWalkPlace.Streets ? _doors.FirstOrDefault(d => d.Exit) : null);
                if (door != null)
                {
                    var frame = new MatrixFrame(Mat3.Identity, door.Position);
                    Vec2 away = -door.Facing;
                    frame.rotation.f = away.ToVec3();
                    frame.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
                    frame.origin = OnGround(door.Position + (away * 0.8f).ToVec3());
                    return frame;
                }
            }
            var tags = new List<string>();
            if (_setup.Place == DikozemieWalkPlace.Arena)
            {
                tags.Add("sp_player_near_arena_master");
            }
            // From the town menu the hero is already inside the walls, as in
            // the singleplayer game after the first visit.
            tags.Add("spawnpoint_player");
            tags.Add("sp_player");
            tags.Add("spawnpoint_player_outside");
            foreach (string tag in tags)
            {
                GameEntity entity = Mission.Scene.FindEntityWithTag(tag);
                if (entity != null)
                {
                    MatrixFrame frame = entity.GetGlobalFrame();
                    frame.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
                    frame.origin = OnGround(frame.origin);
                    Debug.Print("DikozemieOnline: town walk hero start tag=" + tag, 0, Debug.DebugColor.Green);
                    return frame;
                }
            }
            UsableMachine any = Points("npc_common").FirstOrDefault();
            if (any != null)
            {
                MatrixFrame frame = any.GameEntity.GetGlobalFrame();
                frame.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
                frame.origin = OnGround(frame.origin);
                return frame;
            }
            Mission.Scene.GetSceneLimits(out Vec3 min, out Vec3 max);
            var middle = new MatrixFrame(Mat3.Identity, OnGround((min + max) * 0.5f));
            return middle;
        }

        private static string DoorIdOf(DikozemieWalkPlace place)
        {
            switch (place)
            {
                case DikozemieWalkPlace.Tavern: return "tavern";
                case DikozemieWalkPlace.Arena: return "arena";
                case DikozemieWalkPlace.Hall: return "lordshall";
                default: return "center";
            }
        }

        // ---- The people ---------------------------------------------------

        private IEnumerable<UsableMachine> Points(string tag) =>
            _points.TryGetValue(tag, out List<UsableMachine> list) ? list : Enumerable.Empty<UsableMachine>();

        private int PointCount(string tag) => _points.TryGetValue(tag, out List<UsableMachine> list) ? list.Count : 0;

        private void SpawnPeople()
        {
            switch (_setup.Place)
            {
                case DikozemieWalkPlace.Tavern:
                    SpawnTavern();
                    break;
                case DikozemieWalkPlace.Arena:
                    SpawnArena();
                    break;
                case DikozemieWalkPlace.Hall:
                    SpawnHall();
                    break;
                default:
                    if (_setup.IsVillage)
                    {
                        SpawnVillage();
                    }
                    else
                    {
                        SpawnStreets();
                    }
                    break;
            }
        }

        private void SpawnStreets()
        {
            string culture = _setup.Culture;
            bool castle = _setup.IsCastle;
            if (!castle)
            {
                SpawnFixed("sp_merchant", "merchant", "_seller", 1);
                SpawnFixed("sp_weaponsmith", "weaponsmith", "_weaponsmith", 1);
                SpawnFixed("sp_armorer", "armorer", "_seller", 1);
                SpawnFixed("sp_horse_merchant", "horse_merchant", "_seller", 1);
                SpawnFixed("sp_blacksmith", "blacksmith", "_villager", 1);
                SpawnFixed("sp_barber", "barber", "_villager", 1);
                SpawnNotables();
                int beggars = PointCount("npc_beggar");
                SpawnFixed("npc_beggar", "beggar", "_beggar", Math.Max(0, beggars / 2 + beggars % 2));
                SpawnFixed("npc_beggar", "female_beggar", "_beggar", beggars / 2);
                if (!_setup.Night)
                {
                    SpawnFixed("npc_dancer", "dancer", "_dancer", 2);
                    SpawnFixed("spawnpoint_cleaner", "townswoman", "_villager", 2);
                }
            }
            SpawnGuards(castle);
            if (castle)
            {
                SpawnFixed("sp_notable", "notable", "_villager", 1, "Управляющий");
            }
            int places = PointCount("npc_common") + PointCount("npc_common_limited");
            float share = _setup.Night ? 0.18f : castle ? 0.3f : 0.55f;
            int crowd = Math.Max(_setup.Night ? 3 : 6, Math.Min(castle ? 10 : 30, (int)(places * share)));
            SpawnCrowd(crowd, "townsman", "townswoman", outdoor: true);
            Debug.Print("DikozemieOnline: town walk streets people=" + _walkers.Count + " culture=" + culture +
                " places=" + places, 0, Debug.DebugColor.Green);
        }

        private void SpawnVillage()
        {
            SpawnNotables();
            int places = PointCount("npc_common") + PointCount("npc_common_limited");
            int crowd = Math.Max(4, Math.Min(18, (int)(places * (_setup.Night ? 0.15f : 0.5f))));
            SpawnCrowd(crowd, "villager", "village_woman", outdoor: true);
        }

        private void SpawnTavern()
        {
            DikozemieWalker keeper = SpawnFixed("spawnpoint_tavernkeeper", "tavernkeeper", "_tavern_keeper", 1).FirstOrDefault();
            if (keeper != null)
            {
                keeper.Person.Name = NameFor(false, 11);
            }
            int wenches = Math.Max(1, Math.Min(3, PointCount("sp_tavern_wench")));
            foreach (DikozemieWalker wench in SpawnFixed("sp_tavern_wench", "wench", "_barmaid", wenches))
            {
                wench.Person.Name = NameFor(true, 12 + wench.Mood);
            }
            bool aserai = _setup.Culture == "aserai" || _setup.Culture == "khuzait";
            string sitting = aserai ? "_villager_in_aserai_tavern" : "_villager_in_tavern";
            int places = PointCount("npc_common");
            int crowd = Math.Max(3, Math.Min(16, (int)(places * (_setup.Night ? 0.6f : 0.35f))));
            SpawnCrowd(crowd, "townsman", "townswoman", outdoor: false, actionSuffix: sitting);
            SpawnBrawler();
        }

        private void SpawnArena()
        {
            DikozemieWalker master = SpawnFixed("spawnpoint_tournamentmaster", "arena_master", "_villager", 1).FirstOrDefault();
            if (master == null)
            {
                master = SpawnNear("arena_master", "_villager", HeroStartPoint(), 2.5f);
            }
            if (master != null)
            {
                master.Person.Name = NameFor(false, 21);
            }
            SpawnGuards(false);
            int places = PointCount("npc_common") + PointCount("npc_common_limited");
            SpawnCrowd(Math.Max(2, Math.Min(10, (int)(places * 0.4f))), "townsman", "townswoman", outdoor: true);
        }

        private void SpawnHall()
        {
            // The steward stands by the empty throne: the lord is away.
            GameEntity throne = Mission.Scene.FindEntityWithTag("sp_throne");
            DikozemieWalker steward = throne != null
                ? SpawnNear("notable", "_villager", throne.GlobalPosition, 1.6f, "Управляющий")
                : null;
            steward = steward ??
                SpawnFixed("sp_notable", "notable", "_villager", 1, "Управляющий").FirstOrDefault() ??
                SpawnFixed("npc_common", "notable", "_villager", 1, "Управляющий").FirstOrDefault() ??
                SpawnNear("notable", "_villager", HeroStartPoint(), 4f, "Управляющий");
            if (steward != null)
            {
                steward.Person.Name = NameFor(false, 31);
            }
            SpawnGuards(true);
            SpawnCrowd(Math.Min(4, PointCount("npc_common")), "townsman", "townswoman", outdoor: false);
        }

        private Vec3 HeroStartPoint() => _heroAgent?.Position ?? Vec3.Zero;

        // The notables of the town screen («Горожане») stand in the streets.
        private void SpawnNotables()
        {
            IReadOnlyList<DikozemieNotable> notables = DikozemieTown.Notables(_setup.Node);
            var places = new List<UsableMachine>();
            foreach (string tag in new[] { "sp_notable", "sp_merchant_notary", "sp_artisan_notary",
                         "sp_gangleader_notary", "sp_preacher_notary", "sp_ruralnotable_notary", "npc_common" })
            {
                places.AddRange(Points(tag).Where(machine => !places.Contains(machine)));
            }
            int index = 0;
            foreach (DikozemieNotable notable in notables)
            {
                DikozemieTownPerson preset = DikozemieTownFolk.NotablePerson(_setup.Node, notable);
                if (preset == null)
                {
                    continue;
                }
                string role = preset.Role;
                UsableMachine machine = places.Skip(index).FirstOrDefault(m => FreePoint(m) != null);
                index++;
                DikozemieWalker walker = machine == null
                    ? SpawnNear(role, "_villager", HeroStartPoint(), 6f + index * 2f, notable.Role, preset)
                    : SpawnAtMachine(machine, role, "_villager", DikozemieWalkerKind.Fixed, notable.Role,
                        notable.Female ? "_settlement_slow" : "_settlement", preset);
                if (walker == null)
                {
                    continue;
                }
                walker.Person.Name = notable.Name;
                walker.Person.Title = notable.Role;
                walker.Person.Role = "notable";
                SetName(walker.Agent, walker.Person.Label);
                if (notable.Role == "Главарь шайки")
                {
                    // Two of his men stand near.
                    for (int guard = 0; guard < 2; guard++)
                    {
                        DikozemieWalker man = SpawnNear("thug", null, walker.Agent.Position, 2f + guard, "Подручный главаря");
                        if (man != null)
                        {
                            man.Talkable = false;
                            man.IsBrawler = true;
                        }
                    }
                }
            }
        }

        private void SpawnGuards(bool castle)
        {
            int count = 0;
            foreach (string tag in castle
                         ? new[] { "sp_guard_castle", "sp_guard", "sp_guard_with_spear" }
                         : new[] { "sp_guard", "sp_guard_with_spear", "sp_guard_patrol", "sp_guard_unarmed" })
            {
                int wanted = Math.Min(castle ? 6 : 8, PointCount(tag));
                foreach (DikozemieWalker guard in SpawnFixed(tag, "guard", tag == "sp_guard_unarmed" ? "_unarmed_guard" : "_guard", wanted))
                {
                    guard.IsGuard = true;
                    count++;
                }
                if (count >= (castle ? 8 : 10))
                {
                    break;
                }
            }
        }

        // Townsfolk spread over the gathering places; a few walk at once.
        private void SpawnCrowd(int count, string maleRole, string femaleRole, bool outdoor, string actionSuffix = null)
        {
            var places = Points("npc_common").Concat(Points("npc_common_limited")).Distinct().ToList();
            Shuffle(places);
            int spawned = 0;
            foreach (UsableMachine machine in places)
            {
                if (spawned >= count)
                {
                    break;
                }
                bool female = (spawned + _seedBase) % 3 == 1;
                string suffix = actionSuffix ?? (female ? "_villager_2" : (spawned % 3 == 0 ? "_villager" : spawned % 3 == 1 ? "_villager_2" : "_villager_3"));
                DikozemieWalker walker = SpawnAtMachine(machine, female ? femaleRole : maleRole, suffix,
                    outdoor ? DikozemieWalkerKind.Wanderer : DikozemieWalkerKind.Fixed, null,
                    female ? "_settlement_slow" : "_settlement");
                if (walker != null)
                {
                    walker.WaitUntil = Mission.CurrentTime + 4f + (float)_dice.NextDouble() * 40f;
                    spawned++;
                }
            }
        }

        private List<DikozemieWalker> SpawnFixed(string tag, string role, string actionSuffix, int count, string title = null)
        {
            var result = new List<DikozemieWalker>();
            if (count <= 0 || !DikozemieTownFolk.HasRole(role, _setup.Culture))
            {
                return result;
            }
            foreach (UsableMachine machine in Points(tag))
            {
                if (result.Count >= count)
                {
                    break;
                }
                DikozemieWalker walker = SpawnAtMachine(machine, role, actionSuffix, DikozemieWalkerKind.Fixed, title);
                if (walker != null)
                {
                    result.Add(walker);
                }
            }
            return result;
        }

        private StandingPoint FreePoint(UsableMachine machine)
        {
            if (machine?.StandingPoints == null)
            {
                return null;
            }
            foreach (StandingPoint point in machine.StandingPoints)
            {
                if (point == null || point.IsDeactivated || _taken.Contains(point) || point is PassageUsePoint)
                {
                    continue;
                }
                return point;
            }
            return null;
        }

        private DikozemieWalker SpawnAtMachine(UsableMachine machine, string role, string actionSuffix,
            DikozemieWalkerKind kind, string title = null, string monsterSuffix = "_settlement",
            DikozemieTownPerson preset = null)
        {
            StandingPoint point = FreePoint(machine);
            if (point == null)
            {
                return null;
            }
            MatrixFrame frame = point.GameEntity.GetGlobalFrame();
            frame.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
            DikozemieWalker walker = Spawn(role, actionSuffix, frame, kind, title, monsterSuffix, preset);
            if (walker == null)
            {
                return null;
            }
            walker.Machine = machine;
            walker.Point = point;
            _taken.Add(point);
            Arrive(walker);
            return walker;
        }

        private DikozemieWalker SpawnNear(string role, string actionSuffix, Vec3 around, float radius, string title = null,
            DikozemieTownPerson preset = null)
        {
            if (!DikozemieTownFolk.HasRole(role, _setup.Culture))
            {
                return null;
            }
            double angle = _dice.NextDouble() * Math.PI * 2.0;
            Vec3 wanted = around + new Vec3((float)Math.Cos(angle) * radius, (float)Math.Sin(angle) * radius, 0f);
            var frame = new MatrixFrame(Mat3.Identity, OnGround(wanted));
            Vec2 facing = (around - frame.origin).AsVec2;
            if (facing.LengthSquared > 0.01f)
            {
                frame.rotation.f = facing.Normalized().ToVec3();
                frame.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
            }
            return Spawn(role, actionSuffix, frame, DikozemieWalkerKind.Fixed, title, "_settlement", preset);
        }

        private int _spawnCount;

        private DikozemieWalker Spawn(string role, string actionSuffix, MatrixFrame frame,
            DikozemieWalkerKind kind, string title, string monsterSuffix, DikozemieTownPerson preset = null)
        {
            int seed = (int)DikozemieTownFolk.Mix(_seedBase + (++_spawnCount) * 104729);
            DikozemieTownPerson person = preset ?? DikozemieTownFolk.Create(role, _setup.Culture, seed);
            if (preset != null)
            {
                seed = preset.Seed;
            }
            if (person == null)
            {
                return null;
            }
            if (!string.IsNullOrEmpty(title))
            {
                person.Title = title;
            }
            if (string.IsNullOrEmpty(person.Name) && Array.IndexOf(NamedRoles, role) >= 0)
            {
                person.Name = NameFor(person.Female, 50 + _spawnCount);
            }
            try
            {
                BasicCharacterObject body = person.Body();
                Monster monster = TaleWorlds.Core.FaceGen.GetMonsterWithSuffix(body.Race, monsterSuffix ?? "_settlement") ??
                    TaleWorlds.Core.FaceGen.GetBaseMonsterFromRace(body.Race);
                Vec3 position = OnGround(frame.origin);
                Vec2 direction = frame.rotation.f.AsVec2.Normalized();
                if (!direction.IsNonZero())
                {
                    direction = new Vec2(0f, 1f);
                }
                BodyProperties face = DikozemieTownFolk.FaceOf(person);
                var data = new AgentBuildData(new BasicBattleAgentOrigin(body))
                    .Team(kind == DikozemieWalkerKind.Follower ? _heroTeam : Team.Invalid)
                    .Controller(AgentControllerType.AI)
                    .InitialPosition(in position)
                    .InitialDirection(in direction)
                    .Equipment(person.Equipment)
                    .EquipmentSeed(seed)
                    .Monster(monster)
                    .NoHorses(true)
                    .IsFemale(person.Female)
                    .BodyProperties(face)
                    .ClothingColor1(ClothColor(seed))
                    .ClothingColor2(ClothColor(seed / 7 + 3))
                    .CanSpawnOutsideOfMissionBoundary(true);
                Agent agent = Mission.SpawnAgent(data, false);
                if (agent == null)
                {
                    return null;
                }
                if (!string.IsNullOrEmpty(actionSuffix))
                {
                    string code = ActionSetCode.GenerateActionSetNameWithSuffix(monster, person.Female, actionSuffix);
                    MBActionSet set = MBGlobals.GetActionSet(code);
                    if (set.IsValid)
                    {
                        AnimationSystemData animation = monster.FillAnimationSystemData(set, body.GetStepSize(), false);
                        agent.SetActionSet(ref animation);
                    }
                    else
                    {
                        Debug.Print("DikozemieOnline: town walk no action set " + code, 0, Debug.DebugColor.Yellow);
                    }
                }
                agent.SetWatchState(Agent.WatchState.Patrolling);
                agent.SetAutomaticTargetSelection(false);
                // A town brawl ends when somebody yields; nobody dies.
                agent.SetMortalityState(Agent.MortalityState.Immortal);
                var walker = new DikozemieWalker
                {
                    Agent = agent,
                    Person = person,
                    Kind = kind,
                    Mood = (int)((uint)seed % 97u),
                    NextThink = Mission.CurrentTime + (float)_dice.NextDouble() * 2f
                };
                _walkers[agent] = walker;
                SetName(agent, person.Label);
                return walker;
            }
            catch (Exception error)
            {
                Debug.Print("DikozemieOnline: town walk person not spawned " + role + ": " + error.Message,
                    0, Debug.DebugColor.Red);
                return null;
            }
        }

        // People one talks to by name: «Торговец Ингольф», not «Торговец».
        private static readonly string[] NamedRoles =
        {
            "merchant", "weaponsmith", "armorer", "horse_merchant", "blacksmith", "barber",
            "tavernkeeper", "wench", "arena_master", "notable"
        };

        private static readonly uint[] Cloth =
        {
            0xff6b4f2f, 0xff7a6a4a, 0xff4e5b3b, 0xff5b3b3b, 0xff3b4a5b, 0xff8a7350, 0xff59524a, 0xff6e3a22,
            0xff40523f, 0xff7d5d3d
        };

        private static uint ClothColor(int seed) => Cloth[(int)((uint)seed % (uint)Cloth.Length)];

        private string NameFor(bool female, int salt)
        {
            IReadOnlyList<string> names = DikozemieTownCatalog.Names(DikozemieTown.NormalizeCulture(_setup.Node?.Culture), female);
            if (names == null || names.Count == 0)
            {
                return string.Empty;
            }
            return names[(int)((uint)(_seedBase * 31 + salt * 977) % (uint)names.Count)];
        }

        internal static void SetName(Agent agent, string name)
        {
            if (agent == null || string.IsNullOrEmpty(name) || AgentNameField == null)
            {
                return;
            }
            try
            {
                AgentNameField.SetValue(agent, new TextObject("{=!}" + name));
            }
            catch (Exception)
            {
            }
        }

        // ---- Life in the streets ------------------------------------------

        // The person stands at his place and does what the place is for.
        private void Arrive(DikozemieWalker walker)
        {
            Agent agent = walker.Agent;
            StandingPoint point = walker.Point;
            if (agent == null || point == null || !agent.IsActive())
            {
                return;
            }
            MatrixFrame frame = point.GameEntity.GetGlobalFrame();
            frame.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
            agent.DisableScriptedMovement();
            agent.TeleportToPosition(OnGround(frame.origin));
            agent.LookDirection = frame.rotation.f;
            agent.SetMovementDirection(frame.rotation.f.AsVec2.Normalized());
            if (point is AnimationPoint animation && !string.IsNullOrEmpty(animation.LoopStartAction))
            {
                ActionIndexCache loop = ActionIndexCache.Create(animation.LoopStartAction);
                if (loop != ActionIndexCache.act_none)
                {
                    agent.SetActionChannel(0, in loop, false, (AnimFlags)0uL, 0f,
                        0.85f + (float)_dice.NextDouble() * 0.2f, -0.2f, 0.4f, (float)_dice.NextDouble() * 0.5f);
                }
            }
            walker.AtPoint = true;
        }

        // Up from the bench, off to another place.
        private void Depart(DikozemieWalker walker)
        {
            Agent agent = walker.Agent;
            if (agent == null || !agent.IsActive())
            {
                return;
            }
            if (walker.Point is AnimationPoint animation && !string.IsNullOrEmpty(animation.LeaveAction))
            {
                ActionIndexCache leave = ActionIndexCache.Create(animation.LeaveAction);
                agent.SetActionChannel(0, in leave, false, (AnimFlags)0uL);
            }
            else
            {
                agent.SetActionChannel(0, in ActionIndexCache.act_none, true, (AnimFlags)0uL);
            }
            if (walker.Point != null)
            {
                _taken.Remove(walker.Point);
            }
            walker.AtPoint = false;
            walker.Point = null;
            walker.Machine = null;
        }

        private void Think()
        {
            float now = Mission.CurrentTime;
            foreach (DikozemieWalker walker in _walkers.Values.ToList())
            {
                Agent agent = walker.Agent;
                if (agent == null || !agent.IsActive() || walker.Kind == DikozemieWalkerKind.Thug)
                {
                    continue;
                }
                if (_talk != null && _talk.Agent == agent)
                {
                    continue;
                }
                if (InBrawl && (_brawlFoes.Contains(agent) || _brawlFriends.Contains(agent)))
                {
                    continue;
                }
                switch (walker.Kind)
                {
                    case DikozemieWalkerKind.Follower:
                        Follow(walker);
                        break;
                    case DikozemieWalkerKind.Wanderer:
                        Wander(walker, now);
                        break;
                    default:
                        // A guard back from a fight walks to his post.
                        if (walker.Point != null && !walker.AtPoint)
                        {
                            WalkTo(walker, now);
                        }
                        break;
                }
            }
        }

        private void Wander(DikozemieWalker walker, float now)
        {
            Agent agent = walker.Agent;
            if (walker.AtPoint)
            {
                if (walker.WaitUntil < 0f)
                {
                    float wait = walker.Point is AnimationPoint animation ? animation.GetRandomWaitInSeconds() : 20f;
                    walker.WaitUntil = now + (wait <= 0f ? 30f : Math.Min(90f, wait));
                }
                if (now < walker.WaitUntil)
                {
                    return;
                }
                UsableMachine next = PickPlace(agent, walker.Machine);
                if (next == null)
                {
                    walker.WaitUntil = now + 15f;
                    return;
                }
                Depart(walker);
                walker.Machine = next;
                walker.Point = FreePoint(next);
                if (walker.Point == null)
                {
                    walker.Machine = null;
                    walker.WaitUntil = now + 10f;
                    return;
                }
                _taken.Add(walker.Point);
                walker.WaitUntil = -1f;
                walker.LeaveAt = now + 1.2f;
                return;
            }
            if (walker.Point == null)
            {
                // Freed by a conversation: off to some other place.
                if (_talk == null || _talk.Agent != agent)
                {
                    UsableMachine next = PickPlace(agent, null);
                    StandingPoint point = next != null ? FreePoint(next) : null;
                    if (point != null)
                    {
                        walker.Machine = next;
                        walker.Point = point;
                        _taken.Add(point);
                        walker.NextThink = 0f;
                    }
                }
                return;
            }
            if (walker.LeaveAt > 0f && now < walker.LeaveAt)
            {
                return;
            }
            walker.LeaveAt = -1f;
            WalkTo(walker, now);
        }

        // To the person's place at a walk; there he does what the place is for.
        private void WalkTo(DikozemieWalker walker, float now)
        {
            Agent agent = walker.Agent;
            MatrixFrame frame = walker.Point.GameEntity.GetGlobalFrame();
            Vec3 target = frame.origin;
            float distance = agent.Position.AsVec2.Distance(target.AsVec2);
            if (distance < 0.9f)
            {
                Arrive(walker);
                return;
            }
            if (now >= walker.NextThink)
            {
                walker.NextThink = now + 3f;
                var world = new WorldPosition(Mission.Scene, UIntPtr.Zero, OnGround(target), false);
                agent.SetScriptedPosition(ref world, false, Agent.AIScriptedFrameFlags.NoAttack);
                // Townsfolk walk; they never run through the market.
                agent.SetMaximumSpeedLimit(walker.Person.Female ? 0.42f : 0.48f, true);
            }
        }

        private UsableMachine PickPlace(Agent agent, UsableMachine current)
        {
            var places = Points("npc_common").Concat(Points("npc_common_limited"))
                .Where(machine => machine != current && FreePoint(machine) != null)
                .ToList();
            if (places.Count == 0)
            {
                return null;
            }
            // Mostly somewhere near; now and then across the town.
            places.Sort((a, b) => a.GameEntity.GlobalPosition.AsVec2.DistanceSquared(agent.Position.AsVec2)
                .CompareTo(b.GameEntity.GlobalPosition.AsVec2.DistanceSquared(agent.Position.AsVec2)));
            int span = _dice.Next(4) == 0 ? places.Count : Math.Min(places.Count, 8);
            return places[_dice.Next(span)];
        }

        private void Follow(DikozemieWalker walker)
        {
            Agent agent = walker.Agent;
            if (_heroAgent == null || !_heroAgent.IsActive())
            {
                return;
            }
            float distance = agent.Position.AsVec2.Distance(_heroAgent.Position.AsVec2);
            if (distance < 3.2f)
            {
                agent.DisableScriptedMovement();
                return;
            }
            if (distance > 40f)
            {
                agent.TeleportToPosition(OnGround(_heroAgent.Position - _heroAgent.LookDirection * 2f));
                return;
            }
            Vec2 back = -_heroAgent.LookDirection.AsVec2.Normalized();
            Vec2 side = back.LeftVec() * ((walker.Mood % 2 == 0 ? 1f : -1f) * 1.2f);
            Vec3 spot = OnGround(_heroAgent.Position + (back * 1.8f + side).ToVec3());
            var world = new WorldPosition(Mission.Scene, UIntPtr.Zero, spot, false);
            agent.SetScriptedPosition(ref world, false, Agent.AIScriptedFrameFlags.NoAttack);
            agent.SetMaximumSpeedLimit(distance > 8f ? 1f : 0.55f, true);
        }

        // ---- The hero's company -------------------------------------------

        private void SpawnCompany()
        {
            List<DikozemieRoadFighter> company = DikozemieRoads.CompanyFighters();
            if (company == null || _heroAgent == null)
            {
                return;
            }
            int index = 0;
            // Those already walking with the hero stay; a newly hired one
            // gets up from the table and comes over.
            foreach (DikozemieRoadFighter fighter in company.Skip(CompanyCount))
            {
                try
                {
                    Vec2 back = -_heroAgent.LookDirection.AsVec2.Normalized();
                    Vec3 spot = OnGround(_heroAgent.Position + (back * (1.5f + index) + back.LeftVec() * (index % 2 == 0 ? 1f : -1f)).ToVec3());
                    BasicCharacterObject body = fighter.FaceCharacter();
                    Vec2 direction = _heroAgent.LookDirection.AsVec2.Normalized();
                    var data = new AgentBuildData(new BasicBattleAgentOrigin(body))
                        .Team(_heroTeam)
                        .Controller(AgentControllerType.AI)
                        .InitialPosition(in spot)
                        .InitialDirection(in direction)
                        .Equipment(fighter.Equipment)
                        .EquipmentSeed(fighter.Seed)
                        .NoHorses(true)
                        .BodyProperties(body.GetBodyProperties(fighter.Equipment, fighter.Seed))
                        .IsFemale(false)
                        .CanSpawnOutsideOfMissionBoundary(true);
                    Agent agent = Mission.SpawnAgent(data, false);
                    if (agent == null)
                    {
                        continue;
                    }
                    agent.SetWatchState(Agent.WatchState.Patrolling);
                    agent.SetMortalityState(Agent.MortalityState.Immortal);
                    var walker = new DikozemieWalker
                    {
                        Agent = agent,
                        Kind = DikozemieWalkerKind.Follower,
                        Person = new DikozemieTownPerson
                        {
                            Role = fighter.Role,
                            Title = fighter.Name,
                            Culture = fighter.Culture,
                            Seed = fighter.Seed,
                            Equipment = fighter.Equipment
                        },
                        Mood = index
                    };
                    _walkers[agent] = walker;
                    SetName(agent, fighter.Name);
                    index++;
                }
                catch (Exception error)
                {
                    Debug.Print("DikozemieOnline: town walk companion not spawned: " + error.Message, 0, Debug.DebugColor.Red);
                }
            }
        }

        internal int CompanyCount => _walkers.Values.Count(w => w.Kind == DikozemieWalkerKind.Follower && w.Agent.IsActive());

        internal void RefreshCompany() => SpawnCompany();

        // Horses at the posts, sheep and geese by day, as in the singleplayer town.
        private void SpawnAnimals()
        {
            if (_setup.Place != DikozemieWalkPlace.Streets)
            {
                return;
            }
            TrySpawn(() => SandBox.SandBoxHelpers.MissionHelper.SpawnHorses(), "horses");
            if (_setup.Night)
            {
                return;
            }
            TrySpawn(() => SandBox.SandBoxHelpers.MissionHelper.SpawnSheeps(), "sheep");
            TrySpawn(() => SandBox.SandBoxHelpers.MissionHelper.SpawnCows(), "cows");
            TrySpawn(() => SandBox.SandBoxHelpers.MissionHelper.SpawnHogs(), "hogs");
            TrySpawn(() => SandBox.SandBoxHelpers.MissionHelper.SpawnGeese(), "geese");
            TrySpawn(() => SandBox.SandBoxHelpers.MissionHelper.SpawnChicken(), "chicken");
        }

        private static void TrySpawn(Action spawn, string what)
        {
            try
            {
                spawn();
            }
            catch (Exception error)
            {
                Debug.Print("DikozemieOnline: town walk no " + what + ": " + error.Message, 0, Debug.DebugColor.Yellow);
            }
        }

        // ---- Helpers ------------------------------------------------------

        internal Vec3 OnGround(Vec3 wanted)
        {
            Scene scene = Mission.Scene;
            for (int ring = 0; ring <= 5; ring++)
            {
                int steps = ring == 0 ? 1 : 8;
                for (int step = 0; step < steps; step++)
                {
                    float angle = (float)(Math.PI * 2.0 * step / steps);
                    float radius = ring * 0.6f;
                    var probe = new Vec3(
                        wanted.x + (float)Math.Cos(angle) * radius,
                        wanted.y + (float)Math.Sin(angle) * radius,
                        wanted.z + 0.5f);
                    var world = new WorldPosition(scene, UIntPtr.Zero, probe, false);
                    if (world.GetNavMesh() == UIntPtr.Zero)
                    {
                        continue;
                    }
                    Vec3 ground = world.GetGroundVec3();
                    if (!float.IsNaN(ground.z) && Math.Abs(ground.z - wanted.z) < 3f)
                    {
                        return ground;
                    }
                }
            }
            float height = scene.GetGroundHeightAtPosition(wanted);
            return new Vec3(wanted.x, wanted.y, float.IsNaN(height) ? wanted.z : height);
        }

        private void Shuffle<T>(IList<T> list)
        {
            for (int index = list.Count - 1; index > 0; index--)
            {
                int other = _dice.Next(index + 1);
                T swap = list[index];
                list[index] = list[other];
                list[other] = swap;
            }
        }

        private static string Describe(Vec3 position) =>
            position.x.ToString("0.0") + "," + position.y.ToString("0.0") + "," + position.z.ToString("0.0");

        internal void Say(string text)
        {
            InformationManager.DisplayMessage(new InformationMessage(text, new Color(0.95f, 0.85f, 0.6f)));
        }

        private void LogCensus()
        {
            var tags = _points.Where(pair => pair.Key.StartsWith("sp_", StringComparison.Ordinal) ||
                    pair.Key.StartsWith("npc_", StringComparison.Ordinal) ||
                    pair.Key.StartsWith("spawnpoint", StringComparison.Ordinal) ||
                    pair.Key == "musician" || pair.Key == "gambler_npc")
                .OrderBy(pair => pair.Key)
                .Select(pair => pair.Key + "=" + pair.Value.Count);
            Debug.Print("DikozemieOnline: town walk census scene=" + _setup.Scene + " levels='" + _setup.Levels +
                "' points: " + string.Join(" ", tags), 0, Debug.DebugColor.Green);
            Debug.Print("DikozemieOnline: town walk doors: " + string.Join(" ", _doors.Select(d => d.To + "@" + Describe(d.Position))) +
                " people=" + _walkers.Count, 0, Debug.DebugColor.Green);
            DikozemieLobbyShellIntegration.TraceButtonAction("town walk ready scene=" + _setup.Scene +
                " people=" + _walkers.Count + " doors=" + string.Join(",", _doors.Select(d => d.To)));
        }
    }
}
