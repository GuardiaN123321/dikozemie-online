using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;

namespace Dikozemie.Online
{
    // Stage 2 of the plan, «Дороги и передряги» (customer voices 2026-11-05):
    // man-catchers who sell a traveller to the nearest arena, two or three
    // bandits that are deadly for one man, ways out (pay, run, talk), bounty
    // hunting, tolls at bridges and gates, merchants and fellow travellers,
    // night and forest, wounds that slow the hero down.

    internal enum DikozemieRoadKind
    {
        Bandits,
        Catchers,
        Toll,
        Peddler,
        Travellers,
        Caravan,
        Purse,
        Cart,
        Fugitive,
        Lair
    }

    // What the player may do about an encounter.
    internal enum DikozemieRoadAction
    {
        Fight,
        Pay,
        GiveAll,
        Flee,
        Persuade,
        Surrender,
        Avoid,
        Trade,
        Join,
        Ask,
        Take,
        Pass,
        PayToll,
        Dodge,
        Ford,
        Attack,
        Leave
    }

    internal sealed class DikozemieRoadChoice
    {
        internal DikozemieRoadAction Action;
        internal string Label = string.Empty;
        internal string Detail = string.Empty;
    }

    internal sealed class DikozemieRoadEncounter
    {
        internal DikozemieRoadKind Kind;
        internal string Title = string.Empty;
        internal string Text = string.Empty;
        // One line in red under the text: how dangerous this is.
        internal string Threat = string.Empty;
        internal string Culture = "empire";
        internal bool Forest;
        internal bool Night;
        // Scouting saw them first: they can be gone round.
        internal bool Spotted;
        // Talking failed: the price went up, no more talk or running.
        internal bool Haggled;
        // A toll at a bridge: there is a ford downstream.
        internal bool Bridge;
        // Running failed: they surround the hero.
        internal bool Cornered;
        internal int Price;
        internal int Reward;
        internal int Seed;
        internal string Role = string.Empty;
        internal string TownId = string.Empty;
        // Stage 6: the castle or town whose land a toll is (a clan's toll
        // goes into the clan's treasury).
        internal string Land = string.Empty;
        internal List<DikozemieRoadFighter> Foes = new List<DikozemieRoadFighter>();
        // People who are not foes but are shown: a merchant, pilgrims.
        internal List<DikozemieRoadFighter> Figures = new List<DikozemieRoadFighter>();
    }

    // What came of a choice or a fight, for the card on the map.
    internal sealed class DikozemieRoadOutcome
    {
        internal string Title = string.Empty;
        internal string Text = string.Empty;
        internal bool Good;
        // The man-catchers took the hero: the map opens the arena town.
        internal bool Captured;
        // A beaten bandit can be bound and sold to an arena.
        internal bool CanBind;
        internal string BindName = string.Empty;
        internal int BindReward;
    }

    internal static class DikozemieRoads
    {
        // ---- Clock --------------------------------------------------------

        // A game hour passes every ~8 s of travel: a long walk across a
        // kingdom takes a day or two, as in the singleplayer game.
        internal const float HoursPerTravelSecond = 0.12f;
        internal const float StartHour = 8f;

        private static readonly Random Dice = new Random();

        private static DikozemieTravelSave Save => DikozemieTravelState.Current;

        // The word for a hero or a heroine: «связанным» / «связанной».
        internal static string G(string male, string female) =>
            DikozemieHeroProfile.IsCreated && DikozemieHeroProfile.Snapshot.IsFemale ? female : male;

        internal static float Hours => Save?.Hours > 0f ? Save.Hours : StartHour;

        internal static int Day => (int)(Hours / 24f) + 1;

        internal static float HourOfDay => Hours % 24f;

        internal static bool IsNight
        {
            get
            {
                float hour = HourOfDay;
                return hour >= 21f || hour < 5f;
            }
        }

        // 0 by day, up to 1 deep at night: the map darkens with it.
        internal static float Darkness
        {
            get
            {
                float hour = HourOfDay;
                if (hour >= 22f || hour < 4f) return 1f;
                if (hour >= 19f) return (hour - 19f) / 3f;
                if (hour < 7f) return 1f - (hour - 4f) / 3f;
                return 0f;
            }
        }

        internal static string ClockText
        {
            get
            {
                float hour = HourOfDay;
                int whole = (int)hour;
                int minutes = (int)((hour - whole) * 60f) / 10 * 10;
                string part = IsNight ? "ночь" : hour < 11f ? "утро" : hour < 18f ? "день" : "вечер";
                return "День " + Day + " · " + whole.ToString("00") + ":" + minutes.ToString("00") + " · " + part;
            }
        }

        internal static void Advance(float hours)
        {
            DikozemieTravelSave save = Save;
            if (save == null || hours <= 0f)
            {
                return;
            }
            if (save.Hours <= 0f)
            {
                save.Hours = StartHour;
            }
            save.Hours += hours;
            HealOverTime(save, hours);
            if (save.FellowUntil > 0f && save.Hours >= save.FellowUntil && !string.IsNullOrEmpty(save.FellowRole))
            {
                string name = save.FellowName;
                ClearFellow(save);
                Note(name + " получил своё и ушёл: срок найма вышел.");
            }
        }

        // Up to the next morning (a night in a bed or in the stable).
        internal static float HoursToMorning()
        {
            float hour = HourOfDay;
            float until = hour < 7f ? 7f - hour : 31f - hour;
            return Math.Max(6f, until);
        }

        // ---- Notes ------------------------------------------------------

        // A line for the next map or town screen: what happened while the
        // player looked elsewhere (an arena fight of a captive).
        internal static void Note(string text)
        {
            DikozemieTravelSave save = Save;
            if (save == null || string.IsNullOrEmpty(text))
            {
                return;
            }
            save.RoadNote = string.IsNullOrEmpty(save.RoadNote) ? text : save.RoadNote + "\n" + text;
            DikozemieTravelState.Save();
        }

        internal static string TakeNote()
        {
            DikozemieTravelSave save = Save;
            if (save == null || string.IsNullOrEmpty(save.RoadNote))
            {
                return string.Empty;
            }
            string note = save.RoadNote;
            save.RoadNote = string.Empty;
            DikozemieTravelState.Save();
            return note;
        }

        // ---- Wounds -----------------------------------------------------

        internal const float LightWoundHours = 10f;
        internal const float HeavyWoundHours = 20f;
        internal const int TavernBedPrice = 1;
        internal const int HealerPrice = 3;

        internal static int Wound => Save?.Wound ?? 0;

        internal static void Hurt(int level)
        {
            DikozemieTravelSave save = Save;
            if (save == null || level <= 0)
            {
                return;
            }
            // A second beating on top of the first makes it worse.
            int wound = Math.Max(save.Wound, Math.Min(2, level));
            if (save.Wound > 0)
            {
                wound = Math.Max(wound, Math.Min(2, save.Wound + 1));
            }
            save.Wound = wound;
            save.WoundHours = wound >= 2 ? HeavyWoundHours : LightWoundHours;
            DikozemieTravelState.Save();
        }

        // Medicine shortens the healing: a hero who knows herbs mends faster.
        private static void HealOverTime(DikozemieTravelSave save, float hours)
        {
            if (save.Wound <= 0)
            {
                return;
            }
            float medicine = DikozemieHeroProfile.GetEffectiveSkill("Medicine", 0);
            save.WoundHours -= hours * (1f + medicine / 150f);
            while (save.Wound > 0 && save.WoundHours <= 0f)
            {
                save.Wound--;
                save.WoundHours += save.Wound >= 1 ? LightWoundHours : 0f;
            }
            if (save.Wound <= 0)
            {
                save.Wound = 0;
                save.WoundHours = 0f;
            }
        }

        internal static void HealOneLevel()
        {
            DikozemieTravelSave save = Save;
            if (save == null || save.Wound <= 0)
            {
                return;
            }
            save.Wound--;
            save.WoundHours = save.Wound > 0 ? LightWoundHours : 0f;
            DikozemieTravelState.Save();
        }

        internal static string WoundText
        {
            get
            {
                switch (Wound)
                {
                    case 1: return "Ушибы и порезы: идёте медленнее.";
                    case 2: return "Тяжело ранены: еле бредёте, в бою слабее.";
                    default: return string.Empty;
                }
            }
        }

        // The hero's share of health at the start of a road fight.
        internal static float HealthAtStart => Wound >= 2 ? 0.55f : Wound == 1 ? 0.8f : 1f;

        // ---- The hero ---------------------------------------------------

        internal static bool IsMounted
        {
            get
            {
                if (!DikozemieHeroProfile.IsCreated)
                {
                    return false;
                }
                Equipment equipment = DikozemieHeroProfile.CreateBattleEquipment(null);
                return equipment != null && equipment[EquipmentIndex.Horse].Item != null;
            }
        }

