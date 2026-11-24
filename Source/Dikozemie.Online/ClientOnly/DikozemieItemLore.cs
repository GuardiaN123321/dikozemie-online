using System;
using TaleWorlds.Core;

namespace Dikozemie.Online
{
    // Customer, 2026-11-05 evening: «в Маунт энд Блейд нету описания
    // предмета ... обезличивание»; «описание каждому предмету, типа вот
    // кольчуга ... сделанное хорошим мастером, в каком состоянии»; «и
    // обоснование, почему это так дорого стоит или так мало»; «один зерна —
    // это один мешок? ... нет эквивалента». Every item of the market gets a
    // few lines: what it is, who made it and how, why it costs this much, and
    // the price in the old money and in days of a labourer's work.
    internal static class DikozemieItemLore
    {
        // A labourer earned 1–2 pence a day (Docs/ARENA_PRICES_HISTORY_RU.md).
        internal const float LabourerPennyPerDay = 1.5f;

        internal static string Describe(ItemObject item)
        {
            if (item == null)
            {
                return string.Empty;
            }
            string what = WhatItIs(item);
            string make = MakerAndState(item);
            int price = DikozemieItemStore.GetBuyPrice(item);
            string worth = price > 0 ? Worth(price) : string.Empty;
            string upkeep = DikozemieHorseUpkeep.IsMount(item)
                ? " Ест каждый день: " + DikozemieHorseUpkeep.FodderPerDay(item) +
                    " мон. на овёс и сено."
                : string.Empty;
            return what + (string.IsNullOrEmpty(make) ? string.Empty : " " + make) + upkeep +
                (string.IsNullOrEmpty(worth) ? string.Empty : "\n" + worth);
        }

        // «8 ф. 5 ш.» and the labourer's days.
        internal static string Worth(int price)
        {
            int pounds = price / 240;
            int shillings = price % 240 / 12;
            int pence = price % 12;
            string money;
            if (price < 12)
            {
                money = price + " " + Plural(price, "пенни", "пенни", "пенни");
            }
            else
            {
                money = (pounds > 0 ? pounds + " ф. " : string.Empty) +
                    (shillings > 0 ? shillings + " ш. " : string.Empty) +
                    (pence > 0 ? pence + " п." : string.Empty);
                money = money.Trim();
            }
            float days = price / LabourerPennyPerDay;
            string labour;
            if (days < 1.5f)
            {
                labour = "день работы подёнщика";
            }
            else if (days < 45f)
            {
                int whole = (int)Math.Round(days);
                labour = whole + " " + Plural(whole, "день", "дня", "дней") + " работы подёнщика";
            }
            else if (days < 300f)
            {
                // About 25 working days a month.
                int months = Math.Max(2, (int)Math.Round(days / 25f));
                labour = months + " " + Plural(months, "месяц", "месяца", "месяцев") + " работы подёнщика";
            }
            else
            {
                // About 250 working days a year.
                float years = days / 250f;
                labour = years < 10f
                    ? FormatYears(years) + " работы подёнщика"
                    : (int)Math.Round(years) + " " + Plural((int)Math.Round(years), "год", "года", "лет") +
                        " работы подёнщика — больше, чем он заработает за жизнь";
            }
            return "Цена: " + money + " — " + labour + ".";
        }

        private static string FormatYears(float years)
        {
            int halves = (int)Math.Round(years * 2f);
            int whole = halves / 2;
            bool half = halves % 2 == 1;
            if (whole == 0)
            {
                return "полгода";
            }
            if (half)
            {
                return whole + "½ " + (whole < 5 ? "года" : "лет");
            }
            return whole + " " + Plural(whole, "год", "года", "лет");
        }

        private static string Plural(int number, string one, string few, string many)
        {
            int n = Math.Abs(number) % 100;
            if (n >= 11 && n <= 19)
            {
                return many;
            }
            switch (n % 10)
            {
                case 1: return one;
                case 2:
                case 3:
                case 4: return few;
                default: return many;
            }
        }

