using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    /// <summary>
    /// Dedicated-server-safe naval encounter.  The official War Sails mission
    /// code is single-player-only and is not shipped with Bannerlord Dedicated
    /// Server, so this behavior synchronizes a deterministic approach and
    /// boarding phase using Native scene entities and ordinary MP agents.
    /// </summary>
    public sealed class DikozemieNavalBoardingBehavior : MissionLogic
    {
        private const int BridgeCount = 4;
        // The fishnets hung across the player's view on the real hulls.
        private const int BoardingNetCount = 0;
        private const string BoardingNetSourcePrefab =
            "longboat_climber_nested";
        private const string BoardingNetMesh = "fishnet_d";
        // GameEntity.Instantiate expects a prefab, not a raw bo_* physics
        // resource.  Prefer Native platform prefabs because the dedicated
        // server always loads them; verify every candidate by ray-casting its
        // physical body before using it as a walking deck.
        private static readonly string[] DeckSupportPrefabs =
        {
            "wooden_platform_a",
            "wooden_platform_2_a",
            "wooden_platform_plank_a"
        };
        private const string FallbackColliderPrefab = "wooden_platform_a";
        private const string DeckNavigationPrefab = "dikozemie_deck_nav";
        private const float DeckSupportHalfWidth = 3.25f;
        private const float DeckSupportHalfLength = 10.5f;
        private const float SafetyFloorHalfWidth = 72f;
        private const float SafetyFloorHalfLength = 110f;
        // Alongside: hooks fly, bite, the crews haul the ships together, planks go down.
        private const float HookFlightSeconds = 0.95f;
        private const float HookStagger = 0.14f;
        private const float HaulStart = 1.9f;
        private const float HaulSeconds = 2.3f;
        private const float HookReleaseTime = HaulStart + HaulSeconds;
        private const float DockedHookReleaseTime = 1.6f;
        private const float BridgeDeployDuration = 1.0f;
        private const float WaterRescueInterval = 0.20f;
        // Below this nobody stands on a deck any more: every deck is above water.
        private const float WaterSurfaceZ =
            DikozemieNavalHullMetrics.WaterLevel - 1f;
        private const float BridgeSpan = 8.5f;
        // War Sails' own 2.9 needs a deck that rides the waves; a fixed deck
        // stays dry up to this.
        private const float SeaStrength = 1.6f;
        private const int GrappleCount = 6;
        private const string GrappleHookMesh = "hook";
        private const string GrappleRopePrefab = "simple_rope_nested";
        private const float HookScale = 1f;
        private const float HookThrowStart = 0.2f;
        private const float HookRailHeight = 1f;
        private const float HookGravity = 9.8f;
        // Held crews stand still and watch the other ship.
        private const float CrewBearingInterval = 0.3f;

        internal static DikozemieNavalBoardingBehavior Active { get; private set; }

        private readonly GameEntity[] _bridges = new GameEntity[BridgeCount];
        private readonly MatrixFrame[] _bridgeFrames = new MatrixFrame[BridgeCount];
        private readonly GameEntity[] _boardingNets =
            new GameEntity[BoardingNetCount];
        private readonly MatrixFrame[] _boardingNetFrames =
            new MatrixFrame[BoardingNetCount];
        private readonly GameEntity[] _deckSupports = new GameEntity[2];
        private readonly bool[] _deckSupportSurfaceReady = new bool[2];
        private readonly float[] _deckSupportSurfaceOffsets =
            { 0.12f, 0.12f };
        private readonly string[] _deckSupportResources = new string[2];
        private readonly GameEntity[] _deckNavigationOwners =
            new GameEntity[2];
        private readonly int[] _deckNavigationIds = { -1, -1 };
        private readonly HashSet<DikozemieNavalHullMetrics> _deckNavigationStations =
            new HashSet<DikozemieNavalHullMetrics>();
        private readonly HashSet<Agent> _shipBoundCrew =
            new HashSet<Agent>();
        // What players see: the War Sails hull, drawn on clients only. The
        // server has no NavalDLC, so crews walk on invisible decks that both
        // sides create; the hull has no collision of its own.
        private readonly GameEntity[] _hullVisuals = new GameEntity[2];
        // Where each ship stands; the waves rock it around this point.
        private readonly Vec3[] _hullAnchors = new Vec3[2];
        private readonly float[] _hullHeave = new float[2];
        private readonly float[] _hullPitch = new float[2];
        private readonly float[] _hullRoll = new float[2];
        // War Sails' record that keeps the sea out of a hull.
        private readonly UIntPtr[] _hullWaterRecords = new UIntPtr[2];
        private readonly bool[] _hullWaterFramesSet = new bool[2];
        private float _seaLitterCheck;
        private string _hullVisualMesh;
        private int _hullIndex;
        private GameEntity[] _grappleEntities;
        private readonly bool[] _hookThrown = new bool[GrappleCount];
        private readonly bool[] _hookBitten = new bool[GrappleCount];
        private bool _startedDocked;
        private float _crewBearingElapsed;
        private float _crewPoseElapsed;
        private bool _crewPoseHeld;
        private float _crewOffsetY;
        private bool _crewOffsetKnown;
        private bool _crewStandCleared;
        private DikozemieNavalHullMetrics _hull =
            DikozemieNavalHullMetrics.Resolve(null);
        private GameEntity _attackerShip;
        private GameEntity _defenderShip;
        private MatrixFrame _attackerBaseFrame;
        private MatrixFrame _defenderBaseFrame;
        private DikozemieNavalBoardingLayout _layout =
            DikozemieNavalBoardingLayout.Resolve(
                (int)DikozemieNavalShipClass.Light);
        private DikozemieNavalShipClass _shipClass =
            DikozemieNavalShipClass.Light;
        private DikozemieNavalBoardingPhase _phase =
            DikozemieNavalBoardingPhase.Prepare;
        private float _approachElapsed;
        private float _boardingElapsed;
        private float _waterRescueElapsed;
        private float _attackerX;
        private float _attackerY;
        private float _defenderX;
        private float _defenderY;
        private float _attackerDeckHeight;
        private float _defenderDeckHeight;
        private bool _sceneReady;
        private bool _prepared;
        private bool _hookReadied;
        private bool _hookReleased;
        private bool _boardingWaveStarted;
        private bool _approachCaptureTaken;
        private bool _dockedCaptureTaken;
        private bool _boardingCaptureTaken;
        private bool _bindingDiagnosticsLogged;
        private bool _crewShipAttachmentsReleased;
        private bool _deckNavigationReady;
        private bool _deckColliderSurfaceReady;
        private bool _isNavalScene;
        private int _deckPlacementDiagnosticsRemaining = 4;

        internal DikozemieNavalBoardingLayout CurrentLayout => _layout;
        internal DikozemieNavalHullMetrics CurrentHull => _hull;

        internal bool CrewsMayFight =>
            _phase == DikozemieNavalBoardingPhase.Boarded &&
            _boardingWaveStarted;

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            _isNavalScene = DikozemieArenaSceneRules.IsNavalBoardingScene(
                MultiplayerOptions.OptionType.Map.GetStrValue());
            if (!_isNavalScene)
            {
                return;
            }

            Active = this;
            Mission.MissionTeamAIType =
                TaleWorlds.MountAndBlade.Mission
                    .MissionTeamAITypeEnum.NavalBattle;
            MissionMultiplayerDikozemieArena.Trace(
                "naval mission mode enabled before crew spawn");
        }

        public override void AfterStart()
        {
            base.AfterStart();
            if (!_isNavalScene)
            {
                return;
            }

            if (!TryBindSceneEntities())
            {
                MissionMultiplayerDikozemieArena.Trace(
                    "naval scene is waiting for synchronized ship entities");
                return;
            }
            Prepare(DikozemieNavalShipClass.Light, broadcast: false);
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (!_isNavalScene)
            {
                return;
            }
            if (!GameNetwork.IsServer)
            {
                TickNaval(dt);
                return;
            }
            long started = DikozemieServerPerf.Start();
            DikozemieServerPerf.Crumb("naval tick");
            try
            {
                TickNaval(dt);
            }
            finally
            {
                DikozemieServerPerf.Stop(DikozemieServerPerf.Section.NavalTick, started);
                DikozemieServerPerf.Crumb("naval tick done");
            }
        }

        private void TickNaval(float dt)
        {

            if (!_sceneReady && !TryBindSceneEntities())
            {
                TickUnboundClientCapture(dt);
                return;
            }
            if (!_prepared)
            {
                return;
            }

            if (_phase == DikozemieNavalBoardingPhase.Approach)
            {
                TickApproach(dt);
            }
            else if (_phase == DikozemieNavalBoardingPhase.Boarded)
            {
                TickBoarding(dt);
            }
            KeepCombatantsOnDeck();
            if (!GameNetwork.IsServer)
            {
                TickShipsOnWaves(dt);
                HideLitterOverTheSea(dt);
                TickRidingCrewPose(dt);
            }

            if (GameNetwork.IsServer)
            {
                TickHeldCrewBearing(dt);
                _waterRescueElapsed += Math.Max(0f, dt);
                if (_waterRescueElapsed >= WaterRescueInterval)
                {
                    _waterRescueElapsed = 0f;
                    RescueFallenCombatants();
                }
                MaintainSafetyDeckContainment();
            }
        }

        private void TickHeldCrewBearing(float dt)
        {
            if (_boardingWaveStarted || Mission?.AttackerTeam == null ||
                Mission.DefenderTeam == null)
            {
                return;
            }
            _crewBearingElapsed += Math.Max(0f, dt);
            if (_crewBearingElapsed < CrewBearingInterval)
            {
                return;
            }
            _crewBearingElapsed = 0f;
            _crewStandCleared = false;
            HoldTeamFacing(Mission.AttackerTeam, _defenderX, _defenderY);
            HoldTeamFacing(Mission.DefenderTeam, _attackerX, _attackerY);
        }

        private static void HoldTeamFacing(
            Team team,
            float enemyX,
            float enemyY)
        {
            foreach (Agent agent in team.ActiveAgents.ToArray())
            {
                if (agent == null || agent.State != AgentState.Active ||
                    !agent.IsHuman || agent.MissionPeer != null)
                {
                    continue;
                }
                Vec2 toEnemy = new Vec2(
                    enemyX - agent.Position.x,
                    enemyY - agent.Position.y);
                if (toEnemy.Normalize() > 0.01f)
                {
                    agent.LookDirection = toEnemy.ToVec3();
                    agent.IsLookDirectionLocked = true;
                    agent.SetMovementDirection(in toEnemy);
                }
            }
        }

        // The stand action ends with the wait: fighters go back to walking.
        private void ReleaseHeldCrewStand()
        {
            if (_crewStandCleared)
            {
                return;
            }
            _crewStandCleared = true;
            foreach (Team team in new[] { Mission.AttackerTeam, Mission.DefenderTeam })
            {
                foreach (Agent agent in team.ActiveAgents.ToArray())
                {
                    if (agent != null && agent.State == AgentState.Active &&
                        agent.IsHuman && agent.MissionPeer == null)
                    {
                        agent.IsLookDirectionLocked = false;
                    }
                }
            }
        }

        // Neither side can collide with a War Sails hull: the server has no
        // NavalDLC. Everybody stands on the scene's deck plane, so what keeps a
        // fighter on the ship is this limit. The client applies it too: it owns
        // its local agent and ignores small server corrections.
        private void KeepCombatantsOnDeck()
        {
            if (Mission?.AttackerTeam == null || Mission.DefenderTeam == null)
            {
                return;
            }
            if (!GameNetwork.IsServer)
            {
                Agent main = Mission.MainAgent;
                if (main != null && main.State == AgentState.Active &&
                    main.IsHuman && main.MountAgent == null)
                {
                    KeepAgentOnDeck(main, main.Team == Mission.AttackerTeam);
                }
                return;
            }
            KeepTeamOnDeck(Mission.AttackerTeam, attacker: true);
            KeepTeamOnDeck(Mission.DefenderTeam, attacker: false);
        }

        private void KeepTeamOnDeck(Team team, bool attacker)
        {
            foreach (Agent agent in team.ActiveAgents.ToArray())
            {
                if (agent != null && agent.State == AgentState.Active &&
                    agent.IsHuman && agent.MountAgent == null)
                {
                    KeepAgentOnDeck(agent, attacker);
                }
            }
        }

        private void KeepAgentOnDeck(Agent agent, bool attacker)
        {
            Vec3 position = agent.Position;
            if (_boardingWaveStarted)
            {
                // The bridges joined both decks into one walkable area.
                if (IsOnShip(true, position) || IsOnShip(false, position) ||
                    IsOnBridges(position))
                {
                    return;
                }
                attacker = Math.Abs(position.x - _attackerX) <=
                    Math.Abs(position.x - _defenderX);
            }
            else if (IsOnShip(attacker, position))
            {
                return;
            }
            Vec3 kept = ClampToShip(attacker, position);
            agent.TeleportToPosition(new Vec3(kept.x, kept.y, position.z));
        }

        // War Sails hulls point their bow along -Y and the attacker is turned
        // around, so its deck is mirrored in world coordinates.
        private bool IsOnShip(bool attacker, Vec3 position)
        {
            float sign = attacker ? -1f : 1f;
            return _hull.IsOnDeck(
                sign * (position.x - (attacker ? _attackerX : _defenderX)),
                sign * (position.y - (attacker ? _attackerY : _defenderY)));
        }

        private Vec3 ClampToShip(bool attacker, Vec3 position)
        {
            float sign = attacker ? -1f : 1f;
            float shipX = attacker ? _attackerX : _defenderX;
            float shipY = attacker ? _attackerY : _defenderY;
            float x = sign * (position.x - shipX);
            float y = sign * (position.y - shipY);
            _hull.ClampToDeck(ref x, ref y);
            return new Vec3(shipX + (sign * x), shipY + (sign * y), position.z);
        }

        private bool IsOnBridges(Vec3 position)
        {
            float low = Math.Min(_attackerX, _defenderX);
            float high = Math.Max(_attackerX, _defenderX);
            return position.x > low && position.x < high &&
                Math.Abs(position.y - _layout.CenterY) <= BridgeSpan;
        }

        private void TickUnboundClientCapture(float dt)
        {
            if (GameNetwork.IsServer || !_prepared)
            {
                return;
            }
            if (_phase == DikozemieNavalBoardingPhase.Approach)
            {
                _approachElapsed += Math.Max(0f, dt);
                if (!_approachCaptureTaken && _approachElapsed >= 2f)
                {
                    _approachCaptureTaken = true;
                    CaptureDebugFrame("approach");
                }
                return;
            }
            if (_phase != DikozemieNavalBoardingPhase.Boarded)
            {
                return;
            }
            _boardingElapsed += Math.Max(0f, dt);
            if (!_dockedCaptureTaken && _boardingElapsed >= 0.35f)
            {
                _dockedCaptureTaken = true;
                CaptureDebugFrame("hooks-ready");
            }
            if (!_boardingCaptureTaken &&
                _boardingElapsed >=
                    HookReleaseTime + BridgeDeployDuration + 2f)
            {
                _boardingCaptureTaken = true;
                CaptureDebugFrame("boarding");
            }
        }

        // The online sea was silent: our scene has no ambient emitter and the
        // client-side War Sails hulls run without scripts (customer,
        // 2026-11-04: «звука не было в бою»). The open-sea ambience of the War
        // Sails battle scenes plays on the client while the ships are shown.
        private SoundEvent _seaAmbience;
        private bool _boardingHornPlayed;

        private void EnsureSeaAmbience()
        {
            if (GameNetwork.IsServer || _seaAmbience != null || Mission?.Scene == null)
            {
                return;
            }
            try
            {
                _seaAmbience = SoundEvent.CreateEventFromString(
                    "event:/mission/ambient/area/open_sea",
                    Mission.Scene);
                if (_seaAmbience == null)
                {
                    return;
                }
                _seaAmbience.SetParameter("WaveStrength", SeaStrength);
                _seaAmbience.SetPosition(new Vec3(
                    (_attackerX + _defenderX) * 0.5f,
                    _layout.CenterY,
                    4f));
                _seaAmbience.Play();
                MissionMultiplayerDikozemieArena.Trace("naval sea ambience playing");
            }
            catch (Exception error)
            {
                MissionMultiplayerDikozemieArena.Trace(
                    "naval sea ambience failed: " + error.Message);
                _seaAmbience = null;
            }
        }

        private void StopSeaAmbience()
        {
            try
            {
                _seaAmbience?.Stop();
                _seaAmbience?.Release();
            }
            catch
            {
                // Leaving the mission must never fail on a sound.
            }
            _seaAmbience = null;
        }

        public override void OnRemoveBehavior()
        {
            StopSeaAmbience();
            ReleaseHullWaterRecords();
            RestoreCrewGravity();
            if (ReferenceEquals(Active, this))
            {
                Active = null;
            }
            base.OnRemoveBehavior();
        }

        internal void SelectHull(DikozemieNavalBattleDefinition battle)
        {
            int index = 0;
            IReadOnlyList<DikozemieNavalBattleDefinition> all =
                DikozemieNavalBattleCatalog.All;
            for (int candidate = 0; candidate < all.Count; candidate++)
            {
                if (ReferenceEquals(all[candidate], battle))
                {
                    index = candidate;
                    break;
                }
            }
            _hullIndex = index;
        }

        internal void Prepare(
            DikozemieNavalShipClass shipClass,
            bool broadcast)
        {
            _shipClass = shipClass;
            _hull = DikozemieNavalHullMetrics.Resolve(
                DikozemieNavalBattleCatalog.All[_hullIndex].MissionShipId);
            _layout = DikozemieNavalBoardingLayout.Resolve((int)shipClass)
                .ForHull(_hull);
            _phase = DikozemieNavalBoardingPhase.Prepare;
            _approachElapsed = 0f;
            _boardingElapsed = 0f;
            _waterRescueElapsed = 0f;
            _attackerX = _layout.AttackerStartX;
            _attackerY = _layout.AttackerStartY;
            _startedDocked = StartsDocked;
            _defenderX = StartsDocked
                ? _layout.DefenderDockX
                : _layout.DefenderStartX;
            _defenderY = StartsDocked
                ? _layout.DefenderDockY
                : _layout.DefenderStartY;
            _attackerDeckHeight = _layout.DeckHeight;
            _defenderDeckHeight = _layout.DeckHeight;
            _hookReadied = false;
            _hookReleased = false;
            _boardingWaveStarted = false;
            Array.Clear(_hookThrown, 0, GrappleCount);
            _boardingHornPlayed = false;
            Array.Clear(_hookBitten, 0, GrappleCount);
            _crewShipAttachmentsReleased = false;
            _shipBoundCrew.Clear();
            _approachCaptureTaken = false;
            _dockedCaptureTaken = false;
            _boardingCaptureTaken = false;
            _prepared = true;
            if (!GameNetwork.IsServer)
            {
                // Ragdolls are the client's own: last battle's bodies would hang
                // over the sea where the enemy ship stood.
                Mission?.ClearCorpses(false);
            }

            if (_sceneReady)
            {
                EnsureHullVisuals();
                SetShipPositions(
                    _attackerX,
                    _attackerY,
                    _defenderX,
                    _defenderY);
                EnsureDeckSupports();
                SetDeckSupportPositions();
                StowBoardingBridges();
                SetGrapplingVisible(false);
                SetBoardingNetVisibility(false);
            }
            if (broadcast)
            {
                Broadcast(DikozemieNavalBoardingPhase.Prepare);
            }
            MissionMultiplayerDikozemieArena.Trace(
                "naval prepared class=" + shipClass +
                " separation=" +
                DikozemieNavalBoardingLayout.InitialSeparation.ToString("0") +
                "m course=bow-first +Y/-Y dock=right/right deckA=" +
                _attackerDeckHeight.ToString("0.00") +
                " deckD=" + _defenderDeckHeight.ToString("0.00"));
        }

        // A player on each ship: one of them would have to ride a moving deck,
        // so the battle starts with the ships side by side.
        internal bool StartsDocked { get; set; }

        internal void StartApproach(bool broadcast)
        {
            if (!_prepared)
            {
                Prepare(_shipClass, broadcast: false);
            }
            if (StartsDocked)
            {
                CompleteBoarding(broadcast);
                return;
            }
            _phase = DikozemieNavalBoardingPhase.Approach;
            _approachElapsed = 0f;
            _crewOffsetKnown = false;
            if (GameNetwork.IsServer)
            {
                DikozemieArenaSpawningBehavior.Active?
                    .HoldNavalCrewsForBoarding();
                AttachEnemyCrewToDeck();
                TraceEnemyCrewDrift("start");
            }
            if (broadcast)
            {
                Broadcast(DikozemieNavalBoardingPhase.Approach);
            }
            MissionMultiplayerDikozemieArena.Trace(
                "naval approach started class=" + _shipClass +
                " duration=" + _layout.ApproachDuration.ToString("0") + "s");
        }

        internal Vec3 GetDeckPosition(
            bool attacker,
            float acrossOffset,
            float alongOffset)
        {
            float centerX = attacker ? _attackerX : _defenderX;
            float centerY = attacker ? _attackerY : _defenderY;
            float fallbackHeight = attacker
                ? _attackerDeckHeight
                : _defenderDeckHeight;
            Vec3 approximate = new Vec3(
                centerX + acrossOffset,
                centerY + ((attacker ? -1f : 1f) * _hull.DeckCenterY) +
                    alongOffset,
                fallbackHeight);
            return ResolveDeckPosition(attacker, approximate);
        }

        internal Vec3 GetBoardingEdgePosition(
            bool attacker,
            float alongOffset)
        {
            float directionToGap = attacker ? 1f : -1f;
            float inwardEdge = _hull.WalkableHalfWidth * 0.9f;
            return GetDeckPosition(
                attacker,
                directionToGap * inwardEdge,
                alongOffset);
        }

        internal float BridgeDeckHeight =>
            ((_attackerDeckHeight + _defenderDeckHeight) * 0.5f) - 0.18f;

        internal void BeginCrewDeckPlacement(
            bool attacker,
            IReadOnlyList<Agent> crew,
            IReadOnlyList<Vec3> deckPositions)
        {
            // Authored ship entities are ordinary scene geometry, so their
            // decks participate in the same native ground query as terrain.
            // Keep this hook for the spawning transaction, but do not replace
            // the deck with a runtime physics elevator: agents are not rigid
            // bodies and such an elevator passes through them.
            MissionMultiplayerDikozemieArena.Trace(
                "naval authored deck placement ready side=" +
                (attacker ? "attacker" : "defender") +
                " crew=" + (crew?.Count ?? 0) +
                " positions=" + (deckPositions?.Count ?? 0));
        }

        internal void PlaceCrewOnDeck(
            Agent agent,
            bool attacker,
            Vec3 position)
        {
            if (agent == null)
            {
                return;
            }
            Vec3 positionBeforePlacement = agent.Position;
            agent.SetForceAttachedEntity(default(WeakGameEntity));
            agent.SetExcludedFromGravity(false, false);
            agent.TeleportToPosition(position);
            if (_deckPlacementDiagnosticsRemaining > 0)
            {
                _deckPlacementDiagnosticsRemaining--;
                Vec3 navigationProbe = position;
                int faceGroupId = -1;
                UIntPtr navigationMesh =
                    Mission.Scene.GetNavigationMeshForPosition(
                        in navigationProbe,
                        out faceGroupId,
                        6f,
                        false);
                MissionMultiplayerDikozemieArena.Trace(
                    "naval deck placement side=" +
                    (attacker ? "attacker" : "defender") +
                    " before=" + DescribePosition(positionBeforePlacement) +
                    " requested=" + DescribePosition(position) +
                    " after=" + DescribePosition(agent.Position) +
                    " navFound=" + (navigationMesh != UIntPtr.Zero) +
                    " face=" + faceGroupId +
                    " expectedFace=" +
                    _deckNavigationIds[attacker ? 0 : 1]);
            }
        }

        internal void AuditCrewSupport(string stage)
        {
            if (!GameNetwork.IsServer || Mission == null)
            {
                return;
            }
            Agent[] agents = Mission.AttackerTeam.ActiveAgents
                .Concat(Mission.DefenderTeam.ActiveAgents)
                .Where(agent => agent != null &&
                    agent.State == AgentState.Active && agent.IsHuman)
                .ToArray();
            if (agents.Length == 0)
            {
                return;
            }
            int inWater = agents.Count(agent =>
                agent.Position.z <= WaterSurfaceZ + 0.75f);
            int onLand = agents.Count(agent => agent.IsOnLand());
            int onDynamicDeck = agents.Count(agent =>
                Mission.Scene.IsPositionOnADynamicNavMesh(agent.Position));
            int outsideSafetyBounds = agents.Count(agent =>
                Math.Abs(agent.Position.x -
                    _layout.CenterX) >
                        SafetyFloorHalfWidth ||
                Math.Abs(agent.Position.y -
                    _layout.CenterY) >
                        SafetyFloorHalfLength);
            MissionMultiplayerDikozemieArena.Trace(
                "naval support audit stage=" + stage +
                " agents=" + agents.Length +
                " inWater=" + inWater +
                " onLand=" + onLand +
                " onDynamicDeck=" + onDynamicDeck +
                " outsideSafetyBounds=" + outsideSafetyBounds +
                " minZ=" + agents.Min(agent => agent.Position.z).ToString("0.00") +
                " maxZ=" + agents.Max(agent => agent.Position.z).ToString("0.00"));
        }

        internal void ApplyRemoteState(int phase, int shipClass, int hullIndex)
        {
            if (hullIndex >= 0 &&
                hullIndex < DikozemieNavalBattleCatalog.All.Count)
            {
                _hullIndex = hullIndex;
            }
            if (shipClass < (int)DikozemieNavalShipClass.Light ||
                shipClass > (int)DikozemieNavalShipClass.Heavy ||
                phase < (int)DikozemieNavalBoardingPhase.Prepare ||
                phase > (int)DikozemieNavalBoardingPhase.Boarded)
            {
                return;
            }

            var requestedClass = (DikozemieNavalShipClass)shipClass;
            switch ((DikozemieNavalBoardingPhase)phase)
            {
                case DikozemieNavalBoardingPhase.Prepare:
                    Prepare(requestedClass, broadcast: false);
                    break;
                case DikozemieNavalBoardingPhase.Approach:
                    Prepare(requestedClass, broadcast: false);
                    StartApproach(broadcast: false);
                    break;
                case DikozemieNavalBoardingPhase.Boarded:
                    if (_phase == DikozemieNavalBoardingPhase.Boarded)
                    {
                        break;
                    }
                    if (_phase == DikozemieNavalBoardingPhase.Approach)
                    {
                        CompleteBoarding(broadcast: false);
                        break;
                    }
                    Prepare(requestedClass, broadcast: false);
                    _startedDocked = true;
                    CompleteBoarding(broadcast: false);
                    break;
            }
        }

        private void CompleteBoarding(bool broadcast)
        {
            if (_phase == DikozemieNavalBoardingPhase.Boarded)
            {
                return;
            }
            _phase = DikozemieNavalBoardingPhase.Boarded;
            _boardingElapsed = 0f;
            _attackerX = _layout.AttackerDockX;
            _attackerY = _layout.AttackerDockY;
            _defenderX = _startedDocked
                ? _layout.DefenderDockX
                : _layout.DefenderStartX;
            _defenderY = _layout.DefenderDockY;
            SetShipPositions(
                _attackerX,
                _attackerY,
                _defenderX,
                _defenderY,
                isTeleportation: _startedDocked);
            EnsureDeckSupports();
            StowBoardingBridges();
            SetBoardingNetVisibility(true);
            ReadyGrapplingHooks();
            if (GameNetwork.IsServer)
            {
                TraceEnemyCrewDrift("alongside");
                EnsureDeckNavigation();
                DikozemieArenaSpawningBehavior.Active?
                    .HoldNavalCrewsForBoarding();
            }
            if (broadcast)
            {
                Broadcast(DikozemieNavalBoardingPhase.Boarded);
            }
            MissionMultiplayerDikozemieArena.Trace(
                "naval ships docked class=" + _shipClass +
                " separation=" + _layout.DockedSeparation.ToString("0.0") +
                "m; grappling hooks ready");
            AuditCrewSupport("docked");
        }

        private void TickApproach(float dt)
        {
            _approachElapsed = Math.Min(
                _layout.ApproachDuration,
                _approachElapsed + Math.Max(0f, dt));
            float linear = _layout.ApproachDuration <= 0f
                ? 1f
                : _approachElapsed / _layout.ApproachDuration;
            float eased = linear * linear * (3f - (2f * linear));
            float nextDefenderY = Lerp(
                _layout.DefenderStartY,
                _layout.DefenderDockY,
                eased);
            nextDefenderY = FollowEnemyCrew(nextDefenderY, dt);
            float defenderDeltaY = nextDefenderY - _defenderY;
            _defenderY = nextDefenderY;
            SetShipPositions(
                _attackerX,
                _attackerY,
                _defenderX,
                _defenderY,
                isTeleportation: false);
            if (GameNetwork.IsServer)
            {
                CarryEnemyCrew(0f, defenderDeltaY);
            }
            if (!_approachCaptureTaken && _approachElapsed >= 2f)
            {
                _approachCaptureTaken = true;
                CaptureDebugFrame("approach");
                AuditCrewSupport("approach");
            }
            if (linear >= 1f)
            {
                CompleteBoarding(broadcast: GameNetwork.IsServer);
            }
        }

        // Fighters reach a client later than its own clock moves the hull, and
        // they slide along the deck and off the bow. The drawn hull waits for them.
        private float FollowEnemyCrew(float clockY, float dt)
        {
            if (GameNetwork.IsServer || Mission?.DefenderTeam == null)
            {
                return clockY;
            }
            float sum = 0f;
            int count = 0;
            foreach (Agent agent in Mission.DefenderTeam.ActiveAgents)
            {
                if (agent != null && agent.MissionPeer == null)
                {
                    sum += agent.Position.y;
                    count++;
                }
            }
            if (count == 0)
            {
                return clockY;
            }
            float crewY = sum / count;
            if (!_crewOffsetKnown)
            {
                _crewOffsetKnown = true;
                _crewOffsetY = crewY - _layout.DefenderStartY;
            }
            float followed = crewY - _crewOffsetY;
            followed = Math.Max(clockY, Math.Min(_layout.DefenderStartY, followed));
            return Lerp(_defenderY, followed, Math.Min(1f, dt * 12f));
        }

        // Attachment does not carry a fighter by itself, it only lets him stand:
        // detached, each move reads as running on the spot. Attached and moved
        // with the ship, he keeps his place on the deck and his stance.
        private void CarryEnemyCrew(float deltaX, float deltaY)
        {
            Team team = Mission.DefenderTeam;
            if (team == null)
            {
                return;
            }
            foreach (Agent agent in team.ActiveAgents.ToArray())
            {
                if (agent == null || agent.State != AgentState.Active ||
                    !agent.IsHuman || agent.MountAgent != null ||
                    agent.MissionPeer != null)
                {
                    continue;
                }
                Vec3 position = agent.Position;
                position.x += deltaX;
                position.y += deltaY;
                agent.TeleportToPosition(position);
            }
        }

        private void AttachEnemyCrewToDeck()
        {
            GameEntity deck = _deckSupports[1];
            Team team = Mission?.DefenderTeam;
            if (deck == null || team == null)
            {
                return;
            }
            int attached = 0;
            foreach (Agent agent in team.ActiveAgents.ToArray())
            {
                if (agent != null && agent.State == AgentState.Active &&
                    agent.IsHuman && agent.MissionPeer == null &&
                    agent.MountAgent == null)
                {
                    agent.SetForceAttachedEntity(deck.WeakEntity);
                    _shipBoundCrew.Add(agent);
                    attached++;
                }
            }
            MissionMultiplayerDikozemieArena.Trace(
                "naval enemy crew attached to deck count=" + attached);
        }

        private void TraceEnemyCrewDrift(string stage)
        {
            Agent sample = Mission?.DefenderTeam?.ActiveAgents
                .FirstOrDefault(agent => agent.MissionPeer == null);
            if (sample != null)
            {
                MissionMultiplayerDikozemieArena.Trace(
                    "naval enemy crew " + stage + " sample=" +
                    DescribePosition(sample.Position) + " ship=" +
                    _defenderX.ToString("0.0") + "," + _defenderY.ToString("0.0"));
            }
        }

        private void TickBoarding(float dt)
        {
            _boardingElapsed += Math.Max(0f, dt);
            TickGrapplingVisuals();
            if (!_dockedCaptureTaken && _boardingElapsed >= 0.35f)
            {
                _dockedCaptureTaken = true;
                CaptureDebugFrame("hooks-ready");
            }
            if (!_hookReleased && _boardingElapsed >= HookThrowStart)
            {
                _hookReleased = true;
                ReleaseGrapplingHooks();
                MissionMultiplayerDikozemieArena.Trace(
                    "naval grappling hooks released");
                // The crews roar as the hooks fly.
                if (GameNetwork.IsServer && Mission != null)
                {
                    DikozemieBattleCryBehavior.QueueBotYells(
                        Mission.Agents, 0.4f, 1.5f);
                }
            }
            TickHaul();

            float bridgesAt = _startedDocked
                ? DockedHookReleaseTime
                : HookReleaseTime;
            float bridgeProgress = Math.Max(
                0f,
                Math.Min(
                    1f,
                    (_boardingElapsed - bridgesAt) / BridgeDeployDuration));
            if (bridgeProgress > 0f)
            {
                DeployBoardingBridges(bridgeProgress);
            }
            if (bridgeProgress >= 1f && !_boardingWaveStarted)
            {
                _boardingWaveStarted = true;
                ReleaseCrewShipAttachments();
                if (GameNetwork.IsServer)
                {
                    ReleaseHeldCrewStand();
                    DikozemieArenaSpawningBehavior.Active?.BeginNavalBoarding(
                        _layout);
                }
                MissionMultiplayerDikozemieArena.Trace(
                    "naval boarded class=" + _shipClass +
                    " separation=" + _layout.DockedSeparation.ToString("0.0") +
                    "m bridges=" +
                    _bridges.Count(bridge => bridge != null));
                AuditCrewSupport("bridges-deployed");
            }
            if (GameNetwork.IsServer && _boardingWaveStarted)
            {
                DikozemieArenaSpawningBehavior.Active?.TickNavalBoarding(dt);
            }
            if (!_boardingCaptureTaken &&
                _boardingElapsed >= HookReleaseTime + BridgeDeployDuration + 2f)
            {
                _boardingCaptureTaken = true;
                CaptureDebugFrame("boarding");
                AuditCrewSupport("boarding");
            }
        }

        // Taut ropes draw the enemy ship in, slowly at first and then settling.
        private void TickHaul()
        {
            if (_startedDocked)
            {
                return;
            }
            float linear = Math.Max(
                0f,
                Math.Min(1f, (_boardingElapsed - HaulStart) / HaulSeconds));
            float eased = linear * linear * (3f - (2f * linear));
            float nextX = Lerp(
                _layout.DefenderStartX,
                _layout.DefenderDockX,
                eased);
            float deltaX = nextX - _defenderX;
            if (Math.Abs(deltaX) < 0.0001f)
            {
                return;
            }
            _defenderX = nextX;
            SetShipPositions(
                _attackerX,
                _attackerY,
                _defenderX,
                _defenderY,
                isTeleportation: false);
            if (GameNetwork.IsServer)
            {
                CarryEnemyCrew(deltaX, 0f);
            }
        }

        private bool TryBindSceneEntities()
        {
            if (_sceneReady || Mission?.Scene == null)
            {
                return _sceneReady;
            }
            var rootEntities = new List<GameEntity>();
            Mission.Scene.GetEntities(ref rootEntities);
            var sceneEntities = new List<GameEntity>();
            foreach (GameEntity rootEntity in rootEntities)
            {
                foreach (GameEntity entity in rootEntity.GetEntityAndChildren())
                {
                    if (entity != null && !sceneEntities.Contains(entity))
                    {
                        sceneEntities.Add(entity);
                    }
                }
            }
            // On a multiplayer client, synchronized scene objects are exposed
            // through MissionObjects even when Scene.GetEntities omits them.
            // Without these entities the client animates neither ship, while
            // the server moves the decks and agents normally.
            AddMissionObjectEntities(
                Mission.ActiveMissionObjects,
                sceneEntities);
            AddMissionObjectEntities(Mission.MissionObjects, sceneEntities);
            GameEntity attacker = Mission.Scene.FindEntityWithName(
                "dikozemie_attacker_ship");
            GameEntity defender = Mission.Scene.FindEntityWithName(
                "dikozemie_defender_ship");
            if (attacker == null || defender == null)
            {
                GameEntity[] ships = sceneEntities
                    .Where(entity => entity != null &&
                        string.Equals(
                            entity.GetPrefabName(),
                            "ship_a",
                            StringComparison.Ordinal))
                    .OrderBy(entity => entity.GetGlobalFrame().origin.x)
                    .ToArray();
                if (ships.Length >= 2)
                {
                    attacker = ships[0];
                    defender = ships[ships.Length - 1];
                }
                if (attacker == null || defender == null)
                {
                    // Multiplayer clients keep authored static entities but
                    // can strip their editor names and prefab identifiers.
                    // The two ship origins are unique in this scene, so bind
                    // them by their authored positions as a final fallback.
                    attacker = FindNearestSceneEntity(
                        sceneEntities,
                        new Vec3(
                            DikozemieNavalBoardingLayout.Resolve(0).AttackerStartX,
                            DikozemieNavalBoardingLayout.Resolve(0).AttackerStartY,
                            1f),
                        3f);
                    defender = FindNearestSceneEntity(
                        sceneEntities,
                        new Vec3(
                            DikozemieNavalBoardingLayout.Resolve(0).DefenderStartX,
                            DikozemieNavalBoardingLayout.Resolve(0).DefenderStartY,
                            1f),
                        3f,
                        attacker);
                }
            }
            if (attacker == null || defender == null)
            {
                attacker = attacker ?? CreateRuntimeShip(attacker: true);
                defender = defender ?? CreateRuntimeShip(attacker: false);
            }
            if (attacker == null || defender == null)
            {
                if (!_bindingDiagnosticsLogged)
                {
                    _bindingDiagnosticsLogged = true;
                    MissionMultiplayerDikozemieArena.Trace(
                        "naval binding diagnostics sceneEntities=" +
                        sceneEntities.Count +
                        " activeMissionObjects=" +
                        Mission.ActiveMissionObjects.Count() +
                        " missionObjects=" + Mission.MissionObjects.Count() +
                        " synchronized=" + Mission.MissionObjects.Count(
                            missionObject =>
                                missionObject is SynchedMissionObject));
                }
                return false;
            }

            _attackerShip = attacker;
            _defenderShip = defender;
            RetireSceneHull(_attackerShip);
            RetireSceneHull(_defenderShip);
            _attackerBaseFrame = RemoveScale(
                _attackerShip.GetGlobalFrame());
            _defenderBaseFrame = RemoveScale(
                _defenderShip.GetGlobalFrame());
            EnsureDeckSupports();
            for (int index = 0; index < BridgeCount; index++)
            {
                _bridges[index] = Mission.Scene.FindEntityWithName(
                    "dikozemie_boarding_bridge_" + (index + 1));
            }
            if (_bridges.Any(bridge => bridge == null))
            {
                if (sceneEntities.Count == 0)
                {
                    Mission.Scene.GetEntities(ref sceneEntities);
                }
                GameEntity[] bridgeCandidates = sceneEntities
                    .Where(entity => entity != null &&
                        string.Equals(
                            entity.GetPrefabName(),
                            "wooden_platform_plank_a",
                            StringComparison.Ordinal))
                    .OrderBy(entity => entity.GetGlobalFrame().origin.y)
                    .Take(BridgeCount)
                    .ToArray();
                for (int index = 0;
                    index < Math.Min(BridgeCount, bridgeCandidates.Length);
                    index++)
                {
                    _bridges[index] = bridgeCandidates[index];
                }
            }
            float[] bridgeAlongOffsets = { -7.5f, -2.5f, 2.5f, 7.5f };
            for (int index = 0; index < BridgeCount; index++)
            {
                if (_bridges[index] == null)
                {
                    _bridges[index] = FindNearestSceneEntity(
                        sceneEntities,
                        new Vec3(
                            DikozemieNavalBoardingLayout.SeaCenterX,
                            DikozemieNavalBoardingLayout.SeaCenterY +
                                bridgeAlongOffsets[index],
                            -50f - index),
                        3f,
                        attacker,
                        defender);
                }
                if (_bridges[index] == null)
                {
                    MatrixFrame bridgeFrame = MatrixFrame.Identity;
                    bridgeFrame.rotation.ApplyScaleLocal(
                        new Vec3(6f, 1.25f, 0.5f));
                    bridgeFrame.origin = new Vec3(
                        DikozemieNavalBoardingLayout.SeaCenterX,
                        DikozemieNavalBoardingLayout.SeaCenterY +
                            bridgeAlongOffsets[index],
                        -50f - index);
                    _bridges[index] = GameEntity.Instantiate(
                        Mission.Scene,
                        "wooden_platform_plank_a",
                        bridgeFrame,
                        true);
                }
            }
            for (int index = 0; index < BridgeCount; index++)
            {
                if (_bridges[index] != null)
                {
                    _bridgeFrames[index] = RemoveScale(
                        _bridges[index].GetGlobalFrame());
                }
            }
            // This is the same fishnet climbing machine used by the NavalDLC
            // longboat.  It is client-side decoration here; the dedicated
            // server has no NavalDLC assets and keeps authoritative recovery
            // in RescueFallenCombatants.  Never substitute a wooden ladder.
            // longboat_climber_nested is an internal packed prefab, so the
            // multiplayer engine does not expose it through PrefabExists even
            // after War Sails resources are loaded. Build its exact fishnet_d
            // visual directly; the server keeps recovery authoritative.
            MetaMesh nativeBoardingNet = !GameNetwork.IsServer
                ? MetaMesh.GetCopy(
                    BoardingNetMesh,
                    showErrors: false,
                    mayReturnNull: true)
                : null;
            for (int index = 0; index < BoardingNetCount; index++)
            {
                if (nativeBoardingNet == null)
                {
                    continue;
                }
                _boardingNets[index] = GameEntity.CreateEmpty(
                    Mission.Scene,
                    isModifiableFromEditor: false,
                    createPhysics: false,
                    callScriptCallbacks: false);
                if (_boardingNets[index] != null)
                {
                    _boardingNets[index].AddMultiMesh(
                        index == 0
                            ? nativeBoardingNet
                            : MetaMesh.GetCopy(
                                BoardingNetMesh,
                                showErrors: false,
                                mayReturnNull: true));
                    _boardingNetFrames[index] = MatrixFrame.Identity;
                }
            }
        _sceneReady = true;
        if (_prepared)
        {
            EnsureHullVisuals();
            SetShipPositions(
                    _attackerX,
                    _attackerY,
                    _defenderX,
                    _defenderY);
                EnsureDeckSupports();
                SetDeckSupportPositions();
                StowBoardingBridges();
                SetBoardingNetVisibility(
                    _phase == DikozemieNavalBoardingPhase.Boarded);
            }
            MissionMultiplayerDikozemieArena.Trace(
                "naval synchronized entities bound ships=2 bridges=" +
                _bridges.Count(bridge => bridge != null) +
                " nets=" + _boardingNets.Count(net => net != null) +
                " nativeNetMesh=" + (nativeBoardingNet != null) +
                " source=" + BoardingNetSourcePrefab +
                " headings=+Y/-Y gapBoards=right/right");
            return true;
        }

        // The scene's ship_a stays only as the owner of the deck navigation.
        private static void RetireSceneHull(GameEntity ship)
        {
            var parts = new List<GameEntity> { ship };
            ship.GetChildrenRecursive(ref parts);
            foreach (GameEntity part in parts)
            {
                part.SetVisibilityExcludeParents(false);
                part.SetPhysicsState(false, false);
            }
        }

        private void EnsureHullVisuals()
        {
            if (GameNetwork.IsServer || Mission?.Scene == null)
            {
                return;
            }
            string mesh = _hull.Mesh;
            if (string.Equals(mesh, _hullVisualMesh, StringComparison.Ordinal))
            {
                return;
            }
            ReleaseHullWaterRecords();
            foreach (GameEntity old in _hullVisuals)
            {
                old?.Remove(0);
            }
            _hullVisualMesh = mesh;
            Mission.Scene.SetWaterStrength(SeaStrength);
            string prefab = DikozemieNavalHullMetrics.PrefabFor(
                DikozemieNavalBattleCatalog.All[_hullIndex].MissionShipId);
            for (int index = 0; index < _hullVisuals.Length; index++)
            {
                GameEntity ship = TryCreateShipVisual(prefab);
                if (ship != null)
                {
                    _hullVisuals[index] = ship;
                    RegisterHullWithSea(index, ship);
                    continue;
                }
                MetaMesh hull = MetaMesh.GetCopy(
                    mesh,
                    showErrors: false,
                    mayReturnNull: true);
                if (hull == null)
                {
                    MissionMultiplayerDikozemieArena.Trace(
                        "naval hull mesh unavailable mesh=" + mesh);
                    _hullVisuals[index] = null;
                    continue;
                }
                _hullVisuals[index] = GameEntity.CreateEmpty(
                    Mission.Scene,
                    isModifiableFromEditor: false,
                    createPhysics: false,
                    callScriptCallbacks: false);
                _hullVisuals[index]?.AddMultiMesh(hull);
            }
            EnsureSeaAmbience();
        }

        // The whole War Sails ship: mast, rigging, oars and shields. No physics
        // and no scripts: the server knows nothing about it, it is only drawn.
        private GameEntity TryCreateShipVisual(string prefab)
        {
            if (string.IsNullOrEmpty(prefab))
            {
                return null;
            }
            try
            {
                GameEntity ship = GameEntity.Instantiate(
                    Mission.Scene, prefab, false, false, string.Empty);
                if (ship == null)
                {
                    MissionMultiplayerDikozemieArena.Trace(
                        "naval ship prefab missing prefab=" + prefab);
                    return null;
                }
                RemoveUpgradeVariants(ship);
                RemoveInactiveParts(ship);
                FixShipRopes(ship);
                MissionMultiplayerDikozemieArena.Trace(
                    "naval ship prefab placed prefab=" + prefab +
                    " children=" + ship.ChildCount);
                return ship;
            }
            catch (Exception exception)
            {
                MissionMultiplayerDikozemieArena.Trace(
                    "naval ship prefab failed prefab=" + prefab + " " +
                    exception.GetType().Name + ": " + exception.Message);
                return null;
            }
        }

        // Parts War Sails' own scripts wake up or keep hidden. Scripts do not run
        // here, so left alone the ship burns and shows its helper shapes.
        private static readonly HashSet<string> ScriptDrivenShipParts =
            new HashSet<string>
            {
                "ship_burning_system",
                "floater_volume_holder",
                "attachment_machine_holder",
                "climbers_holder",
                "spawn_point_holder",
                "control_point_holder",
                "kinematic_batch_entity",
                "barrier_holder",
                "cam_holder_nested",
                "ship_water_effects",
                "wetness_decals",
                "ship_target_point"
            };

        private static void RemoveInactiveParts(GameEntity ship)
        {
            foreach (WeakGameEntity part in ship.WeakEntity.GetChildren().ToArray())
            {
                if (ScriptDrivenShipParts.Contains(part.Name))
                {
                    part.Remove(77);
                }
            }
            // The sail stays furled: the unfurled and burning copies are shown
            // only by the sail script.
            foreach (string tag in new[]
                {
                    "sail_mesh_entity",
                    "sail_mesh_free_entity",
                    "sail_rotate_pulleys",
                    "sail_fold_pulleys",
                    "stability_ropes_parent",
                    "lvl3_lateens"
                })
            {
                foreach (WeakGameEntity sail in ship.WeakEntity
                    .CollectChildrenEntitiesWithTag(tag))
                {
                    sail.Remove(77);
                }
            }
        }

        // War Sails ropes are drawn by a shader: a script tells it both ends every
        // frame. Left alone it bends them into a twitching loop in the air. The
        // ship's rigging does not move on the ship, so telling it once is enough.
        private static void FixShipRopes(GameEntity ship)
        {
#if CLIENT
            foreach (WeakGameEntity start in ship.WeakEntity
                .CollectChildrenEntitiesWithTag("simple_rope_start").ToArray())
            {
                WeakGameEntity end = start.Parent.GetFirstChildEntityWithTag(
                    "simple_rope_end");
                if (!end.IsValid || start.GetFirstMesh() == null)
                {
                    start.Remove(77);
                    continue;
                }
                PrepareRope(start);
                DrawRope(start, end.GetGlobalFrame().origin, 0.08f);
            }
#endif
        }

