using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Diamond;

namespace Dikozemie.Online
{
    // A battle of the shared world (stage 5): two heroes who met on the road,
    // a party that took a fight there, a raid on a caravan with friends or a
    // ship boarded at sea. It goes through the ordinary arena search, but the
    // profile the hero sends names the people who must fight in it:
    //   duel  — DuelOfferId + DuelOpponentId (the battle servers pair exactly
    //           these two, whatever the ratings, and never hand them a bot);
    //   team, naval — BattleGroupId + BattleGroupSize (the server waits for
    //           everybody of the group, sides by PartyLeaderId).
    // Who won is read by the trade server from the battle server's own lines,
    // never from this client.
    internal sealed class DikozemieWorldBattlePlan
    {
        internal string Kind = "meeting";
        internal string Id = string.Empty;
        internal string Mode = "duel";
        internal string Opponent = string.Empty;
        internal int GroupSize = 1;
        internal string SideLeader = string.Empty;
        internal int TeamSize = 1;
        internal int PreferredPort;
        internal string Title = string.Empty;
        internal DikozemieWorldCard Card;
        internal DateTime StartedUtc;
        internal bool SearchStarted;
        internal bool MissionSeen;
        internal DateTime MissionSeenUtc;
        internal bool OpponentSeen;
        internal bool Leaving;
        internal bool ResultSeen;
        internal bool Victory;
        internal bool Draw;
        // The one who was attacked waits for the attacker to stand on a
        // server first: two duel servers, and both must pick the same one.
        internal DateTime SearchNotBeforeUtc;
        // Stage 6: a siege fields unequal sides; the trade server counted
        // the storming party with its mercenaries and the garrison.
        internal bool Attacking;
        internal int SideSize;
        internal int SideRating;
        internal int FoeSize;
        internal int FoeRating;
    }

    internal static class DikozemieWorldBattle
    {
        internal static DikozemieWorldBattlePlan Active { get; private set; }
        private static DikozemieWorldBattlePlan _finished;

        internal static bool IsPending => Active != null;

        internal static string DuelOfferId =>
            Active != null && Active.Mode == "duel" ? Active.Id : string.Empty;

        internal static string DuelOpponentId =>
            Active != null && Active.Mode == "duel" ? Active.Opponent : string.Empty;

        // A siege is fought alone even by one hero with his men: nobody else
        // from the arena queue may walk into it.
        internal static string BattleGroupId =>
            Active != null && Active.Mode != "duel" && (Active.GroupSize > 1 || Active.Kind == "siege")
                ? Active.Id
                : string.Empty;

        internal static int BattleGroupSize => BattleGroupId.Length > 0 ? Active.GroupSize : 0;

        // Sides of a team battle: everybody under one leader stands together.
        internal static string PartyLeaderId => Active?.SideLeader ?? string.Empty;

        // A group starts the moment it is complete; a duel waits for its pair.
        internal static bool RequestsImmediate => Active != null && Active.Mode != "duel";

        internal static int PreferredPort => Active?.PreferredPort ?? 0;

        internal static int SideSize => Active?.SideSize ?? 0;
        internal static int SideRating => Active?.SideRating ?? 0;
        internal static int FoeSize => Active?.FoeSize ?? 0;
        internal static int FoeRating => Active?.FoeRating ?? 0;

        // The arena's line while a hero of the road has not come yet.
        internal static string WaitingText
        {
            get
            {
                DikozemieWorldBattlePlan plan = Active;
                if (plan == null)
                {
                    return string.Empty;
                }
                if (plan.Mode == "duel")
                {
                    string name = DikozemieWorld.Meeting?.Them?.Count > 0 ? DikozemieWorld.Meeting.Them[0].Name : "соперник";
                    return "Встреча на дороге: ждём, когда выйдет " + name + "…";
                }
                return plan.Kind == "party" ? "Собираем отряд…"
                    : plan.Kind == "siege" ? "Штурм: собираем всех у ворот…"
                    : "Собираем всех, кто встретился на дороге…";
            }
        }

