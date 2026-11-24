using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    internal enum DikozemieBrawlKind
    {
        None,
        // A fist fight on a bet in the tavern: no weapons, the loser yields.
        Fists,
        // The gang leader's men teach a rude guest manners.
        Gang,
        // Night robbers in the streets.
        Robbers
    }

    // Street fights of the walk (the plan: «драка на улице как бой на арене;
    // размер боя зависит от спутников и охраны»). Nobody dies in a town
    // brawl: whoever is beaten down yields, as in the singleplayer alleys.
    public sealed partial class DikozemieTownWalkController
    {
        private const float YieldShare = 0.12f;
        private const float FistYieldShare = 0.3f;

        private readonly List<Agent> _brawlFoes = new List<Agent>();
        private readonly List<Agent> _brawlFriends = new List<Agent>();
        private readonly HashSet<Agent> _yielded = new HashSet<Agent>();
        private DikozemieBrawlKind _brawl;
        private int _brawlBet;
        private float _brawlRetarget;
        private float _guardsAt = -1f;
        private bool _guardsCame;
        private float _brawlResultAt = -1f;
        private bool _brawlWon;
        private MissionEquipment _heroWeapons;
        private MissionEquipment _opponentWeapons;
        private Agent _fistOpponent;
        private bool _crime;

        // Night robbers: decided once per walk.
        private bool _robbersPlanned;
        private bool _robbersDone;
        private float _robbersAt = -1f;
        private readonly List<Agent> _robbers = new List<Agent>();

        internal bool InBrawl => _brawl != DikozemieBrawlKind.None;

        internal string BrawlStatus
        {
            get
            {
                if (!InBrawl)
                {
                    return string.Empty;
                }
                int standing = _brawlFoes.Count(a => !_yielded.Contains(a) && a.IsActive());
                string what = _brawl == DikozemieBrawlKind.Fists ? "КУЛАЧНЫЙ БОЙ" : "УЛИЧНАЯ ДРАКА";
                return what + " · противников на ногах: " + standing +
                    (_brawlFriends.Count > 1 ? " · с вами: " + (_brawlFriends.Count - 1) : string.Empty);
            }
        }

        // -- The tavern brawler

        private void SpawnBrawler()
        {
            UsableMachine machine = Points("npc_common").FirstOrDefault(m => FreePoint(m) != null);
            DikozemieWalker brawler = machine != null
                ? SpawnAtMachine(machine, "thug", "_villager_in_tavern", DikozemieWalkerKind.Fixed, "Задира")
                : SpawnNear("thug", null, HeroStartPoint(), 5f, "Задира");
            if (brawler == null)
            {
                return;
            }
            brawler.IsBrawler = true;
            brawler.Person.Name = NameFor(false, 41);
            SetName(brawler.Agent, brawler.Person.Label);
        }

        private string BrawlerTalk(DikozemieWalkTalk talk)
        {
            foreach (int bet in new[] { 2, 5, 10 })
            {
                int stake = bet;
                Add(talk, "НА КУЛАКАХ · СТАВКА " + stake, "ВЫИГРЫШ " + stake * 2, () => OfferFists(talk, stake));
            }
            AddBye(talk, "НЕ СЕГОДНЯ");
            return "Здоровяк с расквашенным носом стучит кружкой по столу: «Кто на кулаках? Без ножей, без дубин — " +
                "до первого, кто запросит пощады. Ставка на стол, победитель забирает вдвое.»\n\n" + Purse;
        }

        private void OfferFists(DikozemieWalkTalk talk, int bet)
        {
            if (!DikozemieArmoryProgression.TrySpendHeroResources(bet, out int coins, "wager", "ставка на кулачный бой"))
            {
                Answer("«Ставку — на стол.» Нужно " + bet + " мон., у вас " + coins + ".");
                return;
            }
            Agent opponent = talk.Agent;
            CloseTalk();
            StartFistFight(opponent, bet);
        }

        private void StartFistFight(Agent opponent, int bet)
        {
            if (opponent == null || !opponent.IsActive() || _heroAgent == null)
            {
                return;
            }
            if (_walkers.TryGetValue(opponent, out DikozemieWalker walker))
            {
                Depart(walker);
            }
            _heroWeapons = new MissionEquipment();
            _heroWeapons.FillFrom(_heroAgent.Equipment);
            _opponentWeapons = new MissionEquipment();
            _opponentWeapons.FillFrom(opponent.Equipment);
            Disarm(_heroAgent);
            Disarm(opponent);
            _fistOpponent = opponent;
            StartBrawl(new List<Agent> { opponent }, DikozemieBrawlKind.Fists, bet);
            Say("Кулачный бой! Ставка " + bet + " мон. Бейтесь, пока противник не запросит пощады.");
        }

        private static void Disarm(Agent agent)
        {
            agent.TryToSheathWeaponInHand(Agent.HandIndex.OffHand, Agent.WeaponWieldActionType.Instant);
            agent.TryToSheathWeaponInHand(Agent.HandIndex.MainHand, Agent.WeaponWieldActionType.Instant);
            for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
            {
                if (!agent.Equipment[slot].IsEmpty)
                {
                    agent.RemoveEquippedWeapon(slot);
                }
            }
        }

        private static void Rearm(Agent agent, MissionEquipment saved)
        {
            if (agent == null || saved == null || !agent.IsActive())
            {
                return;
            }
            for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
            {
                MissionWeapon weapon = saved[slot];
                if (!weapon.IsEmpty && agent.Equipment[slot].IsEmpty)
                {
                    agent.EquipWeaponWithNewEntity(slot, ref weapon);
                }
            }
        }

        // -- The gang

        private void StartGangFight(Agent leader)
        {
            CloseTalk();
            var foes = _walkers.Values
                .Where(w => w.IsBrawler && w.Person.Title != "Задира" && w.Agent.IsActive() &&
                    w.Agent.Position.DistanceSquared(leader.Position) < 100f)
                .Select(w => w.Agent)
                .ToList();
            // More friends on the hero's side, more men come out of the gate.
            int wanted = Math.Min(5, 2 + CompanyCount);
            for (int index = foes.Count; index < wanted; index++)
            {
                DikozemieWalker man = SpawnNear("thug", null, leader.Position, 3f + index, "Подручный главаря");
                if (man != null)
                {
                    man.IsBrawler = true;
                    man.Talkable = false;
                    foes.Add(man.Agent);
                }
            }
            if (foes.Count == 0)
            {
                Say("Главарь смеётся вам в лицо, но его люди куда-то делись.");
                return;
            }
            StartBrawl(foes, DikozemieBrawlKind.Gang, 0);
            Say("Люди главаря бросаются на вас! Против вас " + foes.Count + ".");
        }

        // -- Night robbers

        private void TickRobbers(float dt)
        {
            if (_robbersDone || InBrawl || _leaving || _heroAgent == null || !_heroAgent.IsActive())
            {
                return;
            }
            if (!_robbersPlanned)
            {
                _robbersPlanned = true;
                bool streets = _setup.Place == DikozemieWalkPlace.Streets && _setup.IsTown;
                bool test = Environment.GetEnvironmentVariable("DIKOZEMIE_TEST_TOWN_ROBBERS") == "1";
                if (streets && (_setup.Night || test) && (test || _dice.Next(100) < 55))
                {
                    _robbersAt = Mission.CurrentTime + (test ? 8f : 25f + (float)_dice.NextDouble() * 45f);
                }
                else
                {
                    _robbersDone = true;
                }
                return;
            }
            if (_robbers.Count == 0)
            {
                if (_robbersAt > 0f && Mission.CurrentTime >= _robbersAt && _talk == null)
                {
                    SpawnRobbers();
                }
                return;
            }
            Agent leader = _robbers.FirstOrDefault(a => a.IsActive());
            if (leader == null)
            {
                _robbersDone = true;
                return;
            }
            float distance = leader.Position.AsVec2.Distance(_heroAgent.Position.AsVec2);
            if (distance > 3.2f)
            {
                foreach (Agent robber in _robbers.Where(a => a.IsActive()))
                {
                    Vec3 spot = OnGround(_heroAgent.Position + (robber.Position - _heroAgent.Position).NormalizedCopy() * 2.2f);
                    var world = new WorldPosition(Mission.Scene, UIntPtr.Zero, spot, false);
                    robber.SetScriptedPosition(ref world, false, Agent.AIScriptedFrameFlags.NoAttack);
                }
                return;
            }
            if (_talk == null)
            {
                _robbersDone = true;
                OpenRobberTalk(leader);
            }
        }

        private void SpawnRobbers()
        {
            int count = Math.Min(5, 2 + CompanyCount + (DikozemieRoads.Darkness >= 1f ? 1 : 0));
            Vec2 back = -_heroAgent.LookDirection.AsVec2.Normalized();
            Vec3 from = _heroAgent.Position + (back * 13f).ToVec3();
            for (int index = 0; index < count; index++)
            {
                DikozemieWalker robber = SpawnNear("thug", null, from, 1f + index * 1.2f, index == 0 ? "Громила" : "Грабитель");
                if (robber == null)
                {
                    continue;
                }
                robber.Kind = DikozemieWalkerKind.Thug;
                robber.Talkable = false;
                robber.Agent.SetMaximumSpeedLimit(0.7f, true);
                _robbers.Add(robber.Agent);
            }
            if (_robbers.Count == 0)
            {
                _robbersDone = true;
                return;
            }
            DikozemieLobbyShellIntegration.TraceButtonAction("town walk robbers count=" + _robbers.Count);
        }

        private void OpenRobberTalk(Agent leader)
        {
            if (!_walkers.TryGetValue(leader, out DikozemieWalker walker))
            {
                return;
            }
            walker.Talkable = true;
            OpenTalk(walker);
            walker.Talkable = false;
            int coins = DikozemieArmoryProgression.HeroResources;
            int take = Math.Max(2, coins / 4);
            int persuade = DikozemieRoads.Clamp(10 + Skill("Charm") / 2 + Skill("Roguery") / 4 +
                (DikozemieArmoryProgression.PlayerRating >= 300 ? 10 : 0), 3, 85);
            int dodge = DikozemieRoads.Clamp(20 + Skill("Athletics") / 3 + Skill("Roguery") / 4 - DikozemieRoads.Wound * 10, 5, 80);
            Answer("Из темноты выходят " + _robbers.Count + ". Тот, что повыше, поигрывает дубинкой: " +
                "«Тихо. Кошелёк сюда — и разойдёмся по-хорошему.»\n\n" + Purse, t =>
            {
                Add(t, "ОТДАТЬ КОШЕЛЁК", "-" + Math.Min(coins, take) + " МОН.", () => PayRobbers(Math.Min(coins, take)));
                Add(t, "ДРАТЬСЯ", _robbers.Count + " ПРОТИВ " + (1 + CompanyCount), () =>
                {
                    CloseTalk();
                    StartBrawl(_robbers.Where(a => a.IsActive()).ToList(), DikozemieBrawlKind.Robbers, 0);
                    Say("Грабители бросаются на вас! Стража может услышать шум.");
                });
                Add(t, "УГОВОРИТЬ", persuade + "% · ОБАЯНИЕ " + Skill("Charm"), () => PersuadeRobbers(persuade));
                Add(t, "БЕЖАТЬ", dodge + "% · АТЛЕТИКА " + Skill("Athletics"), () => FleeRobbers(dodge, take));
            });
        }

        private static int Skill(string id) => DikozemieHeroProfile.GetEffectiveSkill(id, 0);

        private void PayRobbers(int amount)
        {
            if (amount > 0)
            {
                DikozemieArmoryProgression.TrySpendHeroResources(amount, out _, "robbed", "грабители в городе");
            }
            DismissRobbers();
            Answer("Вы отдаёте монеты (-" + amount + "). Громила взвешивает кошель на ладони: «Умный. Живи.» " +
                "Тени растворяются в переулке.\n\n" + Purse, t => AddBye(t, "ДАЛЬШЕ"));
        }

        private void PersuadeRobbers(int chance)
        {
            if (_dice.Next(100) < chance)
            {
                DismissRobbers();
                Answer("«…да ну тебя.» Громила сплёвывает. «С арены? Ладно. Иди. Только по этой улице больше не ходи.»",
                    t => AddBye(t, "ДАЛЬШЕ"));
                return;
            }
            Answer("«Красиво поёшь.» Он ухмыляется. «А теперь — кошелёк.» Кольцо смыкается.", t =>
            {
                Add(t, "ДРАТЬСЯ", string.Empty, () =>
                {
                    CloseTalk();
                    StartBrawl(_robbers.Where(a => a.IsActive()).ToList(), DikozemieBrawlKind.Robbers, 0);
                });
                int coins = DikozemieArmoryProgression.HeroResources;
                Add(t, "ОТДАТЬ КОШЕЛЁК", "-" + Math.Max(0, Math.Min(coins, Math.Max(2, coins / 3))) + " МОН.",
                    () => PayRobbers(Math.Max(0, Math.Min(coins, Math.Max(2, coins / 3)))));
            });
        }

        private void FleeRobbers(int chance, int take)
        {
            if (_dice.Next(100) < chance)
            {
                DismissRobbers();
                CloseTalk();
                LeaveWalk(DikozemieWalkExit.Town, "Вы рванули в узкий проулок, перемахнули через забор и выскочили к воротам. " +
                    "Позади ругались и топали — но отстали.");
                return;
            }
            Answer("Вы бросаетесь в проулок — и упираетесь в стену. Сзади смеются.", t =>
            {
                Add(t, "ДРАТЬСЯ", string.Empty, () =>
                {
                    CloseTalk();
                    StartBrawl(_robbers.Where(a => a.IsActive()).ToList(), DikozemieBrawlKind.Robbers, 0);
                });
                int coins = DikozemieArmoryProgression.HeroResources;
                Add(t, "ОТДАТЬ КОШЕЛЁК", "-" + Math.Min(coins, take) + " МОН.", () => PayRobbers(Math.Min(coins, take)));
            });
        }

        private void DismissRobbers()
        {
            foreach (Agent robber in _robbers.Where(a => a.IsActive()))
            {
                SendAway(robber);
            }
        }

        private void SendAway(Agent agent)
        {
            if (agent == null || !agent.IsActive())
            {
                return;
            }
            agent.SetTeam(Team.Invalid, false);
            agent.SetTargetAgent(null);
            Vec2 away = (agent.Position - (_heroAgent?.Position ?? agent.Position)).AsVec2;
            away = away.LengthSquared > 0.01f ? away.Normalized() : new Vec2(1f, 0f);
            Vec3 spot = OnGround(agent.Position + (away * 25f).ToVec3());
            var world = new WorldPosition(Mission.Scene, UIntPtr.Zero, spot, false);
            agent.SetScriptedPosition(ref world, false, Agent.AIScriptedFrameFlags.NoAttack);
            agent.SetMaximumSpeedLimit(1f, true);
            _fadeOut[agent] = Mission.CurrentTime + 6f;
        }

        private readonly Dictionary<Agent, float> _fadeOut = new Dictionary<Agent, float>();

        // -- The fight

        private void StartBrawl(List<Agent> foes, DikozemieBrawlKind kind, int bet)
        {
            if (foes == null || foes.Count == 0 || _heroAgent == null)
            {
                return;
            }
            _brawl = kind;
            _brawlBet = bet;
            _brawlWon = false;
            _brawlResultAt = -1f;
            _guardsCame = false;
            _yielded.Clear();
            _brawlFoes.Clear();
            _brawlFriends.Clear();
            _brawlFoes.AddRange(foes);
            _brawlFriends.Add(_heroAgent);
            if (kind != DikozemieBrawlKind.Fists)
            {
                _brawlFriends.AddRange(_walkers.Values
                    .Where(w => w.Kind == DikozemieWalkerKind.Follower && w.Agent.IsActive())
                    .Select(w => w.Agent));
            }
            foreach (Agent foe in _brawlFoes)
            {
                if (_walkers.TryGetValue(foe, out DikozemieWalker walker))
                {
                    walker.Talkable = false;
                    if (walker.AtPoint)
                    {
                        Depart(walker);
                    }
                }
                foe.DisableScriptedMovement();
                foe.SetTeam(_rivalTeam, false);
                foe.SetMortalityState(Agent.MortalityState.Immortal);
                foe.SetWatchState(Agent.WatchState.Alarmed);
                if (kind != DikozemieBrawlKind.Fists)
                {
                    foe.WieldInitialWeapons();
                }
            }
            foreach (Agent friend in _brawlFriends)
            {
                friend.SetMortalityState(Agent.MortalityState.Immortal);
                if (friend != _heroAgent)
                {
                    friend.DisableScriptedMovement();
                    friend.SetWatchState(Agent.WatchState.Alarmed);
                    friend.WieldInitialWeapons();
                }
            }
            _brawlRetarget = 0.2f;
            _guardsAt = kind == DikozemieBrawlKind.Fists || _setup.Place != DikozemieWalkPlace.Streets
                ? -1f
                : Mission.CurrentTime + 7f + (float)_dice.NextDouble() * 5f;
            Mission.SetMissionMode(MissionMode.Battle, false);
            DikozemieLobbyShellIntegration.TraceButtonAction("town walk brawl " + kind + " foes=" + _brawlFoes.Count +
                " friends=" + _brawlFriends.Count + " bet=" + bet);
        }

        private void TickBrawl(float dt)
        {
            foreach (KeyValuePair<Agent, float> fade in _fadeOut.ToList())
            {
                if (Mission.CurrentTime >= fade.Value)
                {
                    _fadeOut.Remove(fade.Key);
                    if (fade.Key.IsActive())
                    {
                        fade.Key.FadeOut(false, true);
                    }
                }
            }
            if (!InBrawl)
            {
                return;
            }
            if (_brawlResultAt >= 0f)
            {
                if (Mission.CurrentTime >= _brawlResultAt)
                {
                    _brawlResultAt = -1f;
                    FinishBrawl();
                }
                return;
            }
            _brawlRetarget -= dt;
            if (_brawlRetarget <= 0f)
            {
                _brawlRetarget = 1f;
                Target(_brawlFoes, _brawlFriends);
                Target(_brawlFriends.Where(a => a != _heroAgent), _brawlFoes);
                // Tests on a machine without a player at the keyboard.
                if (Environment.GetEnvironmentVariable("DIKOZEMIE_TEST_ROAD_AUTOPLAY") == "1" &&
                    _heroAgent != null && _heroAgent.IsActive() && !_yielded.Contains(_heroAgent))
                {
                    if (_heroAgent.Controller != AgentControllerType.AI)
                    {
                        _heroAgent.Controller = AgentControllerType.AI;
                    }
                    Target(new[] { _heroAgent }, _brawlFoes);
                }
            }
            if (_guardsAt > 0f && !_guardsCame && Mission.CurrentTime >= _guardsAt)
            {
                CallGuards();
            }
            bool heroDown = _heroAgent == null || !_heroAgent.IsActive() || _yielded.Contains(_heroAgent);
            bool foesDown = _brawlFoes.All(a => _yielded.Contains(a) || !a.IsActive());
            if (heroDown || foesDown)
            {
                _brawlWon = !heroDown;
                _brawlResultAt = Mission.CurrentTime + 2.5f;
                foreach (Agent agent in _brawlFoes.Concat(_brawlFriends))
                {
                    if (agent != _heroAgent && agent.IsActive())
                    {
                        agent.SetTargetAgent(null);
                        agent.SetAutomaticTargetSelection(false);
                    }
                }
                Debug.Print("DikozemieOnline: town walk brawl over won=" + _brawlWon + " kind=" + _brawl,
                    0, Debug.DebugColor.Green);
            }
        }

        private void Target(IEnumerable<Agent> hunters, IEnumerable<Agent> prey)
        {
            List<Agent> targets = prey.Where(a => a.IsActive() && !_yielded.Contains(a)).ToList();
            foreach (Agent hunter in hunters)
            {
                if (hunter == null || !hunter.IsActive() || _yielded.Contains(hunter))
                {
                    continue;
                }
                Agent current = hunter.GetTargetAgent();
                if (current != null && current.IsActive() && !_yielded.Contains(current))
                {
                    continue;
                }
                Agent nearest = targets.OrderBy(t => t.Position.DistanceSquared(hunter.Position)).FirstOrDefault();
                hunter.SetWatchState(Agent.WatchState.Alarmed);
                hunter.SetAutomaticTargetSelection(false);
                hunter.SetTargetAgent(nearest);
            }
        }

        // The town watch heard the noise.
        private void CallGuards()
        {
            _guardsCame = true;
            var guards = _walkers.Values
                .Where(w => w.IsGuard && w.Agent.IsActive() && w.Agent.Position.DistanceSquared(_heroAgent.Position) < 45f * 45f)
                .OrderBy(w => w.Agent.Position.DistanceSquared(_heroAgent.Position))
                .Take(3)
                .ToList();
            if (guards.Count == 0)
            {
                return;
            }
            foreach (DikozemieWalker guard in guards)
            {
                if (guard.AtPoint)
                {
                    Depart(guard);
                }
                guard.Talkable = false;
                guard.Agent.DisableScriptedMovement();
                guard.Agent.SetTeam(_heroTeam, false);
                guard.Agent.SetMortalityState(Agent.MortalityState.Immortal);
                guard.Agent.WieldInitialWeapons();
                _brawlFriends.Add(guard.Agent);
            }
            Say("«Стража! Разойдись!» — " + (guards.Count == 1 ? "стражник бежит" : guards.Count + " стражника бегут") + " на шум.");
        }

        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon,
            in Blow blow, in AttackCollisionData attackCollisionData)
        {
            base.OnAgentHit(affectedAgent, affectorAgent, in affectorWeapon, in blow, in attackCollisionData);
            if (affectedAgent == null || !affectedAgent.IsHuman || _leaving || IsPeer(affectedAgent))
            {
                // Another hero's figure is only his picture here.
                return;
            }
            // Nobody dies in the town: the engine takes no health from an
            // immortal agent, so the walk takes it, down to a last point.
            if (affectedAgent.CurrentMortalityState == Agent.MortalityState.Immortal && blow.InflictedDamage > 0 &&
                Mission.Mode != MissionMode.Conversation)
            {
                // A club in a town brawl beats a man down; it is not a battle
                // axe. Three on one still ends fast, but it is a fight.
                float share = _brawl == DikozemieBrawlKind.Fists ? 0.8f : InBrawl ? 0.5f : 1f;
                affectedAgent.Health = Math.Max(1f, affectedAgent.Health - blow.InflictedDamage * share);
            }
            if (InBrawl && (_brawlFoes.Contains(affectedAgent) || _brawlFriends.Contains(affectedAgent)))
            {
                float share = _brawl == DikozemieBrawlKind.Fists ? FistYieldShare : YieldShare;
                if (!_yielded.Contains(affectedAgent) &&
                    affectedAgent.Health <= affectedAgent.HealthLimit * share)
                {
                    Yield(affectedAgent);
                }
                return;
            }
            // The hero struck a peaceful townsman: the watch.
            if (affectorAgent == _heroAgent && !_crime && _walkers.TryGetValue(affectedAgent, out DikozemieWalker walker) &&
                walker.Kind != DikozemieWalkerKind.Follower && !_fadeOut.ContainsKey(affectedAgent))
            {
                Crime(walker);
            }
        }

        private static readonly string[] Falls = { "act_fall_face_hold", "act_fall_body_back", "act_fall_chest_front" };

        // Beaten down: he goes to the floor and stays there until the fight
        // is over.
        private void Yield(Agent agent)
        {
            _yielded.Add(agent);
            agent.Health = Math.Max(agent.Health, 1f);
            agent.SetTargetAgent(null);
            agent.SetAutomaticTargetSelection(false);
            agent.TryToSheathWeaponInHand(Agent.HandIndex.MainHand, Agent.WeaponWieldActionType.Instant);
            agent.TryToSheathWeaponInHand(Agent.HandIndex.OffHand, Agent.WeaponWieldActionType.Instant);
            ActionIndexCache fall = ActionIndexCache.Create(Falls[_dice.Next(Falls.Length)]);
            if (fall != ActionIndexCache.act_none)
            {
                agent.SetActionChannel(0, in fall, true, (AnimFlags)0uL);
                _fallen.Add(agent);
            }
            if (agent != _heroAgent)
            {
                agent.SetTeam(Team.Invalid, false);
            }
            Debug.Print("DikozemieOnline: town walk brawl yield " + (agent == _heroAgent ? "hero" : agent.Name),
                0, Debug.DebugColor.Green);
        }

        private readonly HashSet<Agent> _fallen = new HashSet<Agent>();

        private void StandUp()
        {
            foreach (Agent agent in _fallen.Where(a => a.IsActive()))
            {
                agent.SetActionChannel(0, in ActionIndexCache.act_none, true, (AnimFlags)0uL);
            }
            _fallen.Clear();
        }

        private void FinishBrawl()
        {
            DikozemieBrawlKind kind = _brawl;
            bool won = _brawlWon;
            int down = _brawlFoes.Count(a => _yielded.Contains(a) || !a.IsActive());
            _brawl = DikozemieBrawlKind.None;
            Mission.SetMissionMode(MissionMode.StartUp, false);
            StandUp();
            if (kind != DikozemieBrawlKind.Fists)
            {
                // The beaten men limp away; the others with them.
                foreach (Agent foe in _brawlFoes.Where(a => a.IsActive()))
                {
                    SendAway(foe);
                }
            }
            if (kind == DikozemieBrawlKind.Fists)
            {
                Rearm(_heroAgent, _heroWeapons);
                Rearm(_fistOpponent, _opponentWeapons);
                if (_fistOpponent != null && _fistOpponent.IsActive())
                {
                    _fistOpponent.SetTeam(Team.Invalid, false);
                    if (_walkers.TryGetValue(_fistOpponent, out DikozemieWalker opponent))
                    {
                        opponent.Talkable = true;
                    }
                }
            }
            foreach (Agent friend in _brawlFriends.Where(a => a != _heroAgent && a.IsActive()))
            {
                friend.SetTargetAgent(null);
                friend.SetWatchState(Agent.WatchState.Patrolling);
                if (_walkers.TryGetValue(friend, out DikozemieWalker walker) && walker.IsGuard)
                {
                    // The watch goes back to its post.
                    friend.SetTeam(Team.Invalid, false);
                    walker.Talkable = true;
                    UsableMachine post = walker.Machine;
                    walker.Point = post != null ? FreePoint(post) : null;
                    if (walker.Point != null)
                    {
                        _taken.Add(walker.Point);
                        walker.AtPoint = false;
                        walker.NextThink = 0f;
                    }
                }
            }
            if (_heroAgent != null && _heroAgent.IsActive())
            {
                _heroAgent.Health = Math.Max(_heroAgent.Health, _heroAgent.HealthLimit * 0.2f);
                if (_heroAgent.Controller != AgentControllerType.Player)
                {
                    _heroAgent.Controller = AgentControllerType.Player;
                    _heroAgent.SetTargetAgent(null);
                }
            }
            string progress = DikozemieHeroProfile.RecordBattleResult(won, Math.Max(1, down));
            string text;
            string title;
            DikozemieWalkExit? exit = null;
            switch (kind)
            {
                case DikozemieBrawlKind.Fists:
                    title = won ? "ВЫ ВЫИГРАЛИ" : "ВЫ ПРОИГРАЛИ";
                    if (won)
                    {
                        DikozemieArmoryProgression.RefundHeroResources(_brawlBet * 2, out _, "bet", "кулачный бой в таверне");
                        text = "Задира поднимает руки: «Хватит, хватит!» Таверна ревёт. Вы сгребаете со стола " +
                            _brawlBet * 2 + " мон.";
                    }
                    else
                    {
                        text = "Пол таверны качнулся и ударил вас в спину. Задира сгребает ставку и хлопает вас по плечу: " +
                            "«Неплохо. Приходи ещё.»";
                    }
                    break;
                case DikozemieBrawlKind.Gang:
                    title = won ? "ПОБЕДА" : "ПОРАЖЕНИЕ";
                    if (won)
                    {
                        int loot = 3 + _dice.Next(4) * down;
                        DikozemieArmoryProgression.RefundHeroResources(loot, out _, "brawl", "драка с людьми главаря");
                        text = "Люди главаря разбегаются, придерживая ушибы. Один обронил кошель: +" + loot +
                            " мон. Главарь цедит сквозь зубы: «Запомню.»";
                    }
                    else
                    {
                        int lost = Math.Min(DikozemieArmoryProgression.HeroResources, Math.Max(2, DikozemieArmoryProgression.HeroResources / 5));
                        if (lost > 0)
                        {
                            DikozemieArmoryProgression.TrySpendHeroResources(lost, out _, "robbed", "побит людьми главаря");
                        }
                        DikozemieRoads.Hurt(1);
                        text = "Вас сбили с ног и обшарили карманы (-" + lost + " мон.). Главарь наклоняется: " +
                            "«Вежливость — дешевле.»";
                        exit = DikozemieWalkExit.Town;
                    }
                    break;
                default:
                    title = won ? "ПОБЕДА" : "ПОРАЖЕНИЕ";
                    if (won)
                    {
                        int loot = 2 + _dice.Next(3) * down;
                        DikozemieArmoryProgression.RefundHeroResources(loot, out _, "brawl", "ночные грабители");
                        text = "Грабители бегут, бросив дубинки. На мостовой звякает чей-то кошель: +" + loot + " мон." +
                            (_guardsCame ? " Стражник кивает вам: «Ловко. Иди, мы приберём.»" : string.Empty);
                    }
                    else
                    {
                        int lost = Math.Min(DikozemieArmoryProgression.HeroResources, Math.Max(2, DikozemieArmoryProgression.HeroResources / 4));
                        if (lost > 0)
                        {
                            DikozemieArmoryProgression.TrySpendHeroResources(lost, out _, "robbed", "ограбили ночью в городе");
                        }
                        DikozemieRoads.Hurt(1);
                        text = "Удар дубинкой — и мостовая уходит из-под ног. Очнулись вы у стены, без " + lost +
                            " мон. и с шишкой на затылке.";
                        exit = DikozemieWalkExit.Town;
                    }
                    DismissRobbers();
                    break;
            }
            DikozemieTravelState.Save();
            text += "\n\n" + Purse + (string.IsNullOrEmpty(progress) ? string.Empty : "\n" + progress);
            ShowResult(title, text, exit);
        }

        // A result in the conversation panel: one button.
        private void ShowResult(string title, string text, DikozemieWalkExit? exit)
        {
            _talk = new DikozemieWalkTalk { Name = title, Text = text };
            if (exit != null)
            {
                DikozemieWalkExit where = exit.Value;
                string note = title + ". " + text.Split('\n')[0];
                Add(_talk, "ОЧНУТЬСЯ", string.Empty, () => LeaveWalk(where, note));
            }
            else
            {
                Add(_talk, "ДАЛЬШЕ", string.Empty, CloseTalk);
            }
            TalkVersion++;
        }

        // -- Crime

        private void Crime(DikozemieWalker victim)
        {
            _crime = true;
            CloseTalk();
            victim.Talkable = false;
            SendAway(victim.Agent);
            var guards = _walkers.Values.Where(w => w.IsGuard && w.Agent.IsActive())
                .OrderBy(w => w.Agent.Position.DistanceSquared(_heroAgent.Position))
                .Take(2)
                .ToList();
            foreach (DikozemieWalker guard in guards)
            {
                if (guard.AtPoint)
                {
                    Depart(guard);
                }
                Vec3 spot = OnGround(_heroAgent.Position + (guard.Agent.Position - _heroAgent.Position).NormalizedCopy() * 1.5f);
                var world = new WorldPosition(Mission.Scene, UIntPtr.Zero, spot, false);
                guard.Agent.SetScriptedPosition(ref world, false, Agent.AIScriptedFrameFlags.NoAttack);
                guard.Agent.SetMaximumSpeedLimit(1f, true);
            }
            int fine = Math.Max(5, DikozemieArmoryProgression.HeroResources / 10);
            DikozemieLobbyShellIntegration.TraceButtonAction("town walk crime fine=" + fine);
            _talk = new DikozemieWalkTalk
            {
                Name = "СТРАЖА",
                Text = "«Стой! Руки!» Стражники хватают вас под локти: бить горожан в городе нельзя. " +
                    "Капитан выносит решение на месте: штраф " + fine + " мон. — или ночь в холодной.\n\n" + Purse
            };
            Add(_talk, "ЗАПЛАТИТЬ ШТРАФ", fine + " МОН.", () =>
            {
                if (!DikozemieArmoryProgression.TrySpendHeroResources(fine, out int coins, "fine", "драка в городе"))
                {
                    Answer("Денег не хватает (у вас " + coins + "). «В холодную его.»", t =>
                        Add(t, "В ХОЛОДНУЮ", string.Empty, Lockup));
                    return;
                }
                LeaveWalk(DikozemieWalkExit.Town, "Вы заплатили штраф за драку в городе (-" + fine +
                    "). Стража выпроводила вас за ворота и долго смотрела вслед.");
            });
            Add(_talk, "ОТСИДЕТЬ НОЧЬ", "В ХОЛОДНОЙ", Lockup);
            TalkVersion++;
        }

        private void Lockup()
        {
            float hours = Math.Max(6f, DikozemieRoads.HoursToMorning());
            DikozemieRoads.Advance(hours);
            DikozemieTravelState.Save();
            LeaveWalk(DikozemieWalkExit.Town, "Вы провели ночь в холодной на гнилой соломе. Утром стражник отпер дверь: " +
                "«Свободен. И чтоб больше без кулаков.» " + DikozemieRoads.ClockText + ".");
        }
    }
}