        private static int Skill(string id) => DikozemieHeroProfile.GetEffectiveSkill(id, 0);

        internal static int Rating => Math.Max(0, DikozemieArmoryProgression.PlayerRating);

        // Wounds slow the hero; a fed horse carries him faster on land, but
        // not with a pack; a caravan guard walks at the pace of the slowest.
        internal static float SpeedFactor(bool boat)
        {
            if (boat)
            {
                return 1f;
            }
            float factor = Wound >= 2 ? 0.55f : Wound == 1 ? 0.8f : 1f;
            float load = DikozemieEconomy.LoadShare;
            if (IsMounted && load <= 0.01f)
            {
                factor *= 1.3f;
            }
            // A laden pack train walks: pack animals set the pace, and a full
            // load slows it by a quarter (stage 3, trade on the map).
            if (load > 0.01f)
            {
                factor *= 1f - 0.25f * load;
            }
            return factor;
        }

        // ---- Company ------------------------------------------------------

        internal static bool HasCompany => !string.IsNullOrEmpty(Save?.FellowRole);

        internal static string CompanyText
        {
            get
            {
                DikozemieTravelSave save = Save;
                if (save == null || string.IsNullOrEmpty(save.FellowRole))
                {
                    return string.Empty;
                }
                string to = string.IsNullOrEmpty(save.FellowTo)
                    ? string.Empty
                    : " до " + DikozemieTown.DisplayName(DikozemieLobbyMapCatalog.Find(save.FellowTo));
                return "С вами: " + save.FellowName + to + ".";
            }
        }

        internal static List<DikozemieRoadFighter> CompanyFighters()
        {
            var result = new List<DikozemieRoadFighter>();
            DikozemieTravelSave save = Save;
            if (save == null || string.IsNullOrEmpty(save.FellowRole))
            {
                return result;
            }
            for (int index = 0; index < Math.Max(1, save.FellowCount); index++)
            {
                DikozemieRoadFighter fighter = DikozemieRoadFolk.Create(
                    save.FellowRole, save.FellowCulture, save.FellowSeed + index * 7919);
                if (fighter != null)
                {
                    result.Add(fighter);
                }
            }
            return result;
        }

        private static void ClearFellow(DikozemieTravelSave save)
        {
            save.FellowRole = string.Empty;
            save.FellowName = string.Empty;
            save.FellowTo = string.Empty;
            save.FellowCulture = string.Empty;
            save.FellowCount = 0;
            save.FellowUntil = 0f;
            save.FellowPay = 0;
        }

        // Called when the hero reaches a settlement on foot.
        internal static string OnArrive(DikozemieLobbyMapNode node)
        {
            DikozemieTravelSave save = Save;
            if (save == null || node == null || string.IsNullOrEmpty(save.FellowRole) ||
                string.IsNullOrEmpty(save.FellowTo))
            {
                return string.Empty;
            }
            string name = save.FellowName;
            if (string.Equals(save.FellowTo, node.Id, StringComparison.OrdinalIgnoreCase))
            {
                int pay = save.FellowPay;
                ClearFellow(save);
                if (pay > 0)
                {
                    DikozemieArmoryProgression.RefundHeroResources(pay, out _, "road", "плата попутчиков");
                }
                DikozemieTravelState.Save();
                return name + " дошли до места и благодарят вас" +
                    (pay > 0 ? ": сунули в руку " + pay + " мон." : ".");
            }
            if (DikozemieTown.IsTown(node))
            {
                string to = DikozemieTown.DisplayName(DikozemieLobbyMapCatalog.Find(save.FellowTo));
                ClearFellow(save);
                DikozemieTravelState.Save();
                return name + " остались здесь: им дальше в " + to + ", вам не по пути.";
            }
            return string.Empty;
        }

        // A caravan guard for a day: 6 coins, as an archer earned a day.
        // «Открыл ИП — никто не идёт работать» (customer): nobody signs on
        // with a nameless man.
        internal const int EscortPrice = 6;
        internal const int EscortRating = 60;
        internal const float EscortHours = 24f;

        internal static bool TryHireEscort(DikozemieLobbyMapNode town, out string message)
        {
            DikozemieTravelSave save = Save;
            if (save == null || town == null)
            {
                message = string.Empty;
                return false;
            }
            if (Rating < EscortRating)
            {
                message = "Охранники обозов смеются: «Кто ты " + G("такой", "такая") + ", чтобы за тобой ходить? " +
                    "Сначала пусть о тебе заговорят на арене.» (Нужна слава " + EscortRating +
                    ", у вас " + Rating + ".)";
                return false;
            }
            if (HasCompany)
            {
                message = "С вами уже идут: " + save.FellowName + ".";
                return false;
            }
            if (!DikozemieArmoryProgression.TrySpendHeroResources(EscortPrice, out int coins, "escort", "охранник обоза"))
            {
                message = "Охранник берёт " + EscortPrice + " мон. за день вперёд, у вас " + coins + ".";
                return false;
            }
            string culture = DikozemieRoadFolk.FolkCulture(town.Culture);
            int seed = DikozemieTown.StableHash(town.Id) ^ Day;
            IReadOnlyList<string> names = DikozemieTownCatalog.Names(culture, false);
            save.FellowRole = "escort_" + culture;
            save.FellowCulture = culture;
            save.FellowSeed = seed;
            save.FellowCount = 1;
            save.FellowName = "охранник " + names[(seed & int.MaxValue) % names.Count];
            save.FellowTo = string.Empty;
            save.FellowPay = 0;
            save.FellowUntil = Hours + EscortHours;
            DikozemieTravelState.Save();
            DikozemieLobbyShellIntegration.TraceButtonAction("road escort hired coinsLeft=" + coins);
            message = "Вы наняли охранника по имени " + save.FellowName.Replace("охранник ", string.Empty) +
                " на сутки (-" + EscortPrice + "). Он пойдёт рядом и встанет с вами против разбойников.";
            return true;
        }

        // ---- Captivity ----------------------------------------------------

        internal const int RansomPrice = 50;
        internal const int FreedomPurse = 15;

        internal static bool IsCaptive => !string.IsNullOrEmpty(Save?.CaptiveTown);

        internal static DikozemieLobbyMapNode CaptiveTown => DikozemieLobbyMapCatalog.Find(Save?.CaptiveTown);

        internal static DikozemieLobbyMapNode NearestTown(float x, float y)
        {
            DikozemieLobbyMapNode best = null;
            float bestDistance = float.MaxValue;
            foreach (DikozemieLobbyMapNode node in DikozemieLobbyMapCatalog.Nodes)
            {
                if (node?.Position == null || !DikozemieTown.IsTown(node))
                {
                    continue;
                }
                float dx = node.Position.X - x;
                float dy = node.Position.Y - y;
                float distance = dx * dx + dy * dy;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = node;
                }
            }
            return best;
        }

        // The catchers drive the hero to the nearest arena town.
        internal static DikozemieLobbyMapNode Capture() =>
            Save == null ? null : CaptureIn(NearestTown(Save.X, Save.Y));

        internal static string CaptiveBy => Save?.CaptiveBy ?? string.Empty;

        // Stage 5: the hero who beat this one on the road sold him to the
        // arena of that town.
        internal static DikozemieLobbyMapNode CaptureIn(DikozemieLobbyMapNode town, string by = "")
        {
            DikozemieTravelSave save = Save;
            if (save == null)
            {
                return null;
            }
            if (town?.Position == null)
            {
                return null;
            }
            float dx = town.Position.X - save.X;
            float dy = town.Position.Y - save.Y;
            float travel = Math.Max(8f, (float)Math.Sqrt(dx * dx + dy * dy) / 40f);
            save.CaptiveTown = town.Id;
            save.CaptiveBy = by ?? string.Empty;
            save.CaptiveFight = false;
            save.EscapeDay = 0;
            save.X = town.Position.X;
            save.Y = town.Position.Y;
            save.NodeId = town.Id;
            save.HasTarget = false;
            save.Boat = false;
            save.PassengerTo = string.Empty;
            Advance(travel);
            DikozemieTravelState.Save();
            DikozemieLobbyShellIntegration.TraceButtonAction("road captured town=" + town.Id);
            return town;
        }

        internal static void PrepareCaptiveFight()
        {
            DikozemieTravelSave save = Save;
            if (save != null && IsCaptive)
            {
                save.CaptiveFight = true;
                DikozemieTravelState.Save();
            }
        }