        internal static bool Start(DikozemieWorldBattlePlan plan, out string error)
        {
            error = string.Empty;
            if (plan == null || string.IsNullOrEmpty(plan.Id))
            {
                error = "Нет боя.";
                return false;
            }
            if (Active != null && Active.Id == plan.Id)
            {
                return true;
            }
            if (Mission.Current != null)
            {
                error = "Сначала закончите нынешний бой.";
                return false;
            }
            if (plan.Mode == "naval" && !DikozemieLaunchGuard.IsNavalAvailable)
            {
                error = DikozemieLaunchGuard.NavalUnavailableText;
                return false;
            }
            var client = NetworkMain.GameClient;
            if (client == null || !client.Connected || !client.AtLobby)
            {
                error = "Сетевое лобби ещё не готово. Подождите несколько секунд.";
                return false;
            }
            if (DikozemiePvpSearchController.IsSearching)
            {
                DikozemiePvpSearchController.Cancel();
            }
            plan.StartedUtc = DateTime.UtcNow;
            Active = plan;
            DikozemieLobbyShellIntegration.TraceButtonAction("world battle planned kind=" + plan.Kind + " mode=" +
                plan.Mode + " id=" + plan.Id + " group=" + plan.GroupSize + " team=" + plan.TeamSize +
                " leader=" + plan.SideLeader + " opponent=" + plan.Opponent);
            if (DateTime.UtcNow >= plan.SearchNotBeforeUtc)
            {
                BeginSearch();
            }
            return true;
        }

        private static void BeginSearch()
        {
            DikozemieWorldBattlePlan plan = Active;
            if (plan == null || plan.SearchStarted)
            {
                return;
            }
            plan.SearchStarted = true;
            DikozemieLobbyShellIntegration.TraceButtonAction("world battle search mode=" + plan.Mode +
                " port=" + plan.PreferredPort);
            switch (plan.Mode)
            {
                case "duel":
                    // The battle servers take a reserved pair only on foot.
                    DikozemieLobbyShellIntegration.StartRoadBattle(DikozemieBattleVenue.Arena, 1,
                        DikozemieArenaDiscipline.Foot);
                    break;
                case "naval":
                    DikozemieLobbyShellIntegration.StartRoadBattle(DikozemieBattleVenue.Naval, Math.Max(1, plan.TeamSize));
                    break;
                case "team" when plan.Kind == "siege":
                    // The storm of a gate is fought on foot.
                    DikozemieLobbyShellIntegration.StartRoadBattle(DikozemieBattleVenue.Arena,
                        Math.Max(2, Math.Min(10, plan.TeamSize)), DikozemieArenaDiscipline.Foot);
                    break;
                default:
                    DikozemieLobbyShellIntegration.StartRoadBattle(DikozemieBattleVenue.Arena,
                        Math.Max(2, Math.Min(10, plan.TeamSize)));
                    break;
            }
        }

        internal static void UpdatePort(int port)
        {
            if (Active != null && port > 0 && Active.PreferredPort == 0)
            {
                Active.PreferredPort = port;
                DikozemieLobbyShellIntegration.TraceButtonAction("world battle port " + port);
            }
        }

        // The arena told the client how the series ended.
        internal static void NoteResult(bool victory, bool draw)
        {
            if (Active == null)
            {
                return;
            }
            Active.ResultSeen = true;
            Active.Victory = victory;
            Active.Draw = draw;
            DikozemieLobbyShellIntegration.TraceButtonAction("world battle result victory=" + victory + " draw=" + draw);
        }

        internal static void Tick()
        {
            DikozemieWorldBattlePlan plan = Active;
            if (plan == null)
            {
                return;
            }
            DateTime now = DateTime.UtcNow;
            if (!plan.SearchStarted)
            {
                // Waiting for the other side's server, but not for ever.
                if (plan.PreferredPort > 0 || now >= plan.SearchNotBeforeUtc)
                {
                    BeginSearch();
                }
                return;
            }
            if (Mission.Current != null)
            {
                if (!plan.MissionSeen)
                {
                    plan.MissionSeen = true;
                    plan.MissionSeenUtc = now;
                }
                TickWaitingForPair(plan, now);
                return;
            }
            bool searching = DikozemiePvpSearchController.IsSearching ||
                DikozemieLobbyShellIntegration.IsRoadBattlePending;
            if (plan.MissionSeen && !searching)
            {
                Finish("back in the lobby");
                return;
            }
            if (!plan.MissionSeen && !searching && (now - plan.StartedUtc).TotalSeconds > 8)
            {
                Finish("search stopped");
                return;
            }
            // Nobody brought the hero onto a server in four minutes: the
            // trade server has given the meeting up by now.
            if (!plan.MissionSeen && (now - plan.StartedUtc).TotalMinutes > 4)
            {
                DikozemiePvpSearchController.Cancel();
                Finish("no server in four minutes");
            }
        }

