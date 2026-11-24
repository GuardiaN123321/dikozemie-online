using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Diamond;
using TaleWorlds.ObjectSystem;

namespace Dikozemie.Online
{
    internal enum DikozemieArenaAdmissionState
    {
        Pending,
        Authorized,
        Rejected
    }

    internal static class DikozemieArenaServerRegistration
    {
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(4)
        };

        private static readonly object Gate = new object();
        private static DateTime _nextHeartbeatUtc;
        private static DateTime _nextResultAttemptUtc;
        private static bool _heartbeatRunning;
        private static bool _resultRunning;
        private static bool _outboxLoaded;
        private static int _lastPeerCount = -1;
        private static DikozemieArenaServerAssignment _assignment;
        private static DikozemieArenaResultSubmission _pendingResult;
        private static readonly Dictionary<string, DikozemieArenaCharacterProfile> ClientProfiles =
            new Dictionary<string, DikozemieArenaCharacterProfile>(StringComparer.Ordinal);

        private static string ArenaFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Dikozemie",
            "arena");

        private static string ServerFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Dikozemie",
            "server");

        private static string ResultOutboxPath => Path.Combine(ServerFolder, "pending-arena-result.json");

        private static string MatchmakerUrl
        {
            get
            {
                string environment = Environment.GetEnvironmentVariable("DIKOZEMIE_MATCHMAKER_URL");
                if (TryNormalizeUrl(environment, out string fromEnvironment))
                {
                    return fromEnvironment;
                }

                string path = Path.Combine(ArenaFolder, "matchmaker-url.txt");
                if (TryReadText(path, out string configured) &&
                    TryNormalizeUrl(configured, out string fromFile))
                {
                    return fromFile;
                }
                return string.Empty;
            }
        }

        // Version 0.3 uses TaleWorlds lobby discovery and direct in-session
        // profile/result messages. Ignore stale environment configuration from
        // earlier local matchmaker prototypes.
        internal static bool UsesExternalMatchmaker => false;

        private static string ServerToken
        {
            get
            {
                string environment = Environment.GetEnvironmentVariable(
                    "DIKOZEMIE_MATCHMAKER_SERVER_TOKEN");
                if (!string.IsNullOrWhiteSpace(environment))
                {
                    return environment.Trim();
                }

                string path = Path.Combine(ArenaFolder, "matchmaker-server-token.txt");
                return TryReadText(path, out string configured) ? configured.Trim() : string.Empty;
            }
        }

        public static void Tick()
        {
            if (!GameNetwork.IsDedicatedServer)
            {
                return;
            }

            if (!UsesExternalMatchmaker)
            {
                return;
            }

            EnsureOutboxLoaded();
            DateTime now = DateTime.UtcNow;
            int peerCount = GameNetwork.NetworkPeerCount;
            if (peerCount != _lastPeerCount)
            {
                _lastPeerCount = peerCount;
                _nextHeartbeatUtc = DateTime.MinValue;
            }
            lock (Gate)
            {
                if (CanSubmit(_pendingResult) && !_resultRunning && now >= _nextResultAttemptUtc)
                {
                    _resultRunning = true;
                    _nextResultAttemptUtc = now.AddSeconds(5);
                    _ = SubmitResultAsync(_pendingResult);
                }
            }

            if (_heartbeatRunning || now < _nextHeartbeatUtc)
            {
                return;
            }

            LobbyClient client = NetworkMain.GameClient;
            if (client == null || client.CustomBattleId == CustomBattleId.Empty)
            {
                return;
            }

            bool assignmentPending;
            lock (Gate)
            {
                assignmentPending = !IsValidAssignment(_assignment);
            }
            _nextHeartbeatUtc = now.AddSeconds(assignmentPending ? 1 : 10);
            _heartbeatRunning = true;
            _ = RegisterAsync(client.CustomBattleId);
        }

        internal static DikozemieArenaAdmissionState GetAdmissionState(
            NetworkCommunicator peer)
        {
            // A successful TaleWorlds custom-game join is the admission
            // authority in the standalone-server mode. During the early
            // synchronization callback PlayerID can still report IsValid ==
            // false; rejecting it here schedules the peer for the engine's
            // delayed (roughly 15 second) disconnect.
            if (!UsesExternalMatchmaker)
            {
                return peer == null
                    ? DikozemieArenaAdmissionState.Rejected
                    : DikozemieArenaAdmissionState.Authorized;
            }

            if (peer?.PlayerConnectionInfo == null ||
                !peer.PlayerConnectionInfo.PlayerID.IsValid)
            {
                return DikozemieArenaAdmissionState.Rejected;
            }

            string actualPlayerId = peer.PlayerConnectionInfo.PlayerID.ToString();
            lock (Gate)
            {
                if (ClientProfiles.TryGetValue(
                    actualPlayerId,
                    out DikozemieArenaCharacterProfile directProfile) &&
                    IsValidCharacterProfile(directProfile, actualPlayerId))
                {
                    return DikozemieArenaAdmissionState.Authorized;
                }

                if (!IsValidAssignment(_assignment) ||
                    NetworkMain.GameClient == null ||
                    NetworkMain.GameClient.CustomBattleId == CustomBattleId.Empty ||
                    !string.Equals(
                        _assignment.BattleId,
                        NetworkMain.GameClient.CustomBattleId.ToString(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return DikozemieArenaAdmissionState.Pending;
                }

                return _assignment.PlayerIds.Any(playerId => string.Equals(
                    playerId,
                    actualPlayerId,
                    StringComparison.Ordinal))
                    ? DikozemieArenaAdmissionState.Authorized
                    : DikozemieArenaAdmissionState.Rejected;
            }
        }

        internal static bool TryGetCharacterProfile(
            NetworkCommunicator peer,
            out DikozemieArenaCharacterProfile profile)
        {
            profile = null;
            if (peer?.PlayerConnectionInfo == null ||
                !peer.PlayerConnectionInfo.PlayerID.IsValid)
            {
                return false;
            }

            string actualPlayerId = peer.PlayerConnectionInfo.PlayerID.ToString();
            lock (Gate)
            {
                if (ClientProfiles.TryGetValue(
                    actualPlayerId,
                    out DikozemieArenaCharacterProfile directProfile) &&
                    IsValidCharacterProfile(directProfile, actualPlayerId))
                {
                    profile = directProfile;
                    return true;
                }

                if (!IsValidAssignment(_assignment))
                {
                    return false;
                }

                profile = _assignment.CharacterProfiles.FirstOrDefault(candidate =>
                    candidate != null && string.Equals(
                        candidate.PlatformPlayerId,
                        actualPlayerId,
                        StringComparison.Ordinal));
                return profile != null;
            }
        }

        internal static void ClearCompletedDuelReservation(NetworkCommunicator peer)
        {
            if (TryGetCharacterProfile(peer, out var profile))
            {
                profile.DuelOfferId = profile.DuelOpponentId = string.Empty;
                profile.BattleGroupId = string.Empty;
                profile.BattleGroupSize = 0;
            }
        }

        // The group's battle has started: the next one is an ordinary battle.
        internal static void ClearBattleSides(NetworkCommunicator peer)
        {
            if (TryGetCharacterProfile(peer, out DikozemieArenaCharacterProfile profile))
            {
                profile.BattleSideSize = profile.BattleFoeSize = 0;
                profile.BattleSideRating = profile.BattleFoeRating = 0;
            }
        }

        internal static void ClearBattleGroup(NetworkCommunicator peer)
        {
            if (TryGetCharacterProfile(peer, out var profile))
            {
                profile.BattleGroupId = string.Empty;
                profile.BattleGroupSize = 0;
            }
        }

        internal static bool UpdateRequestedBattleSettings(
            NetworkCommunicator peer,
            int teamSize,
            DikozemieArenaDiscipline discipline,
            DikozemieArenaComposition composition)
        {
            if (!TryGetCharacterProfile(
                    peer,
                    out DikozemieArenaCharacterProfile profile))
            {
                return false;
            }

            int previousTeamSize = profile.RequestedTeamSize;
            int previousDiscipline = profile.RequestedDiscipline;
            int previousComposition = profile.RequestedComposition;
            profile.RequestedTeamSize = teamSize;
            profile.RequestedDiscipline = (int)discipline;
            profile.RequestedComposition = (int)composition;

            string playerId = peer.PlayerConnectionInfo.PlayerID.ToString();
            if (!IsValidCharacterProfile(profile, playerId))
            {
                profile.RequestedTeamSize = previousTeamSize;
                profile.RequestedDiscipline = previousDiscipline;
                profile.RequestedComposition = previousComposition;
                return false;
            }

            Console.WriteLine(
                "DikozemieOnline: next arena setup accepted player=" +
                playerId + " team=" + teamSize + " discipline=" +
                discipline + " composition=" + composition);
            return true;
        }

        internal static void BeginClientProfileTransfer(NetworkCommunicator peer)
        {
            if (peer?.PlayerConnectionInfo == null)
            {
                return;
            }

            string playerId = peer.PlayerConnectionInfo.PlayerID.ToString();
            lock (Gate)
            {
                ClientProfiles.Remove(playerId);
            }
            DikozemieArenaProfileTransport.Forget(peer);
        }

        internal static bool AcceptClientProfile(
            NetworkCommunicator peer,
            DikozemieArenaCharacterProfile profile)
        {
            if (peer?.PlayerConnectionInfo == null ||
                !peer.PlayerConnectionInfo.PlayerID.IsValid)
            {
                return false;
            }

            string playerId = peer.PlayerConnectionInfo.PlayerID.ToString();
            // Standalone custom games are admitted by TaleWorlds and the
            // authoritative platform id is the one attached to this server
            // peer.  The client can send its saved hero before that id becomes
            // visible locally; bind it here before validating and storing it.
            if (!UsesExternalMatchmaker && profile != null)
            {
                profile.PlatformPlayerId = playerId;
            }
            if (DikozemieBattleSanctions.IsBanned(playerId))
            {
                // The observer closed the Arena to him; the sanctions tick
                // tells him so and disconnects him.
                MissionMultiplayerDikozemieArena.Trace(
                    "profile rejected player=" + playerId + " reason=banned by the observer");
                return false;
            }
            if (!IsValidCharacterProfile(profile, playerId))
            {
                string problem = DescribeProfileProblem(profile, playerId);
                Console.WriteLine(
                    "DikozemieOnline: invalid campaign profile from player " + playerId + ": " + problem);
                MissionMultiplayerDikozemieArena.Trace(
                    "profile rejected player=" + playerId + " reason=" + problem);
                return false;
            }

            lock (Gate)
            {
                ClientProfiles[playerId] = profile;
            }
            // Older test builds created a second leaderboard row under the
            // platform id before the hero GUID profile arrived. Once this
            // authoritative profile is accepted, that legacy alias is stale.
            DikozemieArenaRatingStore.RemoveLegacyPlatformEntry(
                playerId,
                profile.CharacterId);
            if (DikozemieArenaRatingStore.ApplyHeroReset(
                    peer,
                    profile.ProgressionResetToken))
            {
                Console.WriteLine(
                    "DikozemieOnline: hero progression reset character=" +
                    profile.CharacterId + " player=" + playerId);
            }
            DikozemieEconomyInbox.Bind(playerId, profile.EconomyKeyHash);
            if (!(profile.Observer && DikozemieBattleSanctions.IsObserver(playerId)))
            {
                // An observer carries nothing into a fight.
                DikozemieEconomyInbox.Equip(
                    playerId,
                    (profile.Equipment ?? Array.Empty<DikozemieArenaEquipmentValue>())
                        .Select(item => item?.ItemId));
            }
            Console.WriteLine(
                "DikozemieOnline: campaign arena profile accepted character=" +
                profile.CharacterId + " player=" + playerId);
            return true;
        }

        public static void ReportResult(
            NetworkCommunicator winnerPeer,
            NetworkCommunicator loserPeer,
            int winnerScore,
            int loserScore)
        {
            if (!UsesExternalMatchmaker)
            {
                return;
            }

            EnsureOutboxLoaded();
            lock (Gate)
            {
                if (_pendingResult != null)
                {
                    // Stable resultId makes retries idempotent. Never replace an
                    // unacknowledged authoritative result with a second outcome.
                    return;
                }

                string winnerActual = DikozemieArenaRatingStore.GetPlayerId(winnerPeer);
                string loserActual = DikozemieArenaRatingStore.GetPlayerId(loserPeer);
                _pendingResult = new DikozemieArenaResultSubmission
                {
                    SchemaVersion = 1,
                    ResultId = Guid.NewGuid().ToString(),
                    MatchId = string.Empty,
                    BattleId = NetworkMain.GameClient?.CustomBattleId.ToString() ?? string.Empty,
                    ResultToken = string.Empty,
                    WinnerPlayerId = winnerActual,
                    LoserPlayerId = loserActual,
                    WinnerScore = Math.Max(0, winnerScore),
                    LoserScore = Math.Max(0, loserScore),
                    Reason = "score_limit",
                    CreatedAt = DateTime.UtcNow.ToString("O")
                };
                BindPendingResultToAssignment();
                DikozemieArenaStateFiles.WriteAtomic(ResultOutboxPath, _pendingResult);
                _nextResultAttemptUtc = DateTime.MinValue;
                Console.WriteLine(
                    "DikozemieOnline: authoritative arena result staged " + _pendingResult.ResultId +
                    (CanSubmit(_pendingResult) ? string.Empty : "; waiting for reservation heartbeat"));
            }
        }

        private static async Task RegisterAsync(CustomBattleId battleId)
        {
            try
            {
                var registration = new DikozemieArenaServerHeartbeat
                {
                    BattleId = battleId.ToString(),
                    Mode = "ranked_1v1",
                    Available = GameNetwork.NetworkPeerCount == 0
                };
                using (var request = CreateJsonRequest(
                    MatchmakerUrl + "/servers/register",
                    registration,
                    includeServerToken: true))
                using (HttpResponseMessage response = await Http.SendAsync(request))
                {
                    string json = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine(
                            "DikozemieOnline: matchmaker rejected server heartbeat: " +
                            (int)response.StatusCode);
                        return;
                    }

                    DikozemieArenaServerHeartbeatResponse parsed =
                        Deserialize<DikozemieArenaServerHeartbeatResponse>(json);
                    DikozemieArenaServerAssignment nextAssignment =
                        IsValidAssignment(parsed?.Assignment) ? parsed.Assignment : null;
                    lock (Gate)
                    {
                        bool changed = !string.Equals(
                            _assignment?.MatchId,
                            nextAssignment?.MatchId,
                            StringComparison.Ordinal);
                        _assignment = nextAssignment;
                        if (_assignment != null)
                        {
                            BindPendingResultToAssignment();
                            if (changed)
                            {
                                Console.WriteLine(
                                    "DikozemieOnline: arena reservation received " +
                                    _assignment.MatchId);
                            }
                        }
                        else if (changed)
                        {
                            Console.WriteLine(
                                "DikozemieOnline: arena reservation is no longer active");
                        }
                    }
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: server heartbeat failed: " + error.Message);
            }
            finally
            {
                _heartbeatRunning = false;
            }
        }

        private static async Task SubmitResultAsync(DikozemieArenaResultSubmission submission)
        {
            bool accepted = false;
            try
            {
                using (var request = CreateJsonRequest(
                    MatchmakerUrl + "/matches/" + Uri.EscapeDataString(submission.MatchId) + "/result",
                    submission,
                    includeServerToken: true))
                using (HttpResponseMessage response = await Http.SendAsync(request))
                {
                    accepted = response.IsSuccessStatusCode;
                    if (!accepted)
                    {
                        string message = await response.Content.ReadAsStringAsync();
                        Console.WriteLine(
                            "DikozemieOnline: matchmaker rejected result " +
                            (int)response.StatusCode + ": " + message);
                    }
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: result delivery failed: " + error.Message);
            }
            finally
            {
                lock (Gate)
                {
                    if (accepted && _pendingResult != null && string.Equals(
                        _pendingResult.ResultId,
                        submission.ResultId,
                        StringComparison.Ordinal))
                    {
                        _pendingResult = null;
                        _assignment = null;
                        DikozemieArenaStateFiles.TryDelete(ResultOutboxPath);
                        Console.WriteLine(
                            "DikozemieOnline: authoritative arena result accepted " +
                            submission.ResultId);
                    }
                    else
                    {
                        _nextResultAttemptUtc = DateTime.UtcNow.AddSeconds(10);
                    }
                    _resultRunning = false;
                }
            }
        }

        private static HttpRequestMessage CreateJsonRequest<T>(
            string url,
            T value,
            bool includeServerToken) where T : class
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(Serialize(value), Encoding.UTF8, "application/json")
            };
            if (includeServerToken)
            {
                string token = ServerToken;
                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.Headers.TryAddWithoutValidation("X-Dikozemie-Server-Token", token);
                }
            }
            return request;
        }

        private static void EnsureOutboxLoaded()
        {
            lock (Gate)
            {
                if (_outboxLoaded)
                {
                    return;
                }

                _outboxLoaded = true;
                if (DikozemieArenaStateFiles.TryRead(
                    ResultOutboxPath,
                    out DikozemieArenaResultSubmission pending) && pending != null &&
                    !string.IsNullOrWhiteSpace(pending.ResultId))
                {
                    _pendingResult = pending;
                    Console.WriteLine(
                        "DikozemieOnline: recovered pending authoritative result " +
                        pending.ResultId);
                }
            }
        }

        private static bool IsValidAssignment(DikozemieArenaServerAssignment assignment)
        {
            return assignment != null &&
                Guid.TryParse(assignment.MatchId, out _) &&
                Guid.TryParse(assignment.BattleId, out _) &&
                !string.IsNullOrWhiteSpace(assignment.ResultToken) &&
                assignment.PlayerIds != null && assignment.PlayerIds.Length == 2 &&
                !string.IsNullOrWhiteSpace(assignment.PlayerIds[0]) &&
                !string.IsNullOrWhiteSpace(assignment.PlayerIds[1]) &&
                !string.Equals(
                    assignment.PlayerIds[0],
                    assignment.PlayerIds[1],
                    StringComparison.Ordinal) &&
                assignment.CharacterProfiles != null &&
                assignment.CharacterProfiles.Length == 2 &&
                IsValidCharacterProfile(assignment.CharacterProfiles[0], assignment.PlayerIds[0]) &&
                IsValidCharacterProfile(assignment.CharacterProfiles[1], assignment.PlayerIds[1]) &&
                !string.Equals(
                    assignment.CharacterProfiles[0].CharacterId,
                    assignment.CharacterProfiles[1].CharacterId,
                    StringComparison.OrdinalIgnoreCase) &&
                DateTimeOffset.TryParse(assignment.ExpiresAt, out DateTimeOffset expiresAt) &&
                DateTimeOffset.UtcNow < expiresAt.ToUniversalTime();
        }

        private static bool IsValidCharacterProfile(
            DikozemieArenaCharacterProfile profile,
            string assignedPlayerId)
        {
            return profile != null &&
                profile.SchemaVersion == 1 &&
                Guid.TryParse(profile.CharacterId, out Guid characterId) &&
                characterId != Guid.Empty &&
                (string.IsNullOrWhiteSpace(profile.ProgressionResetToken) ||
                 Guid.TryParse(profile.ProgressionResetToken, out Guid resetToken) &&
                 resetToken != Guid.Empty) &&
                string.Equals(
                    profile.PlatformPlayerId,
                    assignedPlayerId,
                    StringComparison.Ordinal) &&
                (profile.DisplayName?.Length ?? 0) <= 128 &&
                (profile.CultureId?.Length ?? 0) <= 64 &&
                (profile.BodyProperties?.Length ?? 0) <= 4096 &&
                BodyProperties.FromString(
                    profile.BodyProperties,
                    out BodyProperties body) &&
                body.Age >= 18f &&
                profile.Race >= 0 && profile.Race <= 64 &&
                profile.Level >= 1 && profile.Level <= 1024 &&
                profile.Rating >= 0 && profile.Rating <= 100000000 &&
                (profile.RequestedBattleCount == 1 ||
                 profile.RequestedBattleCount == 3) &&
                (profile.PartyLeaderId?.Length ?? 0) <= 128 &&
                (string.IsNullOrEmpty(profile.BattleGroupId) ||
                 Guid.TryParse(profile.BattleGroupId, out Guid battleGroup) && battleGroup != Guid.Empty) &&
                profile.BattleGroupSize >= 0 && profile.BattleGroupSize <= 20 &&
                profile.BattleSideSize >= 0 && profile.BattleSideSize <= 20 &&
                profile.BattleFoeSize >= 0 && profile.BattleFoeSize <= 20 &&
                profile.BattleSideRating >= 0 && profile.BattleSideRating <= 1000000 &&
                profile.BattleFoeRating >= 0 && profile.BattleFoeRating <= 1000000 &&
                profile.RequestedDiscipline >= 0 &&
                profile.RequestedDiscipline <= 2 &&
                profile.RequestedComposition >= 0 &&
                profile.RequestedComposition <= 2 &&
                DikozemieArenaCompositionRules.IsSupported(
                    (DikozemieArenaComposition)
                        profile.RequestedComposition,
                    (DikozemieArenaDiscipline)
                        profile.RequestedDiscipline) &&
                IsValidRequestedBattle(profile) &&
                (string.IsNullOrEmpty(profile.DuelOfferId) && string.IsNullOrEmpty(profile.DuelOpponentId) ||
                 Guid.TryParse(profile.DuelOfferId, out Guid duelOffer) && duelOffer != Guid.Empty &&
                 !string.IsNullOrEmpty(profile.DuelOpponentId) && profile.DuelOpponentId.Length <= 128 &&
                 !string.Equals(profile.DuelOpponentId, assignedPlayerId, StringComparison.Ordinal) &&
                 profile.RequestedTeamSize == 1 &&
                 profile.RequestedDiscipline == (int)DikozemieArenaDiscipline.Foot &&
                 profile.RequestedComposition == (int)DikozemieArenaComposition.Mixed) &&
                !string.IsNullOrWhiteSpace(profile.StartingCharacterId) &&
                profile.StartingCharacterId.Length <= 128 &&
                profile.Equipment != null && profile.Equipment.Length <= 12 &&
                profile.Equipment.All(item => item != null &&
                    item.Slot >= 0 && item.Slot < 12 &&
                    (item.ItemId?.Length ?? 0) <= 128 &&
                    (item.ModifierId?.Length ?? 0) <= 128) &&
                profile.Skills != null && profile.Skills.Length <= 64 &&
                profile.Skills.All(skill => skill != null &&
                    !string.IsNullOrWhiteSpace(skill.SkillId) &&
                    skill.SkillId.Length <= 64 &&
                    skill.Value >= 0 && skill.Value <= 1024) &&
                (profile.SkillExperience == null ||
                 profile.SkillExperience.Length <= 64 &&
                 profile.SkillExperience.All(skill => skill != null &&
                    !string.IsNullOrWhiteSpace(skill.SkillId) &&
                    skill.SkillId.Length <= 64 &&
                    skill.Value >= 0)) &&
                profile.Attributes != null && profile.Attributes.Length == 6 &&
                profile.Attributes.All(attribute => attribute != null &&
                    !string.IsNullOrWhiteSpace(attribute.Id) &&
                    attribute.Id.Length <= 64 &&
                    attribute.Value >= 1 && attribute.Value <= 10) &&
                // The client sends every supported development skill.  That is
                // the 18 native Bannerlord skills plus the three War Sails
                // skills (Mariner, Boatswain and Shipmaster).  Keeping the old
                // native-only limit here rejected the entire hero profile and
                // left a joined player stuck on "matching opponent" forever.
                profile.Focus != null && profile.Focus.Length <= 64 &&
                profile.Focus.All(focus => focus != null &&
                    !string.IsNullOrWhiteSpace(focus.Id) &&
                    focus.Id.Length <= 64 &&
                    focus.Value >= 0 && focus.Value <= 5) &&
                profile.SelectedPerks != null &&
                profile.SelectedPerks.Length <= 216 &&
                profile.SelectedPerks.All(perk =>
                    !string.IsNullOrWhiteSpace(perk) &&
                    perk.Length <= 128) &&
                IsValidArenaLoadout(profile) &&
                AreSelectedPerksValid(profile);
        }

        // Names the first rule a rejected hero breaks, for the server log: a
        // player stuck on «Подбираем противника» is otherwise a mystery
        // (2026-11-10, the native Linux server refused every profile).
        private static string DescribeProfileProblem(
            DikozemieArenaCharacterProfile profile,
            string playerId)
        {
            try
            {
                if (profile == null)
                {
                    return "no profile";
                }
                if (profile.SchemaVersion != 1)
                {
                    return "schema " + profile.SchemaVersion;
                }
                if (!Guid.TryParse(profile.CharacterId, out _))
                {
                    return "character id";
                }
                if (!string.Equals(profile.PlatformPlayerId, playerId, StringComparison.Ordinal))
                {
                    return "player id " + profile.PlatformPlayerId;
                }
                if (!BodyProperties.FromString(profile.BodyProperties, out BodyProperties body))
                {
                    return "body properties";
                }
                if (body.Age < 18f)
                {
                    return "age " + body.Age;
                }
                if (!IsValidRequestedBattle(profile))
                {
                    return "requested battle team=" + profile.RequestedTeamSize +
                        " discipline=" + profile.RequestedDiscipline +
                        " hull=" + profile.RequestedNavalHullId +
                        " scene=" + MultiplayerOptions.OptionType.Map.GetStrValue();
                }
                foreach (DikozemieArenaEquipmentValue saved in profile.Equipment ?? new DikozemieArenaEquipmentValue[0])
                {
                    ItemObject item = saved == null || string.IsNullOrWhiteSpace(saved.ItemId)
                        ? null
                        : MBObjectManager.Instance.GetObject<ItemObject>(saved.ItemId);
                    if (item == null)
                    {
                        return "unknown item " + saved?.ItemId;
                    }
                    if (!Equipment.IsItemFitsToSlot((EquipmentIndex)saved.Slot, item))
                    {
                        return "item " + saved.ItemId + " does not fit slot " + saved.Slot;
                    }
                    if (!string.IsNullOrWhiteSpace(saved.ModifierId) &&
                        MBObjectManager.Instance.GetObject<ItemModifier>(saved.ModifierId) == null)
                    {
                        return "unknown modifier " + saved.ModifierId;
                    }
                }
                if (!IsValidArenaLoadout(profile))
                {
                    return "loadout not allowed: discipline=" + profile.RequestedDiscipline +
                        " composition=" + profile.RequestedComposition;
                }
                foreach (string perk in profile.SelectedPerks ?? new string[0])
                {
                    if (!DikozemieNativePerkCatalog.TryGetDefinition(perk, out _))
                    {
                        return "unknown perk " + perk;
                    }
                }
                if (profile.SelectedPerks != null && !AreSelectedPerksValid(profile))
                {
                    return "perk levels";
                }
                if (profile.Attributes == null || profile.Attributes.Length != 6)
                {
                    return "attributes " + (profile.Attributes?.Length ?? 0);
                }
                return "limits (sizes, ratings, skills or focus)";
            }
            catch (Exception error)
            {
                return "check failed: " + error.GetType().Name + " " + error.Message;
            }
        }

        private static bool IsValidRequestedBattle(
            DikozemieArenaCharacterProfile profile)
        {
            string scene = MultiplayerOptions.OptionType.Map.GetStrValue();
            if (DikozemieArenaSceneRules.IsNavalBoardingScene(scene))
            {
                // A naval hull owns its real crew capacity.  The saved arena
                // team-size/stance controls are irrelevant once the player
                // signs on as a sailor, but the requested hull must be one of
                // the mirrored multiplayer presets.
                return !string.IsNullOrWhiteSpace(
                        profile.RequestedNavalHullId) &&
                    profile.RequestedNavalHullId.Length <= 128 &&
                    DikozemieNavalBattleCatalog.All.Any(battle =>
                        string.Equals(
                            battle.HullId,
                            profile.RequestedNavalHullId,
                            StringComparison.Ordinal));
            }

            return string.IsNullOrWhiteSpace(profile.RequestedNavalHullId) &&
                DikozemieArenaTeamRules.IsTeamSizeSupported(
                    profile.RequestedTeamSize,
                    (DikozemieArenaDiscipline)profile.RequestedDiscipline) &&
                DikozemieArenaSceneRules.IsTeamSizeAllowedOnScene(
                    scene,
                    profile.RequestedTeamSize,
                    (DikozemieArenaDiscipline)
                        profile.RequestedDiscipline);
        }

        private static bool IsValidArenaLoadout(
            DikozemieArenaCharacterProfile profile)
        {
            if (profile?.Equipment == null)
            {
                return false;
            }

            var occupiedSlots = new HashSet<int>();
            var equipment = new Equipment(Equipment.EquipmentType.Battle);
            foreach (DikozemieArenaEquipmentValue saved in profile.Equipment)
            {
                if (saved == null || saved.Slot < 0 || saved.Slot >= 12 ||
                    !occupiedSlots.Add(saved.Slot) ||
                    string.IsNullOrWhiteSpace(saved.ItemId))
                {
                    return false;
                }

                ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(
                    saved.ItemId);
                if (item == null || !Equipment.IsItemFitsToSlot(
                        (EquipmentIndex)saved.Slot,
                        item))
                {
                    return false;
                }

                ItemModifier modifier = null;
                if (!string.IsNullOrWhiteSpace(saved.ModifierId))
                {
                    modifier = MBObjectManager.Instance.GetObject<ItemModifier>(
                        saved.ModifierId);
                    if (modifier == null)
                    {
                        return false;
                    }
                }
                equipment[(EquipmentIndex)saved.Slot] =
                    new EquipmentElement(item, modifier);
            }

            return DikozemieArenaLoadoutRules.IsSelectionAvailable(
                equipment,
                (DikozemieArenaDiscipline)profile.RequestedDiscipline,
                (DikozemieArenaComposition)profile.RequestedComposition,
                out _);
        }

        private static bool AreSelectedPerksValid(
            DikozemieArenaCharacterProfile profile)
        {
            var occupiedTiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string perkId in profile.SelectedPerks)
            {
                if (!DikozemieNativePerkCatalog.TryGetDefinition(
                        perkId,
                        out DikozemieNativePerkDefinition definition))
                {
                    return false;
                }

                DikozemieArenaSkillValue skill = profile.Skills.FirstOrDefault(
                    candidate => candidate != null && string.Equals(
                        candidate.SkillId,
                        definition.SkillId,
                        StringComparison.OrdinalIgnoreCase));
                if (skill == null || skill.Value < definition.RequiredLevel)
                {
                    return false;
                }

                string tierKey = definition.SkillId + ":" +
                    definition.RequiredLevel;
                if (!occupiedTiers.Add(tierKey))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool CanSubmit(DikozemieArenaResultSubmission submission)
        {
            return submission != null &&
                !string.IsNullOrWhiteSpace(submission.ResultId) &&
                !string.IsNullOrWhiteSpace(submission.MatchId) &&
                !string.IsNullOrWhiteSpace(submission.ResultToken);
        }

        private static void BindPendingResultToAssignment()
        {
            if (_pendingResult == null || CanSubmit(_pendingResult) || _assignment == null)
            {
                return;
            }

            string winnerId = ResolveAssignedPlayer(
                _pendingResult.WinnerPlayerId,
                _assignment.PlayerIds);
            string loserId = ResolveAssignedPlayer(
                _pendingResult.LoserPlayerId,
                _assignment.PlayerIds);
            if (string.IsNullOrWhiteSpace(winnerId) || string.IsNullOrWhiteSpace(loserId) ||
                string.Equals(winnerId, loserId, StringComparison.Ordinal))
            {
                Console.WriteLine(
                    "DikozemieOnline: connected platform ids do not match the reserved players");
                return;
            }

            _pendingResult.MatchId = _assignment.MatchId;
            _pendingResult.BattleId = _assignment.BattleId;
            _pendingResult.ResultToken = _assignment.ResultToken;
            _pendingResult.WinnerPlayerId = winnerId;
            _pendingResult.LoserPlayerId = loserId;
            DikozemieArenaStateFiles.WriteAtomic(ResultOutboxPath, _pendingResult);
            _nextResultAttemptUtc = DateTime.MinValue;
        }

        private static string ResolveAssignedPlayer(string actual, string[] assigned)
        {
            if (string.IsNullOrWhiteSpace(actual) || assigned == null)
            {
                return null;
            }

            return assigned.FirstOrDefault(playerId =>
                string.Equals(playerId, actual, StringComparison.Ordinal));
        }

        private static bool TryNormalizeUrl(string value, out string normalized)
        {
            normalized = null;
            string candidate = (value ?? string.Empty).Trim().TrimEnd('/');
            if (Uri.TryCreate(candidate, UriKind.Absolute, out Uri uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                normalized = candidate;
                return true;
            }
            return false;
        }

        private static bool TryReadText(string path, out string value)
        {
            value = null;
            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }
                value = File.ReadAllText(path).Trim();
                return true;
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: config read failed: " + error.Message);
                return false;
            }
        }

        private static string Serialize<T>(T value) where T : class
        {
            using (var stream = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                serializer.WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static T Deserialize<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                return serializer.ReadObject(stream) as T;
            }
        }
    }

    [DataContract]
    internal sealed class DikozemieArenaServerHeartbeat
    {
        [DataMember(Name = "battleId")]
        public string BattleId;

        [DataMember(Name = "mode")]
        public string Mode;

        [DataMember(Name = "available")]
        public bool Available;
    }

    [DataContract]
    internal sealed class DikozemieArenaServerHeartbeatResponse
    {
        [DataMember(Name = "ok")]
        public bool Ok = false;

        [DataMember(Name = "assignment")]
        public DikozemieArenaServerAssignment Assignment = null;
    }

    [DataContract]
    internal sealed class DikozemieArenaServerAssignment
    {
        [DataMember(Name = "matchId")]
        public string MatchId = null;

        [DataMember(Name = "battleId")]
        public string BattleId = null;

        [DataMember(Name = "playerIds")]
        public string[] PlayerIds = Array.Empty<string>();

        [DataMember(Name = "characterProfiles")]
        public DikozemieArenaCharacterProfile[] CharacterProfiles =
            Array.Empty<DikozemieArenaCharacterProfile>();

        [DataMember(Name = "resultToken")]
        public string ResultToken = null;

        [DataMember(Name = "assignedAt")]
        public string AssignedAt = null;

        [DataMember(Name = "expiresAt")]
        public string ExpiresAt = null;
    }

    [DataContract]
    internal sealed class DikozemieArenaCharacterProfile
    {
        [DataMember(Name = "schemaVersion")]
        public int SchemaVersion = 1;

        [DataMember(Name = "characterId")]
        public string CharacterId = string.Empty;

        [DataMember(Name = "progressionResetToken")]
        public string ProgressionResetToken = string.Empty;

        [DataMember(Name = "platformPlayerId")]
        public string PlatformPlayerId = string.Empty;

        [DataMember(Name = "displayName")]
        public string DisplayName = string.Empty;

        [DataMember(Name = "cultureId")]
        public string CultureId = string.Empty;

        [DataMember(Name = "bodyProperties")]
        public string BodyProperties = string.Empty;

        [DataMember(Name = "isFemale")]
        public bool IsFemale = false;

        [DataMember(Name = "race")]
        public int Race = 0;

        [DataMember(Name = "level")]
        public int Level = 1;

        [DataMember(Name = "rating")]
        public int Rating = 1000;

        [DataMember(Name = "duelOfferId")]
        public string DuelOfferId = string.Empty;

        [DataMember(Name = "duelOpponentId")]
        public string DuelOpponentId = string.Empty;

        [DataMember(Name = "requestedBattleCount")]
        public int RequestedBattleCount = 1;

        // Lobby party this player came with, named by its leader. Empty alone.
        [DataMember(Name = "partyLeaderId")]
        public string PartyLeaderId = string.Empty;

        [DataMember(Name = "requestedTeamSize")]
        public int RequestedTeamSize = 1;

        // An ambush on the road starts at once: nobody is waited for.
        [DataMember(Name = "requestsImmediateBattle")]
        public bool RequestsImmediateBattle;

        // Stage 5: everybody who must fight in one battle (a party that took
        // a fight on the road, two parties that met there) carries the same
        // id; the server waits for all battleGroupSize of them.
        [DataMember(Name = "battleGroupId")]
        public string BattleGroupId = string.Empty;

        [DataMember(Name = "battleGroupSize")]
        public int BattleGroupSize = 0;

        // Stage 6: a siege fields unequal sides. Each hero names the size
        // and strength (bot rating) of his own side and of the other one,
        // as the trade server counted them: the storming party with its
        // mercenaries, the garrison. 0: the usual equal sides.
        [DataMember(Name = "battleSideSize")]
        public int BattleSideSize = 0;

        [DataMember(Name = "battleSideRating")]
        public int BattleSideRating = 0;

        [DataMember(Name = "battleFoeSize")]
        public int BattleFoeSize = 0;

        [DataMember(Name = "battleFoeRating")]
        public int BattleFoeRating = 0;

        [DataMember(Name = "requestedDiscipline")]
        public int RequestedDiscipline = (int)DikozemieArenaDiscipline.Open;

        [DataMember(Name = "requestedComposition")]
        public int RequestedComposition =
            (int)DikozemieArenaComposition.Mixed;

        [DataMember(Name = "requestedNavalHullId")]
        public string RequestedNavalHullId = string.Empty;

        [DataMember(Name = "startingCharacterId")]
        public string StartingCharacterId = string.Empty;

        [DataMember(Name = "attributes")]
        public DikozemieArenaDevelopmentValue[] Attributes =
            Array.Empty<DikozemieArenaDevelopmentValue>();

        [DataMember(Name = "focus")]
        public DikozemieArenaDevelopmentValue[] Focus =
            Array.Empty<DikozemieArenaDevelopmentValue>();

        [DataMember(Name = "skills")]
        public DikozemieArenaSkillValue[] Skills = Array.Empty<DikozemieArenaSkillValue>();

        [DataMember(Name = "skillExperience")]
        public DikozemieArenaSkillValue[] SkillExperience =
            Array.Empty<DikozemieArenaSkillValue>();

        [DataMember(Name = "selectedPerks")]
        public string[] SelectedPerks = Array.Empty<string>();

        [DataMember(Name = "equipment")]
        public DikozemieArenaEquipmentValue[] Equipment =
            Array.Empty<DikozemieArenaEquipmentValue>();

        // sha256 of the client's treasury key (stage 3): the battle server
        // vouches for the computer that sent it, the key itself stays home.
        [DataMember(Name = "economyKeyHash")]
        public string EconomyKeyHash = string.Empty;

        // The anti-cheat observer comes to watch, not to fight: the battle
        // server keeps him out of every queue and gives him the spectators'
        // free camera, if the trade server lists him as an observer
        // (battle-sanctions.json). Older servers ignore the field.
        [DataMember(Name = "observer")]
        public bool Observer = false;
    }

    [DataContract]
    internal sealed class DikozemieArenaSkillValue
    {
        [DataMember(Name = "skillId")]
        public string SkillId = string.Empty;

        [DataMember(Name = "value")]
        public int Value = 0;
    }

    [DataContract]
    internal sealed class DikozemieArenaDevelopmentValue
    {
        [DataMember(Name = "id")]
        public string Id = string.Empty;

        [DataMember(Name = "value")]
        public int Value = 0;
    }

    [DataContract]
    internal sealed class DikozemieArenaEquipmentValue
    {
        [DataMember(Name = "slot")]
        public int Slot = 0;

        [DataMember(Name = "itemId")]
        public string ItemId = string.Empty;

        [DataMember(Name = "modifierId")]
        public string ModifierId = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieArenaResultSubmission
    {
        [DataMember(Name = "schemaVersion")]
        public int SchemaVersion;

        [DataMember(Name = "resultId")]
        public string ResultId;

        [DataMember(Name = "matchId")]
        public string MatchId;

        [DataMember(Name = "battleId")]
        public string BattleId;

        [DataMember(Name = "resultToken")]
        public string ResultToken;

        [DataMember(Name = "winnerPlayerId")]
        public string WinnerPlayerId;

        [DataMember(Name = "loserPlayerId")]
        public string LoserPlayerId;

        [DataMember(Name = "winnerScore")]
        public int WinnerScore;

        [DataMember(Name = "loserScore")]
        public int LoserScore;

        [DataMember(Name = "reason")]
        public string Reason;

        [DataMember(Name = "createdAt")]
        public string CreatedAt;
    }
}
