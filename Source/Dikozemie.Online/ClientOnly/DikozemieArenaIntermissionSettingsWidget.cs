using System;
using System.Collections.Generic;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace Dikozemie.Online
{
    /// <summary>
    /// The battle rotation's switches on the result screen: the same kinds as
    /// in the lobby's Arena section, so a player can switch a format off
    /// between two battles without leaving. The next battle is shown below.
    /// </summary>
    public sealed class DikozemieArenaIntermissionSettingsWidget : Widget
    {
        private sealed class Toggle
        {
            internal ButtonWidget Button;
            internal Widget Background;
            internal Widget BoxFill;
            internal TextWidget Label;
            internal Func<bool> IsOn;
            internal Func<bool> IsUsable;
        }

        private static readonly Color Gold = new Color(0.996f, 0.757f, 0.341f, 1f);
        private static readonly Color Body = new Color(0.98f, 0.86f, 0.62f, 1f);
        private static readonly Color Muted = new Color(0.72f, 0.58f, 0.38f, 1f);
        private static readonly Color Dim = new Color(0.40f, 0.37f, 0.33f, 1f);
        private static readonly Color Warning = new Color(0.94f, 0.38f, 0.27f, 1f);
        private static readonly Color FillOn = new Color(0.42f, 0.27f, 0.09f, 1f);
        private static readonly Color FillOff = new Color(0.07f, 0.05f, 0.03f, 1f);
        private static readonly Color Frame = new Color(0.43f, 0.30f, 0.16f, 1f);
        private static readonly Color BoxFrame = new Color(0.878f, 0.620f, 0.282f, 0.9f);

        private readonly List<Toggle> _toggles = new List<Toggle>();
        private TextWidget _next;
        private TextWidget _notice;
        private float _noticeSeconds;
        private float _refreshElapsed;
        private bool _built;

        public DikozemieArenaIntermissionSettingsWidget(UIContext context)
            : base(context)
        {
            ClipContents = true;
        }

        protected override void OnConnectedToRoot()
        {
            base.OnConnectedToRoot();
            Build();
            RefreshState();
        }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            if (_noticeSeconds > 0f)
            {
                _noticeSeconds -= Math.Max(0f, dt);
                if (_noticeSeconds <= 0f && _notice != null)
                {
                    _notice.Text = "НАЖМИТЕ НА ВИД БОЯ, ЧТОБЫ ВКЛЮЧИТЬ ИЛИ ВЫКЛЮЧИТЬ ЕГО";
                    _notice.Brush.FontColor = Muted;
                }
            }
            _refreshElapsed += Math.Max(0f, dt);
            if (_refreshElapsed >= 0.5f)
            {
                _refreshElapsed = 0f;
                RefreshState();
            }
        }

        private void Build()
        {
            if (_built || Context == null)
            {
                return;
            }
            _built = true;

            AddCaption("IntermissionSizesCaption", "ЧИСЛО БОЙЦОВ", 0f);
            int[] sizes = DikozemieBattleRotation.TeamSizes;
            for (int index = 0; index < sizes.Length; index++)
            {
                int teamSize = sizes[index];
                AddToggle(
                    "IntermissionSize" + teamSize,
                    teamSize + "×" + teamSize,
                    4f + (index % 4) * 124f,
                    26f + (index / 4) * 38f,
                    120f,
                    () => DikozemieBattleRotation.IsSizeEnabled(teamSize),
                    () => true,
                    () => Report(DikozemieBattleRotation.ToggleSize(teamSize, out string refusal), refusal));
            }

            AddCaption("IntermissionStylesCaption", "СТИЛЬ БОЯ", 180f);
            string[] styles = { "ПЕШИЙ", "КОННЫЙ", "ОТКРЫТЫЙ" };
            for (int index = 0; index < styles.Length; index++)
            {
                var discipline = (DikozemieArenaDiscipline)index;
                AddToggle(
                    "IntermissionStyle" + index,
                    styles[index],
                    4f + index * 166f,
                    206f,
                    162f,
                    () => DikozemieBattleRotation.IsDisciplineEnabled(discipline),
                    () => DikozemieArenaMatchSelection.IsDisciplineAvailable(discipline, out _),
                    () => Report(DikozemieBattleRotation.ToggleDiscipline(discipline, out string refusal), refusal));
            }

            AddCaption("IntermissionMixesCaption", "СОСТАВ ВОЙСК · МЕТАТЕЛЬНОЕ ОРУЖИЕ РАЗРЕШЕНО", 250f);
            string[] mixes = { "СМЕШАННЫЙ", "БЛИЖНИЙ БОЙ", "ДАЛЬНИЙ БОЙ" };
            for (int index = 0; index < mixes.Length; index++)
            {
                var composition = (DikozemieArenaComposition)index;
                AddToggle(
                    "IntermissionMix" + index,
                    mixes[index],
                    4f + index * 166f,
                    276f,
                    162f,
                    () => DikozemieBattleRotation.IsCompositionEnabled(composition),
                    () => IsCompositionUsable(composition),
                    () => Report(DikozemieBattleRotation.ToggleComposition(composition, out string refusal), refusal));
            }

            AddCaption("IntermissionShipsCaption", "МОРСКИЕ БОИ", 320f);
            string[] ships = { "ЛЁГКИЙ", "СРЕДНИЙ", "ТЯЖЁЛЫЙ" };
            for (int index = 0; index < ships.Length; index++)
            {
                var shipClass = (DikozemieNavalShipClass)index;
                AddToggle(
                    "IntermissionShip" + index,
                    ships[index],
                    4f + index * 166f,
                    346f,
                    162f,
                    () => DikozemieBattleRotation.IsShipEnabled(shipClass),
                    () => DikozemieLaunchGuard.IsNavalAvailable,
                    () => Report(DikozemieBattleRotation.ToggleShip(shipClass, out string refusal), refusal));
            }

            Widget line = CreateSolid("IntermissionLine", new Color(0.878f, 0.620f, 0.282f, 0.55f));
            line.HeightSizePolicy = SizePolicy.Fixed;
            line.SuggestedHeight = 2f;
            line.PositionYOffset = 396f;
            AddChild(line);

            _next = AddLabel("IntermissionNext", string.Empty, 404f, 60f, 20, Gold);
            _notice = AddLabel(
                "IntermissionNotice",
                "НАЖМИТЕ НА ВИД БОЯ, ЧТОБЫ ВКЛЮЧИТЬ ИЛИ ВЫКЛЮЧИТЬ ЕГО",
                466f,
                40f,
                15,
                Muted);
        }

        private static bool IsCompositionUsable(DikozemieArenaComposition composition)
        {
            foreach (DikozemieArenaDiscipline discipline in DikozemieBattleRotation.Disciplines)
            {
                if (DikozemieArenaMatchSelection.IsCompositionPlayable(discipline, composition))
                {
                    return true;
                }
            }
            return false;
        }

        private void Report(bool changed, string refusal)
        {
            if (!changed && !string.IsNullOrEmpty(refusal))
            {
                _notice.Text = refusal.ToUpperInvariant();
                _notice.Brush.FontColor = Warning;
                _noticeSeconds = 3f;
            }
            RefreshState();
        }

        private void AddToggle(
            string id,
            string text,
            float x,
            float y,
            float width,
            Func<bool> isOn,
            Func<bool> isUsable,
            Action toggle)
        {
            var button = new ButtonWidget(Context)
            {
                Id = id,
                WidthSizePolicy = SizePolicy.Fixed,
                HeightSizePolicy = SizePolicy.Fixed,
                SuggestedWidth = width,
                SuggestedHeight = 34f,
                PositionXOffset = x,
                PositionYOffset = y,
                DoNotPassEventsToChildren = true
            };
            button.AddChild(CreateSolid(id + "Frame", Frame));
            Widget background = CreateSolid(id + "Background", FillOn);
            background.MarginLeft = 1f;
            background.MarginRight = 1f;
            background.MarginTop = 1f;
            background.MarginBottom = 1f;
            button.AddChild(background);
            Widget box = CreateSolid(id + "Box", BoxFrame);
            box.WidthSizePolicy = SizePolicy.Fixed;
            box.HeightSizePolicy = SizePolicy.Fixed;
            box.SuggestedWidth = 14f;
            box.SuggestedHeight = 14f;
            box.HorizontalAlignment = HorizontalAlignment.Left;
            box.VerticalAlignment = VerticalAlignment.Center;
            box.MarginLeft = 9f;
            button.AddChild(box);
            Widget boxFill = CreateSolid(id + "BoxFill", Gold);
            boxFill.MarginLeft = 2f;
            boxFill.MarginRight = 2f;
            boxFill.MarginTop = 2f;
            boxFill.MarginBottom = 2f;
            box.AddChild(boxFill);
            TextWidget label = CreateText(id + "Text", text, 15, Body);
            label.WidthSizePolicy = SizePolicy.StretchToParent;
            label.HeightSizePolicy = SizePolicy.StretchToParent;
            label.MarginLeft = 20f;
            button.AddChild(label);
            button.ClickEventHandlers.Add(_ => toggle?.Invoke());
            AddChild(button);
            _toggles.Add(new Toggle
            {
                Button = button,
                Background = background,
                BoxFill = boxFill,
                Label = label,
                IsOn = isOn,
                IsUsable = isUsable
            });
        }

        private void AddCaption(string id, string text, float y)
        {
            TextWidget caption = AddLabel(id, text, y, 24f, 15, Muted);
            caption.Brush.TextHorizontalAlignment = TextHorizontalAlignment.Left;
        }

        private TextWidget AddLabel(
            string id,
            string text,
            float y,
            float height,
            int size,
            Color color)
        {
            TextWidget label = CreateText(id, text, size, color);
            label.WidthSizePolicy = SizePolicy.StretchToParent;
            label.HeightSizePolicy = SizePolicy.Fixed;
            label.SuggestedHeight = height;
            label.MarginLeft = 4f;
            label.MarginRight = 4f;
            label.PositionYOffset = y;
            AddChild(label);
            return label;
        }

        private void RefreshState()
        {
            if (!_built)
            {
                return;
            }
            foreach (Toggle toggle in _toggles)
            {
                bool on = toggle.IsOn();
                bool usable = toggle.IsUsable();
                toggle.Background.Color = on ? FillOn : FillOff;
                toggle.BoxFill.Color = on ? Gold : new Color(0.03f, 0.02f, 0.01f, 1f);
                toggle.Label.Brush.FontColor = !usable ? Dim : on ? Gold : Body;
            }
            DikozemieBattlePlan next = DikozemieBattleRotation.PreviewNext();
            _next.Text = next == null
                ? "НЕТ ПОДХОДЯЩЕГО БОЯ:\nВКЛЮЧИТЕ ДРУГИЕ ВИДЫ"
                : next.DescribeForPanel();
            _next.Brush.FontColor = next == null ? Warning : Gold;
        }

        private Widget CreateSolid(string id, Color color)
        {
            return new Widget(Context)
            {
                Id = id,
                WidthSizePolicy = SizePolicy.StretchToParent,
                HeightSizePolicy = SizePolicy.StretchToParent,
                Sprite = Context.SpriteData.GetSprite("BlankWhiteSquare_9"),
                Color = color,
                DoNotAcceptEvents = true
            };
        }

        private TextWidget CreateText(
            string id,
            string text,
            int size,
            Color color)
        {
            var widget = new TextWidget(Context)
            {
                Id = id,
                Text = text,
                DoNotAcceptEvents = true
            };
            widget.Brush.Font = Context.FontFactory.GetMappedFontForLocalization(
                "Galahad");
            widget.Brush.FontSize = size;
            widget.Brush.FontColor = color;
            widget.Brush.TextHorizontalAlignment =
                TextHorizontalAlignment.Center;
            widget.Brush.TextVerticalAlignment = TextVerticalAlignment.Center;
            return widget;
        }
    }
}