        // The arena told the client how the captive's fight ended.
        internal static void ObserveArenaSeries(bool victory)
        {
            DikozemieTravelSave save = Save;
            if (save == null || !save.CaptiveFight || !IsCaptive)
            {
                return;
            }
            string town = DikozemieTown.DisplayName(CaptiveTown);
            save.CaptiveFight = false;
            save.CaptiveTown = string.Empty;
            if (victory)
            {
                DikozemieArmoryProgression.RefundHeroResources(FreedomPurse, out _, "road", "свобода на арене");
                Note("Толпа ревела. Распорядитель арены " + town + " снял с вас ошейник и отдал долю ставок: +" +
                    FreedomPurse + " мон. Вы свободны.");
            }
            else
            {
                int coins = DikozemieArmoryProgression.HeroResources;
                int taken = coins * 30 / 100;
                if (taken > 0)
                {
                    DikozemieArmoryProgression.TrySpendHeroResources(taken, out _, "robbed",
                        "пленник проиграл на арене");
                }
                Hurt(1);
                Note("Вы проиграли бой. Распорядитель забрал " + taken + " мон. «за кормёжку» и велел " +
                    "вышвырнуть вас за ворота " + town + ". Вы свободны, но избиты.");
            }
            DikozemieTravelState.Save();
            DikozemieLobbyShellIntegration.TraceButtonAction("road captive arena victory=" + victory);
        }

        // The search was cancelled or never started.
        internal static void CancelCaptiveFight()
        {
            DikozemieTravelSave save = Save;
            if (save != null && save.CaptiveFight)
            {
                save.CaptiveFight = false;
                DikozemieTravelState.Save();
            }
        }

        internal static bool TryRansom(out string message)
        {
            DikozemieTravelSave save = Save;
            if (save == null || !IsCaptive)
            {
                message = string.Empty;
                return false;
            }
            if (!DikozemieArmoryProgression.TrySpendHeroResources(RansomPrice, out int coins, "ransom", "выкуп из плена"))
            {
                message = "Распорядитель пересчитывает ваш кошель: " + coins + " мон. «Мало. Свобода стоит " +
                    RansomPrice + ".»";
                return false;
            }
            save.CaptiveTown = string.Empty;
            save.CaptiveFight = false;
            DikozemieTravelState.Save();
            message = "Вы выкупились за " + RansomPrice + " мон. Ворота открыты.";
            return true;
        }

        internal static int EscapeChance =>
            Clamp(15 + Skill("Roguery") / 3 + Skill("Athletics") / 5 + (IsNight ? 15 : 0) - Wound * 10, 5, 75);

        internal static bool EscapeTriedToday => Save != null && Save.EscapeDay == Day;

        internal static bool TryEscape(out string message)
        {
            DikozemieTravelSave save = Save;
            if (save == null || !IsCaptive)
            {
                message = string.Empty;
                return false;
            }
            int chance = EscapeChance;
            save.EscapeDay = Day;
            bool success = Dice.Next(100) < chance;
            if (success)
            {
                save.CaptiveTown = string.Empty;
                save.CaptiveFight = false;
                Advance(HoursToMorning() * 0.5f);
                message = "Вы перетёрли верёвку о камень и ушли через сточную канаву. Свободны!";
            }
            else
            {
                Hurt(2);
                message = "Стража поймала вас у стены и избила палками. До завтра бежать не выйдет.";
            }
            DikozemieTravelState.Save();
            DikozemieLobbyShellIntegration.TraceButtonAction("road captive escape success=" + success +
                " chance=" + chance);
            return success;
        }

        // ---- Bounties -----------------------------------------------------

        internal sealed class Bounty
        {
            internal string TownId = string.Empty;
            internal string Name = string.Empty;
            internal string Role = string.Empty;
            internal string GangRole = string.Empty;
            internal string Culture = "empire";
            internal int Gang;
            internal int Reward;
            internal int Seed;
            internal float X;
            internal float Y;
            internal string Near = string.Empty;
        }

        private static readonly string[] Nicknames =
        {
            "Кривой", "Рыжий", "Резаный", "Хромой", "Волк", "Чёрный", "Сова", "Мясник",
            "Беззубый", "Лис", "Ворон", "Косой", "Тихий", "Бешеный"
        };

        private static string ChiefRole(string culture)
        {
            switch (DikozemieRoadFolk.FolkCulture(culture))
            {
                case "battania": return "forest_chief";
                case "khuzait": return "steppe_chief";
                case "aserai": return "desert_chief";
                case "sturgia": return "sea_chief";
                default: return "mountain_chief";
            }
        }

        // Today's wanted man of a town: the same for everyone who asks today.
        internal static Bounty OfferAt(DikozemieLobbyMapNode town)
        {
            if (town?.Position == null || !DikozemieTown.IsTown(town))
            {
                return null;
            }
            int seed = (DikozemieTown.StableHash(town.Id) * 31 + Day * 7349) & int.MaxValue;
            var random = new Random(seed);
            string culture = DikozemieRoadFolk.FolkCulture(town.Culture);
            string chief = ChiefRole(culture);
            IReadOnlyList<string> names = DikozemieTownCatalog.Names(culture, false);
            var bounty = new Bounty
            {
                TownId = town.Id,
                Culture = culture,
                Role = chief,
                GangRole = chief.Replace("_chief", string.Empty),
                Gang = 1 + random.Next(2),
                Seed = seed,
                Name = names[random.Next(names.Count)] + " " + Nicknames[random.Next(Nicknames.Length)]
            };
            bounty.Reward = 30 + bounty.Gang * 8;
            // A hideout off the roads, half a day to a day from the town.
            for (int attempt = 0; attempt < 60; attempt++)
            {
                double angle = random.NextDouble() * Math.PI * 2.0;
                float distance = 380f + (float)random.NextDouble() * 520f;
                float x = town.Position.X + (float)Math.Cos(angle) * distance;
                float y = town.Position.Y + (float)Math.Sin(angle) * distance;
                byte cell = DikozemieTravelTerrain.AtAtlas(x, y);
                if ((cell & (DikozemieTravelTerrain.Water | DikozemieTravelTerrain.Mountain |
                             DikozemieTravelTerrain.Road | DikozemieTravelTerrain.Port)) != 0)
                {
                    continue;
                }
                // Away from every settlement: a hideout, not a suburb.
                bool crowded = DikozemieLobbyMapCatalog.Nodes.Any(node => node?.Position != null &&
                    (node.Position.X - x) * (node.Position.X - x) + (node.Position.Y - y) * (node.Position.Y - y) <
                    140f * 140f);
                if (crowded || DikozemieTravelRouter.Find(town.Position.X, town.Position.Y, x, y, false) == null)
                {
                    continue;
                }
                bounty.X = x;
                bounty.Y = y;
                DikozemieLobbyMapNode near = DikozemieLobbyMapCatalog.FindNearestNode(x, y);
                bounty.Near = DikozemieTown.DisplayName(near);
                return bounty;
            }
            return null;
        }

        internal static bool HasBounty => !string.IsNullOrEmpty(Save?.BountyName);

        internal static bool HasPrisoner => !string.IsNullOrEmpty(Save?.PrisonerName);

        internal static void Accept(Bounty bounty)
        {
            DikozemieTravelSave save = Save;
            if (save == null || bounty == null)
            {
                return;
            }
            save.BountyTown = bounty.TownId;
            save.BountyName = bounty.Name;
            save.BountyRole = bounty.Role;
            save.BountyGangRole = bounty.GangRole;
            save.BountyCulture = bounty.Culture;
            save.BountyGang = bounty.Gang;
            save.BountyReward = bounty.Reward;
            save.BountySeed = bounty.Seed;
            save.BountyX = bounty.X;
            save.BountyY = bounty.Y;
            DikozemieTravelState.Save();
            DikozemieLobbyShellIntegration.TraceButtonAction("road bounty taken town=" + bounty.TownId +
                " reward=" + bounty.Reward);
        }

        internal static bool IsNearLair(float x, float y)
        {
            DikozemieTravelSave save = Save;
            if (save == null || !HasBounty)
            {
                return false;
            }
            float dx = save.BountyX - x;
            float dy = save.BountyY - y;
            return dx * dx + dy * dy < 45f * 45f;
        }

        internal static string BountyText
        {
            get
            {
                DikozemieTravelSave save = Save;
                if (save == null)
                {
                    return string.Empty;
                }
                if (HasPrisoner)
                {
                    return "Пленник: " + save.PrisonerName + " — сдать на арену любого города (" +
                        save.PrisonerReward + " мон.).";
                }
                return HasBounty
                    ? "Розыск: " + save.BountyName + " — логово отмечено на карте, награда " +
                        save.BountyReward + " мон."
                    : string.Empty;
            }
        }

        // Where the prisoner is handed in: the full reward in the town that
        // posted him, half anywhere else.
        internal static int HandInReward(DikozemieLobbyMapNode town)
        {
            DikozemieTravelSave save = Save;
            if (save == null || !HasPrisoner || town == null)
            {
                return 0;
            }
            bool home = string.IsNullOrEmpty(save.PrisonerTown) ||
                string.Equals(save.PrisonerTown, town.Id, StringComparison.OrdinalIgnoreCase);
            return home ? save.PrisonerReward : Math.Max(1, save.PrisonerReward / 2);
        }

