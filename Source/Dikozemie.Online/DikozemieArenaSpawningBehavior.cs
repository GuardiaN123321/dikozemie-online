using System;
using System.Collections.Generic;
using System.Linq;
using NetworkMessages.FromServer;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Dikozemie.Online
{
    public sealed class DikozemieArenaSpawningBehavior : SpawningBehaviorBase
    {
        internal static DikozemieArenaSpawningBehavior Active { get; private set; }

        private const float BotTargetAssignmentDelay = 0.75f;

        private IReadOnlyList<DikozemieArenaOpponent> _trainingOpponents;
        private string _previousTrainingOpponentId;
        private const float BotEngagementInterval = 1f;
        private float _engagementElapsed;
        private float _engagementTraceElapsed;
        private readonly List<PendingBotActivation> _pendingBotActivations =
            new List<PendingBotActivation>();
        private readonly Dictionary<Agent, DikozemieArenaOpponent>
            _teamBotOpponents =
                new Dictionary<Agent, DikozemieArenaOpponent>();
        private readonly HashSet<Agent> _mountedTeamBots =
            new HashSet<Agent>();
        private readonly HashSet<string> _previousTeamFieldIds =
            new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        private readonly List<PendingNavalBoarder> _pendingNavalBoarders =
            new List<PendingNavalBoarder>();
        private float _navalBoardingElapsed;
        private bool _navalBoardingCompletionLogged;
        private int _navalSpawnTeamSize = 1;

        internal bool PlayerStartsAtAttackerWall { get; set; } = true;

        public DikozemieArenaSpawningBehavior()
        {
            IsSpawningEnabled = true;
        }

        public override void Initialize(SpawnComponent spawnComponent)
        {
            base.Initialize(spawnComponent);
            Active = this;
            // All arena bots are explicitly owned by this game mode. The native
            // automatic bot pool must stay empty (including formation escorts).
            MultiplayerOptions.OptionType.NumberOfBotsTeam1.SetValue(0);
            MultiplayerOptions.OptionType.NumberOfBotsTeam2.SetValue(0);
            MultiplayerOptions.OptionType.NumberOfBotsPerFormation.SetValue(0);
            if (GameMode.WarmupComponent == null)
            {
                RequestStartSpawnSession();
            }
        }

        public override void Clear()
        {
            _pendingBotActivations.Clear();
            _teamBotOpponents.Clear();
            _mountedTeamBots.Clear();
            _pendingNavalBoarders.Clear();
            _navalBoardingElapsed = 0f;
            _navalBoardingCompletionLogged = false;
            if (ReferenceEquals(Active, this))
            {
                Active = null;
            }
            base.Clear();
        }

        public override void OnTick(float dt)
        {
            long started = DikozemieServerPerf.Start();
            DikozemieServerPerf.Crumb("spawn tick");
            ProcessPendingBotActivations();
            KeepBotsEngaged(dt);
            MissionMultiplayerDikozemieArena arena =
                Mission.GetMissionBehavior<MissionMultiplayerDikozemieArena>();
            bool hasCandidate = arena != null &&
                GameNetwork.NetworkPeers.Any(arena.CanSpawnPeer);
            if (IsSpawningEnabled && hasCandidate &&
                SpawnCheckTimer.Check(Mission.CurrentTime))
            {
                try
                {
                    SpawnAgents();
                }
                catch (Exception exception)
                {
                    MissionMultiplayerDikozemieArena.Trace(
                        "player spawn failed: " + exception);
                }
            }
            DikozemieServerPerf.Crumb("spawn base tick");
            base.OnTick(dt);
            DikozemieServerPerf.Stop(DikozemieServerPerf.Section.SpawnTick, started);
            DikozemieServerPerf.Crumb("spawn tick done");
        }

        protected override void SpawnAgents()
        {
            MissionMultiplayerDikozemieArena arena =
                Mission.GetMissionBehavior<MissionMultiplayerDikozemieArena>();
            BasicCultureObject attackers = MBObjectManager.Instance.GetObject<BasicCultureObject>(
                MultiplayerOptions.OptionType.CultureTeam1.GetStrValue());
            BasicCultureObject defenders = MBObjectManager.Instance.GetObject<BasicCultureObject>(
                MultiplayerOptions.OptionType.CultureTeam2.GetStrValue());

            foreach (NetworkCommunicator networkPeer in GameNetwork.NetworkPeers)
            {
                if (!networkPeer.IsSynchronized ||
                    arena?.CanSpawnPeer(networkPeer) != true)
                {
                    continue;
                }

                MissionPeer peer = networkPeer.GetComponent<MissionPeer>();
                if (peer == null || peer.ControlledAgent != null || peer.HasSpawnedAgentVisuals ||
                    peer.Team == null || peer.Team == Mission.SpectatorTeam ||
                    peer.Culture == null || !peer.TeamInitialPerkInfoReady ||
                    !peer.SpawnTimer.Check(Mission.CurrentTime))
                {
                    continue;
                }

                BasicCultureObject teamCulture = peer.Team.Side == BattleSideEnum.Attacker ? attackers : defenders;
                DikozemieArenaRatingEntry rating = DikozemieArenaRatingStore.Get(networkPeer);
                if (!DikozemieArenaProgressionRules.IsClassUnlocked(
                        peer.SelectedTroopIndex,
                        rating.Wins))
                {
                    ResetInvalidTroop(peer, networkPeer);
                    continue;
                }

                var heroClass = MultiplayerClassDivisions.GetMPHeroClassForPeer(peer);
                if (heroClass == null)
                {
                    ResetInvalidTroop(peer, networkPeer);
                    continue;
                }

                bool hasProfile = DikozemieArenaServerRegistration.TryGetCharacterProfile(
                    networkPeer,
                    out DikozemieArenaCharacterProfile profile);
                BasicCharacterObject profileCharacter = hasProfile
                    ? MBObjectManager.Instance.GetObject<BasicCharacterObject>(
                        profile.StartingCharacterId)
                    : null;
                // Keep a network-safe MP transport character, but pass the
                // profile to the actual SpawnAgent call. The native visual-only
                // path rebuilds AgentBuildData from the MP class on the next
                // tick, losing the profile's horse and combat mission slots.
                BasicCharacterObject character = heroClass.HeroCharacter;
                if (hasProfile && profileCharacter == null)
                {
                    Console.WriteLine(
                        "DikozemieOnline: created hero character is unavailable: " +
                        profile.StartingCharacterId);
                }
                bool useProfileEquipment = hasProfile &&
                    profile.Equipment != null && profile.Equipment.Length > 0;
                Equipment equipment = useProfileEquipment
                    ? CreateCampaignEquipment(profile)
                    : character.Equipment.Clone();
                if (!useProfileEquipment)
                {
                    var alternatives = MPPerkObject.GetOnSpawnPerkHandler(peer)?.GetAlternativeEquipments(true);
                    if (alternatives != null)
                    {
                        foreach (var alternative in alternatives)
                        {
                            equipment[alternative.Item1] = alternative.Item2;
                        }
                    }

                    GameMode.AddCosmeticItemsToEquipment(
                        equipment,
                        GameMode.GetUsedCosmeticsFromPeer(peer, character));
                }
                DikozemieArenaDiscipline discipline = hasProfile
                    ? NormalizeDiscipline(profile.RequestedDiscipline)
                    : DikozemieArenaDiscipline.Open;
                DikozemieArenaComposition composition = hasProfile &&
                    profile.RequestedComposition >=
                        (int)DikozemieArenaComposition.Mixed &&
                    profile.RequestedComposition <=
                        (int)DikozemieArenaComposition.Ranged
                        ? (DikozemieArenaComposition)
                            profile.RequestedComposition
                        : DikozemieArenaComposition.Mixed;
                if (DikozemieArenaSceneRules.IsNavalBoardingScene(
                        MultiplayerOptions.OptionType.Map.GetStrValue()))
                {
                    // Sea combat never spawns a mount.  This applies to the
                    // campaign hero as well as cavalry-class bot characters.
                    discipline = DikozemieArenaDiscipline.Foot;
                    composition = DikozemieArenaComposition.Mixed;
                }
                if (useProfileEquipment)
                {
                    ApplyPlayerDisciplineToEquipment(equipment, discipline);
                    DikozemieArenaLoadoutRules.ApplyCompositionToEquipment(
                        equipment,
                        composition);
                }
                else
                {
                    ApplyDisciplineToEquipment(
                        equipment,
                        discipline,
                        teamCulture);
                }

                BodyProperties bodyProperties = GetBodyProperties(peer, teamCulture);
                bool isFemale = peer.Peer.IsFemale;
                if (hasProfile)
                {
                    isFemale = profile.IsFemale;
                    if (!string.IsNullOrWhiteSpace(profile.BodyProperties) &&
                        BodyProperties.FromString(
                            profile.BodyProperties,
                            out BodyProperties profileBodyProperties))
                    {
                        bodyProperties = profileBodyProperties;
                    }
                }
                AgentBuildData buildData = new AgentBuildData(character)
                    .MissionPeer(peer)
                    .Equipment(equipment)
                    .Team(peer.Team)
                    .IsFemale(isFemale)
                    .BodyProperties(bodyProperties)
                    .Age((int)bodyProperties.Age)
                    .VisualsIndex(0)
                    .ClothingColor1(peer.Team == Mission.AttackerTeam ? teamCulture.Color : teamCulture.ClothAlternativeColor)
                    .ClothingColor2(peer.Team == Mission.AttackerTeam ? teamCulture.Color2 : teamCulture.ClothAlternativeColor2)
                    .TroopOrigin(new BasicBattleAgentOrigin(character));

                MissionMultiplayerDikozemieArena.Trace(
                    "player spawn prepared character=" + character.StringId +
                    " profile=" + hasProfile +
                    " equipment=" + DescribeEquipment(equipment));

                // This arena has no native equipment-selection phase. Disable
                // equipment updates only after the peer is fully synchronized;
                // doing it during mission initialization breaks the first
                // network synchronization handshake.
                ToggleUpdatingSpawnEquipment(false);
                MatrixFrame spawnFrame = SpawnComponent.GetSpawnFrame(
                    peer.Team,
                    equipment[EquipmentIndex.Horse].Item != null,
                    peer.SpawnCountThisRound == 0);
                if (DikozemieArenaSceneRules.IsNavalBoardingScene(
                        MultiplayerOptions.OptionType.Map.GetStrValue()))
                {
                    _navalSpawnTeamSize = Math.Max(
                        1,
                        arena?.CurrentTeamSize ?? 1);
                    bool attacker = peer.Team == Mission.AttackerTeam;
                    spawnFrame.origin = GetNavalCrewPosition(
                        attacker,
                        index: 0,
                        count: _navalSpawnTeamSize);
                    // Waiting for the crew: in the middle of the deck. Against
                    // the rail the ship stayed below the camera and the player
                    // seemed to stand on open water.
                    DikozemieNavalBoardingBehavior waitingDeck =
                        DikozemieNavalBoardingBehavior.Active;
                    if (waitingDeck != null)
                    {
                        spawnFrame.origin = waitingDeck.GetDeckPosition(
                            attacker,
                            attacker ? -1f : 1f,
                            0f);
                    }
                }
                Vec2 spawnDirection = spawnFrame.rotation.f.AsVec2.Normalized();
                if (!DikozemieArenaSceneRules.IsNavalBoardingScene(
                        MultiplayerOptions.OptionType.Map.GetStrValue()))
                {
                    // The native spawn point looks anywhere. A rider and his
                    // horse keep that direction, so face the middle of the ring.
                    DikozemieArenaSpawnLayout spawnLayout =
                        DikozemieArenaSpawnLayouts.Resolve(
                            MultiplayerOptions.OptionType.Map.GetStrValue());
                    Vec3 toMiddle =
                        ((spawnLayout.AttackerGate + spawnLayout.DefenderGate) * 0.5f) -
                        spawnFrame.origin;
                    toMiddle.z = 0f;
                    if (toMiddle.Normalize() > 0.001f)
                    {
                        spawnDirection = toMiddle.AsVec2;
                    }
                }
                buildData.InitialPosition(in spawnFrame.origin)
                    .InitialDirection(spawnDirection);
                if (DikozemieArenaSceneRules.IsNavalBoardingScene(
                        MultiplayerOptions.OptionType.Map.GetStrValue()))
                {
                    // The runtime deck is valid physics but is intentionally
                    // outside the static open-sea navigation mesh.
                    buildData.CanSpawnOutsideOfMissionBoundary(true);
                }
                GameNetwork.BeginBroadcastModuleEvent();
                GameNetwork.WriteMessage(new SyncPerksForCurrentlySelectedTroop(
                    networkPeer, peer.Perks[peer.SelectedTroopIndex]));
                GameNetwork.EndBroadcastModuleEvent(
                    GameNetwork.EventBroadcastFlags.ExcludeOtherTeamPlayers,
                    networkPeer);

                Agent agent = Mission.SpawnAgent(buildData);
                agent.AddComponent(new MPPerksAgentComponent(agent));
                agent.MountAgent?.UpdateAgentProperties();
                arena?.RegisterOwnMount(agent);
                agent.HealthLimit += MPPerkObject.GetOnSpawnPerkHandler(peer)
                    ?.GetHitpoints(true) ?? 0f;
                agent.Health = agent.HealthLimit;
                agent.WieldInitialWeapons();
                peer.SpawnCountThisRound++;
                peer.HasSpawnedAgentVisuals = false;
                MPPerkObject.GetPerkHandler(peer)?.OnEvent(
                    MPPerkCondition.PerkEventFlags.SpawnEnd);
                MissionMultiplayerDikozemieArena.Trace(
                    "player spawned from profile mount=" +
                    (agent.MountAgent != null) +
                    " position=" + DescribePosition(agent.Position) +
                    " equipment=" + DescribeEquipment(equipment));
            }
        }

        public override bool AllowEarlyAgentVisualsDespawning(MissionPeer lobbyPeer)
        {
            return true;
        }

        internal Agent SpawnTrainingBot(
            float healthMultiplier,
            int playerRating,
            DikozemieArenaDiscipline discipline,
            DikozemieArenaComposition composition,
            out string opponentCharacterId,
            out int opponentRating)
        {
            BasicCultureObject culture = MBObjectManager.Instance.GetObject<BasicCultureObject>(
                MultiplayerOptions.OptionType.CultureTeam2.GetStrValue());
            if (culture == null || Mission?.DefenderTeam == null)
            {
                opponentCharacterId = string.Empty;
                opponentRating = 0;
                return null;
            }

            BasicCharacterObject fallback =
                MBObjectManager.Instance.GetObject<BasicCharacterObject>(
                    "mp_heavy_infantry_empire_troop") ??
                MultiplayerClassDivisions.GetMPHeroClasses(culture)
                    .FirstOrDefault()?.TroopCharacter;
            if (_trainingOpponents == null)
            {
                _trainingOpponents = DikozemieArenaOpponentCatalog.Build(fallback);
            }

            List<DikozemieArenaOpponent> eligibleOpponents =
                _trainingOpponents
                    .Where(candidate =>
                        DikozemieArenaOpponentCatalog.MatchesComposition(
                            candidate,
                            fallback,
                            discipline,
                            composition))
                    .ToList();
            if (eligibleOpponents.Count == 0)
            {
                eligibleOpponents = _trainingOpponents.ToList();
            }
            DikozemieArenaOpponent opponent =
                DikozemieArenaOpponentCatalog.SelectForPlayer(
                    eligibleOpponents,
                    Math.Max(0, playerRating),
                    _previousTrainingOpponentId);
            opponentRating = DikozemieArenaOpponentCatalog.GetCurrentRating(opponent);

            BasicCharacterObject selectedCharacter =
                DikozemieArenaOpponentCatalog.ResolveCharacter(opponent, fallback);
            Equipment sourceEquipment = selectedCharacter?.FirstBattleEquipment ??
                selectedCharacter?.Equipment;
            Equipment selectedEquipment = sourceEquipment == null
                ? new Equipment(Equipment.EquipmentType.Battle)
                : new Equipment(sourceEquipment);
            // The final server-compatible character may differ from the catalog
            // identity. Enforce the mount floor BEFORE SpawnAgent, so a random
            // native cavalry class can never create an unwanted mount.
            if (opponentRating < DikozemieArenaOpponentCatalog.MinimumMountedOpponentRating)
            {
                selectedEquipment[EquipmentIndex.Horse] = default(EquipmentElement);
                selectedEquipment[EquipmentIndex.HorseHarness] = default(EquipmentElement);
            }
            ApplyDisciplineToEquipment(selectedEquipment, discipline, culture);
            Agent bot = SpawnOwnedBot(selectedCharacter, selectedEquipment,
                Mission.DefenderTeam, culture);
            if (bot != null)
            {
                bot.Controller = AgentControllerType.None;
                // The catalog entry is the persistent ranked identity.  Its
                // runtime character may be a multiplayer-compatible visual
                // substitute on the dedicated server and must never create a
                // second leaderboard row.
                opponentCharacterId = opponent?.CharacterId ??
                    selectedCharacter?.StringId ??
                    bot.Character?.StringId ?? string.Empty;
                _previousTrainingOpponentId = opponentCharacterId;
                bot.HealthLimit = Math.Max(
                    1f,
                    bot.HealthLimit * healthMultiplier);
                bot.Health = bot.HealthLimit;
                bot.WieldInitialWeapons();
                Agent player = Mission.AttackerTeam.ActiveAgents.FirstOrDefault(
                    agent => agent != null && agent.MissionPeer != null);
                PositionDuelistsAtAuthoredGateStarts(player, bot);
                OrientTrainingCombatants(player, bot);
                _pendingBotActivations.Add(new PendingBotActivation
                {
                    Bot = bot,
                    Target = player,
                    ActivateAt = Mission.CurrentTime + BotTargetAssignmentDelay
                });
            }
            else
            {
                opponentCharacterId = string.Empty;
                opponentRating = 0;
            }
            return bot;
        }

        // Fills both sides of a team battle up to teamSize. The live players
        // are already spawned (attackerHumans / defenderHumans, possibly on
        // both sides); bots take only the empty places.
        internal IReadOnlyList<Agent> SpawnTeamBots(
            int teamSize,
            DikozemieArenaDiscipline discipline,
            DikozemieArenaComposition composition,
            float healthMultiplier,
            IReadOnlyList<Agent> attackerHumans,
            IReadOnlyList<Agent> defenderHumans,
            long attackerHumanRating,
            long defenderHumanRating,
            out DikozemieArenaTeamField field,
            int attackerSideSize = 0,
            int defenderSideSize = 0,
            int attackerSideRating = 0,
            int defenderSideRating = 0)
        {
            var spawned = new List<Agent>();
            attackerHumans = attackerHumans?.Where(agent => agent != null).ToList() ??
                new List<Agent>();
            defenderHumans = defenderHumans?.Where(agent => agent != null).ToList() ??
                new List<Agent>();
            int humanCount = attackerHumans.Count + defenderHumans.Count;
            int playerRating = (int)Math.Max(
                0L,
                (attackerHumanRating + defenderHumanRating) / Math.Max(1, humanCount));
            field = new DikozemieArenaTeamField
            {
                AttackerAverageRating = playerRating,
                DefenderAverageRating = playerRating
            };
            _teamBotOpponents.Clear();
            _mountedTeamBots.Clear();
            BasicCultureObject attackerCulture =
                MBObjectManager.Instance.GetObject<BasicCultureObject>(
                    MultiplayerOptions.OptionType.CultureTeam1.GetStrValue());
            BasicCultureObject defenderCulture =
                MBObjectManager.Instance.GetObject<BasicCultureObject>(
                    MultiplayerOptions.OptionType.CultureTeam2.GetStrValue());
            if (attackerCulture == null || defenderCulture == null ||
                Mission?.AttackerTeam == null || Mission.DefenderTeam == null)
            {
                return spawned;
            }

            bool naval = DikozemieArenaSceneRules.IsNavalBoardingScene(
                MultiplayerOptions.OptionType.Map.GetStrValue());
            int size = naval
                ? Math.Max(1, teamSize)
                : Math.Max(
                    2,
                    DikozemieArenaTeamRules.NormalizeTeamSize(
                        teamSize,
                        discipline));
            size = Math.Max(
                size,
                Math.Max(attackerHumans.Count, defenderHumans.Count));
            if (naval)
            {
                _navalSpawnTeamSize = size;
            }
            BasicCharacterObject fallback = ResolveTeamBotCharacter(
                defenderCulture,
                discipline,
                0);
            if (_trainingOpponents == null)
            {
                _trainingOpponents = DikozemieArenaOpponentCatalog.Build(fallback);
            }
            // Stage 6: a siege fields unequal sides.
            int attackerSize = !naval && attackerSideSize > 0
                ? Math.Max(attackerHumans.Count, attackerSideSize)
                : size;
            int defenderSize = !naval && defenderSideSize > 0
                ? Math.Max(defenderHumans.Count, defenderSideSize)
                : size;
            int friendlyBotCount = Math.Max(0, attackerSize - attackerHumans.Count);
            int enemyBotCount = Math.Max(0, defenderSize - defenderHumans.Count);
            int requestedBotCount = friendlyBotCount + enemyBotCount;
            bool sideStrength = !naval && (attackerSideRating > 0 || defenderSideRating > 0);
            List<DikozemieArenaOpponent> eligibleOpponents =
                _trainingOpponents
                    .Where(opponent =>
                        DikozemieArenaOpponentCatalog.MatchesComposition(
                            opponent,
                            fallback,
                            discipline,
                            composition))
                    .ToList();
            if (eligibleOpponents.Count < requestedBotCount)
            {
                Console.WriteLine(
                    "DikozemieOnline: composition pool is too small " +
                    "composition=" + composition + " eligible=" +
                    eligibleOpponents.Count + " requested=" +
                    requestedBotCount + "; using mixed reserve");
                eligibleOpponents = _trainingOpponents.ToList();
            }
            List<DikozemieArenaOpponent> freshOpponents = eligibleOpponents
                .Where(opponent => opponent != null &&
                    !_previousTeamFieldIds.Contains(opponent.CharacterId))
                .ToList();
            IReadOnlyList<DikozemieArenaOpponent> selectionPool =
                freshOpponents.Count >= requestedBotCount
                    ? freshOpponents
                    : eligibleOpponents;
            IReadOnlyList<DikozemieArenaOpponent> SelectField(
                IReadOnlyList<DikozemieArenaOpponent> pool, int count, int rating) =>
                !naval && composition == DikozemieArenaComposition.Mixed
                    ? DikozemieArenaOpponentCatalog.SelectMixedTournamentField(
                        pool,
                        count,
                        Math.Max(0, rating),
                        discipline,
                        fallback)
                    : DikozemieArenaOpponentCatalog.SelectTournamentField(
                        pool,
                        count,
                        Math.Max(0, rating),
                        mountedOnly: false,
                        fallback: fallback);
            List<DikozemieArenaOpponent> sideFriendly = null;
            List<DikozemieArenaOpponent> sideEnemy = null;
            IReadOnlyList<DikozemieArenaOpponent> teamField;
            if (sideStrength)
            {
                // Each side's men are drawn near its own strength: a lord's
                // garrison of soldiers, a clan's hired militia.
                sideFriendly = SelectField(selectionPool, friendlyBotCount,
                    attackerSideRating > 0 ? attackerSideRating : playerRating).Take(friendlyBotCount).ToList();
                var taken = new HashSet<string>(sideFriendly.Select(o => o?.CharacterId ?? string.Empty),
                    StringComparer.OrdinalIgnoreCase);
                List<DikozemieArenaOpponent> rest = eligibleOpponents
                    .Where(o => o != null && !taken.Contains(o.CharacterId)).ToList();
                sideEnemy = SelectField(rest.Count >= enemyBotCount ? rest : eligibleOpponents, enemyBotCount,
                    defenderSideRating > 0 ? defenderSideRating : playerRating).Take(enemyBotCount).ToList();
                teamField = sideFriendly.Concat(sideEnemy).ToList();
            }
            else
            {
                teamField = SelectField(selectionPool, requestedBotCount, playerRating);
            }
            _previousTeamFieldIds.Clear();
            foreach (DikozemieArenaOpponent selected in teamField)
            {
                if (!string.IsNullOrWhiteSpace(selected?.CharacterId))
                {
                    _previousTeamFieldIds.Add(selected.CharacterId);
                }
            }

            List<int> teamRatings = teamField.Select(
                DikozemieArenaOpponentCatalog.GetCurrentRating).ToList();
            bool[] friendlyAssignments = sideStrength
                ? teamField.Select((_, index) => index < sideFriendly.Count).ToArray()
                : DikozemieArenaTeamRules.BuildBalancedTeamAssignment(
                teamRatings,
                !naval && composition == DikozemieArenaComposition.Mixed
                    ? teamField.Select(opponent =>
                        DikozemieArenaOpponentCatalog
                            .CharacterHasBowOrCrossbow(
                                opponent,
                                fallback)).ToList()
                    : null,
                friendlyBotCount,
                enemyBotCount,
                attackerHumanRating,
                defenderHumanRating);
            List<DikozemieArenaOpponent> friendlyField = teamField
                .Where((_, index) => friendlyAssignments[index])
                .ToList();
            List<DikozemieArenaOpponent> enemyField = teamField
                .Where((_, index) => !friendlyAssignments[index])
                .ToList();
            // Each side's result screen names one bot of the other side.
            DikozemieArenaOpponent defenderRepresentative = enemyField.FirstOrDefault();
            DikozemieArenaOpponent attackerRepresentative = friendlyField.FirstOrDefault();
            field.DefenderRepresentativeId = defenderRepresentative?.CharacterId ??
                (enemyBotCount > 0 ? fallback?.StringId ?? string.Empty : string.Empty);
            field.DefenderRepresentativeRating = defenderRepresentative == null
                ? 0
                : DikozemieArenaOpponentCatalog.GetCurrentRating(defenderRepresentative);
            field.AttackerRepresentativeId = attackerRepresentative?.CharacterId ??
                (friendlyBotCount > 0 ? fallback?.StringId ?? string.Empty : string.Empty);
            field.AttackerRepresentativeRating = attackerRepresentative == null
                ? 0
                : DikozemieArenaOpponentCatalog.GetCurrentRating(attackerRepresentative);
            if (!string.IsNullOrEmpty(field.DefenderRepresentativeId))
            {
                _previousTrainingOpponentId = field.DefenderRepresentativeId;
            }

            long friendlyRatingTotal = Math.Max(0L, attackerHumanRating);
            for (int index = 0; index < friendlyField.Count; index++)
            {
                friendlyRatingTotal +=
                    DikozemieArenaOpponentCatalog.GetCurrentRating(
                        friendlyField[index]);
            }
            field.AttackerAverageRating = (int)(friendlyRatingTotal / Math.Max(1, attackerSize));
            long enemyRatingTotal = Math.Max(0L, defenderHumanRating);
            for (int index = 0; index < enemyField.Count; index++)
            {
                enemyRatingTotal +=
                    DikozemieArenaOpponentCatalog.GetCurrentRating(
                        enemyField[index]);
            }
            field.DefenderAverageRating = (int)(enemyRatingTotal / Math.Max(1, defenderSize));

            var attackers = new List<Agent>(attackerHumans);
            for (int index = 0; index < friendlyBotCount; index++)
            {
                Agent ally = SpawnTeamBot(
                    attackerCulture,
                    Mission.AttackerTeam,
                    discipline,
                    composition,
                    index < friendlyField.Count ? friendlyField[index] : null,
                    index,
                    healthMultiplier);
                if (ally != null)
                {
                    attackers.Add(ally);
                    spawned.Add(ally);
                }
            }

            var defenders = new List<Agent>(defenderHumans);
            for (int index = 0; index < enemyBotCount; index++)
            {
                Agent enemy = SpawnTeamBot(
                    defenderCulture,
                    Mission.DefenderTeam,
                    discipline,
                    composition,
                    index < enemyField.Count
                        ? enemyField[index]
                        : null,
                    index,
                    healthMultiplier);
                if (enemy != null)
                {
                    defenders.Add(enemy);
                    spawned.Add(enemy);
                }
            }

            PositionTeamCombatants(
                attackers,
                defenders,
                PlayerStartsAtAttackerWall);
            for (int index = 0; index < attackers.Count; index++)
            {
                Agent bot = attackers[index];
                if (bot?.MissionPeer == null && defenders.Count > 0)
                {
                    QueueBotActivation(
                        bot,
                        defenders[index % defenders.Count],
                        useAutomaticTargetSelection: true);
                }
            }
            for (int index = 0; index < defenders.Count; index++)
            {
                Agent bot = defenders[index];
                if (bot?.MissionPeer == null && attackers.Count > 0)
                {
                    QueueBotActivation(
                        bot,
                        attackers[index % attackers.Count],
                        useAutomaticTargetSelection: true);
                }
            }
            MissionMultiplayerDikozemieArena.Trace(
                "team bots spawned format=" + attackerSize + "x" + defenderSize +
                " discipline=" + discipline +
                " composition=" + composition +
                " humans=" + attackerHumans.Count + "+" + defenderHumans.Count +
                " rankedField=" + teamField.Count +
                " freshField=" + freshOpponents.Count +
                " playerWall=" +
                    (PlayerStartsAtAttackerWall ? "attacker" : "defender") +
                " attackerAverage=" + field.AttackerAverageRating +
                " defenderAverage=" + field.DefenderAverageRating +
                " attackers=" + attackers.Count +
                " defenders=" + defenders.Count +
                " attackerRanged=" + friendlyField.Count(opponent =>
                    DikozemieArenaOpponentCatalog.CharacterHasBowOrCrossbow(
                        opponent,
                        fallback)) +
                " defenderRanged=" + enemyField.Count(opponent =>
                    DikozemieArenaOpponentCatalog.CharacterHasBowOrCrossbow(
                        opponent,
                        fallback)) +
                " attackerMounted=" + attackers.Count(agent =>
                    agent?.MountAgent != null) +
                " defenderMounted=" + defenders.Count(agent =>
                    agent?.MountAgent != null));
            return spawned;
        }

        private Agent SpawnTeamBot(
            BasicCultureObject culture,
            Team team,
            DikozemieArenaDiscipline discipline,
            DikozemieArenaComposition composition,
            DikozemieArenaOpponent opponent,
            int index,
            float healthMultiplier)
        {
            BasicCharacterObject fallback = ResolveTeamBotCharacter(
                culture,
                discipline,
                index);
            BasicCharacterObject character =
                DikozemieArenaOpponentCatalog.ResolveCharacter(
                    opponent,
                    fallback);
            Equipment source = character?.FirstBattleEquipment ??
                character?.Equipment;
            Equipment equipment = source == null
                ? new Equipment(Equipment.EquipmentType.Battle)
                : new Equipment(source);
            // A boarding is a fight hand to hand: the crews of the lowest ratings
            // are almost all archers, and they would only shoot across the rails.
            bool boardingParty = index % 5 != 4 &&
                DikozemieArenaSceneRules.IsNavalBoardingScene(
                    MultiplayerOptions.OptionType.Map.GetStrValue());
            DikozemieArenaLoadoutRules.ApplyCompositionToEquipment(
                equipment,
                boardingParty ? DikozemieArenaComposition.Melee : composition);
            ApplyTeamBotDisciplineToEquipment(
                equipment,
                discipline,
                culture,
                index);
            Agent bot = SpawnOwnedBot(character, equipment, team, culture);
            if (bot == null)
            {
                return null;
            }
            bot.Controller = AgentControllerType.None;
            bot.HealthLimit = Math.Max(1f, bot.HealthLimit * healthMultiplier);
            bot.Health = bot.HealthLimit;
            bot.WieldInitialWeapons();
            if (opponent != null)
            {
                _teamBotOpponents[bot] = opponent;
            }
            if (bot.MountAgent != null)
            {
                _mountedTeamBots.Add(bot);
            }
            return bot;
        }

        internal bool TryGetTeamBotOpponent(
            Agent bot,
            out DikozemieArenaOpponent opponent)
        {
            opponent = null;
            return bot != null &&
                _teamBotOpponents.TryGetValue(bot, out opponent);
        }

        internal bool TryGetTeamBotOpponent(
            Agent bot,
            out DikozemieArenaOpponent opponent,
            out bool wasMounted)
        {
            wasMounted = bot != null && _mountedTeamBots.Contains(bot);
            return TryGetTeamBotOpponent(bot, out opponent);
        }

        internal static void ApplyDisciplineToEquipment(
            Equipment equipment,
            DikozemieArenaDiscipline discipline,
            BasicCultureObject culture)
        {
            if (equipment == null || discipline == DikozemieArenaDiscipline.Open)
            {
                return;
            }
            if (discipline == DikozemieArenaDiscipline.Foot)
            {
                equipment[EquipmentIndex.Horse] = default(EquipmentElement);
                equipment[EquipmentIndex.HorseHarness] = default(EquipmentElement);
                return;
            }

            if (equipment[EquipmentIndex.Horse].Item != null)
            {
                return;
            }
            BasicCharacterObject mounted = ResolveTeamBotCharacter(
                culture,
                DikozemieArenaDiscipline.Mounted,
                0);
            Equipment mountedEquipment = mounted?.FirstBattleEquipment ??
                mounted?.Equipment;
            if (mountedEquipment == null)
            {
                return;
            }
            equipment[EquipmentIndex.Horse] =
                mountedEquipment[EquipmentIndex.Horse];
            equipment[EquipmentIndex.HorseHarness] =
                mountedEquipment[EquipmentIndex.HorseHarness];
        }

        private static void ApplyTeamBotDisciplineToEquipment(
            Equipment equipment,
            DikozemieArenaDiscipline discipline,
            BasicCultureObject culture,
            int teamIndex)
        {
            if (equipment == null)
            {
                return;
            }
            if (discipline != DikozemieArenaDiscipline.Open)
            {
                ApplyDisciplineToEquipment(equipment, discipline, culture);
                return;
            }

            // Open means a real combined-arms team. The player's lack of a
            // personal horse restricts only the player, not both armies.
            if (teamIndex % 2 == 0)
            {
                equipment[EquipmentIndex.Horse] = default(EquipmentElement);
                equipment[EquipmentIndex.HorseHarness] =
                    default(EquipmentElement);
                return;
            }

            BasicCharacterObject mounted = ResolveTeamBotCharacter(
                culture,
                DikozemieArenaDiscipline.Mounted,
                teamIndex);
            Equipment mountedEquipment = mounted?.FirstBattleEquipment ??
                mounted?.Equipment;
            if (mountedEquipment != null)
            {
                equipment[EquipmentIndex.Horse] =
                    mountedEquipment[EquipmentIndex.Horse];
                equipment[EquipmentIndex.HorseHarness] =
                    mountedEquipment[EquipmentIndex.HorseHarness];
            }
        }

        internal static void ApplyPlayerDisciplineToEquipment(
            Equipment equipment,
            DikozemieArenaDiscipline discipline)
        {
            if (equipment == null ||
                discipline != DikozemieArenaDiscipline.Foot)
            {
                // Mounted and Open preserve the hero's real inventory. They
                // must never manufacture a default horse for the player.
                return;
            }
            equipment[EquipmentIndex.Horse] = default(EquipmentElement);
            equipment[EquipmentIndex.HorseHarness] =
                default(EquipmentElement);
        }

        private static BasicCharacterObject ResolveTeamBotCharacter(
            BasicCultureObject culture,
            DikozemieArenaDiscipline discipline,
            int index)
        {
            var classes = MultiplayerClassDivisions.GetMPHeroClasses(culture)
                ?.Where(heroClass => heroClass?.TroopCharacter != null)
                .ToList();
            if (classes != null && classes.Count > 0)
            {
                bool wantsMount = discipline == DikozemieArenaDiscipline.Mounted ||
                    (discipline == DikozemieArenaDiscipline.Open && index % 2 == 1);
                BasicCharacterObject matching = classes
                    .Select(heroClass => heroClass.TroopCharacter)
                    .FirstOrDefault(character =>
                    {
                        Equipment equipment = character?.FirstBattleEquipment ??
                            character?.Equipment;
                        return equipment != null &&
                            (equipment[EquipmentIndex.Horse].Item != null) == wantsMount;
                    });
                if (matching != null)
                {
                    return matching;
                }
                return classes[index % classes.Count].TroopCharacter;
            }
            return MBObjectManager.Instance.GetObject<BasicCharacterObject>(
                discipline == DikozemieArenaDiscipline.Mounted
                    ? "mp_heavy_cavalry_empire_troop"
                    : "mp_heavy_infantry_empire_troop");
        }

        private static DikozemieArenaDiscipline NormalizeDiscipline(int value)
        {
            return value == (int)DikozemieArenaDiscipline.Foot ||
                value == (int)DikozemieArenaDiscipline.Mounted
                    ? (DikozemieArenaDiscipline)value
                    : DikozemieArenaDiscipline.Open;
        }

        private void QueueBotActivation(
            Agent bot,
            Agent target,
            bool useAutomaticTargetSelection = false)
        {
            if (bot == null || target == null)
            {
                return;
            }
            _pendingBotActivations.Add(new PendingBotActivation
            {
                Bot = bot,
                Target = target,
                UseAutomaticTargetSelection = useAutomaticTargetSelection,
                ActivateAt = Mission.CurrentTime + BotTargetAssignmentDelay
            });
        }

        private static void PositionTeamCombatants(
            IReadOnlyList<Agent> attackers,
            IReadOnlyList<Agent> defenders,
            bool playerStartsAtAttackerWall)
        {
            string scene = MultiplayerOptions.OptionType.Map.GetStrValue();
            if (DikozemieArenaSceneRules.IsNavalBoardingScene(scene))
            {
                PositionNavalCrew(attackers, attacker: true);
                PositionNavalCrew(defenders, attacker: false);
                MissionMultiplayerDikozemieArena.Trace(
                    "naval crews placed on open-sea decks attackers=" +
                    attackers.Count + " defenders=" + defenders.Count);
                return;
            }
            DikozemieArenaSpawnLayout layout =
                DikozemieArenaSpawnLayouts.Resolve(
                    scene);
            Vec3 forward = layout.DefenderGate - layout.AttackerGate;
            forward.z = 0f;
            if (forward.Normalize() <= 0.001f)
            {
                return;
            }
            bool hasMount = attackers.Concat(defenders)
                .Any(agent => agent?.MountAgent != null);
            float spacing = hasMount
                ? layout.MountedSpacing
                : layout.FootSpacing;
            IReadOnlyList<Vec3> attackerPositions =
                DikozemieArenaSpawnLayouts.BuildFormation(
                    layout,
                    attacker: playerStartsAtAttackerWall,
                    attackers.Count,
                    spacing);
            IReadOnlyList<Vec3> defenderPositions =
                DikozemieArenaSpawnLayouts.BuildFormation(
                    layout,
                    attacker: !playerStartsAtAttackerWall,
                    defenders.Count,
                    spacing);
            if (!playerStartsAtAttackerWall)
            {
                forward = -forward;
            }
            for (int index = 0; index < attackers.Count; index++)
            {
                SetInitialCombatantFrame(
                    attackers[index],
                    attackerPositions[index],
                    forward);
            }
            for (int index = 0; index < defenders.Count; index++)
            {
                SetInitialCombatantFrame(
                    defenders[index],
                    defenderPositions[index],
                    -forward);
            }
            float clearance = hasMount
                ? layout.MountedBoundaryClearance
                : layout.FootBoundaryClearance;
            bool allInside = attackerPositions.Concat(defenderPositions)
                .All(position =>
                    DikozemieArenaSpawnLayouts.IsInsideSafeBoundary(
                        layout,
                        position,
                        clearance));
            MissionMultiplayerDikozemieArena.Trace(
                "team formations placed wallSafe=" + allInside +
                " attackerRows=" +
                DikozemieArenaSpawnLayouts.CountFormationRows(
                    layout,
                    attacker: playerStartsAtAttackerWall,
                    attackerPositions,
                    spacing) +
                " defenderRows=" +
                DikozemieArenaSpawnLayouts.CountFormationRows(
                    layout,
                    attacker: !playerStartsAtAttackerWall,
                    defenderPositions,
                    spacing) +
                " spacing=" + spacing.ToString("0.00"));
        }

        // A live player on each ship starts the battle docked, while the enemy
        // crew waited on a ship still out at sea: bring it along.
        internal void PlaceWaitingEnemyCrew(IReadOnlyList<Agent> crew)
        {
            PositionNavalCrew(
                crew.Where(agent => agent.Team == Mission.DefenderTeam).ToList(),
                attacker: false);
        }

        private static void PositionNavalCrew(
            IReadOnlyList<Agent> crew,
            bool attacker)
        {
            DikozemieNavalBoardingBehavior boarding =
                DikozemieNavalBoardingBehavior.Active;
            Vec3 lookDirection = attacker
                ? new Vec3(1f, 0f, 0f)
                : new Vec3(-1f, 0f, 0f);
            Vec3[] positions = Enumerable.Range(0, crew.Count)
                .Select(index => GetNavalCrewPosition(
                    attacker,
                    index,
                    crew.Count))
                .ToArray();
            boarding?.BeginCrewDeckPlacement(
                attacker,
                crew,
                positions);
            for (int index = 0; index < crew.Count; index++)
            {
                Vec3 position = positions[index];
                if (boarding != null)
                {
                    boarding.PlaceCrewOnDeck(
                        crew[index],
                        attacker,
                        position);
                    crew[index].LookDirection = lookDirection;
                    crew[index].IsLookDirectionLocked = true;
                }
                else
                {
                    SetInitialCombatantFrame(
                        crew[index],
                        position,
                        lookDirection);
                }
                if (crew.Count <= 3)
                {
                    MissionMultiplayerDikozemieArena.Trace(
                        "naval crew spawn side=" +
                        (attacker ? "attacker" : "defender") +
                        " index=" + index +
                        " position=" + DescribePosition(position));
                }
            }
            boarding?.AuditCrewSupport(
                attacker ? "attacker-crew-placed" : "defender-crew-placed");
        }

        private static Vec3 GetNavalCrewPosition(
            bool attacker,
            int index,
            int count)
        {
            const int FightersPerRow = 12;
            DikozemieNavalHullMetrics hull =
                DikozemieNavalBoardingBehavior.Active?.CurrentHull ??
                DikozemieNavalHullMetrics.Resolve(null);
            float AlongDeckSpacing = Math.Min(
                3.25f,
                2f * hull.WalkableHalfLength / (FightersPerRow - 1));
            int safeCount = Math.Max(1, count);
            int safeIndex = Math.Max(0, Math.Min(index, safeCount - 1));
            int rowCount = Math.Max(
                1,
                (safeCount + FightersPerRow - 1) / FightersPerRow);
            int row = safeIndex / FightersPerRow;
            int column = safeIndex % FightersPerRow;
            int rowStart = row * FightersPerRow;
            int fightersInRow = Math.Min(
                FightersPerRow,
                safeCount - rowStart);
            float AcrossDeckSpacing = Math.Min(
                1.45f,
                2f * hull.WalkableHalfWidth / Math.Max(1, rowCount - 1));
            float acrossOffset =
                (row - ((rowCount - 1f) * 0.5f)) *
                AcrossDeckSpacing;
            if (safeCount <= 3)
            {
                acrossOffset += attacker ? 1.75f : -1.75f;
            }
            float alongOffset =
                (column - ((fightersInRow - 1f) * 0.5f)) *
                (safeCount <= 3 ? 4f : AlongDeckSpacing);
            if (safeCount <= 3)
            {
                alongOffset += attacker ? 6f : -6f;
            }
            DikozemieNavalBoardingBehavior boarding =
                DikozemieNavalBoardingBehavior.Active;
            if (boarding != null)
            {
                return boarding.GetDeckPosition(
                    attacker,
                    acrossOffset,
                    alongOffset);
            }
            DikozemieNavalBoardingLayout layout =
                DikozemieNavalBoardingLayout.Resolve(
                    (int)DikozemieNavalShipClass.Light);
            Vec3 deckCenter = layout.GetStartDeckCenter(attacker);
            return new Vec3(
                deckCenter.x + acrossOffset,
                deckCenter.y + alongOffset,
                deckCenter.z);
        }

        internal void BeginNavalBoarding(
            DikozemieNavalBoardingLayout layout)
        {
            if (_navalBoardingCompletionLogged)
            {
                return;
            }
            _navalBoardingCompletionLogged = true;
            ActivateRemainingNavalCrew();
            MissionMultiplayerDikozemieArena.Trace(
                "naval crews released to fight");
            // Boarding: most of both crews charge across with a yell.
            DikozemieBattleCryBehavior.QueueBotYells(Mission.Agents, 0.6f, 2.5f);
            DikozemieNavalBoardingBehavior.Active?
                .AuditCrewSupport("crews-released");
        }

        internal void HoldNavalCrewsForBoarding()
        {
            _navalBoardingCompletionLogged = false;
            _pendingBotActivations.RemoveAll(pending =>
                pending.Bot?.MissionPeer == null);
            Agent[] bots = Mission.AttackerTeam.ActiveAgents
                .Concat(Mission.DefenderTeam.ActiveAgents)
                .Where(agent => agent != null &&
                    agent.State == AgentState.Active && agent.IsHuman &&
                    agent.MissionPeer == null)
                .ToArray();
            foreach (Agent bot in bots)
            {
                bot.Controller = AgentControllerType.None;
                bot.SetAutomaticTargetSelection(false);
                bot.SetTargetAgent(null);
                bot.WieldInitialWeapons(Agent.WeaponWieldActionType.Instant);
            }
            MissionMultiplayerDikozemieArena.Trace(
                "naval crews holding for grappling count=" + bots.Length);
        }

        private void QueueNavalBoarders(
            IReadOnlyList<Agent> boarders,
            IReadOnlyList<Agent> targets,
            bool fromAttacker)
        {
            float[] lanes = { -7.5f, -2.5f, 2.5f, 7.5f };
            for (int index = 0; index < boarders.Count; index++)
            {
                int lane = index % lanes.Length;
                int wave = index / lanes.Length;
                _pendingNavalBoarders.Add(new PendingNavalBoarder
                {
                    Boarder = boarders[index],
                    Target = targets.Count > 0
                        ? targets[index % targets.Count]
                        : null,
                    FromAttacker = fromAttacker,
                    AlongOffset = lanes[lane],
                    StartsAt = (wave * 0.42f) + (lane * 0.08f),
                    Duration = 1.8f + (wave * 0.04f)
                });
            }
        }

        internal void TickNavalBoarding(float dt)
        {
            if (_pendingNavalBoarders.Count == 0)
            {
                return;
            }
            _navalBoardingElapsed += Math.Max(0f, dt);
            DikozemieNavalBoardingBehavior boarding =
                DikozemieNavalBoardingBehavior.Active;
            if (boarding == null)
            {
                return;
            }

            int completed = 0;
            foreach (PendingNavalBoarder pending in _pendingNavalBoarders)
            {
                Agent boarder = pending.Boarder;
                if (pending.Completed || boarder == null ||
                    boarder.State != AgentState.Active)
                {
                    completed++;
                    continue;
                }
                float progress = Math.Max(
                    0f,
                    Math.Min(
                        1f,
                        (_navalBoardingElapsed - pending.StartsAt) /
                            Math.Max(0.1f, pending.Duration)));
                if (progress <= 0f)
                {
                    continue;
                }

                Vec3 start = boarding.GetBoardingEdgePosition(
                    pending.FromAttacker,
                    pending.AlongOffset);
                Vec3 destination = boarding.GetBoardingEdgePosition(
                    !pending.FromAttacker,
                    pending.AlongOffset);
                Vec3 direction = destination - start;
                direction.z = 0f;
                if (direction.Normalize() <= 0.001f)
                {
                    direction = pending.FromAttacker
                        ? new Vec3(1f, 0f, 0f)
                        : new Vec3(-1f, 0f, 0f);
                }
                Vec3 position = new Vec3(
                    start.x + ((destination.x - start.x) * progress),
                    start.y + ((destination.y - start.y) * progress),
                    start.z + ((destination.z - start.z) * progress) + 0.12f);
                boarder.Controller = AgentControllerType.None;
                boarder.LookDirection = direction;
                boarder.IsLookDirectionLocked = true;
                boarder.TeleportToPosition(position);

                if (progress < 1f)
                {
                    continue;
                }
                pending.Completed = true;
                boarder.TeleportToPosition(destination);
                boarder.IsLookDirectionLocked = false;
                if (pending.Target != null)
                {
                    QueueBotActivation(
                        boarder,
                        pending.Target,
                        useAutomaticTargetSelection: true);
                }
                completed++;
            }

            if (completed == _pendingNavalBoarders.Count &&
                !_navalBoardingCompletionLogged)
            {
                _navalBoardingCompletionLogged = true;
                ActivateRemainingNavalCrew();
                MissionMultiplayerDikozemieArena.Trace(
                    "naval boarding waves complete count=" + completed);
                boarding.AuditCrewSupport("boarding-waves-complete");
            }
        }

        private void ActivateRemainingNavalCrew()
        {
            var scriptedBoarders = new HashSet<Agent>(
                _pendingNavalBoarders.Select(pending => pending.Boarder));
            Agent[] attackers = Mission.AttackerTeam.ActiveAgents
                .Where(agent => agent != null &&
                    agent.State == AgentState.Active && agent.IsHuman)
                .ToArray();
            Agent[] defenders = Mission.DefenderTeam.ActiveAgents
                .Where(agent => agent != null &&
                    agent.State == AgentState.Active && agent.IsHuman)
                .ToArray();
            Agent[] attackerReserve = attackers
                .Where(agent => agent.MissionPeer == null &&
                    !scriptedBoarders.Contains(agent))
                .ToArray();
            Agent[] defenderReserve = defenders
                .Where(agent => agent.MissionPeer == null &&
                    !scriptedBoarders.Contains(agent))
                .ToArray();
            for (int index = 0; index < attackerReserve.Length; index++)
            {
                if (defenders.Length > 0)
                {
                    QueueBotActivation(
                        attackerReserve[index],
                        defenders[index % defenders.Length],
                        useAutomaticTargetSelection: true);
                }
            }
            for (int index = 0; index < defenderReserve.Length; index++)
            {
                if (attackers.Length > 0)
                {
                    QueueBotActivation(
                        defenderReserve[index],
                        attackers[index % attackers.Length],
                        useAutomaticTargetSelection: true);
                }
            }
            MissionMultiplayerDikozemieArena.Trace(
                "naval reserves released attacker=" +
                attackerReserve.Length + " defender=" +
                defenderReserve.Length);
        }

        private Agent SpawnOwnedBot(
            BasicCharacterObject character,
            Equipment equipment,
            Team team,
            BasicCultureObject culture)
        {
            if (character == null || equipment == null || team == null)
            {
                return null;
            }
            if (!(SpawnComponent is DikozemieArenaSpawnComponent))
            {
                throw new InvalidOperationException(
                    "Arena-owned bots require DikozemieArenaSpawnComponent.");
            }
            MatrixFrame spawnFrame = SpawnComponent.GetSpawnFrame(
                team, equipment[EquipmentIndex.Horse].Item != null, true);
            if (DikozemieArenaSceneRules.IsNavalBoardingScene(
                    MultiplayerOptions.OptionType.Map.GetStrValue()))
            {
                bool attacker = team == Mission.AttackerTeam;
                int navalIndex = team.ActiveAgents.Count(agent =>
                    agent != null && agent.State == AgentState.Active &&
                    agent.IsHuman);
                spawnFrame.origin = GetNavalCrewPosition(
                    attacker,
                    navalIndex,
                    _navalSpawnTeamSize);
            }
            int seed = MissionLobbyComponent.GetRandomFaceSeedForCharacter(character);
            var buildData = new AgentBuildData(character)
                .Team(team)
                .Equipment(equipment)
                .EquipmentSeed(seed)
                .InitialPosition(in spawnFrame.origin)
                .InitialDirection(spawnFrame.rotation.f.AsVec2.Normalized())
                .TroopOrigin(new BasicBattleAgentOrigin(character))
                .Controller(AgentControllerType.None)
                .ClothingColor1(team.Side == BattleSideEnum.Attacker
                    ? culture.Color : culture.ClothAlternativeColor)
                .ClothingColor2(team.Side == BattleSideEnum.Attacker
                    ? culture.Color2 : culture.ClothAlternativeColor2)
                .IsFemale(character.IsFemale);
            if (DikozemieArenaSceneRules.IsNavalBoardingScene(
                    MultiplayerOptions.OptionType.Map.GetStrValue()))
            {
                buildData.CanSpawnOutsideOfMissionBoundary(true);
            }
            buildData.BodyProperties(BodyProperties.GetRandomBodyProperties(
                buildData.AgentRace, buildData.AgentIsFemale,
                character.GetBodyPropertiesMin(), character.GetBodyPropertiesMax(),
                (int)equipment.HairCoverType, seed,
                character.BodyPropertyRange.HairTags,
                character.BodyPropertyRange.BeardTags,
                character.BodyPropertyRange.TattooTags));
            return Mission.SpawnAgent(buildData);
        }

        private void PositionDuelistsAtAuthoredGateStarts(
            Agent player,
            Agent bot)
        {
            if (player == null || bot == null)
            {
                return;
            }

            Vec3 playerStart = player.Position;
            Vec3 botStart = bot.Position;
            Vec3 direction = botStart - playerStart;
            direction.z = 0f;
            float originalDistance = direction.Normalize();
            if (originalDistance <= 0.001f)
            {
                return;
            }

            DikozemieArenaSpawnLayout layout =
                DikozemieArenaSpawnLayouts.Resolve(
                    MultiplayerOptions.OptionType.Map.GetStrValue());
            Vec3 playerPosition = DikozemieArenaSpawnLayouts.BuildFormation(
                layout,
                attacker: true,
                count: 1,
                spacing: layout.FootSpacing)[0];
            Vec3 botPosition = DikozemieArenaSpawnLayouts.BuildFormation(
                layout,
                attacker: false,
                count: 1,
                spacing: layout.FootSpacing)[0];
            Vec3 configuredDirection = botPosition - playerPosition;
            configuredDirection.z = 0f;
            configuredDirection.Normalize();
            SetInitialCombatantFrame(
                player,
                playerPosition,
                configuredDirection);
            SetInitialCombatantFrame(
                bot,
                botPosition,
                -configuredDirection);
            MissionMultiplayerDikozemieArena.Trace(
                "duelists placed at configured gate starts=True" +
                " pair=physical_boundary_walls source=" +
                MultiplayerOptions.OptionType.Map.GetStrValue() + " distance=" +
                playerPosition.Distance(botPosition).ToString("0.0") +
                " original=" + originalDistance.ToString("0.0") +
                " player=" + DescribePosition(playerPosition) +
                " bot=" + DescribePosition(botPosition));
        }

        private static string DescribePosition(Vec3 position)
        {
            return position.x.ToString("0.0") + "," +
                position.y.ToString("0.0") + "," +
                position.z.ToString("0.0");
        }

        private static void SetInitialCombatantFrame(
            Agent agent,
            Vec3 position,
            Vec3 direction)
        {
            // This helper runs after Mission.SpawnAgent. Agent.SetInitialFrame
            // is a pre-build API; calling it here makes the native runtime
            // re-project a sailor onto the open-sea scene floor below the
            // water. Teleport is the supported post-build placement path.
            agent.TeleportToPosition(position);
            agent.LookDirection = direction;
            agent.IsLookDirectionLocked = true;
            if (agent.MountAgent != null)
            {
                agent.MountAgent.LookDirection = direction;
            }
        }

        // A bot keeps walking to the enemy it was given. Native bots get a new
        // one from a formation's charge order; these have no formation, so with
        // that enemy dead or out of reach they stood beside the others until
        // the player finished the battle for them (2026-10-31).
        private void KeepBotsEngaged(float dt)
        {
            _engagementElapsed += Math.Max(0f, dt);
            if (_engagementElapsed < BotEngagementInterval ||
                Mission?.AttackerTeam == null || Mission.DefenderTeam == null)
            {
                return;
            }
            _engagementElapsed = 0f;
            Agent[] attackers = ActiveFighters(Mission.AttackerTeam);
            Agent[] defenders = ActiveFighters(Mission.DefenderTeam);
            bool naval = DikozemieArenaSceneRules.IsNavalBoardingScene(
                MultiplayerOptions.OptionType.Map.GetStrValue());
            bool teamFight = attackers.Length + defenders.Length > 2 && !naval;
            int retargeted = _closeInSince >= 0f
                ? CloseIn(attackers, defenders, naval) + CloseIn(defenders, attackers, naval)
                : teamFight
                    ? ThinkLikePlayers(attackers, defenders) + ThinkLikePlayers(defenders, attackers)
                    : EngageNearest(attackers, defenders) + EngageNearest(defenders, attackers);
            _engagementTraceElapsed += BotEngagementInterval;
            if (_engagementTraceElapsed >= 5f &&
                attackers.Length > 0 && defenders.Length > 0 &&
                attackers.Length + defenders.Length > 2)
            {
                _engagementTraceElapsed = 0f;
                MissionMultiplayerDikozemieArena.Trace(
                    "bots engaged attackers=" + attackers.Length +
                    " defenders=" + defenders.Length +
                    " retargeted=" + retargeted);
            }
        }

        // Customer 2026-11-19: two bots shot at each other with bows, «один бот
        // другого убил, и тот, который умер, он как-то телепортировался перед
        // тем ботом». When nobody fell for 30 s the battle put the smaller
        // side's bots right in front of their nearest enemy (server log
        // 2026-11-19 10:16:33, Imperial-3: 80 m apart, killed 0.15 s after the
        // jump). Now both sides close in on foot with their side arms out, as
        // players would, and the native combat AI finishes it. Nobody jumps.
        private const float CloseInContact = 5f;
        private float _closeInSince = -1f;

        internal bool ClosingIn => _closeInSince >= 0f;

        internal void BeginCloseIn()
        {
            if (Mission == null)
            {
                return;
            }
            _closeInSince = Mission.CurrentTime;
            _engagementElapsed = BotEngagementInterval;
        }

        internal void EndCloseIn()
        {
            _closeInSince = -1f;
        }

        private int CloseIn(Agent[] bots, Agent[] enemies, bool naval)
        {
            int retargeted = 0;
            if (enemies.Length == 0)
            {
                return 0;
            }
            foreach (Agent bot in bots)
            {
                if (bot.MissionPeer != null || bot.Controller != AgentControllerType.AI)
                {
                    continue;
                }
                Agent nearest = null;
                float nearestDistance = float.MaxValue;
                foreach (Agent enemy in enemies)
                {
                    float distance = bot.Position.DistanceSquared(enemy.Position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = enemy;
                    }
                }
                if (nearest == null)
                {
                    continue;
                }
                try
                {
                    bot.SetMaximumSpeedLimit(1f, true);
                    bot.MountAgent?.SetMaximumSpeedLimit(1f, true);
                    if (bot.GetTargetAgent() != nearest)
                    {
                        bot.SetTargetAgent(nearest);
                        retargeted++;
                    }
                    DikozemieBotArms.WieldSideArm(bot);
                    if (_botMinds.TryGetValue(bot, out BotMind mind))
                    {
                        mind.Scripted = false;
                        mind.RetreatUntil = 0f;
                    }
                    // On a ship the walk could lead over the side: there the
                    // target and the side arm are enough.
                    if (!naval && nearestDistance > CloseInContact * CloseInContact)
                    {
                        TaleWorlds.Engine.WorldPosition toward = nearest.GetWorldPosition();
                        bot.SetScriptedPosition(ref toward, addHumanLikeDelay: false);
                    }
                    else
                    {
                        bot.DisableScriptedMovement();
                    }
                }
                catch (Exception error)
                {
                    MissionMultiplayerDikozemieArena.Trace("close in failed: " + error.Message);
                }
            }
            return retargeted;
        }

        private static Agent[] ActiveFighters(Team team) =>
            team.ActiveAgents
                .Where(agent => agent != null &&
                    agent.State == AgentState.Active && agent.IsHuman)
                .ToArray();

        private static int EngageNearest(Agent[] bots, Agent[] enemies)
        {
            int retargeted = 0;
            foreach (Agent bot in bots)
            {
                if (bot.MissionPeer != null ||
                    bot.Controller != AgentControllerType.AI)
                {
                    continue;
                }
                Agent nearest = null;
                float nearestDistance = float.MaxValue;
                foreach (Agent enemy in enemies)
                {
                    float distance = bot.Position.DistanceSquared(enemy.Position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = enemy;
                    }
                }
                if (nearest == null)
                {
                    continue;
                }
                Agent current = bot.GetTargetAgent();
                // Twice as far (four times the squared distance): a fight is
                // not abandoned for somebody who just stepped a little closer.
                if (current == null || current.State != AgentState.Active ||
                    current.Team == bot.Team ||
                    bot.Position.DistanceSquared(current.Position) >
                        (nearestDistance * 4f) + 4f)
                {
                    bot.SetTargetAgent(nearest);
                    retargeted++;
                }
            }
            return retargeted;
        }

        // Customer 2026-10-31: bots should act like people on a server, as a team,
        // not all grimly sprinting at each other. Each has a temper: some walk up
        // carefully, some rush. They stay near their own, help a friend who is
        // already fighting, do not all pile on one, and the timid back off to
        // their friends when badly hurt.
        private sealed class BotMind
        {
            internal float Courage;
            internal float ApproachPace;
            internal float FirstSeenAt;
            internal float RetreatUntil;
            internal bool Scripted;
        }

        private const float ApproachSeconds = 7f;
        private const float RegroupDistance = 16f;
        private const float ContactDistance = 14f;
        private readonly Dictionary<Agent, BotMind> _botMinds = new Dictionary<Agent, BotMind>();
        private readonly Random _botDice = new Random();

        private int ThinkLikePlayers(Agent[] team, Agent[] enemies)
        {
            int retargeted = 0;
            if (enemies.Length == 0)
            {
                return 0;
            }
            var pressure = new Dictionary<Agent, int>();
            foreach (Agent mate in team)
            {
                Agent target = mate.GetTargetAgent();
                if (target != null)
                {
                    pressure.TryGetValue(target, out int count);
                    pressure[target] = count + 1;
                }
            }
            float now = Mission.CurrentTime;
            foreach (Agent bot in team)
            {
                if (bot.MissionPeer != null || bot.Controller != AgentControllerType.AI)
                {
                    continue;
                }
                if (!_botMinds.TryGetValue(bot, out BotMind mind))
                {
                    mind = new BotMind
                    {
                        Courage = 0.2f + (float)_botDice.NextDouble() * 0.8f,
                        ApproachPace = 0.55f + (float)_botDice.NextDouble() * 0.35f,
                        FirstSeenAt = now
                    };
                    _botMinds[bot] = mind;
                }

                Agent nearest = null;
                float nearestDistance = float.MaxValue;
                Agent best = null;
                float bestScore = float.MaxValue;
                foreach (Agent enemy in enemies)
                {
                    float distance = bot.Position.Distance(enemy.Position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = enemy;
                    }
                    pressure.TryGetValue(enemy, out int onIt);
                    float score = distance;
                    // Help a friend, but do not make a queue around one enemy.
                    score *= onIt == 1 || onIt == 2 ? 0.75f : onIt >= 3 ? 1.6f : 1f;
                    score *= enemy.Health < enemy.HealthLimit * 0.35f ? 0.8f : 1f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = enemy;
                    }
                }

                Agent friend = null;
                float friendDistance = float.MaxValue;
                foreach (Agent mate in team)
                {
                    if (mate == bot)
                    {
                        continue;
                    }
                    float distance = bot.Position.Distance(mate.Position);
                    if (distance < friendDistance)
                    {
                        friendDistance = distance;
                        friend = mate;
                    }
                }

                // Walk up at his own pace; running starts once the enemy is near.
                bool closing = now - mind.FirstSeenAt < ApproachSeconds &&
                    nearestDistance > ContactDistance;
                bot.SetMaximumSpeedLimit(closing ? mind.ApproachPace : 1f, true);
                if (bot.MountAgent != null)
                {
                    bot.MountAgent.SetMaximumSpeedLimit(closing ? mind.ApproachPace : 1f, true);
                }

                bool hurt = bot.Health < bot.HealthLimit * 0.3f;
                if (mind.Scripted && now >= mind.RetreatUntil)
                {
                    bot.DisableScriptedMovement();
                    mind.Scripted = false;
                }
                if (!mind.Scripted && friend != null)
                {
                    bool retreat = hurt && mind.Courage < 0.55f &&
                        nearestDistance < 7f && friendDistance > 4f;
                    bool regroup = friendDistance > RegroupDistance &&
                        nearestDistance > ContactDistance;
                    if (retreat || regroup)
                    {
                        TaleWorlds.Engine.WorldPosition toward = friend.GetWorldPosition();
                        bot.SetScriptedPosition(ref toward, addHumanLikeDelay: true);
                        mind.Scripted = true;
                        mind.RetreatUntil = now + (retreat ? 2.5f : 2f);
                    }
                }

                Agent current = bot.GetTargetAgent();
                bool keep = current != null && current.State == AgentState.Active &&
                    current.Team != bot.Team &&
                    bot.Position.Distance(current.Position) <= bestScore * 1.6f + 2f;
                if (!keep && best != null)
                {
                    bot.SetTargetAgent(best);
                    retargeted++;
                }
            }
            foreach (Agent gone in _botMinds.Keys.Where(agent => agent.State != AgentState.Active).ToArray())
            {
                _botMinds.Remove(gone);
            }
            return retargeted;
        }

        // Held bots used to get their controller, target and facing set again
        // on every server frame (64 naval crewmen x 6 engine calls per frame
        // while the ships approach). Each change is now applied once, and the
        // facing at most four times a second.
        private const float PendingOrientInterval = 0.25f;
        private readonly List<Agent> _activatedThisTick = new List<Agent>();

        private void ProcessPendingBotActivations()
        {
            if (_pendingBotActivations.Count == 0)
            {
                return;
            }
            bool navalScene = DikozemieArenaSceneRules.IsNavalBoardingScene(
                MultiplayerOptions.OptionType.Map.GetStrValue());
            bool crewsHeld = navalScene &&
                DikozemieNavalBoardingBehavior.Active?.CrewsMayFight != true;
            int activatedTeamBots = 0;
            for (int index = _pendingBotActivations.Count - 1; index >= 0; index--)
            {
                PendingBotActivation pending = _pendingBotActivations[index];
                if (pending.Bot == null || pending.Bot.State != AgentState.Active)
                {
                    _pendingBotActivations.RemoveAt(index);
                    continue;
                }
                bool targetActive = pending.Target != null &&
                    pending.Target.State == AgentState.Active;
                if (!pending.UseAutomaticTargetSelection && !targetActive)
                {
                    _pendingBotActivations.RemoveAt(index);
                    continue;
                }
                if (crewsHeld)
                {
                    // The naval behaviour keeps the crews facing the enemy ship.
                    if (!pending.HeldApplied)
                    {
                        pending.HeldApplied = true;
                        pending.Bot.Controller = AgentControllerType.None;
                        pending.Bot.SetAutomaticTargetSelection(false);
                        pending.Bot.SetTargetAgent(null);
                    }
                    continue;
                }
                if (targetActive && !navalScene &&
                    Mission.CurrentTime >= pending.NextOrientAt)
                {
                    pending.NextOrientAt = Mission.CurrentTime + PendingOrientInterval;
                    OrientTrainingCombatants(
                        pending.Target,
                        pending.Bot,
                        trace: false);
                }
                if (Mission.CurrentTime < pending.ActivateAt)
                {
                    if (pending.Bot.Controller != AgentControllerType.None)
                    {
                        pending.Bot.Controller = AgentControllerType.None;
                    }
                    continue;
                }

                Mission.AttackerTeam.SetIsEnemyOf(Mission.DefenderTeam, true);
                Mission.DefenderTeam.SetIsEnemyOf(Mission.AttackerTeam, true);
                pending.Bot.Controller = AgentControllerType.AI;
                pending.Bot.SetWatchState(Agent.WatchState.Alarmed);
                pending.Bot.SetAlarmState(Agent.AIStateFlag.Alarmed);
                if (pending.UseAutomaticTargetSelection)
                {
                    // Automatic acquisition only looks within the native AI
                    // awareness range.  Teams placed at opposite arena walls
                    // could therefore remain idle forever.  Seed the paired
                    // enemy first, then leave automatic selection enabled so
                    // the bot can retarget normally after that enemy dies.
                    if (targetActive)
                    {
                        pending.Bot.SetTargetAgent(pending.Target);
                    }
                    pending.Bot.SetAutomaticTargetSelection(true);
                }
                else
                {
                    pending.Bot.SetAutomaticTargetSelection(false);
                    pending.Bot.SetTargetAgent(pending.Target);
                }
                if (targetActive)
                {
                    pending.Target.IsLookDirectionLocked = false;
                }
                pending.Bot.IsLookDirectionLocked = false;
                _pendingBotActivations.RemoveAt(index);
                if (pending.UseAutomaticTargetSelection)
                {
                    activatedTeamBots++;
                    _activatedThisTick.Add(pending.Bot);
                }
                else
                {
                    MissionMultiplayerDikozemieArena.Trace(
                        "training bot activated with player target");
                }
            }
            if (activatedTeamBots > 0)
            {
                MissionMultiplayerDikozemieArena.Trace(
                    "team bots activated with seeded targets and automatic retargeting count=" +
                    activatedTeamBots);
                // Land battles: a third of the bots shout as they go in.
                if (!navalScene)
                {
                    DikozemieBattleCryBehavior.QueueBotYells(
                        _activatedThisTick, 0.35f, 1.5f);
                }
            }
            _activatedThisTick.Clear();
        }

        private static void OrientTrainingCombatants(
            Agent player,
            Agent bot,
            bool trace = true)
        {
            if (player == null || bot == null)
            {
                return;
            }

            Vec3 direction = bot.Position - player.Position;
            direction.z = 0f;
            if (direction.Normalize() <= 0.001f)
            {
                return;
            }
            player.LookDirection = direction;
            bot.LookDirection = -direction;
            player.IsLookDirectionLocked = true;
            bot.IsLookDirectionLocked = true;
            if (trace)
            {
                MissionMultiplayerDikozemieArena.Trace(
                    "training combatants oriented distance=" +
                    player.Position.Distance(bot.Position).ToString("0.0"));
            }
        }

        private sealed class PendingBotActivation
        {
            internal Agent Bot;
            internal Agent Target;
            internal bool UseAutomaticTargetSelection;
            internal float ActivateAt;
            internal bool HeldApplied;
            internal float NextOrientAt;
        }

        private sealed class PendingNavalBoarder
        {
            internal Agent Boarder;
            internal Agent Target;
            internal bool FromAttacker;
            internal float AlongOffset;
            internal float StartsAt;
            internal float Duration;
            internal bool Completed;
        }

        internal Agent SpawnArenaMaster(Agent visitor)
        {
            BasicCultureObject culture = MBObjectManager.Instance.GetObject<BasicCultureObject>(
                MultiplayerOptions.OptionType.CultureTeam1.GetStrValue());
            if (culture == null || Mission?.AttackerTeam == null || visitor == null)
            {
                return null;
            }

            BasicCharacterObject character = MultiplayerClassDivisions
                .GetMPHeroClasses(culture).FirstOrDefault()?.TroopCharacter;
            Equipment equipment = character?.FirstBattleEquipment ?? character?.Equipment;
            equipment = equipment == null ? null : new Equipment(equipment);
            if (equipment != null)
            {
                equipment[EquipmentIndex.Horse] = default(EquipmentElement);
                equipment[EquipmentIndex.HorseHarness] = default(EquipmentElement);
            }
            Agent arenaMaster = SpawnOwnedBot(character, equipment,
                Mission.AttackerTeam, culture);
            if (arenaMaster == null)
            {
                return null;
            }

            TaleWorlds.Library.Vec3 position = visitor.Position;
            position.x += 1.8f;
            arenaMaster.TeleportToPosition(position);
            arenaMaster.Controller = AgentControllerType.None;
            return arenaMaster;
        }

        public override int GetMaximumReSpawnPeriodForPeer(MissionPeer peer)
        {
            if (GameMode.WarmupComponent != null && GameMode.WarmupComponent.IsInWarmup)
            {
                return MultiplayerWarmupComponent.RespawnPeriodInWarmup;
            }
            if (peer?.Team?.Side == BattleSideEnum.Attacker)
            {
                return MultiplayerOptions.OptionType.RespawnPeriodTeam1.GetIntValue();
            }
            if (peer?.Team?.Side == BattleSideEnum.Defender)
            {
                return MultiplayerOptions.OptionType.RespawnPeriodTeam2.GetIntValue();
            }
            return -1;
        }

        protected override bool IsRoundInProgress()
        {
            return Mission.Current.CurrentState == Mission.State.Continuing;
        }

        private static Equipment CreateCampaignEquipment(
            DikozemieArenaCharacterProfile profile)
        {
            var equipment = new Equipment(Equipment.EquipmentType.Battle);
            if (profile?.Equipment == null)
            {
                return equipment;
            }

            foreach (DikozemieArenaEquipmentValue saved in profile.Equipment)
            {
                if (saved == null || saved.Slot < 0 || saved.Slot >= 12 ||
                    string.IsNullOrWhiteSpace(saved.ItemId))
                {
                    continue;
                }

                ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(saved.ItemId);
                if (item == null || !Equipment.IsItemFitsToSlot((EquipmentIndex)saved.Slot, item))
                {
                    Console.WriteLine(
                        "DikozemieOnline: campaign arena item is unavailable: " + saved.ItemId);
                    continue;
                }

                ItemModifier modifier = string.IsNullOrWhiteSpace(saved.ModifierId)
                    ? null
                    : MBObjectManager.Instance.GetObject<ItemModifier>(saved.ModifierId);
                equipment[saved.Slot] = new EquipmentElement(item, modifier);
            }
            return equipment;
        }

        private static string DescribeEquipment(Equipment equipment)
        {
            return string.Join(
                ",",
                Enumerable.Range(0, 12)
                    .Select(index => equipment[index].Item?.StringId)
                    .Where(itemId => !string.IsNullOrWhiteSpace(itemId)));
        }

        private static void ResetInvalidTroop(MissionPeer peer, NetworkCommunicator networkPeer)
        {
            if (peer.SelectedTroopIndex == 0)
            {
                return;
            }
            peer.SelectedTroopIndex = 0;
            // The first peer on a dedicated server can still have network index 0
            // while synchronization is in progress. UpdateSelectedTroopIndex writes
            // a peer reference whose valid wire range starts at 1, so broadcasting
            // it here aborts the initial synchronization. The arena does not use the
            // native troop picker; the authoritative server-side value is sufficient.
        }
    }

    // What SpawnTeamBots built for one team battle: average side ratings for
    // the victory reward and one bot per side for the result screens.
    internal sealed class DikozemieArenaTeamField
    {
        internal int AttackerAverageRating;
        internal int DefenderAverageRating;
        internal string AttackerRepresentativeId = string.Empty;
        internal int AttackerRepresentativeRating;
        internal string DefenderRepresentativeId = string.Empty;
        internal int DefenderRepresentativeRating;
    }
}

