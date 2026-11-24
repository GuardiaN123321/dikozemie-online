using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace Dikozemie.Online
{
    internal sealed class DikozemieCreationSkillPreviewVM : ViewModel
    {
        internal DikozemieCreationSkillPreviewVM(
            string skillId,
            int focus,
            int level,
            bool isIncreased)
        {
            SkillId = skillId ?? string.Empty;
            Focus = Math.Max(0, Math.Min(5, focus));
            Level = Math.Max(0, level);
            LevelText = Level.ToString();
            IsIncreased = isIncreased;
        }

        [DataSourceProperty]
        public string SkillId { get; }

        [DataSourceProperty]
        public int Focus { get; }

        [DataSourceProperty]
        public int Level { get; }

        [DataSourceProperty]
        public string LevelText { get; }

        [DataSourceProperty]
        public bool IsIncreased { get; }

        [DataSourceProperty]
        public bool IsFocus1Filled => Focus >= 1;
        [DataSourceProperty]
        public bool IsFocus2Filled => Focus >= 2;
        [DataSourceProperty]
        public bool IsFocus3Filled => Focus >= 3;
        [DataSourceProperty]
        public bool IsFocus4Filled => Focus >= 4;
        [DataSourceProperty]
        public bool IsFocus5Filled => Focus >= 5;
    }

    internal sealed class DikozemieCreationAttributePreviewVM : ViewModel
    {
        internal DikozemieCreationAttributePreviewVM(
            string attributeId,
            string name,
            int value,
            bool isIncreased,
            IEnumerable<DikozemieCreationSkillPreviewVM> skills)
        {
            AttributeId = attributeId ?? string.Empty;
            Name = name ?? string.Empty;
            Value = Math.Max(0, value);
            ValueText = Value.ToString();
            IsIncreased = isIncreased;
            Skills = new MBBindingList<DikozemieCreationSkillPreviewVM>();
            foreach (DikozemieCreationSkillPreviewVM skill in
                     skills ?? Enumerable.Empty<DikozemieCreationSkillPreviewVM>())
            {
                Skills.Add(skill);
            }
        }

        [DataSourceProperty]
        public string AttributeId { get; }

        [DataSourceProperty]
        public string Name { get; }

        [DataSourceProperty]
        public int Value { get; }

        [DataSourceProperty]
        public string ValueText { get; }

        [DataSourceProperty]
        public bool IsIncreased { get; }

        [DataSourceProperty]
        public MBBindingList<DikozemieCreationSkillPreviewVM> Skills { get; }
    }

    internal sealed class DikozemieHeroSkillItemVM : ViewModel
    {
        private readonly Action<DikozemieHeroSkillItemVM> _select;
        private readonly Action<DikozemieHeroSkillItemVM> _addFocus;
        private bool _isSelected;

        internal DikozemieHeroSkillItemVM(
            string skillId,
            string name,
            string attributeName,
            string description,
            int level,
            int focus,
            bool canAddFocus,
            Action<DikozemieHeroSkillItemVM> select,
            Action<DikozemieHeroSkillItemVM> addFocus)
        {
            SkillId = skillId;
            Name = name;
            AttributeName = attributeName;
            Description = description;
            Level = level;
            Focus = focus;
            LevelText = level.ToString();
            FocusText = "Фокус " + focus + "/5";
            CanAddFocus = canAddFocus;
            _select = select;
            _addFocus = addFocus;
        }

        [DataSourceProperty]
        public string SkillId { get; }

        [DataSourceProperty]
        public string Name { get; }

        // Native CharacterDeveloper/SkillGridItem aliases. Keeping the same
        // property names lets the multiplayer profile use the original visual
        // contract instead of a look-alike list row.
        [DataSourceProperty]
        public string NameText => Name;

        [DataSourceProperty]
        public string AttributeName { get; }

        [DataSourceProperty]
        public string Description { get; }

        [DataSourceProperty]
        public int Level { get; }

        [DataSourceProperty]
        public int Focus { get; }

        [DataSourceProperty]
        public int CurrentFocusLevel => Focus;

        [DataSourceProperty]
        public bool IsFocus1Filled => Focus >= 1;
        [DataSourceProperty]
        public bool IsFocus2Filled => Focus >= 2;
        [DataSourceProperty]
        public bool IsFocus3Filled => Focus >= 3;
        [DataSourceProperty]
        public bool IsFocus4Filled => Focus >= 4;
        [DataSourceProperty]
        public bool IsFocus5Filled => Focus >= 5;

        [DataSourceProperty]
        public bool CanLearnSkill => true;

        [DataSourceProperty]
        public bool CanAddFocus { get; }

        [DataSourceProperty]
        public bool IsInspected => IsSelected;

        [DataSourceProperty]
        public string LevelText { get; }

        [DataSourceProperty]
        public string FocusText { get; }

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }
                _isSelected = value;
                OnPropertyChangedWithValue(value);
            }
        }

        public void ExecuteSelect()
        {
            _select?.Invoke(this);
        }

        public void ExecuteAddFocus()
        {
            _addFocus?.Invoke(this);
        }

        public void ExecuteInspect()
        {
            ExecuteSelect();
        }

        public void ExecuteHover()
        {
            // Hovering a tile must never change the skill that receives the
            // focus point.  The focus button lives to the right of the grid,
            // so the cursor can cross another tile on its way there.  When
            // hover called ExecuteSelect(), that crossed tile silently became
            // the investment target even though the player had clicked a
            // different skill.  Selection is therefore click-only; Gauntlet's
            // button states still provide the native hover animation.
        }

        public void ExecuteHoverEnd()
        {
            // Native CharacterDeveloper keeps the last inspected skill active.
        }
    }

    internal sealed class DikozemieHeroPerkNodeVM : ViewModel
    {
        private readonly Action<DikozemieHeroPerkNodeVM> _select;
        private readonly Action<DikozemieHeroPerkNodeVM> _hover;
        private readonly Action _hoverEnd;

        internal DikozemieHeroPerkNodeVM(
            string skillId,
            int requiredLevel,
            string name,
            string nativePerkId,
            string spriteId,
            string levelText,
            string description,
            int alternativeType,
            int perkState,
            bool isCommitted,
            Action<DikozemieHeroPerkNodeVM> select,
            Action<DikozemieHeroPerkNodeVM> hover,
            Action hoverEnd)
        {
            SkillId = skillId;
            RequiredLevel = requiredLevel;
            Name = name;
            NativePerkId = nativePerkId;
            PerkId = spriteId;
            LevelText = levelText;
            Description = description;
            AlternativeType = alternativeType;
            PerkState = perkState;
            IsUnlocked = perkState != 0;
            IsAlternative = alternativeType != 0;
            CanSelect = perkState == 1;
            IsSelected = perkState == 2;
            IsCommitted = IsSelected && isCommitted;
            IsPending = IsSelected && !isCommitted;
            StateText = IsPending
                ? "НЕ ПРИМЕНЕНО"
                : IsCommitted
                    ? "ВЫБРАНО"
                    : perkState == 1
                        ? "ВЫБРАТЬ"
                        : perkState == 3
                            ? "АЛЬТЕРНАТИВА НЕ ВЫБРАНА"
                            : perkState == 4
                                ? "СНАЧАЛА ВЫБЕРИТЕ ПРЕДЫДУЩИЙ ПЕРК"
                                : "ЗАКРЫТО";
            _select = select;
            _hover = hover;
            _hoverEnd = hoverEnd;
        }

        [DataSourceProperty]
        public string SkillId { get; }

        [DataSourceProperty]
        public int RequiredLevel { get; }

        [DataSourceProperty]
        public int Level => RequiredLevel;

        [DataSourceProperty]
        public string Name { get; }

        [DataSourceProperty]
        public string PerkId { get; }

        [DataSourceProperty]
        public string NativePerkId { get; }

        [DataSourceProperty]
        public string LevelText { get; }

        [DataSourceProperty]
        public string Description { get; }

        [DataSourceProperty]
        public string StateText { get; }

        [DataSourceProperty]
        public bool IsUnlocked { get; }

        [DataSourceProperty]
        public bool IsAlternative { get; }

        [DataSourceProperty]
        public int AlternativeType { get; }

        [DataSourceProperty]
        public int PerkState { get; }

        [DataSourceProperty]
        public bool CanSelect { get; }

        [DataSourceProperty]
        public bool IsSelected { get; }

        [DataSourceProperty]
        public bool IsPending { get; }

        [DataSourceProperty]
        public bool IsCommitted { get; }

        public void ExecuteSelect()
        {
            if (CanSelect)
            {
                _select?.Invoke(this);
            }
        }

        public void ExecuteStartSelection()
        {
            ExecuteSelect();
        }

        public void ExecuteHover()
        {
            _hover?.Invoke(this);
        }

        public void ExecuteHoverEnd()
        {
            _hoverEnd?.Invoke();
        }
    }

    internal sealed class DikozemieHeroPerkTierVM : ViewModel
    {
        internal DikozemieHeroPerkTierVM(
            int requiredLevel,
            DikozemieHeroPerkNodeVM topPerk,
            DikozemieHeroPerkNodeVM bottomPerk,
            int skillLevel)
        {
            RequiredLevel = requiredLevel;
            LevelText = requiredLevel.ToString();
            TopPerk = topPerk;
            BottomPerk = bottomPerk;
            HasBottomPerk = bottomPerk != null;
            IsUnlocked = skillLevel >= requiredLevel;
        }

        [DataSourceProperty]
        public int RequiredLevel { get; }

        [DataSourceProperty]
        public string LevelText { get; }

        [DataSourceProperty]
        public DikozemieHeroPerkNodeVM TopPerk { get; }

        [DataSourceProperty]
        public DikozemieHeroPerkNodeVM BottomPerk { get; }

        [DataSourceProperty]
        public bool HasBottomPerk { get; }

        [DataSourceProperty]
        public bool IsUnlocked { get; }

        public override void OnFinalize()
        {
            TopPerk?.OnFinalize();
            BottomPerk?.OnFinalize();
            base.OnFinalize();
        }
    }

    internal sealed class DikozemieHeroInventoryItemVM : ViewModel
    {
        private readonly Action<DikozemieHeroInventoryItemVM> _sell;
        private readonly Action<DikozemieHeroInventoryItemVM> _hover;
        private readonly Action _hoverEnd;

        internal DikozemieHeroInventoryItemVM(
            ItemObject item,
            string slotText,
            bool isEquipped,
            int count,
            Action<DikozemieHeroInventoryItemVM> sell,
            Action<DikozemieHeroInventoryItemVM> hover,
            Action hoverEnd)
        {
            ItemId = item?.StringId ?? string.Empty;
            Name = item?.Name?.ToString() ?? "Неизвестный предмет";
            SlotText = slotText ?? string.Empty;
            CanEquip = DikozemieItemStore.CanEquip(item);
            StateText = isEquipped
                ? "НАДЕТО"
                : CanEquip ? "ПЕРЕТАЩИТЕ В СЛОТ" : "ТОВАР";
            IsEquipped = isEquipped;
            Count = Math.Max(1, count);
            CountText = "×" + Count;
            SellPrice = DikozemieItemStore.GetSellPrice(item);
            SellPriceText = SellPrice.ToString();
            ImageIdentifier = new ItemImageIdentifierVM(item, string.Empty);
            HasNativeImage = item != null;
            IsGrain = false;
            _sell = sell;
            _hover = hover;
            _hoverEnd = hoverEnd;
        }

        private DikozemieHeroInventoryItemVM(
            string itemId,
            string name,
            int count,
            Action<DikozemieHeroInventoryItemVM> sell,
            Action<DikozemieHeroInventoryItemVM> hover,
            Action hoverEnd)
        {
            ItemId = itemId;
            Name = name;
            SlotText = "Товар";
            CanEquip = false;
            StateText = "ТОВАР";
            IsEquipped = false;
            Count = Math.Max(1, count);
            CountText = "×" + Count;
            SellPrice = DikozemieItemStore.GrainSellPrice;
            SellPriceText = SellPrice.ToString();
            ImageIdentifier = new ItemImageIdentifierVM(null, string.Empty);
            HasNativeImage = false;
            IsGrain = true;
            _sell = sell;
            _hover = hover;
            _hoverEnd = hoverEnd;
        }

        internal static DikozemieHeroInventoryItemVM CreateGrain(
            int count,
            Action<DikozemieHeroInventoryItemVM> sell,
            Action<DikozemieHeroInventoryItemVM> hover,
            Action hoverEnd)
        {
            return new DikozemieHeroInventoryItemVM(
                DikozemieItemStore.GrainItemId,
                "Зерно",
                count,
                sell,
                hover,
                hoverEnd);
        }

        [DataSourceProperty]
        public string ItemId { get; }

        [DataSourceProperty]
        public string Name { get; }

        [DataSourceProperty]
        public string SlotText { get; }

        [DataSourceProperty]
        public string StateText { get; }

        [DataSourceProperty]
        public bool IsEquipped { get; }

        [DataSourceProperty]
        public bool CanEquip { get; }

        [DataSourceProperty]
        public int Count { get; }

        [DataSourceProperty]
        public string CountText { get; }

        [DataSourceProperty]
        public int SellPrice { get; }

        [DataSourceProperty]
        public string SellPriceText { get; }

        [DataSourceProperty]
        public ItemImageIdentifierVM ImageIdentifier { get; }

        [DataSourceProperty]
        public bool HasNativeImage { get; }

        [DataSourceProperty]
        public bool IsGrain { get; }

        public void ExecuteSell()
        {
            _sell?.Invoke(this);
        }

        public void ExecuteHover()
        {
            _hover?.Invoke(this);
        }

        public void ExecuteHoverEnd()
        {
            _hoverEnd?.Invoke();
        }

        public override void OnFinalize()
        {
            ImageIdentifier?.OnFinalize();
            base.OnFinalize();
        }
    }

    internal sealed class DikozemieStoreItemVM : ViewModel
    {
        private readonly Action<DikozemieStoreItemVM> _buy;
        private readonly Action<DikozemieStoreItemVM> _hover;
        private readonly Action _hoverEnd;

        internal DikozemieStoreItemVM(
            ItemObject item,
            bool canBuy,
            Action<DikozemieStoreItemVM> buy,
            Action<DikozemieStoreItemVM> hover,
            Action hoverEnd)
        {
            ItemId = item?.StringId ?? string.Empty;
            Name = item?.Name?.ToString() ?? "Неизвестный предмет";
            TypeText = DikozemieItemStore.GetTypeName(item);
            Price = DikozemieItemStore.GetBuyPrice(item);
            PriceText = Price.ToString();
            CanBuy = canBuy;
            CannotBuy = !canBuy;
            ActionText = canBuy ? "КУПИТЬ" : "НЕ ХВАТАЕТ ЗОЛОТА";
            ImageIdentifier = new ItemImageIdentifierVM(item, string.Empty);
            _buy = buy;
            _hover = hover;
            _hoverEnd = hoverEnd;
        }

        [DataSourceProperty]
        public string ItemId { get; }

        [DataSourceProperty]
        public string Name { get; }

        [DataSourceProperty]
        public string TypeText { get; }

        [DataSourceProperty]
        public int Price { get; }

        [DataSourceProperty]
        public string PriceText { get; }

        [DataSourceProperty]
        public string ActionText { get; }

        [DataSourceProperty]
        public bool CanBuy { get; }

        [DataSourceProperty]
        public bool CannotBuy { get; }

        [DataSourceProperty]
        public ItemImageIdentifierVM ImageIdentifier { get; }

        public void ExecuteBuy()
        {
            if (CanBuy)
            {
                _buy?.Invoke(this);
            }
        }

        public void ExecuteHover()
        {
            _hover?.Invoke(this);
        }

        public void ExecuteHoverEnd()
        {
            _hoverEnd?.Invoke();
        }

        public override void OnFinalize()
        {
            ImageIdentifier?.OnFinalize();
            base.OnFinalize();
        }
    }

    internal sealed class DikozemieHeroEquipmentSlotVM : ViewModel
    {
        private readonly Action<DikozemieHeroEquipmentSlotVM> _select;
        private readonly Action<DikozemieHeroEquipmentSlotVM> _hover;
        private readonly Action _hoverEnd;

        internal DikozemieHeroEquipmentSlotVM(
            int slot,
            string slotName,
            string backgroundBrush,
            ItemObject item,
            Action<DikozemieHeroEquipmentSlotVM> select,
            Action<DikozemieHeroEquipmentSlotVM> hover,
            Action hoverEnd)
        {
            Slot = slot;
            SlotName = slotName ?? string.Empty;
            BackgroundBrush = backgroundBrush ?? "InventoryWeaponSlot";
            ItemId = item?.StringId ?? string.Empty;
            ItemName = item?.Name?.ToString() ?? SlotName;
            HasItem = item != null;
            ImageIdentifier = new ItemImageIdentifierVM(item, string.Empty);
            _select = select;
            _hover = hover;
            _hoverEnd = hoverEnd;
        }

        [DataSourceProperty]
        public int Slot { get; }

        [DataSourceProperty]
        public string SlotName { get; }

        [DataSourceProperty]
        public string BackgroundBrush { get; }

        [DataSourceProperty]
        public bool IsWeaponSlot => Slot >= 0 && Slot <= 3;

        [DataSourceProperty]
        public bool IsBannerSlot => Slot == 4;

        [DataSourceProperty]
        public bool IsHelmetSlot => Slot == 5;

        [DataSourceProperty]
        public bool IsArmorSlot => Slot == 6;

        [DataSourceProperty]
        public bool IsBootSlot => Slot == 7;

        [DataSourceProperty]
        public bool IsGloveSlot => Slot == 8;

        [DataSourceProperty]
        public bool IsCloakSlot => Slot == 9;

        [DataSourceProperty]
        public bool IsMountSlot => Slot == 10;

        [DataSourceProperty]
        public bool IsMountArmorSlot => Slot == 11;

        [DataSourceProperty]
        public string ItemId { get; }

        [DataSourceProperty]
        public string ItemName { get; }

        [DataSourceProperty]
        public bool HasItem { get; }

        [DataSourceProperty]
        public ItemImageIdentifierVM ImageIdentifier { get; }

        public void ExecuteSelect()
        {
            _select?.Invoke(this);
        }

        public void ExecuteHover()
        {
            _hover?.Invoke(this);
        }

        public void ExecuteHoverEnd()
        {
            _hoverEnd?.Invoke();
        }

        public override void OnFinalize()
        {
            ImageIdentifier?.OnFinalize();
            base.OnFinalize();
        }
    }
}
