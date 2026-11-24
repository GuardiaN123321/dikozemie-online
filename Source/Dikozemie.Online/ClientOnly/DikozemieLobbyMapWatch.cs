using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace Dikozemie.Online
{
    // The observer's screen in the lobby (plan, «Античит»: «панель: кто в
    // игре, сколько заработал за час, отметки античита, жалобы игроков.
    // Кнопки „предупредить“, „выгнать из боя“, „заблокировать“, запись в
    // журнал»). A section of the clan screen with its own button in the line
    // under the header, seen only by observers (TUNING["observers"] of the
    // trade server). The tables ask the trade server (DikozemieWatch); the
    // battle servers carry out what is decided here.
    public sealed partial class DikozemieLobbyMapWidget
    {
        private int _watchRevision = -1;
        private string _watchSelected = string.Empty;

        private bool IsWatchSection =>
            _clanSection == ClanSection.WatchPeople || _clanSection == ClanSection.WatchBattles ||
            _clanSection == ClanSection.WatchComplaints || _clanSection == ClanSection.WatchFlags ||
            _clanSection == ClanSection.WatchSanctions || _clanSection == ClanSection.WatchBook;

        private bool IsWatchSheet =>
            _sheetMode == TradeSheet.WatchPeople || _sheetMode == TradeSheet.WatchBattles ||
            _sheetMode == TradeSheet.WatchComplaints || _sheetMode == TradeSheet.WatchFlags ||
            _sheetMode == TradeSheet.WatchSanctions || _sheetMode == TradeSheet.WatchBook;

        // One hero's book of the treasury (plan, «Античит»: «по книге казны
        // наблюдатель видит, откуда у игрока деньги»).
        private string _watchBookName = string.Empty;

        private void OpenBook(string playerId, string name)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                return;
            }
            _watchSelected = playerId;
            _watchBookName = name ?? playerId;
            ShowClanSection(ClanSection.WatchBook);
            DikozemieWatch.RequestBook(playerId, force: true);
        }

        private bool OpenWatchSection(ClanSection section)
        {
            switch (section)
            {
                case ClanSection.WatchPeople: OpenSheet(TradeSheet.WatchPeople); break;
                case ClanSection.WatchBattles: OpenSheet(TradeSheet.WatchBattles); break;
                case ClanSection.WatchComplaints: OpenSheet(TradeSheet.WatchComplaints); break;
                case ClanSection.WatchFlags: OpenSheet(TradeSheet.WatchFlags); break;
                case ClanSection.WatchSanctions: OpenSheet(TradeSheet.WatchSanctions); break;
                case ClanSection.WatchBook: OpenSheet(TradeSheet.WatchBook); break;
                default: return false;
            }
            DikozemieWatch.IsScreenOpen = true;
            DikozemieWatch.Refresh(force: true);
            return true;
        }

        // ---- the side panel ------------------------------------------------------------

        private void FillWatchPanel(List<string> text)
        {
            DikozemieWatchView view = DikozemieWatch.View;
            _title.Text = "НАБЛЮДЕНИЕ";
            int people = view?.People.Count(p => !p.Observer) ?? 0;
            int fighting = view?.Battles.Sum(b => b.Players.Count(p => !p.Observer)) ?? 0;
            int complaints = view?.Complaints.Count ?? 0;
            int flagsHour = view?.Flags.Count(f => view.ServerTime - f.At < 3600) ?? 0;
            text.Add(view == null
                ? (string.IsNullOrEmpty(DikozemieWatch.LastError) ? "Спрашиваем казну…" : DikozemieWatch.LastError)
                : "В мире " + people + ", в боях " + fighting + ". Жалоб ждёт ответа: " + complaints +
                  ". Отметок античита за час: " + flagsHour + ".");
            text.Add("Наблюдатель входит в бой невидимым, камера свободная. Меры — предупреждение, удаление из боя, " +
                "блокировка — видят игрок и журнал; блокировка закрывает казну и все серверы.");
            if (!string.IsNullOrEmpty(view?.Note))
            {
                text.Add("Внимание: " + view.Note + ".");
            }
            if (_clanSection == ClanSection.WatchBook && !string.IsNullOrEmpty(_watchSelected))
            {
                string id = _watchSelected;
                string name = _watchBookName;
                AddOption("МЕРЫ · " + name, "предупредить, удалить, заблокировать",
                    () => DikozemieWatch.AskMeasure(id, name, () => _sheetRevision = -1));
            }
            AddOption("ЛЮДИ В МИРЕ", people + " · за час, отметки, жалобы",
                () => ShowClanSection(ClanSection.WatchPeople));
            AddOption("БОИ", view == null ? string.Empty : view.Battles.Count(b => b.Players.Any(p => !p.Observer)) + " идут · наблюдать",
                () => ShowClanSection(ClanSection.WatchBattles));
            AddOption("ЖАЛОБЫ", complaints > 0 ? "ждут: " + complaints : "нет новых",
                () => ShowClanSection(ClanSection.WatchComplaints));
            AddOption("ОТМЕТКИ АНТИЧИТА", flagsHour > 0 ? "за час: " + flagsHour : "последние 30",
                () => ShowClanSection(ClanSection.WatchFlags));
            AddOption("МЕРЫ И БЛОКИРОВКИ", view == null ? string.Empty : "заблокированы: " + view.Banned.Count,
                () => ShowClanSection(ClanSection.WatchSanctions));
            AddOption("ОБНОВИТЬ", string.Empty, () =>
            {
                DikozemieWatch.Refresh(force: true);
                RefreshSidePanel();
            });
            AddOption("ЗАКРЫТЬ", string.Empty, CloseClan);
        }

        private void FillWatchTexts()
        {
            DikozemieWatchView view = DikozemieWatch.View;
            _townName.Text = "НАБЛЮДЕНИЕ";
            _townKind.Text = view == null
                ? "ПАНЕЛЬ НАБЛЮДАТЕЛЯ"
                : "В МИРЕ " + view.People.Count(p => !p.Observer) + "  ·  БОЁВ " + view.Battles.Count(b => b.Players.Any(p => !p.Observer)) +
                  "  ·  ЖАЛОБ " + view.Complaints.Count;
            _townOwner.Text = "Дежурный: " + (DikozemieEconomy.State?.Player ?? string.Empty);
            _townLore.Text = "Сервер решает, клиент только просит: монеты, товары и награды считает казна. " +
                "Что игра видит одна — заработок на дорогах, скорость по карте, снаряжение в бою, вещи нового героя — " +
                "казна сверяет и отмечает подозрительное. Жалобы пишут сами игроки из карточки героя на карте.";
        }

        // ---- the tables --------------------------------------------------------------------

        private bool RenderWatchSheet()
        {
            if (!IsWatchSheet)
            {
                return false;
            }
            _watchRevision = DikozemieWatch.Revision;
            DikozemieWatchView view = DikozemieWatch.View;
            _sheetStatus.Text = view == null ? string.Empty : "ОБНОВЛЕНО " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            if (view == null)
            {
                _sheetTitle.Text = "НАБЛЮДЕНИЕ";
                SetColumns(1f);
                SetDetail(string.IsNullOrEmpty(DikozemieWatch.LastError) ? "Спрашиваем казну…" : DikozemieWatch.LastError);
                return true;
            }
            switch (_sheetMode)
            {
                case TradeSheet.WatchPeople: RenderWatchPeople(view); break;
                case TradeSheet.WatchBattles: RenderWatchBattles(view); break;
                case TradeSheet.WatchComplaints: RenderWatchComplaints(view); break;
                case TradeSheet.WatchFlags: RenderWatchFlags(view); break;
                case TradeSheet.WatchSanctions: RenderWatchSanctions(view); break;
                case TradeSheet.WatchBook: RenderWatchBook(); break;
            }
            return true;
        }

        private void RenderWatchBook()
        {
            DikozemieWatchBook book = DikozemieWatch.Book;
            _sheetTitle.Text = "КНИГА КАЗНЫ · " + (book?.Name ?? _watchBookName);
            SetColumns(1f, 1f, 1.1f, 2.2f, 4.2f);
            SetAlign(TextHorizontalAlignment.Left, TextHorizontalAlignment.Right, TextHorizontalAlignment.Right,
                TextHorizontalAlignment.Left, TextHorizontalAlignment.Left);
            SetHeads("КОГДА", "СУММА", "ОСТАТОК", "ЗА ЧТО", "ПОДРОБНО");
            if (book == null || book.Id != _watchSelected)
            {
                SetRow(0, new[] { string.Empty, string.Empty, string.Empty,
                        string.IsNullOrEmpty(DikozemieWatch.BookError) ? "Открываем книгу казны…" : DikozemieWatch.BookError },
                    new[] { default(Color), default(Color), default(Color), SheetDim }, false, null);
                SetDetail("Строка в «ЛЮДИ В МИРЕ» открывает книгу казны героя: откуда его монеты и куда ушли.");
                return;
            }
            int row = 0;
            foreach (DikozemieWatchBookLine line in book.Book.Take(SheetRows))
            {
                SetRow(row++, new[]
                    {
                        DikozemieWatch.AgoText(line.At, book.ServerTime),
                        (line.Amount > 0 ? "+" : string.Empty) + line.Amount.ToString(CultureInfo.InvariantCulture),
                        FormatCoins(line.Balance),
                        string.IsNullOrEmpty(line.Title) ? line.Kind : line.Title,
                        line.Note
                    },
                    new[] { SheetDim, line.Amount < 0 ? SheetBad : default(Color), SheetDim, default(Color), SheetDim },
                    false, null);
            }
            if (row == 0)
            {
                SetRow(0, new[] { string.Empty, string.Empty, string.Empty, "В книге казны пусто." },
                    new[] { default(Color), default(Color), default(Color), SheetDim }, false, null);
            }
            string day = book.Day.Count == 0
                ? "За сутки движения не было."
                : "За сутки: " + string.Join(" · ", book.Day.Take(4).Select(d =>
                    (string.IsNullOrEmpty(d.Title) ? d.Kind : d.Title).ToLowerInvariant() +
                    (d.Earned > 0 ? " +" + d.Earned.ToString(CultureInfo.InvariantCulture) : string.Empty) +
                    (d.Spent > 0 ? " −" + d.Spent.ToString(CultureInfo.InvariantCulture) : string.Empty) +
                    " (" + d.Count.ToString(CultureInfo.InvariantCulture) + ")")) + ".";
            SetDetail(day + "\nМонет " + FormatCoins(book.Coins) + ", вещей " + book.Items.ToString(CultureInfo.InvariantCulture) +
                ". Отметок " + book.Flags.Count.ToString(CultureInfo.InvariantCulture) +
                (book.Flags.Count > 0 ? " (последняя: " + FlagWord(book.Flags[0].Kind) + ")" : string.Empty) +
                ", жалоб " + book.Complaints.Count.ToString(CultureInfo.InvariantCulture) +
                ", мер " + book.Measures.Count.ToString(CultureInfo.InvariantCulture) +
                (book.Banned ? ". ЗАБЛОКИРОВАН." : "."));
        }

        private void RenderWatchPeople(DikozemieWatchView view)
        {
            _sheetTitle.Text = "ЛЮДИ В МИРЕ";
            SetColumns(2.6f, 2.2f, 1.1f, 1.1f, 1f, 1f);
            SetAlign(TextHorizontalAlignment.Left, TextHorizontalAlignment.Left, TextHorizontalAlignment.Right,
                TextHorizontalAlignment.Right, TextHorizontalAlignment.Right, TextHorizontalAlignment.Right);
            SetHeads("ГЕРОЙ", "ГДЕ", "МОНЕТЫ", "ЗА ЧАС", "ОТМЕТКИ", "ЖАЛОБЫ");
            int row = 0;
            foreach (DikozemieWatchPerson person in view.People.Where(p => !p.Observer).Take(SheetRows))
            {
                DikozemieWatchPerson p = person;
                var actions = new List<(string, bool, Action)>();
                if (p.Place == "battle" && p.Port > 0)
                {
                    actions.Add(("НАБЛЮДАТЬ", !DikozemieWatch.IsWatching, () => Watch(p.Port, p.Naval, p.Server)));
                }
                actions.Add(("МЕРЫ", true, () => DikozemieWatch.AskMeasure(p.Id, p.Name, () => _sheetRevision = -1)));
                SetRow(row++, new[]
                    {
                        p.Name + (p.Banned ? " · ЗАБЛОКИРОВАН" : string.Empty),
                        DikozemieWatch.PlaceText(p) + (string.IsNullOrEmpty(p.Node) ? string.Empty : " · " + p.Node),
                        FormatCoins(p.Coins), p.Hour.ToString(CultureInfo.InvariantCulture),
                        p.Flags.ToString(CultureInfo.InvariantCulture), p.Complaints.ToString(CultureInfo.InvariantCulture)
                    },
                    new[]
                    {
                        p.Banned ? SheetBad : default(Color), SheetDim, default(Color), p.Hour > 300 ? SheetWarn : default(Color),
                        p.Flags > 0 ? SheetWarn : SheetDim, p.Complaints > 0 ? SheetBad : SheetDim
                    },
                    _watchSelected == p.Id, () => OpenBook(p.Id, p.Name), actions.ToArray());
            }
            if (row == 0)
            {
                SetRow(0, new[] { "Сейчас в мире никого, кроме наблюдателей." }, new[] { SheetDim }, false, null);
            }
            SetDetail("Сверху — у кого больше отметок и жалоб, потом — кто больше заработал за час. " +
                "Строка — книга казны героя: откуда его монеты." + WatchNote());
        }

        private void RenderWatchBattles(DikozemieWatchView view)
        {
            _sheetTitle.Text = "БОИ";
            SetColumns(3.4f, 0.8f, 1.2f, 4f);
            SetAlign(TextHorizontalAlignment.Left, TextHorizontalAlignment.Right, TextHorizontalAlignment.Right,
                TextHorizontalAlignment.Left);
            SetHeads("СЕРВЕР", "ПОРТ", "ЖИВЫХ", "КТО");
            int row = 0;
            foreach (DikozemieWatchBattle battle in view.Battles.Take(SheetRows))
            {
                DikozemieWatchBattle b = battle;
                List<DikozemieWatchBattlePlayer> fighters = b.Players.Where(p => !p.Observer).ToList();
                bool empty = fighters.Count == 0;
                SetRow(row++, new[]
                    {
                        b.Name, b.Port.ToString(CultureInfo.InvariantCulture),
                        empty ? "—" : fighters.Count(p => p.Alive) + " из " + fighters.Count,
                        empty ? "пусто" : string.Join(", ", fighters.Select(p => p.Name))
                    },
                    new[] { empty ? SheetDim : default(Color), SheetDim, default(Color), empty ? SheetDim : default(Color) },
                    false, null,
                    ("НАБЛЮДАТЬ", !DikozemieWatch.IsWatching, () => Watch(b.Port, b.Naval, b.Name)));
            }
            if (row == 0)
            {
                SetRow(0, new[] { "Серверы боёв молчат: файл присутствия не обновлялся полминуты." }, new[] { SheetDim }, false, null);
            }
            SetDetail("Наблюдать можно и пустой сервер: бой начнётся, когда придут игроки. В бою — свободная камера, " +
                "справа — бойцы и их отметки, F5 — меры к бойцу. Выход — Esc, «Выйти»: наблюдатель вернётся на страницу арены." + WatchNote());
        }

        private void RenderWatchComplaints(DikozemieWatchView view)
        {
            _sheetTitle.Text = "ЖАЛОБЫ";
            SetColumns(0.9f, 1.8f, 1.8f, 5f);
            SetAlign(TextHorizontalAlignment.Left, TextHorizontalAlignment.Left, TextHorizontalAlignment.Left,
                TextHorizontalAlignment.Left);
            SetHeads("КОГДА", "КТО", "НА КОГО", "ЧТО");
            int row = 0;
            foreach (DikozemieWatchComplaint complaint in view.Complaints.Take(SheetRows))
            {
                DikozemieWatchComplaint c = complaint;
                string key = "c" + c.Id.ToString(CultureInfo.InvariantCulture);
                SetRow(row++, new[]
                    {
                        DikozemieWatch.AgoText(c.At, view.ServerTime), c.Reporter, c.TargetName, c.Text
                    },
                    new[] { SheetDim, SheetDim, SheetWarn, default(Color) },
                    _watchSelected == key, () => { _watchSelected = key; _sheetRevision = -1; },
                    ("МЕРЫ", true, () => DikozemieWatch.AskMeasure(c.Target, c.TargetName, () => _sheetRevision = -1)),
                    ("ЗАКРЫТЬ", true, () => DikozemieWatch.CloseComplaint(c.Id)));
            }
            if (row == 0)
            {
                SetRow(0, new[] { string.Empty, string.Empty, string.Empty, "Новых жалоб нет." },
                    new[] { default(Color), default(Color), default(Color), SheetDim }, false, null);
            }
            DikozemieWatchComplaint chosen = view.Complaints.FirstOrDefault(c => "c" + c.Id.ToString(CultureInfo.InvariantCulture) == _watchSelected);
            SetDetail(chosen == null
                ? "Жалобу пишут из карточки героя на карте. «ЗАКРЫТЬ» — разобрано; меры — по кнопке."
                : chosen.Reporter + " → " + chosen.TargetName + (string.IsNullOrEmpty(chosen.Place) ? string.Empty : " (" + chosen.Place + ")") +
                  ":\n«" + chosen.Text + "»");
        }

        private void RenderWatchFlags(DikozemieWatchView view)
        {
            _sheetTitle.Text = "ОТМЕТКИ АНТИЧИТА";
            SetColumns(0.9f, 1.8f, 1.7f, 5.6f);
            SetAlign(TextHorizontalAlignment.Left, TextHorizontalAlignment.Left, TextHorizontalAlignment.Left,
                TextHorizontalAlignment.Left);
            SetHeads("КОГДА", "КТО", "ЧТО", "ПОДРОБНО");
            int row = 0;
            foreach (DikozemieWatchFlag flag in view.Flags.Take(SheetRows))
            {
                DikozemieWatchFlag f = flag;
                SetRow(row++, new[] { DikozemieWatch.AgoText(f.At, view.ServerTime), f.Name, FlagWord(f.Kind), f.Note },
                    new[] { SheetDim, default(Color), SheetWarn, SheetDim },
                    false, null,
                    ("КНИГА", true, () => OpenBook(f.Player, f.Name)),
                    ("МЕРЫ", true, () => DikozemieWatch.AskMeasure(f.Player, f.Name, () => _sheetRevision = -1)));
            }
            if (row == 0)
            {
                SetRow(0, new[] { string.Empty, string.Empty, string.Empty, "Отметок нет." },
                    new[] { default(Color), default(Color), default(Color), SheetDim }, false, null);
            }
            SetDetail("Отметка — не приговор: новый герой, плен и переправа тоже двигают героя по карте. " +
                "Смотрите, повторяется ли, и сверяйте с книгой казны героя («КНИГА»).");
        }

        private void RenderWatchSanctions(DikozemieWatchView view)
        {
            _sheetTitle.Text = "МЕРЫ И БЛОКИРОВКИ";
            SetColumns(0.9f, 1.8f, 1.8f, 4f, 1.4f);
            SetAlign(TextHorizontalAlignment.Left, TextHorizontalAlignment.Left, TextHorizontalAlignment.Left,
                TextHorizontalAlignment.Left, TextHorizontalAlignment.Left);
            SetHeads("КОГДА", "КТО", "ЧТО", "ПРИЧИНА", "КЕМ");
            int row = 0;
            foreach (DikozemieWatchBan ban in view.Banned.Take(4))
            {
                DikozemieWatchBan b = ban;
                SetRow(row++, new[]
                    {
                        "сейчас", b.Name,
                        b.Until > 0 ? "блокировка · ещё " + DikozemieWatch.LeftText(b.Until - view.ServerTime) : "блокировка навсегда",
                        b.Reason, string.Empty
                    },
                    new[] { SheetBad, SheetBad, SheetBad, default(Color), SheetDim },
                    false, null,
                    ("СНЯТЬ", true, () => DikozemieWatch.Act(b.Id, "unban", string.Empty, 0, () => _sheetRevision = -1)));
            }
            foreach (DikozemieWatchSanction sanction in view.Sanctions.Take(SheetRows - row))
            {
                SetRow(row++, new[]
                    {
                        DikozemieWatch.AgoText(sanction.At, view.ServerTime), sanction.Name, sanction.Title, sanction.Reason, sanction.By
                    },
                    new[] { SheetDim, default(Color), sanction.Action == "ban" ? SheetBad : SheetWarn, default(Color), SheetDim },
                    false, null);
            }
            if (row == 0)
            {
                SetRow(0, new[] { string.Empty, string.Empty, string.Empty, "Пока никого не наказывали." },
                    new[] { default(Color), default(Color), default(Color), SheetDim }, false, null);
            }
            SetDetail("Сверху — кто заблокирован сейчас. Блокировка на срок снимается сама." + WatchNote());
        }

        private string WatchNote() => string.IsNullOrEmpty(_sheetNote) ? string.Empty : "\n\n" + _sheetNote;

        private static string FlagWord(string kind)
        {
            switch (kind)
            {
                case "map-jump": return "скачок по карте";
                case "map-speed": return "скорость по карте";
                case "equip-unowned": return "снаряжение не куплено";
                case "starter-items": return "вещи нового героя";
                case "item-grant": return "вещь из ниоткуда";
                case "price": return "цена покупки";
                case "opening": return "старая копилка";
                case "unknown-item-sold": return "продал чужое";
                case "opening-items": return "вещи из старой копилки";
                case "reset-again": return "новый герой снова за сутки";
                default:
                    return kind != null && kind.StartsWith("cap-", StringComparison.Ordinal)
                        ? "заработок сверх предела"
                        : kind ?? string.Empty;
            }
        }


        private void Watch(int port, bool naval, string title)
        {
            if (!DikozemieWatch.StartWatching(port, naval, title, out string error))
            {
                _sheetNote = error;
                _sheetRevision = -1;
                return;
            }
            _sheetNote = "Идём в бой «" + title + "» наблюдателем…";
            _sheetRevision = -1;
        }
    }
}