        private static string WhatItIs(ItemObject item)
        {
            string id = item.StringId ?? string.Empty;
            switch (item.ItemType)
            {
                case ItemObject.ItemTypeEnum.BodyArmor:
                case ItemObject.ItemTypeEnum.HeadArmor:
                case ItemObject.ItemTypeEnum.HandArmor:
                case ItemObject.ItemTypeEnum.LegArmor:
                case ItemObject.ItemTypeEnum.Cape:
                    return Armor(item);
                case ItemObject.ItemTypeEnum.Horse:
                    return Horse(item);
                case ItemObject.ItemTypeEnum.HorseHarness:
                    return (item.ArmorComponent?.BodyArmor ?? 0) >= 20
                        ? "Конская броня: кольчуга или пластины поверх попоны. Защищает коня от копий и стрел, но тяжела: конь медленнее."
                        : "Седло и упряжь. Работа шорника: кожа, войлок и деревянная лука, неделя-другая работы.";
                case ItemObject.ItemTypeEnum.Animal:
                    return "Скотина. Её разводят крестьяне: корова кормит семью молоком, овца даёт шерсть, курица — яйца.";
                case ItemObject.ItemTypeEnum.Shield:
                    return Shield(item);
                case ItemObject.ItemTypeEnum.Bow:
                    return Contains(id, "long_bow", "longbow", "yew", "hunting", "training", "simple", "light_bow")
                        ? "Простой лук из одного куска дерева: тис, вяз или ясень. Лучник делает такой за несколько дней, а стреляет из него годами."
                        : "Составной лук: дерево, рог и сухожилия, склеенные рыбьим клеем. Его делают месяцами и сушат год — отсюда цена.";
                case ItemObject.ItemTypeEnum.Crossbow:
                    return "Арбалет: ложе, стальная или роговая дуга и спусковой механизм. Работа мастера-арбалетчика; стрелять научится любой, перезаряжать долго.";
                case ItemObject.ItemTypeEnum.Arrows:
                    return "Колчан стрел. Древко, оперение и железный наконечник: стрела стоит меньше пенни, но в бою их тратят десятками.";
                case ItemObject.ItemTypeEnum.Bolts:
                    return "Арбалетные болты: короткое толстое древко и тяжёлый гранёный наконечник. Дороже стрел: железа на них больше.";
                case ItemObject.ItemTypeEnum.Thrown:
                    return Contains(id, "stone")
                        ? "Камни для броска. Лежат под ногами, поэтому почти ничего не стоят."
                        : "Метательное оружие: дротики или топорики. Кузнец делает их связкой; бросил — и надо подбирать.";
                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                case ItemObject.ItemTypeEnum.Polearm:
                    return Weapon(item);
                case ItemObject.ItemTypeEnum.Banner:
                    return "Знамя отряда: под ним собираются свои.";
                default:
                    return Goods(item);
            }
        }

        private static string Armor(ItemObject item)
        {
            ArmorComponent armor = item.ArmorComponent;
            int protection = armor == null
                ? 0
                : Math.Max(Math.Max(armor.BodyArmor, armor.HeadArmor),
                    Math.Max(armor.ArmArmor, armor.LegArmor));
            ArmorComponent.ArmorMaterialTypes material = armor?.MaterialType ??
                ArmorComponent.ArmorMaterialTypes.None;
            string where;
            switch (item.ItemType)
            {
                case ItemObject.ItemTypeEnum.HeadArmor: where = "головы"; break;
                case ItemObject.ItemTypeEnum.HandArmor: where = "рук"; break;
                case ItemObject.ItemTypeEnum.LegArmor: where = "ног"; break;
                case ItemObject.ItemTypeEnum.Cape: where = "плеч"; break;
                default: where = "тела"; break;
            }
            switch (material)
            {
                case ArmorComponent.ArmorMaterialTypes.Plate:
                    return "Пластины для " + where + ": железо, выкованное и подогнанное по фигуре. " +
                        "Месяцы работы доспешника и подмастерьев — такое носят рыцари и их люди.";
                case ArmorComponent.ArmorMaterialTypes.Chainmail:
                    return item.ItemType == ItemObject.ItemTypeEnum.BodyArmor
                        ? "Кольчуга: десятки тысяч колец, каждое согнуто и склёпано вручную. Хороший кольчужник плетёт её месяцами — потому она стоит как дом ремесленника."
                        : "Кольчужная защита " + where + ": сотни склёпанных колец. Долгая работа, поэтому недёшево.";
                case ArmorComponent.ArmorMaterialTypes.Leather:
                    return "Кожа для " + where + ": варёная и промасленная, сшитая шорником. Держит скользящий удар, стоит недорого.";
                default:
                    if (protection <= 6)
                    {
                        return item.ItemType == ItemObject.ItemTypeEnum.Cape
                            ? "Плащ или накидка из шерсти: греет в дорогу, но от клинка не спасёт. Швея сделает за несколько дней."
                            : "Одежда из шерсти или льна: греет, но от клинка не спасёт. Такую шьют в любой деревне.";
                    }
                    return "Стёганка для " + where + ": десятки слоёв льна и шерсти, простёганные вручную. " +
                        "Держит удар дубины и стрелу на излёте; неделя работы мастера.";
            }
        }

        private static string Horse(ItemObject item)
        {
            string id = item.StringId ?? string.Empty;
            if (Contains(id, "camel"))
            {
                return "Верблюд: выносливый и неприхотливый, асераи держат их целыми стадами.";
            }
            if (Contains(id, "mule"))
            {
                return "Мул: вьючное животное. Везёт поклажу, но для боя не годится.";
            }
            int price = DikozemieItemStore.GetBasePrice(item);
            if (price <= 480)
            {
                return "Вьючная или рабочая лошадь. Таскает поклажу и плуг. Её держат зажиточные крестьяне; бедняку и такая не по карману.";
            }
            if (price <= 1200)
            {
                return "Верховая лошадь. Нужна гонцу, торговцу, сквайру. Конь — уже не бедняк: его надо кормить, подковывать и где-то держать.";
            }
            if (price <= 8400)
            {
                return "Боевой конь: выучен не бояться шума, стали и крови. Его годами объезжают — поэтому стоит как хорошее хозяйство.";
            }
            return "Дестриэ, конь знати: крупный, сильный, обученный для таранного удара. Таких растят в конюшнях лордов, цена — как у поместья.";
        }