        internal static string HandIn(DikozemieLobbyMapNode town)
        {
            DikozemieTravelSave save = Save;
            int reward = HandInReward(town);
            if (save == null || reward <= 0)
            {
                return string.Empty;
            }
            string name = save.PrisonerName;
            DikozemieArmoryProgression.RefundHeroResources(reward, out int coins, "bounty", "пленник сдан на арену");
            save.PrisonerName = string.Empty;
            save.PrisonerReward = 0;
            save.PrisonerTown = string.Empty;
            DikozemieTravelState.Save();
            DikozemieLobbyShellIntegration.TraceButtonAction("road prisoner handed in reward=" + reward +
                " coins=" + coins);
            return "Стражники увели пленника — " + name + " — к распорядителю арены: в ближайший праздник " +
                "он будет драться за свою жизнь. Ваша награда: +" + reward + " мон.";
        }

        // ---- Tolls --------------------------------------------------------

        // A toll-keeper sits on three bridges of five; pontage was a penny a
        // man on foot, more with a horse or a cart.
        internal static bool IsBridgeManned(int bridge) =>
            bridge != 0 && (int)((uint)(bridge * 2654435761u) % 5u) < 3;

        internal static int TollPrice => IsMounted ? 2 : 1;

        // Stage 6: the lord of the land sets the toll; a clan of players may
        // take none at all, and its own people pass free.
        internal static int TollPriceAt(float x, float y)
        {
            DikozemieLand land = DikozemieClans.LandAt(x, y);
            if (land == null)
            {
                return TollPrice;
            }
            if (DikozemieClans.IsMyLand(land))
            {
                return 0;
            }
            return Math.Max(0, land.Bridge) * (IsMounted ? 2 : 1);
        }

        // The ledger's reference of a toll: a clan's land is credited to it.
        internal static string TollRef(string seat) =>
            DikozemieClans.LandOf(seat) != null ? "land:" + seat : string.Empty;

        // The gate of a town takes a coin from a rider, once a day.
        internal static string GateToll(DikozemieLobbyMapNode town)
        {
            DikozemieTravelSave save = Save;
            if (save == null || town == null || !DikozemieTown.IsTown(town) || !IsMounted)
            {
                return string.Empty;
            }
            string key = town.Id + "@" + Day;
            if (string.Equals(save.LastGate, key, StringComparison.Ordinal))
            {
                return string.Empty;
            }
            DikozemieLand land = DikozemieClans.LandOf(town.Id);
            int price = land == null ? 1 : DikozemieClans.IsMyLand(land) ? 0 : Math.Max(0, land.Bridge);
            if (price <= 0)
            {
                return land != null && DikozemieClans.IsMyLand(land)
                    ? "Стража у ворот салютует знамени вашего клана."
                    : string.Empty;
            }
            save.LastGate = key;
            DikozemieTravelState.Save();
            if (!DikozemieArmoryProgression.TrySpendHeroResources(price, out _, "toll", "пошлина у городских ворот",
                    TollRef(town.Id)))
            {
                return "У ворот с конного берут пошлину, но у вас пусто: стражник махнул рукой.";
            }
            return "У ворот взяли пошлину с конного: -" + price + " мон." +
                (land != null ? " Она идёт клану «" + land.Name + "»." : string.Empty);
        }

        // ---- Market on the road ----------------------------------------

        internal static bool PeddlerOpen { get; private set; }

        internal static int PeddlerSeed { get; private set; }

        internal static void OpenPeddler(bool open, int seed = 0)
        {
            PeddlerOpen = open;
            if (open)
            {
                PeddlerSeed = seed & int.MaxValue;
            }
        }

        // ---- Encounters ---------------------------------------------------

        private static string BanditRole(string culture, bool forest, bool hills, bool coast, Random random)
        {
            switch (DikozemieRoadFolk.FolkCulture(culture))
            {
                case "khuzait":
                    return random.Next(4) == 0 ? "pillager" : "steppe";
                case "aserai":
                    return "desert";
                case "battania":
                    return forest || random.Next(2) == 0 ? "forest" : "looter";
                case "sturgia":
                    return coast ? "sea" : forest ? "forest" : random.Next(3) == 0 ? "sea" : "looter";
                case "vlandia":
                    return hills ? "mountain" : forest ? "forest" : random.Next(3) == 0 ? "mountain" : "looter";
                default:
                    return forest ? "forest" : hills ? "mountain" : random.Next(3) == 0 ? "deserter" : "looter";
            }
        }

        private static int PricePerMan(string role)
        {
            switch (role)
            {
                case "looter": return 3;
                case "forest":
                case "mountain":
                case "desert": return 4;
                case "deserter": return 5;
                case "steppe":
                case "sea":
                case "pillager": return 6;
                default: return role.StartsWith("catcher", StringComparison.Ordinal) ? 10 : 6;
            }
        }

        internal static string Plural(string role, int count)
        {
            DikozemieRoadRole data = DikozemieRoadFolk.Role(role);
            if (data == null)
            {
                return NumberWord(count) + " человек";
            }
            return NumberWord(count) + " " +
                (string.IsNullOrEmpty(data.PluralGenitive) ? data.Plural : data.PluralGenitive);
        }

        internal static string NumberWord(int count)
        {
            switch (count)
            {
                case 1: return "один";
                case 2: return "двое";
                case 3: return "трое";
                case 4: return "четверо";
                default: return count.ToString();
            }
        }

        internal static string ThreatText(int foes, int allies)
        {
            int side = 1 + allies;
            if (foes == 1)
            {
                return side > 1
                    ? "Он один, вас " + NumberWord(side) + ". Он это видит."
                    : "Один на один. Исход решит сталь.";
            }
            if (foes >= side + 2)
            {
                return "Их " + NumberWord(foes) + ", вас " + NumberWord(side) + ": в открытом бою не выстоять.";
            }
            if (foes > side)
            {
                return "Их " + NumberWord(foes) + ", вас " + NumberWord(side) +
                    ": один отвлекает, другой бьёт в спину. Смертельно опасно.";
            }
            return foes == side
                ? "Силы равны. Исход решит сталь."
                : "Вас больше. Они это видят.";
        }

        private static List<DikozemieRoadFighter> Gang(string role, int count, string culture, int seed)
        {
            var list = new List<DikozemieRoadFighter>();
            // A known fighter of the arena meets tougher men on the road.
            int raiders = Rating >= 400 ? 2 : Rating >= 150 ? 1 : 0;
            for (int index = 0; index < count; index++)
            {
                string actual = role;
                if (index < raiders && DikozemieRoadFolk.Role(role + "_raider") != null)
                {
                    actual = role + "_raider";
                }
                DikozemieRoadFighter fighter = DikozemieRoadFolk.Create(actual, culture, seed + index * 7919);
                if (fighter != null)
                {
                    fighter.IsLeader = index == 0;
                    list.Add(fighter);
                }
            }
            return list;
        }

        // A card drawn on a road. «Если идёшь по дороге, то обязательно
        // тянешь карту события» (customer, 2026-10-31).
        internal static DikozemieRoadEncounter DrawRoad(float x, float y, bool forest, bool hills, bool coast)
        {
            int seed = Dice.Next(int.MaxValue / 2);
            var random = new Random(seed);
            DikozemieLobbyMapNode near = DikozemieLobbyMapCatalog.FindNearestNode(x, y);
            string culture = DikozemieRoadFolk.FolkCulture(near?.Culture);
            bool night = IsNight;
            bool company = HasCompany;
            var weights = new List<KeyValuePair<DikozemieRoadKind, float>>
            {
                Weight(DikozemieRoadKind.Bandits, 30f * (night ? 1.6f : 1f) * (company ? 0.6f : 1f)),
                Weight(DikozemieRoadKind.Catchers, IsCaptive ? 0f : 12f * (night ? 1.6f : 1f) * (company ? 0.5f : 1f)),
                Weight(DikozemieRoadKind.Toll, 6f * (night ? 0.4f : 1f)),
                Weight(DikozemieRoadKind.Peddler, 10f * (night ? 0.2f : 1f)),
                Weight(DikozemieRoadKind.Travellers, company ? 0f : 10f * (night ? 0.3f : 1f)),
                Weight(DikozemieRoadKind.Caravan, 7f * (night ? 0.3f : 1f)),
                Weight(DikozemieRoadKind.Purse, 4f),
                Weight(DikozemieRoadKind.Cart, night ? 0f : 6f),
                Weight(DikozemieRoadKind.Fugitive, HasPrisoner ? 0f : 5f)
            };
            // Stage 6: a clan's patrols ride the roads of its land.
            if (DikozemieClans.LandAt(x, y)?.Patrol == true)
            {
                for (int index = 0; index < weights.Count; index++)
                {
                    if (weights[index].Key == DikozemieRoadKind.Bandits || weights[index].Key == DikozemieRoadKind.Catchers)
                    {
                        weights[index] = Weight(weights[index].Key, weights[index].Value / 3f);
                    }
                }
            }
            DikozemieRoadKind kind = Pick(weights, random);
            return Build(kind, culture, forest, hills, coast, night, seed, x, y);
        }

