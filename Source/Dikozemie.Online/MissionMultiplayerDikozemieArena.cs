using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Diamond;
using TaleWorlds.ObjectSystem;

namespace Dikozemie.Online
{
    public sealed class MissionMultiplayerDikozemieArena : MissionMultiplayerGameModeBase
    {
        private const int InitialGold = 1500;
        internal const int WinnerCampaignReward = 1;
        internal const int LoserCampaignReward = 0;

        private static readonly TimeSpan AdmissionTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan HumanOpponentWaitTimeout =
            TimeSpan.FromSeconds(60);
        private static readonly TimeSpan PostKnockoutPresentation =
            TimeSpan.FromSeconds(3);
        // A team battle waits this long for more live players before bots
        // take the empty places (players coming from the lobby or pressing
        // "next battle" on their result screens).
        // Customer 2026-10-31: wait a minute for people, then fill up with bots.
        private static readonly TimeSpan TeamGatherWindow =
            TimeSpan.FromSeconds(60);
        // At sea the crews already stand on deck, so nobody waits long.
        private static readonly TimeSpan NavalGatherWindow =
            TimeSpan.FromSeconds(25);
        // Stage 5: a battle group (a party, two parties that met on the
        // road) waits this long for its last member, then fights without him.
        private static readonly TimeSpan GroupGatherWindow =
            TimeSpan.FromSeconds(120);
        // A gathered player whose game never spawns the hero (still loading,
        // crashed) must not hold the battle for everybody else.
        private static readonly TimeSpan TeamSpawnGrace =
            TimeSpan.FromSeconds(15);
        private static readonly TimeSpan CompletedBattleHoldWhileQueued =
            TimeSpan.FromSeconds(30);
        private static readonly object DiagnosticGate = new object();
        private static readonly string DiagnosticLogPath = ResolveDiagnosticLogPath();

        // The Wine servers run as the Windows user PowerPC. The native Linux
        // server (tried 2026-11-10) cannot use that path: GetDirectoryName of
        // a backslash path is empty there, CreateDirectory throws and the log
        // silently stops, perf lines included.
        private static string ResolveDiagnosticLogPath()
        {
            string configured = Environment.GetEnvironmentVariable("DIKOZEMIE_ARENA_SERVER_LOG");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured.Trim();
            }
            return Path.DirectorySeparatorChar == '\\'
                ? @"C:\Users\PowerPC\AppData\Local\Temp\dikozemie-arena-server.log"
                : Path.Combine(Path.GetTempPath(), "dikozemie-arena-server.log");
        }

        private readonly Dictionary<NetworkCommunicator, DateTimeOffset> _pendingAdmissions =
            new Dictionary<NetworkCommunicator, DateTimeOffset>();
        private readonly HashSet<NetworkCommunicator> _admittedPeers =
            new HashSet<NetworkCommunicator>();
        // Observers of the watch (plan, «Античит»): on the spectators' team,
        // never queued, never spawned. Moved there from the mission tick.
        private readonly HashSet<NetworkCommunicator> _observerPeers =
            new HashSet<NetworkCommunicator>();
        // Fighters who got a seat on this server (MaxNumberOfPlayers less
        // the observer's one, see HasFighterSeat).
        private readonly HashSet<NetworkCommunicator> _seatedFighters =
            new HashSet<NetworkCommunicator>();
        private readonly HashSet<NetworkCommunicator> _pendingObservers =
            new HashSet<NetworkCommunicator>();
        private readonly HashSet<NetworkCommunicator> _profileReadyPeers =
            new HashSet<NetworkCommunicator>();
        private readonly HashSet<NetworkCommunicator> _disconnectingPeers =
            new HashSet<NetworkCommunicator>();
        private readonly Queue<NetworkCommunicator> _waitingPeers =
            new Queue<NetworkCommunicator>();
        private readonly Dictionary<NetworkCommunicator, DateTimeOffset>
            _humanOpponentWaitDeadlines =
                new Dictionary<NetworkCommunicator, DateTimeOffset>();
        // Players who pressed «Начать с ботом»: the next tick stops waiting for
        // live people and fills the battle with bots.
        private readonly HashSet<NetworkCommunicator> _startNowRequests =
            new HashSet<NetworkCommunicator>();
        // A player's own horse stays his while he lives: he may step down and
        // mount again. Every other horse without a rider belongs to a finished
        // battle. 2026-11-05: ~trace died on his horse at 21:30 and quit; the
        // horse stood in the empty arena overnight, and the customer mounted
        // it while waiting for an opponent and rode it into his duel.
        private readonly Dictionary<Agent, Agent> _ownMountRiders =
            new Dictionary<Agent, Agent>();
        private DateTimeOffset _nextStrayMountSweepAt;
        private readonly Dictionary<NetworkCommunicator, string> _queueNoticeSent =
            new Dictionary<NetworkCommunicator, string>();
        private readonly Dictionary<NetworkCommunicator, ArenaSeriesState> _seriesStates =
            new Dictionary<NetworkCommunicator, ArenaSeriesState>();
        private readonly Dictionary<NetworkCommunicator, Dictionary<string, int>>
            _duelSkillExperience =
                new Dictionary<NetworkCommunicator, Dictionary<string, int>>();
        private readonly Dictionary<NetworkCommunicator, string> _duelMovementSkills =
            new Dictionary<NetworkCommunicator, string>();

        private MissionScoreboardComponent _scoreboard;
        private MissionNetworkComponent _missionNetworkComponent;
        private NetworkCommunicator _arenaVisitor;
        private NetworkCommunicator _activeAttacker;
        private NetworkCommunicator _activeDefender;
        private Agent _activeBot;
        private string _activeBotCharacterId;
        private int _activeBotRating;
        private Agent _arenaMaster;
        private int _botRoundWins;
        private int _winsRequired;
        private float _botHealthMultiplier;
        private bool _trainingDuel;
        private bool _reportedTrainingPlayerNotSpawned;
        private bool _visitorRequestedDuel;
        private DateTimeOffset _nextTrainingBotSpawnAt;
        private bool _duelCompleted;
        private DateTimeOffset _duelCompletedAt;
        private bool _pendingTrainingResult;
        private int _pendingTrainingPlayerScore;
        private DateTimeOffset _pendingTrainingResultAt;
        private readonly DikozemieArenaKnockoutWindow _trainingKnockout =
            new DikozemieArenaKnockoutWindow();
        private readonly DikozemieArenaKnockoutWindow _matchKnockout =
            new DikozemieArenaKnockoutWindow();
        private Agent _pendingTrainingPlayerAgent;
        private Agent _pendingTrainingBotAgent;
        private bool _pendingMatchResult;
        private NetworkCommunicator _pendingMatchWinner;
        private NetworkCommunicator _pendingMatchLoser;
        private Agent _pendingMatchWinnerAgent;
        private Agent _pendingMatchLoserAgent;
        private Agent _activeAttackerAgent;
        private Agent _activeDefenderAgent;
        private readonly List<Agent> _activeTeamBots = new List<Agent>();
        private readonly HashSet<Agent> _rewardedTeamVictims =
            new HashSet<Agent>();
        private readonly HashSet<NetworkCommunicator> _deathSummarySentPeers =
            new HashSet<NetworkCommunicator>();
        // Every live player of the current team battle, both sides. The
        // first attacker is also kept in _activeAttacker for the 1x1 paths.
        private readonly List<NetworkCommunicator> _teamParticipants =
            new List<NetworkCommunicator>();
        private readonly Dictionary<NetworkCommunicator, BattleSideEnum>
            _teamParticipantSides =
                new Dictionary<NetworkCommunicator, BattleSideEnum>();
        private readonly HashSet<NetworkCommunicator> _teamEliminatedPeers =
            new HashSet<NetworkCommunicator>();
        private readonly Dictionary<NetworkCommunicator, Agent> _teamHumanAgents =
            new Dictionary<NetworkCommunicator, Agent>();
        private readonly Dictionary<NetworkCommunicator, int> _teamPersonalKills =
            new Dictionary<NetworkCommunicator, int>();
        private readonly Dictionary<NetworkCommunicator, int> _teamKillRatingEarned =
            new Dictionary<NetworkCommunicator, int>();
        private DateTimeOffset _teamGatherDeadline;
        private readonly Dictionary<string, DateTimeOffset> _groupDeadlines =
            new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        private DateTimeOffset _nextQueueStatusAt;
        private int _gatherPlayers;
        private int _gatherCapacity;
        private DateTimeOffset _teamSpawnDeadline;
        private bool _teamBattle;
        private int _teamSize = 1;
        // Stage 6: unequal sides of a siege (0: both sides _teamSize) and
        // the strength of each side's bots (0: near the players' rating).
        private int _teamAttackerSize;
        private int _teamDefenderSize;
        private int _teamAttackerRating;
        private int _teamDefenderRating;
        private DikozemieArenaDiscipline _teamDiscipline =
            DikozemieArenaDiscipline.Open;
        private DikozemieArenaComposition _teamComposition =
            DikozemieArenaComposition.Mixed;
        private bool _teamBotsSpawned;
        private bool _pendingTeamResult;
        private DikozemieArenaTeamOutcome _pendingTeamOutcome;
        private DateTimeOffset _pendingTeamResultAt;
        private DikozemieArenaTeamField _teamField = new DikozemieArenaTeamField();
        private bool _teamPlayerStartsAtAttackerWall;
        private bool _nextTeamPlayerStartsAtAttackerWall;
        private DikozemieNavalBattleDefinition _navalBattle;
        // The crews wait on deck while players gather, as in War Sails, and
        // become the battle's crews: only the places of live players are freed.
        private readonly List<Agent> _navalWaitingCrew = new List<Agent>();
        private DikozemieArenaTeamField _navalWaitingField;
        private DikozemieNavalBattleDefinition _navalWaitingHull;
        private Agent _navalFriendlyCaptain;
        private Agent _navalEnemyCaptain;
        private NetworkCommunicator _pendingNextDuelPeer;
        private DateTimeOffset _pendingNextDuelAt;
        private readonly HashSet<NetworkCommunicator> _pendingNextDuelFollowers =
            new HashSet<NetworkCommunicator>();

        public override bool IsGameModeHidingAllAgentVisuals => true;
        public override bool IsGameModeUsingOpposingTeams => true;

        public bool HasDuelPair => _teamBattle
            ? _teamParticipants.Any(IsConnected)
            : IsConnected(_activeAttacker) &&
              (IsConnected(_activeDefender) || _trainingDuel);

        internal int CurrentTeamSize => Math.Max(1, _teamSize);

        public bool IsActiveDuelist(NetworkCommunicator peer) =>
            peer != null &&
            (peer == _activeAttacker || peer == _activeDefender ||
             (_teamBattle && _teamParticipantSides.ContainsKey(peer))) &&
            !_duelCompleted;

        // A team battle grants each hero exactly one life, and nobody joins
        // a battle whose bots are already fighting. The idle 3D-lobby visitor
        // may walk the empty arena only while no battle is running.
        public bool CanSpawnPeer(NetworkCommunicator peer) =>
            peer != null &&
            _profileReadyPeers.Contains(peer) &&
            (IsActiveDuelist(peer) ||
             (peer == _arenaVisitor && !HasDuelPair)) &&
            !_teamEliminatedPeers.Contains(peer) &&
            !(_teamBattle && _teamBotsSpawned) &&
            !_pendingTrainingResult && !_pendingMatchResult &&
            !_pendingTeamResult && !_duelCompleted;