        // A reserved duel never gets a bot: if the other hero does not come,
        // or the trade server has already settled the meeting without the
        // fight, the hero leaves the arena instead of waiting there for ever.
        private static void TickWaitingForPair(DikozemieWorldBattlePlan plan, DateTime now)
        {
            if (plan.Mode != "duel" || plan.ResultSeen || plan.Leaving)
            {
                return;
            }
            if (!plan.OpponentSeen)
            {
                foreach (NetworkCommunicator peer in GameNetwork.NetworkPeers ??
                             new System.Collections.Generic.List<NetworkCommunicator>())
                {
                    if (peer.IsSynchronized && peer.PlayerConnectionInfo?.PlayerID.ToString() == plan.Opponent)
                    {
                        plan.OpponentSeen = true;
                        DikozemieLobbyShellIntegration.TraceButtonAction("world battle opponent arrived");
                        break;
                    }
                }
            }
            DikozemieWorldMeeting meeting = DikozemieWorld.Meeting;
            bool settled = meeting == null || meeting.Fight?.Guid != plan.Id || meeting.State == "done" ||
                meeting.State == "won";
            bool tooLong = !plan.OpponentSeen && (now - plan.MissionSeenUtc).TotalSeconds > 200;
            if (!settled && !tooLong)
            {
                return;
            }
            plan.Leaving = true;
            DikozemieLobbyShellIntegration.TraceButtonAction("world battle leaves the arena settled=" + settled +
                " waited=" + (int)(now - plan.MissionSeenUtc).TotalSeconds);
            InformationManager.DisplayMessage(new InformationMessage(settled
                ? "Встреча на дороге уже решена: возвращаемся на карту."
                : "Соперник так и не вышел на бой. Возвращаемся на карту."));
            DikozemiePvpSearchController.Cancel();
            DikozemieLobbyShellIntegration.PrepareForTrainingExit();
            var lobby = Mission.Current?.GetMissionBehavior<MissionLobbyComponent>();
            if (lobby != null)
            {
                lobby.QuitMission();
            }
            else
            {
                NetworkMain.GameClient?.QuitFromCustomGame();
            }
            BannerlordNetwork.EndMultiplayerLobbyMission();
        }

        private static void Finish(string why)
        {
            DikozemieLobbyShellIntegration.TraceButtonAction("world battle over (" + why + ") kind=" + Active?.Kind +
                " result=" + Active?.ResultSeen + " victory=" + Active?.Victory);
            bool fought = Active?.MissionSeen == true;
            _finished = Active;
            Active = null;
            DikozemieWorld.PulseSoon();
            if (fought)
            {
                // What came of it waits on the map, not on the arena page.
                DikozemieLobbyShellIntegration.OpenMap(DikozemieTown.Here != null);
            }
        }

        // The map takes the finished battle once (a party fight on the road
        // ends with the outcome card of that road).
        internal static bool TryTakeFinished(out DikozemieWorldBattlePlan plan)
        {
            plan = _finished;
            _finished = null;
            return plan != null;
        }

        // The arena's words for a siege: the place and the sides at its gate.
        internal static bool IsSiege => Active?.Kind == "siege";

        internal static string SiegeLine =>
            Active == null ? string.Empty
                : "ШТУРМ ВОРОТ · " + Active.Title.Replace("ШТУРМ · ", string.Empty) + " · " +
                  Math.Max(1, Active.SideSize) + " ПРОТИВ " + Math.Max(1, Active.FoeSize);

        internal static string SiegeTitle(bool victory, bool draw) =>
            draw ? "ШТУРМ ЗАХЛЕБНУЛСЯ"
                : Active?.Attacking == true
                    ? victory ? "ВОРОТА ВЗЯТЫ" : "ШТУРМ ОТБИТ"
                    : victory ? "СТЕНЫ УСТОЯЛИ" : "КРЕПОСТЬ ПАЛА";

        internal static string ModeText(string mode, int size)
        {
            switch (mode)
            {
                case "duel": return "схватка один на один";
                case "naval": return "абордаж: корабль на корабль";
                case "siege": return "штурм ворот";
                default: return "бой отряда на отряд, " + size + "×" + size;
            }
        }
    }
}