        // Off the roads: rarer, but nobody hears a cry for help.
        internal static DikozemieRoadEncounter DrawWild(float x, float y, bool forest, bool hills, bool coast)
        {
            float chance = (forest ? 0.18f : 0.2f) * (IsNight ? 1.5f : 1f);
            if (Dice.NextDouble() >= chance)
            {
                return null;
            }
            int seed = Dice.Next(int.MaxValue / 2);
            DikozemieLobbyMapNode near = DikozemieLobbyMapCatalog.FindNearestNode(x, y);
            DikozemieRoadEncounter card = Build(DikozemieRoadKind.Bandits,
                DikozemieRoadFolk.FolkCulture(near?.Culture), forest, hills, coast, IsNight, seed, x, y);
            card.Title = forest ? "ЗАСАДА В ЛЕСУ" : "ЗАСАДА";
            card.Text = (forest
                ? "Ветка хрустнула не под вашей ногой. Из-за деревьев выходят "
                : "Вдали от дорог чужаков не ждут. Навстречу поднимаются ") +
                Plural(card.Role, card.Foes.Count) + ".";
            return card;
        }

        // The toll-keeper of a bridge and his two guards.
        internal static DikozemieRoadEncounter BuildBridge(float x, float y, int bridge)
        {
            DikozemieLobbyMapNode near = DikozemieLobbyMapCatalog.FindNearestNode(x, y);
            string culture = DikozemieRoadFolk.FolkCulture(near?.Culture);
            int seed = (bridge * 7919 + Day) & int.MaxValue;
            DikozemieTownInfo info = DikozemieTownCatalog.Get(near?.Id);
            string seat = DikozemieClans.SeatOf(near);
            DikozemieLand land = DikozemieClans.LandOf(seat);
            int price = TollPriceAt(x, y);
            if (land != null && price <= 0)
            {
                // A clan's own bridge, or a lord who takes no toll: no stop.
                return null;
            }
            var card = new DikozemieRoadEncounter
            {
                Kind = DikozemieRoadKind.Toll,
                Bridge = true,
                Culture = culture,
                Night = IsNight,
                Seed = seed,
                Price = price,
                Role = "guard_" + culture,
                Title = "МОСТ · МЫТАРЬ",
                Land = seat
            };
            card.Foes = Gang(card.Role, 2, culture, seed);
            card.Text = land != null
                ? "На мосту будка, над ней знамя клана «" + land.Name + "», и двое стражников. Мытарь протягивает ладонь: " +
                  "«Мостовщина. С пешего " + land.Bridge + ", с конного " + land.Bridge * 2 + ".»"
                : "На мосту будка и двое стражников" +
                  (string.IsNullOrEmpty(info.Clan) ? string.Empty : " клана " + info.Clan) +
                  ". Мытарь протягивает ладонь: «Мостовщина. С пешего монета, с конного две.»";
            return card;
        }

        internal static DikozemieRoadEncounter BuildLair()
        {
            DikozemieTravelSave save = Save;
            if (save == null || !HasBounty)
            {
                return null;
            }
            int gang = save.BountyGang - (IsNight ? 1 : 0);
            var card = new DikozemieRoadEncounter
            {
                Kind = DikozemieRoadKind.Lair,
                Culture = save.BountyCulture,
                Night = IsNight,
                Seed = save.BountySeed,
                Role = save.BountyRole,
                TownId = save.BountyTown,
                Reward = save.BountyReward,
                Title = "ЛОГОВО: " + save.BountyName.ToUpperInvariant()
            };
            DikozemieRoadFighter chief = DikozemieRoadFolk.Create(save.BountyRole, save.BountyCulture,
                save.BountySeed, save.BountyName);
            if (chief != null)
            {
                chief.IsLeader = true;
                card.Foes.Add(chief);
            }
            card.Foes.AddRange(Gang(save.BountyGangRole, Math.Max(0, gang), save.BountyCulture, save.BountySeed + 101));
            card.Text = "Дым костра, голоса, звон котелка. " + save.BountyName + " здесь" +
                (gang > 0 ? ", с ним " + Plural(save.BountyGangRole, gang) : ", один") + "." +
                (IsNight ? " Ночь: часть шайки спит, часовой дремлет." : string.Empty);
            card.Threat = ThreatText(card.Foes.Count, CompanyFighters().Count) +
                " Атаман — бывалый рубака.";
            return card;
        }

        private static KeyValuePair<DikozemieRoadKind, float> Weight(DikozemieRoadKind kind, float weight) =>
            new KeyValuePair<DikozemieRoadKind, float>(kind, weight);

        private static DikozemieRoadKind Pick(List<KeyValuePair<DikozemieRoadKind, float>> weights, Random random)
        {
            float total = weights.Sum(pair => pair.Value);
            double roll = random.NextDouble() * total;
            foreach (KeyValuePair<DikozemieRoadKind, float> pair in weights)
            {
                roll -= pair.Value;
                if (roll <= 0)
                {
                    return pair.Key;
                }
            }
            return weights[0].Key;
        }

        internal static DikozemieRoadEncounter Build(
            DikozemieRoadKind kind, string culture, bool forest, bool hills, bool coast, bool night,
            int seed, float x, float y)
        {
            var random = new Random(seed);
            var card = new DikozemieRoadEncounter
            {
                Kind = kind,
                Culture = culture,
                Forest = forest,
                Night = night,
                Seed = seed
            };
            int allies = CompanyFighters().Count;
            switch (kind)
            {
                case DikozemieRoadKind.Bandits:
                {
                    string role = BanditRole(culture, forest, hills, coast, random);
                    int count = role == "steppe" || role == "pillager" || role == "deserter"
                        ? 2
                        : 2 + (random.Next(2) == 0 || night ? 1 : 0);
                    card.Role = role;
                    card.Foes = Gang(role, count, culture, seed);
                    card.Price = count * PricePerMan(role);
                    card.Title = role == "looter" ? "МАРОДЁРЫ" : role == "deserter" ? "ДЕЗЕРТИРЫ" :
                        DikozemieRoadFolk.Role(role)?.Plural.ToUpperInvariant() ?? "РАЗБОЙНИКИ";
                    int pack = DikozemieEconomy.CargoValue;
                    card.Price += pack / 10;
                    card.Text = (night ? "В темноте у дороги кто-то шевельнулся. " : string.Empty) +
                        "Путь перегораживают " + Plural(role, count) + ". Старший поигрывает оружием: " +
                        (pack > 0
                            ? "«Вьюки-то тяжёлые. Десятую часть товара монетой — или останешься тут вместе с ними.»"
                            : "«Кошелёк — или останешься тут.»");
                    break;
                }
                case DikozemieRoadKind.Catchers:
                {
                    int count = 3;
                    card.Role = "catcher_" + DikozemieRoadFolk.FolkCulture(culture);
                    card.Foes = Gang(card.Role, count, culture, seed);
                    card.Price = count * PricePerMan(card.Role);
                    card.Title = "ЛОВЦЫ ЛЮДЕЙ";
                    card.Text = "Трое с дубинами и верёвкой обходят вас с боков. «" + G("Крепкий", "Крепкая") +
                        ". На арене за " + G("такого", "такую") + " хорошо дадут. Пойдёшь " + G("сам", "сама") +
                        " — бить не будем.»";
                    break;
                }
                case DikozemieRoadKind.Toll:
                    card.Title = "ЗАСТАВА";
                    card.Price = TollPrice;
                    card.Role = "guard_" + DikozemieRoadFolk.FolkCulture(culture);
                    card.Foes = Gang(card.Role, 2, culture, seed);
                    card.Text = "Рогатка поперёк дороги и двое стражников лорда. «Дорожный сбор: с пешего " +
                        "монета, с конного две.»";
                    break;
                case DikozemieRoadKind.Peddler:
                    card.Title = "СТРАНСТВУЮЩИЙ ТОРГОВЕЦ";
                    card.Figures.Add(DikozemieRoadFolk.Create("merchant_" + DikozemieRoadFolk.FolkCulture(culture),
                        culture, seed));
                    card.Text = "Навстречу тащится мул с тюками, рядом хозяин. «Иголки, ножи, соль, обмотки! " +
                        "Дороже, чем в городе, — так и город далеко.»";
                    break;
                case DikozemieRoadKind.Travellers:
                {
                    DikozemieLobbyMapNode town = TownAhead(x, y, random);
                    card.TownId = town?.Id ?? string.Empty;
                    card.Role = "pilgrim_" + DikozemieRoadFolk.FolkCulture(culture);
                    card.Figures = Gang(card.Role, 2, culture, seed);
                    IReadOnlyList<string> pilgrimNames = DikozemieTownCatalog.Names(culture, false);
                    foreach (DikozemieRoadFighter pilgrim in card.Figures)
                    {
                        pilgrim.Name = "Паломник " + pilgrimNames[random.Next(pilgrimNames.Count)];
                    }
                    card.Reward = 2;
                    card.Title = "ПАЛОМНИКИ";
                    card.Text = "Двое путников с посохами идут " +
                        (town == null ? "в ближайший город" : "в " + DikozemieTown.DisplayName(town)) +
                        ". «Пойдём вместе? Втроём разбойник не тронет, а тронет — отобьёмся.»";
                    break;
                }
                case DikozemieRoadKind.Caravan:
                    card.Title = "КУПЕЧЕСКИЙ ОБОЗ";
                    card.Text = "Скрипят телеги, охрана косится на вас. Вы пристроились в хвост и прошли " +
                        "опасный участок под их защитой.";
                    break;
                case DikozemieRoadKind.Purse:
                    card.Title = "КОШЕЛЬ НА ДОРОГЕ";
                    card.Reward = 2 + random.Next(4);
                    card.Text = "В пыли у обочины лежит чей-то кошель. Вокруг никого.";
                    break;
                case DikozemieRoadKind.Cart:
                    card.Title = "ТЕЛЕГА В КАНАВЕ";
                    card.Reward = 2;
                    card.Text = "Крестьянин просит помочь вытащить телегу из канавы и обещает заплатить.";
                    break;
                case DikozemieRoadKind.Fugitive:
                {
                    string role = BanditRole(culture, forest, hills, coast, random);
                    card.Role = role;
                    card.Foes = Gang(role, 1, culture, seed);
                    card.Reward = 6 + random.Next(5);
                    IReadOnlyList<string> names = DikozemieTownCatalog.Names(culture, false);
                    string name = names[random.Next(names.Count)] + " " + Nicknames[random.Next(Nicknames.Length)];
                    if (card.Foes.Count > 0)
                    {
                        card.Foes[0].Name = name;
                        card.Foes[0].Health = 0.45f;
                    }
                    card.Title = "БЕГЛЫЙ РАЗБОЙНИК";
                    card.Text = "У ручья перевязывает руку " + name + " — его лицо нарисовано на каждом столбе: " +
                        "за него живым дают " + card.Reward + " мон. Он один и ранен, но оружие при нём.";
                    break;
                }
            }
            if (card.Foes.Count > 0 && kind != DikozemieRoadKind.Toll)
            {
                card.Threat = ThreatText(card.Foes.Count, allies);
                // «Когда навык прокачиваешь, ты чаще первый обнаруживаешь кого-то».
                card.Spotted = random.NextDouble() < SpotChance(night);
            }
            return card;
        }