        public override MultiplayerGameType GetMissionType() =>
            MultiplayerGameType.TeamDeathmatch;

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            Trace("behavior initialized dedicated=" + GameNetwork.IsDedicatedServer);
            if (GameNetwork.IsDedicatedServer)
            {
                DikozemieItemLoadAudit.Run(Trace);
            }
            _missionNetworkComponent =
                Mission.GetMissionBehavior<MissionNetworkComponent>();
            if (_missionNetworkComponent != null)
            {
                _missionNetworkComponent.OnClientSynchronizedEvent +=
                    OnClientSynchronized;
            }
            _scoreboard = Mission.GetMissionBehavior<MissionScoreboardComponent>();
            _winsRequired = DikozemieArenaRuntimeSettings.WinsRequired;
            _botHealthMultiplier = DikozemieArenaRuntimeSettings.BotHealthMultiplier;
            if (GameNetwork.IsServerOrRecorder || GameNetwork.IsDedicatedServer)
            {
                DikozemieArenaOpponentCatalog.EnsureLeaderboardSeeded();
            }
        }

        public override void OnRemoveBehavior()
        {
            if (_missionNetworkComponent != null)
            {
                _missionNetworkComponent.OnClientSynchronizedEvent -=
                    OnClientSynchronized;
            }
            base.OnRemoveBehavior();
        }

        private void OnClientSynchronized(NetworkCommunicator peer)
        {
            if (peer == null || peer.IsServerPeer ||
                DikozemieArenaServerRegistration.UsesExternalMatchmaker)
            {
                return;
            }

            EnsureRepresentative(peer);
            AdmitPeer(peer);
        }

        public override void AfterStart()
        {
            string attackerCultureId = MultiplayerOptions.OptionType.CultureTeam1.GetStrValue();
            string defenderCultureId = MultiplayerOptions.OptionType.CultureTeam2.GetStrValue();
            BasicCultureObject attacker =
                MBObjectManager.Instance.GetObject<BasicCultureObject>(attackerCultureId);
            BasicCultureObject defender =
                MBObjectManager.Instance.GetObject<BasicCultureObject>(defenderCultureId);

            Mission.Teams.Add(
                BattleSideEnum.Attacker,
                attacker.BackgroundColor1,
                attacker.ForegroundColor1,
                attacker.Banner);
            Mission.Teams.Add(
                BattleSideEnum.Defender,
                defender.BackgroundColor2,
                defender.ForegroundColor2,
                defender.Banner);
        }

        protected override void HandleEarlyNewClientAfterLoadingFinished(
            NetworkCommunicator peer)
        {
            Trace("early peer=" + DescribePeer(peer));
            DikozemieArenaServerRegistration.BeginClientProfileTransfer(peer);
            if (DikozemieArenaServerRegistration.UsesExternalMatchmaker &&
                DikozemieArenaServerRegistration.GetAdmissionState(peer) ==
                DikozemieArenaAdmissionState.Rejected)
            {
                RejectPeer(peer, "player id is not reserved for this arena");
                return;
            }
            EnsureRepresentative(peer);
        }

        protected override void HandleNewClientAfterSynchronized(NetworkCommunicator peer)
        {
            Trace("synchronized peer=" + DescribePeer(peer));
            SendItemRegistry(peer);
            // TaleWorlds already authenticated a peer that reached a standalone
            // custom game. Do not put that peer through the legacy 15-second
            // campaign-profile admission timer: it was the source of the exact
            // 15-second return to the lobby seen by real clients.
            if (!DikozemieArenaServerRegistration.UsesExternalMatchmaker)
            {
                EnsureRepresentative(peer);
                AdmitPeer(peer);
                return;
            }

            DikozemieArenaAdmissionState admission =
                DikozemieArenaServerRegistration.GetAdmissionState(peer);
            if (admission == DikozemieArenaAdmissionState.Rejected)
            {
                RejectPeer(peer, "player id is not reserved for this arena");
                return;
            }
            if (admission == DikozemieArenaAdmissionState.Pending)
            {
                _pendingAdmissions[peer] = DateTimeOffset.UtcNow.Add(AdmissionTimeout);
                Console.WriteLine(
                    "DikozemieOnline: waiting for campaign profile id=" +
                    peer.PlayerConnectionInfo?.PlayerID.ToString());
                return;
            }
            AdmitPeer(peer);
        }

        // The engine waits for the next frame by sleeping only while nobody is
        // connected; with a player it spins (GameNetwork.AddNetworkPeer turns
        // SetFrameLimiterWithSleep off), because Windows sleeps are coarse.
        // On Linux a sleep is precise to a fraction of a millisecond, and the
        // spinning main thread held a whole core per battle (native server,
        // 2026-11-10). Linux keeps the sleeping limiter; Windows/Wine keeps
        // the engine's choice. DIKOZEMIE_SLEEP_FRAME_LIMITER=1/0 overrides.
        private static readonly bool SleepFrameLimiter = ResolveSleepFrameLimiter();
        private float _frameLimiterCheck;

        private static bool ResolveSleepFrameLimiter()
        {
            string configured = Environment.GetEnvironmentVariable("DIKOZEMIE_SLEEP_FRAME_LIMITER");
            if (configured == "1")
            {
                return true;
            }
            if (configured == "0")
            {
                return false;
            }
            return Path.DirectorySeparatorChar == '/';
        }

        private void KeepSleepingFrameLimiter(float dt)
        {
            if (!SleepFrameLimiter)
            {
                return;
            }
            _frameLimiterCheck -= dt;
            if (_frameLimiterCheck > 0f)
            {
                return;
            }
            _frameLimiterCheck = 1f;
            if (!TaleWorlds.Engine.Utilities.GetFrameLimiterWithSleep())
            {
                TaleWorlds.Engine.Utilities.SetFrameLimiterWithSleep(true);
                Trace("frame limiter: sleeping (players=" + GameNetwork.NetworkPeerCount + ")");
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (!GameNetwork.IsServerOrRecorder && !GameNetwork.IsDedicatedServer)
            {
                return;
            }

            if (GameNetwork.IsDedicatedServer)
            {
                DikozemieServerPerf.EnsureWatchdog(DiagnosticLogPath);
                KeepSleepingFrameLimiter(dt);
            }
            DikozemieServerPerf.Frame(dt, GameNetwork.NetworkPeerCount);
            long started = DikozemieServerPerf.Start();
            DikozemieServerPerf.Crumb("arena tick");
            try
            {
                TickServerArena(dt);
            }
            finally
            {
                DikozemieServerPerf.Stop(
                    DikozemieServerPerf.Section.ArenaTick, started);
                DikozemieServerPerf.Crumb("arena tick done");
            }
        }

        private void TickServerArena(float dt)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            long leaderboardStarted = DikozemieServerPerf.Start();
            DikozemieArenaLeaderboardTransport.TickServer();
            DikozemieServerPerf.Stop(
                DikozemieServerPerf.Section.Leaderboard, leaderboardStarted);
            ProcessPendingDisconnects();
            // The observer's bans, kicks and warnings (battle-sanctions.json).
            DikozemieBattleSanctions.Tick(Trace);
            ProcessPendingObservers();
            DikozemieBattlePresence.Tick(
                peer => peer.ControlledAgent != null && peer.ControlledAgent.IsActive(),
                peer => _observerPeers.Contains(peer));
            AdmitStandaloneSynchronizedPeers();
            ProcessPendingAdmissions(now);
            PruneDisconnectedPeers();
            NotifyQueuedPeers();

            // Agent removal is an engine-owned callback. Keep the mission live
            // long enough for the death animation and a short victory lap,
            // then publish the result without ending the arena mission.
            ProcessPendingTeamResult(now);
            ProcessPendingTrainingResult(now);
            ProcessPendingMatchResult(now);

            // The first knockout is still provisional. Do not respawn either
            // fighter or start another opponent before adjudicating both deaths.
            if (_pendingTeamResult || _pendingTrainingResult || _pendingMatchResult)
            {
                return;
            }

            // Between battles (waiting, result screen, empty arena) no horse
            // may stand around for the next visitor to take.
            if (!HasDuelPair || _duelCompleted)
            {
                SweepStrayMounts(now);
            }

            // Network message handlers run inside the engine's peer iteration.
            // Fading the surviving bot and changing the player's team from that
            // callback can invalidate native agent state (dedicated Wine server:
            // access violation 0xC0000005). Finish the old duel from a later
            // mission tick, then submit the already accepted request normally.
            if (_pendingNextDuelPeer != null)
            {
                if (now < _pendingNextDuelAt)
                {
                    return;
                }

                NetworkCommunicator nextPeer = _pendingNextDuelPeer;
                NetworkCommunicator[] followers = _pendingNextDuelFollowers.ToArray();
                _pendingNextDuelPeer = null;
                _pendingNextDuelAt = default;
                _pendingNextDuelFollowers.Clear();
                Trace("next duel cleanup begin peer=" + DescribePeer(nextPeer));
                ReleaseCompletedDuel();
                Trace("next duel cleanup complete peer=" + DescribePeer(nextPeer));
                RequestDuel(nextPeer);
                foreach (NetworkCommunicator follower in followers)
                {
                    RequestDuel(follower);
                }
                return;
            }

            if (!_visitorRequestedDuel)
            {
                TrySpawnArenaMaster();
                return;
            }

            // A completed duel is deliberately held for the result screen, but
            // a player can close the game or time out without pressing either
            // result button. Clear that abandoned pair before the completed
            // guard so it cannot block every later visitor on this server.
            if ((_activeAttacker != null || _activeDefender != null) && !HasDuelPair)
            {
                RequeueSurvivingDuelist();
                ClearActivePair(removeFromAdmission: false);
                _duelCompleted = false;
            }

            if (_duelCompleted)
            {
                // Hold the completed scene exactly as it is. The surviving
                // fighter stays in the arena until a player explicitly asks
                // for the next battle from the result screen - unless other
                // players queue on this server: they must not wait behind a
                // result screen that nobody closes.
                if (_duelCompletedAt == default)
                {
                    _duelCompletedAt = now;
                }
                else if (_pendingNextDuelPeer == null &&
                    now - _duelCompletedAt >= CompletedBattleHoldWhileQueued &&
                    _waitingPeers.Any(peer => IsReadyToFight(peer) &&
                        !IsCompletedBattleParticipant(peer)))
                {
                    Trace("completed battle released for queued players");
                    ReleaseCompletedDuel();
                    TryStartNextDuel();
                }
                return;
            }
            _duelCompletedAt = default;

            if (!HasDuelPair)
            {
                TryStartNextDuel();
            }

            if (_trainingDuel && HasDuelPair)
            {
                if (_teamBattle)
                {
                    TrySpawnTeamBattle(now);
                    // A participant can also leave the fight without a kill
                    // callback (disconnect, faded agent): re-check the sides.
                    if (_teamBotsSpawned)
                    {
                        EvaluateTeamOutcome(null);
                        CheckTeamStalemate(now);
                    }
                }
                else
                {
                    TrySpawnTrainingBot(now);
                }
            }
        }

        internal static void TickStandaloneAdmissionFromSubModule()
        {
            if (DikozemieArenaServerRegistration.UsesExternalMatchmaker ||
                Mission.Current == null)
            {
                return;
            }

            MissionMultiplayerDikozemieArena arena =
                Mission.Current.GetMissionBehavior<MissionMultiplayerDikozemieArena>();
            if (arena == null)
            {
                return;
            }

            foreach (NetworkCommunicator peer in GameNetwork.NetworkPeers.ToArray())
            {
                if (peer == null || peer.IsServerPeer || !peer.IsConnectionActive)
                {
                    continue;
                }

                // Add our representative while the peer is loading, but publish
                // the team only after synchronization. Bannerlord does not send a
                // peer's own pre-sync team state back to that peer (the server log
                // reports "Can't send the info ... to" itself), so assigning here
                // used to leave the client on the native team-selection screen.
                EnsureRepresentative(peer);
                if (peer.IsSynchronized)
                {
                    if (!arena._admittedPeers.Contains(peer))
                    {
                        Trace("submodule synchronized peer=" + DescribePeer(peer));
                    }
                    arena.AdmitPeer(peer);
                }
            }
        }

        private void AdmitStandaloneSynchronizedPeers()
        {
            if (DikozemieArenaServerRegistration.UsesExternalMatchmaker)
            {
                return;
            }

            // Dedicated custom servers do not consistently dispatch the game-mode
            // synchronization callback for third-party modes. Poll the authoritative
            // peer list so a valid player is admitted and assigned before the native
            // team-selection grace period expires.
            foreach (NetworkCommunicator peer in GameNetwork.NetworkPeers.ToArray())
            {
                if (peer == null || peer.IsServerPeer || !peer.IsSynchronized ||
                    !IsConnected(peer) || _admittedPeers.Contains(peer))
                {
                    continue;
                }

                EnsureRepresentative(peer);
                AdmitPeer(peer);
            }
        }

        protected override void AddRemoveMessageHandlers(
            GameNetwork.NetworkMessageHandlerRegistererContainer registerer)
        {
            if (GameNetwork.IsServer)
            {
                registerer.Register<DikozemieArenaProfileChunkMessage>(HandleProfileChunk);
                registerer.Register<DikozemieArenaDuelRequestMessage>(HandleDuelRequest);
            }
        }

        private bool HandleProfileChunk(
            NetworkCommunicator peer,
            DikozemieArenaProfileChunkMessage message)
        {
            Trace(
                "profile chunk received peer=" + DescribePeer(peer) +
                " part=" + (message.ChunkIndex + 1) + "/" +
                message.ChunkCount + " bytes=" +
                (message.Payload?.Length ?? 0));
            bool networkValid = DikozemieArenaProfileTransport.Receive(
                peer,
                message,
                out bool profileAccepted);
            if (profileAccepted &&
                DikozemieArenaServerRegistration.TryGetCharacterProfile(
                    peer,
                    out DikozemieArenaCharacterProfile profile))
            {
                if (_profileReadyPeers.Add(peer))
                {
                    Trace(
                        "profile ready peer=" + DescribePeer(peer) +
                        " hero=" + profile.CharacterId +
                        " character=" + profile.StartingCharacterId);
                }
                TryQueueProfileReadyPeer(peer);
            }
            return networkValid;
        }

        private bool HandleDuelRequest(
            NetworkCommunicator peer,
            DikozemieArenaDuelRequestMessage message)
        {
            if (_observerPeers.Contains(peer) || _pendingObservers.Contains(peer))
            {
                // An observer never asks for a fight; a stray request from
                // his client changes nothing. True keeps him connected.
                return true;
            }
            if (message.IsStartNowRequest)
            {
                // Only a flag here: battles start from the mission tick (see
                // TryQueueProfileReadyPeer). Always true: a false handler
                // result makes the engine drop the player's connection.
                if (IsConnected(peer) && !IsActiveDuelist(peer) &&
                    _startNowRequests.Add(peer))
                {
                    Trace("start now requested peer=" + DescribePeer(peer));
                }
                return true;
            }
            if (_duelCompleted && IsCompletedBattleParticipant(peer))
                DikozemieArenaServerRegistration.ClearCompletedDuelReservation(peer);
            if (!ApplyRequestedBattleSettings(peer, message))
            {
                Trace(
                    "next duel rejected invalid setup peer=" +
                    DescribePeer(peer) + " team=" + message.TeamSize +
                    " discipline=" + message.Discipline +
                    " composition=" + message.Composition);
                return false;
            }
            return RequestDuel(peer);
        }

        private static bool ApplyRequestedBattleSettings(
            NetworkCommunicator peer,
            DikozemieArenaDuelRequestMessage message)
        {
            if (message == null ||
                message.Discipline < (int)DikozemieArenaDiscipline.Foot ||
                message.Discipline > (int)DikozemieArenaDiscipline.Open ||
                message.Composition < (int)DikozemieArenaComposition.Mixed ||
                message.Composition > (int)DikozemieArenaComposition.Ranged)
            {
                return false;
            }

            var discipline =
                (DikozemieArenaDiscipline)message.Discipline;
            var composition =
                (DikozemieArenaComposition)message.Composition;
            return DikozemieArenaTeamRules.IsTeamSizeSupported(
                    message.TeamSize,
                    discipline) &&
                DikozemieArenaCompositionRules.IsSupported(
                    composition,
                    discipline) &&
                DikozemieArenaServerRegistration.UpdateRequestedBattleSettings(
                    peer,
                    message.TeamSize,
                    discipline,
                    composition);
        }

        private void ProcessPendingAdmissions(DateTimeOffset now)
        {
            foreach (KeyValuePair<NetworkCommunicator, DateTimeOffset> pending in
                _pendingAdmissions.ToArray())
            {
                NetworkCommunicator peer = pending.Key;
                if (!IsConnected(peer))
                {
                    _pendingAdmissions.Remove(peer);
                    continue;
                }

                if (!DikozemieArenaServerRegistration.UsesExternalMatchmaker)
                {
                    _pendingAdmissions.Remove(peer);
                    EnsureRepresentative(peer);
                    AdmitPeer(peer);
                    continue;
                }

                DikozemieArenaAdmissionState admission =
                    DikozemieArenaServerRegistration.GetAdmissionState(peer);
                if (admission == DikozemieArenaAdmissionState.Authorized)
                {
                    _pendingAdmissions.Remove(peer);
                    AdmitPeer(peer);
                }
                else if (admission == DikozemieArenaAdmissionState.Rejected ||
                    now >= pending.Value)
                {
                    RejectPeer(
                        peer,
                        admission == DikozemieArenaAdmissionState.Rejected
                            ? "player id is not reserved for this arena"
                            : "campaign profile was not received before timeout");
                }
            }
        }

        private void AdmitPeer(NetworkCommunicator peer)
        {
            if (!IsConnected(peer) || _admittedPeers.Contains(peer))
            {
                return;
            }

            // Bannerlord invokes HandleNewClientAfterSynchronized just before
            // it flips NetworkCommunicator.IsSynchronized. Broadcasting the
            // team in that narrow window excludes the joining peer, while
            // adding it to _admittedPeers prevents the post-sync retry. Wait
            // for the authoritative flag; the mission/submodule tick will
            // retry on the next frame.
            if (!peer.IsSynchronized)
            {
                _pendingAdmissions[peer] = DateTimeOffset.MaxValue;
                return;
            }

            MissionPeer missionPeer = peer.GetComponent<MissionPeer>();
            DikozemieArenaRepresentative representative =
                peer.GetComponent<DikozemieArenaRepresentative>();
            if (missionPeer == null || representative == null)
            {
                Trace(
                    "admit retry peer=" + DescribePeer(peer) +
                    " missionPeer=" + (missionPeer != null) +
                    " representative=" + (representative != null));
                if (!DikozemieArenaServerRegistration.UsesExternalMatchmaker)
                {
                    // Peer components can become visible one tick after the
                    // synchronization callback. Retry instead of scheduling a
                    // disconnect for a valid standalone player.
                    _pendingAdmissions[peer] = DateTimeOffset.MaxValue;
                    return;
                }
                RejectPeer(peer, "required synchronized peer components are missing");
                return;
            }

            _admittedPeers.Add(peer);
            Trace("admitted peer=" + DescribePeer(peer));

            if (DikozemieArenaServerRegistration.TryGetCharacterProfile(
                    peer,
                    out DikozemieArenaCharacterProfile admittedProfile))
            {
                _profileReadyPeers.Add(peer);
                Trace(
                    "profile already ready peer=" + DescribePeer(peer) +
                    " character=" + admittedProfile.StartingCharacterId);
            }

            bool isArenaVisitor = _arenaVisitor == null && !IsObserverPeer(peer);
            if (isArenaVisitor)
            {
                _arenaVisitor = peer;
                if (GameNetwork.IsDedicatedServer)
                {
                    // Clicking "Find battle" is already consent to queue. A
                    // dedicated server must not require another NPC dialogue.
                    _visitorRequestedDuel = true;
                }
            }
            // Native Duel requires a synchronized peer on an active team;
            // keeping it on Spectator triggers the join-grace disconnect.
            // The queue is consumed on the next tick and immediately starts a
            // rating-matched bot duel when there is no second human.
            // Use the native team-selection component even for an automatic
            // assignment. Setting MissionPeer.Team directly changes only the
            // server object; it does not publish SetPeerTeam/UpdateTeams or set
            // the peer culture, leaving the client stuck on team selection.
            // A replacement communicator can inherit Attacker from Bannerlord's
            // native reconnect cache. Never mutate that engine-owned list.
            // Force one public, synchronized transition through Spectator so
            // the fresh Attacker state is published to the joining peer.
            MultiplayerTeamSelectComponent.ChangeTeamServer(
                peer,
                Mission.SpectatorTeam);
            MultiplayerTeamSelectComponent.ChangeTeamServer(
                peer,
                Mission.Teams.Attacker);
            // Complete Bannerlord's normal visual-spawn handshake. Faking this
            // flag as true prevents CreateAgentVisuals from ever being sent.
            missionPeer.HasSpawnedAgentVisuals = false;
            missionPeer.SpawnTimer.Reset(Mission.CurrentTime, 0.1f);
            ChangeCurrentGoldForPeer(missionPeer, InitialGold);
            GameModeBaseClient.OnGoldAmountChangedForRepresentative(
                representative,
                InitialGold);
            DikozemieArenaRatingEntry rating = DikozemieArenaRatingStore.Get(peer);
            ChangeProgress(representative, 0, 0, rating.Rating, rating.Wins);
            TryQueueProfileReadyPeer(peer);

            foreach (var side in _scoreboard.Sides)
            {
                if (side == null)
                {
                    continue;
                }
                foreach (MissionPeer player in side.Players)
                {
                    DikozemieArenaRepresentative existing = player.GetNetworkPeer()
                        .GetComponent<DikozemieArenaRepresentative>();
                    if (existing != null)
                    {
                        SendProgress(peer, existing);
                    }
                }
            }

            Console.WriteLine(
                (isArenaVisitor
                    ? "DikozemieOnline: player entered the 3D arena lobby id="
                    : "DikozemieOnline: player entered 1v1 queue id=") +
                peer.PlayerConnectionInfo.PlayerID);
        }

        private bool IsObserverRequest(NetworkCommunicator peer)
        {
            if (!AsksToObserve(peer))
            {
                return false;
            }
            string id = peer.PlayerConnectionInfo.PlayerID.ToString();
            if (DikozemieBattleSanctions.IsObserver(id))
            {
                return true;
            }
            Trace("observer request refused: not an observer player=" + id);
            return false;
        }

        private static bool AsksToObserve(NetworkCommunicator peer) =>
            peer?.PlayerConnectionInfo != null &&
            DikozemieArenaServerRegistration.TryGetCharacterProfile(
                peer,
                out DikozemieArenaCharacterProfile profile) &&
            profile != null && profile.Observer;

        // An observer, or one who will be once his profile is read: never the
        // arena's lobby visitor (that body belongs to the first fighter).
        private bool IsObserverPeer(NetworkCommunicator peer) =>
            _observerPeers.Contains(peer) || _pendingObservers.Contains(peer) ||
            (AsksToObserve(peer) &&
             DikozemieBattleSanctions.IsObserver(peer.PlayerConnectionInfo.PlayerID.ToString()));

        // Every arena server keeps one place over its fighters for an
        // observer (ds_config MaxNumberOfPlayers = fighters + 1): a duel of two
        // can be watched, and the observer never takes a fighter's place. A
        // config of two (from before) keeps both places for fighters.
        // Clients search the same way (DikozemiePvpSearchController
        // .FighterSeats); an older client that comes anyway is put out with a
        // word instead of joining a full duel.
        internal const int ObserverSeats = 1;

        internal static int FighterSeats(int maxPlayers) =>
            maxPlayers <= 2 ? maxPlayers : maxPlayers - ObserverSeats;

        private bool HasFighterSeat(NetworkCommunicator peer)
        {
            int max = MultiplayerOptions.OptionType.MaxNumberOfPlayers.GetIntValue();
            if (max <= 0)
            {
                return true;
            }
            int taken = _seatedFighters.Count(seated => seated != peer && IsConnected(seated));
            return taken < FighterSeats(max);
        }

        // Mission tick only: a team change from a network message handler is
        // unsafe (see TryQueueProfileReadyPeer).
        private void ProcessPendingObservers()
        {
            _observerPeers.RemoveWhere(peer => !IsConnected(peer));
            if (_pendingObservers.Count == 0)
            {
                return;
            }
            foreach (NetworkCommunicator peer in _pendingObservers.ToArray())
            {
                _pendingObservers.Remove(peer);
                if (!IsConnected(peer) || !_observerPeers.Add(peer))
                {
                    continue;
                }
                RemoveQueuedPeer(peer);
                _seatedFighters.Remove(peer);
                _startNowRequests.Remove(peer);
                if (_arenaVisitor == peer)
                {
                    _arenaVisitor = null;
                }
                MultiplayerTeamSelectComponent.ChangeTeamServer(peer, Mission.SpectatorTeam);
                Trace("observer watches peer=" + DescribePeer(peer));
                try
                {
                    GameNetwork.BeginModuleEventAsServer(peer);
                    GameNetwork.WriteMessage(new NetworkMessages.FromServer.ServerMessage(
                        "Наблюдение: вас не видят, камера свободная. Бойцы этого сервера — в панели наблюдателя."));
                    GameNetwork.EndModuleEventAsServer();
                }
                catch (Exception error)
                {
                    Trace("observer notice failed: " + error.Message);
                }
            }
        }

        private void RemoveQueuedPeer(NetworkCommunicator peer)
        {
            if (!_waitingPeers.Contains(peer))
            {
                return;
            }
            NetworkCommunicator[] keep = _waitingPeers.Where(waiting => waiting != peer).ToArray();
            _waitingPeers.Clear();
            foreach (NetworkCommunicator waiting in keep)
            {
                _waitingPeers.Enqueue(waiting);
            }
        }

        private void TryQueueProfileReadyPeer(NetworkCommunicator peer)
        {
            if (_observerPeers.Contains(peer))
            {
                return;
            }
            if (IsConnected(peer) && _admittedPeers.Contains(peer) && IsObserverRequest(peer))
            {
                _pendingObservers.Add(peer);
                return;
            }
            if (!IsConnected(peer) || !_admittedPeers.Contains(peer) ||
                !_profileReadyPeers.Contains(peer) || IsActiveDuelist(peer) ||
                _waitingPeers.Contains(peer) ||
                (peer == _arenaVisitor && !_visitorRequestedDuel))
            {
                return;
            }
            if (DikozemieBattleSanctions.IsLeaving(peer))
            {
                return;
            }
            if (!_seatedFighters.Contains(peer))
            {
                if (!HasFighterSeat(peer))
                {
                    Trace("no fighter seat peer=" + DescribePeer(peer) +
                        " seated=" + _seatedFighters.Count);
                    if (_arenaVisitor == peer)
                    {
                        _arenaVisitor = null;
                    }
                    DikozemieBattleSanctions.PutOutPeer(
                        peer,
                        "Все места для бойцов на этом сервере заняты. Начните поиск снова: найдётся другая арена.",
                        Trace,
                        "no fighter seat");
                    return;
                }
                _seatedFighters.Add(peer);
            }

            _waitingPeers.Enqueue(peer);
            Trace("profile-ready peer queued=" + DescribePeer(peer));
            // The next mission tick starts the fight. This method also runs
            // from network message handlers, where fading agents and moving
            // teams is unsafe (dedicated Wine server: 0xC0000005).
        }

        private bool IsCompletedBattleParticipant(NetworkCommunicator peer) =>
            peer != null &&
            (peer == _activeAttacker || peer == _activeDefender ||
             (_teamBattle && _teamParticipantSides.ContainsKey(peer)));

        public bool RequestDuel(NetworkCommunicator peer)
        {
            bool resumesCompletedDuel = _duelCompleted &&
                IsCompletedBattleParticipant(peer);
            if (!GameNetwork.IsServer || !IsConnected(peer) ||
                !_admittedPeers.Contains(peer) ||
                (!resumesCompletedDuel && IsActiveDuelist(peer)) ||
                _waitingPeers.Contains(peer) || !_profileReadyPeers.Contains(peer))
            {
                return false;
            }

            if (resumesCompletedDuel)
            {
                if (_pendingNextDuelPeer != null)
                {
                    if (_pendingNextDuelPeer == peer)
                    {
                        return true;
                    }
                    // Team mates pressing "next battle" a moment later join
                    // the same gathering instead of being dropped.
                    _pendingNextDuelFollowers.Add(peer);
                    return true;
                }

                _pendingNextDuelPeer = peer;
                _pendingNextDuelAt = DateTimeOffset.UtcNow.AddMilliseconds(250);
                Trace("next duel accepted for deferred cleanup peer=" +
                    DescribePeer(peer));
                return true;
            }

            _visitorRequestedDuel = true;
            _seriesStates[peer] = CreateSeriesState(peer);
            if (_arenaMaster != null && _arenaMaster.State == AgentState.Active)
            {
                _arenaMaster.FadeOut(hideInstantly: false, hideMount: true);
            }
            _arenaMaster = null;

            var reordered = new Queue<NetworkCommunicator>();
            reordered.Enqueue(peer);
            while (_waitingPeers.Count > 0)
            {
                NetworkCommunicator waiting = _waitingPeers.Dequeue();
                if (waiting != peer)
                {
                    reordered.Enqueue(waiting);
                }
            }
            while (reordered.Count > 0)
            {
                _waitingPeers.Enqueue(reordered.Dequeue());
            }

            Console.WriteLine(
                "DikozemieOnline: arena master accepted 1v1 request id=" +
                peer.PlayerConnectionInfo?.PlayerID);
            // Started from the next mission tick, never from the message
            // handler that delivered this request (see TryQueueProfileReadyPeer).
            return true;
        }

        private void TrySpawnArenaMaster()
        {
            if (_arenaMaster != null && _arenaMaster.State == AgentState.Active)
            {
                return;
            }

            MissionPeer visitor = _arenaVisitor?.GetComponent<MissionPeer>();
            if (visitor?.ControlledAgent == null ||
                visitor.ControlledAgent.State != AgentState.Active)
            {
                return;
            }

            _arenaMaster = DikozemieArenaSpawningBehavior.Active?
                .SpawnArenaMaster(visitor.ControlledAgent);
        }

        private void TryStartNextDuel()
        {
            if (_duelCompleted || HasDuelPair)
            {
                return;
            }

            NetworkCommunicator first = TakeNextWaitingPeer();
            if (first == null)
            {
                _teamGatherDeadline = default;
                ClearNavalWaitingCrew();
                return;
            }

            int requestedTeamSize = ResolveRequestedTeamSize(first);
            bool navalBattle = ResolveRequestedNavalBattle(first) != null;
            if (!navalBattle)
            {
                ClearNavalWaitingCrew();
            }
            if (navalBattle || requestedTeamSize > 1)
            {
                if (!navalBattle)
                {
                    // While there are few people everybody queued plays together,
                    // in the largest format anyone asked for.
                    foreach (NetworkCommunicator waiting in _waitingPeers)
                    {
                        if (IsReadyToFight(waiting))
                        {
                            requestedTeamSize = Math.Max(
                                requestedTeamSize,
                                ResolveRequestedTeamSize(waiting));
                        }
                    }
                }
                List<NetworkCommunicator> squad =
                    GatherTeamBattlePlayers(first, requestedTeamSize);
                if (squad == null)
                {
                    RequeueAtFront(first);
                    return;
                }
                ClearPreviousBattleScene();
                StartTeamBattle(
                    squad,
                    requestedTeamSize,
                    ResolveRequestedDiscipline(first),
                    ResolveRequestedComposition(first));
                return;
            }
            _teamGatherDeadline = default;

            NetworkCommunicator second = TakeCompatibleWaitingPeer(first);
            if (second == null && HasReservedOpponent(first))
            {
                // An accepted invitation must never become a bot duel or be
                // stolen by an unrelated visitor. Native join can take time.
                _waitingPeers.Enqueue(first);
                return;
            }

            if (second == null && RequestsImmediateBattle(first))
            {
                _humanOpponentWaitDeadlines.Remove(first);
                ClearPreviousBattleScene();
                StartTrainingDuel(first);
                return;
            }

            if (second == null && _startNowRequests.Remove(first))
            {
                Trace("start now accepted: bot duel peer=" + DescribePeer(first));
                _humanOpponentWaitDeadlines.Remove(first);
                ClearPreviousBattleScene();
                StartTrainingDuel(first);
                return;
            }

            if (second == null)
            {
                bool startedWaiting =
                    !_humanOpponentWaitDeadlines.ContainsKey(first);
                DateTimeOffset waitDeadline =
                    GetHumanOpponentWaitDeadline(first);
                if (DateTimeOffset.UtcNow < waitDeadline)
                {
                    _waitingPeers.Enqueue(first);
                    if (startedWaiting)
                    {
                        Trace(
                            "waiting up to 60 seconds for human opponent peer=" +
                            DescribePeer(first));
                    }
                    return;
                }

                // Preserve the rating-matched bot ladder when nobody else
                // joins. The one-minute window is long enough for a second
                // computer to load the same custom server without leaving a
                // lone player trapped forever.
                _humanOpponentWaitDeadlines.Remove(first);
                ClearPreviousBattleScene();
                StartTrainingDuel(first);
                return;
            }

            _humanOpponentWaitDeadlines.Remove(first);
            _humanOpponentWaitDeadlines.Remove(second);
            // A live opponent came after all: that beats any bot.
            _startNowRequests.Remove(first);
            _startNowRequests.Remove(second);
            AlignPairDiscipline(first, second);
            ClearPreviousBattleScene();
            DespawnIdleVisitor(first, second);
            _activeAttacker = first;
            _activeDefender = second;
            _trainingDuel = false;
            _botRoundWins = 0;
            PrepareDuelist(first, Mission.Teams.Attacker);
            PrepareDuelist(second, Mission.Teams.Defender);
            Console.WriteLine(
                "DikozemieOnline: starting queued 1v1 " + first.UserName +
                " vs " + second.UserName);
        }

        private static bool RequestsImmediateBattle(NetworkCommunicator peer) =>
            DikozemieArenaServerRegistration.TryGetCharacterProfile(
                peer,
                out DikozemieArenaCharacterProfile profile) &&
            profile.RequestsImmediateBattle;

        private DateTimeOffset GetHumanOpponentWaitDeadline(
            NetworkCommunicator peer)
        {
            if (!_humanOpponentWaitDeadlines.TryGetValue(
                    peer,
                    out DateTimeOffset deadline))
            {
                deadline = DateTimeOffset.UtcNow.Add(HumanOpponentWaitTimeout);
                _humanOpponentWaitDeadlines[peer] = deadline;
            }
            return deadline;
        }

        private void ClearPreviousBattleScene()
        {
            // This runs only from the normal mission tick, after completed
            // fighters have been released.  Do not move native scene cleanup
            // into OnAgentRemoved: mounted-agent teardown from that callback is
            // unsafe and has previously crashed TaleWorlds.Native.dll.
            Trace("next battle scene cleanup begin");
            foreach (Agent mount in Mission.Agents.Where(IsStrayMount).ToArray())
            {
                // A riderless survivor is not a corpse, so ClearCorpses does
                // not cover it. Fade only; never combine this with MakeDead.
                _ownMountRiders.Remove(mount);
                mount.FadeOut(hideInstantly: true, hideMount: false);
            }
            Mission.ClearCorpses(false);
            Mission.RemoveSpawnedItemsAndMissiles();
            Trace("next battle scene cleanup complete");
        }

        // Called by the spawning behaviour for every hero that enters on his
        // own horse, so the horse is not taken for a stray when he steps down.
        internal void RegisterOwnMount(Agent rider)
        {
            if (rider?.MountAgent != null)
            {
                _ownMountRiders[rider.MountAgent] = rider;
            }
        }

        private bool IsStrayMount(Agent agent)
        {
            if (agent == null || !agent.IsMount || agent.RiderAgent != null ||
                agent.State != AgentState.Active)
            {
                return false;
            }
            return !(_ownMountRiders.TryGetValue(agent, out Agent owner) &&
                owner != null && owner.State == AgentState.Active);
        }

        // Mission tick only (see ClearPreviousBattleScene), once a second.
        private void SweepStrayMounts(DateTimeOffset now)
        {
            if (now < _nextStrayMountSweepAt)
            {
                return;
            }
            _nextStrayMountSweepAt = now.AddSeconds(1);
            foreach (Agent gone in _ownMountRiders.Keys
                         .Where(mount => mount == null ||
                             mount.State != AgentState.Active)
                         .ToArray())
            {
                _ownMountRiders.Remove(gone);
            }

            Agent[] strays = Mission.Agents.Where(IsStrayMount).ToArray();
            if (strays.Length == 0)
            {
                return;
            }
            foreach (Agent mount in strays)
            {
                _ownMountRiders.Remove(mount);
                mount.FadeOut(hideInstantly: true, hideMount: false);
            }
            Trace("stray horses removed count=" + strays.Length +
                " battle=" + (HasDuelPair ? "completed" : "none"));
        }

        // Collects every queued live player who asked for a team (or naval)
        // battle on this server, up to both sides' places. Returns null while
        // the gathering window is still open and places remain.
        private List<NetworkCommunicator> GatherTeamBattlePlayers(
            NetworkCommunicator first,
            int requestedTeamSize)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            int perSide = ResolveRequestedNavalBattle(first) != null
                ? Math.Max(1, requestedTeamSize)
                : Math.Max(2, requestedTeamSize);
            int capacity = perSide * 2;
            var squad = new List<NetworkCommunicator> { first };
            int headRating = ResolvePlayerRating(first);
            // Stage 5: people who must fight together (a party that took a
            // fight on the road, two parties that met there) carry one group
            // id. A group fights alone, whatever the ratings; nobody else
            // joins its battle and its members join nobody else's.
            string group = ResolveBattleGroup(first);
            foreach (NetworkCommunicator candidate in _waitingPeers)
            {
                if (squad.Count >= capacity)
                {
                    break;
                }
                if (squad.Contains(candidate) || !IsReadyToFight(candidate) ||
                    !DikozemieArenaTeamRules.SameBattleGroup(group, ResolveBattleGroup(candidate)) ||
                    string.IsNullOrEmpty(group) && !IsWithinPlayerRatingWindow(headRating, candidate))
                {
                    continue;
                }
                squad.Add(candidate);
            }
            _gatherPlayers = squad.Count;
            _gatherCapacity = capacity;
            if (!string.IsNullOrEmpty(group))
            {
                int expected = ResolveBattleGroupSize(first);
                if (!_groupDeadlines.TryGetValue(group, out DateTimeOffset groupDeadline))
                {
                    groupDeadline = now.Add(GroupGatherWindow);
                    _groupDeadlines[group] = groupDeadline;
                    Trace("battle group gathering group=" + group + " expected=" +
                        expected + " head=" + DescribePeer(first));
                }
                if (DikozemieArenaTeamRules.IsBattleGroupWaiting(
                        squad.Count, expected, now < groupDeadline))
                {
                    return null;
                }
                _groupDeadlines.Remove(group);
                _teamGatherDeadline = default;
                foreach (NetworkCommunicator gathered in squad)
                {
                    _startNowRequests.Remove(gathered);
                    DikozemieArenaServerRegistration.ClearBattleGroup(gathered);
                }
                var others = _waitingPeers.Where(peer => !squad.Contains(peer)).ToArray();
                _waitingPeers.Clear();
                foreach (NetworkCommunicator peer in others)
                {
                    _waitingPeers.Enqueue(peer);
                }
                Trace("battle group complete group=" + group + " players=" + squad.Count + "/" +
                    expected + " format=" + perSide + "x" + perSide);
                return squad;
            }

            if (_teamGatherDeadline == default)
            {
                _teamGatherDeadline = now.Add(
                    ResolveRequestedNavalBattle(first) != null
                        ? NavalGatherWindow
                        : TeamGatherWindow);
                Trace(
                    "team gathering started head=" + DescribePeer(first) +
                    " format=" + perSide + "x" + perSide +
                    " players=" + squad.Count + "/" + capacity);
                // The player waits on the ship he chose, not on the default one.
                DikozemieNavalBattleDefinition chosenShip =
                    ResolveRequestedNavalBattle(first);
                DikozemieNavalBoardingBehavior gatheringBoarding =
                    DikozemieNavalBoardingBehavior.Active;
                if (chosenShip != null && gatheringBoarding != null)
                {
                    gatheringBoarding.StartsDocked = false;
                    gatheringBoarding.SelectHull(chosenShip);
                    gatheringBoarding.Prepare(
                        chosenShip.ShipClass,
                        broadcast: true);
                    SpawnNavalWaitingCrew(first, chosenShip);
                }
            }
            // Anyone already gathered may stop the wait: the people who are
            // here play, the empty places go to bots.
            NetworkCommunicator startedNow =
                squad.FirstOrDefault(_startNowRequests.Contains);
            if (squad.Count < capacity && now < _teamGatherDeadline &&
                !RequestsImmediateBattle(first) && startedNow == null)
            {
                return null;
            }

            _teamGatherDeadline = default;
            foreach (NetworkCommunicator gathered in squad)
            {
                _startNowRequests.Remove(gathered);
            }
            var remaining = _waitingPeers.Where(peer => !squad.Contains(peer)).ToArray();
            _waitingPeers.Clear();
            foreach (NetworkCommunicator peer in remaining)
            {
                _waitingPeers.Enqueue(peer);
            }
            Trace(
                "team gathering complete format=" + perSide + "x" + perSide +
                " players=" + squad.Count + "/" + capacity +
                (startedNow != null
                    ? " startedNowBy=" + DescribePeer(startedNow)
                    : string.Empty));
            return squad;
        }

        private void SpawnNavalWaitingCrew(
            NetworkCommunicator head,
            DikozemieNavalBattleDefinition ship)
        {
            ClearNavalWaitingCrew();
            ClearPreviousBattleScene();
            DikozemieArenaSpawningBehavior spawning =
                DikozemieArenaSpawningBehavior.Active;
            DikozemieArenaTeamField field = null;
            IReadOnlyList<Agent> crew = spawning?.SpawnTeamBots(
                Math.Max(1, ship.CrewCapacity),
                ResolveRequestedDiscipline(head),
                ResolveRequestedComposition(head),
                _botHealthMultiplier,
                new List<Agent>(),
                new List<Agent>(),
                ResolvePlayerRating(head),
                0,
                out field);
            if (crew == null || crew.Count == 0)
            {
                return;
            }
            _navalWaitingCrew.AddRange(crew);
            _navalWaitingField = field;
            _navalWaitingHull = ship;
            spawning.HoldNavalCrewsForBoarding();
            Trace("naval crews wait on deck count=" + crew.Count);
        }

        private void ClearNavalWaitingCrew()
        {
            foreach (Agent bot in _navalWaitingCrew)
            {
                if (bot != null && bot.State == AgentState.Active)
                {
                    bot.FadeOut(hideInstantly: true, hideMount: true);
                }
            }
            _navalWaitingCrew.Clear();
            _navalWaitingField = null;
            _navalWaitingHull = null;
        }

        // Live players take bots' places on their ship. Returns null when the
        // waiting crews no longer fit this battle, which then spawns fresh ones.
        private IReadOnlyList<Agent> AdoptNavalWaitingCrew(
            int attackerHumans,
            int defenderHumans,
            out DikozemieArenaTeamField field)
        {
            field = _navalWaitingField;
            bool fits = _navalBattle != null &&
                ReferenceEquals(_navalWaitingHull, _navalBattle) &&
                _navalWaitingCrew.Count == _teamSize * 2 &&
                _navalWaitingCrew.All(bot =>
                    bot != null && bot.State == AgentState.Active);
            if (!fits)
            {
                ClearNavalWaitingCrew();
                return null;
            }
            int freeAttackers = attackerHumans;
            int freeDefenders = defenderHumans;
            var kept = new List<Agent>();
            foreach (Agent bot in _navalWaitingCrew)
            {
                bool attacker = bot.Team == Mission.AttackerTeam;
                if (attacker && freeAttackers > 0)
                {
                    freeAttackers--;
                    bot.FadeOut(hideInstantly: true, hideMount: true);
                }
                else if (!attacker && freeDefenders > 0)
                {
                    freeDefenders--;
                    bot.FadeOut(hideInstantly: true, hideMount: true);
                }
                else
                {
                    kept.Add(bot);
                }
            }
            _navalWaitingCrew.Clear();
            _navalWaitingField = null;
            _navalWaitingHull = null;
            return kept;
        }

        private bool IsReadyToFight(NetworkCommunicator peer) =>
            IsConnected(peer) && _admittedPeers.Contains(peer) &&
            _profileReadyPeers.Contains(peer) &&
            peer != _activeAttacker && peer != _activeDefender;

        private void RequeueAtFront(NetworkCommunicator peer)
        {
            NetworkCommunicator[] rest = _waitingPeers
                .Where(waiting => waiting != peer)
                .ToArray();
            _waitingPeers.Clear();
            _waitingPeers.Enqueue(peer);
            foreach (NetworkCommunicator waiting in rest)
            {
                _waitingPeers.Enqueue(waiting);
            }
        }

        // The idle lobby visitor may walk the empty arena; once a battle that
        // does not include it starts, its hero must leave the battlefield.
        private void DespawnIdleVisitor(params NetworkCommunicator[] fighters)
        {
            NetworkCommunicator visitor = _arenaVisitor;
            if (visitor == null || fighters.Contains(visitor))
            {
                return;
            }
            Agent agent = visitor.GetComponent<MissionPeer>()?.ControlledAgent;
            if (agent != null && agent.State == AgentState.Active)
            {
                agent.FadeOut(hideInstantly: true, hideMount: true);
                Trace("idle visitor left the battlefield peer=" + DescribePeer(visitor));
            }
        }

        private void StartTrainingDuel(NetworkCommunicator player)
        {
            DespawnIdleVisitor(player);
            _navalBattle = null;
            _navalFriendlyCaptain = null;
            _navalEnemyCaptain = null;
            _activeAttacker = player;
            _activeDefender = null;
            _trainingDuel = true;
            _teamBattle = false;
            _teamSize = 1;
            ResetTeamSides();
            _teamDiscipline = ResolveRequestedDiscipline(player);
            _teamComposition = ResolveRequestedComposition(player);
            _activeBot = null;
            _activeBotCharacterId = string.Empty;
            _activeBotRating = 0;
            _botRoundWins = 0;
            _reportedTrainingPlayerNotSpawned = false;
            _nextTrainingBotSpawnAt = DateTimeOffset.UtcNow;
            PrepareDuelist(player, Mission.Teams.Attacker);
            Trace(
                "starting rating-matched bot duel peer=" + DescribePeer(player));
            Console.WriteLine(
                "DikozemieOnline: no human opponent; starting 1v1 training bot for " +
                player.UserName);
        }

        private void StartTeamBattle(
            IReadOnlyList<NetworkCommunicator> squad,
            int requestedTeamSize,
            DikozemieArenaDiscipline discipline,
            DikozemieArenaComposition composition)
        {
            NetworkCommunicator lead = squad[0];
            _navalBattle = ResolveRequestedNavalBattle(lead);
            _navalFriendlyCaptain = null;
            _navalEnemyCaptain = null;
            _activeDefender = null;
            _trainingDuel = true;
            _teamBattle = true;
            _teamSize = _navalBattle != null
                ? Math.Max(1, _navalBattle.CrewCapacity)
                : Math.Max(
                    2,
                    DikozemieArenaTeamRules.NormalizeTeamSize(
                        requestedTeamSize,
                        discipline));
            _teamSize = Math.Max(_teamSize, (squad.Count + 1) / 2);
            _teamDiscipline = discipline;
            _teamComposition = composition;
            _teamPlayerStartsAtAttackerWall =
                _nextTeamPlayerStartsAtAttackerWall;
            _nextTeamPlayerStartsAtAttackerWall =
                !_nextTeamPlayerStartsAtAttackerWall;
            _teamBotsSpawned = false;
            _activeTeamBots.Clear();
            _rewardedTeamVictims.Clear();
            _deathSummarySentPeers.Clear();
            ResetTeamParticipants();
            _teamField = new DikozemieArenaTeamField();
            _activeBot = null;
            _activeBotCharacterId = string.Empty;
            _activeBotRating = 0;
            _botRoundWins = 0;
            _reportedTrainingPlayerNotSpawned = false;
            _nextTrainingBotSpawnAt = DateTimeOffset.UtcNow;
            _teamSpawnDeadline = DateTimeOffset.UtcNow.Add(TeamSpawnGrace);

            // Live players are split over both sides by rating; bots fill the
            // remaining places once every hero stands in the arena.
            bool[] attackerSide = DikozemieArenaTeamRules.AssignHumanSides(
                squad.Select(ResolvePlayerRating).ToList(),
                _teamSize,
                squad.Select(ResolvePartyKey).ToList());
            for (int index = 0; index < squad.Count; index++)
            {
                NetworkCommunicator peer = squad[index];
                _teamParticipants.Add(peer);
                _teamParticipantSides[peer] = attackerSide[index]
                    ? BattleSideEnum.Attacker
                    : BattleSideEnum.Defender;
                _humanOpponentWaitDeadlines.Remove(peer);
                if (discipline == DikozemieArenaDiscipline.Foot &&
                    peer != lead &&
                    ResolveRequestedDiscipline(peer) != DikozemieArenaDiscipline.Foot &&
                    DikozemieArenaServerRegistration.TryGetCharacterProfile(
                        peer,
                        out DikozemieArenaCharacterProfile profile))
                {
                    // A foot battle has no riders, whatever each player chose.
                    DikozemieArenaServerRegistration.UpdateRequestedBattleSettings(
                        peer,
                        profile.RequestedTeamSize,
                        DikozemieArenaDiscipline.Foot,
                        (DikozemieArenaComposition)profile.RequestedComposition);
                }
            }
            _activeAttacker = _teamParticipants.First(peer =>
                _teamParticipantSides[peer] == BattleSideEnum.Attacker);
            if (_navalBattle == null)
            {
                ResolveTeamSides();
            }
            DespawnIdleVisitor(squad.ToArray());
            if (_navalBattle != null)
            {
                DikozemieNavalBoardingBehavior boarding =
                    DikozemieNavalBoardingBehavior.Active;
                if (boarding != null)
                {
                    boarding.StartsDocked = _teamParticipants.Any(peer =>
                        _teamParticipantSides[peer] == BattleSideEnum.Defender);
                    boarding.SelectHull(_navalBattle);
                    boarding.Prepare(_navalBattle.ShipClass, broadcast: true);
                }
            }
            foreach (NetworkCommunicator peer in _teamParticipants)
            {
                PrepareDuelist(
                    peer,
                    _teamParticipantSides[peer] == BattleSideEnum.Attacker
                        ? Mission.Teams.Attacker
                        : Mission.Teams.Defender);
            }
            Trace(
                "starting team battle lead=" + DescribePeer(lead) +
                " format=" + _teamSize + "x" + _teamSize +
                " humans=" + string.Join(",", _teamParticipants.Select(peer =>
                    peer.UserName + ":" + _teamParticipantSides[peer])) +
                " playerWall=" + (_teamPlayerStartsAtAttackerWall
                    ? "attacker"
                    : "defender") +
                " discipline=" + _teamDiscipline +
                " composition=" + _teamComposition);
        }

        private void ResetTeamSides()
        {
            _teamAttackerSize = 0;
            _teamDefenderSize = 0;
            _teamAttackerRating = 0;
            _teamDefenderRating = 0;
        }

        // A siege: every hero tells the size and strength of his side and of
        // the other; the larger word wins. A side nobody of the players
        // stands on (a lord's garrison) is named by the other side's heroes.
        private void ResolveTeamSides()
        {
            ResetTeamSides();
            foreach (NetworkCommunicator peer in _teamParticipants)
            {
                if (!DikozemieArenaServerRegistration.TryGetCharacterProfile(
                        peer,
                        out DikozemieArenaCharacterProfile profile))
                {
                    continue;
                }
                bool attacker = _teamParticipantSides[peer] == BattleSideEnum.Attacker;
                int ownSize = Math.Min(DikozemieArenaSceneRules.MaximumImperialArenaTeamSize, profile.BattleSideSize);
                int foeSize = Math.Min(DikozemieArenaSceneRules.MaximumImperialArenaTeamSize, profile.BattleFoeSize);
                if (attacker)
                {
                    _teamAttackerSize = Math.Max(_teamAttackerSize, ownSize);
                    _teamDefenderSize = Math.Max(_teamDefenderSize, foeSize);
                    _teamAttackerRating = Math.Max(_teamAttackerRating, profile.BattleSideRating);
                    _teamDefenderRating = Math.Max(_teamDefenderRating, profile.BattleFoeRating);
                }
                else
                {
                    _teamDefenderSize = Math.Max(_teamDefenderSize, ownSize);
                    _teamAttackerSize = Math.Max(_teamAttackerSize, foeSize);
                    _teamDefenderRating = Math.Max(_teamDefenderRating, profile.BattleSideRating);
                    _teamAttackerRating = Math.Max(_teamAttackerRating, profile.BattleFoeRating);
                }
                DikozemieArenaServerRegistration.ClearBattleSides(peer);
            }
            if (_teamAttackerSize <= 0 && _teamDefenderSize <= 0)
            {
                return;
            }
            int attackersHere = _teamParticipants.Count(peer =>
                _teamParticipantSides[peer] == BattleSideEnum.Attacker);
            int defendersHere = _teamParticipants.Count - attackersHere;
            _teamAttackerSize = Math.Max(Math.Max(1, attackersHere),
                _teamAttackerSize > 0 ? _teamAttackerSize : _teamSize);
            _teamDefenderSize = Math.Max(Math.Max(1, defendersHere),
                _teamDefenderSize > 0 ? _teamDefenderSize : _teamSize);
            _teamSize = Math.Max(_teamAttackerSize, _teamDefenderSize);
            Trace("team battle sides attackers=" + _teamAttackerSize + " rating=" + _teamAttackerRating +
                " defenders=" + _teamDefenderSize + " rating=" + _teamDefenderRating);
        }

        private int AttackerSideSize => _teamAttackerSize > 0 ? _teamAttackerSize : _teamSize;

        private int DefenderSideSize => _teamDefenderSize > 0 ? _teamDefenderSize : _teamSize;

        private void ResetTeamParticipants()
        {
            _teamParticipants.Clear();
            _teamParticipantSides.Clear();
            _teamEliminatedPeers.Clear();
            _teamHumanAgents.Clear();
            _teamPersonalKills.Clear();
            _teamKillRatingEarned.Clear();
            _teamSpawnDeadline = default;
        }

        private Team GetParticipantTeam(NetworkCommunicator peer) =>
            _teamParticipantSides.TryGetValue(peer, out BattleSideEnum side)
                ? side == BattleSideEnum.Attacker
                    ? Mission.AttackerTeam
                    : Mission.DefenderTeam
                : null;

        private static Agent GetActiveAgent(NetworkCommunicator peer)
        {
            Agent agent = peer?.GetComponent<MissionPeer>()?.ControlledAgent;
            return agent != null && agent.State == AgentState.Active
                ? agent
                : null;
        }

        private void TrySpawnTeamBattle(DateTimeOffset now)
        {
            if (_pendingTeamResult || _duelCompleted || _teamBotsSpawned ||
                now < _nextTrainingBotSpawnAt)
            {
                return;
            }

            // Every connected hero must stand in the arena before the bots
            // appear, but a client that stalls does not hold the others.
            var ready = new List<NetworkCommunicator>();
            var missing = new List<NetworkCommunicator>();
            foreach (NetworkCommunicator peer in _teamParticipants)
            {
                if (!IsConnected(peer) || _teamEliminatedPeers.Contains(peer))
                {
                    continue;
                }
                if (GetActiveAgent(peer) != null)
                {
                    ready.Add(peer);
                }
                else
                {
                    missing.Add(peer);
                }
            }
            if (missing.Count > 0 && (ready.Count == 0 || now < _teamSpawnDeadline))
            {
                if (!_reportedTrainingPlayerNotSpawned)
                {
                    _reportedTrainingPlayerNotSpawned = true;
                    Trace("team battle is waiting for player spawn missing=" +
                        string.Join(",", missing.Select(DescribePeer)));
                }
                return;
            }
            foreach (NetworkCommunicator late in missing)
            {
                // The late player watches from the queue and takes the next
                // battle instead of appearing behind the lines mid-fight.
                _teamParticipants.Remove(late);
                _teamParticipantSides.Remove(late);
                MultiplayerTeamSelectComponent.ChangeTeamServer(
                    late,
                    Mission.SpectatorTeam);
                _waitingPeers.Enqueue(late);
                Trace("team player did not spawn in time; requeued peer=" +
                    DescribePeer(late));
            }
            PromoteTeamLead();

            var attackerAgents = new List<Agent>();
            var defenderAgents = new List<Agent>();
            long attackerRating = 0;
            long defenderRating = 0;
            foreach (NetworkCommunicator peer in ready)
            {
                Agent agent = GetActiveAgent(peer);
                _teamHumanAgents[peer] = agent;
                if (_teamParticipantSides[peer] == BattleSideEnum.Attacker)
                {
                    attackerAgents.Add(agent);
                    attackerRating += ResolvePlayerRating(peer);
                }
                else
                {
                    defenderAgents.Add(agent);
                    defenderRating += ResolvePlayerRating(peer);
                }
            }

            DikozemieArenaSpawningBehavior spawning =
                DikozemieArenaSpawningBehavior.Active;
            if (spawning != null)
            {
                spawning.PlayerStartsAtAttackerWall =
                    _teamPlayerStartsAtAttackerWall;
            }
            DikozemieArenaTeamField field = null;
            IReadOnlyList<Agent> bots = AdoptNavalWaitingCrew(
                attackerAgents.Count,
                defenderAgents.Count,
                out field);
            if (bots != null &&
                DikozemieNavalBoardingBehavior.Active?.StartsDocked == true)
            {
                spawning?.PlaceWaitingEnemyCrew(bots);
            }
            bots = bots ?? spawning?.SpawnTeamBots(
                _teamSize,
                _teamDiscipline,
                _teamComposition,
                _botHealthMultiplier,
                attackerAgents,
                defenderAgents,
                attackerRating,
                defenderRating,
                out field,
                _teamAttackerSize,
                _teamDefenderSize,
                _teamAttackerRating,
                _teamDefenderRating);
            int expectedBots =
                Math.Max(0, AttackerSideSize - attackerAgents.Count) +
                Math.Max(0, DefenderSideSize - defenderAgents.Count);
            if (bots == null || (bots.Count == 0 && expectedBots > 0))
            {
                _nextTrainingBotSpawnAt = now.AddSeconds(1);
                return;
            }

            _teamField = field ?? new DikozemieArenaTeamField();
            _activeBotCharacterId = _teamField.DefenderRepresentativeId;
            _activeBotRating = _teamField.DefenderRepresentativeRating;
            _activeTeamBots.AddRange(bots);
            _activeBot = _activeTeamBots.FirstOrDefault(
                bot => bot?.Team == Mission.DefenderTeam);
            if (_navalBattle != null)
            {
                _navalFriendlyCaptain = _activeTeamBots.FirstOrDefault(
                    bot => bot?.Team == Mission.AttackerTeam &&
                        bot.MissionPeer == null);
                _navalEnemyCaptain = _activeTeamBots.FirstOrDefault(
                    bot => bot?.Team == Mission.DefenderTeam &&
                        bot.MissionPeer == null);
                Trace(
                    "naval crew captains assigned hull=" +
                    _navalBattle.HullId +
                    " missionShip=" + _navalBattle.MissionShipId +
                    " crew=" + _teamSize +
                    " friendly=" +
                    (_navalFriendlyCaptain?.Character?.StringId ?? "none") +
                    " enemy=" +
                    (_navalEnemyCaptain?.Character?.StringId ?? "none"));
                DikozemieNavalBoardingBehavior.Active?.StartApproach(
                    broadcast: true);
            }
            _pendingTrainingPlayerAgent = GetActiveAgent(_activeAttacker);
            _teamBotsSpawned = true;
            _teamProgressAt = DateTimeOffset.UtcNow;
            _teamAliveKey = -1;
            _teamStalemateNudged = false;
            DikozemieArenaSpawningBehavior.Active?.EndCloseIn();
            Trace(
                "team battle ready format=" + _teamSize + "x" + _teamSize +
                " humans=" + attackerAgents.Count + "+" + defenderAgents.Count +
                " spawnedBots=" + _activeTeamBots.Count);
        }

        private void PromoteTeamLead()
        {
            if (!_teamBattle ||
                (_activeAttacker != null && IsConnected(_activeAttacker) &&
                 _teamParticipantSides.ContainsKey(_activeAttacker)))
            {
                return;
            }
            _activeAttacker = _teamParticipants.FirstOrDefault(peer =>
                    IsConnected(peer) &&
                    _teamParticipantSides[peer] == BattleSideEnum.Attacker) ??
                _teamParticipants.FirstOrDefault(IsConnected);
        }

        private void HandleTeamParticipantDisconnect(NetworkCommunicator peer)
        {
            _teamEliminatedPeers.Add(peer);
            _pendingNextDuelFollowers.Remove(peer);
            if (_teamHumanAgents.TryGetValue(peer, out Agent agent) &&
                agent != null && agent.State == AgentState.Active)
            {
                agent.FadeOut(hideInstantly: true, hideMount: true);
            }
            Trace("team participant left peer=" + DescribePeer(peer));
            if (!_teamParticipants.Any(IsConnected))
            {
                ClearActivePair(removeFromAdmission: false);
                _duelCompleted = false;
                return;
            }
            PromoteTeamLead();
        }

        private void TrySpawnTrainingBot(DateTimeOffset now)
        {
            if (_pendingTrainingResult || _duelCompleted)
            {
                return;
            }

            if (_activeBot != null && _activeBot.State == AgentState.Active)
            {
                return;
            }
            _activeBot = null;
            if (now < _nextTrainingBotSpawnAt)
            {
                return;
            }

            MissionPeer player = _activeAttacker?.GetComponent<MissionPeer>();
            if (player?.ControlledAgent == null ||
                player.ControlledAgent.State != AgentState.Active)
            {
                if (!_reportedTrainingPlayerNotSpawned)
                {
                    _reportedTrainingPlayerNotSpawned = true;
                    Trace(
                        "bot duel is waiting for player spawn peer=" +
                        DescribePeer(_activeAttacker) +
                        " missionPeer=" + (player != null) +
                        " team=" + player?.Team?.Side +
                        " selectedTroop=" + player?.SelectedTroopIndex +
                        " visuals=" + player?.HasSpawnedAgentVisuals);
                }
                return;
            }

            DikozemieArenaSpawningBehavior spawning =
                DikozemieArenaSpawningBehavior.Active;
            int playerRating = ResolvePlayerRating(_activeAttacker);
            _activeBot = spawning?.SpawnTrainingBot(
                _botHealthMultiplier,
                playerRating,
                _teamDiscipline,
                _teamComposition,
                out _activeBotCharacterId,
                out _activeBotRating);
            if (_activeBot != null)
            {
                _pendingTrainingPlayerAgent = player.ControlledAgent;
                Trace(
                    "ranked bot spawned character=" + _activeBotCharacterId +
                    " botRating=" + _activeBotRating +
                    " playerRating=" + playerRating + " health=" +
                    _activeBot.HealthLimit.ToString("0"));
                Console.WriteLine(
                    "DikozemieOnline: ranked bot spawned character=" +
                    _activeBotCharacterId + " botRating=" + _activeBotRating +
                    " playerRating=" + playerRating + " health=" +
                    _activeBot.HealthLimit.ToString("0"));
            }
            else
            {
                _nextTrainingBotSpawnAt = now.AddSeconds(1);
            }
        }

        // Stage 5: the battle group a player belongs to (empty: none).
        private static string ResolveBattleGroup(NetworkCommunicator peer) =>
            DikozemieArenaServerRegistration.TryGetCharacterProfile(peer, out var profile)
                ? profile.BattleGroupId ?? string.Empty
                : string.Empty;

        private static int ResolveBattleGroupSize(NetworkCommunicator peer) =>
            DikozemieArenaServerRegistration.TryGetCharacterProfile(peer, out var profile)
                ? Math.Max(1, profile.BattleGroupSize)
                : 1;

        private static bool HasReservedOpponent(NetworkCommunicator peer)
        {
            return DikozemieArenaServerRegistration.TryGetCharacterProfile(peer, out var profile) &&
                !string.IsNullOrEmpty(profile.DuelOfferId);
        }

        private NetworkCommunicator TakeCompatibleWaitingPeer(NetworkCommunicator first)
        {
            // Prefer an opponent who chose the same stance, but never leave a
            // second live player spectating only because the stances differ:
            // the lobby sends every 1x1 seeker to the same imperial arenas
            // (live test 2026-10-27: Open vs Foot left one player flying).
            return TakeWaitingOpponent(first, requireSameDiscipline: true) ??
                TakeWaitingOpponent(first, requireSameDiscipline: false);
        }

        // Customer, 2026-11-02: players meet players within ±10,000 rating,
        // the same window as bots. Farther apart they fight bots instead.
        private const int PlayerRatingWindow = 10000;

        private static bool IsWithinPlayerRatingWindow(
            int headRating,
            NetworkCommunicator candidate)
        {
            return Math.Abs(ResolvePlayerRating(candidate) - headRating) <=
                PlayerRatingWindow;
        }

        private NetworkCommunicator TakeWaitingOpponent(
            NetworkCommunicator first,
            bool requireSameDiscipline)
        {
            DikozemieArenaServerRegistration.TryGetCharacterProfile(first, out var a);
            int firstRating = ResolvePlayerRating(first);
            int remaining = _waitingPeers.Count;
            while (remaining-- > 0)
            {
                NetworkCommunicator candidate = TakeNextWaitingPeer();
                if (candidate == null) break;
                DikozemieArenaServerRegistration.TryGetCharacterProfile(candidate, out var b);
                // Two players who met on the road fight whatever their
                // ratings: the reservation names them both.
                bool reserved = !string.IsNullOrEmpty(a?.DuelOfferId) &&
                    string.Equals(a.DuelOfferId, b?.DuelOfferId, StringComparison.Ordinal);
                if ((reserved || IsWithinPlayerRatingWindow(firstRating, candidate)) &&
                    ResolveRequestedTeamSize(candidate) == 1 &&
                    ResolveRequestedNavalBattle(candidate) == null &&
                    (!requireSameDiscipline ||
                     ResolveRequestedDiscipline(candidate) == ResolveRequestedDiscipline(first)) &&
                    DikozemieArenaDuelReservation.CanPair(
                        first.PlayerConnectionInfo.PlayerID.ToString(), a?.DuelOfferId, a?.DuelOpponentId,
                        candidate.PlayerConnectionInfo.PlayerID.ToString(), b?.DuelOfferId, b?.DuelOpponentId))
                    return candidate;
                _waitingPeers.Enqueue(candidate);
            }
            return null;
        }

        private static void AlignPairDiscipline(
            NetworkCommunicator first,
            NetworkCommunicator second)
        {
            DikozemieArenaDiscipline firstDiscipline = ResolveRequestedDiscipline(first);
            DikozemieArenaDiscipline secondDiscipline = ResolveRequestedDiscipline(second);
            if (firstDiscipline == secondDiscipline)
            {
                return;
            }

            // A player who asked for a foot duel must not meet a rider. Only
            // this pair is dismounted: the next duel request from the client
            // carries its own choice again and overwrites the server copy.
            NetworkCommunicator dismounted =
                firstDiscipline == DikozemieArenaDiscipline.Foot ? second :
                secondDiscipline == DikozemieArenaDiscipline.Foot ? first :
                null;
            bool applied = dismounted != null &&
                DikozemieArenaServerRegistration.TryGetCharacterProfile(
                    dismounted,
                    out DikozemieArenaCharacterProfile profile) &&
                DikozemieArenaServerRegistration.UpdateRequestedBattleSettings(
                    dismounted,
                    1,
                    DikozemieArenaDiscipline.Foot,
                    (DikozemieArenaComposition)profile.RequestedComposition);
            Trace(
                "mixed-stance 1v1 paired first=" + firstDiscipline +
                " second=" + secondDiscipline +
                " dismounted=" + (dismounted == null ? "none" : DescribePeer(dismounted)) +
                " applied=" + applied);
        }

        private void SendQueueStatus(NetworkCommunicator[] queue)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            bool gathering = _teamGatherDeadline != default;
            if ((!gathering && _humanOpponentWaitDeadlines.Count == 0) ||
                now < _nextQueueStatusAt)
            {
                return;
            }
            _nextQueueStatusAt = now.AddSeconds(1);
            int gatherSeconds = gathering
                ? Math.Max(0, (int)Math.Ceiling((_teamGatherDeadline - now).TotalSeconds))
                : 0;
            foreach (NetworkCommunicator waiting in queue)
            {
                if (!IsConnected(waiting) || !waiting.IsSynchronized)
                {
                    continue;
                }
                int seconds;
                int players;
                int capacity;
                if (gathering)
                {
                    seconds = gatherSeconds;
                    players = _gatherPlayers;
                    capacity = _gatherCapacity;
                }
                else if (!HasDuelPair &&
                    _humanOpponentWaitDeadlines.TryGetValue(
                        waiting,
                        out DateTimeOffset deadline))
                {
                    // A duel waits for one more live player, up to a minute
                    // (customer 2026-11-05: say so on screen, not in chat).
                    seconds = Math.Max(0, (int)Math.Ceiling((deadline - now).TotalSeconds));
                    players = 1;
                    capacity = 2;
                }
                else
                {
                    continue;
                }
                GameNetwork.BeginModuleEventAsServer(waiting);
                GameNetwork.WriteMessage(new DikozemieArenaQueueStatusMessage(
                    seconds,
                    players,
                    capacity));
                GameNetwork.EndModuleEventAsServer();
            }
        }

        private void NotifyQueuedPeers()
        {
            // The lobby can route a player onto an arena whose only fight is
            // already running. Tell the spectator why they are waiting
            // instead of leaving a silent free camera (native chat line, so
            // every installed client version can show it).
            NetworkCommunicator[] queue = _waitingPeers.ToArray();
            SendQueueStatus(queue);
            for (int index = 0; index < queue.Length; index++)
            {
                NetworkCommunicator waiting = queue[index];
                if (!IsConnected(waiting) || !waiting.IsSynchronized)
                {
                    continue;
                }

                bool teamSeeker = ResolveRequestedTeamSize(waiting) > 1 ||
                    ResolveRequestedNavalBattle(waiting) != null;
                string kind;
                string message;
                if (HasDuelPair)
                {
                    kind = "busy";
                    message = "Арена занята: идёт бой. Вы в очереди (" +
                        (index + 1) + "), следующий бой начнётся автоматически.";
                }
                else
                {
                    // Gathering and duel waits are on screen now: the header
                    // of every client and the 0.5.124 waiting panel show the
                    // live countdown (SendQueueStatus). A chat line repeating
                    // it sat on top of the panel.
                    continue;
                }
                if (_queueNoticeSent.TryGetValue(waiting, out string sentKind) &&
                    sentKind == kind)
                {
                    continue;
                }
                _queueNoticeSent[waiting] = kind;
                try
                {
                    GameNetwork.BeginModuleEventAsServer(waiting);
                    GameNetwork.WriteMessage(
                        new NetworkMessages.FromServer.ServerMessage(message));
                    GameNetwork.EndModuleEventAsServer();
                }
                catch (Exception error)
                {
                    Trace("queue notice failed peer=" + DescribePeer(waiting) +
                        " error=" + error.Message);
                }
            }
        }

        private NetworkCommunicator TakeNextWaitingPeer()
        {
            while (_waitingPeers.Count > 0)
            {
                NetworkCommunicator peer = _waitingPeers.Dequeue();
                if (IsConnected(peer) && _admittedPeers.Contains(peer) &&
                    _profileReadyPeers.Contains(peer) &&
                    peer != _activeAttacker && peer != _activeDefender)
                {
                    return peer;
                }
            }
            return null;
        }

        private void PrepareDuelist(NetworkCommunicator peer, Team team)
        {
            MissionPeer missionPeer = peer.GetComponent<MissionPeer>();
            DikozemieArenaRepresentative representative =
                peer.GetComponent<DikozemieArenaRepresentative>();
            if (missionPeer == null || representative == null)
            {
                return;
            }

            _duelSkillExperience[peer] =
                new Dictionary<string, int>(StringComparer.Ordinal);
            _duelMovementSkills.Remove(peer);
            _queueNoticeSent.Remove(peer);

            // The idle visitor may still walk the arena on the attacker side.
            // Remove that hero quietly (no corpse) before it changes sides.
            Agent current = missionPeer.ControlledAgent;
            if (current != null && current.State == AgentState.Active &&
                current.Team != team)
            {
                current.FadeOut(hideInstantly: true, hideMount: true);
            }
            MultiplayerTeamSelectComponent.ChangeTeamServer(peer, team);
            // ChangeTeamServer assigns the side, but it does not initialize the
            // peer culture. MultiplayerClassDivisions needs that culture before
            // it can resolve troop 0 and build the player's agent visuals.
            // Without this call the client remained on the team-selection screen
            // forever and the rating-matched bot could never be spawned.
            missionPeer.OverrideCultureWithTeamCulture();
            if (missionPeer.ControlledAgent == null)
            {
                missionPeer.HasSpawnedAgentVisuals = false;
                missionPeer.SpawnTimer.Reset(Mission.CurrentTime, 0.1f);
            }
            ChangeCurrentGoldForPeer(missionPeer, InitialGold);
            DikozemieArenaRatingEntry rating = DikozemieArenaRatingStore.Get(peer);
            ChangeProgress(representative, 0, 0, rating.Rating, rating.Wins);
        }

        private void RequeueSurvivingDuelist(
            NetworkCommunicator excludedPeer = null)
        {
            NetworkCommunicator survivor = _activeAttacker != excludedPeer &&
                IsConnected(_activeAttacker)
                ? _activeAttacker
                : _activeDefender != excludedPeer && IsConnected(_activeDefender)
                    ? _activeDefender
                    : null;
            if (survivor == null)
            {
                return;
            }

            MissionPeer missionPeer = survivor.GetComponent<MissionPeer>();
            if (missionPeer?.ControlledAgent != null)
            {
                missionPeer.ControlledAgent.FadeOut(hideInstantly: true, hideMount: true);
            }
            if (missionPeer != null)
            {
                missionPeer.Team = Mission.SpectatorTeam;
            }
            _waitingPeers.Enqueue(survivor);
        }

        private void ProcessPendingDisconnects()
        {
            foreach (NetworkCommunicator disconnectedPeer in
                _disconnectingPeers.ToArray())
            {
                _disconnectingPeers.Remove(disconnectedPeer);
                _pendingAdmissions.Remove(disconnectedPeer);
                _admittedPeers.Remove(disconnectedPeer);
                _profileReadyPeers.Remove(disconnectedPeer);
                _seriesStates.Remove(disconnectedPeer);
                _duelSkillExperience.Remove(disconnectedPeer);
                _duelMovementSkills.Remove(disconnectedPeer);
                _humanOpponentWaitDeadlines.Remove(disconnectedPeer);
                _startNowRequests.Remove(disconnectedPeer);
                _queueNoticeSent.Remove(disconnectedPeer);

                _pendingNextDuelFollowers.Remove(disconnectedPeer);
                if (_pendingNextDuelPeer == disconnectedPeer)
                {
                    // A team mate who also pressed "next battle" takes over.
                    _pendingNextDuelPeer = _pendingNextDuelFollowers
                        .FirstOrDefault(IsConnected);
                    _pendingNextDuelFollowers.Remove(_pendingNextDuelPeer);
                    if (_pendingNextDuelPeer == null)
                    {
                        _pendingNextDuelAt = default;
                    }
                }
                if (_arenaVisitor == disconnectedPeer)
                {
                    _arenaVisitor = null;
                    _visitorRequestedDuel = false;
                }
                if (_teamBattle &&
                    _teamParticipantSides.ContainsKey(disconnectedPeer))
                {
                    HandleTeamParticipantDisconnect(disconnectedPeer);
                    continue;
                }
                if (_activeAttacker != disconnectedPeer &&
                    _activeDefender != disconnectedPeer)
                {
                    continue;
                }

                Trace(
                    "disconnect cleanup active peer=" +
                    DescribePeer(disconnectedPeer));
                RequeueSurvivingDuelist(disconnectedPeer);
                ClearActivePair(removeFromAdmission: false);
                _duelCompleted = false;
            }
        }

        private void ReleaseCompletedDuel()
        {
            if (_teamBattle)
            {
                Trace("release completed team battle players=" +
                    _teamParticipants.Count);
                foreach (NetworkCommunicator participant in _teamParticipants.ToArray())
                {
                    QueueCompletedDuelist(participant, requeue: false);
                }
            }
            else
            {
                Trace("release completed duel: attacker begin");
                QueueCompletedDuelist(_activeAttacker, requeue: false);
                Trace("release completed duel: attacker complete; defender begin");
                QueueCompletedDuelist(_activeDefender, requeue: false);
            }
            Trace("release completed duel: pair begin");
            ClearActivePair(removeFromAdmission: false);
            Trace("release completed duel: pair complete");
            _duelCompleted = false;
        }

        private void QueueCompletedDuelist(
            NetworkCommunicator peer,
            bool requeue)
        {
            if (!IsConnected(peer))
            {
                return;
            }

            MissionPeer missionPeer = peer.GetComponent<MissionPeer>();
            if (missionPeer?.ControlledAgent != null &&
                missionPeer.ControlledAgent.State == AgentState.Active)
            {
                missionPeer.ControlledAgent.FadeOut(
                    hideInstantly: true,
                    hideMount: true);
            }
            MultiplayerTeamSelectComponent.ChangeTeamServer(
                peer,
                Mission.SpectatorTeam);
            if (requeue)
            {
                _waitingPeers.Enqueue(peer);
                Trace("series continues peer=" + DescribePeer(peer));
            }
        }

        private void ClearActivePair(bool removeFromAdmission)
        {
            if (removeFromAdmission)
            {
                _admittedPeers.Remove(_activeAttacker);
                _admittedPeers.Remove(_activeDefender);
            }
            Agent bot = _activeBot;
            _activeBot = null;
            _activeBotCharacterId = string.Empty;
            _activeBotRating = 0;
            if (bot != null && bot.State == AgentState.Active)
            {
                Trace("clear active pair: bot fade begin");
                bot.FadeOut(hideInstantly: true, hideMount: true);
                Trace("clear active pair: bot fade complete");
            }
            foreach (Agent teamBot in _activeTeamBots.ToArray())
            {
                if (teamBot != null && teamBot != bot &&
                    teamBot.State == AgentState.Active)
                {
                    teamBot.FadeOut(hideInstantly: true, hideMount: true);
                }
            }
            _activeTeamBots.Clear();
            _rewardedTeamVictims.Clear();
            _deathSummarySentPeers.Clear();
            ResetTeamParticipants();
            _teamField = new DikozemieArenaTeamField();
            _activeAttacker = null;
            _activeDefender = null;
            _trainingDuel = false;
            _teamBattle = false;
            _teamSize = 1;
            ResetTeamSides();
            _teamDiscipline = DikozemieArenaDiscipline.Open;
            _teamComposition = DikozemieArenaComposition.Mixed;
            _teamBotsSpawned = false;
            _pendingTeamResult = false;
            _pendingTeamOutcome = DikozemieArenaTeamOutcome.InProgress;
            _pendingTeamResultAt = default;
            _navalBattle = null;
            _navalFriendlyCaptain = null;
            _navalEnemyCaptain = null;
            _reportedTrainingPlayerNotSpawned = false;
            _botRoundWins = 0;
            _pendingTrainingResult = false;
            _pendingTrainingPlayerScore = 0;
            _pendingTrainingResultAt = default;
            _trainingKnockout.Reset();
            _matchKnockout.Reset();
            _pendingTrainingPlayerAgent = null;
            _pendingTrainingBotAgent = null;
            _pendingMatchResult = false;
            _pendingMatchWinner = null;
            _pendingMatchLoser = null;
            _pendingMatchWinnerAgent = null;
            _pendingMatchLoserAgent = null;
            _activeAttackerAgent = null;
            _activeDefenderAgent = null;
            _nextTrainingBotSpawnAt = default;
        }

        private void PruneDisconnectedPeers()
        {
            _admittedPeers.RemoveWhere(peer => !IsConnected(peer));
            _profileReadyPeers.RemoveWhere(peer => !IsConnected(peer));
            foreach (NetworkCommunicator peer in _queueNoticeSent.Keys
                         .Where(peer => !IsConnected(peer)).ToArray())
            {
                _queueNoticeSent.Remove(peer);
            }

            // The arena visitor is a connection object, not an account id. After
            // reconnecting Bannerlord creates a new communicator, so retaining the
            // disconnected visitor leaves the replacement peer queued forever with
            // _visitorRequestedDuel=false. Promote a live admitted peer immediately;
            // on a dedicated arena entering the scene is already consent to fight.
            _seatedFighters.RemoveWhere(peer => !IsConnected(peer));
            if (!IsConnected(_arenaVisitor))
            {
                // An observer never takes the visitor's body: a fighter who
                // came after him would hang as a spectator until his battle.
                _arenaVisitor = _admittedPeers.FirstOrDefault(
                    peer => IsConnected(peer) && !IsObserverPeer(peer) &&
                        !DikozemieBattleSanctions.IsLeaving(peer));
                _visitorRequestedDuel = _arenaVisitor != null &&
                    GameNetwork.IsDedicatedServer;
                if (_arenaVisitor != null)
                {
                    TryQueueProfileReadyPeer(_arenaVisitor);
                }
            }

            foreach (NetworkCommunicator peer in _seriesStates.Keys
                         .Where(peer => !IsConnected(peer)).ToArray())
            {
                _seriesStates.Remove(peer);
            }
            foreach (NetworkCommunicator peer in _humanOpponentWaitDeadlines.Keys
                         .Where(peer => !IsConnected(peer)).ToArray())
            {
                _humanOpponentWaitDeadlines.Remove(peer);
            }
            _startNowRequests.RemoveWhere(peer => !IsConnected(peer));
            foreach (NetworkCommunicator peer in _duelSkillExperience.Keys
                         .Where(peer => !IsConnected(peer)).ToArray())
            {
                _duelSkillExperience.Remove(peer);
                _duelMovementSkills.Remove(peer);
            }
        }

        public override void OnScoreHit(
            Agent affectedAgent,
            Agent affectorAgent,
            WeaponComponentData attackerWeapon,
            bool isBlocked,
            bool isSiegeEngineHit,
            in Blow blow,
            in AttackCollisionData collisionData,
            float damagedHp,
            float hitDistance,
            float shotDifficulty)
        {
            base.OnScoreHit(
                affectedAgent,
                affectorAgent,
                attackerWeapon,
                isBlocked,
                isSiegeEngineHit,
                blow,
                collisionData,
                damagedHp,
                hitDistance,
                shotDifficulty);
            if ((!GameNetwork.IsServerOrRecorder && !GameNetwork.IsDedicatedServer) ||
                damagedHp <= 0f || affectorAgent == null || affectedAgent == null ||
                affectorAgent == affectedAgent || !affectorAgent.IsEnemyOf(affectedAgent))
            {
                return;
            }

            NetworkCommunicator attackerPeer =
                ResolveDuelAgentPeer(affectorAgent);
            if (!IsActiveDuelist(attackerPeer))
            {
                return;
            }
            bool validDuelHit = _teamBattle
                ? affectedAgent.Team != null &&
                  affectedAgent.Team != GetParticipantTeam(attackerPeer) &&
                  (affectedAgent.Team == Mission.AttackerTeam ||
                   affectedAgent.Team == Mission.DefenderTeam)
                : _trainingDuel
                ? attackerPeer == _activeAttacker &&
                  (affectedAgent == _activeBot ||
                   (_pendingTrainingResult &&
                    affectedAgent == _pendingTrainingBotAgent))
                : (attackerPeer == _activeAttacker &&
                   ResolveDuelAgentPeer(affectedAgent) == _activeDefender) ||
                  (attackerPeer == _activeDefender &&
                   ResolveDuelAgentPeer(affectedAgent) == _activeAttacker);
            if (!validDuelHit)
            {
                return;
            }

            RememberMovementSkill(attackerPeer, affectorAgent);
            RememberMovementSkill(
                affectedAgent.MissionPeer?.GetNetworkPeer(),
                affectedAgent);

            string weaponSkillId = ResolveCombatSkillId(
                affectorAgent,
                attackerWeapon);
            if (IsTrainableCombatSkill(weaponSkillId))
            {
                int weaponExperience = Math.Max(1, Math.Min(250,
                    (int)Math.Round(damagedHp * (isBlocked ? 0.2f : 0.8f) +
                        Math.Max(0f, Math.Min(5f, shotDifficulty)))));
                AddDuelSkillExperience(attackerPeer, weaponSkillId, weaponExperience);
            }
            AddDuelSkillExperience(
                attackerPeer,
                affectorAgent.MountAgent != null ? "Riding" : "Athletics",
                Math.Max(1, (int)Math.Round(damagedHp * 0.08f)));
        }

        private void RejectPeer(NetworkCommunicator peer, string reason)
        {
            if (peer == null)
            {
                return;
            }
            Trace("RejectPeer peer=" + DescribePeer(peer) + " reason=" + reason);

            if (!DikozemieArenaServerRegistration.UsesExternalMatchmaker)
            {
                _pendingAdmissions[peer] = DateTimeOffset.MaxValue;
                Console.WriteLine(
                    "DikozemieOnline: standalone peer kept connected id=" +
                    peer.PlayerConnectionInfo?.PlayerID.ToString() + " reason=" + reason);
                return;
            }
            _pendingAdmissions.Remove(peer);
            _admittedPeers.Remove(peer);
            _profileReadyPeers.Remove(peer);
            Console.WriteLine(
                "DikozemieOnline: rejecting arena peer id=" +
                peer.PlayerConnectionInfo?.PlayerID.ToString() + " reason=" + reason);
            if (GameNetwork.IsServer && peer.IsConnectionActive)
            {
                GameNetwork.AddNetworkPeerToDisconnectAsServer(peer);
            }
        }

        public override void OnPeerChangedTeam(
            NetworkCommunicator peer,
            Team oldTeam,
            Team newTeam)
        {
            if (!IsActiveDuelist(peer) || oldTeam == null || oldTeam == newTeam ||
                oldTeam.Side == BattleSideEnum.None)
            {
                return;
            }

            MissionPeer missionPeer = peer.GetComponent<MissionPeer>();
            ChangeCurrentGoldForPeer(missionPeer, InitialGold);
            DikozemieArenaRepresentative representative =
                peer.GetComponent<DikozemieArenaRepresentative>();
            ChangeProgress(
                representative,
                representative?.RoundWins ?? 0,
                representative?.Deaths ?? 0,
                representative?.Rating ?? 1000,
                representative?.RatingWins ?? 0);
        }

        protected override void HandlePlayerDisconnect(NetworkCommunicator peer)
        {
            DisconnectInfo info = peer?.PlayerConnectionInfo?
                .GetParameter<DisconnectInfo>("DisconnectInfo");
            Trace(
                "HandlePlayerDisconnect peer=" + DescribePeer(peer) +
                " type=" + (info == null ? "none" : info.Type.ToString()) +
                " quit=" + (peer?.QuitFromMission ?? false));
            if (peer != null)
            {
                // HandlePlayerDisconnect can run while Bannerlord still reports
                // IsConnectionActive=true. Record the exact communicator now and
                // perform engine-owned agent/team cleanup safely on the next tick.
                _disconnectingPeers.Add(peer);
            }
            base.HandlePlayerDisconnect(peer);
        }

        public override void OnAgentRemoved(
            Agent victim,
            Agent killer,
            AgentState state,
            KillingBlow blow)
        {
            base.OnAgentRemoved(victim, killer, state, blow);
            if (_duelCompleted ||
                blow.DamageType == DamageTypes.Invalid ||
                (state != AgentState.Unconscious && state != AgentState.Killed) ||
                victim?.IsHuman != true)
            {
                return;
            }

            if (_teamBattle)
            {
                RegisterTeamPersonalKill(victim, killer, blow);
            }

            NetworkCommunicator removedPeer = ResolveDuelAgentPeer(victim);
            RememberMovementSkill(removedPeer, victim);
            if (IsActiveDuelist(removedPeer) &&
                _deathSummarySentPeers.Add(removedPeer))
            {
                SendDeathSummary(removedPeer);
            }

            // The first death is provisional while launched projectiles land.
            if (_pendingTeamResult)
            {
                HandleTeamAgentRemoved(victim);
                return;
            }

            if (_pendingTrainingResult || _pendingMatchResult)
            {
                // The killer may already be removed (a posthumous projectile).
                // Only the current victim still has a valid removal callback.
                RememberMovementSkill(ResolveDuelAgentPeer(victim), victim);
                RegisterPendingKnockout(victim);
                return;
            }

            RememberMovementSkill(
                victim.MissionPeer?.GetNetworkPeer(),
                victim);
            RememberMovementSkill(
                killer?.MissionPeer?.GetNetworkPeer(),
                killer);

            if (_teamBattle && HandleTeamAgentRemoved(victim))
            {
                return;
            }

            if (_trainingDuel && HandleTrainingAgentRemoved(
                    victim,
                    killer,
                    state))
            {
                return;
            }

            NetworkCommunicator victimNetworkPeer = ResolveDuelAgentPeer(victim);
            if (!IsActiveDuelist(victimNetworkPeer))
            {
                return;
            }

            NetworkCommunicator killerNetworkPeer = ResolveDuelAgentPeer(killer);
            if (!IsActiveDuelist(killerNetworkPeer) || killer == victim ||
                !killer.IsEnemyOf(victim))
            {
                return;
            }

            bool isPairedKill =
                (killerNetworkPeer == _activeAttacker &&
                 victimNetworkPeer == _activeDefender) ||
                (killerNetworkPeer == _activeDefender &&
                 victimNetworkPeer == _activeAttacker);
            if (!isPairedKill)
            {
                return;
            }

            QueueMatchResult(killerNetworkPeer, victimNetworkPeer, killer, victim);
        }

        private bool HandleTeamAgentRemoved(Agent victim)
        {
            if (!_teamBattle || victim == null)
            {
                return false;
            }
            NetworkCommunicator victimPeer = ResolveDuelAgentPeer(victim);
            bool isPlayer = victimPeer != null &&
                _teamParticipantSides.ContainsKey(victimPeer);
            bool isTeamBot = _activeTeamBots.Contains(victim);
            if (!isPlayer && !isTeamBot)
            {
                return false;
            }

            if (isPlayer && _teamEliminatedPeers.Add(victimPeer))
            {
                // A team battle grants each hero exactly one life. Keep the
                // peer on its team so Bannerlord's spectator camera can
                // follow surviving allies, but never admit it to the spawn
                // loop again before the result screen starts the next battle.
                Trace("team player eliminated; spectator only peer=" +
                    DescribePeer(victimPeer));
            }

            EvaluateTeamOutcome(victim);
            return true;
        }

        private void EvaluateTeamOutcome(Agent removedVictim)
        {
            if (!_teamBattle || !_teamBotsSpawned || _duelCompleted)
            {
                return;
            }
            DikozemieArenaTeamOutcome outcome = DikozemieArenaTeamRules.Resolve(
                CountTeamAlive(BattleSideEnum.Attacker, removedVictim),
                CountTeamAlive(BattleSideEnum.Defender, removedVictim));
            if (outcome == DikozemieArenaTeamOutcome.InProgress)
            {
                return;
            }

            // The first knockout is provisional while projectiles land; a
            // later death inside the window can still turn it into a draw.
            _pendingTeamOutcome = outcome;
            _botRoundWins = outcome == DikozemieArenaTeamOutcome.DefenderVictory ? 1 : 0;
            if (!_pendingTeamResult)
            {
                _pendingTeamResult = true;
                _pendingTeamResultAt =
                    DateTimeOffset.UtcNow + PostKnockoutPresentation;
                Trace("team result queued outcome=" + outcome);
            }
        }

        // A team battle used to end only by elimination. 2026-11-04 a naval
        // battle sat for minutes at "attackers=1 defenders=15": the last
        // fighter could not be reached, and every player waited on it. When
        // nobody falls for 30 s the smaller side's bots are made mortal and
        // put next to their nearest enemy; when nobody falls for 60 s the
        // battle is decided by who still stands (equal numbers: a draw).
        private static readonly TimeSpan StalemateNudgeAfter = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan StalemateResolveAfter = TimeSpan.FromSeconds(60);
        private DateTimeOffset _teamProgressAt;
        private int _teamAliveKey = -1;
        private bool _teamStalemateNudged;

        private void CheckTeamStalemate(DateTimeOffset now)
        {
            if (!_teamBattle || !_teamBotsSpawned || _duelCompleted || _pendingTeamResult)
            {
                return;
            }
            // Crews hold on their decks until the hooks land: no fight yet.
            if (_navalBattle != null &&
                DikozemieNavalBoardingBehavior.Active?.CrewsMayFight != true)
            {
                _teamProgressAt = now;
                return;
            }

            int attackers = CountTeamAlive(BattleSideEnum.Attacker, null);
            int defenders = CountTeamAlive(BattleSideEnum.Defender, null);
            int key = attackers * 100000 + defenders;
            if (key != _teamAliveKey || _teamProgressAt == default)
            {
                _teamAliveKey = key;
                _teamProgressAt = now;
                _teamStalemateNudged = false;
                return;
            }

            TimeSpan quiet = now - _teamProgressAt;
            if (!_teamStalemateNudged && quiet >= StalemateNudgeAfter)
            {
                _teamStalemateNudged = true;
                NudgeTeamStalemate(attackers, defenders);
                return;
            }
            if (quiet < StalemateResolveAfter)
            {
                return;
            }

            DikozemieArenaTeamOutcome outcome = attackers > defenders
                ? DikozemieArenaTeamOutcome.AttackerVictory
                : defenders > attackers
                    ? DikozemieArenaTeamOutcome.DefenderVictory
                    : DikozemieArenaTeamOutcome.Draw;
            _pendingTeamOutcome = outcome;
            _botRoundWins = outcome == DikozemieArenaTeamOutcome.DefenderVictory ? 1 : 0;
            _pendingTeamResult = true;
            _pendingTeamResultAt = now;
            Trace("team stalemate resolved after " + quiet.TotalSeconds.ToString("0") +
                " s attackers=" + attackers + " defenders=" + defenders +
                " outcome=" + outcome);
        }

        private void NudgeTeamStalemate(int attackers, int defenders)
        {
            List<Agent> attackerAgents = new List<Agent>();
            List<Agent> defenderAgents = new List<Agent>();
            foreach (Agent agent in Mission.Agents.ToArray())
            {
                if (agent == null || !agent.IsHuman || agent.State != AgentState.Active)
                {
                    continue;
                }
                if (agent.Team == Mission.AttackerTeam)
                {
                    attackerAgents.Add(agent);
                }
                else if (agent.Team == Mission.DefenderTeam)
                {
                    defenderAgents.Add(agent);
                }
            }

            var report = new System.Text.StringBuilder();
            foreach (Agent agent in attackerAgents.Concat(defenderAgents).Take(12))
            {
                report.Append(" [")
                    .Append(agent.Team == Mission.AttackerTeam ? "A " : "D ")
                    .Append(agent.MissionPeer != null ? "player " : "bot ")
                    .Append(agent.Position.x.ToString("0.0")).Append(',')
                    .Append(agent.Position.y.ToString("0.0")).Append(',')
                    .Append(agent.Position.z.ToString("0.0"))
                    .Append(" hp=").Append(agent.Health.ToString("0"))
                    .Append(']');
            }
            Trace("team stalemate: nobody fell for 30 s attackers=" + attackers +
                " defenders=" + defenders + report);

            // Both sides' bots walk at their nearest enemy with side arms out
            // (DikozemieArenaSpawningBehavior.CloseIn); players keep control of
            // their own heroes. Until 0.5.133 the smaller side was teleported in
            // front of its enemy, which the customer saw as a jump (2026-11-19).
            foreach (Agent agent in attackerAgents.Concat(defenderAgents))
            {
                try
                {
                    agent.SetMortalityState(Agent.MortalityState.Mortal);
                }
                catch (Exception error)
                {
                    Trace("team stalemate nudge failed: " + error.Message);
                }
            }
            DikozemieArenaSpawningBehavior.Active?.BeginCloseIn();
            Trace("team stalemate: bots close in on foot");
        }

        private int CountTeamAlive(BattleSideEnum side, Agent excluded)
        {
            int alive = 0;
            foreach (KeyValuePair<NetworkCommunicator, BattleSideEnum> participant in
                _teamParticipantSides)
            {
                if (participant.Value == side &&
                    !_teamEliminatedPeers.Contains(participant.Key) &&
                    _teamHumanAgents.TryGetValue(participant.Key, out Agent agent) &&
                    agent != null && agent != excluded &&
                    agent.State == AgentState.Active)
                {
                    alive++;
                }
            }
            Team team = side == BattleSideEnum.Attacker
                ? Mission.AttackerTeam
                : Mission.DefenderTeam;
            alive += _activeTeamBots.Count(bot =>
                bot != null && bot != excluded && bot.Team == team &&
                bot.State == AgentState.Active);
            return alive;
        }

        private void RegisterTeamPersonalKill(
            Agent victim,
            Agent killer,
            KillingBlow blow)
        {
            NetworkCommunicator killerPeer = ResolveDuelAgentPeer(killer);
            Team killerTeam = killerPeer == null
                ? null
                : GetParticipantTeam(killerPeer);
            if (victim == null || killer == null || killer == victim ||
                killerTeam == null || victim.Team == null ||
                victim.Team == killerTeam ||
                (victim.Team != Mission.AttackerTeam &&
                 victim.Team != Mission.DefenderTeam) ||
                !_rewardedTeamVictims.Add(victim))
            {
                return;
            }

            // Bots carry their ranked identity; a live enemy is worth the
            // same as a bot of its current rating.
            NetworkCommunicator victimPeer = ResolveDuelAgentPeer(victim);
            bool victimIsPlayer = victimPeer != null &&
                _teamParticipantSides.ContainsKey(victimPeer);
            DikozemieArenaOpponent opponent = null;
            bool victimMounted;
            if (victimIsPlayer)
            {
                victimMounted =
                    victim.SpawnEquipment[EquipmentIndex.Horse].Item != null;
            }
            else
            {
                DikozemieArenaSpawningBehavior spawning =
                    DikozemieArenaSpawningBehavior.Active;
                if (spawning == null ||
                    !spawning.TryGetTeamBotOpponent(
                        victim,
                        out opponent,
                        out victimMounted) ||
                    opponent == null)
                {
                    return;
                }
            }

            bool killerMounted = killer.MountAgent != null;
            bool victimHadBowOrCrossbow =
                DikozemieArenaOpponentCatalog.EquipmentHasBowOrCrossbow(
                    victim.SpawnEquipment);
            int rewardMultiplier = _navalBattle != null
                ? DikozemieNavalRewardRules.PersonalKillReward
                : DikozemieArenaTeamRules.GetPersonalKillRewardMultiplier(
                    _teamDiscipline,
                    _teamComposition,
                    killerMounted,
                    victimMounted,
                    killWasMelee: !blow.IsMissile,
                    victimHadBowOrCrossbow: victimHadBowOrCrossbow);
            int victimRating;
            string victimName;
            int reward;
            DikozemieArenaRatingEntry playerRating;
            if (victimIsPlayer)
            {
                reward = DikozemieArenaRatingStore.RecordTeamPlayerKill(
                    killerPeer,
                    victimPeer,
                    rewardMultiplier,
                    out playerRating,
                    out DikozemieArenaRatingEntry victimEntry);
                victimRating = victimEntry.Rating;
                victimName = victimEntry.DisplayName;
            }
            else
            {
                victimRating =
                    DikozemieArenaOpponentCatalog.GetCurrentRating(opponent);
                victimName = opponent.DisplayName;
                reward = DikozemieArenaRatingStore.RecordTeamKill(
                    killerPeer,
                    opponent.CharacterId,
                    opponent.DisplayName,
                    victimRating,
                    rewardMultiplier,
                    out playerRating,
                    out _);
            }
            ArenaSeriesState series = GetSeriesState(killerPeer);
            series.CoinsEarned += reward;
            series.RatingEarned += reward;
            _teamPersonalKills.TryGetValue(killerPeer, out int kills);
            _teamKillRatingEarned.TryGetValue(killerPeer, out int killRating);
            _teamPersonalKills[killerPeer] = ++kills;
            _teamKillRatingEarned[killerPeer] = killRating += reward;

            GameNetwork.BeginModuleEventAsServer(killerPeer);
            GameNetwork.WriteMessage(new DikozemieArenaKillProgressMessage(
                kills,
                killRating,
                victimName,
                victimRating,
                reward,
                rewardMultiplier));
            GameNetwork.EndModuleEventAsServer();

            DikozemieArenaRepresentative representative =
                killerPeer.GetComponent<DikozemieArenaRepresentative>();
            ChangeProgress(
                representative,
                series.Wins,
                series.Losses,
                playerRating.Rating,
                playerRating.Wins);
            Trace(
                "team personal kill killer=" + DescribePeer(killerPeer) +
                " victim=" + (victimIsPlayer
                    ? "player:" + DescribePeer(victimPeer)
                    : opponent.CharacterId) +
                " victimRating=" + victimRating +
                " killerMounted=" + killerMounted +
                " victimMounted=" + victimMounted +
                " killWasMelee=" + !blow.IsMissile +
                " victimHadBowOrCrossbow=" + victimHadBowOrCrossbow +
                " multiplier=" + rewardMultiplier +
                " reward=" + reward +
                " kills=" + kills);
        }

        private void ProcessPendingTeamResult(DateTimeOffset now)
        {
            if (!_pendingTeamResult || now < _pendingTeamResultAt)
            {
                return;
            }
            DikozemieArenaTeamOutcome outcome = _pendingTeamOutcome;
            _pendingTeamResult = false;
            _pendingTeamResultAt = default;
            CompleteTeamBattle(outcome);
        }

        private void CompleteTeamBattle(DikozemieArenaTeamOutcome outcome)
        {
            if (_duelCompleted || !_teamParticipants.Any(IsConnected))
            {
                return;
            }
            _duelCompleted = true;

            bool draw = outcome == DikozemieArenaTeamOutcome.Draw;
            bool leaderboardChanged = draw;
            foreach (NetworkCommunicator peer in _teamParticipants.ToArray())
            {
                if (!IsConnected(peer))
                {
                    continue;
                }
                bool attacker =
                    _teamParticipantSides[peer] == BattleSideEnum.Attacker;
                bool victory = attacker
                    ? outcome == DikozemieArenaTeamOutcome.AttackerVictory
                    : outcome == DikozemieArenaTeamOutcome.DefenderVictory;
                leaderboardChanged |= CompleteTeamBattleFor(
                    peer,
                    attacker,
                    victory,
                    draw);
            }
            if (leaderboardChanged)
            {
                DikozemieArenaLeaderboardTransport.Broadcast(_admittedPeers);
            }
            Trace("team battle complete outcome=" + outcome +
                " players=" + _teamParticipants.Count(IsConnected));
        }

        // Records one live player's team result; returns true when that
        // player's series ended and the result screen was sent.
        private bool CompleteTeamBattleFor(
            NetworkCommunicator peer,
            bool attacker,
            bool victory,
            bool draw)
        {
            // A won sea battle is +1 rating on top of the kills, as in War Sails mode.
            int navalVictoryRating = _navalBattle != null && victory
                ? DikozemieArenaRatingStore.VictoryRatingReward
                : 0;
            DikozemieArenaRatingStore.RecordTeamBattleOutcome(
                peer,
                victory,
                draw,
                out DikozemieArenaRatingEntry playerRating,
                navalVictoryRating);
            ArenaSeriesState series = GetSeriesState(peer);
            series.RatingEarned += navalVictoryRating;
            series.CompletedBattles++;
            int victoryCoins = 0;
            int lossCoinPenalty = 0;
            if (victory)
            {
                series.Wins++;
                victoryCoins = _navalBattle != null
                    ? DikozemieNavalRewardRules.GetVictoryCoinReward(
                        _navalBattle.ShipClass)
                    : DikozemieArenaTeamRules.CalculateVictoryCoinReward(
                        attacker
                            ? _teamField.AttackerAverageRating
                            : _teamField.DefenderAverageRating,
                        attacker
                            ? _teamField.DefenderAverageRating
                            : _teamField.AttackerAverageRating);
                series.CoinsEarned += victoryCoins;
            }
            else if (!draw)
            {
                series.Losses++;
                lossCoinPenalty = _navalBattle != null
                    ? DikozemieNavalRewardRules.GetLossCoinPenalty(
                        _navalBattle.ShipClass)
                    : DikozemieArenaTeamRules.GetLossCoinPenalty(_teamSize);
            }

            // The result screen names one bot of the other side (none when
            // that side was all live players).
            string opponentId = attacker
                ? _teamField.DefenderRepresentativeId
                : _teamField.AttackerRepresentativeId;
            int opponentStartRating = attacker
                ? _teamField.DefenderRepresentativeRating
                : _teamField.AttackerRepresentativeRating;
            DikozemieArenaRatingEntry opponentRating = null;
            if (!string.IsNullOrEmpty(opponentId))
            {
                BasicCharacterObject opponentCharacter = MBObjectManager.Instance
                    .GetObject<BasicCharacterObject>(opponentId);
                opponentRating = DikozemieArenaRatingStore.GetBot(
                    opponentId,
                    opponentCharacter?.Name?.ToString() ?? opponentId,
                    opponentStartRating);
            }
            ChangeProgress(
                peer.GetComponent<DikozemieArenaRepresentative>(),
                series.Wins,
                series.Losses,
                playerRating.Rating,
                playerRating.Wins);

            bool seriesComplete =
                series.CompletedBattles >= series.TargetBattles;
            if (seriesComplete)
            {
                int seriesPenalty = (_navalBattle != null
                    ? DikozemieNavalRewardRules.GetLossCoinPenalty(_navalBattle.ShipClass)
                    : DikozemieArenaTeamRules.GetLossCoinPenalty(_teamSize)) * series.Losses;
                DikozemieEconomyInbox.Result(
                    peer,
                    _navalBattle != null ? "naval" : "arena",
                    series.CoinsEarned,
                    seriesPenalty,
                    (_navalBattle != null ? "морской бой" : "бой " + _teamSize + "×" + _teamSize) +
                    ": побед " + series.Wins + ", поражений " + series.Losses);
            }
            if (seriesComplete || draw)
            {
                GameNetwork.BeginModuleEventAsServer(peer);
                GameNetwork.WriteMessage(new DikozemieArenaTrainingResultMessage(
                    victory,
                    draw ? 0 : victory ? 1 : 0,
                    draw ? 0 : victory ? 0 : 1,
                    opponentId,
                    playerRating,
                    opponentRating,
                    series.CompletedBattles,
                    series.Wins,
                    series.Losses,
                    series.CoinsEarned,
                    series.RatingEarned));
                GameNetwork.EndModuleEventAsServer();
            }
            SendSkillProgress(peer, victory);
            _teamPersonalKills.TryGetValue(peer, out int kills);
            _teamKillRatingEarned.TryGetValue(peer, out int killRating);
            Trace(
                "team battle result peer=" + DescribePeer(peer) +
                " side=" + (attacker ? "attacker" : "defender") +
                " victory=" + victory +
                " draw=" + draw +
                " personalKills=" + kills +
                " killRating=" + killRating +
                " victoryCoins=" + victoryCoins +
                " lossCoinPenalty=" + lossCoinPenalty +
                " totalCoins=" + series.CoinsEarned);
            return seriesComplete || draw;
        }

        private void QueueMatchResult(
            NetworkCommunicator winner, NetworkCommunicator loser,
            Agent winnerAgent, Agent loserAgent)
        {
            _pendingMatchWinner = winner;
            _pendingMatchLoser = loser;
            _pendingMatchWinnerAgent = winnerAgent;
            _pendingMatchLoserAgent = loserAgent;
            _matchKnockout.Reset();
            // Window roles: provisional winner / first victim, not teams.
            _matchKnockout.RegisterDeath(false, DateTimeOffset.UtcNow,
                PostKnockoutPresentation);
            _pendingMatchResult = true;
            Trace("1v1 result queued; observing both knockouts");
        }

        private void RegisterPendingKnockout(Agent victim)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (_pendingTrainingResult)
            {
                if (victim == _pendingTrainingPlayerAgent)
                    _trainingKnockout.RegisterDeath(true, now, PostKnockoutPresentation);
                else if (victim == _pendingTrainingBotAgent)
                {
                    _trainingKnockout.RegisterDeath(false, now, PostKnockoutPresentation);
                    // Do not later dereference native State on a removed bot.
                    _activeBot = null;
                }
            }
            if (_pendingMatchResult)
            {
                if (victim == _pendingMatchWinnerAgent)
                    _matchKnockout.RegisterDeath(true, now, PostKnockoutPresentation);
                else if (victim == _pendingMatchLoserAgent)
                    _matchKnockout.RegisterDeath(false, now, PostKnockoutPresentation);
            }
        }

        private NetworkCommunicator ResolveDuelAgentPeer(Agent agent)
        {
            NetworkCommunicator peer = agent?.MissionPeer?.GetNetworkPeer();
            if (peer != null || agent == null) return peer;
            foreach (KeyValuePair<NetworkCommunicator, Agent> teamHuman in _teamHumanAgents)
            {
                if (teamHuman.Value == agent) return teamHuman.Key;
            }
            if (agent == _activeAttackerAgent) return _activeAttacker;
            if (agent == _activeDefenderAgent) return _activeDefender;
            // Preserve ownership when a dead shooter's MissionPeer is detached.
            if (agent == _pendingTrainingPlayerAgent) return _activeAttacker;
            if (agent == _pendingMatchWinnerAgent) return _pendingMatchWinner;
            if (agent == _pendingMatchLoserAgent) return _pendingMatchLoser;
            return null;
        }

        private static string ResolvePartyKey(NetworkCommunicator peer) =>
            DikozemieArenaServerRegistration.TryGetCharacterProfile(
                peer,
                out DikozemieArenaCharacterProfile profile)
                    ? profile.PartyLeaderId ?? string.Empty
                    : string.Empty;

        private static bool IsSameParty(
            NetworkCommunicator first,
            NetworkCommunicator second)
        {
            string key = ResolvePartyKey(first);
            return !string.IsNullOrEmpty(key) &&
                string.Equals(
                    key,
                    ResolvePartyKey(second),
                    StringComparison.Ordinal);
        }

        // A party fights the battle its leader chose, whatever the members
        // had left selected in their own menus.
        private static NetworkCommunicator ResolveBattleChooser(
            NetworkCommunicator peer)
        {
            string leaderId = ResolvePartyKey(peer);
            if (string.IsNullOrEmpty(leaderId))
            {
                return peer;
            }
            foreach (NetworkCommunicator candidate in GameNetwork.NetworkPeers)
            {
                if (string.Equals(
                        candidate.PlayerConnectionInfo?.PlayerID.ToString(),
                        leaderId,
                        StringComparison.Ordinal))
                {
                    return candidate;
                }
            }
            return peer;
        }

        private static int ResolveRequestedTeamSize(NetworkCommunicator peer)
        {
            peer = ResolveBattleChooser(peer);
            if (!DikozemieArenaServerRegistration.TryGetCharacterProfile(
                    peer,
                    out DikozemieArenaCharacterProfile profile))
            {
                return 1;
            }

            DikozemieNavalBattleDefinition navalBattle =
                ResolveRequestedNavalBattle(peer);
            if (navalBattle != null)
            {
                return Math.Min(
                    DikozemieArenaTeamRules.MaximumTeamSize,
                    Math.Max(1, navalBattle.CrewCapacity));
            }

            return DikozemieArenaTeamRules.NormalizeTeamSize(
                profile.RequestedTeamSize,
                ResolveRequestedDiscipline(peer));
        }

        private static DikozemieArenaDiscipline ResolveRequestedDiscipline(
            NetworkCommunicator peer)
        {
            peer = ResolveBattleChooser(peer);
            // A sailor is an ordinary fighter, never the helmsman.  Force the
            // on-foot rules even when the saved arena preset was cavalry.
            if (ResolveRequestedNavalBattle(peer) != null)
            {
                return DikozemieArenaDiscipline.Foot;
            }
            if (!DikozemieArenaServerRegistration.TryGetCharacterProfile(
                    peer,
                    out DikozemieArenaCharacterProfile profile))
            {
                return DikozemieArenaDiscipline.Open;
            }
            return profile.RequestedDiscipline ==
                    (int)DikozemieArenaDiscipline.Foot ||
                profile.RequestedDiscipline ==
                    (int)DikozemieArenaDiscipline.Mounted
                    ? (DikozemieArenaDiscipline)profile.RequestedDiscipline
                    : DikozemieArenaDiscipline.Open;
        }

        private static DikozemieArenaComposition ResolveRequestedComposition(
            NetworkCommunicator peer)
        {
            peer = ResolveBattleChooser(peer);
            // Naval crews are always a mixed foot complement: infantry,
            // archers and skirmishers.  A saved arena filter must not turn a
            // ship into an all-cavalry or single-weapon roster.
            if (ResolveRequestedNavalBattle(peer) != null)
            {
                return DikozemieArenaComposition.Mixed;
            }
            DikozemieArenaDiscipline discipline =
                ResolveRequestedDiscipline(peer);
            if (!DikozemieArenaServerRegistration.TryGetCharacterProfile(
                    peer,
                    out DikozemieArenaCharacterProfile profile))
            {
                return DikozemieArenaComposition.Mixed;
            }
            DikozemieArenaComposition composition =
                (DikozemieArenaComposition)profile.RequestedComposition;
            return DikozemieArenaCompositionRules.IsSupported(
                composition,
                discipline)
                    ? composition
                    : DikozemieArenaComposition.Mixed;
        }

        private static DikozemieNavalBattleDefinition
            ResolveRequestedNavalBattle(NetworkCommunicator peer)
        {
            peer = ResolveBattleChooser(peer);
            if (!DikozemieArenaSceneRules.IsNavalBoardingScene(
                    MultiplayerOptions.OptionType.Map.GetStrValue()) ||
                !DikozemieArenaServerRegistration.TryGetCharacterProfile(
                    peer,
                    out DikozemieArenaCharacterProfile profile) ||
                string.IsNullOrWhiteSpace(profile.RequestedNavalHullId))
            {
                return null;
            }
            return DikozemieNavalBattleCatalog.Find(
                profile.RequestedNavalHullId);
        }

        private void ProcessPendingMatchResult(DateTimeOffset now)
        {
            if (!_pendingMatchResult ||
                !_matchKnockout.TryResolve(now, out DikozemieArenaDuelOutcome outcome))
                return;
            NetworkCommunicator winnerPeer = _pendingMatchWinner;
            NetworkCommunicator loserPeer = _pendingMatchLoser;
            _pendingMatchResult = false;
            if (IsConnected(winnerPeer) && IsConnected(loserPeer))
            {
                if (outcome == DikozemieArenaDuelOutcome.Draw)
                    CompleteMatchDraw(winnerPeer, loserPeer);
                else
                    CompleteMatchVictory(winnerPeer, loserPeer);
            }
            _pendingMatchWinner = null;
            _pendingMatchLoser = null;
            _pendingMatchWinnerAgent = null;
            _pendingMatchLoserAgent = null;
        }

        private void CompleteMatchVictory(
            NetworkCommunicator killerNetworkPeer, NetworkCommunicator victimNetworkPeer)
        {
            MissionPeer victimPeer = victimNetworkPeer.GetComponent<MissionPeer>();
            MissionPeer killerPeer = killerNetworkPeer.GetComponent<MissionPeer>();
            DikozemieArenaRepresentative victimRep =
                victimNetworkPeer.GetComponent<DikozemieArenaRepresentative>();
            if (victimRep != null)
            {
                ChangeProgress(victimRep, victimRep.RoundWins, victimRep.Deaths + 1,
                    victimRep.Rating, victimRep.RatingWins);
                ChangeCurrentGoldForPeer(victimPeer, InitialGold);
            }

            DikozemieArenaRepresentative killerRep =
                killerNetworkPeer.GetComponent<DikozemieArenaRepresentative>();
            if (killerRep == null)
            {
                return;
            }

            int nextRoundWins = killerRep.RoundWins + 1;
            ChangeProgress(
                killerRep,
                nextRoundWins,
                killerRep.Deaths,
                killerRep.Rating,
                killerRep.RatingWins);

            if (nextRoundWins < _winsRequired || victimRep == null)
            {
                return;
            }

            _duelCompleted = true;

            int delta = DikozemieArenaRatingStore.RecordResult(
                killerNetworkPeer,
                victimNetworkPeer);
            DikozemieArenaRatingEntry winnerRating =
                DikozemieArenaRatingStore.Get(killerNetworkPeer);
            DikozemieArenaRatingEntry loserRating =
                DikozemieArenaRatingStore.Get(victimNetworkPeer);
            ArenaSeriesState winnerSeries = GetSeriesState(killerNetworkPeer);
            ArenaSeriesState loserSeries = GetSeriesState(victimNetworkPeer);
            winnerSeries.CompletedBattles++;
            winnerSeries.Wins++;
            winnerSeries.CoinsEarned += delta;
            loserSeries.CompletedBattles++;
            loserSeries.Losses++;
            bool winnerSeriesComplete =
                winnerSeries.CompletedBattles >= winnerSeries.TargetBattles;
            bool loserSeriesComplete =
                loserSeries.CompletedBattles >= loserSeries.TargetBattles;
            ChangeProgress(
                killerRep,
                winnerSeries.Wins,
                winnerSeries.Losses,
                winnerRating.Rating,
                winnerRating.Wins);
            ChangeProgress(
                victimRep,
                loserSeries.Wins,
                loserSeries.Losses,
                loserRating.Rating,
                loserRating.Wins);

            GameNetwork.BeginBroadcastModuleEvent();
            GameNetwork.WriteMessage(new DikozemieArenaMatchResultMessage(
                killerNetworkPeer,
                victimNetworkPeer,
                delta,
                delta,
                LoserCampaignReward,
                winnerRating,
                loserRating,
                winnerSeries.CompletedBattles,
                winnerSeries.Wins,
                winnerSeries.Losses,
                winnerSeries.CoinsEarned,
                winnerSeriesComplete,
                loserSeries.CompletedBattles,
                loserSeries.Wins,
                loserSeries.Losses,
                loserSeries.CoinsEarned,
                loserSeriesComplete));
            GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.None);
            if (winnerSeriesComplete)
            {
                DikozemieEconomyInbox.Result(killerNetworkPeer, "arena", winnerSeries.CoinsEarned,
                    DikozemieArenaTeamRules.CalculateLossCoinPenalty(1, winnerSeries.Losses),
                    "дуэль с " + victimPeer.Name + ": побед " + winnerSeries.Wins +
                    ", поражений " + winnerSeries.Losses);
            }
            if (loserSeriesComplete)
            {
                DikozemieEconomyInbox.Result(victimNetworkPeer, "arena", loserSeries.CoinsEarned,
                    DikozemieArenaTeamRules.CalculateLossCoinPenalty(1, loserSeries.Losses),
                    "дуэль с " + killerPeer.Name + ": побед " + loserSeries.Wins +
                    ", поражений " + loserSeries.Losses);
            }

            // Keep the result panel and the earned experience in a deterministic
            // order on every client.  Previously the progress event arrived first
            // and could be hidden by the result panel refresh that followed it.
            SendSkillProgress(killerNetworkPeer, victory: true);
            SendSkillProgress(victimNetworkPeer, victory: false);

            if (winnerSeriesComplete || loserSeriesComplete)
            {
                DikozemieArenaLeaderboardTransport.Broadcast(_admittedPeers);
            }
            Console.WriteLine(
                "DikozemieOnline: queued 1v1 winner=" + killerPeer.Name +
                " loser=" + victimPeer.Name + " ratingDelta=" + delta);
        }

        private void CompleteMatchDraw(
            NetworkCommunicator firstPeer, NetworkCommunicator secondPeer)
        {
            if (_duelCompleted) return;
            _duelCompleted = true;
            DikozemieArenaRatingEntry firstRating = DikozemieArenaRatingStore.Get(firstPeer);
            DikozemieArenaRatingEntry secondRating = DikozemieArenaRatingStore.Get(secondPeer);
            ArenaSeriesState first = GetSeriesState(firstPeer);
            ArenaSeriesState second = GetSeriesState(secondPeer);
            first.CompletedBattles++;
            second.CompletedBattles++;
            ChangeProgress(firstPeer.GetComponent<DikozemieArenaRepresentative>(),
                first.Wins, first.Losses, firstRating.Rating, firstRating.Wins);
            ChangeProgress(secondPeer.GetComponent<DikozemieArenaRepresentative>(),
                second.Wins, second.Losses, secondRating.Rating, secondRating.Wins);
            // Every victory earns >=1; Delta=0 encodes no winner. The stable
            // result packet remains backward-compatible with older clients.
            GameNetwork.BeginBroadcastModuleEvent();
            GameNetwork.WriteMessage(new DikozemieArenaMatchResultMessage(
                firstPeer, secondPeer, 0, 0, 0, firstRating, secondRating,
                first.CompletedBattles, first.Wins, first.Losses, first.CoinsEarned,
                first.CompletedBattles >= first.TargetBattles,
                second.CompletedBattles, second.Wins, second.Losses, second.CoinsEarned,
                second.CompletedBattles >= second.TargetBattles));
            GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.None);
            if (first.CompletedBattles >= first.TargetBattles)
            {
                DikozemieEconomyInbox.Result(firstPeer, "arena", first.CoinsEarned,
                    DikozemieArenaTeamRules.CalculateLossCoinPenalty(1, first.Losses), "дуэль: ничья");
            }
            if (second.CompletedBattles >= second.TargetBattles)
            {
                DikozemieEconomyInbox.Result(secondPeer, "arena", second.CoinsEarned,
                    DikozemieArenaTeamRules.CalculateLossCoinPenalty(1, second.Losses), "дуэль: ничья");
            }
            SendSkillProgress(firstPeer, victory: false);
            SendSkillProgress(secondPeer, victory: false);
            DikozemieArenaLeaderboardTransport.Broadcast(_admittedPeers);
            Trace("1v1 result draw; rating and wins unchanged");
        }

        private bool HandleTrainingAgentRemoved(
            Agent victim,
            Agent killer,
            AgentState state)
        {
            if (state != AgentState.Unconscious && state != AgentState.Killed)
            {
                return false;
            }

            if (victim == _activeBot)
            {
                _pendingTrainingBotAgent = victim;
                _activeBot = null;
                NetworkCommunicator killerPeer =
                    ResolveDuelAgentPeer(killer);
                if (killerPeer != _activeAttacker || killer == victim ||
                    !killer.IsEnemyOf(victim))
                {
                    _nextTrainingBotSpawnAt = DateTimeOffset.UtcNow.AddSeconds(2);
                    return true;
                }

                DikozemieArenaRepresentative representative =
                    _activeAttacker.GetComponent<DikozemieArenaRepresentative>();
                int playerWins = (representative?.RoundWins ?? 0) + 1;
                _pendingTrainingPlayerAgent = killer;
                // A ranked bot entry is exactly one 1x1 duel.  Reusing the
                // configurable human-series round limit here used to spawn a
                // second and then a third bot after the first one died.
                QueueTrainingResult(victory: true, playerWins);
                return true;
            }

            NetworkCommunicator victimPeer = ResolveDuelAgentPeer(victim);
            if (victimPeer != _activeAttacker)
            {
                return false;
            }

            DikozemieArenaRepresentative playerRepresentative =
                _activeAttacker.GetComponent<DikozemieArenaRepresentative>();
            _pendingTrainingPlayerAgent = victim;
            _pendingTrainingBotAgent = _activeBot;
            _botRoundWins++;
            QueueTrainingResult(
                victory: false,
                playerRepresentative?.RoundWins ?? 0);
            return true;
        }

        private void QueueTrainingResult(bool victory, int playerScore)
        {
            if (_duelCompleted || _pendingTrainingResult)
            {
                return;
            }

            _pendingTrainingPlayerScore = playerScore;
            _trainingKnockout.Reset();
            _trainingKnockout.RegisterDeath(!victory, DateTimeOffset.UtcNow,
                PostKnockoutPresentation);
            _pendingTrainingResultAt =
                _trainingKnockout.ResolveAt;
            _pendingTrainingResult = true;
            Trace("training result queued victory=" + victory);
        }

        private void ProcessPendingTrainingResult(DateTimeOffset now)
        {
            if (!_pendingTrainingResult || now < _pendingTrainingResultAt)
            {
                return;
            }

            if (!_trainingKnockout.TryResolve(now, out DikozemieArenaDuelOutcome outcome))
                return;
            bool victory = outcome == DikozemieArenaDuelOutcome.AttackerVictory;
            int playerScore = _pendingTrainingPlayerScore;
            _pendingTrainingResult = false;
            _pendingTrainingResultAt = default;
            Trace("training result processing victory=" + victory);
            Trace("training result outcome=" + outcome);
            CompleteTrainingDuel(victory, playerScore,
                draw: outcome == DikozemieArenaDuelOutcome.Draw);
        }

        private void CompleteTrainingDuel(bool victory, int playerScore, bool draw = false)
        {
            if (_duelCompleted || !IsConnected(_activeAttacker))
            {
                return;
            }

            // Mark the duel complete before any result side effects.  This
            // prevents a second removal notification from resolving it again.
            _duelCompleted = true;

            BasicCharacterObject opponentCharacter = MBObjectManager.Instance
                .GetObject<BasicCharacterObject>(_activeBotCharacterId);
            string opponentName = opponentCharacter?.Name?.ToString() ??
                _activeBotCharacterId;
            Trace("training result rating begin victory=" + victory);
            int ratingDelta = DikozemieArenaRatingStore.RecordTrainingResult(
                _activeAttacker,
                _activeBotCharacterId,
                opponentName,
                _activeBotRating,
                victory,
                out DikozemieArenaRatingEntry playerRating,
                out DikozemieArenaRatingEntry botRating,
                draw);
            Trace("training result rating complete player=" + playerRating.Rating);
            ArenaSeriesState series = GetSeriesState(_activeAttacker);
            series.CompletedBattles++;
            if (victory)
            {
                series.Wins++;
                series.CoinsEarned += ratingDelta;
            }
            else if (!draw)
            {
                series.Losses++;
            }

            DikozemieArenaRepresentative representative =
                _activeAttacker.GetComponent<DikozemieArenaRepresentative>();
            ChangeProgress(
                representative,
                series.Wins,
                series.Losses,
                playerRating.Rating,
                playerRating.Wins);
            Trace("training result progress sent");
            bool seriesComplete = series.CompletedBattles >= series.TargetBattles;
            if (seriesComplete)
            {
                DikozemieEconomyInbox.Result(
                    _activeAttacker,
                    "training",
                    series.CoinsEarned,
                    DikozemieArenaTeamRules.CalculateLossCoinPenalty(CurrentTeamSize, series.Losses),
                    "дуэль с ботом: побед " + series.Wins + ", поражений " + series.Losses);
            }
            if (seriesComplete || draw)
            {
                GameNetwork.BeginModuleEventAsServer(_activeAttacker);
                GameNetwork.WriteMessage(new DikozemieArenaTrainingResultMessage(
                    victory,
                    draw ? 0 : playerScore,
                    draw ? 0 : _botRoundWins,
                    _activeBotCharacterId,
                    playerRating,
                    botRating,
                    series.CompletedBattles,
                    series.Wins,
                    series.Losses,
                    series.CoinsEarned,
                    victory ? ratingDelta : 0));
                GameNetwork.EndModuleEventAsServer();
                Trace("training result message sent");
                // The progress event follows the result so the result VM can
                // immediately show the current duel's skill gains.
                SendSkillProgress(_activeAttacker, victory);
                DikozemieArenaLeaderboardTransport.Broadcast(_admittedPeers);
                Trace("training result leaderboard queued");
            }
            else
            {
                SendSkillProgress(_activeAttacker, victory);
            }
            Console.WriteLine(
                "DikozemieOnline: training 1v1 battle=" +
                series.CompletedBattles + "/" + series.TargetBattles +
                " victory=" + victory + " ratingDelta=" + ratingDelta);
        }

        private ArenaSeriesState GetSeriesState(NetworkCommunicator peer)
        {
            if (!_seriesStates.TryGetValue(peer, out ArenaSeriesState series))
            {
                series = CreateSeriesState(peer);
                _seriesStates[peer] = series;
            }
            return series;
        }

        private static ArenaSeriesState CreateSeriesState(NetworkCommunicator peer)
        {
            int targetBattles = 1;
            if (DikozemieArenaServerRegistration.TryGetCharacterProfile(
                    peer,
                    out DikozemieArenaCharacterProfile profile) &&
                (profile.RequestedBattleCount == 1 ||
                 profile.RequestedBattleCount == 3))
            {
                targetBattles = profile.RequestedBattleCount;
            }
            return new ArenaSeriesState { TargetBattles = targetBattles };
        }

        private sealed class ArenaSeriesState
        {
            public int TargetBattles = 1;
            public int CompletedBattles;
            public int Wins;
            public int Losses;
            public int CoinsEarned;
            public int RatingEarned;
        }

        private void AddDuelSkillExperience(
            NetworkCommunicator peer,
            string skillId,
            int amount)
        {
            if (peer == null || string.IsNullOrWhiteSpace(skillId) || amount <= 0)
            {
                return;
            }
            if (!_duelSkillExperience.TryGetValue(
                    peer,
                    out Dictionary<string, int> experience))
            {
                experience = new Dictionary<string, int>(StringComparer.Ordinal);
                _duelSkillExperience[peer] = experience;
            }
            experience.TryGetValue(skillId, out int current);
            experience[skillId] = Math.Min(5000, current + amount);
        }

        private int ResolveEarnedHeroExperience(NetworkCommunicator peer)
        {
            if (peer == null)
            {
                return 0;
            }

            int total = 0;
            Dictionary<string, int> experience = null;
            if (_duelSkillExperience.TryGetValue(peer, out experience))
            {
                total = experience.Values.Sum();
            }

            string movementSkill = _duelMovementSkills.TryGetValue(
                peer,
                out string rememberedMovementSkill)
                ? rememberedMovementSkill
                : "Athletics";
            int currentMovementExperience = 0;
            if (experience != null)
            {
                experience.TryGetValue(
                    movementSkill,
                    out currentMovementExperience);
            }
            total += Math.Min(8, Math.Max(0, 5000 - currentMovementExperience));
            return Math.Max(1, total);
        }

        // Every joining client checks that it registered the same items in the
        // same order (DikozemieItemRegistry): items travel as object ids.
        private void SendItemRegistry(NetworkCommunicator peer)
        {
            if (!IsConnected(peer) || DikozemieItemRegistry.Count == 0)
            {
                return;
            }

            GameNetwork.BeginModuleEventAsServer(peer);
            GameNetwork.WriteMessage(new DikozemieArenaItemRegistryMessage(
                DikozemieItemRegistry.Count,
                DikozemieItemRegistry.Fingerprint));
            GameNetwork.EndModuleEventAsServer();
        }

        private void SendDeathSummary(NetworkCommunicator peer)
        {
            if (!IsConnected(peer))
            {
                return;
            }

            int heroExperience = ResolveEarnedHeroExperience(peer);
            GameNetwork.BeginModuleEventAsServer(peer);
            GameNetwork.WriteMessage(
                new DikozemieArenaDeathSummaryMessage(heroExperience));
            GameNetwork.EndModuleEventAsServer();
            Trace(
                "death summary peer=" + DescribePeer(peer) +
                " heroXp=" + heroExperience);
        }

        private void SendSkillProgress(NetworkCommunicator peer, bool victory)
        {
            if (!IsConnected(peer))
            {
                return;
            }

            if (!_duelMovementSkills.TryGetValue(peer, out string movementSkill))
            {
                MissionPeer missionPeer = peer.GetComponent<MissionPeer>();
                movementSkill = missionPeer?.ControlledAgent?.MountAgent != null
                    ? "Riding"
                    : "Athletics";
            }
            AddDuelSkillExperience(peer, movementSkill, 8);

            if (!_duelSkillExperience.TryGetValue(
                    peer,
                    out Dictionary<string, int> experience))
            {
                experience = new Dictionary<string, int>(StringComparer.Ordinal);
            }
            string payload = string.Join(
                ";",
                experience.OrderBy(pair => pair.Key)
                    .Select(pair => pair.Key + "=" + pair.Value));
            int heroExperience = Math.Max(1, experience.Values.Sum());
            if (DikozemieArenaServerRegistration.TryGetCharacterProfile(
                    peer,
                    out DikozemieArenaCharacterProfile profile))
            {
                // The next duel in the same mission must already use the newly
                // earned skill values; reconnecting is not required.
                DikozemieArenaCombatProgression.Apply(profile, experience);
            }
            GameNetwork.BeginModuleEventAsServer(peer);
            GameNetwork.WriteMessage(new DikozemieArenaSkillProgressMessage(
                victory,
                heroExperience,
                payload));
            GameNetwork.EndModuleEventAsServer();
            Trace(
                "skill progress peer=" + DescribePeer(peer) +
                " victory=" + victory +
                " heroXp=" + heroExperience +
                " skills=" + payload);
            _duelSkillExperience.Remove(peer);
            _duelMovementSkills.Remove(peer);
        }

        private void RememberMovementSkill(
            NetworkCommunicator peer,
            Agent agent)
        {
            if (!IsActiveDuelist(peer) || agent == null || !agent.IsHuman)
            {
                return;
            }
            if (peer == _activeAttacker) _activeAttackerAgent = agent;
            else if (peer == _activeDefender) _activeDefenderAgent = agent;
            string movementSkill = "Athletics";
            if (agent.MountAgent != null)
            {
                movementSkill = "Riding";
            }
            _duelMovementSkills[peer] = movementSkill;
        }

        private static bool IsTrainableCombatSkill(string skillId)
        {
            return string.Equals(skillId, "OneHanded", StringComparison.Ordinal) ||
                string.Equals(skillId, "TwoHanded", StringComparison.Ordinal) ||
                string.Equals(skillId, "Polearm", StringComparison.Ordinal) ||
                string.Equals(skillId, "Bow", StringComparison.Ordinal) ||
                string.Equals(skillId, "Crossbow", StringComparison.Ordinal) ||
                string.Equals(skillId, "Throwing", StringComparison.Ordinal);
        }

        private static string ResolveCombatSkillId(
            Agent affectorAgent,
            WeaponComponentData attackerWeapon)
        {
            string skillId = attackerWeapon?.RelevantSkill?.StringId;
            if (IsTrainableCombatSkill(skillId))
            {
                return skillId;
            }

            // For projectile hits Bannerlord may report the arrow/bolt as
            // attackerWeapon.  The ammunition has no Bow/Crossbow skill, while
            // the weapon still held by the attacker does.
            MissionWeapon wieldedWeapon = affectorAgent?.WieldedWeapon ??
                MissionWeapon.Invalid;
            skillId = wieldedWeapon.CurrentUsageItem?.RelevantSkill?.StringId;
            if (IsTrainableCombatSkill(skillId))
            {
                return skillId;
            }

            ItemObject item = wieldedWeapon.Item;
            if (item == null)
            {
                return null;
            }
            switch (item.ItemType)
            {
                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                    return "OneHanded";
                case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                    return "TwoHanded";
                case ItemObject.ItemTypeEnum.Polearm:
                    return "Polearm";
                case ItemObject.ItemTypeEnum.Bow:
                    return "Bow";
                case ItemObject.ItemTypeEnum.Crossbow:
                    return "Crossbow";
                case ItemObject.ItemTypeEnum.Thrown:
                    return "Throwing";
                default:
                    return null;
            }
        }

        public void ChangeProgress(
            DikozemieArenaRepresentative representative,
            int roundWins,
            int deaths,
            int rating,
            int ratingWins)
        {
            if (representative == null ||
                !representative.Peer.Communicator.IsConnectionActive)
            {
                return;
            }

            Debug.Assert(
                GameNetwork.IsServerOrRecorder,
                "Arena progress must be server-authoritative");
            representative.UpdateProgress(roundWins, deaths, rating, ratingWins);
            GameNetwork.BeginBroadcastModuleEvent();
            GameNetwork.WriteMessage(new DikozemieArenaProgressMessage(
                representative,
                roundWins,
                deaths,
                rating,
                ratingWins));
            GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.None);
        }

        private static void SendProgress(
            NetworkCommunicator receiver,
            DikozemieArenaRepresentative representative)
        {
            GameNetwork.BeginModuleEventAsServer(receiver);
            GameNetwork.WriteMessage(new DikozemieArenaProgressMessage(
                representative,
                representative.RoundWins,
                representative.Deaths,
                representative.Rating,
                representative.RatingWins));
            GameNetwork.EndModuleEventAsServer();
        }

        public override Team GetWinnerTeam() => null;

        public override bool CheckForMatchEnd() => false;

        private static bool IsConnected(NetworkCommunicator peer) =>
            peer != null && peer.IsConnectionActive;

        private static int ResolvePlayerRating(NetworkCommunicator peer)
        {
            return Math.Max(0, DikozemieArenaRatingStore.Get(peer).Rating);
        }

        private static void EnsureRepresentative(NetworkCommunicator peer)
        {
            if (peer != null &&
                peer.GetComponent<DikozemieArenaRepresentative>() == null)
            {
                peer.AddComponent<DikozemieArenaRepresentative>();
            }
        }

        private static string DescribePeer(NetworkCommunicator peer) =>
            peer == null
                ? "null"
                : peer.UserName + "/" +
                  peer.PlayerConnectionInfo?.PlayerID.ToString() +
                  " active=" + peer.IsConnectionActive +
                  " sync=" + peer.IsSynchronized;

        // One open writer instead of opening and closing the file for every
        // line: under Wine each open/close costs milliseconds, and a battle
        // start writes dozens of lines in one frame.
        private static StreamWriter _diagnosticWriter;
        private static bool _diagnosticWriterFailed;

        internal static void Trace(string message)
        {
            try
            {
                lock (DiagnosticGate)
                {
                    if (_diagnosticWriter == null && !_diagnosticWriterFailed)
                    {
                        // Only the dedicated server's Windows user has this
                        // folder; a client stops trying after one failure.
                        _diagnosticWriterFailed = true;
                        Directory.CreateDirectory(Path.GetDirectoryName(DiagnosticLogPath));
                        _diagnosticWriter = new StreamWriter(
                            new FileStream(
                                DiagnosticLogPath,
                                FileMode.Append,
                                FileAccess.Write,
                                FileShare.ReadWrite | FileShare.Delete),
                            new System.Text.UTF8Encoding(false));
                    }
                    if (_diagnosticWriter != null)
                    {
                        _diagnosticWriter.WriteLine(
                            DateTimeOffset.Now.ToString("O") + " " + message);
                        _diagnosticWriter.Flush();
                    }
                }
            }
            catch
            {
                // Diagnostics must never affect a live match.
            }
#if CLIENT
            Debug.Print(
                "[DikozemieArena] " + message,
                0,
                Debug.DebugColor.Cyan);
#else
            try
            {
                TaleWorlds.Engine.MBDebug.Print(
                    "[DikozemieArena] " + message,
                    0,
                    Debug.DebugColor.Cyan,
                    17592186044416UL);
            }
            catch
            {
                // Diagnostics must never affect a live match.
            }
#endif
        }
    }
}
