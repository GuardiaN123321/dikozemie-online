using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

namespace Dikozemie.Online
{
    internal sealed class DikozemieHeroMenuVM : ViewModel
    {
        private enum NativePerkState
        {
            NotEarned = 0,
            EarnedButNotSelected = 1,
            EarnedAndActive = 2,
            EarnedAndNotActive = 3,
            EarnedPreviousPerkNotSelected = 4
        }

        private static readonly string[] CultureIds =
        {
            "vlandia", "sturgia", "empire", "aserai",
            "khuzait", "battania", "nords"
        };
        private static readonly string[] CultureNames =
        {
            "Вландия", "Стургия", "Империя", "Султанат асераев",
            "Хузаитское ханство", "Баттания", "Норды"
        };
        private static readonly string[] CultureDescriptions =
        {
            L("{=ZPaw3WuO}The Vlandians are the descendants of adventurers from the west who lived under the Empire for centuries before forming an independent kingdom. With the decline of imperial authority, they have evolved into a well organised feudal society led by a caste of warlike nobles who prefer to fight with spears and lances on horseback."),
            L("{=HD2wzAxz}Sturgians are the descendants of the foreign tribes of northern Calradia. As the empire expanded into their cold forests, they found a ready market for the luxurious furs of woodland animals whom they traditionally hunted and trapped. Sturgian chieftains became princes, vying for pre-eminence with the help of Nord mercenaries who migrated into their realm. They are good hunters and wanderers, travelling far in search of opportunities both for trade and for plunder."),
            L("{=iLEhyGI8}The Calradian Empire is in decline. Even before the suspicious death of the Emperor Arenicos, the once united realm was torn by political rivalries. Today, those factions are in open war. Yet Calradians endure. Their methodical study of defensive strategy, in which garrison cities slow down invading forces until mobile field armies can come to their rescue, helps hold their neighbors at bay."),
            L("{=Khr5yETv}The Aserai are the inhabitants of the Nahasa desert, a mixture of nomadic bedouin and settled oasis farmers. They are famous for their horsemanship and their knowledge of the learning of many lands, especially medicine, gained from lying on some of the continent's most lucrative trade routes. Each clan is fiercely proud of its lineage and often jealous of the others, but when united by a charismatic leader, they become a major force in the south."),
            L("{=HkZ4E94O}The Khuzait confederation of steppe tribes used to live a nomadic life, but have recently settled in the eastern frontier of the Empire and are slowly transitioning into an agrarian society with permanent town centers. Despite this, they still retain many aspects of their nomadic life, including their affinity with horses. They are masters of mounted archery, shooting and then galloping out of reach."),
            L("{=c1Ag8YJS}The Battanians still remember the olden days when the woods stretched across northern Calradia, and the Empire and its cities had yet to violate their sanctity. The fierce warriors remain loyal to their traditional ways. They paint their faces when going to battle and even their noblemen prefer to fight on foot while using great axes and two-handed swords with deadly efficiency."),
            L("{=danasORQ}The Nords are a people made by the sea. The very name of their home, the Nordvyg, refers to the icy stretch of glacier-cut coastline that links their scattered settlements. Until recently, their towns were little more than a shrine or market beside a fjord or estuary, surrounded by a palisade. Some make a modest living by farming barley and raising sheep, but those with ambition must venture forth in their longships to trade, plunder or fight for hire.")
        };
        private static readonly string[] CulturePositiveFeat1Texts =
        {
            L("{=ppdrgOL8}5% more renown from the battles, 15% more income while serving as a mercenary."),
            L("{=5BabRyaa}Villages grain production is increased by 10%."),
            L("{=a2eM0QUb}20% less garrison troop wage."),
            L("{=7kGGgkro}Caravans are 30% cheaper to build. 10% less trade penalty."),
            L("{=JUpZuals}Recruiting and upgrading mounted troops are 10% cheaper."),
            L("{=38W2WloI}50% less speed penalty and 15% sight range bonus in forests."),
            L("{=eI8zKXld}20% raid speed bonus while raiding.")
        };
        private static readonly string[] CulturePositiveFeat2Texts =
        {
            L("{=3GsZXXOi}10% production bonus to villages that are bound to castles."),
            L("{=Lmjm5Q9D}Armies are gathered with 50% less influence."),
            L("{=xgPNGOa8}Being in army brings 25% more influence."),
            L("{=6aFTN1Nb}No speed penalty on desert."),
            L("{=Xaw2CoCG}25% production bonus to horse, mule, cow and sheep in villages owned by Khuzait rulers."),
            L("{=HLI5zAMV}Towns owned by Battanian rulers will have +20% chance of militias to spawn as veteran militias."),
            L("{=hUSnaX6O}+30% more loot from villages, villagers and caravans.")
        };
        private static readonly string[] CulturePositiveFeat3Texts =
        {
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            L("{=bEw6FNpM}20% ship movement speed in rivers and coastal seas.")
        };
        private static readonly string[] CultureNegativeFeatTexts =
        {
            L("{=O1XCNeZr}Recruiting lords to armies costs 20% more influence."),
            L("{=fB7kS9Cx}20% more relationship penalty from kingdom decisions."),
            L("{=UWiqIFUb}Village hearths increase 20% less."),
            L("{=GacrZ1Jl}Daily wages of troops in the party are increased by 5%."),
            L("{=8PsaGhI8}20% less tax income from towns."),
            L("{=ruP9jbSq}10% slower build rate for town projects in settlements."),
            L("{=AnanB4d6}Armies that are commanded by a Nord commander lose 30% more cohesion on land.")
        };

        private static readonly string[] CreationAttributeIds =
        {
            "vigor", "control", "endurance", "cunning", "social", "intelligence",
            "naval"
        };
        private static readonly string[] CreationAttributeNames =
        {
            "Энергия", "Контроль", "Выносливость", "Хитрость", "Социальность",
            "Интеллект", "Морское дело"
        };
        private static readonly string[][] CreationSkillGroups =
        {
            new[] { "OneHanded", "TwoHanded", "Polearm" },
            new[] { "Bow", "Crossbow", "Throwing" },
            new[] { "Riding", "Athletics", "Crafting" },
            new[] { "Scouting", "Tactics", "Roguery" },
            new[] { "Charm", "Leadership", "Trade" },
            new[] { "Steward", "Medicine", "Engineering" },
            new[] { "Mariner", "Boatswain", "Shipmaster" }
        };

        private int _section;
        private int _creationStep;
        private int _selectedChoice = -1;
        private int _selectedCulture = -1;
        private bool _isVisible;
        private bool _isCreationMode;
        private bool _isCultureStage;
        private bool _isReviewStage;
        private string _questionText = string.Empty;
        private string _questionProgress = string.Empty;
        private string _choice1Text = string.Empty;
        private string _choice2Text = string.Empty;
        private string _choice3Text = string.Empty;
        private string _choice4Text = string.Empty;
        private string _choice1Hint = string.Empty;
        private string _choice2Hint = string.Empty;
        private string _choice3Hint = string.Empty;
        private string _choice4Hint = string.Empty;
        private string _choice5Text = string.Empty;
        private string _choice6Text = string.Empty;
        private string _choice7Text = string.Empty;
        private string _choice8Text = string.Empty;
        private string _selectedChoiceHint = string.Empty;
        private string _selectedChoiceDescription = string.Empty;
        private string _selectedChoiceEffectText = string.Empty;
        private string _creationStageTitle = string.Empty;
        private string _heroName = string.Empty;
        private string _reviewText = string.Empty;
        private string _cultureDescription = string.Empty;
        private string _cultureName = string.Empty;
        private string _culturePositiveFeat1 = string.Empty;
        private string _culturePositiveFeat2 = string.Empty;
        private string _culturePositiveFeat3 = string.Empty;
        private string _cultureNegativeFeat = string.Empty;
        private string _selectedCultureId = "empire";
        private string _heroTitle = string.Empty;
        private string _progressText = string.Empty;
        private string _heroLevelHudText = string.Empty;
        private string _heroExperienceHudText = string.Empty;
        private string _heroCurrencyHudText = string.Empty;
        private string _heroVitalityHudText = string.Empty;
        private string _nextDevelopmentRewardText = string.Empty;
        private string _availablePointsText = string.Empty;
        private string _freeAttributePointsText = string.Empty;
        private string _freeFocusPointsText = string.Empty;
        private string _currencyText = string.Empty;
        private string _attributesText = string.Empty;
        private string _contentTitle = string.Empty;
        private string _contentText = string.Empty;
        private string _statusText = string.Empty;
        private string _creationAttributesText = string.Empty;
        private string _creationSkillsText = string.Empty;
        private string _lifeStageText = string.Empty;
        private string _selectedSkillId = "OneHanded";
        private string _selectedSkillName = string.Empty;
        private string _selectedSkillDescription = string.Empty;
        private string _selectedSkillAttribute = string.Empty;
        private string _selectedSkillHowToLearn = string.Empty;
        private string _selectedSkillEffects = string.Empty;
        private string _selectedSkillLevelText = string.Empty;
        private string _selectedSkillLevelNumberText = string.Empty;
        private string _selectedSkillFocusText = string.Empty;
        private string _selectedSkillExperienceText = string.Empty;
        private string _selectedSkillLearningRateText = string.Empty;
        private string _selectedSkillLearningLimitText = string.Empty;
        private string _selectedPerkDescription = string.Empty;
        private string _restingPerkDescription = string.Empty;
        private string _selectedSkillHeading = string.Empty;
        private float _heroExperienceBarWidth;
        private float _selectedSkillBarWidth;
        private float _selectedSkillProgressWidth;
        private float _selectedSkillLearningLimitOffset;
        private float _selectedSkillLearningRate;
        private int _selectedSkillLevel;
        private int _selectedSkillLearningLimit;
        private int _selectedSkillFocusLevel;
        private bool _canSpendAttributePoints;
        private bool _canInvestSelectedSkill;
        private bool _canApplyDevelopmentChanges;
        private string _vigorValue = string.Empty;
        private string _controlValue = string.Empty;
        private string _enduranceValue = string.Empty;
        private string _cunningValue = string.Empty;
        private string _socialValue = string.Empty;
        private string _intelligenceValue = string.Empty;
        private readonly List<ItemObject> _itemStoreCatalog =
            new List<ItemObject>();
        private bool _storePriceAscending = true;
        private DikozemieStoreCategory _storeCategory =
            DikozemieStoreCategory.All;
        private string _storeSearchText = string.Empty;
        private string _storePageText = string.Empty;
        private string _storeTitle = "РЫНОК";
        private string _storeEmptyText = string.Empty;
        private ItemImageIdentifierVM _inventoryDragImageIdentifier;
        private bool _isInventoryDragVisualVisible;
        private float _inventoryDragVisualX;
        private float _inventoryDragVisualY;
        private readonly Dictionary<string, int> _pendingFocusIncreases =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _pendingAttributeIncreases =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _pendingPerkChoices =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal DikozemieHeroMenuVM()
        {
            IReadOnlyList<string> creationErrors =
                DikozemieNativeCreationCatalog.Validate();
            // A missing description only leaves one line blank; it must not
            // lock the whole Hero tab as it did for an English game.
            List<string> structuralErrors = creationErrors
                .Where(error => !error.Contains(
                    DikozemieNativeCreationCatalog.MissingTextError))
                .ToList();
            if (structuralErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Native character-creation catalogue is invalid: " +
                    string.Join("; ", structuralErrors));
            }
            if (creationErrors.Count > structuralErrors.Count)
            {
                DikozemieHeroMenuController.TraceLifecycle(
                    "native creation texts missing: " +
                    (creationErrors.Count - structuralErrors.Count));
            }
            DikozemieHeroMenuController.TraceLifecycle(
                "native creation audit passed: cultures=7 paths=" +
                DikozemieNativeCreationCatalog.CompleteNarrativePathCount);
            HeroVisual = new CharacterViewModel();
            FamilyVisualLeft = new CharacterViewModel();
            FamilyVisualRight = new CharacterViewModel();
            InventoryDragImageIdentifier = new ItemImageIdentifierVM(
                null,
                string.Empty);
            CreationAttributeGroups =
                new MBBindingList<DikozemieCreationAttributePreviewVM>();
            SkillItems = new MBBindingList<DikozemieHeroSkillItemVM>();
            PerkItems = new MBBindingList<DikozemieHeroPerkNodeVM>();
            PerkTiers = new MBBindingList<DikozemieHeroPerkTierVM>();
            StoreItems = new MBBindingList<DikozemieStoreItemVM>();
            InventoryItems = new MBBindingList<DikozemieHeroInventoryItemVM>();
            EquippedItems = new MBBindingList<DikozemieHeroInventoryItemVM>();
            ArmorEquipmentSlots = new MBBindingList<DikozemieHeroEquipmentSlotVM>();
            WeaponEquipmentSlots = new MBBindingList<DikozemieHeroEquipmentSlotVM>();
            BannerEquipmentSlots = new MBBindingList<DikozemieHeroEquipmentSlotVM>();
            ItemTooltip = new DikozemieItemTooltipVM();
            Refresh();
        }

        [DataSourceProperty]
        public CharacterViewModel HeroVisual { get; }

        [DataSourceProperty]
        public CharacterViewModel FamilyVisualLeft { get; }

        [DataSourceProperty]
        public CharacterViewModel FamilyVisualRight { get; }

        [DataSourceProperty]
        public MBBindingList<DikozemieCreationAttributePreviewVM>
            CreationAttributeGroups { get; }

        [DataSourceProperty]
        public MBBindingList<DikozemieHeroSkillItemVM> SkillItems { get; }

        [DataSourceProperty]
        public MBBindingList<DikozemieHeroPerkNodeVM> PerkItems { get; }

        [DataSourceProperty]
        public MBBindingList<DikozemieHeroPerkTierVM> PerkTiers { get; }

        [DataSourceProperty]
        public MBBindingList<DikozemieStoreItemVM> StoreItems { get; }

        [DataSourceProperty]
        public MBBindingList<DikozemieHeroInventoryItemVM> InventoryItems { get; }

        [DataSourceProperty]
        public MBBindingList<DikozemieHeroInventoryItemVM> EquippedItems { get; }

        [DataSourceProperty]
        public MBBindingList<DikozemieHeroEquipmentSlotVM> ArmorEquipmentSlots { get; }

        [DataSourceProperty]
        public MBBindingList<DikozemieHeroEquipmentSlotVM> WeaponEquipmentSlots { get; }

        [DataSourceProperty]
        public MBBindingList<DikozemieHeroEquipmentSlotVM> BannerEquipmentSlots { get; }

        [DataSourceProperty]
        public DikozemieItemTooltipVM ItemTooltip { get; }

        [DataSourceProperty]
        public bool IsVisible
        {
            get => _isVisible;
            set { if (_isVisible != value) { _isVisible = value; OnPropertyChangedWithValue(value); } }
        }

        [DataSourceProperty]
        public bool IsCreationMode
        {
            get => _isCreationMode;
            set { if (_isCreationMode != value) { _isCreationMode = value; OnPropertyChangedWithValue(value); OnPropertyChanged(nameof(IsProfileMode)); OnPropertyChanged(nameof(IsNarrativeStage)); OnPropertyChanged(nameof(IsFamilyCreationStage)); OnPropertyChanged(nameof(IsSoloCreationStage)); OnPropertyChanged(nameof(IsLobbyHeaderVisible)); } }
        }

        [DataSourceProperty]
        public bool IsProfileMode => !IsCreationMode;

        [DataSourceProperty]
        public bool IsNarrativeStage => IsCreationMode && !IsCultureStage && !IsReviewStage;

        [DataSourceProperty]
        public bool IsFamilyCreationStage => IsNarrativeStage && _creationStep == 0;

        [DataSourceProperty]
        public bool IsSoloCreationStage => IsNarrativeStage && _creationStep > 0;

        [DataSourceProperty]
        public bool IsCultureStage
        {
            get => _isCultureStage;
            set
            {
                if (_isCultureStage == value)
                {
                    return;
                }
                _isCultureStage = value;
                OnPropertyChangedWithValue(value);
                OnPropertyChanged(nameof(IsNarrativeStage));
                OnPropertyChanged(nameof(IsFamilyCreationStage));
                OnPropertyChanged(nameof(IsSoloCreationStage));
                OnPropertyChanged(nameof(IsLobbyHeaderVisible));
            }
        }

        [DataSourceProperty]
        public bool IsLobbyHeaderVisible => !IsCreationMode;

        [DataSourceProperty]
        public bool CanAdvanceCulture => _selectedCulture >= 0;