        private static DikozemieLobbyMapNode TownAhead(float x, float y, Random random)
        {
            DikozemieTravelSave save = Save;
            DikozemieLobbyMapNode target = DikozemieLobbyMapCatalog.Find(save?.TargetNodeId);
            bool Far(DikozemieLobbyMapNode node) =>
                (node.Position.X - x) * (node.Position.X - x) + (node.Position.Y - y) * (node.Position.Y - y) >
                150f * 150f;
            if (target?.Position != null && DikozemieTown.IsTown(target) && Far(target))
            {
                return target;
            }
            var towns = DikozemieLobbyMapCatalog.Nodes
                .Where(node => node?.Position != null && DikozemieTown.IsTown(node) && Far(node))
                .OrderBy(node => (node.Position.X - x) * (node.Position.X - x) + (node.Position.Y - y) * (node.Position.Y - y))
                .Take(3)
                .ToList();
            return towns.Count == 0 ? null : towns[random.Next(towns.Count)];
        }

        internal static float SpotChance(bool night) =>
            Math.Min(0.8f, 0.15f + Skill("Scouting") / 200f) * (night ? 0.6f : 1f);

        // ---- Ways out -----------------------------------------------------

        private static bool FoesMounted(DikozemieRoadEncounter card) =>
            card.Foes.Count > 0 && card.Foes.All(foe => foe.IsMounted);

        internal static int FleeChance(DikozemieRoadEncounter card)
        {
            bool mounted = IsMounted;
            bool chasers = FoesMounted(card);
            int chance = mounted ? (chasers ? 40 : 70) : (chasers ? 5 : 35);
            chance += mounted ? Skill("Riding") / 4 : Skill("Athletics") / 4;
            chance += Skill("Scouting") / 10;
            if (card.Night) chance += 15;
            if (card.Forest && !mounted) chance += 10;
            chance -= Wound * 12;
            return Clamp(chance, 5, 95);
        }

        internal static string FleeDetail(DikozemieRoadEncounter card)
        {
            bool mounted = IsMounted;
            return FleeChance(card) + "% · " + (mounted ? "Езда " + Skill("Riding") : "Атлетика " + Skill("Athletics"));
        }

        // Why the chances are what they are, in one line.
        internal static string FactorsText(DikozemieRoadEncounter card)
        {
            // Only where running away is a choice.
            if (card.Foes.Count == 0 || card.Haggled ||
                (card.Kind != DikozemieRoadKind.Bandits && card.Kind != DikozemieRoadKind.Catchers))
            {
                return string.Empty;
            }
            var parts = new List<string>();
            if (IsMounted)
            {
                parts.Add(FoesMounted(card) ? "они тоже верхом" : "вы верхом: от пеших уйти легко");
            }
            else if (FoesMounted(card))
            {
                parts.Add("они верхом: пешему не убежать");
            }
            if (card.Night)
            {
                parts.Add("ночь: в темноте легче скрыться");
            }
            if (card.Forest && !IsMounted)
            {
                parts.Add("лес укроет беглеца");
            }
            if (Wound > 0)
            {
                parts.Add("раны мешают бежать");
            }
            return parts.Count == 0 ? string.Empty : char.ToUpper(parts[0][0]) + string.Join("; ", parts).Substring(1) + ".";
        }

        internal static int PersuadeChance(DikozemieRoadEncounter card)
        {
            bool catchers = card.Kind == DikozemieRoadKind.Catchers;
            int chance = (catchers ? 5 : 10) + Skill("Charm") / 2 + Skill("Leadership") / 5 +
                (catchers ? 0 : Skill("Roguery") / 4) + (Rating >= 300 ? 10 : 0) - (card.Night ? 5 : 0);
            return Clamp(chance, 3, 90);
        }

        internal static string PersuadeDetail(DikozemieRoadEncounter card) =>
            PersuadeChance(card) + "% · Обаяние " + Skill("Charm");

        internal static int DodgeChance => Clamp(15 + Skill("Roguery") / 3 + Skill("Athletics") / 5 +
            (IsNight ? 20 : 0), 5, 85);