#if CLIENT
        private static void PrepareRope(WeakGameEntity start)
        {
            Mesh mesh = start.GetFirstMesh();
            mesh?.SetupAdditionalBoneBuffer(2);
            MatrixFrame identity = MatrixFrame.Identity;
            mesh?.SetAdditionalBoneFrame(1, ref identity);
            start.SetBoundingboxDirty();
            start.SetDoNotCheckVisibility(true);
        }

        private static void DrawRope(
            WeakGameEntity start,
            Vec3 worldEnd,
            float slack)
        {
            Mesh mesh = start.GetFirstMesh();
            if (mesh == null)
            {
                return;
            }
            MatrixFrame frame = start.GetGlobalFrame();
            Vec3 localEnd = frame.TransformToLocalNonOrthogonal(ref worldEnd);
            MatrixFrame bone = MatrixFrame.Identity;
            bone.rotation.s = new Vec3(0f, 0f, 0f, -1f);
            bone.origin = localEnd;
            mesh.SetAdditionalBoneFrame(0, ref bone);
            mesh.SetVectorArgument(
                localEnd.Length + Math.Max(0.005f, slack),
                25.9f,
                1f,
                mesh.GetVectorArgument().w);
        }
#endif

        // A prefab holds every upgrade a shipyard can fit. Without this the ship
        // wears all of them at once; the same rule as a ship with no upgrades.
        private static void RemoveUpgradeVariants(GameEntity ship)
        {
            foreach (WeakGameEntity slot in ship.WeakEntity
                .CollectChildrenEntitiesWithTag("upgrade_slot"))
            {
                bool hasBase = false;
                for (int index = slot.ChildCount - 1; index >= 0; index--)
                {
                    hasBase |= slot.GetChild(index).HasTag("base");
                }
                if (!hasBase)
                {
                    slot.Remove(77);
                    continue;
                }
                for (int index = slot.ChildCount - 1; index >= 0; index--)
                {
                    WeakGameEntity child = slot.GetChild(index);
                    if (!child.HasTag("base"))
                    {
                        child.Remove(77);
                    }
                }
            }
        }

        private void PlaceHullVisual(
            int index,
            float x,
            float y,
            float deckHeight,
            bool isTeleportation)
        {
            GameEntity visual = _hullVisuals[index];
            if (visual == null)
            {
                return;
            }
            MatrixFrame frame = MatrixFrame.Identity;
            // War Sails hulls point their bow along -Y. Both ships sail bow
            // first towards each other, attacker along +Y.
            if (index == 0)
            {
                frame.rotation.RotateAboutUp((float)Math.PI);
            }
            frame.origin = new Vec3(x, y, deckHeight - _hull.DeckZ);
            _hullAnchors[index] = frame.origin;
            visual.SetGlobalFrame(frame, isTeleportation);
            visual.SetFrameChanged();
            visual.UpdateGlobalBounds();
        }

        private void RegisterHullWithSea(int index, GameEntity ship)
        {
#if CLIENT
            Vec3 box = new Vec3(
                (_hull.HalfBeam + 1f) * 2f,
                (_hull.HalfLength + 1f) * 2f,
                12f);
            UIntPtr record = Mission.Scene.RegisterShipVisualToWaterRenderer(
                ship.WeakEntity,
                in box);
            _hullWaterRecords[index] = record;
            _hullWaterFramesSet[index] = false;
            if (record == UIntPtr.Zero)
            {
                MissionMultiplayerDikozemieArena.Trace(
                    "naval hull water record refused index=" + index);
                return;
            }
            ship.WeakEntity.ChangeResolutionMultiplierOfWaterVisual(
                record,
                0.5f,
                in box);
            ship.WeakEntity.RefreshMeshesToRenderToHullWater(
                record,
                "render_to_depth");
#endif
        }

        private void ReleaseHullWaterRecords()
        {
#if CLIENT
            for (int index = 0; index < _hullWaterRecords.Length; index++)
            {
                if (_hullWaterRecords[index] != UIntPtr.Zero)
                {
                    Mission.Scene?.DeRegisterShipVisual(_hullWaterRecords[index]);
                    _hullWaterRecords[index] = UIntPtr.Zero;
                }
            }
#endif
        }

        // The crews walk on a flat plane the server knows about, so the hull only
        // follows the waves a little: enough to float, not enough to lose the feet.
        private const float HeaveLimit = 0.22f;
        private const float PitchLimit = 0.018f;
        private const float RollLimit = 0.03f;

        private void TickShipsOnWaves(float dt)
        {
#if CLIENT
            Scene scene = Mission?.Scene;
            if (scene == null || dt <= 0f)
            {
                return;
            }
            float follow = Math.Min(1f, dt * 1.6f);
            for (int index = 0; index < _hullVisuals.Length; index++)
            {
                GameEntity visual = _hullVisuals[index];
                if (visual == null)
                {
                    continue;
                }
                Vec3 anchor = _hullAnchors[index];
                float along = _hull.HalfLength * 0.7f;
                float across = _hull.HalfBeam;
                float bow = scene.GetWaterLevelAtPosition(
                    new Vec2(anchor.x, anchor.y - along), true, false);
                float stern = scene.GetWaterLevelAtPosition(
                    new Vec2(anchor.x, anchor.y + along), true, false);
                float left = scene.GetWaterLevelAtPosition(
                    new Vec2(anchor.x - across, anchor.y), true, false);
                float right = scene.GetWaterLevelAtPosition(
                    new Vec2(anchor.x + across, anchor.y), true, false);
                float heave = ((bow + stern + left + right) * 0.25f) -
                    DikozemieNavalHullMetrics.WaterLevel;
                float pitch = (float)Math.Atan2(bow - stern, along * 2f);
                float roll = (float)Math.Atan2(right - left, across * 2f);
                _hullHeave[index] = Lerp(
                    _hullHeave[index],
                    Math.Max(-HeaveLimit, Math.Min(HeaveLimit, heave)),
                    follow);
                _hullPitch[index] = Lerp(
                    _hullPitch[index],
                    Math.Max(-PitchLimit, Math.Min(PitchLimit, pitch)),
                    follow);
                _hullRoll[index] = Lerp(
                    _hullRoll[index],
                    Math.Max(-RollLimit, Math.Min(RollLimit, roll)),
                    follow);

                MatrixFrame frame = MatrixFrame.Identity;
                if (index == 0)
                {
                    frame.rotation.RotateAboutUp((float)Math.PI);
                }
                frame.rotation.RotateAboutSide(_hullPitch[index]);
                frame.rotation.RotateAboutForward(_hullRoll[index]);
                frame.origin = new Vec3(
                    anchor.x,
                    anchor.y,
                    anchor.z + _hullHeave[index]);
                visual.SetGlobalFrame(frame, false);

                UIntPtr record = _hullWaterRecords[index];
                if (record != UIntPtr.Zero)
                {
                    visual.WeakEntity.SetWaterVisualRecordFrameAndDt(
                        record,
                        frame,
                        dt);
                    if (!_hullWaterFramesSet[index])
                    {
                        _hullWaterFramesSet[index] = true;
                        visual.WeakEntity.UpdateHullWaterEffectFrames(record);
                    }
                }
            }
#endif
        }

        // Moved with its ship, the enemy crew would run on the spot: the engine
        // takes any change of position for running. Standing still while carried
        // is a matter of looks, so it is decided on the client. Never on the
        // dedicated server: forcing this channel there brought it down.
        private void TickRidingCrewPose(float dt)
        {
#if CLIENT
            Team enemyCrew = Mission?.DefenderTeam;
            if (enemyCrew == null)
            {
                return;
            }
            // This action plays the plain standing animation on both legs.
            ActionIndexCache stand = ActionIndexCache.Create(
                "act_usage_trebuchet_reload_2_idle");
            bool riding = !_boardingWaveStarted && !_startedDocked &&
                (_phase == DikozemieNavalBoardingPhase.Approach ||
                 _phase == DikozemieNavalBoardingPhase.Boarded);
            if (!riding && !_crewPoseHeld)
            {
                return;
            }
            _crewPoseElapsed += Math.Max(0f, dt);
            if (riding && _crewPoseElapsed < 0.1f)
            {
                return;
            }
            _crewPoseElapsed = 0f;
            _crewPoseHeld = riding;
            foreach (Agent agent in enemyCrew.ActiveAgents.ToArray())
            {
                if (agent == null || agent.State != AgentState.Active ||
                    !agent.IsHuman || agent.MissionPeer != null)
                {
                    continue;
                }
                if (!riding)
                {
                    agent.SetActionChannel(
                        0,
                        in ActionIndexCache.act_none,
                        ignorePriority: true);
                }
                else if (agent.GetCurrentAction(0) != stand)
                {
                    agent.SetActionChannel(0, in stand, ignorePriority: true);
                }
            }
#endif
        }

        // The deck plane reaches beyond the hulls, so dropped weapons lie on
        // "air" above the waves. They stay real for the server, just not seen.
        private void HideLitterOverTheSea(float dt)
        {
            _seaLitterCheck += Math.Max(0f, dt);
            if (_seaLitterCheck < 0.5f)
            {
                return;
            }
            _seaLitterCheck = 0f;
            foreach (MissionObject missionObject in Mission.MissionObjects)
            {
                if (!(missionObject is SpawnedItemEntity item))
                {
                    continue;
                }
                WeakGameEntity entity = item.GameEntity;
                Vec3 position = entity.GlobalPosition;
                bool onShip = IsOnShip(true, position) ||
                    IsOnShip(false, position) ||
                    IsOnBridges(position);
                entity.SetVisibilityExcludeParents(onShip);
            }
        }

        // Client-side only: both crews throw grappling hooks across the gap.
        private void EnsureGrapplingVisuals()
        {
            if (_grappleEntities != null || GameNetwork.IsServer)
            {
                return;
            }
            _grappleEntities = new GameEntity[GrappleCount * 2];
            for (int index = 0; index < GrappleCount; index++)
            {
                _grappleEntities[index * 2] = CreateVisual(GrappleHookMesh);
                _grappleEntities[(index * 2) + 1] = CreateRope();
            }
            SetGrapplingVisible(false);
        }

        private GameEntity CreateRope()
        {
            GameEntity rope = GameEntity.Instantiate(
                Mission.Scene,
                GrappleRopePrefab,
                false,
                false,
                string.Empty);
#if CLIENT
            if (rope != null)
            {
                WeakGameEntity start = rope.WeakEntity
                    .GetFirstChildEntityWithTag("simple_rope_start");
                if (start.IsValid)
                {
                    PrepareRope(start);
                }
            }
#endif
            return rope;
        }

        private GameEntity CreateVisual(string mesh)
        {
            MetaMesh meta = MetaMesh.GetCopy(mesh, false, true);
            if (meta == null)
            {
                MissionMultiplayerDikozemieArena.Trace(
                    "naval visual mesh unavailable mesh=" + mesh);
                return null;
            }
            GameEntity entity = GameEntity.CreateEmpty(
                Mission.Scene,
                isModifiableFromEditor: false,
                createPhysics: false,
                callScriptCallbacks: false);
            entity?.AddMultiMesh(meta);
            return entity;
        }

        private void SetGrapplingVisible(bool visible)
        {
            if (_grappleEntities == null)
            {
                return;
            }
            foreach (GameEntity entity in _grappleEntities)
            {
                entity?.SetVisibilityExcludeParents(visible);
            }
        }

        // A hook is thrown by hand: it leaves the rail fast, rises, drops on the
        // other rail under its own weight and trails a slack rope. Each hook has
        // its own moment, and a bitten one stays on the rail while the ships close.
        private void TickGrapplingVisuals()
        {
#if CLIENT
            if (_boardingElapsed < HookThrowStart)
            {
                return;
            }
            EnsureGrapplingVisuals();
            float railZ = _layout.DeckHeight + HookRailHeight;
            float haul = _startedDocked
                ? 1f
                : Math.Max(
                    0f,
                    Math.Min(1f, (_boardingElapsed - HaulStart) / HaulSeconds));
            for (int index = 0; index < GrappleCount; index++)
            {
                GameEntity hook = _grappleEntities[index * 2];
                GameEntity rope = _grappleEntities[(index * 2) + 1];
                float time = _boardingElapsed - HookThrowStart -
                    (index * HookStagger);
                if (time < 0f)
                {
                    continue;
                }
                bool fromAttacker = index % 2 == 0;
                float along = _hull.WalkableHalfLength * 0.8f *
                    (((index + 0.5f) / GrappleCount * 2f) - 1f);
                float centerY = (_attackerY + _defenderY) * 0.5f;
                float side = fromAttacker ? 1f : -1f;
                Vec3 from = new Vec3(
                    (fromAttacker ? _attackerX : _defenderX) +
                        (side * _hull.WalkableHalfWidth),
                    centerY + along,
                    railZ);
                Vec3 target = new Vec3(
                    (fromAttacker ? _defenderX : _attackerX) -
                        (side * _hull.WalkableHalfWidth),
                    centerY + along,
                    railZ + 0.05f);

                float flight = Math.Min(1f, time / HookFlightSeconds);
                float rise = ((target.z - from.z) +
                    (0.5f * HookGravity * HookFlightSeconds * HookFlightSeconds)) /
                    HookFlightSeconds;
                float flown = Math.Min(time, HookFlightSeconds);
                Vec3 position = new Vec3(
                    Lerp(from.x, target.x, flight),
                    centerY + along,
                    from.z + (rise * flown) -
                        (0.5f * HookGravity * flown * flown));
                Vec3 heading = flight < 1f
                    ? new Vec3(
                        (target.x - from.x) / HookFlightSeconds,
                        0f,
                        rise - (HookGravity * flown))
                    : new Vec3(side * 0.5f, 0f, -0.85f);
                heading.Normalize();

                if (!_hookThrown[index])
                {
                    _hookThrown[index] = true;
                    hook?.SetVisibilityExcludeParents(true);
                    rope?.SetVisibilityExcludeParents(true);
                    SoundManager.StartOneShotEvent(
                        "event:/mission/movement/vessel/hook_throw",
                        in from);
                    if (!_boardingHornPlayed)
                    {
                        // The attack horn as the first hook flies.
                        _boardingHornPlayed = true;
                        SoundManager.StartOneShotEvent(
                            "event:/alerts/horns/attack",
                            in from);
                    }
                }
                if (flight >= 1f && !_hookBitten[index])
                {
                    _hookBitten[index] = true;
                    SoundManager.StartOneShotEvent(
                        "event:/mission/movement/vessel/hook_impact_attach",
                        in target);
                }

                if (hook != null)
                {
                    MatrixFrame frame = MatrixFrame.Identity;
                    frame.rotation.f = heading;
                    frame.rotation.s = Vec3.CrossProduct(heading, Vec3.Up);
                    frame.rotation.s.Normalize();
                    frame.rotation.u = Vec3.CrossProduct(
                        frame.rotation.s,
                        heading);
                    frame.rotation.RotateAboutSide(-(float)Math.PI / 2f);
                    frame.rotation.ApplyScaleLocal(HookScale);
                    frame.origin = position;
                    hook.SetGlobalFrame(frame);
                }
                if (rope != null)
                {
                    MatrixFrame ropeFrame = MatrixFrame.Identity;
                    ropeFrame.origin = from;
                    rope.SetGlobalFrame(ropeFrame);
                    WeakGameEntity start = rope.WeakEntity
                        .GetFirstChildEntityWithTag("simple_rope_start");
                    if (start.IsValid)
                    {
                        // Slack while it flies, tightening as the crews haul.
                        float slack = flight < 1f
                            ? Lerp(2.6f, 1.4f, flight)
                            : Lerp(1.4f, 0.04f, Math.Min(1f, haul * 2.5f));
                        DrawRope(start, position, slack);
                    }
                }
            }
#endif
        }

        private GameEntity CreateRuntimeShip(bool attacker)
        {
            MatrixFrame frame = MatrixFrame.Identity;
            // The Native ship_a longitudinal axis already follows world Y.
            // Opposite headings put both right-hand boards toward the gap.
            if (!attacker)
            {
                frame.rotation.RotateAboutUp((float)Math.PI);
            }
            // Prefab children inherit scale during instantiation. Applying it
            // only after creation leaves the visible hull at 1:1 scale.
            frame.rotation.ApplyScaleLocal(_layout.ShipScale);
            frame.origin = new Vec3(
                attacker ? _layout.AttackerStartX : _layout.DefenderStartX,
                attacker ? _layout.AttackerStartY : _layout.DefenderStartY,
                1f);
            return GameEntity.Instantiate(
                Mission.Scene,
                "ship_a",
                frame,
                true);
        }

        private static void AddMissionObjectEntities(
            IEnumerable<MissionObject> missionObjects,
            ICollection<GameEntity> sceneEntities)
        {
            foreach (MissionObject missionObject in missionObjects)
            {
                WeakGameEntity? weakEntity = missionObject?.GameEntity;
                GameEntity missionEntity = weakEntity.HasValue &&
                    weakEntity.Value.IsValid
                    ? GameEntity.CreateFromWeakEntity(weakEntity.Value)
                    : null;
                if (missionEntity != null &&
                    !sceneEntities.Contains(missionEntity))
                {
                    sceneEntities.Add(missionEntity);
                }
            }
        }

        private static GameEntity FindNearestSceneEntity(
            IEnumerable<GameEntity> entities,
            Vec3 target,
            float maximumDistance,
            params GameEntity[] excluded)
        {
            var excludedSet = new HashSet<GameEntity>(
                excluded?.Where(entity => entity != null) ??
                    Enumerable.Empty<GameEntity>());
            float maximumDistanceSquared = maximumDistance * maximumDistance;
            return entities
                .Where(entity => entity != null && !excludedSet.Contains(entity))
                .Select(entity => new
                {
                    Entity = entity,
                    DistanceSquared =
                        entity.GetGlobalFrame().origin.DistanceSquared(target)
                })
                .Where(candidate =>
                    candidate.DistanceSquared <= maximumDistanceSquared)
                .OrderBy(candidate => candidate.DistanceSquared)
                .Select(candidate => candidate.Entity)
                .FirstOrDefault();
        }

        private static MatrixFrame RemoveScale(MatrixFrame frame)
        {
            frame.rotation.f.Normalize();
            frame.rotation.s.Normalize();
            frame.rotation.u.Normalize();
            return frame;
        }

        private static string DescribePosition(Vec3 position) =>
            position.x.ToString("0.0") + "," +
            position.y.ToString("0.0") + "," +
            position.z.ToString("0.0");

        private void SetShipPositions(
            float attackerX,
            float attackerY,
            float defenderX,
            float defenderY,
            bool isTeleportation = true)
        {
            if (!_sceneReady)
            {
                return;
            }
            MatrixFrame attacker = _attackerBaseFrame;
            MatrixFrame defender = _defenderBaseFrame;
            float scale = _layout.ShipScale;
            attacker.rotation.ApplyScaleLocal(scale);
            defender.rotation.ApplyScaleLocal(scale);
            attacker.origin = new Vec3(
                attackerX,
                attackerY,
                1f);
            defender.origin = new Vec3(
                defenderX,
                defenderY,
                1f);
            _attackerShip.SetGlobalFrame(attacker, isTeleportation);
            _defenderShip.SetGlobalFrame(defender, isTeleportation);
            _attackerShip.SetFrameChanged();
            _defenderShip.SetFrameChanged();
            _attackerShip.UpdateGlobalBounds();
            _defenderShip.UpdateGlobalBounds();
            PlaceHullVisual(
                0,
                attackerX,
                attackerY,
                _attackerDeckHeight,
                isTeleportation);
            PlaceHullVisual(
                1,
                defenderX,
                defenderY,
                _defenderDeckHeight,
                isTeleportation);
            // The navigation faces and the physical walking body are two
            // halves of the same deck.  Keep both on the moving hull every
            // frame: previously a ready navmesh skipped this call and left
            // the actual collider behind at the ships' starting positions.
            SetDeckSupportPositions(isTeleportation);
            for (int index = 0; index < BoardingNetCount; index++)
            {
                GameEntity net = _boardingNets[index];
                if (net == null)
                {
                    continue;
                }
                MatrixFrame frame = _boardingNetFrames[index];
                float shipX = index < 2 ? attackerX : defenderX;
                float shipY = index < 2 ? attackerY : defenderY;
                bool attackerNet = index < 2;
                float scaleFactor = _layout.ShipScale / 2.268f;
                frame = MatrixFrame.Identity;
                frame.rotation.RotateAboutUp(
                    attackerNet
                        ? (float)(Math.PI * 0.5)
                        : (float)(-Math.PI * 0.5));
                frame.rotation.ApplyScaleLocal(new Vec3(
                    scaleFactor,
                    scaleFactor,
                    2f * scaleFactor));
                frame.origin = new Vec3(
                    shipX +
                        ((attackerNet ? 1f : -1f) *
                            DeckSupportHalfWidth * scaleFactor),
                    shipY +
                        ((index % 2 == 0 ? -1f : 1f) *
                            5.5f * scaleFactor),
                    (attackerNet
                        ? _attackerDeckHeight
                        : _defenderDeckHeight) - (4.7f * scaleFactor));
                net.SetGlobalFrame(frame, isTeleportation);
                net.SetFrameChanged();
                net.UpdateGlobalBounds();
            }
        }

        // Bots only walk where a navmesh lets them and the sea scene has none:
        // once the ships are docked, one flat surface at deck height carries
        // them across. The containment keeps everybody inside the hulls.
        private void EnsureDeckNavigation()
        {
            if (!GameNetwork.IsServer || Mission?.Scene == null ||
                _deckNavigationStations.Contains(_hull))
            {
                return;
            }
            // Every hull stands at its own place in the sea; a place keeps its
            // surface for the following battles of this mission.
            _deckNavigationStations.Add(_hull);
            GameEntity owner = GameEntity.CreateEmpty(
                Mission.Scene,
                isModifiableFromEditor: false,
                createPhysics: false,
                callScriptCallbacks: false);
            MatrixFrame frame = MatrixFrame.Identity;
            frame.origin = new Vec3(
                _layout.CenterX,
                _layout.CenterY,
                _layout.DeckHeight);
            owner.SetGlobalFrame(frame);
            int navigationId = Mission.GetNextDynamicNavMeshIdStart();
            Mission.Scene.ImportNavigationMeshPrefab(
                DeckNavigationPrefab,
                navigationId);
            owner.AttachNavigationMeshFaces(
                navigationId,
                isConnected: false,
                isBlocker: false,
                autoLocalize: false,
                finalizeBlockerConvexHullComputation: false,
                updateEntityFrame: true);
            Mission.Scene.SetAbilityOfFacesWithId(navigationId, true);
            owner.UpdateAttachedNavigationMeshFaces();
            owner.UpdateGlobalBounds();
            _deckNavigationIds[0] = navigationId;
            _deckNavigationReady = Mission.Scene.IsAnyFaceWithId(navigationId);
            MissionMultiplayerDikozemieArena.Trace(
                "naval deck navigation ready=" + _deckNavigationReady +
                " id=" + navigationId +
                " at=" + DescribePosition(frame.origin));
        }

        private void EnsureDeckSupports()
        {
            for (int index = 0; index < _deckSupports.Length; index++)
            {
                if (_deckSupports[index] != null)
                {
                    continue;
                }
                MatrixFrame supportFrame = CreateDeckSupportFrame(index);
                float expectedHeight = index == 0
                    ? GetExpectedAttackerDeckHeight()
                    : GetExpectedDefenderDeckHeight();
                Vec3 probe = new Vec3(
                    index == 0 ? _attackerX : _defenderX,
                    index == 0 ? _attackerY : _defenderY,
                    expectedHeight);
                foreach (string prefab in DeckSupportPrefabs)
                {
                    GameEntity candidate = GameEntity.Instantiate(
                        Mission.Scene,
                        prefab,
                        supportFrame,
                        true);
                    if (candidate == null)
                    {
                        MissionMultiplayerDikozemieArena.Trace(
                            "naval deck collider candidate side=" +
                            (index == 0 ? "attacker" : "defender") +
                            " resource=" + prefab + " available=False");
                        continue;
                    }
                    PrepareWalkingCollider(candidate);
                    (Vec3 physicsMin, Vec3 physicsMax) =
                        candidate.ComputeGlobalPhysicsBoundingBoxMinMax();
                    float surfaceOffset = physicsMax.z - supportFrame.origin.z;
                    if (surfaceOffset <= 0.01f ||
                        float.IsNaN(surfaceOffset) ||
                        float.IsInfinity(surfaceOffset))
                    {
                        candidate.Remove(0);
                        continue;
                    }
                    _deckSupportSurfaceOffsets[index] = surfaceOffset;
                    MatrixFrame calibratedFrame =
                        CreateDeckSupportFrame(index);
                    candidate.SetGlobalFrame(calibratedFrame, true);
                    candidate.SetFrameChanged();
                    candidate.UpdateGlobalBounds();
                    (physicsMin, physicsMax) =
                        candidate.ComputeGlobalPhysicsBoundingBoxMinMax();
                    bool surfaceReady = Math.Abs(
                        physicsMax.z - expectedHeight) <= 0.20f;
                    MissionMultiplayerDikozemieArena.Trace(
                        "naval deck collider candidate side=" +
                        (index == 0 ? "attacker" : "defender") +
                        " resource=" + prefab +
                        " surfaceReady=" + surfaceReady +
                        " surfaceZ=" + physicsMax.z.ToString("0.00") +
                        " surfaceOffset=" + surfaceOffset.ToString("0.00") +
                        " dynamicBody=" + candidate.HasDynamicRigidBody() +
                        " activeSimulation=" +
                            candidate.HasDynamicRigidBodyAndActiveSimulation() +
                        " kinematicBody=" + candidate.HasKinematicRigidBody() +
                        " body=" + candidate.BodyFlag);
                    if (surfaceReady)
                    {
                        _deckSupports[index] = candidate;
                        _deckSupportSurfaceReady[index] = true;
                        _deckSupportResources[index] = prefab;
                        break;
                    }
                    candidate.Remove(0);
                }
            }
            _deckColliderSurfaceReady = _deckSupportSurfaceReady.All(ready => ready);
            MissionMultiplayerDikozemieArena.Trace(
                "naval invisible deck supports ready=" +
                _deckSupports.Count(support => support != null) +
                " walkable=" + _deckColliderSurfaceReady +
                " resources=" + string.Join(",", _deckSupportResources));
        }

        private static void PrepareWalkingCollider(GameEntity collider)
        {
            collider.SetMobility(GameEntity.Mobility.Dynamic);
            collider.SetVisibilityExcludeParents(false);
            var colliderParts = new List<GameEntity> { collider };
            collider.GetChildrenRecursive(ref colliderParts);
            foreach (GameEntity colliderPart in colliderParts)
            {
                colliderPart.SetVisibilityExcludeParents(false);
                BodyFlags flags = colliderPart.BodyFlag &
                    ~BodyFlags.BodyOwnerFilter;
                colliderPart.BodyFlag = flags |
                    BodyFlags.BodyOwnerEntity |
                    BodyFlags.AgentOnly |
                    BodyFlags.NotDestructible;
            }
            collider.SetPhysicsState(true, true);
            // No dynamic body: agents find their ground on ordinary geometry and
            // fall through a dynamic one (58 of 64 in the water, 2026-10-30).
            collider.SetFrameChanged();
            collider.UpdateGlobalBounds();
        }

        private void SetDeckSupportPositions(bool isTeleportation = true)
        {
            for (int index = 0; index < _deckSupports.Length; index++)
            {
                GameEntity support = _deckSupports[index];
                if (support == null)
                {
                    continue;
                }
                MatrixFrame worldFrame = CreateDeckSupportFrame(index);
                support.SetGlobalFrame(worldFrame, isTeleportation);
                support.SetFrameChanged();
                support.UpdateGlobalBounds();
            }
        }

        private MatrixFrame CreateDeckSupportFrame(int index)
        {
            float scaleFactor = _layout.ShipScale / 2.268f;
            MatrixFrame frame = MatrixFrame.Identity;
            frame.rotation.ApplyScaleLocal(new Vec3(
                2f * scaleFactor,
                12f * scaleFactor,
                0.5f));
            bool attacker = index == 0;
            frame.origin = new Vec3(
                attacker ? _attackerX : _defenderX,
                attacker ? _attackerY : _defenderY,
                (attacker
                    ? GetExpectedAttackerDeckHeight()
                    : GetExpectedDefenderDeckHeight()) -
                        _deckSupportSurfaceOffsets[index]);
            return frame;
        }

        private float GetExpectedAttackerDeckHeight() =>
            _attackerDeckHeight > WaterSurfaceZ + 0.75f
                ? _attackerDeckHeight
                : _layout.DeckHeight;

        private float GetExpectedDefenderDeckHeight() =>
            _defenderDeckHeight > WaterSurfaceZ + 0.75f
                ? _defenderDeckHeight
                : _layout.DeckHeight;

        private void SetBoardingNetVisibility(bool visible)
        {
            foreach (GameEntity net in _boardingNets)
            {
                net?.SetVisibilityExcludeParents(visible);
            }
        }

        private void StowBoardingBridges()
        {
            for (int index = 0; index < BridgeCount; index++)
            {
                if (_bridges[index] == null)
                {
                    continue;
                }
                MatrixFrame frame = _bridgeFrames[index];
                frame.rotation.ApplyScaleLocal(
                    new Vec3(6f, 1.25f, 0.5f));
                frame.origin = new Vec3(
                    DikozemieNavalBoardingLayout.SeaCenterX,
                    DikozemieNavalBoardingLayout.SeaCenterY,
                    -50f - index);
                _bridges[index].SetGlobalFrame(frame);
                _bridges[index].SetFrameChanged();
            }
        }

        private void DeployBoardingBridges(float progress)
        {
            float[] offsets = { -7.5f, -2.5f, 2.5f, 7.5f };
            float lengthScale = _layout.DockedSeparation / 24f;
            for (int index = 0; index < BridgeCount; index++)
            {
                if (_bridges[index] == null)
                {
                    continue;
                }
                MatrixFrame frame = _bridgeFrames[index];
                frame.rotation.ApplyScaleLocal(new Vec3(
                    6f * Math.Max(0.04f, progress) * lengthScale,
                    1.25f,
                    0.5f));
                frame.origin = new Vec3(
                    _layout.CenterX,
                    _layout.CenterY + offsets[index],
                    _layout.DeckHeight - 0.10f);
                _bridges[index].SetGlobalFrame(frame);
                _bridges[index].SetFrameChanged();
            }
        }

        private void MaintainCrewSupportBindings()
        {
            if (_crewShipAttachmentsReleased || Mission == null ||
                Mission.AttackerTeam == null ||
                Mission.DefenderTeam == null)
            {
                return;
            }
            foreach (Agent agent in Mission.AttackerTeam.ActiveAgents.ToArray())
            {
                BindCrewMemberToShip(agent, attacker: true, agent.Position);
            }
            foreach (Agent agent in Mission.DefenderTeam.ActiveAgents.ToArray())
            {
                BindCrewMemberToShip(agent, attacker: false, agent.Position);
            }
        }

        private void BindCrewMemberToShip(
            Agent agent,
            bool attacker,
            Vec3 intendedPosition)
        {
            if (_crewShipAttachmentsReleased || agent == null ||
                agent.State != AgentState.Active ||
                !agent.IsHuman || agent.MountAgent != null ||
                _shipBoundCrew.Contains(agent))
            {
                return;
            }
            GameEntity deck = attacker ? _attackerShip : _defenderShip;
            if (deck == null)
            {
                return;
            }
            // The local player owns client movement, so server-side position
            // corrections alone do not carry its camera with a moving ship.
            // Attach to the authored hull itself while it approaches. This is
            // done once and preserves ordinary walking relative to the deck.
            agent.SetForceAttachedEntity(deck.WeakEntity);
            agent.SetExcludedFromGravity(false, false);
            _shipBoundCrew.Add(agent);
        }

        private void ReleaseCrewShipAttachments()
        {
            if (_crewShipAttachmentsReleased)
            {
                return;
            }
            _crewShipAttachmentsReleased = true;
            foreach (Agent agent in _shipBoundCrew.Where(agent =>
                agent != null && agent.IsHuman))
            {
                agent.SetForceAttachedEntity(default(WeakGameEntity));
                agent.SetExcludedFromGravity(false, false);
            }
            MissionMultiplayerDikozemieArena.Trace(
                "naval crews detached from stationary hull parents onto " +
                "boarding bridges");
        }

        private void RestoreCrewGravity()
        {
            foreach (Agent agent in _shipBoundCrew.Where(agent =>
                agent != null && agent.IsHuman))
            {
                agent.SetForceAttachedEntity(default(WeakGameEntity));
                agent.SetExcludedFromGravity(false, false);
            }
            _shipBoundCrew.Clear();
        }

        private void MaintainSafetyDeckContainment()
        {
            if (Mission?.AttackerTeam == null || Mission.DefenderTeam == null)
            {
                return;
            }
            foreach (Agent agent in Mission.AttackerTeam.ActiveAgents
                .Concat(Mission.DefenderTeam.ActiveAgents)
                .Where(agent => agent != null &&
                    agent.State == AgentState.Active && agent.IsHuman &&
                    agent.MountAgent == null))
            {
                Vec3 original = agent.Position;
                if (Math.Abs(original.x - _layout.CenterX) <=
                        SafetyFloorHalfWidth &&
                    Math.Abs(original.y - _layout.CenterY) <=
                        SafetyFloorHalfLength)
                {
                    continue;
                }
                bool attacker = agent.Team == Mission.AttackerTeam;
                agent.TeleportToPosition(GetDeckPosition(attacker, 0f, 0f));
            }
        }

        private Vec3 ResolveDeckPosition(bool attacker, Vec3 approximate)
        {
            GameEntity ship = attacker ? _attackerShip : _defenderShip;
            GameEntity support = _deckSupports[attacker ? 0 : 1];
            float centerX = attacker ? _attackerX : _defenderX;
            float centerY = attacker ? _attackerY : _defenderY;
            float fallback = attacker
                ? _attackerDeckHeight
                : _defenderDeckHeight;
            approximate = ClampToShip(attacker, approximate);
            approximate.z = fallback;
            if (TryResolveDeckSurface(
                    support,
                    approximate,
                    out Vec3 supported))
            {
                return supported;
            }
            float[] contractions = { 1f, 0.85f, 0.65f, 0.45f, 0.2f, 0f };
            foreach (float contraction in contractions)
            {
                Vec3 candidate = new Vec3(
                    centerX + ((approximate.x - centerX) * contraction),
                    centerY +
                        ((approximate.y - centerY) * contraction),
                    fallback);
                if (TryResolveDeckSurface(ship, candidate, out Vec3 resolved))
                {
                    return resolved;
                }
            }
            return new Vec3(
                centerX,
                centerY,
                fallback);
        }

        private static float ResolveDeckSurfaceHeight(
            GameEntity ship,
            Vec3 position,
            float fallback)
        {
            return TryResolveDeckSurface(ship, position, out Vec3 resolved)
                ? resolved.z
                : fallback;
        }

        private static bool TryResolveDeckSurface(
            GameEntity ship,
            Vec3 position,
            out Vec3 resolved,
            float maximumHeightDelta = 10f)
        {
            resolved = position;
            if (ship == null)
            {
                return false;
            }
            Vec3 rayOrigin = new Vec3(
                position.x,
                position.y,
                // Start just above the expected walking deck.  Casting from
                // high above the ship hits sails, yards and mast caps first.
                position.z + 2f);
            Vec3 normal = Vec3.Up;
            float hitLength = 0f;
            const float maxLength = 160f;
            if (!ship.RayHitEntityWithNormal(
                    rayOrigin,
                    new Vec3(0f, 0f, -1f),
                    maxLength,
                    ref normal,
                    ref hitLength) ||
                normal.z < 0.25f ||
                hitLength < 0f ||
                hitLength > maxLength)
            {
                return false;
            }
            float z = rayOrigin.z - hitLength + 0.08f;
            if (z <= WaterSurfaceZ + 0.75f ||
                Math.Abs(z - position.z) > maximumHeightDelta)
            {
                return false;
            }
            resolved = new Vec3(position.x, position.y, z);
            return true;
        }

        private void RescueFallenCombatants()
        {
            Agent[] agents = Mission.AttackerTeam.ActiveAgents
                .Concat(Mission.DefenderTeam.ActiveAgents)
                .Where(agent => agent != null &&
                    agent.State == AgentState.Active && agent.IsHuman &&
                    agent.MountAgent == null &&
                    agent.Position.z <= WaterSurfaceZ + 0.75f)
                .ToArray();
            foreach (Agent agent in agents)
            {
                float attackerDistanceSquared =
                    ((agent.Position.x - _attackerX) *
                        (agent.Position.x - _attackerX)) +
                    ((agent.Position.y - _attackerY) *
                        (agent.Position.y - _attackerY));
                float defenderDistanceSquared =
                    ((agent.Position.x - _defenderX) *
                        (agent.Position.x - _defenderX)) +
                    ((agent.Position.y - _defenderY) *
                        (agent.Position.y - _defenderY));
                bool attackerShip =
                    attackerDistanceSquared <= defenderDistanceSquared;
                float shipCenterX = attackerShip ? _attackerX : _defenderX;
                float shipCenterY = attackerShip ? _attackerY : _defenderY;
                float acrossOffset = Math.Max(
                    -DeckSupportHalfWidth,
                    Math.Min(
                        DeckSupportHalfWidth,
                        agent.Position.x - shipCenterX));
                if (Math.Abs(acrossOffset) < 1f)
                {
                    acrossOffset = attackerShip ? 1.75f : -1.75f;
                }
                float alongOffset =
                    agent.Position.y >= shipCenterY
                        ? 6f
                        : -6f;
                Vec3 safe = GetDeckPosition(
                    attackerShip,
                    acrossOffset,
                    alongOffset);
                PlaceCrewOnDeck(agent, attackerShip, safe);
            }
            if (agents.Length > 0)
            {
                MissionMultiplayerDikozemieArena.Trace(
                    "naval water rescue count=" + agents.Length);
            }
        }

        private void ReadyGrapplingHooks()
        {
            if (_hookReadied)
            {
                return;
            }
            _hookReadied = true;
            foreach (Agent thrower in GetHookThrowers())
            {
                ActionIndexCache action = ActionIndexCache.act_usage_hook_ready;
                thrower.SetActionChannel(1, in action, ignorePriority: true);
            }
        }

        private void ReleaseGrapplingHooks()
        {
            foreach (Agent thrower in GetHookThrowers())
            {
                ActionIndexCache action = ActionIndexCache.act_usage_hook_release;
                thrower.SetActionChannel(1, in action, ignorePriority: true);
            }
        }

        private Agent[] GetHookThrowers() =>
            Mission.AttackerTeam.ActiveAgents
                .Concat(Mission.DefenderTeam.ActiveAgents)
                .Where(agent => agent != null &&
                    agent.State == AgentState.Active && agent.IsHuman &&
                    agent.MissionPeer == null)
                .GroupBy(agent => agent.Team)
                .SelectMany(group => group.Take(2))
                .ToArray();

        private void CaptureDebugFrame(string stage)
        {
            if (GameNetwork.IsServer ||
                !string.Equals(
                    Environment.GetEnvironmentVariable(
                        "DIKOZEMIE_NAVAL_CAPTURE"),
                    "1",
                    StringComparison.Ordinal))
            {
                return;
            }
            try
            {
                string directory = Environment.GetEnvironmentVariable(
                    "DIKOZEMIE_NAVAL_CAPTURE_DIR");
                if (string.IsNullOrWhiteSpace(directory))
                {
                    directory = System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(),
                        "dikozemie-naval-captures");
                }
                Directory.CreateDirectory(directory);
                string path = System.IO.Path.Combine(
                    directory,
                    "naval-" + _shipClass.ToString().ToLowerInvariant() +
                    "-" + stage + ".png");
                Utilities.TakeScreenshot(path);
                MissionMultiplayerDikozemieArena.Trace(
                    "naval screenshot requested path=" + path);
            }
            catch (Exception exception)
            {
                MissionMultiplayerDikozemieArena.Trace(
                    "naval screenshot failed: " + exception.Message);
            }
        }

        private static string Format(Vec3 value) =>
            value.x.ToString("0.0") + "," +
            value.y.ToString("0.0") + "," +
            value.z.ToString("0.0");

        private void Broadcast(DikozemieNavalBoardingPhase phase)
        {
            if (!GameNetwork.IsServer)
            {
                return;
            }
            GameNetwork.BeginBroadcastModuleEvent();
            GameNetwork.WriteMessage(
                new DikozemieNavalBoardingStateMessage(
                    phase,
                    _shipClass,
                    _hullIndex));
            GameNetwork.EndBroadcastModuleEvent(
                GameNetwork.EventBroadcastFlags.None);
        }

        private static float Lerp(float from, float to, float amount) =>
            from + ((to - from) * amount);
    }
}
