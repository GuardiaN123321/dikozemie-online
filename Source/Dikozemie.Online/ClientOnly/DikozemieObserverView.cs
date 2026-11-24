using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace Dikozemie.Online
{
    // The observer of the watch inside a battle (plan, «Античит»: «дежурный
    // входит в бой невидимым и летает по полю. Рядом панель: кто в игре,
    // сколько заработал за час, отметки античита, жалобы игроков»).
    // The camera is free (MissionMultiplayerDikozemieArenaClient); this view
    // lists the fighters of this server and their marks. F5: measures to a
    // fighter (the same as on the observer's screen in the lobby).
    public sealed class DikozemieObserverView : MissionView
    {
        private DikozemieObserverVM _dataSource;
        private GauntletLayer _layer;
        private float _refresh;
        private int _revision = -1;

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();
            _dataSource = new DikozemieObserverVM();
            _layer = new GauntletLayer("DikozemieObserver", 20);
            _layer.LoadMovie("DikozemieObserver", _dataSource);
            MissionScreen.AddLayer(_layer);
        }

        public override void OnMissionScreenFinalize()
        {
            if (_layer != null)
            {
                MissionScreen.RemoveLayer(_layer);
                _layer = null;
            }
            _dataSource?.OnFinalize();
            _dataSource = null;
            if (DikozemieWatch.IsWatching)
            {
                DikozemieWatch.StopWatching();
            }
            base.OnMissionScreenFinalize();
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);
            if (_dataSource == null)
            {
                return;
            }
            bool watching = DikozemieWatch.IsWatching;
            _dataSource.IsVisible = watching;
            if (!watching)
            {
                return;
            }
            _refresh -= dt;
            if (_refresh <= 0f)
            {
                _refresh = 6f;
                DikozemieWatch.Refresh(force: true);
            }
            if (_revision != DikozemieWatch.Revision)
            {
                _revision = DikozemieWatch.Revision;
                Fill();
            }
            if (Input.IsKeyReleased(InputKey.F5))
            {
                ChooseFighter();
            }
        }

        private DikozemieWatchBattle Battle =>
            DikozemieWatch.View?.Battles?.FirstOrDefault(b => b.Port == DikozemieWatch.WatchPort);

        private void Fill()
        {
            DikozemieWatchBattle battle = Battle;
            _dataSource.Title = "НАБЛЮДЕНИЕ · " + (battle?.Name ?? ("сервер " + DikozemieWatch.WatchPort));
            var text = new StringBuilder();
            List<DikozemieWatchBattlePlayer> fighters = battle?.Players?.Where(p => !p.Observer).ToList() ??
                new List<DikozemieWatchBattlePlayer>();
            if (fighters.Count == 0)
            {
                text.Append(string.IsNullOrEmpty(DikozemieWatch.LastError)
                    ? "Живых игроков здесь сейчас нет."
                    : DikozemieWatch.LastError);
            }
            foreach (DikozemieWatchBattlePlayer fighter in fighters.Take(12))
            {
                DikozemieWatchPerson person = DikozemieWatch.View?.People?.FirstOrDefault(p => p.Id == fighter.Id);
                text.Append(fighter.Alive ? "● " : "○ ").Append(fighter.Name);
                if (person != null)
                {
                    text.Append("  ·  ").Append(person.Hour.ToString(CultureInfo.InvariantCulture)).Append(" мон./час");
                    if (person.Flags > 0)
                    {
                        text.Append("  ·  отметок ").Append(person.Flags);
                    }
                    if (person.Complaints > 0)
                    {
                        text.Append("  ·  жалоб ").Append(person.Complaints);
                    }
                    if (person.Banned)
                    {
                        text.Append("  ·  ЗАБЛОКИРОВАН");
                    }
                }
                text.Append('\n');
            }
            if (fighters.Count > 12)
            {
                text.Append("… и ещё ").Append(fighters.Count - 12);
            }
            _dataSource.Body = text.ToString().TrimEnd('\n');
            // Checked 2026-11-11: W flies where the camera looks, space lifts it.
            _dataSource.Hint = "Камера свободная: WASD — лететь туда, куда смотрите, мышь — поворот, пробел — выше.\n" +
                "F5 — меры к бойцу.   Esc → «Выйти» — из наблюдения в лобби.";
        }

        private void ChooseFighter()
        {
            List<DikozemieWatchBattlePlayer> fighters = Battle?.Players?.Where(p => !p.Observer).ToList();
            if (fighters == null || fighters.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage("Здесь сейчас нет игроков."));
                return;
            }
            var choices = fighters.Select(f => new InquiryElement(f.Id, f.Name, null, true, string.Empty)).ToList();
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "МЕРЫ К БОЙЦУ", "Кого?", choices, true, 1, 1, "ДАЛЕЕ", "ОТМЕНА",
                picked =>
                {
                    if (picked == null || picked.Count == 0)
                    {
                        return;
                    }
                    string id = (string)picked[0].Identifier;
                    DikozemieWatch.AskMeasure(id, fighters.First(f => f.Id == id).Name);
                },
                null), true);
        }
    }

    public sealed class DikozemieObserverVM : ViewModel
    {
        private bool _isVisible;
        private string _title = string.Empty;
        private string _body = string.Empty;
        private string _hint = string.Empty;

        [DataSourceProperty]
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (value != _isVisible)
                {
                    _isVisible = value;
                    OnPropertyChangedWithValue(value, nameof(IsVisible));
                }
            }
        }

        [DataSourceProperty]
        public string Title
        {
            get => _title;
            set
            {
                if (value != _title)
                {
                    _title = value;
                    OnPropertyChangedWithValue(value, nameof(Title));
                }
            }
        }

        [DataSourceProperty]
        public string Body
        {
            get => _body;
            set
            {
                if (value != _body)
                {
                    _body = value;
                    OnPropertyChangedWithValue(value, nameof(Body));
                }
            }
        }

        [DataSourceProperty]
        public string Hint
        {
            get => _hint;
            set
            {
                if (value != _hint)
                {
                    _hint = value;
                    OnPropertyChangedWithValue(value, nameof(Hint));
                }
            }
        }
    }
}