        internal static List<DikozemieRoadChoice> Choices(DikozemieRoadEncounter card)
        {
            var list = new List<DikozemieRoadChoice>();
            int coins = DikozemieArmoryProgression.HeroResources;
            int allies = CompanyFighters().Count;
            void Add(DikozemieRoadAction action, string label, string detail = "") =>
                list.Add(new DikozemieRoadChoice { Action = action, Label = label, Detail = detail });
            switch (card.Kind)
            {
                case DikozemieRoadKind.Bandits:
                case DikozemieRoadKind.Catchers:
                    if (card.Spotted)
                    {
                        Add(DikozemieRoadAction.Avoid, "ОБОЙТИ СТОРОНОЙ", "+30 МИН.");
                    }
                    Add(DikozemieRoadAction.Fight, "ПРИНЯТЬ БОЙ",
                        (1 + allies) + " ПРОТИВ " + card.Foes.Count);
                    if (coins >= card.Price)
                    {
                        Add(DikozemieRoadAction.Pay, "ОТКУПИТЬСЯ", card.Price + " МОН.");
                    }
                    else if (card.Kind == DikozemieRoadKind.Bandits)
                    {
                        Add(DikozemieRoadAction.GiveAll, "ОТДАТЬ ВСЁ, ЧТО ЕСТЬ", coins + " МОН.");
                    }
                    if (!card.Haggled)
                    {
                        Add(DikozemieRoadAction.Flee, "БЕЖАТЬ", FleeDetail(card));
                        Add(DikozemieRoadAction.Persuade, "УГОВОРИТЬ", PersuadeDetail(card));
                    }
                    if (card.Kind == DikozemieRoadKind.Catchers)
                    {
                        Add(DikozemieRoadAction.Surrender, "СДАТЬСЯ", "на арену ближайшего города");
                    }
                    break;
                case DikozemieRoadKind.Toll:
                    Add(DikozemieRoadAction.PayToll, "ЗАПЛАТИТЬ", card.Price + " МОН.");
                    Add(DikozemieRoadAction.Dodge, "ПРОСКОЧИТЬ", DodgeChance + "% · Плутовство " + Skill("Roguery"));
                    Add(DikozemieRoadAction.Fight, "ПРОРВАТЬСЯ С БОЕМ", (1 + allies) + " ПРОТИВ " + card.Foes.Count);
                    if (card.Bridge)
                    {
                        Add(DikozemieRoadAction.Ford, "ИСКАТЬ БРОД", "+2 часа пути");
                    }
                    break;
                case DikozemieRoadKind.Peddler:
                    Add(DikozemieRoadAction.Trade, "ПОСМОТРЕТЬ ТОВАР", "+25% к цене");
                    Add(DikozemieRoadAction.Pass, "МИМО");
                    break;
                case DikozemieRoadKind.Travellers:
                    Add(DikozemieRoadAction.Join, "ИДТИ ВМЕСТЕ", "в бою встанут рядом");
                    Add(DikozemieRoadAction.Ask, "РАССПРОСИТЬ О ДОРОГЕ");
                    Add(DikozemieRoadAction.Pass, "МИМО");
                    break;
                case DikozemieRoadKind.Purse:
                case DikozemieRoadKind.Cart:
                    Add(DikozemieRoadAction.Take, card.Kind == DikozemieRoadKind.Purse ? "ВЗЯТЬ" : "ПОМОЧЬ",
                        "+" + card.Reward + " МОН.");
                    Add(DikozemieRoadAction.Pass, "МИМО");
                    break;
                case DikozemieRoadKind.Fugitive:
                    Add(DikozemieRoadAction.Fight, "СХВАТИТЬ", (1 + allies) + " ПРОТИВ 1 · награда " + card.Reward);
                    Add(DikozemieRoadAction.Pass, "НЕ СВЯЗЫВАТЬСЯ");
                    break;
                case DikozemieRoadKind.Lair:
                    Add(DikozemieRoadAction.Attack, "НАПАСТЬ", (1 + allies) + " ПРОТИВ " + card.Foes.Count);
                    Add(DikozemieRoadAction.Leave, "УЙТИ", "вернуться можно потом");
                    break;
                default:
                    Add(DikozemieRoadAction.Pass, "ИДТИ ДАЛЬШЕ");
                    break;
            }
            return list;
        }

        // A choice that needs no fight. Fights go through BuildFight.
        internal static DikozemieRoadOutcome Resolve(DikozemieRoadEncounter card, DikozemieRoadAction action)
        {
            var outcome = new DikozemieRoadOutcome { Title = card.Title };
            switch (action)
            {
                case DikozemieRoadAction.Avoid:
                    Advance(0.5f);
                    outcome.Good = true;
                    outcome.Text = "Вы сошли с дороги и обошли их кустами. Полчаса крюка — и ни одной царапины.";
                    break;
                case DikozemieRoadAction.Pay:
                    DikozemieArmoryProgression.TrySpendHeroResources(card.Price, out int left, "bribe", "откуп на дороге");
                    outcome.Text = "Вы отсчитали " + card.Price + " мон. Старший взвесил их на ладони и отступил: " +
                        "«Иди. Сегодня ты живой.» Осталось " + left + ".";
                    break;
                case DikozemieRoadAction.GiveAll:
                {
                    int coins = DikozemieArmoryProgression.HeroResources;
                    if (coins > 0)
                    {
                        DikozemieArmoryProgression.TrySpendHeroResources(coins, out _, "robbed",
                            "отдал всё на дороге");
                    }
                    // «Отдать всё»: the pack goes with the purse.
                    DikozemieEconomy.ReportCargoLost(1.0, "отдал всё на дороге");
                    if (coins * 3 < card.Price)
                    {
                        Hurt(1);
                        outcome.Text = "«И это всё?» Вас обшарили, забрали " + coins + " мон. и для науки " +
                            "намяли бока. Идти будет тяжелее.";
                    }
                    else
                    {
                        outcome.Text = "Вы вывернули кошель: " + coins + " мон. Разбойники хмыкнули и ушли в кусты.";
                    }
                    break;
                }
                case DikozemieRoadAction.Persuade:
                {
                    int chance = PersuadeChance(card);
                    if (Dice.Next(100) < chance)
                    {
                        outcome.Good = true;
                        outcome.Text = card.Kind == DikozemieRoadKind.Catchers
                            ? "«Я человек лорда, меня ищут.» Ловцы переглянулись: с людьми лорда связываться " +
                                "себе дороже. Они ушли."
                            : "Вы заговорили на их языке, вспомнили общих знакомых и пообещали не помнить лиц. " +
                                "Старший сплюнул и махнул рукой: «Проваливай.»";
                    }
                    else
                    {
                        card.Haggled = true;
                        card.Price = card.Price * 3 / 2;
                        outcome.Title = string.Empty;
                        outcome.Text = "«Красиво поёшь.» Они смеются и подходят ближе. Цена выросла до " +
                            card.Price + " мон.";
                    }
                    break;
                }
                case DikozemieRoadAction.Flee:
                {
                    int chance = FleeChance(card);
                    if (Dice.Next(100) < chance)
                    {
                        Advance(0.5f);
                        outcome.Good = true;
                        outcome.Text = IsMounted
                            ? "Вы дали коню шпоры. Крики остались позади."
                            : "Вы рванули через кусты и петляли, пока погоня не отстала.";
                    }
                    else
                    {
                        card.Haggled = true;
                        card.Cornered = true;
                        outcome.Title = string.Empty;
                        outcome.Text = "Не вышло: вас догнали и окружили. Теперь только драться — или платить.";
                    }
                    break;
                }
                case DikozemieRoadAction.Surrender:
                    outcome.Captured = true;
                    // A captive keeps nothing on his back.
                    DikozemieEconomy.ReportCargoLost(1.0, "ловцы людей забрали обоз");
                    Hurt(1);
                    outcome.Text = "Вам скрутили руки и бросили в повозку к другим пленникам.";
                    break;
                case DikozemieRoadAction.PayToll:
                    if (DikozemieArmoryProgression.TrySpendHeroResources(card.Price, out int rest, "toll",
                            (card.Title ?? "пошлина").ToLowerInvariant(), TollRef(card.Land)))
                    {
                        outcome.Text = "Мытарь сунул монету в мешок и поднял рогатку. Осталось " + rest + ".";
                    }
                    else
                    {
                        outcome.Text = "Денег нет. Мытарь плюнул: «Голытьба. Иди уж.»";
                    }
                    break;
                case DikozemieRoadAction.Dodge:
                    if (Dice.Next(100) < DodgeChance)
                    {
                        outcome.Good = true;
                        outcome.Text = "Пока стражник отвернулся, вы проскользнули мимо рогатки.";
                    }
                    else
                    {
                        int fine = card.Price * 3;
                        if (DikozemieArmoryProgression.TrySpendHeroResources(fine, out _, "fine", "штраф мытаря",
                                TollRef(card.Land)))
                        {
                            outcome.Text = "Поймали за шиворот. Пошлина и штраф: -" + fine + " мон.";
                        }
                        else
                        {
                            Hurt(1);
                            outcome.Text = "Поймали за шиворот, а платить нечем: отходили древком и выкинули в канаву.";
                        }
                    }
                    break;
                case DikozemieRoadAction.Ford:
                    Advance(2f);
                    outcome.Good = true;
                    outcome.Text = "Вы спустились ниже по реке и перешли вброд. Промокли до пояса, " +
                        "потеряли два часа, зато не заплатили.";
                    break;
                case DikozemieRoadAction.Join:
                {
                    DikozemieTravelSave save = Save;
                    if (save != null)
                    {
                        save.FellowRole = card.Role;
                        save.FellowCulture = card.Culture;
                        save.FellowSeed = card.Seed;
                        save.FellowCount = Math.Max(1, card.Figures.Count);
                        save.FellowName = "паломники";
                        save.FellowTo = card.TownId;
                        save.FellowPay = card.Reward;
                        save.FellowUntil = 0f;
                        DikozemieTravelState.Save();
                    }
                    outcome.Good = true;
                    outcome.Text = "Паломники идут рядом. С ними разбойники реже выходят на дорогу, а если выйдут — " +
                        "встанут с вами.";
                    break;
                }
                case DikozemieRoadAction.Ask:
                    outcome.Text = RoadRumor(card);
                    break;
                case DikozemieRoadAction.Take:
                    DikozemieArmoryProgression.RefundHeroResources(card.Reward, out _, "road", "находка на дороге");
                    outcome.Good = true;
                    outcome.Text = card.Kind == DikozemieRoadKind.Purse
                        ? "В кошеле " + card.Reward + " мон. Теперь они ваши."
                        : "Вы вдвоём вытолкали телегу. Крестьянин отсчитал " + card.Reward + " мон. и поклонился.";
                    break;
                case DikozemieRoadAction.Trade:
                    OpenPeddler(true, card.Seed);
                    outcome.Text = string.Empty;
                    break;
                case DikozemieRoadAction.Leave:
                    outcome.Text = "Вы отошли тихо, как пришли. Логово никуда не денется.";
                    break;
                default:
                    outcome.Text = card.Kind == DikozemieRoadKind.Caravan
                        ? "Обоз ушёл вперёд. Дальше вы снова один."
                        : "Вы пошли дальше.";
                    break;
            }
            DikozemieTravelState.Save();
            DikozemieLobbyShellIntegration.TraceButtonAction("road choice " + card.Kind + " " + action +
                " good=" + outcome.Good + " coins=" + DikozemieArmoryProgression.HeroResources);
            return outcome;
        }

