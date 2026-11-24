using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    [DataContract]
    internal sealed class DikozemieNotice
    {
        [DataMember(Name = "id")] public long Id;
        [DataMember(Name = "at")] public double At;
        [DataMember(Name = "text")] public string Text = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieWatchPerson
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "place")] public string Place = string.Empty;
        [DataMember(Name = "node")] public string Node = string.Empty;
        [DataMember(Name = "port")] public int Port;
        [DataMember(Name = "coins")] public int Coins;
        [DataMember(Name = "hour")] public int Hour;
        [DataMember(Name = "flags")] public int Flags;
        [DataMember(Name = "complaints")] public int Complaints;
        [DataMember(Name = "banned")] public bool Banned;
        [DataMember(Name = "observer")] public bool Observer;
        [DataMember(Name = "naval")] public bool Naval;
        [DataMember(Name = "server")] public string Server = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieWatchBattlePlayer
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "alive")] public bool Alive;
        [DataMember(Name = "observer")] public bool Observer;
    }

    // What a battle server says is on it (battle-presence/*.json).
    [DataContract]
    internal sealed class DikozemieWatchBattle
    {
        [DataMember(Name = "server")] public string Server = string.Empty;
        [DataMember(Name = "port")] public int Port;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "naval")] public bool Naval;
        [DataMember(Name = "players")] public List<DikozemieWatchBattlePlayer> Players = new List<DikozemieWatchBattlePlayer>();
    }

    [DataContract]
    internal sealed class DikozemieWatchFlag
    {
        [DataMember(Name = "at")] public double At;
        [DataMember(Name = "player")] public string Player = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "kind")] public string Kind = string.Empty;
        [DataMember(Name = "note")] public string Note = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieWatchComplaint
    {
        [DataMember(Name = "id")] public int Id;
        [DataMember(Name = "at")] public double At;
        [DataMember(Name = "reporter")] public string Reporter = string.Empty;
        [DataMember(Name = "target")] public string Target = string.Empty;
        [DataMember(Name = "targetName")] public string TargetName = string.Empty;
        [DataMember(Name = "place")] public string Place = string.Empty;
        [DataMember(Name = "text")] public string Text = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieWatchSanction
    {
        [DataMember(Name = "id")] public int Id;
        [DataMember(Name = "at")] public double At;
        [DataMember(Name = "player")] public string Player = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "action")] public string Action = string.Empty;
        [DataMember(Name = "title")] public string Title = string.Empty;
        [DataMember(Name = "reason")] public string Reason = string.Empty;
        [DataMember(Name = "by")] public string By = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieWatchBan
    {
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "until")] public double Until;
        [DataMember(Name = "reason")] public string Reason = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieWatchView
    {
        [DataMember(Name = "ok")] public bool Ok;
        [DataMember(Name = "message")] public string Message = string.Empty;
        [DataMember(Name = "serverTime")] public double ServerTime;
        [DataMember(Name = "people")] public List<DikozemieWatchPerson> People = new List<DikozemieWatchPerson>();
        [DataMember(Name = "battles")] public List<DikozemieWatchBattle> Battles = new List<DikozemieWatchBattle>();
        [DataMember(Name = "flags")] public List<DikozemieWatchFlag> Flags = new List<DikozemieWatchFlag>();
        [DataMember(Name = "complaints")] public List<DikozemieWatchComplaint> Complaints = new List<DikozemieWatchComplaint>();
        [DataMember(Name = "sanctions")] public List<DikozemieWatchSanction> Sanctions = new List<DikozemieWatchSanction>();
        [DataMember(Name = "banned")] public List<DikozemieWatchBan> Banned = new List<DikozemieWatchBan>();
        // The trade server's own trouble (no starter rosters: the check of
        // new heroes is off).
        [DataMember(Name = "note")] public string Note = string.Empty;
    }

    // One hero's page for the observer: where his coins came from.
    [DataContract]
    internal sealed class DikozemieWatchBookDay
    {
        [DataMember(Name = "kind")] public string Kind = string.Empty;
        [DataMember(Name = "title")] public string Title = string.Empty;
        [DataMember(Name = "earned")] public int Earned;
        [DataMember(Name = "spent")] public int Spent;
        [DataMember(Name = "count")] public int Count;
    }

    [DataContract]
    internal sealed class DikozemieWatchBookLine
    {
        [DataMember(Name = "at")] public double At;
        [DataMember(Name = "amount")] public int Amount;
        [DataMember(Name = "balance")] public int Balance;
        [DataMember(Name = "kind")] public string Kind = string.Empty;
        [DataMember(Name = "title")] public string Title = string.Empty;
        [DataMember(Name = "note")] public string Note = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieWatchBook
    {
        [DataMember(Name = "ok")] public bool Ok;
        [DataMember(Name = "serverTime")] public double ServerTime;
        [DataMember(Name = "id")] public string Id = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "coins")] public int Coins;
        [DataMember(Name = "created")] public double Created;
        [DataMember(Name = "items")] public int Items;
        [DataMember(Name = "banned")] public bool Banned;
        [DataMember(Name = "day")] public List<DikozemieWatchBookDay> Day = new List<DikozemieWatchBookDay>();
        [DataMember(Name = "book")] public List<DikozemieWatchBookLine> Book = new List<DikozemieWatchBookLine>();
        [DataMember(Name = "flags")] public List<DikozemieWatchFlag> Flags = new List<DikozemieWatchFlag>();
        [DataMember(Name = "complaints")] public List<DikozemieWatchComplaint> Complaints = new List<DikozemieWatchComplaint>();
        [DataMember(Name = "measures")] public List<DikozemieWatchSanction> Measures = new List<DikozemieWatchSanction>();
    }

    [DataContract]
    internal sealed class DikozemieWatchAnswer
    {
        [DataMember(Name = "ok")] public bool Ok;
        [DataMember(Name = "message")] public string Message = string.Empty;
        [DataMember(Name = "text")] public string Text = string.Empty;
    }

    // The watch on the client (plan, «Античит»: «дежурный входит в бой
    // невидимым и летает по полю. Рядом панель: кто в игре, сколько
    // заработал за час, отметки античита, жалобы игроков. Кнопки
    // „предупредить“, „выгнать из боя“, „заблокировать“»). Everything is
    // decided by the trade server (EconomyServer, class Watch) and carried
    // out by the battle servers; this side shows and asks.
    //
    // For every hero: the observer's warnings arrive as a message box, and a
    // complaint about another hero can be sent from his card on the map.
    internal static class DikozemieWatch
    {
        private static readonly HashSet<string> ShownNotices = new HashSet<string>(StringComparer.Ordinal);
        // Warnings wait for the lobby: a box over a fight would stop it (the
        // observer warns most often the player he is watching in battle).
        private static readonly List<DikozemieNotice> WaitingNotices = new List<DikozemieNotice>();
        private static bool _noticeOpen;
        private static bool _pendingObserverSaid;
        private static bool _loading;
        private static DateTime _loadedUtc = DateTime.MinValue;

        internal static bool IsObserver => DikozemieEconomy.State?.Observer ?? false;

        // The observer's screen (a section of the clan screen) and its
        // button in the line under the header.
        private static bool _screenRequest;
        internal static bool IsScreenOpen { get; set; }

        internal static void RequestScreen()
        {
            _screenRequest = true;
            DikozemieLobbyShellIntegration.OpenMap(false);
        }

        internal static bool ConsumeScreenRequest()
        {
            bool request = _screenRequest;
            _screenRequest = false;
            return request;
        }
        internal static DikozemieWatchView View { get; private set; }
        internal static string LastError { get; private set; } = string.Empty;
        internal static int Revision { get; private set; }

        // A battle the observer chose to watch: the arena search goes to
        // exactly this server, and the hero's profile says «observer».
        internal static int WatchPort { get; private set; }
        internal static bool WatchNaval { get; private set; }
        internal static string WatchTitle { get; private set; } = string.Empty;
        internal static bool IsWatching => WatchPort > 0;

        // ---- every hero: warnings -------------------------------------------------

        internal static void OnState(DikozemieEconomyState state)
        {
            if (state != null && state.ObserverPending && !_pendingObserverSaid)
            {
                _pendingObserverSaid = true;
                Say("Вы наблюдатель. Панель «НАБЛЮДЕНИЕ» откроется после первого боя на арене с этого компьютера: " +
                    "сервер боя подтвердит, что это вы.", true);
            }
            if (state?.Notices == null)
            {
                return;
            }
            foreach (DikozemieNotice notice in state.Notices)
            {
                if (!ShownNotices.Contains(Key(notice)) && WaitingNotices.All(n => Key(n) != Key(notice)))
                {
                    WaitingNotices.Add(notice);
                }
            }
        }

        // Application tick (client): a waiting warning shows in the lobby,
        // never over a battle, a town walk or another window.
        internal static void Tick()
        {
            if (WaitingNotices.Count == 0 || _noticeOpen || Mission.Current != null ||
                InformationManager.IsAnyInquiryActive() ||
                !(GameStateManager.Current?.ActiveState is LobbyState))
            {
                return;
            }
            List<DikozemieNotice> fresh = WaitingNotices.ToList();
            WaitingNotices.Clear();
            foreach (DikozemieNotice notice in fresh)
            {
                ShownNotices.Add(Key(notice));
            }
            _noticeOpen = true;
            string text = string.Join("\n\n", fresh.Select(n => "«" + n.Text + "»")) +
                "\n\nНаблюдатель следит за честностью боёв и торговли. Повторное нарушение — " +
                "удаление из боя или блокировка казны и серверов.";
            // «Understood» names the warnings read: one that comes while the
            // box is open stays on the server for the next box.
            string ids = string.Join(",", fresh.Where(n => n.Id > 0)
                .Select(n => n.Id.ToString(CultureInfo.InvariantCulture)));
            InformationManager.ShowInquiry(new InquiryData(
                "ПРЕДУПРЕЖДЕНИЕ НАБЛЮДАТЕЛЯ", text, true, false, "ПОНЯТНО", string.Empty,
                () =>
                {
                    _noticeOpen = false;
                    DikozemieEconomy.Request(HttpMethod.Post, "notices/ack", "{\"ids\":[" + ids + "]}",
                        (status, raw, error) => { });
                },
                null), true, true);
        }

        private static string Key(DikozemieNotice notice) =>
            notice.Id > 0
                ? "#" + notice.Id.ToString(CultureInfo.InvariantCulture)
                : notice.At.ToString("0.000", CultureInfo.InvariantCulture) + "|" + notice.Text;

        // ---- every hero: a complaint ------------------------------------------------

        internal static void AskReport(string targetId, string targetName, string place)
        {
            if (string.IsNullOrEmpty(targetId) || !DikozemieEconomy.CanAsk)
            {
                Say("Жалобу сейчас не отправить: нет связи с казной.", false);
                return;
            }
            InformationManager.ShowTextInquiry(new TextInquiryData(
                "ЖАЛОБА НАБЛЮДАТЕЛЮ",
                "На героя «" + targetName + "». Что случилось? Наблюдатель прочтёт и решит; " +
                "за ложные жалобы тоже спрашивают.",
                true, true, "ОТПРАВИТЬ", "ОТМЕНА",
                text =>
                {
                    string body = "{\"target\":" + DikozemieEconomy.Json(targetId) +
                        ",\"place\":" + DikozemieEconomy.Json(place ?? string.Empty) +
                        ",\"text\":" + DikozemieEconomy.Json(text ?? string.Empty) + "}";
                    DikozemieEconomy.Request(HttpMethod.Post, "report", body, (status, raw, error) =>
                    {
                        DikozemieWatchAnswer answer = DikozemieEconomy.ParseJson<DikozemieWatchAnswer>(raw);
                        Say(status == 200
                            ? answer?.Text ?? "Жалоба передана наблюдателю."
                            : answer?.Message ?? error ?? "Жалоба не ушла.", status == 200);
                    });
                },
                null, false, text => new Tuple<bool, string>(
                    (text ?? string.Empty).Trim().Length >= 3, "Опишите, что случилось.")));
        }

        // ---- the observer -----------------------------------------------------------

        internal static void Refresh(bool force = false)
        {
            if (!IsObserver || _loading || (!force && (DateTime.UtcNow - _loadedUtc).TotalSeconds < 8))
            {
                return;
            }
            _loading = true;
            DikozemieEconomy.Request(HttpMethod.Get, "watch", null, (status, raw, error) =>
            {
                _loading = false;
                _loadedUtc = DateTime.UtcNow;
                DikozemieWatchView view = status == 200 ? DikozemieEconomy.ParseJson<DikozemieWatchView>(raw) : null;
                if (view != null)
                {
                    View = view;
                    LastError = string.Empty;
                }
                else
                {
                    LastError = DikozemieEconomy.ParseJson<DikozemieWatchAnswer>(raw)?.Message ??
                        error ?? "Панель не ответила.";
                }
                Revision++;
            });
        }

        // ---- the observer: one hero's book ---------------------------------------------

        internal static DikozemieWatchBook Book { get; private set; }
        internal static string BookError { get; private set; } = string.Empty;
        private static bool _bookLoading;
        private static string _bookAsked = string.Empty;
        private static DateTime _bookLoadedUtc = DateTime.MinValue;

        internal static void RequestBook(string playerId, bool force = false)
        {
            if (!IsObserver || string.IsNullOrEmpty(playerId) || _bookLoading ||
                (!force && playerId == _bookAsked && (DateTime.UtcNow - _bookLoadedUtc).TotalSeconds < 15))
            {
                return;
            }
            if (playerId != _bookAsked)
            {
                Book = null;
                BookError = string.Empty;
            }
            _bookAsked = playerId;
            _bookLoading = true;
            DikozemieEconomy.Request(HttpMethod.Get, "watch/person?id=" + Uri.EscapeDataString(playerId), null,
                (status, raw, error) =>
                {
                    _bookLoading = false;
                    _bookLoadedUtc = DateTime.UtcNow;
                    if (playerId != _bookAsked)
                    {
                        return;
                    }
                    DikozemieWatchBook book = status == 200 ? DikozemieEconomy.ParseJson<DikozemieWatchBook>(raw) : null;
                    Book = book;
                    BookError = book != null
                        ? string.Empty
                        : DikozemieEconomy.ParseJson<DikozemieWatchAnswer>(raw)?.Message ?? error ?? "Казна не ответила.";
                    Revision++;
                });
        }

        internal static readonly (string action, string title, double hours)[] Measures =
        {
            ("warn", "Предупредить", 0),
            ("kick", "Удалить из боя", 0),
            ("ban", "Заблокировать на сутки", 24),
            ("ban", "Заблокировать на неделю", 24 * 7),
            ("ban", "Заблокировать навсегда", 0),
            ("unban", "Снять блокировку", 0)
        };

        // A measure against a hero: pick it, write the reason, send.
        internal static void AskMeasure(string playerId, string name, Action after = null)
        {
            if (!IsObserver || string.IsNullOrEmpty(playerId))
            {
                return;
            }
            var choices = Measures.Select((m, index) =>
                new InquiryElement(index, m.title, null, true, string.Empty)).ToList();
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "МЕРЫ · " + name, "Что сделать с героем? Игрок увидит причину.", choices, true, 1, 1,
                "ДАЛЕЕ", "ОТМЕНА",
                picked =>
                {
                    if (picked == null || picked.Count == 0)
                    {
                        return;
                    }
                    int index = (int)picked[0].Identifier;
                    (string action, string title, double hours) measure = Measures[index];
                    if (measure.action == "unban")
                    {
                        Act(playerId, measure.action, string.Empty, 0, after);
                        return;
                    }
                    InformationManager.ShowTextInquiry(new TextInquiryData(
                        measure.title.ToUpperInvariant() + " · " + name,
                        "Причина (её увидит игрок и журнал наблюдателя):",
                        true, true, "ГОТОВО", "ОТМЕНА",
                        reason => Act(playerId, measure.action, reason, measure.hours, after),
                        null, false, text => new Tuple<bool, string>(
                            (text ?? string.Empty).Trim().Length >= 3, "Напишите причину.")));
                },
                null), true);
        }

        internal static void Act(string playerId, string action, string reason, double hours, Action after = null)
        {
            string body = "{\"player\":" + DikozemieEconomy.Json(playerId) +
                ",\"action\":" + DikozemieEconomy.Json(action) +
                ",\"reason\":" + DikozemieEconomy.Json(reason ?? string.Empty) +
                ",\"hours\":" + hours.ToString("0.##", CultureInfo.InvariantCulture) + "}";
            DikozemieEconomy.Request(HttpMethod.Post, "watch/act", body, (status, raw, error) =>
            {
                DikozemieWatchAnswer answer = DikozemieEconomy.ParseJson<DikozemieWatchAnswer>(raw);
                Say(status == 200 ? answer?.Text ?? "Сделано." : answer?.Message ?? error ?? "Не вышло.", status == 200);
                Refresh(force: true);
                after?.Invoke();
            });
        }

        internal static void CloseComplaint(int id)
        {
            DikozemieEconomy.Request(HttpMethod.Post, "watch/close",
                "{\"complaint\":" + id.ToString(CultureInfo.InvariantCulture) + "}",
                (status, raw, error) => Refresh(force: true));
        }

        // ---- watching a battle --------------------------------------------------------

        internal static bool StartWatching(int port, bool naval, string title, out string error)
        {
            error = string.Empty;
            if (!IsObserver)
            {
                error = "Наблюдать может только наблюдатель.";
                return false;
            }
            if (!DikozemieHeroProfile.IsCreated)
            {
                // The arena page sends a heroless player to hero creation, and
                // the search would start by itself the next time he came back.
                error = "Сначала создайте героя: наблюдатель входит в бой с его профилем.";
                return false;
            }
            if (port <= 0)
            {
                error = "Этот бой уже кончился.";
                return false;
            }
            if (Mission.Current != null || DikozemiePvpSearchController.IsSearching)
            {
                error = "Сначала выйдите из нынешнего боя.";
                return false;
            }
            var client = NetworkMain.GameClient;
            if (client == null || !client.Connected || !client.AtLobby)
            {
                error = "Сетевое лобби ещё не готово.";
                return false;
            }
            if (naval && !DikozemieLaunchGuard.IsNavalAvailable)
            {
                error = DikozemieLaunchGuard.NavalUnavailableText;
                return false;
            }
            WatchPort = port;
            WatchNaval = naval;
            WatchTitle = title ?? string.Empty;
            DikozemieLobbyShellIntegration.TraceButtonAction("observer watch port=" + WatchPort + " naval=" + naval);
            DikozemieLobbyShellIntegration.StartObserverWatch(
                naval ? DikozemieBattleVenue.Naval : DikozemieBattleVenue.Arena);
            return true;
        }

        internal static void StopWatching()
        {
            if (WatchPort > 0)
            {
                DikozemieLobbyShellIntegration.TraceButtonAction("observer watch ends port=" + WatchPort);
            }
            WatchPort = 0;
            WatchNaval = false;
            WatchTitle = string.Empty;
        }

        internal static string PlaceText(DikozemieWatchPerson person)
        {
            switch (person.Place)
            {
                case "battle": return person.Port > 0 ? "в бою · " + person.Port : "в бою";
                case "town": return "в городе";
                case "walk": return "гуляет по городу";
                case "road": return "на дороге";
                case "menu": return "в меню";
                default: return "на карте";
            }
        }

        internal static string LeftText(double seconds)
        {
            if (seconds < 3600)
            {
                return Math.Max(1, (int)Math.Ceiling(seconds / 60)).ToString(CultureInfo.InvariantCulture) + " мин";
            }
            if (seconds < 86400 * 2)
            {
                return ((int)Math.Ceiling(seconds / 3600)).ToString(CultureInfo.InvariantCulture) + " ч";
            }
            return ((int)Math.Ceiling(seconds / 86400)).ToString(CultureInfo.InvariantCulture) + " дн";
        }

        internal static string AgoText(double at, double serverTime)
        {
            double seconds = Math.Max(0, serverTime - at);
            if (seconds < 90)
            {
                return "только что";
            }
            if (seconds < 3600)
            {
                return ((int)(seconds / 60)).ToString(CultureInfo.InvariantCulture) + " мин";
            }
            if (seconds < 86400)
            {
                return ((int)(seconds / 3600)).ToString(CultureInfo.InvariantCulture) + " ч";
            }
            return ((int)(seconds / 86400)).ToString(CultureInfo.InvariantCulture) + " дн";
        }

        private static void Say(string text, bool good)
        {
            InformationManager.DisplayMessage(new InformationMessage(text,
                good ? new Color(0.62f, 0.86f, 0.62f) : new Color(0.92f, 0.55f, 0.42f)));
        }
    }
}