        [DataSourceProperty]
        public bool IsReviewStage
        {
            get => _isReviewStage;
            set
            {
                if (_isReviewStage == value)
                {
                    return;
                }
                _isReviewStage = value;
                OnPropertyChangedWithValue(value);
                OnPropertyChanged(nameof(IsNarrativeStage));
                OnPropertyChanged(nameof(IsFamilyCreationStage));
                OnPropertyChanged(nameof(IsSoloCreationStage));
            }
        }

        [DataSourceProperty]
        public bool IsOverview => _section == 0;

        [DataSourceProperty]
        public bool IsInventory => _section == 1;

        [DataSourceProperty]
        public bool IsSkills => _section == 2;

        [DataSourceProperty]
        public bool IsQuests => _section == 3;

        [DataSourceProperty]
        public string QuestionText { get => _questionText; set => SetString(ref _questionText, value); }

        [DataSourceProperty]
        public string QuestionProgress { get => _questionProgress; set => SetString(ref _questionProgress, value); }

        [DataSourceProperty]
        public string Choice1Text { get => _choice1Text; set => SetString(ref _choice1Text, value); }

        [DataSourceProperty]
        public string Choice2Text { get => _choice2Text; set => SetString(ref _choice2Text, value); }

        [DataSourceProperty]
        public string Choice3Text { get => _choice3Text; set => SetString(ref _choice3Text, value); }

        [DataSourceProperty]
        public string Choice4Text { get => _choice4Text; set => SetString(ref _choice4Text, value); }

        [DataSourceProperty]
        public string Choice1Hint { get => _choice1Hint; set => SetString(ref _choice1Hint, value); }

        [DataSourceProperty]
        public string Choice2Hint { get => _choice2Hint; set => SetString(ref _choice2Hint, value); }

        [DataSourceProperty]
        public string Choice3Hint { get => _choice3Hint; set => SetString(ref _choice3Hint, value); }

        [DataSourceProperty]
        public string Choice4Hint { get => _choice4Hint; set => SetString(ref _choice4Hint, value); }

        [DataSourceProperty]
        public string Choice5Text { get => _choice5Text; set => SetString(ref _choice5Text, value); }

        [DataSourceProperty]
        public string Choice6Text { get => _choice6Text; set => SetString(ref _choice6Text, value); }

        [DataSourceProperty]
        public string Choice7Text { get => _choice7Text; set => SetString(ref _choice7Text, value); }

        [DataSourceProperty]
        public string Choice8Text { get => _choice8Text; set => SetString(ref _choice8Text, value); }

        [DataSourceProperty]
        public string SelectedChoiceHint { get => _selectedChoiceHint; set => SetString(ref _selectedChoiceHint, value); }

        [DataSourceProperty]
        public string SelectedChoiceDescription
        {
            get => _selectedChoiceDescription;
            set => SetString(ref _selectedChoiceDescription, value);
        }

        [DataSourceProperty]
        public string SelectedChoiceEffectText
        {
            get => _selectedChoiceEffectText;
            set => SetString(ref _selectedChoiceEffectText, value);
        }

        [DataSourceProperty]
        public string CreationStageTitle
        {
            get => _creationStageTitle;
            set => SetString(ref _creationStageTitle, value);
        }

        [DataSourceProperty]
        public bool HasSelectedChoice => _selectedChoice >= 0;

        [DataSourceProperty]
        public string HeroName { get => _heroName; set => SetString(ref _heroName, value); }

        [DataSourceProperty]
        public string ReviewText { get => _reviewText; set => SetString(ref _reviewText, value); }

        [DataSourceProperty]
        public string CultureDescription { get => _cultureDescription; set => SetString(ref _cultureDescription, value); }

        [DataSourceProperty]
        public string CultureName { get => _cultureName; set => SetString(ref _cultureName, value); }

        [DataSourceProperty]
        public string CulturePositiveFeat1 { get => _culturePositiveFeat1; set => SetString(ref _culturePositiveFeat1, value); }

        [DataSourceProperty]
        public string CulturePositiveFeat2 { get => _culturePositiveFeat2; set => SetString(ref _culturePositiveFeat2, value); }

        [DataSourceProperty]
        public string CulturePositiveFeat3 { get => _culturePositiveFeat3; set => SetString(ref _culturePositiveFeat3, value); }

        [DataSourceProperty]
        public bool HasCulturePositiveFeat3 => !string.IsNullOrWhiteSpace(CulturePositiveFeat3);

        [DataSourceProperty]
        public string CultureNegativeFeat { get => _cultureNegativeFeat; set => SetString(ref _cultureNegativeFeat, value); }

        [DataSourceProperty]
        public string SelectedCultureId { get => _selectedCultureId; set => SetString(ref _selectedCultureId, value); }

        [DataSourceProperty]
        public bool IsCulture1Selected => _selectedCulture == 0;
        [DataSourceProperty]
        public bool IsCulture2Selected => _selectedCulture == 1;
        [DataSourceProperty]
        public bool IsCulture3Selected => _selectedCulture == 2;
        [DataSourceProperty]
        public bool IsCulture4Selected => _selectedCulture == 3;
        [DataSourceProperty]
        public bool IsCulture5Selected => _selectedCulture == 4;
        [DataSourceProperty]
        public bool IsCulture6Selected => _selectedCulture == 5;
        [DataSourceProperty]
        public bool IsCulture7Selected => _selectedCulture == 6;

        [DataSourceProperty]
        public bool HasChoice5 => !string.IsNullOrWhiteSpace(Choice5Text);

        [DataSourceProperty]
        public bool HasChoice6 => !string.IsNullOrWhiteSpace(Choice6Text);

        [DataSourceProperty]
        public bool HasChoice7 => !string.IsNullOrWhiteSpace(Choice7Text);

        [DataSourceProperty]
        public bool HasChoice8 => !string.IsNullOrWhiteSpace(Choice8Text);

        [DataSourceProperty]
        public bool CanContinue => _selectedChoice >= 0;

        [DataSourceProperty]
        public bool CanGoBack => IsCreationMode;

        [DataSourceProperty]
        public bool IsChoice1Selected => _selectedChoice == 0;
        [DataSourceProperty]
        public bool IsChoice2Selected => _selectedChoice == 1;
        [DataSourceProperty]
        public bool IsChoice3Selected => _selectedChoice == 2;
        [DataSourceProperty]
        public bool IsChoice4Selected => _selectedChoice == 3;
        [DataSourceProperty]
        public bool IsChoice5Selected => _selectedChoice == 4;
        [DataSourceProperty]
        public bool IsChoice6Selected => _selectedChoice == 5;
        [DataSourceProperty]
        public bool IsChoice7Selected => _selectedChoice == 6;
        [DataSourceProperty]
        public bool IsChoice8Selected => _selectedChoice == 7;

        [DataSourceProperty]
        public string HeroTitle { get => _heroTitle; set => SetString(ref _heroTitle, value); }

        [DataSourceProperty]
        public string ProgressText { get => _progressText; set => SetString(ref _progressText, value); }

        [DataSourceProperty]
        public string HeroLevelHudText { get => _heroLevelHudText; set => SetString(ref _heroLevelHudText, value); }

        [DataSourceProperty]
        public string HeroExperienceHudText { get => _heroExperienceHudText; set => SetString(ref _heroExperienceHudText, value); }

        [DataSourceProperty]
        public string HeroCurrencyHudText { get => _heroCurrencyHudText; set => SetString(ref _heroCurrencyHudText, value); }

        [DataSourceProperty]
        public string HeroVitalityHudText { get => _heroVitalityHudText; set => SetString(ref _heroVitalityHudText, value); }

        [DataSourceProperty]
        public string NextDevelopmentRewardText { get => _nextDevelopmentRewardText; set => SetString(ref _nextDevelopmentRewardText, value); }

        [DataSourceProperty]
        public string AvailablePointsText { get => _availablePointsText; set => SetString(ref _availablePointsText, value); }

        [DataSourceProperty]
        public string FreeAttributePointsText { get => _freeAttributePointsText; set => SetString(ref _freeAttributePointsText, value); }

        [DataSourceProperty]
        public string FreeFocusPointsText { get => _freeFocusPointsText; set => SetString(ref _freeFocusPointsText, value); }

