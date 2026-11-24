using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    /// <summary>
    /// Turns the native War Sails custom battle into a hands-off boarding
    /// delivery. NavalDLC is resolved only after its CustomGame is running so
    /// the multiplayer module keeps no hard assembly dependency on the DLC.
    /// </summary>
    internal static class DikozemieNavalAutoboardingController
    {
        private const string ShipsLogicTypeName =
            "NavalDLC.Missions.MissionLogics.NavalShipsLogic";
        private const string AgentsLogicTypeName =
            "NavalDLC.Missions.MissionLogics.NavalAgentsLogic";
        private const string BoardingBehaviorTypeName =
            "NavalDLC.Missions.AI.Behaviors." +
            "BehaviorNavalEngageCorrespondingEnemy";
        private const string SkirmishBehaviorTypeName =
            "NavalDLC.Missions.AI.Behaviors.BehaviorNavalSkirmish";
        private const string RammingBehaviorTypeName =
            "NavalDLC.Missions.AI.Behaviors.BehaviorNavalRamming";
        private const string SmokeLogName = "naval-autoboarding.log";

        internal static void RecordLockedPresetLaunch()
        {
            Trace(
                "DikozemieOnline: native naval setup hidden " +
                "playerConfigurable=false presetLocked=true");
        }

        internal static void RecordLifecycle(string phase)
        {
            Trace("DikozemieOnline: naval return " + phase);
        }

        internal static void RecordLaunchFailure(Exception exception)
        {
            Trace(
                "DikozemieOnline: naval launch failed " +
                (exception?.ToString() ?? "unknown error"));
        }

        private static readonly TimeSpan ApplyInterval =
            TimeSpan.FromMilliseconds(250d);

        private static bool _requested;
        private static bool _missionObserved;
        private static bool _readyMessageShown;
        private static bool _initialSpacingApplied;
        private static bool _playerProfileApplied;
        private static bool _rosterAuditLogged;
        private static string _hullId = string.Empty;
        private static string _rosterSummary = string.Empty;
        private static float _targetStartDistance;
        private static int _crewCapacity;
        private static int _safetySampleIndex;
        private static DateTimeOffset _requestedAt;
        private static DateTimeOffset _nextApplyAt;
        private static DateTimeOffset _nextFailureLogAt;
        private static DateTimeOffset _nextSafetyLogAt;
        private static DateTimeOffset _resultObservedAt;
        private static bool _progressRecorded;
        private static bool _missionExitRequested;
        private static bool _resultAcknowledged;
        private static bool _smokeResultAcknowledge;
        private static DikozemieNavalResultSummary _navalResultSummary;
        private static int _killTrackerCallbacks;
        private static int _heroAttributedRemovals;
        private static int _personalKills;
        private static Mission _mission;
        private static Agent _navalHeroAgent;
        private static object _shipsLogic;
        private static object _agentsLogic;
        private static Type _shipsLogicType;
        private static Type _agentsLogicType;
        private static Type _missionShipType;
        private static Type _boardingBehaviorType;
        private static Type _skirmishBehaviorType;
        private static Type _rammingBehaviorType;
        private static EventInfo _hookEvent;
        private static EventInfo _connectedEvent;
        private static EventInfo _bridgeConnectedEvent;
        private static EventInfo _boardingEvent;
        private static EventInfo _missionEndEvent;
        private static Delegate _hookHandler;
        private static Delegate _connectedHandler;
        private static Delegate _bridgeConnectedHandler;
        private static Delegate _boardingHandler;
        private static Delegate _missionEndHandler;
        private static NavalPersonalKillTracker _personalKillTracker;
        private static readonly HashSet<int> CrewChargedShipIndices =
            new HashSet<int>();
        private static readonly HashSet<Agent> RewardedNavalVictims =
            new HashSet<Agent>();
        private static readonly HashSet<Agent> ObservedRuntimeAgents =
            new HashSet<Agent>();
        private static readonly Dictionary<Agent, DateTimeOffset>
            SwimmerSince = new Dictionary<Agent, DateTimeOffset>();
        private static readonly Dictionary<string, int>
            ExpectedRuntimeCharacterCounts =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, int>
            ObservedRuntimeCharacterCounts =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        internal static void BeginSession(
            string hullId,
            float targetStartDistance,
            int crewCapacity,
            string rosterSummary,
            IEnumerable<string> expectedRuntimeCharacterIds,
            bool smokeResultAcknowledge)
        {
            ResetSession("rearmed");
            _requested = true;
            _hullId = hullId ?? string.Empty;
            _targetStartDistance = Math.Max(48f, targetStartDistance);
            _crewCapacity = Math.Max(1, crewCapacity);
            _rosterSummary = rosterSummary ?? string.Empty;
            _smokeResultAcknowledge = smokeResultAcknowledge;
            foreach (string characterId in expectedRuntimeCharacterIds ??
                     Enumerable.Empty<string>())
            {
                string id = characterId ?? string.Empty;
                ExpectedRuntimeCharacterCounts.TryGetValue(
                    id,
                    out int count);
                ExpectedRuntimeCharacterCounts[id] = count + 1;
            }
            _requestedAt = DateTimeOffset.UtcNow;
            _nextApplyAt = DateTimeOffset.MinValue;
            Trace(
                "DikozemieOnline: naval autoboarding armed hull=" +
                _hullId + " targetStartDistance=" +
                _targetStartDistance.ToString("0.0") + "m");
            if (!string.IsNullOrWhiteSpace(_rosterSummary))
            {
                Trace("DikozemieOnline: naval " + _rosterSummary);
            }
        }

        internal static void CancelSession()
        {
            ResetSession("cancelled");
        }

        internal static void Tick()
        {
            if (!_requested)
            {
                return;
            }

            try
            {
                TickCore();
            }
            catch (Exception exception)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                if (now >= _nextFailureLogAt)
                {
                    _nextFailureLogAt = now + TimeSpan.FromSeconds(5d);
                    Exception cause = exception is TargetInvocationException tie
                        ? tie.InnerException ?? tie
                        : exception;
                    Trace(
                        "DikozemieOnline: naval autoboarding retry after " +
                        cause.GetType().Name + ": " + cause.Message);
                }
            }
        }

        private static void TickCore()
        {
            Mission currentMission = Mission.Current;
            if (_missionObserved && !ReferenceEquals(currentMission, _mission))
            {
                ResetSession("mission changed");
                return;
            }

            if (currentMission == null)
            {
                if (!_missionObserved &&
                    DateTimeOffset.UtcNow - _requestedAt >
                    TimeSpan.FromSeconds(90d))
                {
                    ResetSession("mission did not appear");
                }
                return;
            }

            if (!ResolveNavalTypes())
            {
                return;
            }

            object shipsLogic = GetMissionBehavior(
                currentMission,
                _shipsLogicType);
            if (shipsLogic == null)
            {
                return;
            }

            if (!_missionObserved)
            {
                ObserveMission(currentMission, shipsLogic);
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (currentMission.IsDeploymentFinished)
            {
                EnsureSavedArenaHeroRuntime(currentMission);
                TraceArenaRosterAudit(currentMission);
                MonitorStrandedSwimmers(currentMission, now);
            }

            if (TryAutoFinishCompletedMission(currentMission, now))
            {
                return;
            }

            if (now < _nextApplyAt)
            {
                return;
            }
            _nextApplyAt = now + ApplyInterval;

            IEnumerable ships = _shipsLogicType
                .GetProperty("AllShips", BindingFlags.Instance |
                    BindingFlags.Public)
                ?.GetValue(shipsLogic) as IEnumerable;
            if (ships == null)
            {
                return;
            }

            int configuredShips = 0;
            bool playerShipReady = false;
            var activeShips = new List<object>();
            foreach (object ship in ships)
            {
                if (ConfigureShip(ship))
                {
                    configuredShips++;
                    playerShipReady |= GetBool(ship, "IsPlayerShip");
                    activeShips.Add(ship);
                }
            }

            if (configuredShips >= 2 && playerShipReady)
            {
                TryApplyInitialSpacing(activeShips);
                ReassertBoardingCharge(activeShips);
                TraceCrewSafety(now);
            }

            if (!_readyMessageShown && configuredShips >= 2 &&
                playerShipReady)
            {
                _readyMessageShown = true;
                InformationManager.DisplayMessage(new InformationMessage(
                    "Корабли сами идут на абордаж. Штурвал отключён — " +
                    "готовьтесь к схватке.",
                    Colors.Cyan));
                Trace(
                    "DikozemieOnline: naval autoboarding active ships=" +
                    configuredShips + " playerShipAI=true formationAI=true " +
                    "helmDisabled=true");
            }
        }

        private static bool ResolveNavalTypes()
        {
            if (_shipsLogicType != null)
            {
                return true;
            }

            _shipsLogicType = FindLoadedType(ShipsLogicTypeName);
            _agentsLogicType = FindLoadedType(AgentsLogicTypeName);
            _missionShipType = FindLoadedType(
                "NavalDLC.Missions.Objects.MissionShip");
            _boardingBehaviorType = FindLoadedType(
                BoardingBehaviorTypeName);
            _skirmishBehaviorType = FindLoadedType(
                SkirmishBehaviorTypeName);
            _rammingBehaviorType = FindLoadedType(
                RammingBehaviorTypeName);
            return _shipsLogicType != null &&
                _agentsLogicType != null &&
                _missionShipType != null &&
                _boardingBehaviorType != null &&
                _skirmishBehaviorType != null &&
                _rammingBehaviorType != null;
        }

        private static Type FindLoadedType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain
                .GetAssemblies())
            {
                Type type = assembly.GetType(
                    fullName,
                    throwOnError: false,
                    ignoreCase: false);
                if (type != null)
                {
                    return type;
                }
            }
            return null;
        }

        private static object GetMissionBehavior(
            Mission mission,
            Type behaviorType)
        {
            MethodInfo getter = typeof(Mission).GetMethods(
                    BindingFlags.Instance | BindingFlags.Public)
                .First(method =>
                    method.Name == "GetMissionBehavior" &&
                    method.IsGenericMethodDefinition &&
                    method.GetParameters().Length == 0);
            return getter.MakeGenericMethod(behaviorType)
                .Invoke(mission, null);
        }

        private static void ObserveMission(
            Mission mission,
            object shipsLogic)
        {
            _missionObserved = true;
            _mission = mission;
            _shipsLogic = shipsLogic;
            _agentsLogic = GetMissionBehavior(
                mission,
                _agentsLogicType);
            _personalKillTracker = new NavalPersonalKillTracker();
            mission.AddMissionBehavior(_personalKillTracker);

            _hookEvent = _shipsLogicType.GetEvent("ShipHookThrowEvent");
            _connectedEvent = _shipsLogicType.GetEvent("ShipsConnectedEvent");
            _bridgeConnectedEvent = _shipsLogicType.GetEvent(
                "BridgeConnectedEvent");
            _boardingEvent = _shipsLogicType.GetEvent("BoardingOrderEvent");
            _missionEndEvent = _shipsLogicType.GetEvent("MissionEndEvent");
            _hookHandler = CreateShipEventHandler(
                nameof(OnShipHookThrown));
            _connectedHandler = CreateShipEventHandler(
                nameof(OnShipsConnected));
            _bridgeConnectedHandler = CreateShipEventHandler(
                nameof(OnBridgeConnected));
            _boardingHandler = CreateShipEventHandler(
                nameof(OnBoardingOrdered));
            _missionEndHandler = Delegate.CreateDelegate(
                _missionEndEvent.EventHandlerType,
                typeof(DikozemieNavalAutoboardingController).GetMethod(
                    nameof(OnMissionEnded),
                    BindingFlags.Static | BindingFlags.NonPublic));

            _hookEvent.AddEventHandler(_shipsLogic, _hookHandler);
            _connectedEvent.AddEventHandler(
                _shipsLogic,
                _connectedHandler);
            _bridgeConnectedEvent.AddEventHandler(
                _shipsLogic,
                _bridgeConnectedHandler);
            _boardingEvent.AddEventHandler(
                _shipsLogic,
                _boardingHandler);
            _missionEndEvent.AddEventHandler(
                _shipsLogic,
                _missionEndHandler);
            Trace(
                "DikozemieOnline: naval autoboarding observed native " +
                "mission hull=" + _hullId);
        }

        private static void EnsureSavedArenaHeroRuntime(Mission mission)
        {
            if (_playerProfileApplied || mission?.MainAgent == null)
            {
                return;
            }

            Agent player = mission.MainAgent;
            _navalHeroAgent = player;
            DikozemieHeroProfileState profile = DikozemieHeroProfile.Snapshot;
            if (profile == null || !profile.CreationCompleted)
            {
                throw new InvalidOperationException(
                    "Saved arena hero is unavailable in naval mission.");
            }

            Equipment equipment = new Equipment(
                DikozemieHeroProfile.CreateBattleEquipment(
                    player.Character));
            equipment[EquipmentIndex.Horse] = default(EquipmentElement);
            equipment[EquipmentIndex.HorseHarness] =
                default(EquipmentElement);

            if (BodyProperties.FromString(
                    profile.BodyProperties,
                    out BodyProperties bodyProperties))
            {
                player.IsFemale = profile.IsFemale;
                player.UpdateBodyProperties(bodyProperties);
            }
            player.UpdateSpawnEquipmentAndRefreshVisuals(equipment);
            DikozemieHeroProfile.ApplyHealthBonus(player);
            player.WieldInitialWeapons();
            _playerProfileApplied = true;

            Trace(
                "DikozemieOnline: naval player runtime profile applied " +
                "playerProfile=true arenaEquipment=true character=" +
                player.Character?.StringId + " name=" + profile.Name +
                " equipment=" + DescribeEquipment(equipment));
        }

        private static void TraceArenaRosterAudit(
            Mission mission,
            bool final = false)
        {
            if (_rosterAuditLogged || mission == null ||
                ExpectedRuntimeCharacterCounts.Count == 0)
            {
                return;
            }

            List<Agent> activeHumans = mission.Teams
                .Where(team => team != null &&
                    (team.Side == BattleSideEnum.Attacker ||
                        team.Side == BattleSideEnum.Defender))
                .SelectMany(team => team.ActiveAgents)
                .Where(agent => agent != null && agent.IsHuman &&
                    agent.IsActive())
                .ToList();
            foreach (Agent agent in activeHumans)
            {
                if (!ObservedRuntimeAgents.Add(agent))
                {
                    continue;
                }

                string characterId = agent.Character?.StringId ??
                    string.Empty;
                ObservedRuntimeCharacterCounts.TryGetValue(
                    characterId,
                    out int observedCount);
                ObservedRuntimeCharacterCounts[characterId] =
                    observedCount + 1;
            }

            int expectedTotal = ExpectedRuntimeCharacterCounts.Values.Sum();
            int observedTotal = ObservedRuntimeAgents.Count;
            if (observedTotal < expectedTotal && !final)
            {
                return;
            }

            bool exact = observedTotal == expectedTotal &&
                ExpectedRuntimeCharacterCounts.All(expected =>
                    ObservedRuntimeCharacterCounts.TryGetValue(
                        expected.Key,
                        out int actualCount) &&
                    actualCount == expected.Value);
            _rosterAuditLogged = true;

            string actualIds = string.Join(
                ",",
                ObservedRuntimeCharacterCounts
                    .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(pair => pair.Key + "x" + pair.Value));
            Trace(
                "DikozemieOnline: naval arena roster audit exact=" +
                exact + " expected=" + expectedTotal + " observed=" +
                observedTotal + " active=" + activeHumans.Count +
                " distinct=" + ObservedRuntimeCharacterCounts.Count +
                " ids=" + actualIds);
        }

        private static string DescribeEquipment(Equipment equipment)
        {
            return string.Join(
                ",",
                Enumerable.Range(0, 12)
                    .Select(index => equipment[index].Item?.StringId)
                    .Where(id => !string.IsNullOrWhiteSpace(id)));
        }

        private static void TryApplyInitialSpacing(
            IReadOnlyList<object> ships)
        {
            if (_initialSpacingApplied || ships == null || ships.Count < 2)
            {
                return;
            }

            object playerShip = ships.FirstOrDefault(ship =>
                GetBool(ship, "IsPlayerShip"));
            object enemyShip = ships.FirstOrDefault(ship =>
                !ReferenceEquals(ship, playerShip));
            if (playerShip == null || enemyShip == null ||
                !(GetProperty(playerShip, "GlobalFrame") is MatrixFrame
                    playerFrame) ||
                !(GetProperty(enemyShip, "GlobalFrame") is MatrixFrame
                    enemyFrame))
            {
                return;
            }

            float deltaX = enemyFrame.origin.x - playerFrame.origin.x;
            float deltaY = enemyFrame.origin.y - playerFrame.origin.y;
            float currentDistance = (float)Math.Sqrt(
                deltaX * deltaX + deltaY * deltaY);
            if (currentDistance < 0.01f)
            {
                return;
            }

            float safeHullDistance = Math.Max(
                GetShipLength(playerShip),
                GetShipLength(enemyShip)) + 18f;
            float desiredDistance = Math.Max(
                _targetStartDistance,
                safeHullDistance);
            _initialSpacingApplied = true;
            if (currentDistance <= desiredDistance + 1f)
            {
                Trace(
                    "DikozemieOnline: naval start spacing kept current=" +
                    currentDistance.ToString("0.0") + "m desired=" +
                    desiredDistance.ToString("0.0") + "m");
                return;
            }

            float directionX = deltaX / currentDistance;
            float directionY = deltaY / currentDistance;
            float middleX =
                (playerFrame.origin.x + enemyFrame.origin.x) * 0.5f;
            float middleY =
                (playerFrame.origin.y + enemyFrame.origin.y) * 0.5f;
            float halfDistance = desiredDistance * 0.5f;
            playerFrame.origin.x = middleX - directionX * halfDistance;
            playerFrame.origin.y = middleY - directionY * halfDistance;
            enemyFrame.origin.x = middleX + directionX * halfDistance;
            enemyFrame.origin.y = middleY + directionY * halfDistance;

            Invoke(_shipsLogic, "SetTeleportShips", true);
            try
            {
                // NavalShipsLogic.TeleportShip moves the complete mission
                // object, refreshes its attached navmesh, and raises the
                // native event that moves every assigned agent by the same
                // local transform. Hull and crew therefore cannot separate.
                Invoke(
                    _shipsLogic,
                    "TeleportShip",
                    playerShip,
                    playerFrame,
                    false,
                    false,
                    true);
                Invoke(
                    _shipsLogic,
                    "TeleportShip",
                    enemyShip,
                    enemyFrame,
                    false,
                    false,
                    true);
            }
            finally
            {
                Invoke(_shipsLogic, "SetTeleportShips", false);
            }

            Trace(
                "DikozemieOnline: naval start spacing shortened from=" +
                currentDistance.ToString("0.0") + "m to=" +
                desiredDistance.ToString("0.0") +
                "m nativeTeleport=true agentsMovedWithShip=true");
        }

        private static float GetShipLength(object ship)
        {
            object missionShipObject = GetProperty(
                ship,
                "MissionShipObject");
            object area = GetProperty(missionShipObject, "DeploymentArea");
            return area is Vec2 deploymentArea
                ? Math.Max(deploymentArea.x, deploymentArea.y)
                : 0f;
        }

        private static List<Agent> GetActiveBattleHumans(Mission mission)
        {
            return mission?.Teams
                .Where(team => team != null &&
                    (team.Side == BattleSideEnum.Attacker ||
                        team.Side == BattleSideEnum.Defender))
                .SelectMany(team => team.ActiveAgents)
                .Where(agent => agent != null && agent.IsHuman &&
                    agent.IsActive())
                .Distinct()
                .ToList() ?? new List<Agent>();
        }

        private static void MonitorStrandedSwimmers(
            Mission mission,
            DateTimeOffset now)
        {
            List<Agent> activeHumans = GetActiveBattleHumans(mission);
            var activeSet = new HashSet<Agent>(activeHumans);
            foreach (Agent tracked in SwimmerSince.Keys
                         .Where(agent => !activeSet.Contains(agent) ||
                             !agent.IsInWater())
                         .ToList())
            {
                SwimmerSince.Remove(tracked);
            }

            bool boardingConnected = CrewChargedShipIndices.Count > 0;
            var casualties = new List<Agent>();
            foreach (Agent agent in activeHumans)
            {
                if (!agent.IsInWater())
                {
                    SwimmerSince.Remove(agent);
                    continue;
                }

                if (!SwimmerSince.TryGetValue(agent, out DateTimeOffset since))
                {
                    since = now;
                    SwimmerSince[agent] = since;
                }
                if (DikozemieNavalCompletionRules.ShouldResolveSwimmer(
                        boardingConnected,
                        activeHuman: agent.IsActive() && agent.IsHuman,
                        inWater: agent.IsInWater(),
                        swimmingFor: now - since))
                {
                    casualties.Add(agent);
                }
            }

            foreach (Agent agent in casualties)
            {
                if (!agent.IsActive() || !agent.IsInWater())
                {
                    SwimmerSince.Remove(agent);
                    continue;
                }

                var drowningBlow = new Blow(-1)
                {
                    DamageType = DamageTypes.Blunt,
                    InflictedDamage = Math.Max(
                        1,
                        (int)Math.Ceiling(agent.Health)),
                    DamageCalculated = true,
                    IsFallDamage = true,
                    GlobalPosition = agent.Position,
                    BoneIndex = 0,
                    VictimBodyPart = BoneBodyPartType.Chest
                };
                SwimmerSince.Remove(agent);
                Trace(
                    "DikozemieOnline: naval overboard casualty character=" +
                    (agent.Character?.StringId ?? "unknown") +
                    " side=" + agent.Team?.Side +
                    " mainAgent=" + agent.IsMainAgent +
                    " swimmingSeconds=" +
                    DikozemieNavalCompletionRules.OverboardGrace
                        .TotalSeconds.ToString("0"));
                agent.Die(drowningBlow, Agent.KillInfo.Gravity);
            }
        }

        private static void TraceCrewSafety(DateTimeOffset now)
        {
            if (_agentsLogic == null || _mission == null ||
                !_mission.IsDeploymentFinished ||
                now < _nextSafetyLogAt)
            {
                return;
            }
            _nextSafetyLogAt = now + TimeSpan.FromSeconds(5d);

            MethodInfo isOnAnyShip = _agentsLogicType.GetMethods(
                    BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(method =>
                    method.Name == "IsAgentOnAnyShip" &&
                    method.GetParameters().Length == 3);
            if (isOnAnyShip == null)
            {
                return;
            }

            Type teamSideType = isOnAnyShip.GetParameters()[2]
                .ParameterType;
            object anyTeamSide = Enum.ToObject(teamSideType, -1);
            List<Agent> activeHumans = GetActiveBattleHumans(_mission);
            int onAnyShip = 0;
            int swimming = 0;
            foreach (Agent agent in activeHumans)
            {
                if (agent.IsInWater())
                {
                    swimming++;
                }
                object[] arguments =
                {
                    agent,
                    null,
                    anyTeamSide
                };
                if (isOnAnyShip.Invoke(
                        _agentsLogic,
                        arguments) is bool onShip && onShip)
                {
                    onAnyShip++;
                }
            }

            if (activeHumans.Count <= 0)
            {
                return;
            }
            _safetySampleIndex++;
            Trace(
                "DikozemieOnline: naval crew safety sample=" +
                _safetySampleIndex + " activeHumans=" +
                activeHumans.Count +
                " onShip=" + onAnyShip + " overboard=" +
                (activeHumans.Count - onAnyShip) + " swimming=" +
                swimming + " expectedRoster=" +
                (_crewCapacity * 2));
        }

        private static Delegate CreateShipEventHandler(string methodName)
        {
            MethodInfo openMethod =
                typeof(DikozemieNavalAutoboardingController).GetMethod(
                    methodName,
                    BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo closedMethod = openMethod.MakeGenericMethod(
                _missionShipType);
            EventInfo sourceEvent = methodName == nameof(OnShipHookThrown)
                ? _hookEvent
                : methodName == nameof(OnShipsConnected)
                    ? _connectedEvent
                    : methodName == nameof(OnBridgeConnected)
                        ? _bridgeConnectedEvent
                        : _boardingEvent;
            return Delegate.CreateDelegate(
                sourceEvent.EventHandlerType,
                closedMethod);
        }

        private static bool ConfigureShip(object ship)
        {
            if (ship == null ||
                GetBool(ship, "IsRemoved") ||
                GetBool(ship, "IsSinking"))
            {
                return false;
            }

            Formation formation = GetProperty(ship, "Formation") as Formation;
            if (formation == null)
            {
                return false;
            }

            object controllerMachine = GetProperty(
                ship,
                "ShipControllerMachine");
            Agent pilot = GetProperty(
                controllerMachine,
                "PilotAgent") as Agent;
            if (pilot?.IsPlayerControlled == true)
            {
                pilot.StopUsingGameObject(
                    true,
                    (Agent.StopUsingGameObjectFlags)1);
                Trace(
                    "DikozemieOnline: player detached from naval helm " +
                    "ship=" + GetShipIndex(ship));
            }

            StandingPoint standingPoint = GetProperty(
                controllerMachine,
                "PilotStandingPoint") as StandingPoint;
            if (standingPoint != null)
            {
                standingPoint.IsDisabledForPlayers = true;
            }

            Invoke(ship, "SetCanBeTakenOver", false);
            if (!formation.IsAIControlled)
            {
                formation.SetControlledByAI(true, false);
            }
            if (!GetBool(ship, "IsAIControlled"))
            {
                SetShipAiController(ship);
            }

            ForceBoardingBehavior(formation);
            return GetBool(ship, "IsAIControlled") &&
                formation.IsAIControlled;
        }

        private static void SetShipAiController(object ship)
        {
            MethodInfo setter = ship.GetType().GetMethods(
                    BindingFlags.Instance | BindingFlags.Public)
                .First(method =>
                    method.Name == "SetController" &&
                    method.GetParameters().Length == 2);
            Type controllerType = setter.GetParameters()[0].ParameterType;
            object aiController = Enum.Parse(controllerType, "AI");
            // false disables MissionShip's automatic Player takeover.
            setter.Invoke(ship, new[] { aiController, (object)false });
        }

        private static void ForceBoardingBehavior(Formation formation)
        {
            if (formation.AI == null)
            {
                return;
            }

            bool hasBoarding = false;
            bool hasSkirmish = false;
            bool hasRamming = false;
            for (int index = 0;
                index < formation.AI.BehaviorCount;
                index++)
            {
                object behavior = formation.AI.GetBehaviorAtIndex(index);
                hasBoarding |= _boardingBehaviorType.IsInstanceOfType(
                    behavior);
                hasSkirmish |= _skirmishBehaviorType.IsInstanceOfType(
                    behavior);
                hasRamming |= _rammingBehaviorType.IsInstanceOfType(
                    behavior);
            }

            if (hasBoarding)
            {
                object boarding = SetBehaviorWeight(
                    formation.AI,
                    _boardingBehaviorType,
                    25f);
                Invoke(
                    boarding,
                    "SetTargetShipSideAndOrder",
                    true,
                    0);
            }
            if (hasSkirmish)
            {
                SetBehaviorWeight(
                    formation.AI,
                    _skirmishBehaviorType,
                    0f);
            }
            if (hasRamming)
            {
                SetBehaviorWeight(
                    formation.AI,
                    _rammingBehaviorType,
                    0f);
            }
        }

        private static object SetBehaviorWeight(
            object formationAi,
            Type behaviorType,
            float weight)
        {
            MethodInfo setter = formationAi.GetType().GetMethods(
                    BindingFlags.Instance | BindingFlags.Public)
                .First(method =>
                    method.Name == "SetBehaviorWeight" &&
                    method.IsGenericMethodDefinition &&
                    method.GetParameters().Length == 1);
            return setter.MakeGenericMethod(behaviorType)
                .Invoke(formationAi, new object[] { weight });
        }

        private static object GetProperty(object target, string name)
        {
            return target?.GetType().GetProperty(
                    name,
                    BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(target);
        }

        private static bool GetBool(object target, string name)
        {
            return GetProperty(target, name) is bool value && value;
        }

        private static void Invoke(
            object target,
            string methodName,
            params object[] arguments)
        {
            if (target == null)
            {
                return;
            }
            MethodInfo method = target.GetType().GetMethods(
                    BindingFlags.Instance | BindingFlags.Public)
                .First(candidate =>
                    candidate.Name == methodName &&
                    candidate.GetParameters().Length == arguments.Length);
            method.Invoke(target, arguments);
        }

        private static int GetShipIndex(object ship)
        {
            object value = GetProperty(ship, "Index");
            return value is int index ? index : -1;
        }

        private static void OnShipHookThrown<T>(T source, T target)
        {
            Trace(
                "DikozemieOnline: naval autoboarding hook thrown source=" +
                GetShipIndex(source) + " target=" + GetShipIndex(target));
        }

        private static void OnShipsConnected<T>(T source, T target)
        {
            Trace(
                "DikozemieOnline: naval autoboarding ships connected " +
                "source=" + GetShipIndex(source) +
                " target=" + GetShipIndex(target));
        }

        private static void OnBridgeConnected<T>(T source, T target)
        {
            ForceCrewCharge(source, target);
            Trace(
                "DikozemieOnline: naval autoboarding bridge connected " +
                "source=" + GetShipIndex(source) +
                " target=" + GetShipIndex(target));
        }

        private static void ForceCrewCharge<T>(T source, T target)
        {
            int releasedFromMachines = 0;
            int chargedFormations = 0;
            foreach (object ship in new object[] { source, target })
            {
                int shipIndex = GetShipIndex(ship);
                if (!CrewChargedShipIndices.Add(shipIndex))
                {
                    continue;
                }

                Formation formation = GetProperty(
                    ship,
                    "Formation") as Formation;
                if (formation != null)
                {
                    if (!formation.IsAIControlled)
                    {
                        formation.SetControlledByAI(true, false);
                    }
                    formation.SetMovementOrder(
                        MovementOrder.MovementOrderCharge);
                    formation.SetFiringOrder(
                        FiringOrder.FiringOrderFireAtWill);
                    chargedFormations++;
                }

                releasedFromMachines += ReleaseShipCrewFromMachines(ship);
            }

            Trace(
                "DikozemieOnline: naval crew charge released ships=" +
                chargedFormations + " agentsFromMachines=" +
                releasedFromMachines);
        }

        private static void ReassertBoardingCharge(
            IReadOnlyList<object> ships)
        {
            if (ships == null || CrewChargedShipIndices.Count == 0)
            {
                return;
            }

            foreach (object ship in ships)
            {
                int shipIndex = GetShipIndex(ship);
                if (!CrewChargedShipIndices.Contains(shipIndex))
                {
                    continue;
                }

                // Native ship AI may immediately put surviving fighters back
                // on oars after a bridge is connected.  From this point on the
                // ships are already boarded, so keep every survivor detached
                // from machines and available to finish the melee.
                ReleaseShipCrewFromMachines(ship);

                Formation formation = GetProperty(
                    ship,
                    "Formation") as Formation;
                if (formation == null)
                {
                    continue;
                }
                if (!formation.IsAIControlled)
                {
                    formation.SetControlledByAI(true, false);
                }
                formation.SetMovementOrder(
                    MovementOrder.MovementOrderCharge);
                formation.SetFiringOrder(
                    FiringOrder.FiringOrderFireAtWill);
            }
        }

        private static int ReleaseShipCrewFromMachines(object ship)
        {
            if (_agentsLogic == null || ship == null)
            {
                return 0;
            }

            MethodInfo getActiveAgents = _agentsLogicType.GetMethod(
                "GetActiveAgentsOfShip",
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo releaseAgent = _agentsLogicType.GetMethod(
                "TryStopMachineUseAndReattachAgent",
                BindingFlags.Static | BindingFlags.NonPublic |
                    BindingFlags.Public);
            IEnumerable activeAgents = getActiveAgents?.Invoke(
                _agentsLogic,
                new[] { ship }) as IEnumerable;
            if (activeAgents == null || releaseAgent == null)
            {
                return 0;
            }

            int released = 0;
            foreach (object item in activeAgents)
            {
                if (!(item is Agent agent) || !agent.IsActive() ||
                    agent.IsMainAgent)
                {
                    continue;
                }
                if (agent.InteractingWithAnyGameObject())
                {
                    released++;
                }
                releaseAgent.Invoke(null, new object[] { agent });
            }
            return released;
        }

        private static void OnBoardingOrdered<T>(T source, T target)
        {
            Trace(
                "DikozemieOnline: naval autoboarding order source=" +
                GetShipIndex(source) + " target=" + GetShipIndex(target));
        }

        private static void OnMissionEnded()
        {
            RecordNavalProgress();
            DikozemieNavalResultOverlayController.Hide();
            DikozemieNavalBattleLauncher.NotifyNativeMissionEnded();
            ResetSession("mission ended");
        }

        private static void OnNavalAgentRemoved(
            Agent victim,
            Agent killer,
            AgentState state,
            KillingBlow blow)
        {
            bool attributedToHero =
                ReferenceEquals(killer, _navalHeroAgent) ||
                ReferenceEquals(killer, _mission?.MainAgent);
            if (attributedToHero)
            {
                _heroAttributedRemovals++;
            }

            if (!_requested || _mission == null ||
                victim == null || killer == null || victim == killer ||
                victim.IsHuman != true ||
                blow.DamageType == DamageTypes.Invalid ||
                (state != AgentState.Unconscious &&
                    state != AgentState.Killed) ||
                (!ReferenceEquals(killer, _navalHeroAgent) &&
                    !ReferenceEquals(killer, _mission.MainAgent)) ||
                !killer.IsEnemyOf(victim) ||
                !RewardedNavalVictims.Add(victim))
            {
                return;
            }

            _personalKills++;
            Trace(
                "DikozemieOnline: naval personal kill victim=" +
                (victim.Character?.StringId ?? "unknown") +
                " multiplier=2 ratingReward=2 coinReward=2 kills=" +
                _personalKills);
            InformationManager.DisplayMessage(new InformationMessage(
                "Личное убийство: +2 рейтинга, +2 монеты.",
                Colors.Yellow));
        }

        private static bool TryAutoFinishCompletedMission(
            Mission mission,
            DateTimeOffset now)
        {
            if (mission?.MissionResult == null)
            {
                return false;
            }

            bool finished = mission.MissionResult.PlayerVictory ||
                mission.MissionResult.PlayerDefeated;
            if (!finished)
            {
                return false;
            }

            if (_resultObservedAt == default)
            {
                _resultObservedAt = now;
                TraceArenaRosterAudit(mission, final: true);
                RecordNavalProgress();
                DikozemieNavalResultOverlayController.Show(
                    _navalResultSummary,
                    now);
                Trace(
                    "DikozemieOnline: naval result menu visible victory=" +
                    mission.MissionResult.PlayerVictory +
                    " experience=" +
                    (_navalResultSummary?.ExperienceEarned ?? 0) +
                    " ratingDelta=" +
                    (_navalResultSummary?.RatingEarned ?? 0) +
                    " coinDelta=" +
                    (_navalResultSummary?.GoldEarned ?? 0));
            }

            if (!_resultAcknowledged)
            {
                if (_smokeResultAcknowledge &&
                    now - _resultObservedAt >= TimeSpan.FromSeconds(6d))
                {
                    DikozemieNavalResultOverlayController
                        .ContinueForSmoke();
                }
                if (!DikozemieNavalResultOverlayController.Tick(now))
                {
                    return true;
                }

                _resultAcknowledged = true;
                DikozemieNavalResultOverlayController.Hide();
                Trace(
                    "DikozemieOnline: naval result acknowledged" +
                    (_smokeResultAcknowledge
                        ? " smokeAuto=true"
                        : " playerInput=true"));
            }

            if (_missionExitRequested)
            {
                return true;
            }

            _missionExitRequested = true;
            DikozemieNavalBattleLauncher.NotifyNativeMissionEnded();
            Trace(
                "DikozemieOnline: naval result exit requested");
            mission.EndMission();
            return true;
        }

        private static DikozemieNavalResultSummary RecordNavalProgress()
        {
            if (_progressRecorded)
            {
                return _navalResultSummary;
            }
            if (_mission?.MissionResult == null)
            {
                Trace(
                    "DikozemieOnline: naval progression skipped " +
                    "reason=missing mission result");
                return null;
            }

            bool playerVictory = _mission.MissionResult.PlayerVictory;
            if (!playerVictory && !_mission.MissionResult.PlayerDefeated)
            {
                Trace(
                    "DikozemieOnline: naval progression skipped " +
                    "reason=draw or unfinished result");
                return null;
            }

            DikozemieHeroBattleProgress battleProgress =
                DikozemieHeroProfile.RecordBattleProgress(playerVictory);
            int ratingBefore = DikozemieArmoryProgression.PlayerRating;
            int goldBefore = DikozemieArmoryProgression.HeroResources;
            DikozemieNavalBattleDefinition battle =
                DikozemieNavalBattleCatalog.Find(_hullId);
            string result = DikozemieArmoryProgression.RecordNavalResult(
                playerVictory,
                _personalKills,
                battle?.ShipClass ?? DikozemieNavalShipClass.Light,
                battleProgress.Summary);
            int ratingAfter = DikozemieArmoryProgression.PlayerRating;
            int goldAfter = DikozemieArmoryProgression.HeroResources;
            _navalResultSummary = new DikozemieNavalResultSummary(
                playerVictory,
                battle?.DisplayName ?? _hullId,
                _personalKills,
                battleProgress.ExperienceEarned,
                ratingAfter - ratingBefore,
                goldAfter - goldBefore,
                ratingAfter,
                goldAfter,
                battleProgress.Summary);
            _progressRecorded = true;
            Trace(
                "DikozemieOnline: naval progression applied victory=" +
                playerVictory + " rating=" +
                DikozemieArmoryProgression.PlayerRating + " coins=" +
                DikozemieArmoryProgression.HeroResources +
                " personalKills=" + _personalKills +
                " killTrackerCallbacks=" + _killTrackerCallbacks +
                " heroAttributedRemovals=" + _heroAttributedRemovals +
                " result=" +
                result.Replace("\r", string.Empty).Replace("\n", " | "));
            return _navalResultSummary;
        }

        private static void ResetSession(string reason)
        {
            if (_shipsLogic != null)
            {
                _hookEvent?.RemoveEventHandler(_shipsLogic, _hookHandler);
                _connectedEvent?.RemoveEventHandler(
                    _shipsLogic,
                    _connectedHandler);
                _bridgeConnectedEvent?.RemoveEventHandler(
                    _shipsLogic,
                    _bridgeConnectedHandler);
                _boardingEvent?.RemoveEventHandler(
                    _shipsLogic,
                    _boardingHandler);
                _missionEndEvent?.RemoveEventHandler(
                    _shipsLogic,
                    _missionEndHandler);
            }

            if (_requested || _missionObserved)
            {
                Trace(
                    "DikozemieOnline: naval autoboarding reset reason=" +
                    reason);
            }

            DikozemieNavalResultOverlayController.Hide();

            _requested = false;
            _missionObserved = false;
            _readyMessageShown = false;
            _initialSpacingApplied = false;
            _playerProfileApplied = false;
            _rosterAuditLogged = false;
            _hullId = string.Empty;
            _rosterSummary = string.Empty;
            _targetStartDistance = 0f;
            _crewCapacity = 0;
            _safetySampleIndex = 0;
            CrewChargedShipIndices.Clear();
            SwimmerSince.Clear();
            ObservedRuntimeAgents.Clear();
            ExpectedRuntimeCharacterCounts.Clear();
            ObservedRuntimeCharacterCounts.Clear();
            _requestedAt = default;
            _nextApplyAt = default;
            _nextFailureLogAt = default;
            _nextSafetyLogAt = default;
            _resultObservedAt = default;
            _progressRecorded = false;
            _missionExitRequested = false;
            _resultAcknowledged = false;
            _smokeResultAcknowledge = false;
            _navalResultSummary = null;
            _killTrackerCallbacks = 0;
            _heroAttributedRemovals = 0;
            _personalKills = 0;
            _mission = null;
            _navalHeroAgent = null;
            _shipsLogic = null;
            _agentsLogic = null;
            _hookEvent = null;
            _connectedEvent = null;
            _bridgeConnectedEvent = null;
            _boardingEvent = null;
            _missionEndEvent = null;
            _hookHandler = null;
            _connectedHandler = null;
            _bridgeConnectedHandler = null;
            _boardingHandler = null;
            _missionEndHandler = null;
            _personalKillTracker = null;
            RewardedNavalVictims.Clear();
        }

        private sealed class NavalPersonalKillTracker : MissionLogic
        {
            public override void OnAgentRemoved(
                Agent victim,
                Agent killer,
                AgentState state,
                KillingBlow blow)
            {
                base.OnAgentRemoved(victim, killer, state, blow);
                _killTrackerCallbacks++;
                OnNavalAgentRemoved(victim, killer, state, blow);
            }
        }

        private static void Trace(string message)
        {
            Console.WriteLine(message);
            try
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "Dikozemie");
                Directory.CreateDirectory(directory);
                File.AppendAllText(
                    Path.Combine(directory, SmokeLogName),
                    DateTimeOffset.Now.ToString("O") + " " + message +
                    Environment.NewLine);
            }
            catch
            {
                // Diagnostics must never interfere with the naval mission.
            }
        }
    }
}
