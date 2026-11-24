using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace Dikozemie.Online
{
    // The trade yard of a town (stage 3, customer 2026-11-08: «биржа
    // товаров», «караваны сухопутные ... морские», workshops, errands). A
    // ledger board laid over the town picture: one table, the side menu
    // picks what it shows. Everything here asks the trade server
    // (DikozemieEconomy) and draws its answer; nothing is decided locally.
    public sealed partial class DikozemieLobbyMapWidget
    {
        private enum TradeSheet
        {
            None,
            Exchange,
            Contracts,
            Workshops,
            WorkshopPick,
            Caravans,
            Ledger,
            Letter,
            Commenda,
            // Stage 6: the clan's tables, a castle of one's own, a tavern's men.
            ClanMembers,
            ClanTreasury,
            ClanLands,
            ClanWarband,
            ClanRelations,
            ClanList,
            ClanChronicle,
            Mercs,
            Garrison,
            LandRates,
            // The observer's tables (DikozemieLobbyMapWatch).
            WatchPeople,
            WatchBattles,
            WatchComplaints,
            WatchFlags,
            WatchSanctions,
            WatchBook
        }

        private const int SheetRows = 12;
        private const int SheetCells = 7;
        private const int SheetActions = 2;
        private const int SheetTabs = 4;
        private const float SheetPad = 34f;
        private const float RowHeight = 40f;
        private const float ActionWidth = 128f;

        private sealed class SheetRow
        {
            internal Widget Band;
            internal ButtonWidget Select;
            internal readonly TextWidget[] Cells = new TextWidget[SheetCells];
            internal readonly ButtonWidget[] Actions = new ButtonWidget[SheetActions];
            internal readonly Widget[] ActionFills = new Widget[SheetActions];
            internal readonly Widget[] ActionFrames = new Widget[SheetActions];
            internal readonly TextWidget[] ActionLabels = new TextWidget[SheetActions];
            internal readonly Action[] Handlers = new Action[SheetActions];
            internal readonly bool[] Enabled = new bool[SheetActions];
            internal Action OnSelect;
            internal bool Selected;
            internal bool Shown;
        }

        private sealed class SheetTab
        {
            internal ButtonWidget Button;
            internal Widget Fill;
            internal Widget Frame;
            internal TextWidget Label;
            internal Action Handler;
            internal bool Active;
        }

        private Widget _sheet;
        private readonly Widget[] _sheetFrame = new Widget[4];
        private TextWidget _sheetTitle;
        private TextWidget _sheetStatus;
        private TextWidget _sheetDetail;
        private TextWidget _sheetLetter;
        private Widget _sheetHeadLine;
        private Widget _sheetDetailLine;
        private readonly TextWidget[] _sheetHeads = new TextWidget[SheetCells];
        private readonly SheetRow[] _sheetRows = new SheetRow[SheetRows];
        private readonly SheetTab[] _sheetTabs = new SheetTab[SheetTabs];
        private float[] _colX = new float[SheetCells];
        private float[] _colW = new float[SheetCells];
        private float _sheetWidth;
        private float _sheetHeight;

        private TradeSheet _sheetMode;
        private string _exchangeGroup = "food";
        private string _selectedGood;
        private string _selectedCaravan;
        private string _selectedWorkshop;
        private int _pickSlot = -1;
        private string _sheetNote = string.Empty;
        private int _sheetRevision = -1;
        private bool _sheetBusy;
        private DateTime _marketAskedUtc = DateTime.MinValue;
        private bool _marketLoading;
        private string _marketError = string.Empty;
        private List<DikozemieContractInfo> _offersHere;
        private string _offersFor;

        private static readonly Color SheetInk = new Color(0.91f, 0.86f, 0.75f, 1f);
        private static readonly Color SheetDim = new Color(0.66f, 0.57f, 0.42f, 1f);
        private static readonly Color SheetGold = new Color(0.96f, 0.78f, 0.38f, 1f);
        private static readonly Color SheetGood = new Color(0.58f, 0.82f, 0.52f, 1f);
        private static readonly Color SheetBad = new Color(0.92f, 0.47f, 0.36f, 1f);
        private static readonly Color SheetWarn = new Color(0.96f, 0.70f, 0.32f, 1f);
        private static readonly Color BandEven = new Color(1f, 0.9f, 0.7f, 0.035f);
        private static readonly Color BandOdd = new Color(0f, 0f, 0f, 0f);
        private static readonly Color BandSelected = new Color(0.85f, 0.58f, 0.18f, 0.20f);
        private static readonly Color BandHover = new Color(1f, 0.85f, 0.55f, 0.08f);

        private bool TradeSheetOpen => _sheetMode != TradeSheet.None;

        // ---- building ---------------------------------------------------------

        private void BuildTradeLayer()
        {
            _sheet = new Widget(Context)
            {
                Id = "TradeSheet",
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                ClipContents = true,
                IsVisible = false
            };
            _townView.AddChild(_sheet);
            Widget fill = CreateSolid("TradeSheetFill", new Color(0.055f, 0.040f, 0.026f, 1f));
            _sheet.AddChild(fill);
            for (int index = 0; index < _sheetFrame.Length; index++)
            {
                _sheetFrame[index] = CreateSolid("TradeSheetFrame" + index, new Color(0.72f, 0.47f, 0.17f, 0.75f));
                PlaceFixed(_sheetFrame[index]);
                _sheet.AddChild(_sheetFrame[index]);
            }

            _sheetTitle = SheetText("TradeSheetTitle", 34, SheetGold, TextHorizontalAlignment.Left);
            _sheetStatus = SheetText("TradeSheetStatus", 20, SheetInk, TextHorizontalAlignment.Right);

            for (int index = 0; index < SheetTabs; index++)
            {
                var tab = new SheetTab();
                int captured = index;
                tab.Button = new ButtonWidget(Context)
                {
                    Id = "TradeTab" + index,
                    WidthSizePolicy = SizePolicy.Fixed,
                    HeightSizePolicy = SizePolicy.Fixed,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    DoNotPassEventsToChildren = true,
                    IsVisible = false
                };
                tab.Frame = CreateSolid("TradeTabFrame" + index, new Color(0.72f, 0.47f, 0.17f, 0.8f));
                tab.Button.AddChild(tab.Frame);
                tab.Fill = CreateSolid("TradeTabFill" + index, OptionFill);
                tab.Fill.MarginLeft = tab.Fill.MarginRight = tab.Fill.MarginTop = tab.Fill.MarginBottom = 2f;
                tab.Button.AddChild(tab.Fill);
                tab.Label = CreateText("TradeTabText" + index, string.Empty, 19, SheetGold);
                tab.Label.WidthSizePolicy = SizePolicy.StretchToParent;
                tab.Label.HeightSizePolicy = SizePolicy.StretchToParent;
                tab.Button.AddChild(tab.Label);
                tab.Button.ClickEventHandlers.Add(_ => _sheetTabs[captured].Handler?.Invoke());
                _sheet.AddChild(tab.Button);
                _sheetTabs[index] = tab;
            }

            for (int cell = 0; cell < SheetCells; cell++)
            {
                _sheetHeads[cell] = SheetText("TradeHead" + cell, 17, SheetDim, TextHorizontalAlignment.Left);
            }
            _sheetHeadLine = CreateSolid("TradeHeadLine", new Color(0.72f, 0.47f, 0.17f, 0.55f));
            PlaceFixed(_sheetHeadLine);
            _sheet.AddChild(_sheetHeadLine);

            for (int index = 0; index < SheetRows; index++)
            {
                _sheetRows[index] = BuildSheetRow(index);
            }

            _sheetDetailLine = CreateSolid("TradeDetailLine", new Color(0.72f, 0.47f, 0.17f, 0.4f));
            PlaceFixed(_sheetDetailLine);
            _sheet.AddChild(_sheetDetailLine);
            _sheetDetail = SheetText("TradeDetail", 19, SheetInk, TextHorizontalAlignment.Left);
            _sheetDetail.Brush.TextVerticalAlignment = TextVerticalAlignment.Top;
            _sheetLetter = SheetText("TradeLetter", 24, SheetInk, TextHorizontalAlignment.Left);
            _sheetLetter.Brush.TextVerticalAlignment = TextVerticalAlignment.Top;
        }

        private SheetRow BuildSheetRow(int index)
        {
            var row = new SheetRow();
            row.Band = CreateSolid("TradeRowBand" + index, BandOdd);
            PlaceFixed(row.Band);
            _sheet.AddChild(row.Band);
            row.Select = new ButtonWidget(Context)
            {
                Id = "TradeRowSelect" + index,
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                DoNotPassEventsToChildren = true,
                IsVisible = false
            };
            int capturedRow = index;
            row.Select.ClickEventHandlers.Add(_ => _sheetRows[capturedRow].OnSelect?.Invoke());
            _sheet.AddChild(row.Select);
            for (int cell = 0; cell < SheetCells; cell++)
            {
                row.Cells[cell] = SheetText("TradeCell" + index + "_" + cell, 20, SheetInk, TextHorizontalAlignment.Left);
            }
            for (int action = 0; action < SheetActions; action++)
            {
                int capturedAction = action;
                var button = new ButtonWidget(Context)
                {
                    Id = "TradeAction" + index + "_" + action,
                    WidthSizePolicy = SizePolicy.Fixed,
                    HeightSizePolicy = SizePolicy.Fixed,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    SuggestedWidth = ActionWidth,
                    SuggestedHeight = RowHeight - 10f,
                    DoNotPassEventsToChildren = true,
                    IsVisible = false
                };
                Widget frame = CreateSolid("TradeActionFrame" + index + "_" + action,
                    new Color(0.72f, 0.47f, 0.17f, 0.9f));
                button.AddChild(frame);
                Widget fill = CreateSolid("TradeActionFill" + index + "_" + action, OptionFill);
                fill.MarginLeft = fill.MarginRight = fill.MarginTop = fill.MarginBottom = 2f;
                button.AddChild(fill);
                TextWidget label = CreateText("TradeActionText" + index + "_" + action, string.Empty, 17, SheetGold);
                label.WidthSizePolicy = SizePolicy.StretchToParent;
                label.HeightSizePolicy = SizePolicy.StretchToParent;
                button.AddChild(label);
                button.ClickEventHandlers.Add(_ =>
                {
                    SheetRow target = _sheetRows[capturedRow];
                    if (target.Enabled[capturedAction])
                    {
                        target.Handlers[capturedAction]?.Invoke();
                    }
                });
                _sheet.AddChild(button);
                row.Actions[action] = button;
                row.ActionFills[action] = fill;
                row.ActionFrames[action] = frame;
                row.ActionLabels[action] = label;
            }
            return row;
        }

        private TextWidget SheetText(string id, int size, Color color, TextHorizontalAlignment align)
        {
            TextWidget text = CreateText(id, string.Empty, size, color);
            text.WidthSizePolicy = SizePolicy.Fixed;
            text.HeightSizePolicy = SizePolicy.Fixed;
            text.HorizontalAlignment = HorizontalAlignment.Left;
            text.VerticalAlignment = VerticalAlignment.Top;
            text.Brush.TextHorizontalAlignment = align;
            _sheet.AddChild(text);
            return text;
        }

        private static void PlaceFixed(Widget widget)
        {
            widget.WidthSizePolicy = SizePolicy.Fixed;
            widget.HeightSizePolicy = SizePolicy.Fixed;
            widget.HorizontalAlignment = HorizontalAlignment.Left;
            widget.VerticalAlignment = VerticalAlignment.Top;
        }

        // ---- layout -----------------------------------------------------------

        private void LayoutTradeSheet(float width, float height)
        {
            if (_sheet == null)
            {
                return;
            }
            _sheet.IsVisible = LayerOpen && TradeSheetOpen;
            // The town's name and story stay under the board, unseen.
            bool picture = !_sheet.IsVisible;
            _townName.IsVisible = picture;
            _townKind.IsVisible = picture;
            _townOwner.IsVisible = picture;
            _townLore.IsVisible = picture;
            if (!_sheet.IsVisible)
            {
                return;
            }
            const float margin = 18f;
            float w = Math.Max(600f, width - margin * 2f);
            float h = Math.Max(400f, height - margin * 2f);
            _sheet.PositionXOffset = margin;
            _sheet.PositionYOffset = margin;
            _sheet.SuggestedWidth = w;
            _sheet.SuggestedHeight = h;
            if (Math.Abs(w - _sheetWidth) > 0.5f || Math.Abs(h - _sheetHeight) > 0.5f)
            {
                _sheetWidth = w;
                _sheetHeight = h;
                _sheetRevision = -1;
            }
            const float line = 2f;
            SetFrame(_sheetFrame[0], 0f, 0f, w, line);
            SetFrame(_sheetFrame[1], 0f, h - line, w, line);
            SetFrame(_sheetFrame[2], 0f, 0f, line, h);
            SetFrame(_sheetFrame[3], w - line, 0f, line, h);
            SetText(_sheetTitle, SheetPad, 20f, w * 0.55f, 48f);
            SetText(_sheetStatus, w * 0.45f, 26f, w * 0.55f - SheetPad, 40f);
        }

        // Column widths from weights, after the action buttons on the right.
        private void SetColumns(params float[] weights)
        {
            float inner = _sheetWidth - SheetPad * 2f - (ActionWidth + 10f) * SheetActions;
            float total = weights.Sum();
            float x = SheetPad;
            for (int cell = 0; cell < SheetCells; cell++)
            {
                float share = cell < weights.Length ? weights[cell] / total * inner : 0f;
                _colX[cell] = x;
                _colW[cell] = share;
                x += share;
            }
        }

        private float RowsTop => _sheetTabs.Any(t => t.Button.IsVisible) ? 168f : 124f;

        private float DetailTop => RowsTop + RowHeight * SheetRows + 14f;

        // ---- the table ----------------------------------------------------------

        private void ClearSheet()
        {
            for (int index = 0; index < SheetRows; index++)
            {
                SheetRow row = _sheetRows[index];
                row.Shown = false;
                row.Selected = false;
                row.OnSelect = null;
                row.Band.IsVisible = false;
                row.Select.IsVisible = false;
                foreach (TextWidget cell in row.Cells)
                {
                    cell.IsVisible = false;
                }
                for (int action = 0; action < SheetActions; action++)
                {
                    row.Actions[action].IsVisible = false;
                    row.Handlers[action] = null;
                }
            }
            foreach (TextWidget head in _sheetHeads)
            {
                head.IsVisible = false;
            }
            foreach (SheetTab tab in _sheetTabs)
            {
                tab.Button.IsVisible = false;
                tab.Handler = null;
            }
            _sheetHeadLine.IsVisible = false;
            _sheetLetter.IsVisible = false;
            _sheetDetail.IsVisible = true;
            _sheetDetailLine.IsVisible = true;
        }

        private void SetTabs(params (string label, bool active, Action handler)[] tabs)
        {
            float x = SheetPad;
            for (int index = 0; index < SheetTabs; index++)
            {
                SheetTab tab = _sheetTabs[index];
                if (index >= tabs.Length)
                {
                    tab.Button.IsVisible = false;
                    continue;
                }
                tab.Button.IsVisible = true;
                tab.Label.Text = tabs[index].label;
                tab.Active = tabs[index].active;
                tab.Handler = tabs[index].handler;
                tab.Button.SuggestedWidth = 200f;
                tab.Button.SuggestedHeight = 40f;
                tab.Button.PositionXOffset = x;
                tab.Button.PositionYOffset = 82f;
                tab.Label.Brush.FontColor = tab.Active ? new Color(1f, 0.92f, 0.70f, 1f) : SheetGold;
                tab.Frame.Color = tab.Active ? new Color(0.98f, 0.76f, 0.34f, 1f) : new Color(0.72f, 0.47f, 0.17f, 0.8f);
                x += 212f;
            }
        }

        private void SetHeads(params string[] heads)
        {
            float top = RowsTop - 34f;
            for (int cell = 0; cell < SheetCells; cell++)
            {
                TextWidget head = _sheetHeads[cell];
                head.IsVisible = cell < heads.Length && !string.IsNullOrEmpty(heads[cell]) && _colW[cell] > 0f;
                if (!head.IsVisible)
                {
                    continue;
                }
                head.Text = heads[cell];
                SetText(head, _colX[cell] + 6f, top, _colW[cell] - 12f, 30f);
                head.Brush.TextHorizontalAlignment = AlignOf(cell);
            }
            _sheetHeadLine.IsVisible = true;
            SetFrame(_sheetHeadLine, SheetPad, RowsTop - 4f, _sheetWidth - SheetPad * 2f, 1.5f);
        }

        private TextHorizontalAlignment[] _align = new TextHorizontalAlignment[SheetCells];

        private void SetAlign(params TextHorizontalAlignment[] aligns)
        {
            for (int cell = 0; cell < SheetCells; cell++)
            {
                _align[cell] = cell < aligns.Length ? aligns[cell] : TextHorizontalAlignment.Left;
            }
        }

        private TextHorizontalAlignment AlignOf(int cell) => _align[cell];

        private void SetRow(int index, string[] cells, Color[] colors, bool selected, Action onSelect,
            params (string label, bool enabled, Action handler)[] actions)
        {
            if (index < 0 || index >= SheetRows)
            {
                return;
            }
            SheetRow row = _sheetRows[index];
            float top = RowsTop + index * RowHeight;
            row.Shown = true;
            row.Selected = selected;
            row.OnSelect = onSelect;
            row.Band.IsVisible = true;
            row.Band.Color = selected ? BandSelected : index % 2 == 0 ? BandEven : BandOdd;
            SetFrame(row.Band, SheetPad, top, _sheetWidth - SheetPad * 2f, RowHeight - 2f);
            row.Select.IsVisible = onSelect != null;
            SetFrame(row.Select, SheetPad, top, _colX[SheetCells - 1] + _colW[SheetCells - 1] - SheetPad, RowHeight - 2f);
            for (int cell = 0; cell < SheetCells; cell++)
            {
                TextWidget text = row.Cells[cell];
                bool show = cell < cells.Length && !string.IsNullOrEmpty(cells[cell]) && _colW[cell] > 0f;
                text.IsVisible = show;
                if (!show)
                {
                    continue;
                }
                text.Text = cells[cell];
                text.Brush.FontColor = colors != null && cell < colors.Length && colors[cell].Alpha > 0f
                    ? colors[cell]
                    : SheetInk;
                text.Brush.TextHorizontalAlignment = AlignOf(cell);
                SetText(text, _colX[cell] + 6f, top + 2f, _colW[cell] - 12f, RowHeight - 6f);
            }
            float right = _sheetWidth - SheetPad - ActionWidth;
            for (int action = SheetActions - 1; action >= 0; action--)
            {
                ButtonWidget button = row.Actions[action];
                int from = action - (SheetActions - actions.Length);
                bool show = from >= 0 && from < actions.Length && !string.IsNullOrEmpty(actions[from].label);
                button.IsVisible = show;
                if (!show)
                {
                    row.Handlers[action] = null;
                    continue;
                }
                row.ActionLabels[action].Text = actions[from].label;
                row.Enabled[action] = actions[from].enabled && !_sheetBusy;
                row.Handlers[action] = actions[from].handler;
                row.ActionLabels[action].Brush.FontColor = row.Enabled[action]
                    ? SheetGold
                    : new Color(0.50f, 0.42f, 0.30f, 1f);
                row.ActionFrames[action].Color = row.Enabled[action]
                    ? new Color(0.72f, 0.47f, 0.17f, 0.9f)
                    : new Color(0.40f, 0.30f, 0.18f, 0.6f);
                button.PositionXOffset = right - (SheetActions - 1 - action) * (ActionWidth + 10f);
                button.PositionYOffset = top + 4f;
            }
        }

        private void SetDetail(string text)
        {
            _sheetDetail.Text = text ?? string.Empty;
            float top = DetailTop;
            SetFrame(_sheetDetailLine, SheetPad, top - 8f, _sheetWidth - SheetPad * 2f, 1.5f);
            SetText(_sheetDetail, SheetPad + 4f, top, _sheetWidth - SheetPad * 2f - 8f,
                Math.Max(60f, _sheetHeight - top - 18f));
        }

        private void TickTradeSheet()
        {
            if (_sheet == null || !_sheet.IsVisible)
            {
                return;
            }
            // Hover, as built-in buttons do not light up by themselves.
            foreach (SheetRow row in _sheetRows)
            {
                if (!row.Shown)
                {
                    continue;
                }
                bool hover = row.Select.IsVisible && row.Select.IsHovered;
                row.Band.Color = row.Selected ? BandSelected : hover ? BandHover :
                    Array.IndexOf(_sheetRows, row) % 2 == 0 ? BandEven : BandOdd;
                for (int action = 0; action < SheetActions; action++)
                {
                    if (row.Actions[action].IsVisible)
                    {
                        row.ActionFills[action].Color = row.Enabled[action] && row.Actions[action].IsHovered
                            ? OptionFillHover
                            : OptionFill;
                    }
                }
            }
            foreach (SheetTab tab in _sheetTabs)
            {
                if (tab.Button.IsVisible)
                {
                    tab.Fill.Color = tab.Active
                        ? new Color(0.42f, 0.25f, 0.07f, 1f)
                        : tab.Button.IsHovered ? OptionFillHover : OptionFill;
                }
            }
            // The world moves on the server: the yard refreshes itself.
            if (_sheetMode == TradeSheet.Exchange && !_marketLoading &&
                (DateTime.UtcNow - _marketAskedUtc).TotalSeconds > 20)
            {
                LoadMarket();
            }
            if (IsWatchSheet)
            {
                // The panel asks the trade server every few seconds by itself.
                DikozemieWatch.Refresh();
                if (_sheetMode == TradeSheet.WatchBook)
                {
                    DikozemieWatch.RequestBook(_watchSelected);
                }
            }
            if (DikozemieEconomy.Revision != _sheetRevision || DikozemieClans.Revision != _clanRevision ||
                (IsWatchSheet && DikozemieWatch.Revision != _watchRevision))
            {
                RenderSheet();
                if (_clanOpen || _townSection == TownSection.Mercs || _townSection == TownSection.Holding)
                {
                    RefreshSidePanel();
                }
            }
        }

        // ---- opening ----------------------------------------------------------

        private void OpenSheet(TradeSheet mode, string note = "")
        {
            _sheetMode = mode;
            _sheetNote = note ?? string.Empty;
            _sheetRevision = -1;
            if (mode == TradeSheet.Exchange || mode == TradeSheet.Workshops || mode == TradeSheet.WorkshopPick ||
                mode == TradeSheet.Contracts)
            {
                LoadMarket();
            }
            if (mode == TradeSheet.Contracts && _townNode != null && !DikozemieTown.IsTown(_townNode))
            {
                LoadOffers();
            }
            DikozemieEconomy.RequestRefresh();
            DikozemieLobbyShellIntegration.TraceButtonAction("trade sheet " + mode + " node=" + _townNode?.Id);
            RefreshSidePanel();
        }

        private void CloseSheet()
        {
            _sheetMode = TradeSheet.None;
            _sheetNote = string.Empty;
        }

        private void LoadMarket()
        {
            DikozemieLobbyMapNode node = _townNode;
            if (node == null || !DikozemieTown.IsTown(node))
            {
                return;
            }
            _marketLoading = true;
            _marketAskedUtc = DateTime.UtcNow;
            DikozemieEconomy.Market(node.Id, (market, error) =>
            {
                _marketLoading = false;
                _marketError = market == null ? error ?? string.Empty : string.Empty;
                _sheetRevision = -1;
            });
        }

        private void LoadOffers()
        {
            DikozemieLobbyMapNode node = _townNode;
            if (node == null)
            {
                return;
            }
            _offersFor = node.Id;
            DikozemieEconomy.Offers(node.Id, (offers, error) =>
            {
                _offersHere = offers ?? new List<DikozemieContractInfo>();
                if (offers == null)
                {
                    _marketError = error ?? string.Empty;
                }
                _sheetRevision = -1;
            });
        }

        private void Act(string path, string body, string busyNote)
        {
            if (_sheetBusy)
            {
                return;
            }
            _sheetBusy = true;
            _sheetNote = busyNote;
            _sheetRevision = -1;
            DikozemieEconomy.Act(path, body, (state, error) =>
            {
                _sheetBusy = false;
                _sheetNote = error ?? NoteFor(path, state);
                _sheetRevision = -1;
                if (state != null && _sheetMode == TradeSheet.Contracts && _townNode != null &&
                    !DikozemieTown.IsTown(_townNode))
                {
                    LoadOffers();
                }
                if (state != null && (path.StartsWith("workshop", StringComparison.Ordinal) ||
                                      path.StartsWith("contract", StringComparison.Ordinal)))
                {
                    LoadMarket();
                }
                RefreshSidePanel();
            });
        }

        private static string NoteFor(string path, DikozemieEconomyState state)
        {
            if (state == null)
            {
                return string.Empty;
            }
            switch (path)
            {
                case "workshop/buy": return "Мастерская ваша. Доход придёт в казну с первым расчётом.";
                case "workshop/sell": return "Мастерская продана горожанину.";
                case "caravan/hire": return "Караван снаряжён. Караванщик сам выберет, что и куда везти.";
                case "caravan/disband": return "Караван распущен: кошель и лошади проданы, деньги в казне.";
                case "commenda": return "Вклад записан. Корабль вернётся через полтора–два с половиной часа.";
                case "contract/take": return "Поручение взято. Срок — в списке поручений.";
                case "contract/deliver": return "Поручение исполнено, деньги в казне.";
                case "contract/drop": return "Вы отказались от поручения.";
                default: return string.Empty;
            }
        }

        // ---- rendering --------------------------------------------------------

        private void RenderSheet()
        {
            if (_sheet == null || _sheetWidth <= 0f)
            {
                return;
            }
            _sheetRevision = DikozemieEconomy.Revision;
            _clanRevision = DikozemieClans.Revision;
            ClearSheet();
            DikozemieEconomyState state = DikozemieEconomy.State;
            _sheetStatus.Text = StatusLine(state);
            if (!DikozemieEconomy.IsOnline)
            {
                _sheetTitle.Text = "КАЗНА";
                SetColumns(1f);
                SetDetail(DikozemieEconomy.StatusText +
                    (string.IsNullOrEmpty(DikozemieEconomy.LastError) ? string.Empty : "\n\n" + DikozemieEconomy.LastError));
                return;
            }
            if (RenderClanSheet())
            {
                return;
            }
            switch (_sheetMode)
            {
                case TradeSheet.Exchange: RenderExchange(state); break;
                case TradeSheet.Contracts: RenderContracts(state); break;
                case TradeSheet.Workshops: RenderWorkshops(state); break;
                case TradeSheet.WorkshopPick: RenderWorkshopPick(state); break;
                case TradeSheet.Caravans: RenderCaravans(state); break;
                case TradeSheet.Ledger: RenderLedger(state); break;
                case TradeSheet.Letter: RenderLetter(state); break;
                case TradeSheet.Commenda: RenderCommenda(state); break;
            }
        }

        private static string StatusLine(DikozemieEconomyState state)
        {
            if (state == null)
            {
                return "МОНЕТЫ: " + FormatCoins(DikozemieArmoryProgression.HeroResources);
            }
            return "ОБОЗ " + state.Load + " ИЗ " + state.Capacity + "   ·   МОНЕТЫ: " +
                FormatCoins(DikozemieArmoryProgression.HeroResources);
        }

        // ---- exchange -----------------------------------------------------------

        private void RenderExchange(DikozemieEconomyState state)
        {
            DikozemieLobbyMapNode node = _townNode;
            _sheetTitle.Text = "ТОРГОВЫЙ ДВОР · " + DikozemieTown.DisplayName(node).ToUpperInvariant();
            string[] groups = { "food", "raw", "wares", "rare" };
            string[] names = { "ЕДА", "СЫРЬЁ", "ИЗДЕЛИЯ", "РЕДКОЕ" };
            SetTabs(Enumerable.Range(0, 4).Select(i =>
            {
                string group = groups[i];
                return (names[i], _exchangeGroup == group, (Action)(() =>
                {
                    _exchangeGroup = group;
                    _selectedGood = null;
                    _sheetRevision = -1;
                }));
            }).ToArray());
            SetColumns(2.3f, 1.9f, 0.95f, 0.95f, 0.9f, 2.4f, 0.95f);
            SetAlign(TextHorizontalAlignment.Left, TextHorizontalAlignment.Left, TextHorizontalAlignment.Right,
                TextHorizontalAlignment.Right, TextHorizontalAlignment.Right, TextHorizontalAlignment.Left,
                TextHorizontalAlignment.Right);
            SetHeads("ТОВАР", "НА ДВОРЕ", "КУПИТЬ", "ПРОДАТЬ", "ОБЫЧНО", "ГДЕ ДОРОЖЕ (ВЫ ВИДЕЛИ)", "В ОБОЗЕ");
            DikozemieMarketInfo market = DikozemieEconomy.CachedMarket(node?.Id);
            if (market == null)
            {
                SetDetail(_marketLoading || string.IsNullOrEmpty(_marketError)
                    ? "Приказчик раскладывает товар…"
                    : _marketError);
                return;
            }
            List<DikozemieGoodRow> rows = market.Goods
                .Where(g => DikozemieEconomy.Good(g.Good)?.Group == _exchangeGroup)
                .ToList();
            int room = Math.Max(0, state.Capacity - state.Load);
            int coins = DikozemieArmoryProgression.HeroResources;
            int index = 0;
            foreach (DikozemieGoodRow row in rows.Take(SheetRows))
            {
                DikozemieEconomyGood good = DikozemieEconomy.Good(row.Good);
                state.Cargo.TryGetValue(row.Good, out int carried);
                string good_id = row.Good;
                (string stockText, Color stockColor) = StockWords(row);
                (string dearer, Color dearerColor) = BestElsewhere(row, market.Town);
                bool canBuy = row.Buy > 0 && row.Stock >= 1 && room > 0 && coins >= row.Buy;
                bool canSell = carried > 0;
                SetRow(index++,
                    new[]
                    {
                        good?.Name ?? row.Good,
                        stockText,
                        row.Buy > 0 && row.Stock >= 1 ? row.Buy.ToString(CultureInfo.InvariantCulture) : "нет",
                        row.Sell.ToString(CultureInfo.InvariantCulture),
                        row.Base.ToString(CultureInfo.InvariantCulture),
                        dearer,
                        carried > 0 ? carried.ToString(CultureInfo.InvariantCulture) : string.Empty
                    },
                    new[]
                    {
                        _selectedGood == good_id ? SheetGold : SheetInk, stockColor,
                        row.Buy > row.Base * 1.25 ? SheetBad : row.Buy < row.Base * 0.85 ? SheetGood : SheetInk,
                        row.Sell > row.Base * 1.15 ? SheetGood : SheetInk,
                        SheetDim, dearerColor, SheetGold
                    },
                    _selectedGood == good_id,
                    () => { _selectedGood = good_id; _sheetRevision = -1; },
                    ("КУПИТЬ", canBuy, () => TradeClick(good_id, 1)),
                    ("ПРОДАТЬ", canSell, () => TradeClick(good_id, -1)));
            }
            SetDetail(ExchangeDetail(state, market));
        }

        private void TradeClick(string good, int sign)
        {
            int qty = Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.RightShift) ? 5 : 1;
            if (Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl))
            {
                qty = 50;
            }
            _selectedGood = good;
            DikozemieLobbyMapNode node = _townNode;
            if (node == null || _sheetBusy)
            {
                return;
            }
            _sheetBusy = true;
            _sheetNote = "Приказчик отсчитывает…";
            _sheetRevision = -1;
            DikozemieEconomy.Trade(node.Id, good, qty * sign, (state, error) =>
            {
                _sheetBusy = false;
                if (state == null)
                {
                    _sheetNote = error ?? string.Empty;
                }
                else if (state.Bought > 0)
                {
                    _sheetNote = "Куплено: " + DikozemieEconomy.GoodName(good).ToLowerInvariant() + " × " +
                        state.Bought + " за " + state.Paid + ".";
                }
                else if (state.Sold > 0)
                {
                    int profit = (int)Math.Round(state.Profit);
                    _sheetNote = "Продано: " + DikozemieEconomy.GoodName(good).ToLowerInvariant() + " × " +
                        state.Sold + " за " + state.Got + (profit != 0
                            ? profit > 0 ? ", прибыль " + profit + "." : ", убыток " + (-profit) + "."
                            : ".");
                }
                _sheetRevision = -1;
                RefreshSidePanel();
            });
        }

        private static (string, Color) StockWords(DikozemieGoodRow row)
        {
            if (row.Stock < 1)
            {
                return ("пусто", SheetBad);
            }
            string count = " · " + row.Stock;
            if (row.Stock <= 2)
            {
                // Few people buy it here, so a couple is enough for them,
                // but a trader cannot load up.
                return ("почти нет" + count, SheetWarn);
            }
            if (row.Level < 0.35)
            {
                return ("мало" + count, SheetBad);
            }
            if (row.Level < 0.8)
            {
                return ("меньше нужного" + count, SheetWarn);
            }
            if (row.Level < 1.7)
            {
                return ("в достатке" + count, SheetInk);
            }
            return ("завалено" + count, SheetGood);
        }

        // The dearest town for this good among those the hero has seen.
        private static (string, Color) BestElsewhere(DikozemieGoodRow row, string here)
        {
            KeyValuePair<string, DikozemieKnownPrice> best = DikozemieEconomy.Known(row.Good)
                .Where(k => k.Key != here)
                .OrderByDescending(k => k.Value.Sell)
                .FirstOrDefault();
            if (best.Key == null)
            {
                return ("—", SheetDim);
            }
            string town = DikozemieEconomy.Town(best.Key)?.Name ?? best.Key;
            return (town + " " + best.Value.Sell + " · " + Ago(best.Value.At),
                best.Value.Sell > row.Buy && row.Buy > 0 ? SheetGood : SheetDim);
        }

        private static string Ago(double at)
        {
            double seconds = Math.Max(0, DikozemieEconomy.ServerNow - at);
            if (seconds < 90)
            {
                return "только что";
            }
            if (seconds < 3600)
            {
                return (int)(seconds / 60) + " мин";
            }
            if (seconds < 86400)
            {
                return (int)(seconds / 3600) + " ч";
            }
            return (int)(seconds / 86400) + " дн";
        }

        private string ExchangeDetail(DikozemieEconomyState state, DikozemieMarketInfo market)
        {
            var text = new List<string>();
            if (!string.IsNullOrEmpty(_sheetNote))
            {
                text.Add(_sheetNote);
            }
            DikozemieGoodRow row = market.Goods.FirstOrDefault(g => g.Good == _selectedGood);
            // Stage 6: whose market this is: traders pick the cheaper lands.
            text.Add("Пошлина с продажи — " + market.Tax.ToString("0.#", CultureInfo.GetCultureInfo("ru-RU")) + "%" +
                (market.Owner != null
                    ? ", её берёт клан игроков «" + market.Owner.Name + "»" +
                      (market.Owner.Id == DikozemieClans.MyClanId ? " (ваш клан)." : ".")
                    : string.IsNullOrEmpty(market.Lord) ? "." : ", её берёт клан " + market.Lord + "."));
            if (row == null)
            {
                text.Add("Выберите товар, чтобы узнать, откуда он и где за него дают больше. " +
                    "КУПИТЬ и ПРОДАТЬ — по одному; с Shift — по пять, с Ctrl — сколько унесёте. " +
                    "Пешком уносите один мешок; вьючная лошадь, мул или верблюд берут ещё по четыре.");
                if (state.Load > 0)
                {
                    text.Add("В обозе: " + CargoList(state) + ".");
                }
                return string.Join("\n", text);
            }
            DikozemieEconomyGood good = DikozemieEconomy.Good(row.Good);
            text.Add((good?.Name ?? row.Good) + " — " + (good?.Unit ?? string.Empty) + ". " + (good?.About ?? string.Empty));
            var here = new List<string>();
            if (row.Made > 0.05)
            {
                here.Add("деревни и мастерские дают " + row.Made.ToString("0.#", CultureInfo.GetCultureInfo("ru-RU")) + " в день");
            }
            if (row.Used > 0.05)
            {
                here.Add("горожане берут " + row.Used.ToString("0.#", CultureInfo.GetCultureInfo("ru-RU")) + " в день");
            }
            if (row.Trend > 0.05)
            {
                here.Add("со вчера дороже на " + (int)Math.Round(row.Trend * 100) + "%");
            }
            else if (row.Trend < -0.05)
            {
                here.Add("со вчера дешевле на " + (int)Math.Round(-row.Trend * 100) + "%");
            }
            if (here.Count > 0)
            {
                text.Add("Здесь " + string.Join(", ", here) + ".");
            }
            List<KeyValuePair<string, DikozemieKnownPrice>> known = DikozemieEconomy.Known(row.Good)
                .Where(k => k.Key != market.Town).ToList();
            if (known.Count > 0)
            {
                string dear = string.Join(", ", known.OrderByDescending(k => k.Value.Sell).Take(3)
                    .Select(k => (DikozemieEconomy.Town(k.Key)?.Name ?? k.Key) + " " + k.Value.Sell + " (" + Ago(k.Value.At) + ")"));
                string cheap = string.Join(", ", known.Where(k => k.Value.Buy > 0).OrderBy(k => k.Value.Buy).Take(3)
                    .Select(k => (DikozemieEconomy.Town(k.Key)?.Name ?? k.Key) + " " + k.Value.Buy + " (" + Ago(k.Value.At) + ")"));
                text.Add("Продают дороже всего: " + dear + ". Купить дешевле всего: " + (cheap.Length > 0 ? cheap : "—") +
                    ". Цены — какими вы их видели сами; с тех пор могли измениться.");
            }
            else
            {
                text.Add("Других цен вы ещё не знаете: зайдите на торговый двор другого города.");
            }
            if (state.Cargo.TryGetValue(row.Good, out int carried) && carried > 0)
            {
                state.Basis.TryGetValue(row.Good, out double basis);
                text.Add("В обозе " + carried + (basis > 0 ? ", куплено по " + basis.ToString("0.#", CultureInfo.GetCultureInfo("ru-RU")) : string.Empty) +
                    ". Продать здесь: " + row.Sell + " за штуку, после пошлины рынка.");
            }
            return string.Join("\n", text);
        }

        private static string CargoList(DikozemieEconomyState state) =>
            string.Join(", ", state.Cargo.Where(p => p.Value > 0)
                .Select(p => DikozemieEconomy.GoodName(p.Key).ToLowerInvariant() + " " + p.Value));

        // ---- contracts --------------------------------------------------------

        private void RenderContracts(DikozemieEconomyState state)
        {
            DikozemieLobbyMapNode node = _townNode;
            _sheetTitle.Text = DikozemieTown.IsTown(node)
                ? "ПОРУЧЕНИЯ КУПЦОВ"
                : DikozemieTown.IsCastle(node) ? "ДЕЛА ЛОРДА" : "ПОРУЧЕНИЕ СТАРОСТЫ";
            SetColumns(1.7f, 3.4f, 1.9f, 1.0f, 1.2f);
            SetAlign(TextHorizontalAlignment.Left, TextHorizontalAlignment.Left, TextHorizontalAlignment.Left,
                TextHorizontalAlignment.Right, TextHorizontalAlignment.Right);
            SetHeads("ОТ КОГО", "ЧТО НУЖНО", "КУДА", "НАГРАДА", "СРОК");
            int index = 0;
            foreach (DikozemieContractInfo c in state.Contracts.OrderBy(c => c.Due))
            {
                DikozemieContractInfo captured = c;
                bool here = node != null && c.At == node.Id;
                bool ready = here && (string.IsNullOrEmpty(c.Good) ||
                    state.Cargo.TryGetValue(c.Good, out int have) && have >= c.Qty);
                SetRow(index++, ContractCells(c), new[] { SheetGold, SheetInk, here ? SheetGood : SheetInk, SheetGold, DueColor(c) },
                    false, null,
                    ("СДАТЬ", ready, () => Act("contract/deliver",
                        "{\"id\":" + DikozemieEconomy.Json(captured.Id) + ",\"at\":" + DikozemieEconomy.Json(node?.Id) + "}",
                        "Сдаём…")),
                    ("ОТКАЗАТЬСЯ", true, () => Act("contract/drop",
                        "{\"id\":" + DikozemieEconomy.Json(captured.Id) + "}", "…")));
            }
            List<DikozemieContractInfo> offers = DikozemieTown.IsTown(node)
                ? DikozemieEconomy.CachedMarket(node?.Id)?.Contracts ?? new List<DikozemieContractInfo>()
                : _offersFor == node?.Id ? _offersHere ?? new List<DikozemieContractInfo>() : new List<DikozemieContractInfo>();
            foreach (DikozemieContractInfo c in offers.Where(o => state.Contracts.All(m => m.Id != o.Id)))
            {
                if (DikozemieTown.IsCastle(node) && !DikozemieTown.HasCastlePass(node))
                {
                    break;
                }
                DikozemieContractInfo captured = c;
                bool full = state.Contracts.Count >= 3;
                SetRow(index++, ContractCells(c), new[] { SheetDim, SheetInk, SheetInk, SheetGold, DueColor(c) },
                    false, null,
                    ("ВЗЯТЬСЯ", !full, () => Act("contract/take",
                        "{\"id\":" + DikozemieEconomy.Json(captured.Id) + "}", "Купец записывает…")));
            }
            var detail = new List<string>();
            if (!string.IsNullOrEmpty(_sheetNote))
            {
                detail.Add(_sheetNote);
            }
            if (index == 0)
            {
                detail.Add(DikozemieTown.IsTown(node)
                    ? "Сегодня купцам этого города ничего не нужно сверх рынка. Купцы ищут перевозчика, когда товара в городе мало: загляните позже или в другой город."
                    : DikozemieTown.IsCastle(node) && !DikozemieTown.HasCastlePass(node)
                        ? "Без пропуска управляющий с вами не говорит."
                        : "Сейчас поручений нет.");
            }
            detail.Add("Купец платит больше рынка, если привезти в срок. Взятое поручение держится за вами; " +
                "не успели — купец найдёт другого. Одновременно можно держать три поручения.");
            SetDetail(string.Join("\n", detail));
        }

        private static string[] ContractCells(DikozemieContractInfo c)
        {
            string where = c.Kind == "courier"
                ? DikozemieEconomy.World.Castles.FirstOrDefault(x => x.Id == c.At)?.Name ?? c.At
                : DikozemieEconomy.Town(c.At)?.Name ??
                  DikozemieEconomy.World.Villages.FirstOrDefault(v => v.Id == c.At)?.Name ?? c.At;
            string what = c.Kind == "courier"
                ? "Отвезти письмо лорда"
                : c.Qty + " × " + (string.IsNullOrEmpty(c.GoodName) ? DikozemieEconomy.GoodName(c.Good) : c.GoodName).ToLowerInvariant();
            return new[]
            {
                Capital(c.Giver), what, where, c.Reward.ToString(CultureInfo.InvariantCulture), Left(c.Due)
            };
        }

        private static Color DueColor(DikozemieContractInfo c) =>
            c.Due - DikozemieEconomy.ServerNow < 600 ? SheetBad : SheetInk;

        private static string Left(double due)
        {
            double seconds = due - DikozemieEconomy.ServerNow;
            if (seconds <= 0)
            {
                return "вышел";
            }
            if (seconds < 3600)
            {
                return (int)Math.Ceiling(seconds / 60) + " мин";
            }
            return (int)(seconds / 3600) + " ч " + (int)(seconds % 3600 / 60) + " мин";
        }

        private static string Capital(string text) =>
            string.IsNullOrEmpty(text) ? string.Empty : char.ToUpper(text[0]) + text.Substring(1);

        // ---- workshops ---------------------------------------------------------

        private void RenderWorkshops(DikozemieEconomyState state)
        {
            DikozemieLobbyMapNode node = _townNode;
            _sheetTitle.Text = "МАСТЕРСКИЕ · " + DikozemieTown.DisplayName(node).ToUpperInvariant();
            SetColumns(2.2f, 2.3f, 1.6f, 1.1f, 1.2f, 1.1f);
            SetAlign(TextHorizontalAlignment.Left, TextHorizontalAlignment.Left, TextHorizontalAlignment.Left,
                TextHorizontalAlignment.Right, TextHorizontalAlignment.Right, TextHorizontalAlignment.Right);
            SetHeads("ДЕЛО", "ХОЗЯИН", "РАБОТА", "ЗА СУТКИ", "ВСЕГО", "ЦЕНА");
            DikozemieMarketInfo market = DikozemieEconomy.CachedMarket(node?.Id);
            int index = 0;
            if (market != null)
            {
                for (int slot = 0; slot < 4; slot++)
                {
                    DikozemieWorkshopInfo shop = market.Workshops.FirstOrDefault(w => w.Slot == slot);
                    int capturedSlot = slot;
                    if (shop == null)
                    {
                        SetRow(index++, new[] { "Свободное место", "—", "пустует", string.Empty, string.Empty, "от 2 400" },
                            new[] { SheetDim, SheetDim, SheetDim }, false, null,
                            ("ОТКРЫТЬ ДЕЛО", true, () =>
                            {
                                _pickSlot = capturedSlot;
                                OpenSheet(TradeSheet.WorkshopPick);
                            }));
                        continue;
                    }
                    bool mine = shop.Owner == state.Player;
                    DikozemieWorkshopInfo captured = shop;
                    SetRow(index++,
                        new[]
                        {
                            shop.Name,
                            mine ? "вы" : string.IsNullOrEmpty(shop.Owner) ? shop.Npc : shop.OwnerName,
                            WorkWords(shop.Work),
                            // A shop bought today has no full day yet: show what it should bring.
                            mine && DikozemieEconomy.ServerNow - shop.Since > 3600
                                ? Signed(shop.Yesterday)
                                : "~" + Signed(shop.Estimate),
                            mine ? Signed(shop.Total) : string.Empty,
                            FormatCoins(shop.Price)
                        },
                        new[]
                        {
                            mine ? SheetGold : SheetInk, mine ? SheetGold : SheetDim,
                            shop.Work < 0.6 ? SheetBad : SheetInk,
                            mine ? shop.Yesterday < 0 ? SheetBad : SheetGood : shop.Estimate < 0 ? SheetBad : SheetDim,
                            shop.Total < 0 ? SheetBad : SheetGood, SheetInk
                        },
                        _selectedWorkshop == shop.Id,
                        () => { _selectedWorkshop = captured.Id; _sheetRevision = -1; },
                        mine
                            ? ("ПРОДАТЬ", true, (Action)(() => Act("workshop/sell",
                                "{\"id\":" + DikozemieEconomy.Json(captured.Id) + ",\"at\":" + DikozemieEconomy.Json(node?.Id) + "}",
                                "Ищем покупателя…")))
                            : ("КУПИТЬ", string.IsNullOrEmpty(shop.Owner) && DikozemieArmoryProgression.HeroResources >= shop.Price,
                                (Action)(() => Act("workshop/buy",
                                    "{\"slot\":" + captured.Slot + ",\"at\":" + DikozemieEconomy.Json(node?.Id) + "}",
                                    "Пишем купчую…"))));
                }
            }
            List<DikozemieWorkshopInfo> elsewhere = state.Workshops.Where(w => w.Town != node?.Id).ToList();
            foreach (DikozemieWorkshopInfo shop in elsewhere)
            {
                SetRow(index++,
                    new[] { shop.Name, "вы · " + shop.TownName, WorkWords(shop.Work), Signed(shop.Yesterday), Signed(shop.Total), string.Empty },
                    new[] { SheetGold, SheetGold, shop.Work < 0.6 ? SheetBad : SheetInk,
                        shop.Yesterday < 0 ? SheetBad : SheetGood, shop.Total < 0 ? SheetBad : SheetGood },
                    false, null);
            }
            var detail = new List<string>();
            if (!string.IsNullOrEmpty(_sheetNote))
            {
                detail.Add(_sheetNote);
            }
            DikozemieWorkshopInfo selected = market?.Workshops.FirstOrDefault(w => w.Id == _selectedWorkshop);
            if (selected != null)
            {
                detail.Add(Recipe(selected.Type));
            }
            detail.Add("«~» — сколько мастерская принесла бы за торговые сутки по сегодняшним ценам. " +
                "Мастерская берёт сырьё с рынка своего города и сдаёт туда товар. Прибыль после жалованья и " +
                "налога города (десятая часть) приходит в казну за каждые пять минут, пока вы в бою или вне игры. " +
                "Нет сырья — работники простаивают, но жалованье получают. Продать обратно — за 70% цены. " +
                "В одном городе — до двух ваших мастерских, всего — до шести. Торговые сутки — час.");
            if (market == null)
            {
                detail.Insert(0, _marketLoading ? "Мастер цеха листает книгу…" : _marketError);
            }
            SetDetail(string.Join("\n", detail));
        }

        private void RenderWorkshopPick(DikozemieEconomyState state)
        {
            DikozemieLobbyMapNode node = _townNode;
            _sheetTitle.Text = "СВОЁ ДЕЛО · " + DikozemieTown.DisplayName(node).ToUpperInvariant();
            SetColumns(2.0f, 3.6f, 1.6f, 1.1f);
            SetAlign(TextHorizontalAlignment.Left, TextHorizontalAlignment.Left, TextHorizontalAlignment.Left,
                TextHorizontalAlignment.Right);
            SetHeads("ДЕЛО", "ИЗ ЧЕГО — ЧТО, В ДЕНЬ", "СЫРЬЁ ЗДЕСЬ", "ЦЕНА");
            DikozemieMarketInfo market = DikozemieEconomy.CachedMarket(node?.Id);
            int index = 0;
            foreach (DikozemieEconomyWorkshopType kind in DikozemieEconomy.World.WorkshopTypes.Take(SheetRows - 1))
            {
                DikozemieEconomyWorkshopType captured = kind;
                string input = kind.Inputs.Keys.FirstOrDefault();
                DikozemieGoodRow raw = market?.Goods.FirstOrDefault(g => g.Good == input);
                (string rawText, Color rawColor) = raw == null ? ("—", SheetDim) : StockWords(raw);
                SetRow(index++,
                    new[] { kind.Name, RecipeShort(kind), rawText, FormatCoins(kind.Price) },
                    new[] { SheetInk, SheetInk, rawColor, SheetInk },
                    false, null,
                    ("ОТКРЫТЬ", DikozemieArmoryProgression.HeroResources >= kind.Price && _pickSlot >= 0,
                        () => Act("workshop/buy",
                            "{\"slot\":" + _pickSlot + ",\"type\":" + DikozemieEconomy.Json(captured.Id) +
                            ",\"at\":" + DikozemieEconomy.Json(node?.Id) + "}", "Нанимаем мастера…")));
            }
            SetRow(index, new[] { "Назад к мастерским" }, new[] { SheetDim }, false, null,
                ("НАЗАД", true, () => OpenSheet(TradeSheet.Workshops)));
            SetDetail((string.IsNullOrEmpty(_sheetNote) ? string.Empty : _sheetNote + "\n") +
                "Своё дело на пустом месте: дом, инструмент и первые работники. Выбирайте то, чьё сырьё в городе " +
                "есть в достатке или дёшево: тогда мастерская работает весь день.");
        }

        private static string RecipeShort(DikozemieEconomyWorkshopType kind)
        {
            string Part(Dictionary<string, double> goods) => string.Join(" + ", goods.Select(p =>
                p.Value.ToString("0.#", CultureInfo.GetCultureInfo("ru-RU")) + " " +
                DikozemieEconomy.GoodName(p.Key).ToLowerInvariant()));
            return Part(kind.Inputs) + " → " + Part(kind.Outputs);
        }

        private static string Recipe(string type)
        {
            DikozemieEconomyWorkshopType kind = DikozemieEconomy.WorkshopType(type);
            return kind == null
                ? string.Empty
                : kind.Name + ": " + RecipeShort(kind) + " в торговые сутки, жалованье " +
                  kind.Wages.ToString("0", CultureInfo.InvariantCulture) + " в день. " + kind.About;
        }

        private static string WorkWords(double work)
        {
            if (work >= 0.95)
            {
                return "работает";
            }
            if (work <= 0.05)
            {
                return "стоит: нет сырья";
            }
            return "вполсилы · " + (int)Math.Round(work * 100) + "%";
        }

        private static string Signed(double value)
        {
            int rounded = (int)Math.Round(value);
            return rounded > 0 ? "+" + rounded : rounded.ToString(CultureInfo.InvariantCulture);
        }

        // ---- caravans ----------------------------------------------------------

        private void RenderCaravans(DikozemieEconomyState state)
        {
            DikozemieLobbyMapNode node = _townNode;
            _sheetTitle.Text = "КАРАВАНЫ";
            SetColumns(2.3f, 2.9f, 0.8f, 1.0f, 1.0f, 0.8f);
            SetAlign(TextHorizontalAlignment.Left, TextHorizontalAlignment.Left, TextHorizontalAlignment.Right,
                TextHorizontalAlignment.Right, TextHorizontalAlignment.Right, TextHorizontalAlignment.Right);
            SetHeads("КАРАВАН", "ГДЕ И ЧТО", "ХОДОК", "ПРИБЫЛЬ", "КОШЕЛЬ", "ОХРАНА");
            int coins = DikozemieArmoryProgression.HeroResources;
            int index = 0;
            bool town = DikozemieTown.IsTown(node);
            bool full = state.Caravans.Count >= 3;
            SetRow(index++,
                new[] { "Малый караван", "3 вьючные лошади, 12 мест, караванщик", string.Empty, string.Empty,
                    "1 200 + 300", "2" },
                new[] { SheetGold, SheetInk, SheetInk, SheetInk, SheetInk, SheetInk }, false, null,
                ("ОСТОРОЖНО", town && !full && coins >= 1500, () => HireCaravan("small", "careful", 300)),
                ("СМЕЛО", town && !full && coins >= 1500, () => HireCaravan("small", "bold", 300)));
            SetRow(index++,
                new[] { "Большой караван", "8 вьючных лошадей, 32 места, караванщик", string.Empty, string.Empty,
                    "3 000 + 800", "5" },
                new[] { SheetGold, SheetInk, SheetInk, SheetInk, SheetInk, SheetInk }, false, null,
                ("ОСТОРОЖНО", town && !full && coins >= 3800, () => HireCaravan("large", "careful", 800)),
                ("СМЕЛО", town && !full && coins >= 3800, () => HireCaravan("large", "bold", 800)));
            foreach (DikozemieCaravanInfo caravan in state.Caravans)
            {
                DikozemieCaravanInfo captured = caravan;
                string where = caravan.State == "travelling"
                    ? "дорога: " + caravan.FromName + " — " + caravan.ToName + ", ещё " + Left(caravan.Arrives) +
                      (caravan.Cargo.Count > 0 ? " · " + string.Join(", ", caravan.Cargo.Select(p =>
                          DikozemieEconomy.GoodName(p.Key).ToLowerInvariant() + " " + p.Value)) : " · порожняком")
                    : caravan.FromName + ": " + (caravan.Arrives > DikozemieEconomy.ServerNow ? "грузится" : "собирается в путь");
                SetRow(index++,
                    new[]
                    {
                        caravan.Name + " · " + caravan.Master + (caravan.Mode == "bold" ? " · смело" : string.Empty),
                        where,
                        caravan.Trips.ToString(CultureInfo.InvariantCulture),
                        Signed(caravan.Earned),
                        FormatCoins((int)caravan.Purse),
                        caravan.Guards.ToString(CultureInfo.InvariantCulture)
                    },
                    new[] { SheetGold, SheetInk, SheetInk, caravan.Earned < 0 ? SheetBad : SheetGood, SheetInk, SheetInk },
                    _selectedCaravan == caravan.Id,
                    () => { _selectedCaravan = captured.Id; _sheetRevision = -1; },
                    ("РАСПУСТИТЬ", true, () => InquiryConfirm("РАСПУСТИТЬ КАРАВАН?",
                        "Груз продадут там, где стоит караван, лошадей — за полцены. Кошель вернётся в казну.",
                        () => Act("caravan/disband", "{\"id\":" + DikozemieEconomy.Json(captured.Id) + "}",
                            "Караванщик пересчитывает…"))));
            }
            var detail = new List<string>();
            if (!string.IsNullOrEmpty(_sheetNote))
            {
                detail.Add(_sheetNote);
            }
            DikozemieCaravanInfo chosen = state.Caravans.FirstOrDefault(c => c.Id == _selectedCaravan);
            if (chosen != null && chosen.Log.Count > 0)
            {
                detail.Add("Записи караванщика " + chosen.Master + ": " +
                    string.Join(" ", chosen.Log.Skip(Math.Max(0, chosen.Log.Count - 4)).Select(l => l.Text)));
            }
            else
            {
                detail.Add("Караванщик сам покупает там, где дёшево, и везёт туда, где дороже, по той же бирже, что и вы. " +
                    "Прибыль сверх кошеля уходит в казну. Осторожный ходит к соседям, смелый — далеко: выгоднее, но " +
                    "разбойники нападают чаще. Охрана отбивается, но может погибнуть; без кошеля люди расходятся. " +
                    "Содержание: малый — 10 монет в торговые сутки, большой — 26.");
            }
            if (!town)
            {
                detail.Add("Снарядить караван можно только в городе.");
            }
            SetDetail(string.Join("\n", detail));
        }

        private void HireCaravan(string size, string mode, int purse)
        {
            DikozemieLobbyMapNode node = _townNode;
            InquiryConfirm(size == "small" ? "МАЛЫЙ КАРАВАН" : "БОЛЬШОЙ КАРАВАН",
                (size == "small" ? "1 200 монет на лошадей и охрану" : "3 000 монет на лошадей и охрану") +
                " и " + purse + " в кошель каравана. " +
                (mode == "bold" ? "Смелый караванщик ходит далеко: выгоднее, но опаснее." : "Осторожный караванщик ходит к соседним городам."),
                () => Act("caravan/hire",
                    "{\"size\":" + DikozemieEconomy.Json(size) + ",\"mode\":" + DikozemieEconomy.Json(mode) +
                    ",\"purse\":" + purse + ",\"at\":" + DikozemieEconomy.Json(node?.Id) + "}",
                    "Нанимаем людей…"));
        }

        private static void InquiryConfirm(string title, string text, Action yes)
        {
            Inquiry(title, text, "ДА", "НЕТ", yes, null);
        }

        // ---- ledger --------------------------------------------------------------

        private void RenderLedger(DikozemieEconomyState state)
        {
            _sheetTitle.Text = "КНИГА КАЗНЫ";
            SetColumns(0.9f, 2.0f, 4.4f, 0.9f, 1.0f);
            SetAlign(TextHorizontalAlignment.Left, TextHorizontalAlignment.Left, TextHorizontalAlignment.Left,
                TextHorizontalAlignment.Right, TextHorizontalAlignment.Right);
            SetHeads("КОГДА", "ЗА ЧТО", "ПОДРОБНО", "СУММА", "ОСТАТОК");
            int index = 0;
            foreach (DikozemieLedgerRow row in state.Ledger.Take(SheetRows))
            {
                DateTime at = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(row.At).ToLocalTime();
                SetRow(index++,
                    new[]
                    {
                        at.ToString(at.Date == DateTime.Now.Date ? "HH:mm" : "dd.MM HH:mm", CultureInfo.InvariantCulture),
                        row.Title, row.Note, Signed(row.Amount), FormatCoins(row.Balance)
                    },
                    new[] { SheetDim, SheetInk, SheetDim, row.Amount < 0 ? SheetBad : SheetGood, SheetInk },
                    false, null);
            }
            var summary = new List<string>
            {
                "Каждая монета записана на сервере: откуда пришла и куда ушла. Подделать её на своём компьютере нельзя."
            };
            if (state.Workshops.Count > 0)
            {
                double today = state.Workshops.Sum(w => w.Today);
                summary.Add("Мастерских: " + state.Workshops.Count + ", сегодня принесли " + Signed(today) +
                    (state.WorkshopsYesterday != 0 ? ", вчера " + Signed(state.WorkshopsYesterday) : string.Empty) + ".");
            }
            if (state.Caravans.Count > 0)
            {
                summary.Add("Караванов: " + state.Caravans.Count + ", заработали " +
                    Signed(state.Caravans.Sum(c => c.Earned)) + ".");
            }
            if (state.Commendas.Count > 0)
            {
                summary.Add("В плаваниях: " + string.Join("; ", state.Commendas.Select(c =>
                    c.Ship + " — " + c.Amount + " мон., вернётся через " + Left(c.Returns))) + ".");
            }
            int pending = DikozemieArmoryProgression.HeroResources - state.Coins;
            if (pending != 0)
            {
                summary.Add("В пути к казне: " + Signed(pending) + " (бои и дорога, расчёт через несколько секунд).");
            }
            SetDetail(string.Join("\n", summary));
        }

        // ---- the father's letter -------------------------------------------------

        private void RenderLetter(DikozemieEconomyState state)
        {
            DikozemieStoryInfo story = state.Story;
            _sheetTitle.Text = "ПИСЬМО ОТЦА";
            SetColumns(1f);
            _sheetDetail.IsVisible = false;
            _sheetDetailLine.IsVisible = false;
            _sheetLetter.IsVisible = true;
            string text;
            if (story == null)
            {
                text = "Писем нет.";
            }
            else if (story.Done)
            {
                text = story.Letter + "\n\nВсе поручения отца исполнены.";
            }
            else
            {
                text = story.Title.ToUpperInvariant() + "\n\n" + story.Letter + "\n\n" +
                    "Что сделать: " + story.Task + "\nОтец пришлёт: " + story.Reward + " монет.";
            }
            SetText(_sheetLetter, SheetPad + 40f, 110f, Math.Min(920f, _sheetWidth - SheetPad * 2f - 80f),
                _sheetHeight - 150f);
            _sheetLetter.Text = text + "\n\n                                        Твой отец";
        }

        // ---- commenda: a share in a merchant ship's voyage -------------------------

        private void RenderCommenda(DikozemieEconomyState state)
        {
            DikozemieLobbyMapNode node = _townNode;
            _sheetTitle.Text = "КУПЕЧЕСКИЙ КОРАБЛЬ";
            SetColumns(2.0f, 4.0f, 1.2f, 1.4f);
            SetAlign(TextHorizontalAlignment.Left, TextHorizontalAlignment.Left, TextHorizontalAlignment.Right,
                TextHorizontalAlignment.Right);
            SetHeads("ВКЛАД", "ЧТО БУДЕТ", "СУММА", "ВЕРНЁТСЯ");
            int coins = DikozemieArmoryProgression.HeroResources;
            int index = 0;
            bool full = state.Commendas.Count >= 3;
            foreach (int amount in new[] { 20, 50, 100, 300, 600 })
            {
                int captured = amount;
                SetRow(index++,
                    new[] { "Доля в плавании", "обычно " + (int)(amount * 1.09) + "–" + (int)(amount * 1.25) +
                        ", бывает меньше или ничего", FormatCoins(amount), "1,5–2,5 ч" },
                    new[] { SheetInk, SheetDim, SheetGold, SheetInk }, false, null,
                    ("ВЛОЖИТЬ", !full && coins >= amount, () => Act("commenda",
                        "{\"amount\":" + captured + ",\"at\":" + DikozemieEconomy.Json(node?.Id) + "}",
                        "Корабельщик пишет расписку…")));
            }
            foreach (DikozemieCommendaInfo voyage in state.Commendas)
            {
                SetRow(index++,
                    new[] { voyage.Ship, voyage.FromName + " → " + voyage.ToName + " и обратно, шкипер " + voyage.Captain,
                        FormatCoins(voyage.Amount), Left(voyage.Returns) },
                    new[] { SheetGold, SheetInk, SheetGold, SheetInk }, false, null);
            }
            SetDetail((string.IsNullOrEmpty(_sheetNote) ? string.Empty : _sheetNote + "\n") +
                "Коменда, как у купцов Генуи и Ганзы: вы даёте деньги, шкипер везёт товар за море. Три четверти " +
                "прибыли — ваши, четверть — его. Обычно корабль приносит от десятой до трети сверху; бывает, " +
                "товар подмокнет или упадут цены, а изредка корабль не возвращается вовсе. Одновременно — до трёх вкладов.");
        }
    }
}
