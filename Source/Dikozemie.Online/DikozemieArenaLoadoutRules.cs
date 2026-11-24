using TaleWorlds.Core;

namespace Dikozemie.Online
{
    internal static class DikozemieArenaLoadoutRules
    {
        internal static bool IsDisciplineAvailable(
            Equipment equipment,
            DikozemieArenaDiscipline discipline,
            out string reason)
        {
            reason = string.Empty;
            if (discipline != DikozemieArenaDiscipline.Mounted)
            {
                return true;
            }

            ItemObject horse = equipment?[EquipmentIndex.Horse].Item;
            if (horse?.ItemType == ItemObject.ItemTypeEnum.Horse)
            {
                return true;
            }

            reason = "КОННЫЙ БОЙ: НУЖЕН ЭКИПИРОВАННЫЙ КОНЬ";
            return false;
        }

        internal static bool IsCompositionAvailable(
            Equipment equipment,
            DikozemieArenaComposition composition,
            out string reason)
        {
            bool hasMelee = HasMeleeWeapon(equipment);
            bool hasBow = HasItemType(
                equipment,
                ItemObject.ItemTypeEnum.Bow);
            bool hasCrossbow = HasItemType(
                equipment,
                ItemObject.ItemTypeEnum.Crossbow);
            bool hasUsableRanged =
                hasBow && HasItemType(
                    equipment,
                    ItemObject.ItemTypeEnum.Arrows) ||
                hasCrossbow && HasItemType(
                    equipment,
                    ItemObject.ItemTypeEnum.Bolts);

            switch (composition)
            {
                case DikozemieArenaComposition.Melee:
                    if (!hasMelee)
                    {
                        reason =
                            "БЛИЖНИЙ БОЙ: НУЖНО ОРУЖИЕ БЛИЖНЕГО БОЯ";
                        return false;
                    }
                    reason = string.Empty;
                    return true;
                case DikozemieArenaComposition.Ranged:
                    if (!hasUsableRanged)
                    {
                        reason =
                            "ДАЛЬНИЙ БОЙ: НУЖНЫ ЛУК СО СТРЕЛАМИ ИЛИ АРБАЛЕТ С БОЛТАМИ";
                        return false;
                    }
                    reason = string.Empty;
                    return true;
                default:
                    if (!hasMelee && !hasUsableRanged)
                    {
                        reason =
                            "СМЕШАННЫЙ БОЙ: НУЖНО БЛИЖНЕЕ ОРУЖИЕ ИЛИ ЛУК СО СТРЕЛАМИ ИЛИ АРБАЛЕТ С БОЛТАМИ";
                        return false;
                    }
                    reason = string.Empty;
                    return true;
            }
        }

        internal static bool IsSelectionAvailable(
            Equipment equipment,
            DikozemieArenaDiscipline discipline,
            DikozemieArenaComposition composition,
            out string reason)
        {
            if (!IsDisciplineAvailable(equipment, discipline, out reason))
            {
                return false;
            }
            return IsCompositionAvailable(equipment, composition, out reason);
        }

        internal static void ApplyCompositionToEquipment(
            Equipment equipment,
            DikozemieArenaComposition composition)
        {
            if (equipment == null ||
                composition != DikozemieArenaComposition.Melee)
            {
                // Ranged fighters keep a normal melee backup. Mixed fighters
                // keep the complete saved loadout.
                return;
            }

            // A bow no longer blocks entry when the hero owns a real melee
            // weapon. It is only suppressed in the spawned melee battle copy;
            // the saved hero equipment remains untouched.
            for (int slot = (int)EquipmentIndex.Weapon0;
                 slot <= (int)EquipmentIndex.Weapon3;
                 slot++)
            {
                ItemObject.ItemTypeEnum? itemType =
                    equipment[(EquipmentIndex)slot].Item?.ItemType;
                if (itemType == ItemObject.ItemTypeEnum.Bow ||
                    itemType == ItemObject.ItemTypeEnum.Crossbow ||
                    itemType == ItemObject.ItemTypeEnum.Arrows ||
                    itemType == ItemObject.ItemTypeEnum.Bolts)
                {
                    equipment[(EquipmentIndex)slot] =
                        default(EquipmentElement);
                }
            }
        }

        private static bool HasMeleeWeapon(Equipment equipment)
        {
            if (equipment == null)
            {
                return false;
            }
            for (int slot = (int)EquipmentIndex.Weapon0;
                 slot <= (int)EquipmentIndex.Weapon3;
                 slot++)
            {
                ItemObject item = equipment[(EquipmentIndex)slot].Item;
                switch (item?.ItemType)
                {
                    case ItemObject.ItemTypeEnum.OneHandedWeapon:
                    case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                    case ItemObject.ItemTypeEnum.Polearm:
                    case ItemObject.ItemTypeEnum.Thrown:
                        return true;
                }
            }
            return false;
        }

        private static bool HasItemType(
            Equipment equipment,
            ItemObject.ItemTypeEnum itemType)
        {
            if (equipment == null)
            {
                return false;
            }
            for (int slot = (int)EquipmentIndex.Weapon0;
                 slot <= (int)EquipmentIndex.Weapon3;
                 slot++)
            {
                if (equipment[(EquipmentIndex)slot].Item?.ItemType == itemType)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
