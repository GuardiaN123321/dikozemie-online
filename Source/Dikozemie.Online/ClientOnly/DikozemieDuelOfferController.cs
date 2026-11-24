using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    // HTTP discovers willing people across scene/mode choices. Native Bannerlord
    // remains responsible for the actual connection and authoritative identity.
    internal static class DikozemieDuelOfferController
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        private static Task<DuelOfferReply> _request;
        private static DuelOfferReply _reply;
        private static string _token = string.Empty;
        private static string _shownOffer = string.Empty;
        private static string _activeOffer = string.Empty;
        private static string _completedOffer = string.Empty;
        private static string _activeOpponent = string.Empty;
        private static DateTimeOffset _nextPoll;
        private static DateTimeOffset _joinDeadline;
        private static bool _missionSeen;
        private static bool _combatSeen;
        private static bool _dialogOpen;
        private static bool _stopped;
        private static int _pollFailures;
        private static DikozemieBattleVenue _oldVenue;
        private static int _oldSize;
        private static DikozemieArenaDiscipline _oldDiscipline;
        private static DikozemieArenaComposition _oldComposition;

        internal static string ActiveOfferId => _activeOffer;
        internal static string ActiveOpponentId => _activeOpponent;

        internal static void AbandonForApplicationShutdown()
        {
            _stopped = true;
            // No late HTTP continuation touches native UI after shutdown.
        }

        internal static void Tick()
        {
            if (_stopped) return;
            try { TickCore(); }
            catch (Exception error)
            {
                // An optional invitation must not break the native lobby tick.
                Console.WriteLine("DikozemieOnline: duel offer tick failed: " + error.Message);
                _nextPoll = DateTimeOffset.UtcNow.AddSeconds(10);
            }
        }

        private static void TickCore()
        {
            var client = NetworkMain.GameClient;
            bool available = Game.Current?.GameType is MultiplayerGame &&
                Mission.Current == null && client?.Connected == true && client.AtLobby &&
                client.PlayerID.IsValid && DikozemieHeroProfile.IsCreated &&
                !DikozemieHeroAppearanceEditor.IsOpen && !DikozemiePvpSearchController.IsSearching;

            if (_request?.IsCompleted == true)
            {
                try
                {
                    _reply = _request.GetAwaiter().GetResult();
                    _token = _reply.SessionToken ?? string.Empty;
                    _pollFailures = 0;
                }
                catch (DuelSessionExpiredException) { _token = string.Empty; _reply = null; }
                catch (Exception error)
                {
                    Console.WriteLine("DikozemieOnline: duel presence unavailable: " + error.Message);
                    _reply = null;
                    _pollFailures = Math.Min(5, _pollFailures + 1);
                    _nextPoll = DateTimeOffset.UtcNow.AddSeconds(Math.Min(30, 1 << _pollFailures));
                }
                _request = null;
            }

            if (!string.IsNullOrEmpty(_activeOffer))
            {
                if (Mission.Current != null)
                {
                    _missionSeen = true;
                    foreach (NetworkCommunicator peer in GameNetwork.NetworkPeers ??
                        new System.Collections.Generic.List<NetworkCommunicator>())
                        if (peer.IsSynchronized && peer.PlayerConnectionInfo?.PlayerID.ToString() == _activeOpponent &&
                            peer.GetComponent<MissionPeer>()?.ControlledAgent != null)
                            _combatSeen = true;
                }
                if (Mission.Current == null && client?.AtLobby == true &&
                    (_missionSeen || !DikozemiePvpSearchController.IsSearching))
                    FinishPlan();
                else if (!_combatSeen && DateTimeOffset.UtcNow >= _joinDeadline)
                {
                    DikozemiePvpSearchController.Cancel();
                    DikozemieLobbyShellIntegration.PrepareForTrainingExit();
                    var lobby = Mission.Current?.GetMissionBehavior<MissionLobbyComponent>();
                    if (lobby != null) lobby.QuitMission(); else client?.QuitFromCustomGame();
                    BannerlordNetwork.EndMultiplayerLobbyMission();
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Соперник не смог загрузиться за две минуты. Дуэль отменена; бот его не заменял."));
                    FinishPlan();
                }
            }

            bool currentOffer = available && _reply?.Status == "offered" &&
                _reply.ExpiresAtMs > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (_dialogOpen && (!currentOffer || _reply.OfferId != _shownOffer))
            {
                InformationManager.HideInquiry();
                _dialogOpen = false;
            }
            if (currentOffer && !_reply.SelfAccepted && _reply.OfferId != _shownOffer &&
                !InformationManager.IsAnyInquiryActive())
            {
                string id = _reply.OfferId;
                _shownOffer = id;
                _dialogOpen = true;
                InformationManager.ShowInquiry(new InquiryData(
                    "ЕСТЬ ЖИВОЙ СОПЕРНИК",
                    "Игрок «" + _reply.OpponentName + "» готов сразиться.\n\n" +
                    "Пешая дуэль 1×1, смешанное вооружение. Начать после согласия обоих?\n" +
                    "При отказе выбранный режим сохранится.",
                    true, true, "СРАЗИТЬСЯ", "НЕ СЕЙЧАС",
                    () => Respond(id, true), () => Respond(id, false)), true, true);
            }
            if (available && _reply?.Status == "matched" &&
                _reply.OfferId != _completedOffer && string.IsNullOrEmpty(_activeOffer))
                JoinAcceptedDuel(client);

            if (_request == null && DateTimeOffset.UtcNow >= _nextPoll &&
                client?.PlayerID.IsValid == true && (available || !string.IsNullOrEmpty(_token)))
            {
                _nextPoll = DateTimeOffset.UtcNow.AddSeconds(2);
                _request = Post("presence", new DuelOfferRequest {
                    PlayerId = client.PlayerID.ToString(), DisplayName = client.Name,
                    SessionToken = _token, Available = available });
            }
        }

        private static void Respond(string offerId, bool accept)
        {
            _dialogOpen = false;
            if (_request != null || _reply?.OfferId != offerId ||
                _reply.ExpiresAtMs <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            {
                // A heartbeat can be in flight when the button is pressed.
                // Queue the response after it without touching native UI off-thread.
                var previous = _request;
                _request = RespondAfter(previous, offerId, accept);
            }
            else _request = Post("respond", new DuelOfferRequest {
                SessionToken = _token, OfferId = offerId, Accept = accept });
            _nextPoll = DateTimeOffset.UtcNow.AddSeconds(2);
            if (accept) InformationManager.DisplayMessage(new InformationMessage(
                "Согласие отправлено. Ждём подтверждения соперника…"));
        }

        private static async Task<DuelOfferReply> RespondAfter(Task<DuelOfferReply> previous, string id, bool accept)
        {
            string token = _token;
            if (previous != null) await previous.ConfigureAwait(false);
            return await Post("respond", new DuelOfferRequest {
                SessionToken = token, OfferId = id, Accept = accept }).ConfigureAwait(false);
        }

        private static void JoinAcceptedDuel(TaleWorlds.MountAndBlade.Diamond.LobbyClient client)
        {
            _oldVenue = DikozemieArenaMatchSelection.Venue;
            _oldSize = DikozemieArenaMatchSelection.TeamSize;
            _oldDiscipline = DikozemieArenaMatchSelection.Discipline;
            _oldComposition = DikozemieArenaMatchSelection.Composition;
            if (!DikozemieArenaMatchSelection.SelectDiscipline(DikozemieArenaDiscipline.Foot) ||
                !DikozemieArenaMatchSelection.SelectComposition(DikozemieArenaComposition.Mixed))
            {
                Respond(_reply.OfferId, false);
                return;
            }
            DikozemieArenaMatchSelection.SelectVenue(DikozemieBattleVenue.Arena);
            DikozemieArenaMatchSelection.SelectTeamSize(1);
            _activeOffer = _reply.OfferId;
            _activeOpponent = _reply.OpponentId;
            _joinDeadline = DateTimeOffset.UtcNow.AddMinutes(2);
            _missionSeen = _combatSeen = false;
            if (!DikozemiePvpSearchController.Start(client)) FinishPlan();
        }

        internal static void NotifyCombatComplete()
        {
            if (!string.IsNullOrEmpty(_activeOffer)) FinishPlan();
        }

        private static async Task<DuelOfferReply> CompleteAfter(
            Task<DuelOfferReply> previous, string token, string offer)
        {
            if (previous != null)
            {
                try { await previous.ConfigureAwait(false); } catch { }
            }
            return await Post("respond", new DuelOfferRequest {
                SessionToken = token, OfferId = offer, Complete = true }).ConfigureAwait(false);
        }

        private static void FinishPlan()
        {
            _completedOffer = _activeOffer;
            if (!string.IsNullOrEmpty(_activeOffer))
                _request = CompleteAfter(_request, _token, _activeOffer);
            _activeOffer = _activeOpponent = string.Empty;
            _reply = null;
            DikozemieArenaMatchSelection.SelectDiscipline(_oldDiscipline);
            DikozemieArenaMatchSelection.SelectTeamSize(_oldSize);
            DikozemieArenaMatchSelection.SelectComposition(_oldComposition);
            DikozemieArenaMatchSelection.SelectVenue(_oldVenue);
            _nextPoll = DateTimeOffset.UtcNow.AddSeconds(5);
        }

        private static async Task<DuelOfferReply> Post(string action, DuelOfferRequest payload)
        {
            // Override is useful for isolated live QA; production uses the same
            // certificate-protected domain as the public game download.
            string root = Environment.GetEnvironmentVariable("DIKOZEMIE_DUEL_OFFERS_URL") ??
                "https://136-234-5-140.sslip.io/pvp/duels";
            byte[] json;
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(DuelOfferRequest)).WriteObject(stream, payload);
                json = stream.ToArray();
            }
            using (var content = new StringContent(Encoding.UTF8.GetString(json), Encoding.UTF8, "application/json"))
            using (var response = await Http.PostAsync(root.TrimEnd('/') + "/" + action, content).ConfigureAwait(false))
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized) throw new DuelSessionExpiredException();
                response.EnsureSuccessStatusCode();
                using (var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false)))
                    return (DuelOfferReply)new DataContractJsonSerializer(typeof(DuelOfferReply)).ReadObject(stream);
            }
        }
        private sealed class DuelSessionExpiredException : Exception { }
        [DataContract]
        private sealed class DuelOfferRequest
        {
            [DataMember(Name="playerId")] public string PlayerId = "";
            [DataMember(Name="displayName")] public string DisplayName = "";
            [DataMember(Name="sessionToken")] public string SessionToken = "";
            [DataMember(Name="available")] public bool Available;
            [DataMember(Name="offerId")] public string OfferId = "";
            [DataMember(Name="accept", EmitDefaultValue=false)] public bool? Accept;
            [DataMember(Name="complete")] public bool Complete;
        }
        [DataContract]
        private sealed class DuelOfferReply
        {
            [DataMember(Name="sessionToken")] public string SessionToken = "";
            [DataMember(Name="status")] public string Status = "";
            [DataMember(Name="offerId")] public string OfferId = "";
            [DataMember(Name="opponentId")] public string OpponentId = "";
            [DataMember(Name="opponentName")] public string OpponentName = "";
            [DataMember(Name="expiresAtMs")] public long ExpiresAtMs = 0;
            [DataMember(Name="selfAccepted")] public bool SelfAccepted = false;
        }
    }
}
