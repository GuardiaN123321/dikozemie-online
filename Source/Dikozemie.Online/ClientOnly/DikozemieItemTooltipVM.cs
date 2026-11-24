using System;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace Dikozemie.Online
{
    internal sealed class DikozemieItemTooltipPropertyVM : ViewModel
    {
        internal DikozemieItemTooltipPropertyVM(
            string name,
            string targetValue,
            string comparedValue,
            int comparison)
        {
            Name = name ?? string.Empty;
            TargetValue = targetValue ?? string.Empty;
            ComparedValue = comparedValue ?? string.Empty;
            HasComparedValue = !string.IsNullOrWhiteSpace(ComparedValue);
            IsTargetBetter = HasComparedValue && comparison > 0;
            IsTargetWorse = HasComparedValue && comparison < 0;
            IsTargetNeutral = !IsTargetBetter && !IsTargetWorse;
            IsComparedBetter = HasComparedValue && comparison < 0;
            IsComparedWorse = HasComparedValue && comparison > 0;
            IsComparedNeutral = !IsComparedBetter && !IsComparedWorse;
        }

        [DataSourceProperty]
        public string Name { get; }

        [DataSourceProperty]
        public string TargetValue { get; }

        [DataSourceProperty]
        public string ComparedValue { get; }

        [DataSourceProperty]
        public bool HasComparedValue { get; }

        [DataSourceProperty]
        public bool IsTargetBetter { get; }

        [DataSourceProperty]
        public bool IsTargetWorse { get; }

        [DataSourceProperty]
        public bool IsTargetNeutral { get; }

        [DataSourceProperty]
        public bool IsComparedBetter { get; }

        [DataSourceProperty]
        public bool IsComparedWorse { get; }

        [DataSourceProperty]
        public bool IsComparedNeutral { get; }
    }

    /// <summary>
    /// Multiplayer-safe counterpart of Bannerlord's campaign ItemMenuVM.
    /// It intentionally reads only TaleWorlds.Core item data so opening the
    /// armory does not create a Campaign dependency in a multiplayer lobby.
    /// </summary>
    internal sealed class DikozemieItemTooltipVM : ViewModel
    {
        private ItemImageIdentifierVM _targetImageIdentifier;
        private ItemImageIdentifierVM _comparedImageIdentifier;
        private bool _isVisible;
        private bool _hasComparison;
        private string _targetItemName = string.Empty;
        private string _comparedItemName = string.Empty;
        private string _targetDescription = string.Empty;
        private float _horizontalOffset;

        internal DikozemieItemTooltipVM()
        {
            TargetImageIdentifier = new ItemImageIdentifierVM(null, string.Empty);
            ComparedImageIdentifier = new ItemImageIdentifierVM(null, string.Empty);
            Properties = new MBBindingList<DikozemieItemTooltipPropertyVM>();
        }

        [DataSourceProperty]
        public bool IsVisible
        {
            get => _isVisible;
            private set
            {
                if (_isVisible == value)
                {
                    return;
                }
                _isVisible = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool HasComparison
        {
            get => _hasComparison;
            private set
            {
                if (_hasComparison == value)
                {
                    return;
                }
                _hasComparison = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string TargetItemName
        {
            get => _targetItemName;
            private set
            {
                _targetItemName = value ?? string.Empty;
                OnPropertyChangedWithValue(_targetItemName);
            }
        }

        // What the thing is, who made it, why it costs this much
        // (DikozemieItemLore, customer 2026-11-05).
        [DataSourceProperty]
        public string TargetDescription
        {
            get => _targetDescription;
            private set
            {
                if (string.Equals(_targetDescription, value, StringComparison.Ordinal))
                {
                    return;
                }
                _targetDescription = value ?? string.Empty;
                OnPropertyChangedWithValue(_targetDescription);
            }
        }

        [DataSourceProperty]
        public string ComparedItemName
        {
            get => _comparedItemName;
            private set
            {
                _comparedItemName = value ?? string.Empty;
                OnPropertyChangedWithValue(_comparedItemName);
            }
        }

        [DataSourceProperty]
        public float HorizontalOffset
        {
            get => _horizontalOffset;
            private set
            {
                _horizontalOffset = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public ItemImageIdentifierVM TargetImageIdentifier
        {
            get => _targetImageIdentifier;
            private set
            {
                _targetImageIdentifier = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public ItemImageIdentifierVM ComparedImageIdentifier
        {
            get => _comparedImageIdentifier;
            private set
            {
                _comparedImageIdentifier = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public MBBindingList<DikozemieItemTooltipPropertyVM> Properties { get; }

        internal void Show(ItemObject target, ItemObject compared, float horizontalOffset)
        {
            if (target == null)
            {
                Hide();
                return;
            }

            ItemImageIdentifierVM previousTarget = TargetImageIdentifier;
            ItemImageIdentifierVM previousCompared = ComparedImageIdentifier;
            TargetImageIdentifier = new ItemImageIdentifierVM(target, string.Empty);
            ComparedImageIdentifier = new ItemImageIdentifierVM(compared, string.Empty);
            previousTarget?.OnFinalize();
            previousCompared?.OnFinalize();

            TargetItemName = target.Name?.ToString() ?? target.StringId;
            TargetDescription = DikozemieItemLore.Describe(target);
            ComparedItemName = compared?.Name?.ToString() ?? string.Empty;
            HasComparison = compared != null;
            HorizontalOffset = horizontalOffset;
            Properties.Clear();
            BuildProperties(target, compared);
            IsVisible = true;
        }

        internal void Hide()
        {
            IsVisible = false;
        }

        private void BuildProperties(ItemObject target, ItemObject compared)
        {
            AddText(
                "Тип",
                DikozemieItemStore.GetTypeName(target),
                compared == null ? null : DikozemieItemStore.GetTypeName(compared));
            AddInt(
                "Стоимость",
                DikozemieItemStore.GetBuyPrice(target),
                compared == null ? (int?)null : DikozemieItemStore.GetBuyPrice(compared),
                null);
            AddFloat("Вес", target.Weight, compared?.Weight, false);
            // ItemObject.Tier/Tierf delegates to the campaign ItemValueModel.
            // That model is intentionally absent in a pure multiplayer lobby
            // and dereferencing it terminates the native client.  The armory
            // already has a multiplayer-safe prepared price, so use it only
            // to expose a stable broad item class without a Campaign dependency.
            AddInt(
                "Класс предмета",
                GetSafeItemClass(target),
                compared == null ? (int?)null : GetSafeItemClass(compared),
                true);

            if (target.Difficulty > 0)
            {
                string skillName = target.RelevantSkill?.Name?.ToString();
                string label = string.IsNullOrWhiteSpace(skillName)
                    ? "Требуемый навык"
                    : "Требуется: " + skillName;
                int? comparedDifficulty = compared != null &&
                    string.Equals(
                        target.RelevantSkill?.StringId,
                        compared.RelevantSkill?.StringId,
                        StringComparison.Ordinal)
                    ? compared.Difficulty
                    : (int?)null;
                AddInt(label, target.Difficulty, comparedDifficulty, false);
            }

            AddArmorProperties(target, compared);
            AddWeaponProperties(target, compared);
            AddHorseProperties(target, compared);
        }

        private static int GetSafeItemClass(ItemObject item)
        {
            // Penny prices (0.5.124): under 2 shillings, under 8, under
            // 1.5 pounds, under 5, under 20, and the dearest pieces.
            int price = DikozemieItemStore.GetBuyPrice(item);
            if (price >= 4800) return 6;
            if (price >= 1200) return 5;
            if (price >= 360) return 4;
            if (price >= 96) return 3;
            if (price >= 24) return 2;
            return 1;
        }

        private void AddArmorProperties(ItemObject target, ItemObject compared)
        {
            ArmorComponent armor = target.HasArmorComponent
                ? target.ArmorComponent
                : null;
            ArmorComponent comparedArmor = compared != null && compared.HasArmorComponent
                ? compared.ArmorComponent
                : null;
            if (armor == null)
            {
                return;
            }

            AddPositiveInt("Броня головы", armor.HeadArmor, comparedArmor?.HeadArmor, true);
            AddPositiveInt("Броня тела", armor.BodyArmor, comparedArmor?.BodyArmor, true);
            AddPositiveInt("Броня рук", armor.ArmArmor, comparedArmor?.ArmArmor, true);
            AddPositiveInt("Броня ног", armor.LegArmor, comparedArmor?.LegArmor, true);
            AddNonZeroInt("Скорость", armor.SpeedBonus, comparedArmor?.SpeedBonus, true);
            AddNonZeroInt("Манёвренность", armor.ManeuverBonus, comparedArmor?.ManeuverBonus, true);
            AddNonZeroInt("Натиск", armor.ChargeBonus, comparedArmor?.ChargeBonus, true);
        }

        private void AddWeaponProperties(ItemObject target, ItemObject compared)
        {
            WeaponComponentData weapon = target.HasWeaponComponent
                ? target.PrimaryWeapon
                : null;
            WeaponComponentData comparedWeapon = compared != null && compared.HasWeaponComponent
                ? compared.PrimaryWeapon
                : null;
            if (weapon == null)
            {
                return;
            }

            if (weapon.SwingDamage > 0)
            {
                AddInt(
                    "Рубящий урон (" + GetDamageTypeName(weapon.SwingDamageType) + ")",
                    weapon.SwingDamage,
                    comparedWeapon != null && comparedWeapon.SwingDamage > 0
                        ? comparedWeapon.SwingDamage
                        : (int?)null,
                    true);
            }
            if (weapon.ThrustDamage > 0)
            {
                AddInt(
                    "Колющий урон (" + GetDamageTypeName(weapon.ThrustDamageType) + ")",
                    weapon.ThrustDamage,
                    comparedWeapon != null && comparedWeapon.ThrustDamage > 0
                        ? comparedWeapon.ThrustDamage
                        : (int?)null,
                    true);
            }
            AddPositiveInt(
                "Урон снаряда",
                weapon.MissileDamage,
                comparedWeapon?.MissileDamage,
                true);
            AddPositiveInt(
                "Скорость взмаха",
                weapon.SwingSpeed,
                comparedWeapon?.SwingSpeed,
                true);
            AddPositiveInt(
                "Скорость укола",
                weapon.ThrustSpeed,
                comparedWeapon?.ThrustSpeed,
                true);
            AddPositiveInt(
                "Скорость снаряда",
                weapon.MissileSpeed,
                comparedWeapon?.MissileSpeed,
                true);
            AddPositiveInt(
                "Управляемость",
                weapon.Handling,
                comparedWeapon?.Handling,
                true);
            AddPositiveInt(
                "Длина",
                weapon.WeaponLength,
                comparedWeapon?.WeaponLength,
                true);
            AddPositiveInt(
                "Точность",
                weapon.Accuracy,
                comparedWeapon?.Accuracy,
                true);
            AddPositiveInt(
                weapon.IsShield ? "Прочность щита" : "Боезапас",
                weapon.MaxDataValue,
                comparedWeapon?.MaxDataValue,
                true);
            AddPositiveInt(
                "Защита щита",
                weapon.BodyArmor,
                comparedWeapon?.BodyArmor,
                true);
        }

        private void AddHorseProperties(ItemObject target, ItemObject compared)
        {
            HorseComponent horse = target.HasHorseComponent
                ? target.HorseComponent
                : null;
            HorseComponent comparedHorse = compared != null && compared.HasHorseComponent
                ? compared.HorseComponent
                : null;
            if (horse == null)
            {
                return;
            }

            EquipmentElement emptyHarness = default;
            EquipmentElement targetElement = new EquipmentElement(target);
            EquipmentElement comparedElement = comparedHorse == null
                ? EquipmentElement.Invalid
                : new EquipmentElement(compared);
            AddPositiveInt(
                "Скорость коня",
                targetElement.GetModifiedMountSpeed(in emptyHarness),
                comparedHorse == null
                    ? (int?)null
                    : comparedElement.GetModifiedMountSpeed(in emptyHarness),
                true);
            AddPositiveInt(
                "Манёвренность коня",
                targetElement.GetModifiedMountManeuver(in emptyHarness),
                comparedHorse == null
                    ? (int?)null
                    : comparedElement.GetModifiedMountManeuver(in emptyHarness),
                true);
            AddPositiveInt(
                "Урон натиском",
                targetElement.GetModifiedMountCharge(in emptyHarness),
                comparedHorse == null
                    ? (int?)null
                    : comparedElement.GetModifiedMountCharge(in emptyHarness),
                true);
            AddPositiveInt(
                "Здоровье коня",
                new EquipmentElement(target).GetModifiedMountHitPoints(),
                comparedHorse == null
                    ? (int?)null
                    : new EquipmentElement(compared).GetModifiedMountHitPoints(),
                true);
        }

        private void AddText(string name, string targetValue, string comparedValue)
        {
            Properties.Add(new DikozemieItemTooltipPropertyVM(
                name,
                targetValue,
                comparedValue,
                0));
        }

        private void AddPositiveInt(
            string name,
            int targetValue,
            int? comparedValue,
            bool higherIsBetter)
        {
            if (targetValue > 0)
            {
                AddInt(name, targetValue, comparedValue, higherIsBetter);
            }
        }

        private void AddNonZeroInt(
            string name,
            int targetValue,
            int? comparedValue,
            bool higherIsBetter)
        {
            if (targetValue != 0)
            {
                AddInt(name, targetValue, comparedValue, higherIsBetter);
            }
        }

        private void AddInt(
            string name,
            int targetValue,
            int? comparedValue,
            bool? higherIsBetter)
        {
            int comparison = GetComparison(
                targetValue,
                comparedValue,
                higherIsBetter);
            Properties.Add(new DikozemieItemTooltipPropertyVM(
                name,
                targetValue.ToString(),
                comparedValue?.ToString(),
                comparison));
        }

        private void AddFloat(
            string name,
            float targetValue,
            float? comparedValue,
            bool higherIsBetter)
        {
            int comparison = GetComparison(
                targetValue,
                comparedValue,
                higherIsBetter);
            Properties.Add(new DikozemieItemTooltipPropertyVM(
                name,
                targetValue.ToString("0.##"),
                comparedValue?.ToString("0.##"),
                comparison));
        }

        private static int GetComparison(
            float targetValue,
            float? comparedValue,
            bool? higherIsBetter)
        {
            if (!comparedValue.HasValue || !higherIsBetter.HasValue ||
                Math.Abs(targetValue - comparedValue.Value) < 0.001f)
            {
                return 0;
            }

            bool targetIsHigher = targetValue > comparedValue.Value;
            return targetIsHigher == higherIsBetter.Value ? 1 : -1;
        }

        private static string GetDamageTypeName(DamageTypes damageType)
        {
            switch (damageType.ToString())
            {
                case "Cut": return "рубящий";
                case "Pierce": return "колющий";
                case "Blunt": return "дробящий";
                default: return damageType.ToString();
            }
        }

        public override void OnFinalize()
        {
            TargetImageIdentifier?.OnFinalize();
            ComparedImageIdentifier?.OnFinalize();
            foreach (DikozemieItemTooltipPropertyVM property in Properties)
            {
                property.OnFinalize();
            }
            base.OnFinalize();
        }
    }
}