        private static string Shield(ItemObject item)
        {
            WeaponComponentData weapon = item.PrimaryWeapon;
            bool large = weapon != null && weapon.WeaponClass == WeaponClass.LargeShield;
            return large
                ? "Большой щит: доски, обтянутые кожей, с железной оковкой. Плотник и шорник сделают его за пару дней."
                : "Малый щит: несколько досок, кожа и железный умбон. Сделать его несложно, поэтому он недорог.";
        }

        private static string Weapon(ItemObject item)
        {
            string id = item.StringId ?? string.Empty;
            if (Contains(id, "pitchfork", "sickle", "scythe", "hoe", "cleaver", "hammer_tool", "pickaxe"))
            {
                return "Сельский инструмент, а не оружие. Им косят, жнут и копают, а в драку берут от бедности.";
            }
            if (Contains(id, "staff", "quarterstaff", "club", "cudgel"))
            {
                return "Палка или дубина. Срезал в лесу — и вооружён: оружие тех, у кого нет денег.";
            }
            WeaponClass weaponClass = item.PrimaryWeapon?.WeaponClass ?? WeaponClass.Undefined;
            switch (weaponClass)
            {
                case WeaponClass.OneHandedSword:
                case WeaponClass.TwoHandedSword:
                    return "Меч: клинок ковали и закаляли днями, хорошей стали мало. Меч — знак человека с деньгами, крестьянину он не по карману.";
                case WeaponClass.Dagger:
                    return "Нож или кинжал. Есть у каждого — режут хлеб и верёвки, а в тесноте и врага.";
                case WeaponClass.OneHandedAxe:
                case WeaponClass.TwoHandedAxe:
                case WeaponClass.ThrowingAxe:
                    return "Топор: железа немного, кузнец откуёт за день. Им рубят лес, и с ним идут в бой.";
                case WeaponClass.Mace:
                case WeaponClass.TwoHandedMace:
                case WeaponClass.Pick:
                    return "Булава или клевец: железное навершие на рукояти. Против брони лучше меча, а стоит дешевле.";
                case WeaponClass.OneHandedPolearm:
                case WeaponClass.TwoHandedPolearm:
                case WeaponClass.LowGripPolearm:
                    return "Копьё или древковое оружие: ясеневое древко и наконечник. Кузнец сделает его за день — оружие ополчения.";
                default:
                    return "Оружие. Цена зависит от железа и труда кузнеца.";
            }
        }

        private static string Goods(ItemObject item)
        {
            string id = item.StringId ?? string.Empty;
            if (string.Equals(id, DikozemieItemStore.GrainItemId, StringComparison.Ordinal))
            {
                return "Мешок зерна — бушель, около 25 кг. Человеку на три недели хлеба, коню — на пару дней овса.";
            }
            if (Contains(id, "fish"))
            {
                return "Рыба, корзина. Солёная хранится неделями.";
            }
            if (Contains(id, "wine", "beer", "ale"))
            {
                return "Бочонок. Пиво пьют все и каждый день, вино возят издалека.";
            }
            if (Contains(id, "velvet", "linen", "wool", "cloth", "silk"))
            {
                return "Тюк ткани. Из него шьют одежду; бархат и шёлк — для знати.";
            }
            if (Contains(id, "iron", "ore", "tools"))
            {
                return "Железо или инструменты, ящик. Кузнецы покупают его постоянно.";
            }
            return "Товар, одна мера: тюк, мешок или бочонок. Его возят купцы из города в город.";
        }

        private static string MakerAndState(ItemObject item)
        {
            string id = (item.StringId ?? string.Empty).ToLowerInvariant();
            string state = string.Empty;
            if (Contains(id, "noble", "lordly", "royal", "decorated", "ornate", "gilded", "emir", "khan", "sultan", "fine"))
            {
                state = "Богатая отделка: за узор и серебро знать платит вдвое, рубит такая вещь не лучше простой.";
            }
            else if (Contains(id, "rusty", "worn", "ragged", "old_", "tattered", "crude", "peasant"))
            {
                state = "Простая или поношенная вещь: потёрта, кое-где залатана.";
            }
            string culture = CultureName(item.Culture?.StringId);
            if (string.IsNullOrEmpty(culture))
            {
                return state;
            }
            return ("Работа " + culture + " мастеров. " + state).Trim();
        }

        private static string CultureName(string culture)
        {
            switch (DikozemieTown.NormalizeCulture(culture))
            {
                case "aserai": return "асерайских";
                case "battania": return "баттанских";
                case "empire": return "имперских";
                case "khuzait": return "хузаитских";
                case "nord": return "нордских";
                case "sturgia": return "стургийских";
                case "vlandia": return "вландийских";
                default: return string.Empty;
            }
        }

        private static bool Contains(string text, params string[] parts)
        {
            foreach (string part in parts)
            {
                if (text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
