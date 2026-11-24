using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace Dikozemie.Online
{
    // The tavern's table (stage 5, «Таверна с живым чатом игроков этого
    // города»): a board over the town picture like the trade yard's. On the
    // left what was said, on the right who is in town now and where, at the
    // bottom the line to say something. The party's talk is a second tab.
    public sealed partial class DikozemieLobbyMapWidget
    {
        private const int ChatRows = 24;
        private const int ChatPeople = 12;
        private const float ChatPeopleWidth = 300f;
        private const float ChatLineHeight = 25f;

        private sealed class ChatRow
        {
            internal TextWidget Time;
            internal TextWidget Name;
            internal TextWidget Text;
        }

        private Widget _chat;
        private readonly Widget[] _chatFrame = new Widget[4];
        private TextWidget _chatTitle;
        private TextWidget _chatStatus;
        private TextWidget _chatEmpty;
        private TextWidget _chatPeopleHead;
        private readonly TextWidget[] _chatPeople = new TextWidget[ChatPeople];
        private readonly ChatRow[] _chatRows = new ChatRow[ChatRows];
        private Widget _chatDivider;
        private Widget _chatInputFrame;
        private EditableTextWidget _chatInput;
        private ButtonWidget _chatSend;
        private Widget _chatSendFill;
        private TextWidget _chatHint;
        private readonly SheetTab[] _chatTabs = new SheetTab[3];
        private bool _chatOpen;
        private string _chatChannel = string.Empty;
        private int _chatRevision = -1;
        private bool _chatSending;
        private string _chatError = string.Empty;
        private float _chatWidth;
        private float _chatHeight;

        private void BuildChatLayer()
        {
            _chat = new Widget(Context)
            {
                Id = "TavernChat",
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                ClipContents = true,
                IsVisible = false
            };
            _townView.AddChild(_chat);
            _chat.AddChild(CreateSolid("TavernChatFill", new Color(0.055f, 0.040f, 0.026f, 1f)));
            for (int index = 0; index < _chatFrame.Length; index++)
            {
                _chatFrame[index] = CreateSolid("TavernChatFrame" + index, new Color(0.72f, 0.47f, 0.17f, 0.75f));
                PlaceFixed(_chatFrame[index]);
                _chat.AddChild(_chatFrame[index]);
            }
            _chatTitle = ChatText("TavernChatTitle", 34, SheetGold, TextHorizontalAlignment.Left);
            _chatStatus = ChatText("TavernChatStatus", 20, SheetInk, TextHorizontalAlignment.Right);
            for (int index = 0; index < _chatTabs.Length; index++)
            {
                int captured = index;
                var tab = new SheetTab();
                tab.Button = new ButtonWidget(Context)
                {
                    Id = "TavernChatTab" + index,
                    WidthSizePolicy = SizePolicy.Fixed,
                    HeightSizePolicy = SizePolicy.Fixed,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    SuggestedWidth = 200f,
                    SuggestedHeight = 40f,
                    DoNotPassEventsToChildren = true,
                    IsVisible = false
                };
                tab.Frame = CreateSolid("TavernChatTabFrame" + index, new Color(0.72f, 0.47f, 0.17f, 0.8f));
                tab.Button.AddChild(tab.Frame);
                tab.Fill = CreateSolid("TavernChatTabFill" + index, OptionFill);
                tab.Fill.MarginLeft = tab.Fill.MarginRight = tab.Fill.MarginTop = tab.Fill.MarginBottom = 2f;
                tab.Button.AddChild(tab.Fill);
                tab.Label = CreateText("TavernChatTabText" + index, string.Empty, 19, SheetGold);
                tab.Label.WidthSizePolicy = SizePolicy.StretchToParent;
                tab.Label.HeightSizePolicy = SizePolicy.StretchToParent;
                tab.Button.AddChild(tab.Label);
                tab.Button.ClickEventHandlers.Add(_ => _chatTabs[captured].Handler?.Invoke());
                _chat.AddChild(tab.Button);
                _chatTabs[index] = tab;
            }
            for (int index = 0; index < ChatRows; index++)
            {
                _chatRows[index] = new ChatRow
                {
                    Time = ChatText("TavernChatTime" + index, 16, SheetDim, TextHorizontalAlignment.Left),
                    Name = ChatText("TavernChatName" + index, 19, SheetGold, TextHorizontalAlignment.Right),
                    Text = ChatText("TavernChatText" + index, 19, SheetInk, TextHorizontalAlignment.Left)
                };
                _chatRows[index].Text.Brush.TextVerticalAlignment = TextVerticalAlignment.Top;
                _chatRows[index].Name.Brush.TextVerticalAlignment = TextVerticalAlignment.Top;
                _chatRows[index].Time.Brush.TextVerticalAlignment = TextVerticalAlignment.Top;
            }
            _chatEmpty = ChatText("TavernChatEmpty", 20, SheetDim, TextHorizontalAlignment.Center);
            _chatDivider = CreateSolid("TavernChatDivider", new Color(0.72f, 0.47f, 0.17f, 0.4f));
            PlaceFixed(_chatDivider);
            _chat.AddChild(_chatDivider);
            _chatPeopleHead = ChatText("TavernChatPeopleHead", 17, SheetDim, TextHorizontalAlignment.Left);
            for (int index = 0; index < ChatPeople; index++)
            {
                _chatPeople[index] = ChatText("TavernChatPerson" + index, 19, SheetInk, TextHorizontalAlignment.Left);
            }

            _chatInputFrame = CreateSolid("TavernChatInputFrame", new Color(0.72f, 0.47f, 0.17f, 0.9f));
            PlaceFixed(_chatInputFrame);
            _chat.AddChild(_chatInputFrame);
            Widget inputFill = CreateSolid("TavernChatInputFill", new Color(0.025f, 0.018f, 0.010f, 1f));
            inputFill.MarginLeft = inputFill.MarginRight = inputFill.MarginTop = inputFill.MarginBottom = 2f;
            _chatInputFrame.AddChild(inputFill);
            _chatInput = new EditableTextWidget(Context)
            {
                Id = "TavernChatInput",
                WidthSizePolicy = SizePolicy.StretchToParent,
                HeightSizePolicy = SizePolicy.StretchToParent,
                MarginLeft = 16f,
                MarginRight = 12f,
                MaxLength = 240,
                DoNotPassEventsToChildren = true
            };
            _chatInput.Brush.Font = Context.FontFactory.GetMappedFontForLocalization("Galahad");
            _chatInput.Brush.FontSize = 21;
            _chatInput.Brush.FontColor = new Color(0.96f, 0.92f, 0.82f, 1f);
            _chatInput.Brush.TextHorizontalAlignment = TextHorizontalAlignment.Left;
            _chatInput.Brush.TextVerticalAlignment = TextVerticalAlignment.Center;
            _chatInput.EventFire += (widget, name, args) =>
            {
                if (name == "TextEntered")
                {
                    SendChat();
                }
            };
            _chatInputFrame.AddChild(_chatInput);

            _chatSend = new ButtonWidget(Context)
            {
                Id = "TavernChatSend",
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                SuggestedWidth = 170f,
                SuggestedHeight = 50f,
                DoNotPassEventsToChildren = true
            };
            _chatSend.AddChild(CreateSolid("TavernChatSendFrame", new Color(0.72f, 0.47f, 0.17f, 1f)));
            _chatSendFill = CreateSolid("TavernChatSendFill", OptionFill);
            _chatSendFill.MarginLeft = _chatSendFill.MarginRight = _chatSendFill.MarginTop = _chatSendFill.MarginBottom = 2f;
            _chatSend.AddChild(_chatSendFill);
            TextWidget sendLabel = CreateText("TavernChatSendText", "СКАЗАТЬ", 21, SheetGold);
            sendLabel.WidthSizePolicy = SizePolicy.StretchToParent;
            sendLabel.HeightSizePolicy = SizePolicy.StretchToParent;
            _chatSend.AddChild(sendLabel);
            _chatSend.ClickEventHandlers.Add(_ => SendChat());
            _chat.AddChild(_chatSend);
            _chatHint = ChatText("TavernChatHint", 17, SheetDim, TextHorizontalAlignment.Left);
        }

        private TextWidget ChatText(string id, int size, Color color, TextHorizontalAlignment align)
        {
            TextWidget text = CreateText(id, string.Empty, size, color);
            PlaceFixed(text);
            text.Brush.TextHorizontalAlignment = align;
            text.IsVisible = false;
            _chat.AddChild(text);
            return text;
        }

        // ---- opening -----------------------------------------------------------

        private void OpenChat(string channel = null)
        {
            if (_chat == null)
            {
                return;
            }
            CloseSheet();
            _chatOpen = true;
            _chatChannel = channel ?? DikozemieWorld.PlaceChannel ?? "place:" + _townNode?.Id;
            _chatRevision = -1;
            _chatError = string.Empty;
            DikozemieWorld.ChatOpen = true;
            DikozemieWorld.ChatViewing = _chatChannel;
            DikozemieWorld.MarkRead(_chatChannel);
            DikozemieWorld.PulseSoon();
            DikozemieLobbyShellIntegration.TraceButtonAction("tavern chat open channel=" + _chatChannel);
            RefreshSidePanel();
            LayoutContent();
            FocusChatInput();
        }

        private void CloseChat()
        {
            if (!_chatOpen)
            {
                return;
            }
            _chatOpen = false;
            DikozemieWorld.ChatOpen = false;
            DikozemieWorld.ChatViewing = string.Empty;
            if (_chatInput != null && EventManager?.FocusedWidget == _chatInput)
            {
                EventManager.ClearFocus();
            }
        }

        private void FocusChatInput()
        {
            if (_chatInput != null && _chatInput.ConnectedToRoot && _chat.IsVisible)
            {
                EventManager.FocusedWidget = _chatInput;
            }
        }

        private void SendChat()
        {
            if (_chatInput == null || _chatSending)
            {
                return;
            }
            string text = (_chatInput.RealText ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return;
            }
            _chatSending = true;
            _chatError = string.Empty;
            string channel = _chatChannel;
            DikozemieWorld.Say(channel, text, error =>
            {
                _chatSending = false;
                if (error == null)
                {
                    _chatInput.SetAllText(string.Empty);
                }
                else
                {
                    _chatError = error;
                }
                _chatRevision = -1;
                FocusChatInput();
            });
        }

        // ---- layout and drawing ------------------------------------------------------

        private void LayoutChatSheet(float width, float height)
        {
            if (_chat == null)
            {
                return;
            }
            _chat.IsVisible = LayerOpen && _chatOpen;
            if (!_chat.IsVisible)
            {
                return;
            }
            _townName.IsVisible = false;
            _townKind.IsVisible = false;
            _townOwner.IsVisible = false;
            _townLore.IsVisible = false;
            const float margin = 18f;
            float w = Math.Max(640f, width - margin * 2f);
            float h = Math.Max(420f, height - margin * 2f);
            _chat.PositionXOffset = margin;
            _chat.PositionYOffset = margin;
            _chat.SuggestedWidth = w;
            _chat.SuggestedHeight = h;
            if (Math.Abs(w - _chatWidth) > 0.5f || Math.Abs(h - _chatHeight) > 0.5f)
            {
                _chatWidth = w;
                _chatHeight = h;
                _chatRevision = -1;
            }
            const float line = 2f;
            SetFrame(_chatFrame[0], 0f, 0f, w, line);
            SetFrame(_chatFrame[1], 0f, h - line, w, line);
            SetFrame(_chatFrame[2], 0f, 0f, line, h);
            SetFrame(_chatFrame[3], w - line, 0f, line, h);
            SetText(_chatTitle, SheetPad, 20f, w * 0.6f, 48f);
            SetText(_chatStatus, w * 0.4f, 26f, w * 0.6f - SheetPad, 40f);
            _chatTitle.IsVisible = true;
            _chatStatus.IsVisible = true;
            float inputTop = h - 104f;
            float logRight = w - SheetPad - ChatPeopleWidth - 24f;
            SetFrame(_chatInputFrame, SheetPad, inputTop, logRight - SheetPad - 184f, 50f);
            _chatSend.PositionXOffset = logRight - 170f;
            _chatSend.PositionYOffset = inputTop;
            SetText(_chatHint, SheetPad + 4f, inputTop + 58f, logRight - SheetPad, 28f);
            _chatHint.IsVisible = true;
            SetFrame(_chatDivider, logRight + 12f, 132f, 1.5f, h - 132f - 30f);
        }

        private void TickChatSheet(float dt)
        {
            if (_chat == null || !_chat.IsVisible)
            {
                return;
            }
            _chatSendFill.Color = _chatSend.IsHovered ? OptionFillHover : OptionFill;
            foreach (SheetTab tab in _chatTabs)
            {
                if (tab.Button.IsVisible)
                {
                    tab.Fill.Color = tab.Active
                        ? new Color(0.42f, 0.25f, 0.07f, 1f)
                        : tab.Button.IsHovered ? OptionFillHover : OptionFill;
                }
            }
            if (DikozemieWorld.Revision != _chatRevision)
            {
                RenderChat();
            }
        }

        private void RenderChat()
        {
            _chatRevision = DikozemieWorld.Revision;
            DikozemieLobbyMapNode node = _townOpen ? _townNode : null;
            float w = _chatWidth;
            float h = _chatHeight;
            bool clan = _chatChannel.StartsWith("clan:", StringComparison.Ordinal);
            if (w <= 0f || node == null && !clan && !_chatChannel.StartsWith("party:", StringComparison.Ordinal))
            {
                return;
            }
            // The town's channel appears with the first answer after arriving.
            if (_chatChannel.StartsWith("place:", StringComparison.Ordinal) && DikozemieWorld.PlaceChannel != null &&
                _chatChannel != DikozemieWorld.PlaceChannel)
            {
                _chatChannel = DikozemieWorld.PlaceChannel;
            }
            DikozemieWorld.ChatViewing = _chatChannel;
            DikozemieWorld.MarkRead(_chatChannel);
            bool party = _chatChannel.StartsWith("party:", StringComparison.Ordinal);
            DikozemieClanFull clanState = DikozemieClans.Clan;
            _chatTitle.Text = party ? "ОТРЯД" : clan ? "КЛАН · " + (clanState?.Name ?? string.Empty).ToUpperInvariant()
                : (DikozemieTown.IsTown(node) ? "ТАВЕРНА · " : "У ОГНЯ · ") + DikozemieTown.DisplayName(node).ToUpperInvariant();
            List<DikozemieWorldPlayer> here = node == null
                ? new List<DikozemieWorldPlayer>()
                : DikozemieWorld.Players.Where(p => p.In == node.Id).ToList();
            _chatStatus.Text = party
                ? "В ОТРЯДЕ: " + (DikozemieWorld.Party?.Members.Count ?? 1)
                : clan ? "В МИРЕ: " + (clanState?.Members.Count(m => m.Online) ?? 1) + " ИЗ " + (clanState?.Members.Count ?? 1)
                : "В " + (DikozemieTown.IsTown(node) ? "ГОРОДЕ" : "ПОСЕЛЕНИИ") + ": " + (here.Count + 1);

            // Tabs: the tavern, the party's own talk, the clan's (stage 6).
            var tabs = new List<(string label, string channel, bool active)>();
            if (node != null)
            {
                string place = DikozemieWorld.PlaceChannel ?? "place:" + node.Id;
                tabs.Add(("ТАВЕРНА" + Unread(place), place, !party && !clan));
            }
            string partyChannel = DikozemieWorld.PartyChannel;
            if (partyChannel != null)
            {
                tabs.Add(("ОТРЯД" + Unread(partyChannel), partyChannel, party));
            }
            string clanChannel = DikozemieWorld.ClanChannel;
            if (clanChannel != null)
            {
                tabs.Add(("КЛАН" + Unread(clanChannel), clanChannel, clan));
            }
            bool tabbed = tabs.Count > 1;
            for (int index = 0; index < _chatTabs.Length; index++)
            {
                if (tabbed && index < tabs.Count)
                {
                    string channel = tabs[index].channel;
                    SetChatTab(index, tabs[index].label, tabs[index].active, () => SwitchChat(channel));
                }
                else
                {
                    _chatTabs[index].Button.IsVisible = false;
                }
            }
            float top = tabbed ? 140f : 100f;
            float logLeft = SheetPad;
            float logRight = w - SheetPad - ChatPeopleWidth - 24f;
            float bottom = h - 120f;

            // The log, newest at the bottom, as many as fit.
            IReadOnlyList<DikozemieWorldLine> lines = DikozemieWorld.Lines(_chatChannel);
            const float timeWidth = 58f;
            const float nameWidth = 170f;
            float textLeft = logLeft + timeWidth + nameWidth + 16f;
            float textWidth = Math.Max(200f, logRight - textLeft);
            int perLine = Math.Max(20, (int)(textWidth / (19f * 0.49f)));
            float y = bottom;
            int row = 0;
            for (int index = lines.Count - 1; index >= 0 && row < ChatRows; index--)
            {
                DikozemieWorldLine said = lines[index];
                string text = said.Text ?? string.Empty;
                int count = Math.Max(1, (text.Length + perLine - 1) / perLine);
                float height = count * ChatLineHeight + 4f;
                if (y - height < top)
                {
                    break;
                }
                y -= height;
                ChatRow widgets = _chatRows[row++];
                DateTime at = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(said.At).ToLocalTime();
                widgets.Time.Text = at.ToString("HH:mm", CultureInfo.InvariantCulture);
                SetText(widgets.Time, logLeft, y + 3f, timeWidth, ChatLineHeight);
                widgets.Time.IsVisible = true;
                widgets.Name.Text = said.IsSystem ? "·" : said.Name;
                widgets.Name.Brush.FontColor = said.IsSystem ? SheetDim
                    : said.Player == DikozemieWorld.MyId ? new Color(0.62f, 0.86f, 0.62f, 1f) : SheetGold;
                SetText(widgets.Name, logLeft + timeWidth, y, nameWidth, ChatLineHeight);
                widgets.Name.IsVisible = true;
                widgets.Text.Text = text;
                widgets.Text.Brush.FontColor = said.IsSystem ? SheetDim : SheetInk;
                SetText(widgets.Text, textLeft, y, textWidth, height);
                widgets.Text.IsVisible = true;
            }
            for (; row < ChatRows; row++)
            {
                _chatRows[row].Time.IsVisible = false;
                _chatRows[row].Name.IsVisible = false;
                _chatRows[row].Text.IsVisible = false;
            }
            _chatEmpty.IsVisible = lines.Count == 0;
            if (_chatEmpty.IsVisible)
            {
                _chatEmpty.Text = !DikozemieWorld.IsOnline
                    ? "Нет связи с сервером мира. " + DikozemieWorld.LastError
                    : party ? "В отряде пока молчат." : clan ? "В клане пока молчат. Скажите — услышат все люди клана, где бы они ни были."
                    : "За столами тихо. Скажите что-нибудь — услышат все, кто сейчас в городе.";
                SetText(_chatEmpty, logLeft, (top + bottom) * 0.5f - 20f, logRight - logLeft, 40f);
            }

            // Who is here: the people of the town, or the party.
            float rightLeft = w - SheetPad - ChatPeopleWidth;
            _chatPeopleHead.Text = party ? "В ОТРЯДЕ" : clan ? "ЛЮДИ КЛАНА" : "СЕЙЧАС ЗДЕСЬ";
            SetText(_chatPeopleHead, rightLeft, top - 4f, ChatPeopleWidth, 28f);
            _chatPeopleHead.IsVisible = true;
            var people = new List<(string text, Color color)>();
            if (clan)
            {
                foreach (DikozemieClanMember member in (clanState?.Members ?? new List<DikozemieClanMember>())
                             .OrderByDescending(m => m.Online))
                {
                    people.Add((member.Name + " · " + (member.Online ? (string.IsNullOrEmpty(member.Where) ? "в мире" : member.Where) : "нет в мире"),
                        member.Id == DikozemieWorld.MyId ? new Color(0.62f, 0.86f, 0.62f, 1f) : member.Online ? SheetInk : SheetDim));
                }
            }
            else if (party)
            {
                foreach (DikozemieWorldMember member in DikozemieWorld.Party?.Members ?? new List<DikozemieWorldMember>())
                {
                    string where = !member.Fresh ? "отстал" : !string.IsNullOrEmpty(member.In)
                        ? DikozemieWorld.PlaceName(member.In) : member.Place == "battle" ? "в бою" : "в пути";
                    people.Add((member.Name + (member.Id == DikozemieWorld.Party.Leader ? " (ведёт)" : string.Empty) +
                        " · " + where, member.Id == DikozemieWorld.MyId ? new Color(0.62f, 0.86f, 0.62f, 1f) : SheetInk));
                }
            }
            else
            {
                people.Add(("Вы · " + WhereWord(_townSection == TownSection.Tavern ? "tavern" : string.Empty, "town"),
                    new Color(0.62f, 0.86f, 0.62f, 1f)));
                foreach (DikozemieWorldPlayer p in here)
                {
                    people.Add((p.Name + " · " + WhereWord(p.Section, p.Place), p.Outlaw ? SheetBad : SheetInk));
                }
            }
            for (int index = 0; index < ChatPeople; index++)
            {
                TextWidget person = _chatPeople[index];
                person.IsVisible = index < people.Count;
                if (!person.IsVisible)
                {
                    continue;
                }
                person.Text = people[index].text;
                person.Brush.FontColor = people[index].color;
                SetText(person, rightLeft, top + 30f + index * 30f, ChatPeopleWidth, 28f);
            }
            _chatHint.Text = !string.IsNullOrEmpty(_chatError)
                ? _chatError
                : party ? "Enter — сказать. Слышит весь отряд, где бы он ни был."
                : clan ? "Enter — сказать. Слышат все люди клана, где бы они ни были."
                    : "Enter — сказать. Слышат все, кто сейчас в " + DikozemieTown.DisplayName(node) + ".";
            _chatHint.Brush.FontColor = string.IsNullOrEmpty(_chatError) ? SheetDim : SheetBad;
        }

        private static string Unread(string channel)
        {
            int count = DikozemieWorld.UnreadIn(channel);
            return count > 0 ? " · " + count : string.Empty;
        }

        private void SetChatTab(int index, string label, bool active, Action handler)
        {
            SheetTab tab = _chatTabs[index];
            tab.Button.IsVisible = true;
            tab.Label.Text = label;
            tab.Active = active;
            tab.Handler = handler;
            tab.Button.PositionXOffset = SheetPad + index * 212f;
            tab.Button.PositionYOffset = 82f;
            tab.Label.Brush.FontColor = active ? new Color(1f, 0.92f, 0.70f, 1f) : SheetGold;
            tab.Frame.Color = active ? new Color(0.98f, 0.76f, 0.34f, 1f) : new Color(0.72f, 0.47f, 0.17f, 0.8f);
        }

        private void SwitchChat(string channel)
        {
            _chatChannel = channel;
            _chatError = string.Empty;
            DikozemieWorld.ChatViewing = channel;
            DikozemieWorld.MarkRead(channel);
            _chatRevision = -1;
            RefreshSidePanel();
            FocusChatInput();
        }

        private static string WhereWord(string section, string place)
        {
            if (place == "walk")
            {
                return "гуляет по городу";
            }
            if (place == "battle")
            {
                return "на арене";
            }
            if (place == "menu")
            {
                return "у торговцев";
            }
            switch ((section ?? string.Empty).ToLowerInvariant())
            {
                case "tavern": return "в таверне";
                case "trade": return "на торговом дворе";
                case "commenda":
                case "port": return "в порту";
                case "castle": return "у замка";
                case "people": return "у горожан";
                case "wanted": return "у капитана стражи";
                default: return "на площади";
            }
        }

        // The side panel while the table is open.
        private void FillChatPanel(out string text)
        {
            DikozemieLobbyMapNode node = _townNode;
            int here = DikozemieWorld.Players.Count(p => p.In == node?.Id);
            _title.Text = "ЗА СТОЛОМ";
            text = here == 0
                ? "Вы сели за стол. Других героев в городе сейчас нет, но всё сказанное останется: кто придёт — прочтёт."
                : "За соседними столами — " + DikozemieWorld.Heroes(here) + ". Говорите: слышат все, кто в городе.";
            if (DikozemieWorld.InParty)
            {
                text += "\nВкладка «Отряд» — разговор вашего отряда, где бы он ни был.";
            }
            if (!DikozemieWorld.IsOnline)
            {
                text += "\n\nНет связи с сервером мира.";
            }
            if (_clanOpen)
            {
                _title.Text = "РАЗГОВОР КЛАНА";
                text = "Говорите: слышат все люди клана, где бы они ни были. Новые слова приходят строкой внизу экрана.";
                AddOption("НАЗАД", string.Empty, () =>
                {
                    CloseChat();
                    ShowClanSection(ClanSection.Main);
                });
                return;
            }
            AddOption("НАЗАД", string.Empty, () =>
            {
                CloseChat();
                ShowSection(TownSection.Tavern);
            });
        }
    }
}