        [DataSourceProperty]
        public bool CanSpendAttributePoints
        {
            get => _canSpendAttributePoints;
            set
            {
                if (_canSpendAttributePoints != value)
                {
                    _canSpendAttributePoints = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public bool CanInvestSelectedSkill
        {
            get => _canInvestSelectedSkill;
            set
            {
                if (_canInvestSelectedSkill != value)
                {
                    _canInvestSelectedSkill = value;
                    OnPropertyChangedWithValue(value);
                }
            }
        }

        [DataSourceProperty]
        public bool CanApplyDevelopmentChanges
        {
            get => _canApplyDevelopmentChanges;
            set
            {
                if (_canApplyDevelopmentChanges != value)
                {
                    _canApplyDevelopmentChanges = value;
                    OnPropertyChangedWithValue(value);
                    OnPropertyChanged(nameof(CannotApplyDevelopmentChanges));
                }
            }
        }

        [DataSourceProperty]
        public bool CannotApplyDevelopmentChanges => !CanApplyDevelopmentChanges;

        [DataSourceProperty]
        public string CurrencyText { get => _currencyText; set => SetString(ref _currencyText, value); }

        [DataSourceProperty]
        public string StorePageText { get => _storePageText; set => SetString(ref _storePageText, value); }

        // The market of the place where the hero stands (DikozemieTown).
        [DataSourceProperty]
        public string StoreTitle { get => _storeTitle; set => SetString(ref _storeTitle, value); }

        [DataSourceProperty]
        public string StoreEmptyText
        {
            get => _storeEmptyText;
            set
            {
                SetString(ref _storeEmptyText, value);
                OnPropertyChanged(nameof(IsStoreEmpty));
            }
        }

        [DataSourceProperty]
        public bool IsStoreEmpty => !string.IsNullOrEmpty(_storeEmptyText);

        [DataSourceProperty]
        public ItemImageIdentifierVM InventoryDragImageIdentifier
        {
            get => _inventoryDragImageIdentifier;
            private set
            {
                if (ReferenceEquals(_inventoryDragImageIdentifier, value))
                {
                    return;
                }
                _inventoryDragImageIdentifier = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public bool IsInventoryDragVisualVisible
        {
            get => _isInventoryDragVisualVisible;
            private set
            {
                if (_isInventoryDragVisualVisible == value)
                {
                    return;
                }
                _isInventoryDragVisualVisible = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public float InventoryDragVisualX
        {
            get => _inventoryDragVisualX;
            private set
            {
                if (Math.Abs(_inventoryDragVisualX - value) < 0.1f)
                {
                    return;
                }
                _inventoryDragVisualX = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public float InventoryDragVisualY
        {
            get => _inventoryDragVisualY;
            private set
            {
                if (Math.Abs(_inventoryDragVisualY - value) < 0.1f)
                {
                    return;
                }
                _inventoryDragVisualY = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string StorePriceSortText =>
            _storePriceAscending ? "ЦЕНА  ▲" : "ЦЕНА  ▼";

        [DataSourceProperty]
        public string StoreSearchText
        {
            get => _storeSearchText;
            set => SetString(ref _storeSearchText, value);
        }

        [DataSourceProperty]
        public bool IsStoreAll => _storeCategory == DikozemieStoreCategory.All;
        [DataSourceProperty]
        public bool IsStoreWeapons => _storeCategory == DikozemieStoreCategory.Weapons;
        [DataSourceProperty]
        public bool IsStoreShields => _storeCategory == DikozemieStoreCategory.Shields;
        [DataSourceProperty]
        public bool IsStoreArmor => _storeCategory == DikozemieStoreCategory.Armor;
        [DataSourceProperty]
        public bool IsStoreMounts => _storeCategory == DikozemieStoreCategory.Mounts;
        [DataSourceProperty]
        public bool IsStoreGoods => _storeCategory == DikozemieStoreCategory.Goods;

        [DataSourceProperty]
        public string AttributesText { get => _attributesText; set => SetString(ref _attributesText, value); }

        [DataSourceProperty]
        public string ContentTitle { get => _contentTitle; set => SetString(ref _contentTitle, value); }

        [DataSourceProperty]
        public string ContentText { get => _contentText; set => SetString(ref _contentText, value); }

        [DataSourceProperty]
        public string StatusText { get => _statusText; set => SetString(ref _statusText, value); }

        [DataSourceProperty]
        public string CreationAttributesText { get => _creationAttributesText; set => SetString(ref _creationAttributesText, value); }

        [DataSourceProperty]
        public string CreationSkillsText { get => _creationSkillsText; set => SetString(ref _creationSkillsText, value); }

        [DataSourceProperty]
        public string LifeStageText { get => _lifeStageText; set => SetString(ref _lifeStageText, value); }

        [DataSourceProperty]
        public string SelectedSkillName { get => _selectedSkillName; set => SetString(ref _selectedSkillName, value); }

        [DataSourceProperty]
        public string SelectedSkillId => _selectedSkillId;

        [DataSourceProperty]
        public string SelectedSkillDescription { get => _selectedSkillDescription; set => SetString(ref _selectedSkillDescription, value); }

        [DataSourceProperty]
        public string SelectedSkillAttribute { get => _selectedSkillAttribute; set => SetString(ref _selectedSkillAttribute, value); }

        [DataSourceProperty]
        public string SelectedSkillHowToLearn { get => _selectedSkillHowToLearn; set => SetString(ref _selectedSkillHowToLearn, value); }

        [DataSourceProperty]
        public string SelectedSkillEffects { get => _selectedSkillEffects; set => SetString(ref _selectedSkillEffects, value); }

        [DataSourceProperty]
        public string SelectedSkillLevelText { get => _selectedSkillLevelText; set => SetString(ref _selectedSkillLevelText, value); }

        [DataSourceProperty]
        public string SelectedSkillLevelNumberText { get => _selectedSkillLevelNumberText; set => SetString(ref _selectedSkillLevelNumberText, value); }

        [DataSourceProperty]
        public string SelectedSkillFocusText { get => _selectedSkillFocusText; set => SetString(ref _selectedSkillFocusText, value); }

        [DataSourceProperty]
        public string SelectedSkillExperienceText { get => _selectedSkillExperienceText; set => SetString(ref _selectedSkillExperienceText, value); }

        [DataSourceProperty]
        public string SelectedSkillLearningRateText { get => _selectedSkillLearningRateText; set => SetString(ref _selectedSkillLearningRateText, value); }

        [DataSourceProperty]
        public string SelectedSkillLearningLimitText { get => _selectedSkillLearningLimitText; set => SetString(ref _selectedSkillLearningLimitText, value); }

        [DataSourceProperty]
        public float SelectedSkillLearningRate
        {
            get => _selectedSkillLearningRate;
            set { if (Math.Abs(_selectedSkillLearningRate - value) > 0.001f) { _selectedSkillLearningRate = value; OnPropertyChangedWithValue(value); } }
        }

        [DataSourceProperty]
        public int SelectedSkillLevel
        {
            get => _selectedSkillLevel;
            set { if (_selectedSkillLevel != value) { _selectedSkillLevel = value; OnPropertyChangedWithValue(value); } }
        }

        [DataSourceProperty]
        public int SelectedSkillLearningLimit
        {
            get => _selectedSkillLearningLimit;
            set { if (_selectedSkillLearningLimit != value) { _selectedSkillLearningLimit = value; OnPropertyChangedWithValue(value); } }
        }

        [DataSourceProperty]
        public string SelectedPerkDescription { get => _selectedPerkDescription; set => SetString(ref _selectedPerkDescription, value); }

        [DataSourceProperty]
        public string SelectedSkillHeading { get => _selectedSkillHeading; set => SetString(ref _selectedSkillHeading, value); }

        [DataSourceProperty]
        public float HeroExperienceBarWidth
        {
            get => _heroExperienceBarWidth;
            set { if (Math.Abs(_heroExperienceBarWidth - value) > 0.01f) { _heroExperienceBarWidth = value; OnPropertyChangedWithValue(value); } }
        }

        [DataSourceProperty]
        public float SelectedSkillBarWidth
        {
            get => _selectedSkillBarWidth;
            set { if (Math.Abs(_selectedSkillBarWidth - value) > 0.01f) { _selectedSkillBarWidth = value; OnPropertyChangedWithValue(value); } }
        }

        [DataSourceProperty]
        public float SelectedSkillProgressWidth
        {
            get => _selectedSkillProgressWidth;
            set { if (Math.Abs(_selectedSkillProgressWidth - value) > 0.01f) { _selectedSkillProgressWidth = value; OnPropertyChangedWithValue(value); } }
        }

        [DataSourceProperty]
        public float SelectedSkillLearningLimitOffset
        {
            get => _selectedSkillLearningLimitOffset;
            set { if (Math.Abs(_selectedSkillLearningLimitOffset - value) > 0.01f) { _selectedSkillLearningLimitOffset = value; OnPropertyChangedWithValue(value); } }
        }

        [DataSourceProperty]
        public int SelectedSkillFocusLevel
        {
            get => _selectedSkillFocusLevel;
            set
            {
                if (_selectedSkillFocusLevel == value)
                {
                    return;
                }
                _selectedSkillFocusLevel = value;
                OnPropertyChangedWithValue(value);
                OnPropertyChanged(nameof(IsSelectedSkillFocus1Filled));
                OnPropertyChanged(nameof(IsSelectedSkillFocus2Filled));
                OnPropertyChanged(nameof(IsSelectedSkillFocus3Filled));
                OnPropertyChanged(nameof(IsSelectedSkillFocus4Filled));
                OnPropertyChanged(nameof(IsSelectedSkillFocus5Filled));
            }
        }

        [DataSourceProperty]
        public bool IsSelectedSkillFocus1Filled => SelectedSkillFocusLevel >= 1;
        [DataSourceProperty]
        public bool IsSelectedSkillFocus2Filled => SelectedSkillFocusLevel >= 2;
        [DataSourceProperty]
        public bool IsSelectedSkillFocus3Filled => SelectedSkillFocusLevel >= 3;
        [DataSourceProperty]
        public bool IsSelectedSkillFocus4Filled => SelectedSkillFocusLevel >= 4;
        [DataSourceProperty]
        public bool IsSelectedSkillFocus5Filled => SelectedSkillFocusLevel >= 5;

        [DataSourceProperty]
        public string VigorValue { get => _vigorValue; set => SetString(ref _vigorValue, value); }
        [DataSourceProperty]
        public string ControlValue { get => _controlValue; set => SetString(ref _controlValue, value); }
        [DataSourceProperty]
        public string EnduranceValue { get => _enduranceValue; set => SetString(ref _enduranceValue, value); }
        [DataSourceProperty]
        public string CunningValue { get => _cunningValue; set => SetString(ref _cunningValue, value); }
        [DataSourceProperty]
        public string SocialValue { get => _socialValue; set => SetString(ref _socialValue, value); }
        [DataSourceProperty]
        public string IntelligenceValue { get => _intelligenceValue; set => SetString(ref _intelligenceValue, value); }

        public void ExecuteChoice1() => Choose(0);
        public void ExecuteChoice2() => Choose(1);
        public void ExecuteChoice3() => Choose(2);
        public void ExecuteChoice4() => Choose(3);
        public void ExecuteChoice5() => Choose(4);
        public void ExecuteChoice6() => Choose(5);
        public void ExecuteChoice7() => Choose(6);
        public void ExecuteChoice8() => Choose(7);
        public void ExecuteCulture1() => ChooseCulture(0);
        public void ExecuteCulture2() => ChooseCulture(1);
        public void ExecuteCulture3() => ChooseCulture(2);
        public void ExecuteCulture4() => ChooseCulture(3);
        public void ExecuteCulture5() => ChooseCulture(4);
        public void ExecuteCulture6() => ChooseCulture(5);
        public void ExecuteCulture7() => ChooseCulture(6);

        public void ExecuteLobbyExit()
        {
            DikozemieLobbyShellIntegration.TryHandleLobbyExitButtonClick();
        }

        public void ExecuteCultureNext()
        {
            DikozemieLobbyShellIntegration.TraceButtonAction(
                "hero-culture-next command selected=" + _selectedCulture);
            if (_selectedCulture < 0 || !DikozemieHeroProfile.ConfirmCultureSelection())
            {
                StatusText = "Сначала выберите культуру.";
                return;
            }
            DikozemieLobbyShellIntegration.TraceButtonAction(
                "hero-culture-next culture confirmed");
            DikozemieHeroAppearanceEditor.Open();
        }

        public void ExecuteCultureBack()
        {
            DikozemieLobbyShellIntegration.SelectTopTab(0);
        }

        public void ExecuteOverview() { ItemTooltip.Hide(); _section = 0; RefreshSection(); }
        public void ExecuteInventory() { _section = 1; RefreshSection(); }
        public void ExecuteSkills() { ItemTooltip.Hide(); _section = 2; RefreshSection(); }
        public void ExecuteQuests() { ItemTooltip.Hide(); _section = 3; RefreshSection(); }

        public void ExecuteInventoryDone()
        {
            DikozemieLobbyShellIntegration.TraceButtonAction(
                "inventory transaction confirmed; store remains open");
            StatusText = "Сделка применена. Баланс и инвентарь сохранены.";
            RefreshHeader();
            RefreshHeroVisual();
            RefreshInventory();
        }

        public void ExecuteStoreAll() => SetStoreCategory(DikozemieStoreCategory.All);
        public void ExecuteStoreWeapons() => SetStoreCategory(DikozemieStoreCategory.Weapons);
        public void ExecuteStoreShields() => SetStoreCategory(DikozemieStoreCategory.Shields);
        public void ExecuteStoreArmor() => SetStoreCategory(DikozemieStoreCategory.Armor);
        public void ExecuteStoreMounts() => SetStoreCategory(DikozemieStoreCategory.Mounts);
        public void ExecuteStoreGoods() => SetStoreCategory(DikozemieStoreCategory.Goods);

        public void ExecuteToggleStorePriceSort()
        {
            _storePriceAscending = !_storePriceAscending;
            OnPropertyChanged(nameof(StorePriceSortText));
            DikozemieHeroMenuController.TraceLifecycle(
                "store price sort clicked direction=" +
                (_storePriceAscending ? "ascending" : "descending"));
            RefreshInventory();
        }

        public void ExecuteApplyStoreSearch()
        {
            RefreshInventory();
        }

        public void ExecuteClearStoreSearch()
        {
            StoreSearchText = string.Empty;
            RefreshInventory();
        }

        public void ExecuteEditAppearance()
        {
            DikozemieHeroAppearanceEditor.Open();
        }

        public void ExecutePrevious()
        {
            if (IsReviewStage)
            {
                _creationStep = DikozemieHeroProfile.CreationStepCount - 1;
                DikozemieHeroProfile.SetCreationStep(_creationStep);
                IsReviewStage = false;
                RefreshQuestion();
                return;
            }
            if (_creationStep <= 0)
            {
                DikozemieHeroProfile.MarkAppearanceConfirmed(false);
                DikozemieHeroAppearanceEditor.Open();
                return;
            }
            _creationStep--;
            DikozemieHeroProfile.SetCreationStep(_creationStep);
            RefreshQuestion();
        }

        public void ExecuteNext()
        {
            if (_selectedChoice < 0)
            {
                StatusText = "Сначала выберите один вариант.";
                return;
            }
            if (_creationStep + 1 >= DikozemieHeroProfile.CreationStepCount)
            {
                DikozemieHeroProfile.SetCreationStep(DikozemieHeroProfile.CreationStepCount);
                _creationStep = DikozemieHeroProfile.CreationStepCount;
                RefreshReview();
                return;
            }
            _creationStep++;
            DikozemieHeroProfile.SetCreationStep(_creationStep);
            RefreshQuestion();
        }

        public void ExecuteFinishCreation()
        {
            if (!DikozemieHeroProfile.CompleteCreation(HeroName))
            {
                StatusText = "Не все этапы предыстории завершены.";
                return;
            }
            IsReviewStage = false;
            IsCreationMode = false;
            _section = 2;
            StatusText = "Герой создан. Арена и карта разблокированы.";
            RefreshHeader();
            RefreshSection();
            DikozemieLobbyShellIntegration.SelectTopTab(4);
        }

        internal void ShowProfileSection(int section)
        {
            if (!DikozemieHeroProfile.IsCreated)
            {
                return;
            }
            IsCultureStage = false;
            IsReviewStage = false;
            IsCreationMode = false;
            _section = Math.Max(0, Math.Min(3, section));
            RefreshHeader();
            RefreshSection();
            RefreshHeroVisual();
            DikozemieHeroMenuController.TraceLifecycle(
                "profile section=" + _section +
                " created=" + DikozemieHeroProfile.IsCreated);
        }

        public void ExecuteResetHero()
        {
            InformationManager.ShowInquiry(
                new InquiryData(
                    "УДАЛИТЬ ГЕРОЯ И НАЧАТЬ ЗАНОВО?",
                    "Вы точно хотите удалить героя? Сгорят рейтинг, победы, уровень, " +
                    "опыт, навыки, перки, купленные предметы, экипировка и внешность. " +
                    "Монеты останутся на счёте: на них новый герой сможет купить снаряжение.\n\n" +
                    "Новый герой начнёт на родине пешком, в одежде и с оружием по новым ответам. " +
                    "Коня нужно заработать и купить на рынке.",
                    true,
                    true,
                    "ДА, УДАЛИТЬ",
                    "ОТМЕНА",
                    () =>
                    {
                        DikozemieHeroProfile.ResetHero();
                        _creationStep = 0;
                        _selectedChoice = -1;
                        _selectedCulture = -1;
                        _section = 0;
                        ClearPendingDevelopmentChanges();
                        StatusText = string.Empty;
                        Refresh();
                        // The skills page must not stay over the new hero's creation.
                        RefreshSection();
                    },
                    null),
                true,
                true);
        }

        public void ExecuteAddOneHanded() => AddFocus("OneHanded", "Одноручное");
        public void ExecuteAddTwoHanded() => AddFocus("TwoHanded", "Двуручное");
        public void ExecuteAddBow() => AddFocus("Bow", "Лук");
        public void ExecuteAddAthletics() => AddFocus("Athletics", "Атлетика");
        public void ExecuteAddVigor() => AddAttribute("vigor", "Сила");
        public void ExecuteAddControl() => AddAttribute("control", "Контроль");
        public void ExecuteAddEndurance() => AddAttribute("endurance", "Выносливость");
        public void ExecuteAddCunning() => AddAttribute("cunning", "Хитрость");
        public void ExecuteAddSocial() => AddAttribute("social", "Общение");
        public void ExecuteAddIntelligence() => AddAttribute("intelligence", "Интеллект");

        public void ExecuteInvestSelectedSkill()
        {
            AddFocus(_selectedSkillId, GetSkillName(_selectedSkillId));
        }

        public void ExecuteApplyDevelopmentChanges()
        {
            int focusCost = _pendingFocusIncreases.Values.Sum();
            int attributeCost = _pendingAttributeIncreases.Values.Sum();
            int perkCount = _pendingPerkChoices.Count;
            string focusTargets = string.Join(",", _pendingFocusIncreases
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key + ":" + pair.Value));
            string attributeTargets = string.Join(",", _pendingAttributeIncreases
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => pair.Key + ":" + pair.Value));
            if (!CanApplyDevelopmentChanges)
            {
                // The native Gauntlet command and the multiplayer input
                // fallback can both observe the same mouse release.  The
                // first invocation has already committed and cleared the
                // plan; the duplicate must be a harmless no-op instead of
                // replacing the success message with a false failure.
                DikozemieHeroMenuController.TraceLifecycle(
                    "development apply duplicate ignored");
                return;
            }
            if (!DikozemieHeroProfile.TryApplyDevelopmentPlan(
                    _pendingFocusIncreases,
                    _pendingAttributeIncreases,
                    _pendingPerkChoices))
            {
                DikozemieHeroMenuController.TraceLifecycle(
                    "development apply failed focus=" + focusTargets +
                    " attributes=" + attributeTargets +
                    " perks=" + perkCount);
                ClearPendingDevelopmentChanges();
                StatusText = "Не удалось применить очки: профиль изменился. " +
                    "Распределите их заново.";
                RefreshHeader();
                RefreshSkills();
                return;
            }

            DikozemieHeroMenuController.TraceLifecycle(
                "development applied focus=" + focusTargets +
                " attributes=" + attributeTargets +
                " perks=" + perkCount);
            ClearPendingDevelopmentChanges();
            StatusText = "Изменения применены: фокус +" + focusCost +
                ", характеристики +" + attributeCost +
                ", перки +" + perkCount +
                ". Они действуют в следующем бою.";
            RefreshHeader();
            RefreshSkills();
        }

        public void ExecuteDiscardDevelopmentChanges()
        {
            if (!CanApplyDevelopmentChanges)
            {
                return;
            }
            ClearPendingDevelopmentChanges();
            StatusText = "Неприменённые изменения отменены.";
            RefreshHeader();
            RefreshSkills();
        }

        internal void Refresh()
        {
            DikozemieHeroProfile.Initialize();
            IsCreationMode = !DikozemieHeroProfile.IsCreated;
            if (IsCreationMode)
            {
                if (!DikozemieHeroProfile.CultureConfirmed ||
                    !DikozemieHeroProfile.AppearanceConfirmed)
                {
                    RefreshCulture();
                    return;
                }
                IsCultureStage = false;
                _creationStep = Math.Max(0, Math.Min(
                    DikozemieHeroProfile.CreationStepCount,
                    DikozemieHeroProfile.Snapshot.CreationStep));
                HeroName = DikozemieHeroProfile.Snapshot.Name;
                if (_creationStep >= DikozemieHeroProfile.CreationStepCount)
                {
                    RefreshReview();
                }
                else
                {
                    IsReviewStage = false;
                    RefreshQuestion();
                }
            }
            else
            {
                RefreshHeader();
                RefreshSection();
            }
        }

        private void Choose(int index)
        {
            IReadOnlyList<DikozemieHeroBackgroundChoice> choices =
                DikozemieHeroProfile.GetCreationChoices(_creationStep);
            if (index < 0 || index >= choices.Count)
            {
                return;
            }
            DikozemieHeroProfile.SelectCreationChoice(_creationStep, index);
            _selectedChoice = index;
            StatusText = string.Empty;
            RefreshSelectedChoicePresentation(choices);
            NotifyChoiceState();
            RefreshCreationSummary();
            RefreshHeroVisual();
        }

        private void ChooseCulture(int index)
        {
            if (index < 0 || index >= CultureIds.Length)
            {
                return;
            }
            _selectedCulture = index;
            DikozemieHeroProfile.SetCulture(CultureIds[index]);
            ApplyCulturePresentation(index);
            StatusText = string.Empty;
            NotifyCultureState();
            RefreshHeroVisual();
        }

        private void RefreshCulture()
        {
            IsReviewStage = false;
            IsCultureStage = true;
            QuestionProgress = "СОЗДАНИЕ ГЕРОЯ  •  КУЛЬТУРА";
            QuestionText = "Выберите культуру героя";
            _selectedCulture = DikozemieHeroProfile.CultureConfirmed
                ? Array.FindIndex(CultureIds, id => string.Equals(
                    id, DikozemieHeroProfile.CultureId, StringComparison.Ordinal))
                : 0;
            if (_selectedCulture < 0)
            {
                _selectedCulture = 0;
            }
            if (!DikozemieHeroProfile.CultureConfirmed)
            {
                DikozemieHeroProfile.SetCulture(CultureIds[_selectedCulture]);
            }
            ApplyCulturePresentation(_selectedCulture);
            NotifyCultureState();
            RefreshHeroVisual();
        }

        private void ApplyCulturePresentation(int index)
        {
            int safeIndex = Math.Max(0, Math.Min(CultureIds.Length - 1, index));
            SelectedCultureId = CultureIds[safeIndex];
            CultureName = CultureNames[safeIndex];
            CultureDescription = CultureDescriptions[safeIndex];
            CulturePositiveFeat1 = CulturePositiveFeat1Texts[safeIndex];
            CulturePositiveFeat2 = CulturePositiveFeat2Texts[safeIndex];
            CulturePositiveFeat3 = CulturePositiveFeat3Texts[safeIndex];
            CultureNegativeFeat = CultureNegativeFeatTexts[safeIndex];
            OnPropertyChanged(nameof(HasCulturePositiveFeat3));
            LifeStageText = CultureNames[safeIndex].ToUpperInvariant();
        }

        private void NotifyCultureState()
        {
            OnPropertyChanged(nameof(IsCulture1Selected));
            OnPropertyChanged(nameof(IsCulture2Selected));
            OnPropertyChanged(nameof(IsCulture3Selected));
            OnPropertyChanged(nameof(IsCulture4Selected));
            OnPropertyChanged(nameof(IsCulture5Selected));
            OnPropertyChanged(nameof(IsCulture6Selected));
            OnPropertyChanged(nameof(IsCulture7Selected));
            OnPropertyChanged(nameof(CanAdvanceCulture));
        }

        private static string GetCultureName(string id)
        {
            switch (id)
            {
                case "vlandia": return "Вландия";
                case "sturgia": return "Стургия";
                case "aserai": return "Асераи";
                case "khuzait": return "Хузаиты";
                case "battania": return "Баттания";
                case "nords": return "Норды";
                default: return "Империя";
            }
        }

        private void RefreshQuestion()
        {
            QuestionProgress = "СОЗДАНИЕ ГЕРОЯ  •  ШАГ " + (_creationStep + 1) +
                " ИЗ " + DikozemieHeroProfile.CreationStepCount;
            QuestionText = DikozemieHeroProfile.GetCreationQuestion(_creationStep);
            IReadOnlyList<DikozemieHeroBackgroundChoice> choices =
                DikozemieHeroProfile.GetCreationChoices(_creationStep);
            string[] titles = new string[8];
            for (int i = 0; i < Math.Min(8, choices.Count); i++)
            {
                titles[i] = choices[i].Title;
            }
            Choice1Text = titles[0];
            Choice2Text = titles[1];
            Choice3Text = titles[2];
            Choice4Text = titles[3];
            Choice5Text = titles[4];
            Choice6Text = titles[5];
            Choice7Text = titles[6];
            Choice8Text = titles[7];
            Choice1Hint = choices.Count > 0 ? BuildChoiceHint(choices[0]) : string.Empty;
            Choice2Hint = choices.Count > 1 ? BuildChoiceHint(choices[1]) : string.Empty;
            Choice3Hint = choices.Count > 2 ? BuildChoiceHint(choices[2]) : string.Empty;
            Choice4Hint = choices.Count > 3 ? BuildChoiceHint(choices[3]) : string.Empty;
            _selectedChoice = DikozemieHeroProfile.GetSelectedCreationChoiceIndex(_creationStep);
            RefreshSelectedChoicePresentation(choices);
            NotifyChoiceState();
            RefreshCreationSummary();
            RefreshHeroVisual();
        }

        private static string L(string source) => DikozemieCreationText.Localize(source);

        private void RefreshSelectedChoicePresentation(
            IReadOnlyList<DikozemieHeroBackgroundChoice> choices)
        {
            if (_selectedChoice < 0 || _selectedChoice >= choices.Count)
            {
                SelectedChoiceHint =
                    "Выберите происхождение — изменения сразу появятся слева и на героях.";
                SelectedChoiceDescription = string.Empty;
                SelectedChoiceEffectText = string.Empty;
                OnPropertyChanged(nameof(HasSelectedChoice));
                return;
            }

            DikozemieHeroBackgroundChoice choice = choices[_selectedChoice];
            SelectedChoiceHint = BuildChoiceHint(choice);
            SelectedChoiceDescription = choice.Description;
            if (choice.Skills.Count == 0)
            {
                SelectedChoiceEffectText = "+" + choice.UnspentFocusPoints +
                    " очк. фокуса  •  +" + choice.UnspentAttributePoints +
                    " очк. характеристик";
            }
            else
            {
                SelectedChoiceEffectText = string.Join("  •  ", choice.Skills.Select(
                        skill => "+10 «" + GetSkillName(skill) + "», +1 фокус")) +
                    "\n+1 к характеристике «" +
                    GetAttributeName(choice.Attribute) + "»";
            }
            OnPropertyChanged(nameof(HasSelectedChoice));
        }

        private void RefreshReview()
        {
            IsReviewStage = true;
            QuestionProgress = "СОЗДАНИЕ ГЕРОЯ  •  ПРОВЕРКА";
            QuestionText = "Подтвердите историю героя";
            HeroName = string.IsNullOrWhiteSpace(HeroName)
                ? DikozemieHeroProfile.Snapshot.Name
                : HeroName;
            var lines = new List<string>();
            for (int step = 0; step < DikozemieHeroProfile.CreationStepCount; step++)
            {
                int selected = DikozemieHeroProfile.GetSelectedCreationChoiceIndex(step);
                IReadOnlyList<DikozemieHeroBackgroundChoice> choices =
                    DikozemieHeroProfile.GetCreationChoices(step);
                if (selected >= 0 && selected < choices.Count)
                {
                    lines.Add("• " + choices[selected].Title);
                }
            }
            ReviewText = string.Join("\n", lines) +
                "\n\nСложность сетевой игры: ГЕРОИЧЕСКАЯ";
            LifeStageText = "ГОТОВ К ВЫХОДУ НА АРЕНУ";
            RefreshCreationSummary();
            RefreshHeroVisual();
        }

        private void NotifyChoiceState()
        {
            OnPropertyChanged(nameof(HasChoice5));
            OnPropertyChanged(nameof(HasChoice6));
            OnPropertyChanged(nameof(HasChoice7));
            OnPropertyChanged(nameof(HasChoice8));
            OnPropertyChanged(nameof(CanContinue));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(IsChoice1Selected));
            OnPropertyChanged(nameof(IsChoice2Selected));
            OnPropertyChanged(nameof(IsChoice3Selected));
            OnPropertyChanged(nameof(IsChoice4Selected));
            OnPropertyChanged(nameof(IsChoice5Selected));
            OnPropertyChanged(nameof(IsChoice6Selected));
            OnPropertyChanged(nameof(IsChoice7Selected));
            OnPropertyChanged(nameof(IsChoice8Selected));
            OnPropertyChanged(nameof(HasSelectedChoice));
            OnPropertyChanged(nameof(IsFamilyCreationStage));
            OnPropertyChanged(nameof(IsSoloCreationStage));
        }

        private void RefreshCreationSummary()
        {
            IReadOnlyList<DikozemieHeroBackgroundChoice> choices =
                DikozemieHeroProfile.GetCreationChoices(_creationStep);
            DikozemieHeroBackgroundChoice selected =
                _selectedChoice >= 0 && _selectedChoice < choices.Count
                    ? choices[_selectedChoice]
                    : null;

            CreationAttributeGroups.Clear();
            for (int attributeIndex = 0;
                 attributeIndex < CreationAttributeIds.Length;
                 attributeIndex++)
            {
                string attributeId = CreationAttributeIds[attributeIndex];
                string[] skillIds = CreationSkillGroups[attributeIndex];
                var skills = new List<DikozemieCreationSkillPreviewVM>();
                foreach (string skillId in skillIds)
                {
                    int focus = DikozemieHeroProfile.GetCreationFocusPreview(skillId);
                    skills.Add(new DikozemieCreationSkillPreviewVM(
                        skillId,
                        focus,
                        focus * 10,
                        selected?.Skills.Any(skill => string.Equals(
                            skill,
                            skillId,
                            StringComparison.OrdinalIgnoreCase)) == true));
                }
                CreationAttributeGroups.Add(
                    new DikozemieCreationAttributePreviewVM(
                        attributeId,
                        CreationAttributeNames[attributeIndex],
                        attributeId == "naval"
                            ? 0
                            : DikozemieHeroProfile.GetCreationAttributePreview(attributeId),
                        selected != null && string.Equals(
                            selected.Attribute,
                            attributeId,
                            StringComparison.OrdinalIgnoreCase),
                        skills));
            }

            CreationAttributesText =
                "СИЛА                         " +
                DikozemieHeroProfile.GetCreationAttributePreview("vigor") +
                "\nКОНТРОЛЬ                 " +
                DikozemieHeroProfile.GetCreationAttributePreview("control") +
                "\nВЫНОСЛИВОСТЬ       " +
                DikozemieHeroProfile.GetCreationAttributePreview("endurance") +
                "\nХИТРОСТЬ                  " +
                DikozemieHeroProfile.GetCreationAttributePreview("cunning") +
                "\nОБЩЕНИЕ                    " +
                DikozemieHeroProfile.GetCreationAttributePreview("social") +
                "\nИНТЕЛЛЕКТ               " +
                DikozemieHeroProfile.GetCreationAttributePreview("intelligence");

            CreationSkillsText =
                "Одноручное  " + DikozemieHeroProfile.GetCreationFocusPreview("OneHanded") +
                "     Двуручное  " + DikozemieHeroProfile.GetCreationFocusPreview("TwoHanded") +
                "\nЛук  " + DikozemieHeroProfile.GetCreationFocusPreview("Bow") +
                "     Верховая езда  " + DikozemieHeroProfile.GetCreationFocusPreview("Riding") +
                "\nАтлетика  " + DikozemieHeroProfile.GetCreationFocusPreview("Athletics") +
                "     Тактика  " + DikozemieHeroProfile.GetCreationFocusPreview("Tactics");

            string[] stages =
            {
                "СЕМЬЯ  •  33 ГОДА  •  " + GetCultureName(DikozemieHeroProfile.CultureId).ToUpperInvariant(),
                "РАННЕЕ ДЕТСТВО  •  7 ЛЕТ",
                "ОТРОЧЕСТВО  •  12 ЛЕТ",
                "ЮНОСТЬ  •  17 ЛЕТ",
                "МОЛОДОСТЬ  •  20 ЛЕТ",
                "НАЧАЛЬНЫЙ ВОЗРАСТ  •  20 / 30 / 40 / 50 ЛЕТ"
            };
            // The review page names the age chosen, not the four on offer.
            LifeStageText = _creationStep >= stages.Length
                ? "НАЧАЛЬНЫЙ ВОЗРАСТ  •  " +
                    Math.Max(20, DikozemieHeroProfile.StartingAge) + " ЛЕТ"
                : stages[Math.Max(0, _creationStep)];
            CreationStageTitle = DikozemieHeroProfile.GetCreationStageTitle(_creationStep);
            OnPropertyChanged(nameof(IsFamilyCreationStage));
            OnPropertyChanged(nameof(IsSoloCreationStage));
        }

        private void RefreshHeroVisual()
        {
            BasicCharacterObject fallback =
                MBObjectManager.Instance.GetObject<BasicCharacterObject>(
                    "mp_character") ??
                MBObjectManager.Instance.GetObject<BasicCharacterObject>(
                    "mp_heavy_infantry_empire_troop");
            BasicCharacterObject character =
                DikozemieHeroProfile.IsCreated
                    ? DikozemieHeroProfile.ResolveCharacter(fallback) ?? fallback
                    : DikozemieHeroProfile.ResolveCreationChoicePreviewCharacter(
                        _creationStep,
                        _selectedChoice,
                        fallback) ?? fallback;
            if (character == null)
            {
                return;
            }

            DikozemieHeroProfileState profile = DikozemieHeroProfile.Snapshot;
            DikozemieHeroBackgroundChoice previewChoice = IsCreationMode
                ? DikozemieHeroProfile.GetCreationChoicePreview(
                    _creationStep,
                    _selectedChoice)
                : null;
            HeroVisual.FillFrom(character, -1, string.Empty);
            Equipment fallbackEquipment = character.FirstBattleEquipment ?? character.Equipment;
            Equipment equipment = DikozemieHeroProfile.IsCreated
                ? DikozemieHeroProfile.CreateDisplayEquipment(character)
                : DikozemieHeroProfile.CreateCreationPreviewEquipment(
                    _creationStep,
                    _selectedChoice,
                    profile.IsFemale,
                    fallbackEquipment);
            if (equipment != null)
            {
                HeroVisual.SetEquipment(equipment);
            }
            ApplyPreviewProps(
                HeroVisual,
                previewChoice?.LeftPropId,
                previewChoice?.RightPropId);

            BodyProperties body = character.GetBodyProperties(equipment);
            if (BodyProperties.FromString(
                    profile.BodyProperties,
                    out BodyProperties savedBody))
            {
                body = savedBody;
            }

            // Age progression belongs only to the unfinished creation story.
            // A completed profile already contains the exact adult body chosen
            // in FaceGen; applying creation step 0 here turned every saved hero
            // back into a five-year-old whenever the Hero tab was reopened.
            if (IsCreationMode)
            {
                body = MBBodyProperties.GetBodyPropertiesWithAge(
                    ref body,
                    DikozemieNativeCreationCatalog.GetPreviewAge(
                        _creationStep,
                        previewChoice));
            }
            HeroVisual.BodyProperties = body.ToString();
            HeroVisual.IsFemale = profile.IsFemale;
            HeroVisual.Race = Math.Max(0, profile.Race);
            HeroVisual.IsTableauEnabled = true;

            HeroVisual.IdleAction = "act_inventory_idle";
            if (IsCreationMode)
            {
                string animation = previewChoice?.AnimationId;
                if (string.IsNullOrWhiteSpace(animation))
                {
                    animation = "act_character_creation_" +
                        (profile.IsFemale ? "female" : "male") +
                        "_default_standing";
                }
                HeroVisual.ExecuteStartCustomAnimation(animation, false, 0f);
            }
            else
            {
                // Creation poses contain a hand-forward gesture.  Looping that
                // pose on the finished hero made the arm repeat forever.
                HeroVisual.ExecuteStopCustomAnimation();
            }

            if (IsFamilyCreationStage)
            {
                DikozemieHeroBackgroundChoice parentChoice =
                    DikozemieHeroProfile.GetCreationChoicePreview(0, _selectedChoice);
                BodyProperties motherBody = body;
                BodyProperties fatherBody = body;
                TaleWorlds.Core.FaceGen.GenerateParentKey(
                    body,
                    Math.Max(0, profile.Race),
                    ref motherBody,
                    ref fatherBody);
                motherBody = new BodyProperties(
                    new DynamicBodyProperties(33f, 0.3f, 0.2f),
                    motherBody.StaticProperties);
                fatherBody = new BodyProperties(
                    new DynamicBodyProperties(33f, 0.5f, 0.5f),
                    fatherBody.StaticProperties);
                RefreshFamilyVisual(
                    FamilyVisualLeft,
                    character,
                    DikozemieHeroProfile.CreateCreationPreviewEquipment(
                        0, _selectedChoice, false, fallbackEquipment),
                    fatherBody,
                    false,
                    parentChoice?.FatherAnimationId,
                    parentChoice?.FatherLeftPropId,
                    parentChoice?.FatherRightPropId);
                RefreshFamilyVisual(
                    FamilyVisualRight,
                    character,
                    DikozemieHeroProfile.CreateCreationPreviewEquipment(
                        0, _selectedChoice, true, fallbackEquipment),
                    motherBody,
                    true,
                    parentChoice?.MotherAnimationId,
                    parentChoice?.MotherLeftPropId,
                    parentChoice?.MotherRightPropId);
            }
            else
            {
                FamilyVisualLeft.ExecuteStopCustomAnimation();
                FamilyVisualRight.ExecuteStopCustomAnimation();
            }
            DikozemieHeroMenuController.TraceLifecycle(
                "visual creation=" + IsCreationMode +
                " age=" + body.Age.ToString("0.00") +
                " character=" + character.StringId +
                " choice=" + _selectedChoice);
        }

        private static void RefreshFamilyVisual(
            CharacterViewModel visual,
            BasicCharacterObject character,
            Equipment equipment,
            BodyProperties body,
            bool isFemale,
            string animation,
            string leftPropId,
            string rightPropId)
        {
            visual.FillFrom(character, -1, string.Empty);
            if (equipment != null)
            {
                visual.SetEquipment(equipment);
            }
            ApplyPreviewProps(visual, leftPropId, rightPropId);
            visual.BodyProperties = body.ToString();
            visual.IsFemale = isFemale;
            visual.Race = Math.Max(0, DikozemieHeroProfile.Snapshot.Race);
            visual.IsTableauEnabled = true;
            if (string.IsNullOrWhiteSpace(animation))
            {
                animation = "act_character_creation_" +
                    (isFemale ? "female" : "male") + "_default_standing";
            }
            // The campaign's paired clips contain scene-space root transforms.
            // Playing them in the multiplayer inventory tableau either loops a
            // combat-looking transition or turns the actors sideways. Keep the
            // exact faces, clothes and props but use its stable neutral stance.
            visual.ExecuteStopCustomAnimation();
            visual.IdleAction = "act_inventory_idle";
        }

        private static void ApplyPreviewProps(
            CharacterViewModel visual,
            string leftPropId,
            string rightPropId)
        {
            visual.LeftHandWieldedEquipmentIndex = -1;
            visual.RightHandWieldedEquipmentIndex = -1;
            ItemObject left = string.IsNullOrWhiteSpace(leftPropId)
                ? null
                : MBObjectManager.Instance.GetObject<ItemObject>(leftPropId);
            if (left != null)
            {
                visual.SetEquipment(
                    EquipmentIndex.Weapon0,
                    new EquipmentElement(left));
                visual.LeftHandWieldedEquipmentIndex =
                    (int)EquipmentIndex.Weapon0;
            }
            ItemObject right = string.IsNullOrWhiteSpace(rightPropId)
                ? null
                : MBObjectManager.Instance.GetObject<ItemObject>(rightPropId);
            if (right != null)
            {
                visual.SetEquipment(
                    EquipmentIndex.Weapon1,
                    new EquipmentElement(right));
                visual.RightHandWieldedEquipmentIndex =
                    (int)EquipmentIndex.Weapon1;
            }
        }

        private static string BuildChoiceHint(DikozemieHeroBackgroundChoice choice)
        {
            if (choice.Skills.Count == 0)
            {
                return choice.Description + "\n+" + choice.UnspentFocusPoints +
                    " фокус  •  +" + choice.UnspentAttributePoints +
                    " характеристики";
            }
            return choice.Description + "\n+1 " +
                GetAttributeName(choice.Attribute) + "  •  +1 фокус: " +
                string.Join(", ", choice.Skills.Select(GetSkillName));
        }

        private static string GetAttributeName(string id)
        {
            switch (id)
            {
                case "vigor": return "Сила";
                case "control": return "Контроль";
                case "endurance": return "Выносливость";
                case "cunning": return "Хитрость";
                case "social": return "Общение";
                default: return "Интеллект";
            }
        }

        private static string GetSkillName(string id)
        {
            switch (id)
            {
                case "OneHanded": return "Одноручное";
                case "TwoHanded": return "Двуручное";
                case "Polearm": return "Древковое";
                case "Bow": return "Лук";
                case "Throwing": return "Метательное";
                case "Crossbow": return "Арбалет";
                case "Riding": return "Верховая езда";
                case "Athletics": return "Атлетика";
                case "Tactics": return "Тактика";
                case "Crafting": return "Кузнечное дело";
                case "Scouting": return "Разведка";
                case "Roguery": return "Плутовство";
                case "Charm": return "Обаяние";
                case "Leadership": return "Лидерство";
                case "Trade": return "Торговля";
                case "Steward": return "Управление";
                case "Medicine": return "Медицина";
                case "Engineering": return "Инженерное дело";
                case "Mariner": return "Мореход";
                case "Boatswain": return "Боцман";
                case "Shipmaster": return "Капитан корабля";
                default: return id;
            }
        }

        private void RefreshHeader()
        {
            DikozemieHeroProfileState hero = DikozemieHeroProfile.Snapshot;
            int pendingFocusPoints = _pendingFocusIncreases.Values.Sum();
            int pendingAttributePoints = _pendingAttributeIncreases.Values.Sum();
            int pendingPerkChoices = _pendingPerkChoices.Count;
            int remainingFocusPoints = Math.Max(
                0,
                hero.FocusPoints - pendingFocusPoints);
            int remainingAttributePoints = Math.Max(
                0,
                hero.AttributePoints - pendingAttributePoints);
            int requiredHeroExperience = Math.Max(1,
                DikozemieHeroProfile.ExperienceForNextLevel(hero.Level));
            int remainingHeroExperience = Math.Max(
                0,
                requiredHeroExperience - Math.Max(0, hero.Experience));
            int nextLevel = Math.Min(
                DikozemieHeroProfile.MaximumHeroLevel,
                hero.Level + 1);
            int nextAttributeLevel = Math.Min(
                DikozemieHeroProfile.MaximumHeroLevel,
                ((hero.Level / DikozemieHeroProfile.LevelsPerAttributePoint) + 1) *
                    DikozemieHeroProfile.LevelsPerAttributePoint);
            HeroTitle = hero.Name + "  •  Уникальный герой";
            ProgressText = "Уровень " + hero.Level + "  •  Опыт " + hero.Experience +
                "/" + requiredHeroExperience +
                "  •  Фокус: " + remainingFocusPoints +
                "  •  Характеристики: " + remainingAttributePoints;
            HeroLevelHudText = "УРОВЕНЬ  " + hero.Level;
            HeroExperienceHudText = "ОПЫТ  " + hero.Experience +
                " / " + requiredHeroExperience;
            HeroCurrencyHudText = DikozemieArmoryProgression.HeroResources.ToString();
            HeroVitalityHudText = "ЗДОРОВЬЕ  100%";
            NextDevelopmentRewardText = hero.Level >=
                DikozemieHeroProfile.MaximumHeroLevel
                ? "Достигнут максимальный уровень героя"
                : "До уровня " + nextLevel + ": " + remainingHeroExperience +
                    " опыта  •  Награда: +" +
                    DikozemieHeroProfile.FocusPointsPerLevel + " фокус" +
                    (nextLevel % DikozemieHeroProfile.LevelsPerAttributePoint == 0
                        ? " и +1 характеристика"
                        : "  •  Следующая характеристика: уровень " +
                            nextAttributeLevel);
            AvailablePointsText = "Характеристики: " + remainingAttributePoints +
                "  •  Фокус: " + remainingFocusPoints +
                (pendingFocusPoints + pendingAttributePoints + pendingPerkChoices > 0
                    ? "  •  НЕ ПРИМЕНЕНО: " +
                        (pendingFocusPoints + pendingAttributePoints) +
                        " очк., " + pendingPerkChoices + " перк."
                    : string.Empty);
            FreeAttributePointsText = remainingAttributePoints.ToString();
            FreeFocusPointsText = remainingFocusPoints.ToString();
            CanSpendAttributePoints = remainingAttributePoints > 0;
            CanApplyDevelopmentChanges =
                pendingFocusPoints + pendingAttributePoints + pendingPerkChoices > 0;
            CurrencyText = "Рейтинг: " + DikozemieArmoryProgression.PlayerRating +
                "  •  Монеты: " + DikozemieArmoryProgression.HeroResources;
            AttributesText = "Сила " + GetPreviewAttribute("vigor") +
                "  •  Контроль " + GetPreviewAttribute("control") +
                "  •  Выносливость " + GetPreviewAttribute("endurance") +
                "  •  Хитрость " + GetPreviewAttribute("cunning") +
                "  •  Общение " + GetPreviewAttribute("social") +
                "  •  Интеллект " + GetPreviewAttribute("intelligence");
            HeroExperienceBarWidth = Math.Min(540f,
                540f * Math.Max(0, hero.Experience) / requiredHeroExperience);
            VigorValue = GetPreviewAttribute("vigor").ToString();
            ControlValue = GetPreviewAttribute("control").ToString();
            EnduranceValue = GetPreviewAttribute("endurance").ToString();
            CunningValue = GetPreviewAttribute("cunning").ToString();
            SocialValue = GetPreviewAttribute("social").ToString();
            IntelligenceValue = GetPreviewAttribute("intelligence").ToString();
        }

        private void RefreshSection()
        {
            OnPropertyChanged(nameof(IsOverview));
            OnPropertyChanged(nameof(IsInventory));
            OnPropertyChanged(nameof(IsSkills));
            OnPropertyChanged(nameof(IsQuests));
            switch (_section)
            {
                case 1:
                    ContentTitle = "ОРУЖЕЙНАЯ ЛАВКА И СНАРЯЖЕНИЕ";
                    ContentText = DikozemieHeroProfile.BuildInventoryText() +
                        "\n\nОружие из одиночной игры продаётся здесь по его базовой стоимости. " +
                        "Купленные предметы навсегда остаются у героя и передаются в следующий бой.";
                    RefreshInventory();
                    break;
                case 2:
                    ContentTitle = "НАВЫКИ";
                    ContentText = DikozemieHeroProfile.BuildSkillsText() +
                        "\n\nУрон развивает использованное оружие; пеший бой — атлетику, " +
                        "бой верхом — верховую езду. Новый уровень даёт очко фокуса, " +
                        "а каждый четвёртый — очко характеристики. Навыки действуют в PvP.";
                    RefreshSkills();
                    break;
                case 3:
                    ContentTitle = "КВЕСТЫ";
                    ContentText = "Путь гладиатора\nПобедите 10 разных бойцов арены.\n\n" +
                        "Выше головы\nОдолейте соперника с рейтингом на 10 000 выше вашего.\n\n" +
                        "Первый венец\nВыиграйте турнир.\n\nНаграды и отслеживание квестов подключаются к этому постоянному профилю.";
                    break;
                default:
                    ContentTitle = "ПРОФИЛЬ ГЕРОЯ";
                    ContentText = "Это ваш уникальный герой: предыстория сформировала его характеристики " +
                        "и личный стартовый комплект.\n\n" +
                        "Сохранённые внешность, оружие, экипировка, уровень и навыки передаются в тренировочные бои, " +
                        "турнир и сетевую арену. Готовый тип войск не заменяет этого персонажа; подбор соперника " +
                        "остаётся в диапазоне ±10 000 рейтинга.";
                    break;
            }
        }

        private void AddFocus(string skillId, string displayName)
        {
            bool staged = StageFocusIncrease(skillId);
            DikozemieHeroMenuController.TraceLifecycle(
                "focus staged target=" + skillId +
                " success=" + staged +
                " selected=" + _selectedSkillId);
            StatusText = staged
                ? displayName + ": очко подготовлено. Нажмите «Готово», " +
                    "чтобы применить."
                : "Нет свободных очков фокуса или навык уже достиг предела.";
            RefreshHeader();
            RefreshSkills();
        }

        private void AddAttribute(string attributeId, string displayName)
        {
            StatusText = StageAttributeIncrease(attributeId)
                ? displayName + ": очко подготовлено. Нажмите «Готово», " +
                    "чтобы применить."
                : "Нет свободных очков характеристик или достигнут предел.";
            RefreshHeader();
            RefreshSkills();
        }

        private bool StageFocusIncrease(string skillId)
        {
            DikozemieHeroProfileState hero = DikozemieHeroProfile.Snapshot;
            int remainingPoints = Math.Max(
                0,
                hero.FocusPoints - _pendingFocusIncreases.Values.Sum());
            int currentIncrease = GetPendingIncrease(
                _pendingFocusIncreases,
                skillId);
            int previewFocus = DikozemieHeroProfile.GetFocus(skillId) +
                currentIncrease;
            if (remainingPoints <= 0 ||
                previewFocus >= DikozemieHeroProfile.MaximumFocusPerSkill)
            {
                return false;
            }
            _pendingFocusIncreases[skillId] = currentIncrease + 1;
            return true;
        }

        private bool StageAttributeIncrease(string attributeId)
        {
            DikozemieHeroProfileState hero = DikozemieHeroProfile.Snapshot;
            int remainingPoints = Math.Max(
                0,
                hero.AttributePoints - _pendingAttributeIncreases.Values.Sum());
            int currentIncrease = GetPendingIncrease(
                _pendingAttributeIncreases,
                attributeId);
            int previewAttribute = DikozemieHeroProfile.GetAttribute(attributeId) +
                currentIncrease;
            if (remainingPoints <= 0 ||
                previewAttribute >= DikozemieHeroProfile.MaximumAttribute)
            {
                return false;
            }
            _pendingAttributeIncreases[attributeId] = currentIncrease + 1;
            return true;
        }

        private void ClearPendingDevelopmentChanges()
        {
            _pendingFocusIncreases.Clear();
            _pendingAttributeIncreases.Clear();
            _pendingPerkChoices.Clear();
        }

        private static int GetPendingIncrease(
            IReadOnlyDictionary<string, int> increases,
            string id)
        {
            return id != null && increases.TryGetValue(id, out int increase)
                ? Math.Max(0, increase)
                : 0;
        }

        private int GetPreviewAttribute(string attributeId)
        {
            return DikozemieHeroProfile.GetAttribute(attributeId) +
                GetPendingIncrease(_pendingAttributeIncreases, attributeId);
        }

        private int GetRemainingFocusPoints()
        {
            return Math.Max(
                0,
                DikozemieHeroProfile.Snapshot.FocusPoints -
                    _pendingFocusIncreases.Values.Sum());
        }

        private void RefreshSkills()
        {
            foreach (DikozemieHeroSkillItemVM item in SkillItems)
            {
                item.OnFinalize();
            }
            SkillItems.Clear();
            string[][] definitions =
            {
                new[] { "OneHanded", "Одноручное", "СИЛА" },
                new[] { "TwoHanded", "Двуручное", "СИЛА" },
                new[] { "Polearm", "Древковое", "СИЛА" },
                new[] { "Bow", "Лук", "КОНТРОЛЬ" },
                new[] { "Crossbow", "Арбалет", "КОНТРОЛЬ" },
                new[] { "Throwing", "Метательное", "КОНТРОЛЬ" },
                new[] { "Riding", "Верховая езда", "ВЫНОСЛИВОСТЬ" },
                new[] { "Athletics", "Атлетика", "ВЫНОСЛИВОСТЬ" },
                // Bannerlord's native StringId is "Crafting".  Using the
                // display-name-derived "Smithing" leaves SkillIconVisualWidget
                // without the native icon and disconnects the tile from the
                // single-player skill definition.
                new[] { "Crafting", "Кузнечное дело", "ВЫНОСЛИВОСТЬ" },
                new[] { "Scouting", "Разведка", "ХИТРОСТЬ" },
                new[] { "Tactics", "Тактика", "ХИТРОСТЬ" },
                new[] { "Roguery", "Плутовство", "ХИТРОСТЬ" },
                new[] { "Charm", "Обаяние", "ОБЩЕНИЕ" },
                new[] { "Leadership", "Лидерство", "ОБЩЕНИЕ" },
                new[] { "Trade", "Торговля", "ОБЩЕНИЕ" },
                new[] { "Steward", "Управление", "ИНТЕЛЛЕКТ" },
                new[] { "Medicine", "Медицина", "ИНТЕЛЛЕКТ" },
                new[] { "Engineering", "Инженерное дело", "ИНТЕЛЛЕКТ" }
            };
            int remainingFocusPoints = GetRemainingFocusPoints();
            foreach (string[] definition in definitions)
            {
                int previewFocus = DikozemieHeroProfile.GetFocus(definition[0]) +
                    GetPendingIncrease(
                        _pendingFocusIncreases,
                        definition[0]);
                SkillItems.Add(new DikozemieHeroSkillItemVM(
                    definition[0],
                    definition[1],
                    definition[2],
                    GetSkillDescription(definition[0]),
                    DikozemieHeroProfile.GetEffectiveSkill(definition[0], 0),
                    previewFocus,
                    remainingFocusPoints > 0 &&
                        previewFocus <
                            DikozemieHeroProfile.MaximumFocusPerSkill,
                    SelectSkill,
                    AddFocusFromItem));
            }
            SelectSkill(SkillItems.FirstOrDefault(item =>
                string.Equals(item.SkillId, _selectedSkillId, StringComparison.Ordinal)) ??
                SkillItems.FirstOrDefault());
        }

        private void SelectSkill(DikozemieHeroSkillItemVM selected)
        {
            if (selected == null)
            {
                return;
            }
            _selectedSkillId = selected.SkillId;
            OnPropertyChanged(nameof(SelectedSkillId));
            foreach (DikozemieHeroSkillItemVM item in SkillItems)
            {
                item.IsSelected = ReferenceEquals(item, selected);
            }
            SelectedSkillName = selected.Name;
            SelectedSkillHeading = selected.Name + "  •  " +
                GetAttributeAbbreviation(selected.AttributeName);
            SelectedSkillDescription = selected.Description;
            SelectedSkillAttribute = "ХАРАКТЕРИСТИКА: " + selected.AttributeName;
            SelectedSkillHowToLearn = "КАК РАЗВИВАТЬ\n" + GetSkillTrainingText(selected.SkillId);
            SelectedSkillEffects = "ЭФФЕКТЫ НАВЫКА\n" + GetSkillEffectsText(selected.SkillId, selected.Level);
            SelectedSkillLevelText = "УРОВЕНЬ НАВЫКА  " + selected.Level;
            SelectedSkillLevelNumberText = selected.Level.ToString();
            SelectedSkillLevel = selected.Level;
            SelectedSkillFocusText = "ФОКУС  " + selected.Focus + "/5";
            SelectedSkillFocusLevel = selected.Focus;
            CanInvestSelectedSkill = GetRemainingFocusPoints() > 0 &&
                selected.Focus < DikozemieHeroProfile.MaximumFocusPerSkill;
            int currentXp = DikozemieHeroProfile.GetSkillExperience(selected.SkillId);
            int requiredXp = DikozemieHeroProfile.SkillExperienceForNextLevel(selected.Level);
            SelectedSkillExperienceText = currentXp + " / " + requiredXp + " опыта";
            string attributeId = GetSkillAttributeId(selected.SkillId);
            int previewAttribute = GetPreviewAttribute(attributeId);
            float learningRate = DikozemieArenaCombatProgression.GetLearningRate(
                previewAttribute,
                selected.Focus,
                selected.Level);
            int learningLimit = Math.Max(
                0,
                (previewAttribute - 1) * 10 + selected.Focus * 30);
            SelectedSkillLearningRateText = "СКОРОСТЬ ОБУЧЕНИЯ x" +
                learningRate.ToString("0.00");
            SelectedSkillLearningLimitText = string.Empty;
            SelectedSkillLearningRate = learningRate;
            SelectedSkillLearningLimit = learningLimit;
            SelectedSkillBarWidth = requiredXp <= 0
                ? 0f
                : Math.Min(290f, 290f * currentXp / requiredXp);
            RefreshPerks(selected);
        }

        private void AddFocusFromItem(DikozemieHeroSkillItemVM item)
        {
            if (item != null)
            {
                AddFocus(item.SkillId, item.Name);
            }
        }

        private void RefreshPerks(DikozemieHeroSkillItemVM selected)
        {
            foreach (DikozemieHeroPerkTierVM tier in PerkTiers)
            {
                tier.OnFinalize();
            }
            PerkTiers.Clear();
            PerkItems.Clear();
            IReadOnlyList<DikozemieNativePerkTier> nativeTiers =
                DikozemieNativePerkCatalog.GetSkill(selected.SkillId);
            if (nativeTiers.Count == 0)
            {
                SelectedPerkDescription = "Для этого навыка в текущей версии игры нет перков.";
                _restingPerkDescription = SelectedPerkDescription;
                return;
            }

            SelectedPerkDescription =
                "Достигните порога и выберите один из двух перков. " +
                "Выбор вступит в силу после кнопки «Готово».";
            bool previousTierSelected = true;
            foreach (DikozemieNativePerkTier nativeTier in nativeTiers)
            {
                string key = BuildPerkChoiceKey(
                    selected.SkillId,
                    nativeTier.RequiredLevel);
                string committedPerk = DikozemieHeroProfile.GetSelectedPerkId(
                    selected.SkillId,
                    nativeTier.RequiredLevel);
                string selectedPerk = _pendingPerkChoices.TryGetValue(
                    key,
                    out string pendingPerk)
                    ? pendingPerk
                    : committedPerk;
                bool isUnlocked = selected.Level >= nativeTier.RequiredLevel;
                bool topSelected = string.Equals(
                    selectedPerk,
                    nativeTier.Top?.StringId,
                    StringComparison.Ordinal);
                bool bottomSelected = string.Equals(
                    selectedPerk,
                    nativeTier.Bottom?.StringId,
                    StringComparison.Ordinal);
                int topState = GetPerkState(
                    isUnlocked,
                    topSelected,
                    bottomSelected,
                    previousTierSelected);
                int bottomState = GetPerkState(
                    isUnlocked,
                    bottomSelected,
                    topSelected,
                    previousTierSelected);
                DikozemieHeroPerkNodeVM top = CreatePerkNode(
                    nativeTier.Top,
                    selected.SkillId,
                    nativeTier.RequiredLevel,
                    nativeTier.Bottom == null ? 0 : 1,
                    topState,
                    string.Equals(
                        committedPerk,
                        nativeTier.Top?.StringId,
                        StringComparison.Ordinal));
                DikozemieHeroPerkNodeVM bottom = CreatePerkNode(
                    nativeTier.Bottom,
                    selected.SkillId,
                    nativeTier.RequiredLevel,
                    2,
                    bottomState,
                    string.Equals(
                        committedPerk,
                        nativeTier.Bottom?.StringId,
                        StringComparison.Ordinal));
                if (top != null)
                {
                    PerkItems.Add(top);
                }
                if (bottom != null)
                {
                    PerkItems.Add(bottom);
                }
                DikozemieHeroPerkNodeVM active = top?.IsSelected == true
                    ? top
                    : bottom?.IsSelected == true ? bottom : null;
                if (active != null)
                {
                    SelectedPerkDescription = active.Name + "\n" +
                        active.Description +
                        (string.IsNullOrWhiteSpace(committedPerk)
                            ? "\nНЕ ПРИМЕНЕНО — нажмите «Готово»."
                            : "\nВЫБРАНО");
                }
                previousTierSelected = topSelected || bottomSelected;
            }
            _restingPerkDescription = SelectedPerkDescription;
        }

        private static int GetPerkState(
            bool isUnlocked,
            bool isSelected,
            bool isAlternativeSelected,
            bool previousTierSelected)
        {
            if (!isUnlocked)
            {
                return (int)NativePerkState.NotEarned;
            }
            if (isSelected)
            {
                return (int)NativePerkState.EarnedAndActive;
            }
            if (isAlternativeSelected)
            {
                return (int)NativePerkState.EarnedAndNotActive;
            }
            return previousTierSelected
                ? (int)NativePerkState.EarnedButNotSelected
                : (int)NativePerkState.EarnedPreviousPerkNotSelected;
        }

        private DikozemieHeroPerkNodeVM CreatePerkNode(
            DikozemieNativePerkDefinition perk,
            string skillId,
            int requiredLevel,
            int alternativeType,
            int perkState,
            bool isCommitted)
        {
            if (perk == null)
            {
                return null;
            }
            return new DikozemieHeroPerkNodeVM(
                skillId,
                requiredLevel,
                perk.DisplayName,
                perk.StringId,
                perk.SpriteId,
                requiredLevel.ToString(),
                perk.BuildDescription(),
                alternativeType,
                perkState,
                isCommitted,
                SelectPerk,
                PreviewPerk,
                RestorePerkPreview);
        }

        private void PreviewPerk(DikozemieHeroPerkNodeVM perk)
        {
            if (perk == null)
            {
                return;
            }
            int currentLevel = DikozemieHeroProfile.GetEffectiveSkill(
                perk.SkillId,
                0);
            string state = perk.IsUnlocked
                ? perk.StateText
                : "ЗАКРЫТО — требуется навык " + perk.RequiredLevel +
                  ", сейчас " + currentLevel + ". До открытия осталось " +
                  Math.Max(0, perk.RequiredLevel - currentLevel) + ".";
            SelectedPerkDescription = perk.Name + "  •  УРОВЕНЬ " +
                perk.RequiredLevel + "\n" + perk.Description + "\n" +
                state;
        }

        private void RestorePerkPreview()
        {
            SelectedPerkDescription = _restingPerkDescription;
        }

        private void SelectPerk(DikozemieHeroPerkNodeVM perk)
        {
            if (perk == null || !perk.CanSelect)
            {
                return;
            }
            _pendingPerkChoices[BuildPerkChoiceKey(
                perk.SkillId,
                perk.RequiredLevel)] = perk.NativePerkId;
            StatusText = perk.Name + " подготовлен. Нажмите «Готово», " +
                "чтобы подтвердить выбор перка.";
            RefreshHeader();
            RefreshSkills();
        }

        private static string BuildPerkChoiceKey(string skillId, int requiredLevel)
        {
            return skillId + ":" + requiredLevel;
        }

        private static string GetAttributeAbbreviation(string attributeName)
        {
            switch (attributeName)
            {
                case "СИЛА": return "СИЛ";
                case "КОНТРОЛЬ": return "КТР";
                case "ВЫНОСЛИВОСТЬ": return "ВЫН";
                case "ХИТРОСТЬ": return "ХТР";
                case "ОБЩЕНИЕ": return "ОБЩ";
                case "ИНТЕЛЛЕКТ": return "ИНТ";
                default: return attributeName ?? string.Empty;
            }
        }

        private static string GetSkillAttributeId(string skillId)
        {
            switch (skillId)
            {
                case "OneHanded":
                case "TwoHanded":
                case "Polearm":
                    return "vigor";
                case "Bow":
                case "Crossbow":
                case "Throwing":
                    return "control";
                case "Riding":
                case "Athletics":
                case "Crafting":
                    return "endurance";
                case "Scouting":
                case "Tactics":
                case "Roguery":
                    return "cunning";
                case "Charm":
                case "Leadership":
                case "Trade":
                    return "social";
                case "Steward":
                case "Medicine":
                case "Engineering":
                    return "intelligence";
                default:
                    return "cunning";
            }
        }

        private void SetStoreCategory(DikozemieStoreCategory category)
        {
            if (_storeCategory == category)
            {
                return;
            }
            _storeCategory = category;
            OnPropertyChanged(nameof(IsStoreAll));
            OnPropertyChanged(nameof(IsStoreWeapons));
            OnPropertyChanged(nameof(IsStoreShields));
            OnPropertyChanged(nameof(IsStoreArmor));
            OnPropertyChanged(nameof(IsStoreMounts));
            OnPropertyChanged(nameof(IsStoreGoods));
            RefreshInventory();
        }

        private void RefreshInventory()
        {
            ItemTooltip.Hide();
            FinalizeStoreItems(StoreItems);
            FinalizeItems(InventoryItems);
            FinalizeItems(EquippedItems);
            FinalizeEquipmentSlots(ArmorEquipmentSlots);
            FinalizeEquipmentSlots(WeaponEquipmentSlots);
            FinalizeEquipmentSlots(BannerEquipmentSlots);
            StoreItems.Clear();
            InventoryItems.Clear();
            EquippedItems.Clear();
            ArmorEquipmentSlots.Clear();
            WeaponEquipmentSlots.Clear();
            BannerEquipmentSlots.Clear();
            DikozemieHeroProfileState snapshot = DikozemieHeroProfile.Snapshot;
            if (_itemStoreCatalog.Count == 0)
            {
                _itemStoreCatalog.AddRange(DikozemieItemStore.GetCatalog());
            }
            string search = (StoreSearchText ?? string.Empty).Trim();
            IEnumerable<ItemObject> filteredItems = _itemStoreCatalog
                .Where(item => item != null &&
                    DikozemieTown.IsSoldHere(item, DikozemieItemStore.GetBasePrice(item)) &&
                    (_storeCategory == DikozemieStoreCategory.All ||
                     DikozemieItemStore.GetCategory(item) == _storeCategory) &&
                    (string.IsNullOrWhiteSpace(search) ||
                     (item.Name?.ToString() ?? string.Empty).IndexOf(
                         search,
                         StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                     item.StringId.IndexOf(
                         search,
                         StringComparison.OrdinalIgnoreCase) >= 0));
            List<ItemObject> availableItems = (_storePriceAscending
                    ? filteredItems
                        .OrderBy(DikozemieItemStore.GetBuyPrice)
                        .ThenBy(item => item.Name?.ToString() ?? item.StringId,
                            StringComparer.CurrentCultureIgnoreCase)
                    : filteredItems
                        .OrderByDescending(DikozemieItemStore.GetBuyPrice)
                        .ThenBy(item => item.Name?.ToString() ?? item.StringId,
                            StringComparer.CurrentCultureIgnoreCase))
                .ToList();
            int currentGold = DikozemieArmoryProgression.HeroResources;
            foreach (ItemObject item in availableItems)
            {
                StoreItems.Add(new DikozemieStoreItemVM(
                    item,
                    currentGold >= DikozemieItemStore.GetBuyPrice(item),
                    BuyItem,
                    HoverStoreItem,
                    HideItemTooltip));
            }
            DikozemieMarketKind market = DikozemieTown.MarketKind;
            DikozemieLobbyMapNode here = DikozemieTown.Here;
            StoreTitle = market == DikozemieMarketKind.Armory
                ? "ОРУЖЕЙНАЯ"
                : market == DikozemieMarketKind.Village
                    ? "СЕЛЬСКИЙ РЫНОК"
                    : market == DikozemieMarketKind.Peddler ? "ТОРГОВЕЦ" : "РЫНОК";
            StorePageText = market == DikozemieMarketKind.None
                ? "ЗДЕСЬ НЕ ТОРГУЮТ"
                : market == DikozemieMarketKind.Peddler
                    ? "НА ДОРОГЕ  •  ТОВАРОВ: " + availableItems.Count + "  •  НА ЧЕТВЕРТЬ ДОРОЖЕ ГОРОДА"
                    : DikozemieTown.DisplayName(here).ToUpperInvariant() +
                        "  •  ТОВАРОВ: " + availableItems.Count +
                        (market == DikozemieMarketKind.Town
                            ? "  •  МЕСТНОЕ -10%, ПРИВОЗНОЕ +10%"
                            : string.Empty);
            StoreEmptyText = market == DikozemieMarketKind.None
                ? DikozemieTown.MarketHint
                : availableItems.Count == 0 && !string.IsNullOrWhiteSpace(search)
                    ? "Ничего не нашлось."
                    : string.Empty;

            foreach (DikozemieHeroItem saved in snapshot.Equipment.OrderBy(saved => saved.Slot))
            {
                ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(saved.ItemId);
                if (item != null)
                {
                    EquippedItems.Add(new DikozemieHeroInventoryItemVM(
                        item,
                        GetEquipmentSlotName(saved.Slot),
                        true,
                        snapshot.OwnedItems.Count(id => string.Equals(
                            id,
                            saved.ItemId,
                            StringComparison.Ordinal)),
                        SellInventoryItem,
                        HoverInventoryItem,
                        HideItemTooltip));
                }
            }
            foreach (IGrouping<string, string> owned in snapshot.OwnedItems
                         .Where(id => !string.IsNullOrWhiteSpace(id))
                         .GroupBy(id => id, StringComparer.Ordinal)
                         .OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                string itemId = owned.Key;
                ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
                int equippedCount = snapshot.Equipment.Count(saved =>
                    saved != null && string.Equals(
                        saved.ItemId,
                        itemId,
                        StringComparison.Ordinal));
                int inventoryCount = Math.Max(0, owned.Count() - equippedCount);
                // The controls on the left belong to the merchant only.
                // Hiding owned items behind the merchant's active filter made
                // a displaced weapon look as if it had been deleted.
                if (item != null && inventoryCount > 0)
                {
                    InventoryItems.Add(new DikozemieHeroInventoryItemVM(
                        item,
                        DikozemieItemStore.GetTypeName(item),
                        false,
                        inventoryCount,
                        SellInventoryItem,
                        HoverInventoryItem,
                        HideItemTooltip));
                }
                else if (item == null &&
                    string.Equals(itemId, "grain", StringComparison.Ordinal) &&
                    inventoryCount > 0)
                {
                    InventoryItems.Add(
                        DikozemieHeroInventoryItemVM.CreateGrain(
                            inventoryCount,
                            SellInventoryItem,
                            HoverInventoryItem,
                            HideItemTooltip));
                }
            }
            int[] armorSlots = { 5, 9, 6, 8, 7, 10, 11 };
            int[] weaponSlots = { 0, 1, 2, 3 };
            foreach (int slot in armorSlots)
            {
                ArmorEquipmentSlots.Add(CreateEquipmentSlot(snapshot, slot));
            }
            foreach (int slot in weaponSlots)
            {
                WeaponEquipmentSlots.Add(CreateEquipmentSlot(snapshot, slot));
            }
            BannerEquipmentSlots.Add(CreateEquipmentSlot(snapshot, 4));
            DikozemieHeroMenuController.TraceLifecycle(
                "inventory loaded store=" + availableItems.Count +
                " category=" + _storeCategory +
                " rendered=" + StoreItems.Count +
                " sort=" + (_storePriceAscending ? "ascending" : "descending") +
                " visiblePrices=" +
                (StoreItems.Count > 0 ? StoreItems[0].Price.ToString() : "-") +
                ".." +
                (StoreItems.Count > 0
                    ? StoreItems[StoreItems.Count - 1].Price.ToString()
                    : "-") +
                " owned=" + InventoryItems.Count +
                " equipped=" + EquippedItems.Count);
        }

        private void BuyItem(DikozemieStoreItemVM item)
        {
            if (item == null)
            {
                return;
            }

            if (DikozemieItemStore.TryPurchase(
                    item.ItemId,
                    out int remainingGold,
                    out string message))
            {
                StatusText = message +
                    " Предмет добавлен в инвентарь; наденьте его перетаскиванием.";
                DikozemieHeroMenuController.TraceLifecycle(
                    "trade action=buy-to-inventory item=" + item.ItemId +
                    " remainingGold=" + remainingGold);
            }
            else
            {
                StatusText = message;
            }

            RefreshHeader();
            RefreshHeroVisual();
            RefreshInventory();
        }

        private void SellInventoryItem(DikozemieHeroInventoryItemVM item)
        {
            if (item == null)
            {
                return;
            }

            DikozemieItemStore.TrySell(
                item.ItemId,
                out int remainingGold,
                out string message);
            StatusText = message;
            RefreshHeader();
            RefreshHeroVisual();
            RefreshInventory();
        }

        private DikozemieHeroEquipmentSlotVM CreateEquipmentSlot(
            DikozemieHeroProfileState snapshot,
            int slot)
        {
            DikozemieHeroItem saved = snapshot.Equipment.FirstOrDefault(item =>
                item != null && item.Slot == slot);
            ItemObject item = saved == null
                ? null
                : MBObjectManager.Instance.GetObject<ItemObject>(saved.ItemId);
            return new DikozemieHeroEquipmentSlotVM(
                slot,
                GetEquipmentSlotName(slot),
                GetEquipmentSlotBrush(slot),
                item,
                UnequipEquipmentSlot,
                HoverEquipmentSlot,
                HideItemTooltip);
        }

        private void HoverStoreItem(DikozemieStoreItemVM item)
        {
            ShowItemTooltip(item?.ItemId, compareWithEquipment: true, -155f);
        }

        private void HoverInventoryItem(DikozemieHeroInventoryItemVM item)
        {
            ShowItemTooltip(item?.ItemId, compareWithEquipment: true, 155f);
        }

        private void HoverEquipmentSlot(DikozemieHeroEquipmentSlotVM slot)
        {
            ShowItemTooltip(slot?.ItemId, compareWithEquipment: false,
                slot != null && slot.Slot >= 5 && slot.Slot <= 11 ? -155f : 155f);
        }

        private void HideItemTooltip()
        {
            ItemTooltip.Hide();
        }

        private void ShowItemTooltip(
            string itemId,
            bool compareWithEquipment,
            float horizontalOffset)
        {
            try
            {
                ItemObject target = string.IsNullOrWhiteSpace(itemId)
                    ? null
                    : MBObjectManager.Instance.GetObject<ItemObject>(itemId);
                if (target == null)
                {
                    ItemTooltip.Hide();
                    return;
                }

                ItemObject compared = compareWithEquipment
                    ? FindEquippedComparison(target, DikozemieHeroProfile.Snapshot)
                    : null;
                ItemTooltip.Show(target, compared, horizontalOffset);
            }
            catch (Exception error)
            {
                // A malformed or campaign-only item property must never be
                // able to take the whole multiplayer client down on hover.
                ItemTooltip.Hide();
                DikozemieHeroMenuController.TraceLifecycle(
                    "item tooltip failed item=" + (itemId ?? string.Empty) +
                    " error=" + error);
            }
        }

        private static ItemObject FindEquippedComparison(
            ItemObject target,
            DikozemieHeroProfileState snapshot)
        {
            if (target == null || snapshot == null)
            {
                return null;
            }

            int fixedSlot = GetFixedEquipmentSlot(target);
            if (fixedSlot >= 0)
            {
                return ResolveEquippedItem(snapshot, fixedSlot);
            }

            string targetType = target.ItemType.ToString();
            WeaponComponentData targetWeapon = target.HasWeaponComponent
                ? target.PrimaryWeapon
                : null;
            foreach (DikozemieHeroItem equipped in snapshot.Equipment
                         .Where(saved => saved != null && saved.Slot >= 0 && saved.Slot <= 3)
                         .OrderBy(saved => saved.Slot))
            {
                ItemObject candidate = MBObjectManager.Instance.GetObject<ItemObject>(
                    equipped.ItemId);
                if (candidate == null)
                {
                    continue;
                }

                if (string.Equals(
                        targetType,
                        candidate.ItemType.ToString(),
                        StringComparison.Ordinal))
                {
                    return candidate;
                }

                WeaponComponentData candidateWeapon = candidate.HasWeaponComponent
                    ? candidate.PrimaryWeapon
                    : null;
                if (targetWeapon != null && candidateWeapon != null &&
                    targetWeapon.WeaponClass == candidateWeapon.WeaponClass)
                {
                    return candidate;
                }
            }
            return null;
        }

        private static ItemObject ResolveEquippedItem(
            DikozemieHeroProfileState snapshot,
            int slot)
        {
            DikozemieHeroItem saved = snapshot.Equipment.FirstOrDefault(item =>
                item != null && item.Slot == slot);
            return saved == null
                ? null
                : MBObjectManager.Instance.GetObject<ItemObject>(saved.ItemId);
        }

        private static int GetFixedEquipmentSlot(ItemObject item)
        {
            switch (item?.ItemType.ToString())
            {
                case "Banner": return 4;
                case "HeadArmor": return 5;
                case "BodyArmor": return 6;
                case "LegArmor": return 7;
                case "HandArmor": return 8;
                case "Cape": return 9;
                case "Horse": return 10;
                case "HorseHarness": return 11;
                default: return -1;
            }
        }

        private void UnequipEquipmentSlot(DikozemieHeroEquipmentSlotVM slot)
        {
            if (slot == null || !slot.HasItem)
            {
                return;
            }

            bool removed = DikozemieHeroProfile.TryUnequipItemAtSlot(slot.Slot);
            StatusText = removed
                ? slot.ItemName + ": перемещено в инвентарь."
                : "Не удалось снять предмет.";
            DikozemieHeroMenuController.TraceLifecycle(
                "equipment action=unequip slot=" + slot.Slot +
                " item=" + slot.ItemId +
                " success=" + removed);
            RefreshHeroVisual();
            RefreshInventory();
        }

        private static string GetEquipmentSlotBrush(int slot)
        {
            switch (slot)
            {
                case 4: return "InventoryBannerSlot";
                case 5: return "InventoryHelmetSlot";
                case 6: return "InventoryArmorSlot";
                case 7: return "InventoryBootSlot";
                case 8: return "InventoryGloveSlot";
                case 9: return "InventoryCloakSlot";
                case 10: return "InventoryMountSlot";
                case 11: return "InventoryMountArmorSlot";
                default: return "InventoryWeaponSlot";
            }
        }

        internal void BeginInventoryDragVisual(
            string itemId,
            float mouseX,
            float mouseY)
        {
            ItemTooltip.Hide();
            ItemObject item = string.IsNullOrWhiteSpace(itemId)
                ? null
                : MBObjectManager.Instance.GetObject<ItemObject>(itemId);
            if (item == null)
            {
                return;
            }

            ItemImageIdentifierVM previous = InventoryDragImageIdentifier;
            InventoryDragImageIdentifier = new ItemImageIdentifierVM(
                item,
                string.Empty);
            previous?.OnFinalize();
            UpdateInventoryDragVisualPosition(mouseX, mouseY);
            IsInventoryDragVisualVisible = true;
        }

        internal void UpdateInventoryDragVisualPosition(float mouseX, float mouseY)
        {
            InventoryDragVisualX = mouseX + 18f;
            InventoryDragVisualY = mouseY + 18f;
        }

        internal void EndInventoryDragVisual()
        {
            IsInventoryDragVisualVisible = false;
        }

        internal void ExecuteInventoryUnequipDrop(string itemId, int sourceSlot)
        {
            bool removed = sourceSlot >= 0 && sourceSlot < 12 &&
                DikozemieHeroProfile.TryUnequipItemAtSlot(sourceSlot);
            ItemObject item = string.IsNullOrWhiteSpace(itemId)
                ? null
                : MBObjectManager.Instance.GetObject<ItemObject>(itemId);
            StatusText = removed
                ? (item?.Name?.ToString() ?? itemId) +
                  ": возвращено в ваш инвентарь."
                : "Не удалось вернуть предмет в инвентарь.";
            DikozemieHeroMenuController.TraceLifecycle(
                "equipment drag unequip source=" + sourceSlot +
                " item=" + (itemId ?? string.Empty) +
                " success=" + removed);
            RefreshHeroVisual();
            RefreshInventory();
        }

        internal void ExecuteInventoryDrop(
            string itemId,
            int sourceSlot,
            int targetSlot)
        {
            if (string.IsNullOrWhiteSpace(itemId) ||
                targetSlot < 0 || targetSlot >= 12)
            {
                return;
            }

            ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
            bool equipped;
            bool swapped = false;
            string displacedItemId;
            if (sourceSlot >= 0)
            {
                equipped = DikozemieHeroProfile.TryMoveEquippedItem(
                    sourceSlot,
                    targetSlot,
                    out string movedItemId,
                    out displacedItemId,
                    out swapped);
                if (!string.Equals(movedItemId, itemId, StringComparison.Ordinal))
                {
                    equipped = false;
                }
            }
            else
            {
                equipped = DikozemieHeroProfile.TryEquipOwnedItemAtSlot(
                    itemId,
                    targetSlot,
                    out displacedItemId);
            }
            ItemObject displacedItem = string.IsNullOrWhiteSpace(displacedItemId)
                ? null
                : MBObjectManager.Instance.GetObject<ItemObject>(displacedItemId);
            string targetSlotName = GetEquipmentSlotName(targetSlot);
            StatusText = equipped
                ? (item?.Name?.ToString() ?? itemId) + ": помещено в «" +
                  targetSlotName + "»." +
                  (string.IsNullOrWhiteSpace(displacedItemId)
                      ? string.Empty
                      : swapped
                      ? " " + (displacedItem?.Name?.ToString() ?? displacedItemId) +
                        ": перемещено в «" + GetEquipmentSlotName(sourceSlot) + "»."
                      : " " + (displacedItem?.Name?.ToString() ?? displacedItemId) +
                        ": возвращено в инвентарь.")
                : "Этот предмет нельзя поместить в слот «" + targetSlotName + "».";
            DikozemieHeroMenuController.TraceLifecycle(
                "equipment drag drop source=" + sourceSlot +
                " target=" + targetSlot +
                " item=" + itemId +
                " displaced=" + (displacedItemId ?? string.Empty) +
                " swapped=" + swapped +
                " success=" + equipped);
            RefreshHeroVisual();
            RefreshInventory();
        }

        private static void FinalizeItems(MBBindingList<DikozemieHeroInventoryItemVM> items)
        {
            foreach (DikozemieHeroInventoryItemVM item in items)
            {
                item.OnFinalize();
            }
        }

        private static void FinalizeStoreItems(
            MBBindingList<DikozemieStoreItemVM> items)
        {
            foreach (DikozemieStoreItemVM item in items)
            {
                item.OnFinalize();
            }
        }

        private static void FinalizeEquipmentSlots(
            MBBindingList<DikozemieHeroEquipmentSlotVM> slots)
        {
            foreach (DikozemieHeroEquipmentSlotVM slot in slots)
            {
                slot.OnFinalize();
            }
        }

        private static string GetEquipmentSlotName(int slot)
        {
            string[] names =
            {
                "Оружие I", "Оружие II", "Оружие III", "Оружие IV",
                "Знамя", "Голова", "Тело", "Ноги", "Руки", "Накидка",
                "Конь", "Сбруя"
            };
            return slot >= 0 && slot < names.Length ? names[slot] : "Снаряжение";
        }

        private static string GetSkillDescription(string skillId)
        {
            switch (skillId)
            {
                case "OneHanded": return "Владение одноручным оружием и щитом.";
                case "TwoHanded": return "Сила и скорость ударов двуручным оружием.";
                case "Polearm": return "Копья, пики и другое древковое оружие.";
                case "Bow": return "Точность и скорость стрельбы из лука.";
                case "Crossbow": return "Точность, перезарядка и обращение с арбалетом.";
                case "Throwing": return "Меткость и сила метательного оружия.";
                case "Riding": return "Контроль лошади и боевая эффективность в седле.";
                case "Athletics": return "Скорость, выносливость и управление пешим героем.";
                case "Crafting": return "Ковка оружия, выплавка металла и переработка материалов.";
                case "Scouting": return "Поиск следов, обзор местности и раннее обнаружение угроз.";
                case "Tactics": return "Построения, управление войсками и преимущество в бою.";
                case "Roguery": return "Хитрость, добыча трофеев и рискованные способы заработка.";
                case "Charm": return "Убеждение, отношения и влияние на других героев.";
                case "Leadership": return "Боевой дух, командование и управление воинами.";
                case "Trade": return "Оценка товаров и получение прибыли от сделок.";
                case "Steward": return "Управление припасами, имуществом и большим отрядом.";
                case "Medicine": return "Лечение ран и сохранение бойцов после сражений.";
                case "Engineering": return "Строительство укреплений и применение осадных машин.";
                default: return "Навык развивается по мере побед и повышения уровня героя.";
            }
        }

        private static string GetSkillTrainingText(string skillId)
        {
            switch (skillId)
            {
                case "OneHanded": return "Сражайтесь одноручным оружием — мечами, топорами и булавами.";
                case "TwoHanded": return "Наносите урон двуручными мечами, топорами и молотами.";
                case "Polearm": return "Используйте копья, пики и другое древковое оружие в бою.";
                case "Bow": return "Попадайте во врагов из лука, особенно с большой дистанции.";
                case "Crossbow": return "Поражайте цели из арбалета и выполняйте точные дальние выстрелы.";
                case "Throwing": return "Наносите урон метательными копьями, топорами и ножами.";
                case "Riding": return "Передвигайтесь верхом и сражайтесь, не покидая седла.";
                case "Athletics": return "Передвигайтесь пешком и сражайтесь без верхового животного.";
                case "Crafting": return "Куйте оружие, очищайте материалы и выплавляйте металл.";
                case "Scouting": return "Исследуйте местность, находите следы и замечайте противника раньше.";
                case "Tactics": return "Побеждайте в сложных боях и грамотно командуйте отрядами.";
                case "Roguery": return "Используйте хитрость, трофеи и рискованные способы заработка.";
                case "Charm": return "Убеждайте людей и улучшайте отношения с другими героями.";
                case "Leadership": return "Поддерживайте боевой дух и ведите воинов за собой.";
                case "Trade": return "Получайте прибыль от купли и продажи товаров и снаряжения.";
                case "Steward": return "Управляйте припасами, имуществом и большим отрядом.";
                case "Medicine": return "Лечите раненых и помогайте бойцам пережить тяжёлые ранения.";
                case "Engineering": return "Создавайте и применяйте осадные машины и укрепления.";
                default: return "Применяйте этот навык в соответствующих действиях и боях.";
            }
        }

        private static string GetSkillEffectsText(string skillId, int level)
        {
            switch (skillId)
            {
                case "OneHanded":
                    return "• Урон одноручным оружием: +" + FormatPercent(level, 0.15f) + "\n" +
                           "• Скорость одноручного оружия: +" + FormatPercent(level, 0.07f);
                case "TwoHanded":
                    return "• Урон двуручным оружием: +" + FormatPercent(level, 0.16f) + "\n" +
                           "• Скорость двуручного оружия: +" + FormatPercent(level, 0.06f);
                case "Polearm":
                    return "• Урон древковым оружием: +" + FormatPercent(level, 0.07f) + "\n" +
                           "• Скорость древкового оружия: +" + FormatPercent(level, 0.06f);
                case "Bow":
                    return "• Урон из лука: +" + FormatPercent(level, 0.11f) + "\n" +
                           "• Точность лука: +" + FormatPercent(level, 0.09f);
                case "Crossbow":
                    return "• Скорость перезарядки арбалета: +" + FormatPercent(level, 0.07f) + "\n" +
                           "• Точность арбалета: +" + FormatPercent(level, 0.05f);
                case "Throwing":
                    return "• Скорость метательного оружия: +" + FormatPercent(level, 0.07f) + "\n" +
                           "• Урон метательным оружием: +" + FormatPercent(level, 0.06f) + "\n" +
                           "• Точность метательного оружия: +" + FormatPercent(level, 0.06f);
                case "Riding":
                    return "• Скорость лошади: +" + FormatPercent(level, 0.20f) + "\n" +
                           "• Манёвренность лошади: +" + FormatPercent(level, 0.04f) + "\n" +
                           "• Штраф урона верхом: " +
                               Math.Min(0f, -20f + level * 0.20f).ToString("0.0") + "%";
                case "Athletics":
                    return "• Скорость бега: +" + FormatPercent(level, 0.10f) + "\n" +
                           "• Штраф от веса уменьшен на: " + FormatPercent(level, 0.10f) + "\n" +
                           "• Сопротивление сбиванию: " +
                               (15f + level * 0.10f).ToString("0.0") + "% здоровья";
                case "Crafting":
                    return "• Сложность оружия без штрафа: " + FormatValue(level, 1f);
                case "Scouting":
                    return "• Радиус обнаружения следов: +" + FormatPercent(level, 0.10f) + "\n" +
                           "• Дальность обзора: +" + FormatPercent(level, 0.06f) + "\n" +
                           "• Подробность следов: " + FormatValue(level, 0.04f);
                case "Tactics":
                    return "• Преимущество в симуляции: +" + FormatPercent(level, 0.10f) + "\n" +
                           "• Потери при прорыве снижены на: " + FormatPercent(level, 0.10f);
                case "Roguery":
                    return "• Добыча после боя: +" + FormatPercent(level, 0.25f) + "\n" +
                           "• Урон скрытой атаки: +" +
                               (50f + level * 0.20f).ToString("0.0") + "%";
                case "Charm":
                    return "• Рост отношений с героями: +" + FormatPercent(level, 0.50f);
                case "Leadership":
                    return "• Боевой дух отрядов под командованием: +" +
                               FormatValue(level, 0.10f) + "\n" +
                           "• Размер гарнизона: +" + FormatValue(level, 0.20f);
                case "Trade":
                    return "• Торговый штраф уменьшен на: " + FormatPercent(level, 0.20f);
                case "Steward":
                    return "• Размер отряда: +" + FormatValue(level, 0.25f);
                case "Medicine":
                    return "• Шанс выживания раненых: +" + FormatPercent(level, 0.01f) + "\n" +
                           "• Скорость лечения героев: +" + FormatPercent(level, 0.50f) + "\n" +
                           "• Скорость лечения войск: +" + FormatPercent(level, 1.00f);
                case "Engineering":
                    return "• Производство осадных машин: +" + FormatPercent(level, 0.10f) + "\n" +
                           "• Строительство городских проектов: +" + FormatPercent(level, 0.25f);
                default:
                    return "• Нативные эффекты навыка применяются по его текущему уровню.";
            }
        }

        private static string FormatPercent(int level, float percentPerLevel)
        {
            return (Math.Max(0, level) * percentPerLevel).ToString("0.0") + "%";
        }

        private static string FormatValue(int level, float valuePerLevel)
        {
            return (Math.Max(0, level) * valuePerLevel).ToString("0.0");
        }

        private void SetString(
            ref string field,
            string value,
            [CallerMemberName] string propertyName = null)
        {
            string safe = value ?? string.Empty;
            if (string.Equals(field, safe, StringComparison.Ordinal))
            {
                return;
            }
            field = safe;
            OnPropertyChanged(propertyName);
        }

        public override void OnFinalize()
        {
            foreach (DikozemieHeroSkillItemVM item in SkillItems)
            {
                item.OnFinalize();
            }
            foreach (DikozemieHeroPerkTierVM tier in PerkTiers)
            {
                tier.OnFinalize();
            }
            FinalizeStoreItems(StoreItems);
            FinalizeItems(InventoryItems);
            FinalizeItems(EquippedItems);
            FinalizeEquipmentSlots(ArmorEquipmentSlots);
            FinalizeEquipmentSlots(WeaponEquipmentSlots);
            FinalizeEquipmentSlots(BannerEquipmentSlots);
            ItemTooltip?.OnFinalize();
            InventoryDragImageIdentifier?.OnFinalize();
            HeroVisual?.OnFinalize();
            FamilyVisualLeft?.OnFinalize();
            FamilyVisualRight?.OnFinalize();
            base.OnFinalize();
        }
    }

    internal sealed class DikozemieHeroMenuLayer : GlobalLayer, IDisposable
    {
        private readonly GauntletLayer _gauntletLayer;
        private readonly DikozemieHeroMenuVM _viewModel;
        private GauntletMovieIdentifier _movie;
        private Widget _rootWidget;
        private ButtonWidget _cultureNextButton;
        private ButtonWidget _inventoryDoneButton;
        private ButtonWidget _skillsDoneButton;
        private bool _inventoryLeftMouseWasDown;
        private DikozemieInventoryDragSourceWidget _inventoryDragSource;
        private System.Numerics.Vector2 _inventoryDragStartPosition;
        private bool _inventoryDragThresholdReached;

        internal DikozemieHeroMenuLayer()
        {
            DikozemieHeroMenuController.TraceLifecycle("layer construction started");
            LoadNativeSkillSpriteCategories();
            _viewModel = new DikozemieHeroMenuVM { IsVisible = true };
            DikozemieHeroMenuController.TraceLifecycle("view model created");
            _gauntletLayer = new GauntletLayer("DikozemieHeroMenu", 10001);
            _movie = _gauntletLayer.LoadMovie("DikozemieHeroMenu", _viewModel);
            DikozemieHeroMenuController.TraceLifecycle(
                _movie == null ? "movie load returned null" : "movie loaded");
            _rootWidget = _movie?.Movie?.RootWidget;
            if (_rootWidget != null)
            {
                foreach (ButtonWidget button in
                         _rootWidget.GetAllChildrenOfTypeRecursive<ButtonWidget>())
                {
                    if (string.Equals(
                            button.Id,
                            "HeroCultureNextButton",
                            StringComparison.Ordinal))
                    {
                        _cultureNextButton = button;
                    }
                    else if (string.Equals(
                            button.Id,
                            "HeroInventoryDoneButton",
                            StringComparison.Ordinal))
                    {
                        _inventoryDoneButton = button;
                    }
                    else if (string.Equals(
                            button.Id,
                            "HeroSkillsDoneButton",
                            StringComparison.Ordinal))
                    {
                        _skillsDoneButton = button;
                    }
                    else if (_skillsDoneButton == null && string.Equals(
                            button.Id,
                            "ConfirmButton",
                            StringComparison.Ordinal))
                    {
                        _skillsDoneButton = button;
                    }
                }
            }
            _gauntletLayer.InputRestrictions.SetInputRestrictions(
                true,
                InputUsageMask.Mouse | InputUsageMask.Keyboardkeys);
            Layer = _gauntletLayer;
            DikozemieHeroMenuController.TraceLifecycle("layer construction completed");
        }

        private static void LoadNativeSkillSpriteCategories()
        {
            try
            {
                UIResourceManager.LoadSpriteCategory("ui_characterdeveloper");
                UIResourceManager.LoadSpriteCategory("ui_inventory");
                UIResourceManager.LoadSpriteCategory("ui_mapbar");
            }
            catch (Exception error)
            {
                DikozemieHeroMenuController.TraceLifecycle(
                    "native profile art load failed: " + error.Message);
            }
        }

        internal void Refresh()
        {
            _viewModel.Refresh();
        }

        internal void ShowProfileSection(int section)
        {
            _viewModel.ShowProfileSection(section);
        }

        internal bool IsCloseRequested()
        {
            return _gauntletLayer.Input != null &&
                _gauntletLayer.Input.IsKeyReleased(InputKey.Escape);
        }

        internal bool PollCultureNextClick()
        {
            if (!_viewModel.IsCultureStage ||
                _cultureNextButton == null ||
                _gauntletLayer.Input == null ||
                !_gauntletLayer.Input.IsKeyReleased(InputKey.LeftMouseButton))
            {
                return false;
            }

            var rawMousePosition = _gauntletLayer.Input.GetMousePositionPixel();
            bool isInside = _cultureNextButton.IsPointInsideMeasuredArea(
                new System.Numerics.Vector2(
                    rawMousePosition.X,
                    rawMousePosition.Y));
            if (isInside)
            {
                DikozemieLobbyShellIntegration.TraceButtonAction(
                    "hero-culture-next direct input captured");
            }
            return isInside;
        }

        internal void ExecuteCultureNext()
        {
            _viewModel.ExecuteCultureNext();
        }

        internal bool PollInventoryDoneClick()
        {
            if (!_viewModel.IsInventory ||
                _inventoryDoneButton == null ||
                _gauntletLayer.Input == null ||
                !_gauntletLayer.Input.IsKeyReleased(InputKey.LeftMouseButton))
            {
                return false;
            }

            var rawMousePosition = _gauntletLayer.Input.GetMousePositionPixel();
            bool isInside = _inventoryDoneButton.IsPointInsideMeasuredArea(
                new System.Numerics.Vector2(
                    rawMousePosition.X,
                    rawMousePosition.Y));
            if (isInside)
            {
                DikozemieHeroMenuController.TraceLifecycle(
                    "inventory done direct input captured");
            }
            return isInside;
        }

        internal void ExecuteInventoryDone()
        {
            _viewModel.ExecuteInventoryDone();
        }

        internal bool PollInventoryDragDrop()
        {
            if (!_viewModel.IsInventory ||
                _rootWidget == null ||
                _gauntletLayer.Input == null)
            {
                ResetInventoryDrag();
                return false;
            }

            bool leftMouseDown = _gauntletLayer.Input.IsKeyDown(
                InputKey.LeftMouseButton);
            var rawMousePosition = _gauntletLayer.Input.GetMousePositionPixel();
            var mousePosition = new System.Numerics.Vector2(
                rawMousePosition.X,
                rawMousePosition.Y);

            if (leftMouseDown && !_inventoryLeftMouseWasDown)
            {
                _inventoryDragSource = FindInventoryDragWidget(
                    mousePosition,
                    sourceOnly: true);
                _inventoryDragStartPosition = mousePosition;
                _inventoryDragThresholdReached = false;
                if (_inventoryDragSource != null)
                {
                    _viewModel.BeginInventoryDragVisual(
                        _inventoryDragSource.ItemId,
                        mousePosition.X,
                        mousePosition.Y);
                    DikozemieHeroMenuController.TraceLifecycle(
                        "equipment drag press source=" +
                        _inventoryDragSource.SourceSlot +
                        " item=" + _inventoryDragSource.ItemId);
                }
            }
            else if (leftMouseDown && _inventoryLeftMouseWasDown &&
                _inventoryDragSource != null &&
                !_inventoryDragThresholdReached)
            {
                System.Numerics.Vector2 movement =
                    mousePosition - _inventoryDragStartPosition;
                _inventoryDragThresholdReached = movement.LengthSquared() >= 9f;
                if (_inventoryDragThresholdReached)
                {
                    DikozemieHeroMenuController.TraceLifecycle(
                        "equipment drag begin source=" +
                        _inventoryDragSource.SourceSlot +
                        " item=" + _inventoryDragSource.ItemId);
                }
            }
            else if (!leftMouseDown && _inventoryLeftMouseWasDown)
            {
                DikozemieInventoryDragSourceWidget source = _inventoryDragSource;
                bool wasDrag = source != null && _inventoryDragThresholdReached;
                _inventoryDragSource = null;
                _inventoryDragThresholdReached = false;
                _inventoryLeftMouseWasDown = false;
                _viewModel.EndInventoryDragVisual();
                if (wasDrag)
                {
                    DikozemieInventoryDragSourceWidget target =
                        FindInventoryDragWidget(mousePosition, sourceOnly: false);
                    if (target?.TargetSlot >= 0)
                    {
                        _viewModel.ExecuteInventoryDrop(
                            source.ItemId,
                            source.SourceSlot,
                            target.TargetSlot);
                    }
                    else if (target?.TargetSlot == -2 && source.SourceSlot >= 0)
                    {
                        _viewModel.ExecuteInventoryUnequipDrop(
                            source.ItemId,
                            source.SourceSlot);
                    }
                    else
                    {
                        DikozemieHeroMenuController.TraceLifecycle(
                            "equipment drag cancelled source=" +
                            source.SourceSlot + " item=" + source.ItemId);
                    }
                    return true;
                }
            }

            if (leftMouseDown && _inventoryDragSource != null)
            {
                _viewModel.UpdateInventoryDragVisualPosition(
                    mousePosition.X,
                    mousePosition.Y);
            }

            _inventoryLeftMouseWasDown = leftMouseDown;
            return false;
        }

        private DikozemieInventoryDragSourceWidget FindInventoryDragWidget(
            System.Numerics.Vector2 mousePosition,
            bool sourceOnly)
        {
            foreach (DikozemieInventoryDragSourceWidget widget in
                     _rootWidget.GetAllChildrenOfTypeRecursive<
                         DikozemieInventoryDragSourceWidget>())
            {
                if (sourceOnly)
                {
                    if (string.IsNullOrWhiteSpace(widget.ItemId))
                    {
                        continue;
                    }
                }
                else if (widget.TargetSlot == -1)
                {
                    continue;
                }

                if (widget.IsPointInsideMeasuredArea(mousePosition))
                {
                    return widget;
                }
            }
            return null;
        }

        private void ResetInventoryDrag()
        {
            _inventoryLeftMouseWasDown = false;
            _inventoryDragSource = null;
            _inventoryDragThresholdReached = false;
            _viewModel.EndInventoryDragVisual();
        }

        internal bool PollSkillsDoneClick()
        {
            if (!_viewModel.IsSkills ||
                !_viewModel.CanApplyDevelopmentChanges ||
                _skillsDoneButton == null ||
                _gauntletLayer.Input == null ||
                !_gauntletLayer.Input.IsKeyReleased(InputKey.LeftMouseButton))
            {
                return false;
            }

            var rawMousePosition = _gauntletLayer.Input.GetMousePositionPixel();
            bool isInside = _skillsDoneButton.IsPointInsideMeasuredArea(
                new System.Numerics.Vector2(
                    rawMousePosition.X,
                    rawMousePosition.Y));
            if (isInside)
            {
                DikozemieHeroMenuController.TraceLifecycle(
                    "skills done direct input captured");
            }
            return isInside;
        }

        internal void ExecuteApplyDevelopmentChanges()
        {
            _viewModel.ExecuteApplyDevelopmentChanges();
        }

        public void Dispose()
        {
            ResetInventoryDrag();
            _rootWidget = null;
            _gauntletLayer.InputRestrictions.ResetInputRestrictions();
            if (_movie != null)
            {
                _gauntletLayer.ReleaseMovie(_movie);
                _movie = null;
            }
            _viewModel.OnFinalize();
        }
    }

    internal static class DikozemieHeroMenuController
    {
        private static DikozemieHeroMenuLayer _layer;
        private static DateTimeOffset _nextShowAttemptUtc;
        private static bool _showFailureAnnounced;

        internal static bool IsOpen => _layer != null;

        internal static void Show()
        {
            if (_layer == null)
            {
                // The lobby tick re-requests the menu every frame while the
                // Hero tab is selected; a failing build wrote the same stack
                // trace hundreds of times per second (client-ui.log, 300 MB).
                if (DateTimeOffset.UtcNow < _nextShowAttemptUtc)
                {
                    return;
                }
                try
                {
                    TraceLifecycle("show requested");
                    _layer = new DikozemieHeroMenuLayer();
                    ScreenManager.AddGlobalLayer(_layer, false);
                    TraceLifecycle("global layer added");
                    Console.WriteLine("DikozemieOnline: hero menu opened");
                }
                catch (Exception error)
                {
                    _layer = null;
                    _nextShowAttemptUtc = DateTimeOffset.UtcNow.AddSeconds(5);
                    TraceLifecycle("show failed: " + error);
                    Console.WriteLine("DikozemieOnline: hero menu failed: " + error);
                    if (!_showFailureAnnounced)
                    {
                        _showFailureAnnounced = true;
                        InformationManager.DisplayMessage(new InformationMessage(
                            "Меню героя не открылось. Переустановите Арену с сайта; " +
                            "журнал: %LOCALAPPDATA%\\Dikozemie\\logs\\client-ui.log",
                            Colors.Red));
                    }
                }
            }
            else
            {
                _layer.Refresh();
            }
        }

        internal static void TraceLifecycle(string message)
        {
            try
            {
                string folder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Dikozemie",
                    "logs");
                System.IO.Directory.CreateDirectory(folder);
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(folder, "client-ui.log"),
                    DateTimeOffset.Now.ToString("O") + " hero-menu " + message +
                    Environment.NewLine);
            }
            catch
            {
                // UI diagnostics must never become another reason the menu fails.
            }
        }

        internal static void Refresh()
        {
            _layer?.Refresh();
        }

        internal static void ShowProfileSection(int section)
        {
            _layer?.ShowProfileSection(section);
        }

        internal static void Tick()
        {
            DikozemieHeroMenuLayer layer = _layer;
            if (layer?.PollInventoryDragDrop() == true)
            {
                return;
            }
            if (layer?.PollCultureNextClick() == true)
            {
                // A full-screen native lobby widget can consume the normal
                // Gauntlet command. The visible button remains authoritative.
                layer.ExecuteCultureNext();
                return;
            }
            if (layer?.PollInventoryDoneClick() == true)
            {
                // Keep the native-looking confirmation responsive even when
                // the underlying multiplayer lobby consumes the mouse release.
                layer.ExecuteInventoryDone();
                return;
            }
            if (layer?.PollSkillsDoneClick() == true)
            {
                // The native multiplayer header may consume the normal
                // command; commit the visible skills confirmation directly.
                layer.ExecuteApplyDevelopmentChanges();
                return;
            }
            if (layer?.IsCloseRequested() == true)
            {
                DikozemieLobbyShellIntegration.SelectTopTab(0);
            }
        }

        internal static void Hide()
        {
            DikozemieHeroMenuLayer layer = _layer;
            if (layer == null)
            {
                return;
            }
            _layer = null;
            // Bannerlord 1.5.7 finalizes a removed global layer, so the movie
            // is released first (the native views do the same).
            try
            {
                layer.Dispose();
            }
            finally
            {
                ScreenManager.RemoveGlobalLayer(layer);
            }
        }

        internal static void AbandonForApplicationShutdown()
        {
            _layer = null;
        }
    }
}