        private static string RoadRumor(DikozemieRoadEncounter card)
        {
            DikozemieTravelSave save = Save;
            if (save != null && HasBounty)
            {
                return "«" + save.BountyName + "? Видели его шайку: костёр у " +
                    DikozemieTown.DisplayName(DikozemieLobbyMapCatalog.FindNearestNode(save.BountyX, save.BountyY)) +
                    ". Ночью они спят крепко.»";
            }
            string[] rumors =
            {
                "«Ночью на дорогах вдвое опаснее. Но и убежать в темноте легче.»",
                "«В лесу лучники бьют из-за деревьев. Сближайтесь, не стойте на месте.»",
                "«Ловцы людей ходят по трое. Сдашься — привезут на арену, а там уж как повезёт.»",
                "«У мостов сидят мытари. Брод обычно ниже по течению.»",
                "«Капитан стражи в городе платит за разбойников. Живыми.»",
                "«Знахарка в деревне перевяжет за пару монет — раны сами заживают долго.»"
            };
            return rumors[(card.Seed & int.MaxValue) % rumors.Length];
        }

        // ---- Fights -----------------------------------------------------

        internal static DikozemieRoadFightSetup BuildFight(DikozemieRoadEncounter card, bool ambushed)
        {
            DikozemieTravelSave save = Save;
            var setup = new DikozemieRoadFightSetup
            {
                Title = card.Title,
                Scene = DikozemieRoadScenes.Pick(card.Culture, card.Forest, card.Seed),
                Season = DikozemieRoadScenes.Season(card.Culture),
                Hour = DikozemieRoadScenes.AtmosphereHour(HourOfDay),
                Ambush = ambushed,
                HeroHealth = HealthAtStart * (ambushed ? 0.9f : 1f),
                Purpose = card.Kind.ToString()
            };
            setup.Foes.AddRange(card.Foes);
            setup.Allies.AddRange(CompanyFighters());
            return setup;
        }

        // What a road fight left behind.
        internal static DikozemieRoadOutcome ApplyFightResult(DikozemieRoadFightResult result, DikozemieRoadEncounter card)
        {
            var outcome = new DikozemieRoadOutcome { Title = result.Victory ? "ПОБЕДА" : result.Fled ? "БЕГСТВО" : "ПОРАЖЕНИЕ" };
            DikozemieTravelSave save = Save;
            Advance(1f);
            int coins = DikozemieArmoryProgression.HeroResources;
            string progress = string.IsNullOrEmpty(result.Progress) ? string.Empty : "\n\n" + result.Progress;
            if (result.AlliesDown > 0 && save != null && HasCompany)
            {
                string name = save.FellowName;
                ClearFellow(save);
                progress = "\n" + name + " ранены и остались лечиться в ближайшей деревне." + progress;
            }
            if (result.Victory)
            {
                outcome.Good = true;
                int loot = 0;
                var random = new Random(card.Seed ^ 0x5bd1);
                foreach (DikozemieRoadFighter foe in card.Foes)
                {
                    loot += PricePerMan(foe.Role.Replace("_raider", string.Empty)) / 2 + random.Next(3);
                }
                switch (card.Kind)
                {
                    case DikozemieRoadKind.Lair:
                        if (save != null)
                        {
                            save.PrisonerName = save.BountyName;
                            save.PrisonerReward = save.BountyReward;
                            save.PrisonerTown = save.BountyTown;
                            save.BountyName = string.Empty;
                            save.BountyTown = string.Empty;
                        }
                        DikozemieArmoryProgression.RefundHeroResources(loot, out _, "road", "кошели побеждённых");
                        outcome.Text = "Шайка разбита. В котомках нашлось " + loot + " мон. " + save?.PrisonerName +
                            " связан и идёт за вами на верёвке: сдайте его в " +
                            DikozemieTown.DisplayName(DikozemieLobbyMapCatalog.Find(save?.PrisonerTown)) +
                            " за " + save?.PrisonerReward + " мон. (в другом городе — за полцены).";
                        break;
                    case DikozemieRoadKind.Fugitive:
                        if (save != null && card.Foes.Count > 0)
                        {
                            save.PrisonerName = card.Foes[0].Name;
                            save.PrisonerReward = card.Reward;
                            save.PrisonerTown = string.Empty;
                        }
                        outcome.Text = card.Foes.FirstOrDefault()?.Name + " связан. Сдайте его на арену любого " +
                            "города: " + card.Reward + " мон.";
                        break;
                    case DikozemieRoadKind.Toll:
                        DikozemieArmoryProgression.RefundHeroResources(loot, out _, "road", "кошели побеждённых");
                        outcome.Text = "Стражники лежат. В их сумке " + loot + " мон. дорожного сбора. " +
                            "Лорд вряд ли это забудет.";
                        break;
                    default:
                        DikozemieArmoryProgression.RefundHeroResources(loot, out _, "road", "кошели побеждённых");
                        outcome.Text = (card.Kind == DikozemieRoadKind.Catchers
                            ? "Ловцы разбиты. В повозке плачут связанные крестьяне — вы перерезали им верёвки. "
                            : "Все лежат в пыли. ") + "В их кошелях " + loot + " мон.";
                        if (!HasPrisoner && card.Foes.Count > 0)
                        {
                            outcome.CanBind = true;
                            outcome.BindName = card.Kind == DikozemieRoadKind.Catchers ? "главаря ловцов" : "главаря";
                            outcome.BindReward = card.Kind == DikozemieRoadKind.Catchers ? 10 : 4 + card.Foes.Count * 2;
                        }
                        break;
                }
            }
            else if (card.Kind == DikozemieRoadKind.Catchers)
            {
                outcome.Captured = true;
                Hurt(result.Fled ? 1 : 2);
                outcome.Text = result.Fled
                    ? "Вы побежали, но ловцы знали своё дело: сеть, удар по ногам — и верёвка на руках."
                    : "Вас оглушили дубиной. Очнулись вы в повозке, " + G("связанным", "связанной") +
                        ", среди других пленников.";
            }
            else
            {
                int share = result.Fled ? 25 : card.Kind == DikozemieRoadKind.Lair ? 70 : 60;
                int taken = coins * share / 100;
                if (taken > 0)
                {
                    DikozemieArmoryProgression.TrySpendHeroResources(taken, out _, "robbed",
                        result.Fled ? "бросил кошель, убегая" : "разбойники забрали кошель");
                }
                // Robbers take the pack first: whoever fled drops a quarter of it.
                DikozemieEconomy.ReportCargoLost(share / 100.0,
                    result.Fled ? "сбросил вьюки, убегая" : "разбойники забрали вьюки");
                Hurt(result.Fled ? 1 : 2);
                string where = card.Kind == DikozemieRoadKind.Lair
                    ? "Вас оставили у потухшего костра, обобрав до нитки"
                    : card.Kind == DikozemieRoadKind.Toll
                        ? "Стражники отходили вас древками и вытряхнули кошель"
                        : "Вас бросили в канаве, обобрав до нитки";
                outcome.Text = result.Fled
                    ? "Вы ушли, бросив часть поклажи: пропало " + taken + " мон. Порезы саднят."
                    : where + ": -" + taken + " мон. Тело в ранах: идти будет тяжело, пока не отлежитесь " +
                        "в таверне или у знахарки.";
            }
            outcome.Text += progress;
            DikozemieTravelState.Save();
            DikozemieLobbyShellIntegration.TraceButtonAction("road fight outcome kind=" + card.Kind +
                " victory=" + result.Victory + " fled=" + result.Fled + " coins=" + DikozemieArmoryProgression.HeroResources +
                " wound=" + Wound);
            return outcome;
        }

        internal static void Bind(DikozemieRoadOutcome outcome)
        {
            DikozemieTravelSave save = Save;
            if (save == null || !outcome.CanBind || HasPrisoner)
            {
                return;
            }
            save.PrisonerName = outcome.BindName;
            save.PrisonerReward = outcome.BindReward;
            save.PrisonerTown = string.Empty;
            outcome.CanBind = false;
            DikozemieTravelState.Save();
        }

        internal static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
    }
}
