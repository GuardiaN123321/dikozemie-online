using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    internal sealed class DikozemieWalkChoice
    {
        internal string Label = string.Empty;
        internal string Detail = string.Empty;
        internal Action Act;
    }

    // A conversation with one person of the walk, shown at the right of the
    // screen as in the singleplayer game.
    internal sealed class DikozemieWalkTalk
    {
        internal Agent Agent;
        internal DikozemieWalker Walker;
        internal string Name = string.Empty;
        internal string Title = string.Empty;
        internal string Text = string.Empty;
        internal readonly List<DikozemieWalkChoice> Choices = new List<DikozemieWalkChoice>();
    }

    public sealed partial class DikozemieTownWalkController
    {
        private DikozemieWalkTalk _talk;
        private int _rumorIndex;
        private bool _barmaidGossip;
        private int _alms;

        internal DikozemieWalkTalk Talk => _talk;

        internal bool IsTalking => _talk != null;

        // Bumped whenever the conversation changes: the screen redraws it.
        internal int TalkVersion { get; private set; }

        // Where the hero asked the way to: shown at the top until he is there.
        internal DikozemieWalkDoor Waypoint { get; private set; }

        internal void ClearWaypoint() => Waypoint = null;

        public override bool IsThereAgentAction(Agent userAgent, Agent otherAgent)
        {
            return userAgent != null && userAgent == _heroAgent && otherAgent != null && otherAgent.IsActive() &&
                !_leaving && !InBrawl && _talk == null &&
                _walkers.TryGetValue(otherAgent, out DikozemieWalker walker) && walker.Talkable;
        }

        public override void OnAgentInteraction(Agent userAgent, Agent agent, sbyte agentBoneIndex)
        {
            base.OnAgentInteraction(userAgent, agent, agentBoneIndex);
            if (userAgent != _heroAgent || agent == null || _leaving || InBrawl || _talk != null ||
                !_walkers.TryGetValue(agent, out DikozemieWalker walker) || !walker.Talkable)
            {
                return;
            }
            OpenTalk(walker);
        }

        private void OpenTalk(DikozemieWalker walker)
        {
            Agent agent = walker.Agent;
            if (walker.Kind == DikozemieWalkerKind.Wanderer && !walker.AtPoint)
            {
                // He stops where he is and turns to the hero.
                agent.DisableScriptedMovement();
                var here = new WorldPosition(Mission.Scene, UIntPtr.Zero, agent.Position, false);
                agent.SetScriptedPosition(ref here, false, Agent.AIScriptedFrameFlags.NoAttack);
            }
            agent.SetLookAgent(_heroAgent);
            // A passer-by turns to the hero; a trader at his stall or a man on
            // a bench only turns his head.
            Vec2 toHero = (_heroAgent.Position - agent.Position).AsVec2;
            bool turnedAway = toHero.LengthSquared > 0.01f &&
                Vec2.DotProduct(agent.LookDirection.AsVec2.Normalized(), toHero.Normalized()) < -0.2f;
            if (walker.Kind == DikozemieWalkerKind.Wanderer || walker.Kind == DikozemieWalkerKind.Follower ||
                !walker.AtPoint || turnedAway)
            {
                Vec2 toward = toHero;
                if (toward.LengthSquared > 0.01f)
                {
                    if (walker.AtPoint)
                    {
                        Depart(walker);
                        walker.WaitUntil = -1f;
                        walker.LeaveAt = Mission.CurrentTime + 1f;
                    }
                    var spot = new WorldPosition(Mission.Scene, UIntPtr.Zero, agent.Position, false);
                    agent.SetScriptedPositionAndDirection(ref spot, toward.Normalized().RotationInRadians, false,
                        Agent.AIScriptedFrameFlags.NoAttack);
                }
            }
            var talk = new DikozemieWalkTalk
            {
                Agent = agent,
                Walker = walker,
                Name = string.IsNullOrEmpty(walker.Person.Name) ? walker.Person.Title : walker.Person.Name,
                Title = string.IsNullOrEmpty(walker.Person.Name) ? string.Empty : walker.Person.Title
            };
            _talk = talk;
            Fill(talk);
            DikozemieLobbyShellIntegration.TraceButtonAction("town walk talk role=" + walker.Person.Role +
                " title=" + walker.Person.Title + " place=" + _setup.Place);
        }

        internal void CloseTalk()
        {
            DikozemieWalkTalk talk = _talk;
            _talk = null;
            TalkVersion++;
            if (talk?.Agent != null && talk.Agent.IsActive())
            {
                talk.Agent.ResetLookAgent();
                DikozemieWalker walker = talk.Walker;
                if (walker != null && walker.Kind == DikozemieWalkerKind.Wanderer && !walker.AtPoint)
                {
                    walker.NextThink = 0f;
                }
            }
        }

        internal void Choose(int index)
        {
            DikozemieWalkTalk talk = _talk;
            if (talk == null || index < 0 || index >= talk.Choices.Count)
            {
                return;
            }
            DikozemieWalkChoice choice = talk.Choices[index];
            DikozemieLobbyShellIntegration.TraceButtonAction("town walk choice " + choice.Label + " role=" +
                talk.Walker?.Person.Role);
            choice.Act?.Invoke();
        }

        // The person answers: new words and new choices.
        private void Answer(string text, Action<DikozemieWalkTalk> choices = null)
        {
            DikozemieWalkTalk talk = _talk;
            if (talk == null)
            {
                return;
            }
            talk.Text = text ?? string.Empty;
            talk.Choices.Clear();
            if (choices != null)
            {
                choices(talk);
            }
            else
            {
                Fill(talk, keepText: true);
                return;
            }
            TalkVersion++;
        }

        private static void Add(DikozemieWalkTalk talk, string label, string detail, Action act)
        {
            talk.Choices.Add(new DikozemieWalkChoice { Label = label, Detail = detail ?? string.Empty, Act = act });
        }

        private void AddBye(DikozemieWalkTalk talk, string label = "ПРОЩАЙ")
        {
            Add(talk, label, string.Empty, CloseTalk);
        }

        // ---- Who says what ------------------------------------------------

        private void Fill(DikozemieWalkTalk talk, bool keepText = false)
        {
            DikozemieWalker walker = talk.Walker;
            string role = walker.Person.Role ?? string.Empty;
            string title = walker.Person.Title ?? string.Empty;
            talk.Choices.Clear();
            string text;
            switch (role)
            {
                case "tavernkeeper":
                    text = TavernKeeperTalk(talk);
                    break;
                case "wench":
                    text = BarmaidTalk(talk);
                    break;
                case "arena_master":
                    text = ArenaMasterTalk(talk);
                    break;
                case "merchant":
                case "weaponsmith":
                case "armorer":
                case "horse_merchant":
                case "blacksmith":
                case "barber":
                    text = TraderTalk(talk, role);
                    break;
                case "notable":
                    text = NotableTalk(talk, title);
                    break;
                case "guard":
                    text = GuardTalk(talk);
                    break;
                case "beggar":
                case "female_beggar":
                    text = BeggarTalk(talk);
                    break;
                case "thug":
                    text = walker.IsBrawler && title == "Задира" ? BrawlerTalk(talk) : ThugTalk(talk);
                    break;
                case "dancer":
                    text = "Танцовщица не сбивается с ритма: только улыбается и кивает на шапку для монет у ног музыкантов.";
                    AddBye(talk, "ДАЛЬШЕ");
                    break;
                default:
                    if (walker.Kind == DikozemieWalkerKind.Follower)
                    {
                        text = CompanionTalk(talk);
                    }
                    else
                    {
                        text = FolkTalk(talk);
                    }
                    break;
            }
            if (!keepText)
            {
                talk.Text = text;
            }
            TalkVersion++;
        }

        private string Purse => "Ваши монеты: " + DikozemieArmoryProgression.HeroResources + ".";

        // -- The tavern

        private string TavernKeeperTalk(DikozemieWalkTalk talk)
        {
            Add(talk, "КАК ЗДЕСЬ ЗАРАБОТАТЬ?", string.Empty, () => Answer(DikozemieTown.TavernKeeper.Replace(
                "Трактирщик вытирает кружку: ", string.Empty)));
            Add(talk, "УГОСТИТЬ ЗАВСЕГДАТАЕВ", DikozemieTown.RumorPrice + " МОН.", BuyRumor);
            Add(talk, "СНЯТЬ УГОЛ НА НОЧЬ", DikozemieArmoryProgression.HeroResources >= DikozemieRoads.TavernBedPrice
                ? DikozemieRoads.TavernBedPrice + " МОН." + (DikozemieRoads.Wound > 0 ? " · РАНЫ" : string.Empty)
                : "В КОНЮШНЕ ДАРОМ", Rest);
            Add(talk, "НАНЯТЬ ОХРАНУ", DikozemieRoads.EscortPrice + " МОН. В ДЕНЬ", () =>
            {
                bool done = DikozemieRoads.TryHireEscort(_setup.Node, out string hired);
                if (done)
                {
                    RefreshCompany();
                }
                Answer(hired);
            });
            AddBye(talk);
            string greeting = _setup.Night
                ? "«Поздно бродите. Садитесь ближе к огню — тут теплее и спокойнее, чем на улице.»"
                : "«Заходите, заходите. Пиво свежее, похлёбка горячая, а слухи — за угощение.»";
            return greeting + "\n\n" + Purse;
        }

        private void BuyRumor()
        {
            if (!DikozemieArmoryProgression.TrySpendHeroResources(DikozemieTown.RumorPrice, out int coins, "rumor",
                    "угостил завсегдатаев"))
            {
                Answer("«Без монет никто не разговорится.» Нужно " + DikozemieTown.RumorPrice + ", у вас " + coins + ".");
                return;
            }
            string rumor = DikozemieTown.Rumor(_setup.Node, _rumorIndex++);
            Answer("Кружки пошли по кругу (-" + DikozemieTown.RumorPrice + "). Старик у камина наклоняется к вам:\n" +
                rumor + "\n\n" + Purse);
        }

        private void Rest()
        {
            bool paid = DikozemieArmoryProgression.TrySpendHeroResources(DikozemieRoads.TavernBedPrice, out _, "bed",
                "ночлег в таверне");
            float hours = DikozemieRoads.IsNight || DikozemieRoads.HourOfDay >= 17f ? DikozemieRoads.HoursToMorning() : 8f;
            int before = DikozemieRoads.Wound;
            DikozemieRoads.Advance(hours);
            if (paid || DikozemieRoads.Wound == 1)
            {
                DikozemieRoads.HealOneLevel();
            }
            DikozemieTravelState.Save();
            string wounds = before == 0
                ? string.Empty
                : DikozemieRoads.Wound == 0 ? " Раны затянулись." : " Раны ещё болят, но легче.";
            string note = (paid
                ? "Вы заплатили за угол и тюфяк (-" + DikozemieRoads.TavernBedPrice + ") и проспали "
                : "Хозяин пустил вас спать в конюшню. Вы проспали ") +
                (int)Math.Round(hours) + " ч." + wounds + "\n" + DikozemieRoads.ClockText + ".";
            Answer(note, t => Add(t, "ВСТАТЬ И ВЫЙТИ", string.Empty, () => LeaveWalk(DikozemieWalkExit.Tavern, note)));
        }

        private string BarmaidTalk(DikozemieWalkTalk talk)
        {
            Add(talk, "КРУЖКУ ЭЛЯ", "1 МОН.", () =>
            {
                if (!DikozemieArmoryProgression.TrySpendHeroResources(1, out int coins, "drink", "кружка эля"))
                {
                    Answer("«В долг не наливаем, " + DikozemieRoads.G("красавчик", "красавица") + ".» У вас " + coins + " мон.");
                    return;
                }
                string[] ale =
                {
                    "Эль тёмный, густой, с горчинкой. Тепло расходится по рукам.",
                    "Пена шапкой, кружка тяжёлая. Служанка подмигивает и уходит к другому столу.",
                    "Кислое, но холодное. После дороги — лучше вина."
                };
                Answer(ale[_dice.Next(ale.Length)] + " (-1)\n\n" + Purse);
            });
            if (!_barmaidGossip)
            {
                Add(talk, "ЧТО ГОВОРЯТ В ГОРОДЕ?", string.Empty, () =>
                {
                    _barmaidGossip = true;
                    Answer("Она понижает голос: " + DikozemieTown.Rumor(_setup.Node, _rumorIndex++ + 3) +
                        "\n«Только я вам ничего не говорила.»");
                });
            }
            AddBye(talk);
            return _setup.Night
                ? "«Ещё кружку? Ночь длинная, а хозяин сегодня добрый.»"
                : "«Садитесь где хотите. Пиво, эль, вино из погреба — что подать?»";
        }

        // -- The arena

        private string ArenaMasterTalk(DikozemieWalkTalk talk)
        {
            Add(talk, "ВЫЙТИ НА ПЕСОК", "БОИ ЗА МОНЕТЫ", () => LeaveWalk(DikozemieWalkExit.ArenaFights, string.Empty));
            Add(talk, "КАК ЗДЕСЬ ПЛАТЯТ?", string.Empty, () => Answer(
                "«Побил — получил, побили — заплатил лекарю. Монеты и рейтинг за бой считает арена, не я.»\n\n" +
                DikozemieArenaEarnings.Text));
            AddBye(talk);
            int rating = DikozemieArmoryProgression.PlayerRating;
            string known = rating >= 1000
                ? "«А, вас здесь знают. Народ ставит на вас, когда вы выходите.»"
                : rating >= 300
                    ? "«Видел ваш бой. Неплохо. Ещё пара таких — и о вас заговорят.»"
                    : "«Новенький? На песке все равны, пока не прольётся первая кровь.»";
            return known + "\nРейтинг арены: " + rating + ". Бойцы всех городов сходятся на одном песке: соперник найдётся.";
        }

        // -- The market

        private string TraderTalk(DikozemieWalkTalk talk, string role)
        {
            bool town = _setup.IsTown;
            string goods;
            switch (role)
            {
                case "weaponsmith":
                    goods = "«Мечи, топоры, копья — своя работа и привозная. Острое не возвращают.»";
                    break;
                case "armorer":
                    goods = "«Кольчуга, стёганка, шлем. Хорошая броня стоит как дом — зато дом не спасёт от стрелы.»";
                    break;
                case "horse_merchant":
                    goods = "«Конь — не игрушка. Ест каждый день, а сытый несёт быстрее. Вьючная лошадь утащит четыре мешка.»";
                    break;
                case "blacksmith":
                    goods = "Кузнец не отрывается от наковальни: «Подковы, гвозди, петли. Мечи — к оружейнику.»";
                    break;
                case "barber":
                    goods = "«Побрить, постричь, зуб вырвать, кровь пустить. Всё за медяк — и почти не больно.»";
                    break;
                default:
                    goods = town
                        ? "«Соль, зерно, сукно, масло — весь товар на торговом дворе. Цены общие: продадите — подешевеет для всех.»"
                        : "«Что вырастили, то и продаём.»";
                    break;
            }
            if (role != "barber")
            {
                Add(talk, "ПОСМОТРЕТЬ ТОВАР", "РЫНОК", () => LeaveWalk(DikozemieWalkExit.Market, string.Empty));
            }
            if (role == "merchant" && town)
            {
                Add(talk, "ТОРГОВЫЙ ДВОР", "БИРЖА ТОВАРОВ", () => LeaveWalk(DikozemieWalkExit.Exchange, string.Empty));
            }
            AddBye(talk);
            return goods + "\n\n" + Purse;
        }

        // -- The notables of the town screen

        private string NotableTalk(DikozemieWalkTalk talk, string title)
        {
            DikozemieNotable notable = DikozemieTown.Notables(_setup.Node).FirstOrDefault(n => n.Role == title);
            string speech = notable?.Speech ?? "«Слушаю вас.»";
            switch (title)
            {
                case "Мастер цеха":
                    Add(talk, "МАСТЕРСКИЕ", "КУПИТЬ, ОТКРЫТЬ ДЕЛО", () => LeaveWalk(DikozemieWalkExit.Workshops, string.Empty));
                    break;
                case "Купец":
                case "Купчиха":
                    Add(talk, "ПОРУЧЕНИЯ", "ПРИВЕЗТИ ТОВАР", () => LeaveWalk(DikozemieWalkExit.Contracts, string.Empty));
                    Add(talk, "ТОРГОВЫЙ ДВОР", "БИРЖА", () => LeaveWalk(DikozemieWalkExit.Exchange, string.Empty));
                    break;
                case "Главарь шайки":
                    Add(talk, "ЕСТЬ РАБОТА?", string.Empty, () => Answer(
                        "Он смеётся: «Для тебя? Пока нет. Приходи, когда о тебе заговорят на арене. " +
                        "А пока — ходи по моим улицам осторожно.»"));
                    Add(talk, "ДЕРЗИТЬ", "ДРАКА С ЕГО ЛЮДЬМИ", () => Answer(
                        "Его люди отлипают от стены. Главарь лениво машет рукой: «Поучите-ка гостя вежливости.»",
                        t => Add(t, "ДРАТЬСЯ", (2 + CompanyCount) + " ПРОТИВ ВАС", () => StartGangFight(talk.Agent))));
                    break;
                case "Корабельщик":
                    Add(talk, "В ПОРТ", "КОРАБЛИ, МАТРОСЫ", () => LeaveWalk(DikozemieWalkExit.Port, string.Empty));
                    break;
                case "Староста":
                    Add(talk, "ПОРУЧЕНИЕ СТАРОСТЫ", "ДЕРЕВНЕ НУЖНО", () => LeaveWalk(DikozemieWalkExit.Contracts, string.Empty));
                    Add(talk, "ЗАНОЧЕВАТЬ", RestDetail(), Rest);
                    Add(talk, "СЕЛЬСКИЙ РЫНОК", string.Empty, () => LeaveWalk(DikozemieWalkExit.Market, string.Empty));
                    break;
                case "Знахарка":
                    Add(talk, "ПЕРЕВЯЗАТЬ РАНЫ", DikozemieRoads.Wound > 0
                        ? DikozemieRoads.HealerPrice + " МОН."
                        : "ВЫ ЗДОРОВЫ", Heal);
                    break;
                case "Управляющий":
                    Add(talk, "ДЕЛА ЛОРДА", "ПОРУЧЕНИЯ", () => LeaveWalk(DikozemieWalkExit.Contracts, string.Empty));
                    if (DikozemieTown.HasCastlePass(_setup.Node))
                    {
                        Add(talk, "ОРУЖЕЙНАЯ ЗНАТИ", "ОТ " + DikozemieTown.ArmoryThreshold + " МОН.", () =>
                            LeaveWalk(DikozemieWalkExit.Armory, string.Empty));
                    }
                    speech = "«Лорд в отъезде, дела веду я. Тем, о ком говорят на арене, у нас найдётся поручение.»";
                    break;
            }
            AddBye(talk);
            return speech;
        }

        private static string RestDetail() =>
            DikozemieArmoryProgression.HeroResources >= DikozemieRoads.TavernBedPrice
                ? DikozemieRoads.TavernBedPrice + " МОН."
                : "ДАРОМ, НА СЕНОВАЛЕ";

        private void Heal()
        {
            if (DikozemieRoads.Wound <= 0)
            {
                Answer("Знахарка оглядела вас: «" + DikozemieRoads.G("Здоров как бык", "Здорова как ломовая лошадь") +
                    ". Иди, не мешай.»");
                return;
            }
            if (!DikozemieArmoryProgression.TrySpendHeroResources(DikozemieRoads.HealerPrice, out int coins, "healer",
                    "перевязка у знахарки"))
            {
                Answer("«Травы денег стоят.» Нужно " + DikozemieRoads.HealerPrice + " мон., у вас " + coins + ".");
                return;
            }
            DikozemieRoads.HealOneLevel();
            DikozemieRoads.Advance(1f);
            DikozemieTravelState.Save();
            if (_heroAgent != null)
            {
                _heroAgent.Health = Math.Max(_heroAgent.Health, _heroAgent.HealthLimit * DikozemieRoads.HealthAtStart);
            }
            Answer("Знахарка промыла раны отваром, наложила мазь и тугую повязку (-" + DikozemieRoads.HealerPrice + "). " +
                (DikozemieRoads.Wound == 0 ? "Через час вы на ногах." : "Ещё болит, но идти уже можно."));
        }

        // -- The guard

        private string GuardTalk(DikozemieWalkTalk talk)
        {
            AddWay(talk, "tavern", "ГДЕ ТАВЕРНА?");
            AddWay(talk, "arena", "ГДЕ АРЕНА?");
            AddWay(talk, "lordshall", "КАК ПРОЙТИ В ЗАМОК?");
            AddWay(talk, "port", "ГДЕ ПОРТ?");
            if (_setup.IsTown)
            {
                Add(talk, "КАПИТАН СТРАЖИ", DikozemieRoads.HasPrisoner
                    ? "СДАТЬ ПЛЕННИКА"
                    : DikozemieRoads.HasBounty ? "ВЫ ВЗЯЛИСЬ" : "НАГРАДЫ ЗА РАЗБОЙНИКОВ", () =>
                        LeaveWalk(DikozemieWalkExit.Wanted, string.Empty));
            }
            AddBye(talk);
            string[] lines = _setup.Night
                ? new[]
                {
                    "«Ночью по задворкам не шляйтесь. Резали тут одного на прошлой неделе.»",
                    "«Факел бы вам. А лучше — до таверны и под крышу.»"
                }
                : new[]
                {
                    "«Проходите, не задерживайтесь. Оружие в ножнах держите — здесь не арена.»",
                    "«Тише в городе — легче службе. Драку затеете — сначала штраф, потом колодки.»",
                    "Стражник опирается на копьё: «Чего надо?»"
                };
            return lines[talk.Walker.Mood % lines.Length];
        }

        private void AddWay(DikozemieWalkTalk talk, string to, string label)
        {
            DikozemieWalkDoor door = NearestDoor(to);
            if (door == null)
            {
                return;
            }
            Add(talk, label, string.Empty, () =>
            {
                Waypoint = door;
                Answer(WayText(door));
            });
        }

        private DikozemieWalkDoor NearestDoor(string to)
        {
            if (_heroAgent == null)
            {
                return null;
            }
            Vec2 at = _heroAgent.Position.AsVec2;
            return _doors.Where(d => d.To == to)
                .OrderBy(d => d.Position.AsVec2.DistanceSquared(at))
                .FirstOrDefault();
        }

        private string WayText(DikozemieWalkDoor door)
        {
            string side = SideOf(door);
            int steps = (int)Math.Round(StepsTo(door) / 10.0) * 10;
            return "Он показывает рукой: «" + (steps <= 10 ? "Да вот же, рядом" : "Шагов " + Math.Max(10, steps) + ", " + side) +
                ".» Дорога отмечена вверху экрана.";
        }

        internal int StepsTo(DikozemieWalkDoor door) =>
            _heroAgent == null || door == null ? 0 : (int)(door.Position.AsVec2.Distance(_heroAgent.Position.AsVec2) / 0.75f);

        internal string SideOf(DikozemieWalkDoor door)
        {
            if (_heroAgent == null || door == null)
            {
                return string.Empty;
            }
            Vec2 to = (door.Position - _heroAgent.Position).AsVec2;
            if (to.LengthSquared < 1f)
            {
                return "здесь";
            }
            to.Normalize();
            Vec2 facing = _heroAgent.LookDirection.AsVec2.Normalized();
            float ahead = Vec2.DotProduct(facing, to);
            float left = Vec2.DotProduct(facing.LeftVec(), to);
            if (ahead > 0.7f) return "прямо";
            if (ahead < -0.6f) return "позади";
            return left > 0f ? "по левую руку" : "по правую руку";
        }

        // -- Beggars

        private string BeggarTalk(DikozemieWalkTalk talk)
        {
            Add(talk, "ПОДАТЬ МОНЕТУ", "1 МОН.", () =>
            {
                if (!DikozemieArmoryProgression.TrySpendHeroResources(1, out int coins, "alms", "милостыня"))
                {
                    Answer("Вы шарите по карманам — пусто. Нищий вздыхает: «Бог подаст. Тебе бы самому кто подал.»");
                    return;
                }
                _alms++;
                string tip = _alms == 1
                    ? "\n\nОн хватает вас за рукав и шепчет: " + DikozemieTown.Rumor(_setup.Node, _rumorIndex++ + 5)
                    : string.Empty;
                Answer("«Да хранят вас небеса.» (-1)" + tip + "\n\n" + Purse);
            });
            AddBye(talk, "ПРОЙТИ МИМО");
            return talk.Walker.Person.Female
                ? "«Подайте вдове на хлеб, добрый человек…»"
                : "«Подайте калеке, ради всего святого. Я тоже когда-то держал меч.»";
        }

        // -- Townsfolk

        private string FolkTalk(DikozemieWalkTalk talk)
        {
            DikozemieWalker walker = talk.Walker;
            AddWay(talk, "tavern", "КАК ПРОЙТИ К ТАВЕРНЕ?");
            AddWay(talk, "arena", "ГДЕ ЗДЕСЬ АРЕНА?");
            Add(talk, "ЧТО НОВОГО?", string.Empty, () => Answer(FolkNews(walker)));
            AddBye(talk);
            return FolkGreeting(walker);
        }

        private string FolkGreeting(DikozemieWalker walker)
        {
            bool female = walker.Person.Female;
            string[] day = _setup.IsVillage
                ? new[]
                {
                    "«День добрый. Пришли за зерном или так, поглазеть?»",
                    "«Скотина не ждёт, говорите скорее.»",
                    "«Нечасто к нам заходят такие, как вы. С арены, что ли?»",
                    "«Сено нынче дорогое, а коню всё равно — ест.»"
                }
                : new[]
                {
                    "«Чего вам? Я спешу на рынок.»",
                    "«Вы с арены? У вас вид человека, которого били.»",
                    "«Хорошая погода для торговли. Для драки, впрочем, тоже.»",
                    female ? "«Не стойте на дороге, у меня руки заняты.»" : "«Если ищете работу — сходите к мастеру цеха.»",
                    "«Говорят, на тракте опять пошаливают. Не ходите в одиночку.»",
                    "«Город большой, а поговорить не с кем.»"
                };
            string[] night =
            {
                "«Поздно уже. Шли бы вы домой — если он у вас есть.»",
                "«Ночью тут не гуляют. Ни честные люди, ни глупые.»"
            };
            string[] lines = _setup.Night ? night : day;
            return lines[walker.Mood % lines.Length];
        }

        private string FolkNews(DikozemieWalker walker)
        {
            var news = new List<string>
            {
                "«Цены на хлеб опять поползли. Кто-то скупает зерно мешками.»",
                "«На арене вчера одного унесли — живой, говорят.»",
                "«Купец из-за моря привёз специи. Кто ж их купит по такой цене?»",
                "«Ловцы людей бродят по дорогам: поймают одиночку — и прямиком на песок.»",
                "«Мастерская на углу снова продаётся. Не везёт ей с хозяевами.»",
                "«Стража берёт монету с конных у ворот. С пеших — пока нет.»",
                "«Знахарка за городом лечит лучше городского лекаря и берёт дешевле.»",
                "«Разбойников развелось — хоть караван не снаряжай.»"
            };
            if (DikozemieRoads.HasBounty && !string.IsNullOrEmpty(DikozemieRoads.BountyText))
            {
                news.Add("«Слыхали? За одного атамана капитан стражи обещает награду.»");
            }
            return news[(walker.Mood + _rumorIndex++) % news.Count];
        }

        private string CompanionTalk(DikozemieWalkTalk talk)
        {
            AddBye(talk, "ИДЁМ ДАЛЬШЕ");
            return "«Я рядом. Если кто полезет — увидит.»";
        }

        private string ThugTalk(DikozemieWalkTalk talk)
        {
            AddBye(talk, "ОТОЙТИ");
            return "Он оглядывает вас с головы до ног и сплёвывает под ноги. Разговаривать он не намерен.";
        }

        // The hall door without a pass: the guard of the castle.
        private void OpenHallGuardTalk()
        {
            DikozemieWalker guard = _walkers.Values
                .Where(w => w.IsGuard && w.Agent.IsActive())
                .OrderBy(w => w.Agent.Position.DistanceSquared(_heroAgent.Position))
                .FirstOrDefault();
            if (guard == null)
            {
                Say("Двери зала заперты. Без пропуска стражи туда не войти.");
                return;
            }
            OpenTalk(guard);
            Answer("Стражник загораживает дверь копьём: «Без дела не пускаем.» Десятник косится на ваш кошель: " +
                DikozemieTown.CastleEntryPrice + " монет — и вас запомнят.\n\n" + Purse, t =>
            {
                Add(t, "ЗАПЛАТИТЬ СТРАЖЕ", DikozemieTown.CastleEntryPrice + " МОН.", () =>
                {
                    if (DikozemieTown.TryBuyCastlePass(_setup.Node, out string message) && DikozemieTown.HasCastlePass(_setup.Node))
                    {
                        CloseTalk();
                        Say(message);
                        GoTo(DikozemieWalkPlace.Hall);
                        return;
                    }
                    Answer(message, t2 => AddBye(t2, "ОТОЙТИ"));
                });
                AddBye(t, "ОТОЙТИ");
            });
        }
    }
}
