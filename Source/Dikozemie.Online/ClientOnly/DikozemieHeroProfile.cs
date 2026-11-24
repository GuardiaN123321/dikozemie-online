using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;
using TaleWorlds.Core;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using TaleWorlds.Localization;

namespace Dikozemie.Online
{
    [DataContract]
    internal sealed class DikozemieHeroValue
    {
        [DataMember(Name = "id")]
        public string Id = string.Empty;

        [DataMember(Name = "value")]
        public int Value;
    }

    [DataContract]
    internal sealed class DikozemieHeroItem
    {
        [DataMember(Name = "slot")]
        public int Slot;

        [DataMember(Name = "itemId")]
        public string ItemId = string.Empty;

        [DataMember(Name = "modifierId")]
        public string ModifierId = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieHeroPerkChoice
    {
        [DataMember(Name = "skillId")]
        public string SkillId = string.Empty;

        [DataMember(Name = "requiredLevel")]
        public int RequiredLevel;

        [DataMember(Name = "perkId")]
        public string PerkId = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieHeroProfileState
    {
        [DataMember(Name = "schemaVersion")]
        public int SchemaVersion = DikozemieHeroProfile.CurrentSchemaVersion;

        [DataMember(Name = "heroId")]
        public string HeroId = Guid.NewGuid().ToString();

        [DataMember(Name = "creationCompleted")]
        public bool CreationCompleted;

        [DataMember(Name = "creationStep")]
        public int CreationStep;

        [DataMember(Name = "name")]
        public string Name = "Герой арены";

        [DataMember(Name = "cultureId")]
        public string CultureId = string.Empty;

        [DataMember(Name = "cultureConfirmed")]
        public bool CultureConfirmed;

        [DataMember(Name = "appearanceConfirmed")]
        public bool AppearanceConfirmed;

        [DataMember(Name = "bodyProperties")]
        public string BodyProperties = string.Empty;

        [DataMember(Name = "isFemale")]
        public bool IsFemale;

        [DataMember(Name = "race")]
        public int Race;

        [DataMember(Name = "level")]
        public int Level = 1;

        [DataMember(Name = "experience")]
        public int Experience;

        [DataMember(Name = "attributePoints")]
        public int AttributePoints;

        [DataMember(Name = "focusPoints")]
        public int FocusPoints;

        [DataMember(Name = "startingCharacterId")]
        public string StartingCharacterId = string.Empty;

        [DataMember(Name = "archetypeName")]
        public string ArchetypeName = "Легионер";

        [DataMember(Name = "battleMounted")]
        public bool BattleMounted;

        [DataMember(Name = "backstoryChoices")]
        public List<string> BackstoryChoices = new List<string>();

        [DataMember(Name = "attributes")]
        public List<DikozemieHeroValue> Attributes = new List<DikozemieHeroValue>();

        [DataMember(Name = "focus")]
        public List<DikozemieHeroValue> Focus = new List<DikozemieHeroValue>();

        [DataMember(Name = "skills")]
        public List<DikozemieHeroValue> Skills = new List<DikozemieHeroValue>();

        [DataMember(Name = "skillExperience")]
        public List<DikozemieHeroValue> SkillExperience = new List<DikozemieHeroValue>();

        [DataMember(Name = "selectedPerks")]
        public List<DikozemieHeroPerkChoice> SelectedPerks =
            new List<DikozemieHeroPerkChoice>();

        [DataMember(Name = "ownedItems")]
        public List<string> OwnedItems = new List<string>();

        [DataMember(Name = "equipment")]
        public List<DikozemieHeroItem> Equipment = new List<DikozemieHeroItem>();

        [DataMember(Name = "progressionResetToken")]
        public string ProgressionResetToken = string.Empty;

        [DataMember(Name = "parentOccupation")]
        public string ParentOccupation = string.Empty;

        [DataMember(Name = "selectedTitleType")]
        public string SelectedTitleType = "guard";

        [DataMember(Name = "startingAge")]
        public int StartingAge = 20;

        [DataMember(Name = "creationRenown")]
        public int CreationRenown;

        [DataMember(Name = "traits")]
        public List<DikozemieHeroValue> Traits = new List<DikozemieHeroValue>();
    }

    internal sealed class DikozemieHeroBattleProgress
    {
        internal DikozemieHeroBattleProgress(
            int experienceEarned,
            string summary)
        {
            ExperienceEarned = Math.Max(0, experienceEarned);
            Summary = summary ?? string.Empty;
        }

        internal int ExperienceEarned { get; }
        internal string Summary { get; }
    }

    internal sealed class DikozemieHeroBackgroundChoice
    {
        internal DikozemieHeroBackgroundChoice(
            string id,
            string title,
            string description,
            string track,
            string attribute,
            params string[] skills)
        {
            Id = id;
            TitleSource = title;
            DescriptionSource = description;
            Track = track;
            Attribute = attribute;
            Skills = skills ?? Array.Empty<string>();
        }

        internal string Id { get; }
        internal string TitleSource { get; }
        internal string DescriptionSource { get; }
        internal string Title => Localize(TitleSource);
        internal string Description => Localize(DescriptionSource);
        internal string Track { get; }
        internal string Attribute { get; }
        internal IReadOnlyList<string> Skills { get; }
        internal string Skill => Skills.FirstOrDefault() ?? string.Empty;
        internal string ParentOccupation { get; set; } = string.Empty;
        internal string SelectedTitleType { get; set; } = string.Empty;
        internal string AnimationId { get; set; } = string.Empty;
        internal string MotherAnimationId { get; set; } = string.Empty;
        internal string FatherAnimationId { get; set; } = string.Empty;
        internal string MotherLeftPropId { get; set; } = string.Empty;
        internal string MotherRightPropId { get; set; } = string.Empty;
        internal string FatherLeftPropId { get; set; } = string.Empty;
        internal string FatherRightPropId { get; set; } = string.Empty;
        internal string LeftPropId { get; set; } = string.Empty;
        internal string RightPropId { get; set; } = string.Empty;
        internal int PreviewAge { get; set; }
        internal int UnspentFocusPoints { get; set; }
        internal int UnspentAttributePoints { get; set; }
        internal int Renown { get; set; }
        internal IReadOnlyList<KeyValuePair<string, int>> Traits { get; set; } =
            Array.Empty<KeyValuePair<string, int>>();

        private static string Localize(string source)
        {
            return DikozemieCreationText.Localize(source);
        }
    }

    /// <summary>
    /// One durable multiplayer avatar. The file is deliberately separate from
    /// armory-progression.json so existing rating, coins and purchases survive
    /// profile upgrades unchanged.
    /// </summary>
    internal static class DikozemieHeroProfile
    {
        internal const int CreationStepCount = 6;
        internal const int StartingAttributePoints = 1;
        internal const int StartingFocusPoints = 2;
        internal const int FocusPointsPerLevel = 1;
        internal const int LevelsPerAttributePoint = 4;
        internal const int MaximumFocusPerSkill = 5;
        internal const int MaximumAttribute = 10;
        internal const int MaximumHeroLevel = 62;
        // 14: heroes no longer get the creation roster's horse for free.
        internal const int CurrentSchemaVersion = 14;
        private const string HeroTemplateCharacterId = "mp_character";

        private static readonly object Gate = new object();
        private static readonly string[] AttributeIds =
        {
            "vigor", "control", "endurance", "cunning", "social", "intelligence"
        };
        private static readonly string[] AllSkillIds =
        {
            "OneHanded", "TwoHanded", "Polearm", "Bow", "Crossbow",
            "Throwing", "Riding", "Athletics", "Crafting", "Scouting",
            "Tactics", "Roguery", "Charm", "Leadership", "Trade",
            "Steward", "Medicine", "Engineering", "Mariner", "Boatswain",
            "Shipmaster"
        };

        private static DikozemieHeroProfileState _state;
        private static bool _starterEquipmentMigrationPending;
        private static bool _freeHorseMigrationPending;
        private static readonly Dictionary<string, Equipment>
            CreationEquipmentRosterCache =
                new Dictionary<string, Equipment>(StringComparer.Ordinal);
        private static readonly IReadOnlyDictionary<string, string>
            MultiplayerPreviewItemFallbacks =
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    // NavalDLC contributes the Nord and maritime creator
                    // branches, but its mesh packages are not mounted by the
                    // official multiplayer module set.  Keep every native
                    // answer/roster and replace only the DLC-exclusive
                    // visual items with the closest loaded base-game piece.
                    ["heavy_gambeson_armor"] = "northern_padded_gambeson",
                    ["heavy_gambeson_armor_plain"] = "northern_padded_cloth",
                    ["nord_casual_boots"] = "sturgia_boots_c",
                    ["nord_casual_cloak"] = "battania_cloak",
                    ["nord_casual_tunic"] = "northern_tunic",
                    // Added by Bannerlord 1.5.7 / War Sails 1.3.7.
                    ["nord_helmet_fa"] = "nasalhelm_over_leather",
                    ["nordic_civilian_dress"] = "northern_dress",
                    ["nordic_civilian_dress_plaid"] = "laced_dress",
                    ["nordic_civilian_hat_fur_brim"] = "nordic_fur_cap",
                    ["nord_battle_axe_4_t3"] = "northern_battle_axe_t3",
                    ["nord_battle_axe_6_t2"] = "sturgia_axe_2_t2",
                    ["nord_battle_axe_7_t2"] = "sturgia_axe_2_t2",
                    ["nord_hatchett_3_t1"] = "peasant_hatchet_1_t1",
                    ["nord_spear_1_t2"] = "northern_spear_1_t2",
                    ["nord_spear_2_t2"] = "northern_spear_1_t2",
                    ["nord_spear_javelin_1_t3"] = "generic_javelin_1_t3",
                    ["nord_sword_1_t2"] = "sturgia_sword_1_t2",
                    ["nord_throwing_axe_1_t2"] =
                        "highland_throwing_axe_1_t2",
                    ["northman_raider_armor"] = "northern_leather_tabard",
                    ["northman_raider_boot"] = "sturgia_boots_a",
                    ["pirate_a"] = "northern_leather_vest",
                    ["pirate_a_head"] = "turban",
                    ["southern_pirate_armor"] = "aserai_civil_c",
                    ["splinted_bracers"] = "northern_brass_bracers",
                    ["strapped_jackboots"] = "strapped_leather_boots"
                };
        private static bool _creationEquipmentRosterCacheLoaded;

        private static string StatePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Dikozemie",
            "arena",
            "hero-profile.json");

        internal static DikozemieHeroProfileState Snapshot
        {
            get
            {
                EnsureLoaded();
                lock (Gate)
                {
                    return _state;
                }
            }
        }

        internal static bool IsCreated => Snapshot.CreationCompleted;
        internal static int Level => Snapshot.Level;
        internal static int Experience => Snapshot.Experience;
        internal static int AttributePoints => Snapshot.AttributePoints;
        internal static int FocusPoints => Snapshot.FocusPoints;
        internal static string ArchetypeName => Snapshot.ArchetypeName;
        internal static bool BattleMounted
        {
            get
            {
                EnsureLoaded();
                lock (Gate)
                {
                    return HasEquippedHorse(_state.Equipment);
                }
            }
        }
        internal static string CultureId => Snapshot.CultureId;
        internal static int StartingAge => Snapshot.StartingAge;
        internal static bool CultureConfirmed => Snapshot.CultureConfirmed;
        internal static bool AppearanceConfirmed => Snapshot.AppearanceConfirmed;

        internal static void Initialize()
        {
            EnsureLoaded();
            CaptureAppearanceFromMultiplayerProfile(false);
        }

        internal static void TickMigrations()
        {
            EnsureLoaded();
            lock (Gate)
            {
                if (!_starterEquipmentMigrationPending &&
                    !_freeHorseMigrationPending)
                {
                    return;
                }

                // OnSubModuleLoad runs before Bannerlord has populated the
                // object manager.  Rebuilding equipment there crashes native
                // startup, so perform the one-time conversion from the first
                // application tick where characters and items are available.
                MBObjectManager objects = MBObjectManager.Instance;
                if (objects == null ||
                    objects.GetObject<BasicCharacterObject>(HeroTemplateCharacterId) == null ||
                    objects.GetObject<ItemObject>("mp_vlandian_short_sword") == null)
                {
                    return;
                }

                if (_starterEquipmentMigrationPending)
                {
                    MigrateNativeTroopProfileLocked();
                    if (_state.Equipment.Count == 0)
                    {
                        return;
                    }
                    _starterEquipmentMigrationPending = false;
                }
                if (_freeHorseMigrationPending)
                {
                    RemoveFreeCreationHorseLocked();
                    _freeHorseMigrationPending = false;
                }
                _state.SchemaVersion = CurrentSchemaVersion;
                SaveLocked();
            }
        }

        // Customer 2026-11-05: «Надо, чтоб все бедолаги начинали без лошади.
        // Лошадь ещё надо заслужить». Earlier builds handed out the horse and
        // harness of the creation roster; take back exactly those items. A
        // horse bought in the market is a different item and stays.
        private static void RemoveFreeCreationHorseLocked()
        {
            if (!_state.CreationCompleted)
            {
                return;
            }
            string rosterId =
                DikozemieNativeCreationCatalog.ResolvePreviewEquipmentId(
                    5,
                    _state.IsFemale,
                    _state.CultureId,
                    ResolveParentOccupationLocked(),
                    _state.SelectedTitleType,
                    null);
            MBEquipmentRoster roster = MBObjectManager.Instance
                .GetObject<MBEquipmentRoster>(rosterId);
            Equipment creation = roster?.DefaultEquipment ??
                roster?.AllEquipments?.FirstOrDefault() ??
                ResolveCreationEquipmentFromCatalog(rosterId);
            if (creation == null)
            {
                return;
            }

            var removed = new List<string>();
            foreach (EquipmentIndex slot in new[]
                     {
                         EquipmentIndex.Horse,
                         EquipmentIndex.HorseHarness
                     })
            {
                string freeItemId = creation[slot].Item?.StringId;
                if (string.IsNullOrWhiteSpace(freeItemId))
                {
                    continue;
                }
                int unequipped = _state.Equipment.RemoveAll(saved =>
                    saved != null && saved.Slot == (int)slot &&
                    string.Equals(saved.ItemId, freeItemId, StringComparison.Ordinal));
                bool unowned = _state.OwnedItems.Remove(freeItemId);
                if (unequipped > 0 || unowned)
                {
                    removed.Add(freeItemId);
                }
            }
            if (removed.Count > 0)
            {
                SynchronizeMountedStateLocked();
                DikozemieHeroMenuController.TraceLifecycle(
                    "hero free creation horse taken back roster=" + rosterId +
                    " items=" + string.Join(",", removed));
            }
        }

        internal static string GetCreationQuestion(int step)
        {
            EnsureLoaded();
            lock (Gate)
            {
                return DikozemieNativeCreationCatalog.GetQuestion(
                    step,
                    ResolveParentOccupationLocked(),
                    _state.IsFemale);
            }
        }

        internal static IReadOnlyList<DikozemieHeroBackgroundChoice> GetCreationChoices(int step)
        {
            EnsureLoaded();
            lock (Gate)
            {
                return DikozemieNativeCreationCatalog.GetChoices(
                    step,
                    _state.CultureId,
                    ResolveParentOccupationLocked());
            }
        }

        internal static string GetCreationStageTitle(int step)
        {
            return DikozemieNativeCreationCatalog.GetStageTitle(step);
        }

        internal static DikozemieHeroBackgroundChoice GetCreationChoicePreview(
            int step,
            int choiceIndex)
        {
            EnsureLoaded();
            lock (Gate)
            {
                IReadOnlyList<DikozemieHeroBackgroundChoice> choices =
                    DikozemieNativeCreationCatalog.GetChoices(
                        step,
                        _state.CultureId,
                        ResolveParentOccupationLocked());
                int index = choiceIndex;
                if (index < 0 || index >= choices.Count)
                {
                    index = GetSelectedCreationChoiceIndex(step);
                }
                return index >= 0 && index < choices.Count ? choices[index] : null;
            }
        }

        internal static string ResolveCreationEquipmentRosterId(
            int step,
            int choiceIndex,
            bool isFemale)
        {
            EnsureLoaded();
            lock (Gate)
            {
                DikozemieHeroBackgroundChoice choice = GetCreationChoicePreview(
                    step,
                    choiceIndex);
                return DikozemieNativeCreationCatalog.ResolvePreviewEquipmentId(
                    step,
                    isFemale,
                    _state.CultureId,
                    ResolveParentOccupationLocked(),
                    _state.SelectedTitleType,
                    choice);
            }
        }

        internal static Equipment CreateCreationPreviewEquipment(
            int step,
            int choiceIndex,
            bool isFemale,
            Equipment fallback)
        {
            string rosterId = ResolveCreationEquipmentRosterId(
                step,
                choiceIndex,
                isFemale);
            MBEquipmentRoster roster = MBObjectManager.Instance
                .GetObject<MBEquipmentRoster>(rosterId);
            Equipment equipment = roster?.DefaultEquipment ??
                roster?.AllEquipments?.FirstOrDefault();
            equipment = equipment ?? ResolveCreationEquipmentFromCatalog(rosterId) ??
                fallback;
            if (equipment == null || (equipment[EquipmentIndex.Horse].Item == null &&
                    equipment[EquipmentIndex.HorseHarness].Item == null))
            {
                return equipment;
            }
            // The campaign rosters stand the youth and the finished hero next
            // to a horse. The Arena hero starts on foot (customer 2026-11-05),
            // so the creation pages must not promise one.
            var onFoot = new Equipment(equipment);
            onFoot[EquipmentIndex.Horse] = default(EquipmentElement);
            onFoot[EquipmentIndex.HorseHarness] = default(EquipmentElement);
            return onFoot;
        }

        private static Equipment ResolveCreationEquipmentFromCatalog(
            string rosterId)
        {
            if (string.IsNullOrWhiteSpace(rosterId))
            {
                return null;
            }

            EnsureCreationEquipmentRosterCacheLoaded();
            return CreationEquipmentRosterCache.TryGetValue(
                rosterId,
                out Equipment equipment)
                    ? equipment
                    : null;
        }

        private static void EnsureCreationEquipmentRosterCacheLoaded()
        {
            if (_creationEquipmentRosterCacheLoaded)
            {
                return;
            }

            _creationEquipmentRosterCacheLoaded = true;
            try
            {
                string path = Path.Combine(
                    ModuleHelper.GetModuleFullPath(DikozemieSubModule.ModuleId),
                    "ModuleData",
                    "singleplayer_catalog",
                    "creation_equipment_sets.xml");
                if (!File.Exists(path))
                {
                    return;
                }

                var document = new XmlDocument();
                document.Load(path);
                XmlNodeList rosters = document.SelectNodes(
                    "/EquipmentRosters/EquipmentRoster");
                if (rosters == null)
                {
                    return;
                }

                foreach (XmlNode rosterNode in rosters)
                {
                    if (!(rosterNode is XmlElement rosterElement))
                    {
                        continue;
                    }

                    string rosterId = rosterElement.GetAttribute("id");
                    if (string.IsNullOrWhiteSpace(rosterId) ||
                        CreationEquipmentRosterCache.ContainsKey(rosterId))
                    {
                        continue;
                    }

                    var equipment = new Equipment(
                        Equipment.EquipmentType.Civilian);
                    XmlNodeList equipmentNodes = rosterElement.SelectNodes(
                        "./EquipmentSet[1]/Equipment");
                    if (equipmentNodes != null)
                    {
                        foreach (XmlNode equipmentNode in equipmentNodes)
                        {
                            if (!(equipmentNode is XmlElement element) ||
                                !Enum.TryParse(
                                    element.GetAttribute("slot"),
                                    true,
                                    out EquipmentIndex slot))
                            {
                                continue;
                            }

                            string itemId = StripObjectPrefix(
                                element.GetAttribute("id"),
                                "Item.");
                            if (MultiplayerPreviewItemFallbacks.TryGetValue(
                                    itemId,
                                    out string fallbackItemId))
                            {
                                itemId = fallbackItemId;
                            }
                            ItemObject item = MBObjectManager.Instance
                                .GetObject<ItemObject>(itemId);
                            if (item == null)
                            {
                                continue;
                            }

                            string modifierId = StripObjectPrefix(
                                element.GetAttribute("modifier"),
                                "ItemModifier.");
                            ItemModifier modifier = string.IsNullOrWhiteSpace(
                                modifierId)
                                    ? null
                                    : MBObjectManager.Instance
                                        .GetObject<ItemModifier>(modifierId);
                            equipment[slot] = new EquipmentElement(item, modifier);
                        }
                    }
                    CreationEquipmentRosterCache[rosterId] = equipment;
                }
            }
            catch (Exception error)
            {
                Console.WriteLine(
                    "DikozemieOnline: character-creation equipment catalogue " +
                    "failed to load: " + error);
            }
        }

        private static string StripObjectPrefix(string value, string prefix)
        {
            string result = value ?? string.Empty;
            return result.StartsWith(prefix, StringComparison.Ordinal)
                ? result.Substring(prefix.Length)
                : result;
        }

        internal static void SetCulture(string cultureId)
        {
            EnsureLoaded();
            lock (Gate)
            {
                _state.CultureId = NormalizeCultureId(cultureId);
                _state.CultureConfirmed = false;
                _state.AppearanceConfirmed = false;
                string bodyCulture = string.Equals(
                    _state.CultureId,
                    "nords",
                    StringComparison.Ordinal)
                        ? "nord"
                        : _state.CultureId;
                MBBodyProperty cultureBody = MBObjectManager.Instance
                    .GetObject<MBBodyProperty>(
                        "default_character_creation_body_property_" +
                        bodyCulture);
                if (cultureBody != null)
                {
                    _state.BodyProperties = cultureBody.BodyPropertyMax.ToString();
                    _state.Race = 0;
                }
                _state.BackstoryChoices.Clear();
                _state.ParentOccupation = string.Empty;
                _state.SelectedTitleType = "guard";
                _state.StartingAge = 20;
                _state.CreationStep = 0;
                SaveLocked();
            }
        }

        internal static bool ConfirmCultureSelection()
        {
            EnsureLoaded();
            lock (Gate)
            {
                if (string.IsNullOrWhiteSpace(_state.CultureId))
                {
                    return false;
                }
                _state.CultureConfirmed = true;
                SaveLocked();
                return true;
            }
        }

        internal static void MarkAppearanceConfirmed(bool confirmed = true)
        {
            EnsureLoaded();
            lock (Gate)
            {
                _state.AppearanceConfirmed = confirmed;
                SaveLocked();
            }
        }

        internal static int GetCreationAttributePreview(string attributeId)
        {
            EnsureLoaded();
            lock (Gate)
            {
                int value = 2;
                foreach (DikozemieHeroBackgroundChoice selected in
                         GetSelectedCreationChoicesLocked())
                {
                    if (string.Equals(
                            selected.Attribute,
                            attributeId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        value++;
                    }
                }
                return value;
            }
        }

        internal static int GetCreationFocusPreview(string skillId)
        {
            EnsureLoaded();
            lock (Gate)
            {
                int value = 0;
                foreach (DikozemieHeroBackgroundChoice selected in
                         GetSelectedCreationChoicesLocked())
                {
                    if (selected.Skills.Any(skill => string.Equals(
                            skill, skillId, StringComparison.OrdinalIgnoreCase)))
                    {
                        value++;
                    }
                }
                return Math.Min(5, value);
            }
        }

        internal static int GetSelectedCreationChoiceIndex(int step)
        {
            EnsureLoaded();
            int safeStep = Math.Max(0, Math.Min(CreationStepCount - 1, step));
            lock (Gate)
            {
                if (_state.BackstoryChoices.Count <= safeStep)
                {
                    return -1;
                }
                string id = _state.BackstoryChoices[safeStep];
                IReadOnlyList<DikozemieHeroBackgroundChoice> choices =
                    DikozemieNativeCreationCatalog.GetChoices(
                        safeStep,
                        _state.CultureId,
                        ResolveParentOccupationLocked());
                for (int index = 0; index < choices.Count; index++)
                {
                    if (string.Equals(choices[index].Id, id, StringComparison.Ordinal))
                    {
                        return index;
                    }
                }
                return -1;
            }
        }

        internal static void SelectCreationChoice(int step, int choiceIndex)
        {
            EnsureLoaded();
            int safeStep = Math.Max(0, Math.Min(CreationStepCount - 1, step));
            lock (Gate)
            {
                IReadOnlyList<DikozemieHeroBackgroundChoice> choices =
                    DikozemieNativeCreationCatalog.GetChoices(
                        safeStep,
                        _state.CultureId,
                        ResolveParentOccupationLocked());
                if (choices.Count == 0)
                {
                    return;
                }
                int safeChoice = Math.Max(0, Math.Min(choices.Count - 1, choiceIndex));
                while (_state.BackstoryChoices.Count <= safeStep)
                {
                    _state.BackstoryChoices.Add(string.Empty);
                }
                DikozemieHeroBackgroundChoice selected = choices[safeChoice];
                _state.BackstoryChoices[safeStep] = selected.Id;
                if (_state.BackstoryChoices.Count > safeStep + 1)
                {
                    _state.BackstoryChoices.RemoveRange(
                        safeStep + 1,
                        _state.BackstoryChoices.Count - safeStep - 1);
                }
                if (safeStep == 0)
                {
                    _state.ParentOccupation = selected.ParentOccupation;
                }
                if (safeStep == 3)
                {
                    _state.SelectedTitleType = selected.SelectedTitleType;
                }
                if (safeStep == 5)
                {
                    _state.StartingAge = selected.PreviewAge;
                }
                _state.CreationStep = safeStep;
                SaveLocked();
            }
        }

        internal static void SetCreationStep(int step)
        {
            EnsureLoaded();
            lock (Gate)
            {
                _state.CreationStep = Math.Max(0, Math.Min(CreationStepCount, step));
                SaveLocked();
            }
        }

        internal static bool CompleteCreation(string name)
        {
            EnsureLoaded();
            lock (Gate)
            {
                if (_state.BackstoryChoices.Count < CreationStepCount ||
                    Enumerable.Range(0, CreationStepCount).Any(step =>
                        string.IsNullOrWhiteSpace(_state.BackstoryChoices[step])) ||
                    !_state.CultureConfirmed || !_state.AppearanceConfirmed ||
                    !IsAdultBodyProperties(_state.BodyProperties))
                {
                    return false;
                }
                _state.Name = string.IsNullOrWhiteSpace(name) ? "Герой арены" : name.Trim();
                CompleteCreationLocked();
                SaveLocked();
                return true;
            }
        }

        internal static void SetBattleMounted(bool mounted)
        {
            EnsureLoaded();
            lock (Gate)
            {
                // Kept for compatibility with the old local-training stance
                // prompt. Arena mounting is authoritative from the equipment
                // slots: an equipped horse must never disappear in transit.
                _state.BattleMounted = HasEquippedHorse(_state.Equipment);
                SaveLocked();
            }
        }

        // Changes with every new hero: a new hero starts in his homeland.
        internal static string TravelToken
        {
            get
            {
                EnsureLoaded();
                lock (Gate)
                {
                    return (_state.HeroId ?? string.Empty) + "/" +
                        (_state.ProgressionResetToken ?? string.Empty);
                }
            }
        }

        internal static void ResetHero()
        {
            EnsureLoaded();
            lock (Gate)
            {
                // HeroId is the stable account slot used by the server
                // leaderboard. Keeping it lets the server replace the same
                // row instead of leaving a duplicate retired hero behind.
                string heroId = Guid.TryParse(_state.HeroId, out Guid parsed) &&
                    parsed != Guid.Empty
                        ? parsed.ToString()
                        : Guid.NewGuid().ToString();
                _state = new DikozemieHeroProfileState
                {
                    HeroId = heroId,
                    ProgressionResetToken = Guid.NewGuid().ToString()
                };
                _starterEquipmentMigrationPending = false;
                _freeHorseMigrationPending = false;
                SaveLocked();
            }
            DikozemieArmoryProgression.ResetHeroProgression();
        }

        internal static void CaptureAppearanceFromMultiplayerProfile(bool force)
        {
            EnsureLoaded();
            var playerData = NetworkMain.GameClient?.PlayerData;
            if (playerData == null)
            {
                return;
            }
            lock (Gate)
            {
                if (force || string.IsNullOrWhiteSpace(_state.BodyProperties))
                {
                    _state.BodyProperties = playerData.BodyProperties.ToString();
                    _state.IsFemale = playerData.IsFemale;
                    _state.Race = 0;
                    SaveLocked();
                }
            }
        }

        internal static void SetAppearance(
            string bodyProperties,
            bool isFemale,
            int race)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(bodyProperties))
            {
                return;
            }
            lock (Gate)
            {
                _state.BodyProperties = bodyProperties;
                _state.IsFemale = isFemale;
                _state.Race = Math.Max(0, race);
                SaveLocked();
            }
        }

        internal static int GetAttribute(string id)
        {
            EnsureLoaded();
            lock (Gate)
            {
                return GetValue(_state.Attributes, id, 2);
            }
        }

        internal static int GetFocus(string skillId)
        {
            EnsureLoaded();
            lock (Gate)
            {
                return GetValue(_state.Focus, skillId, 0);
            }
        }

        internal static int GetEffectiveSkill(string skillId, int fallback)
        {
            EnsureLoaded();
            lock (Gate)
            {
                if (!_state.CreationCompleted)
                {
                    return Math.Max(0, fallback);
                }
                return Math.Max(0, Math.Min(300,
                    GetValue(_state.Skills, skillId, 0)));
            }
        }

        internal static int GetSkillExperience(string skillId)
        {
            EnsureLoaded();
            lock (Gate)
            {
                return GetValue(_state.SkillExperience, skillId, 0);
            }
        }

        internal static int SkillExperienceForNextLevel(int level)
        {
            return DikozemieArenaCombatProgression.ExperienceForNextLevel(
                Math.Max(0, Math.Min(300, level)));
        }

        internal static float GetLearningRate(string skillId)
        {
            EnsureLoaded();
            lock (Gate)
            {
                return GetLearningRateLocked(skillId);
            }
        }

        internal static string GetSelectedPerkId(
            string skillId,
            int requiredLevel)
        {
            EnsureLoaded();
            lock (Gate)
            {
                return _state.SelectedPerks.FirstOrDefault(choice =>
                    choice != null &&
                    choice.RequiredLevel == requiredLevel &&
                    string.Equals(
                        choice.SkillId,
                        skillId,
                        StringComparison.OrdinalIgnoreCase))?.PerkId ??
                    string.Empty;
            }
        }

        internal static bool HasSelectedPerk(string perkId)
        {
            EnsureLoaded();
            lock (Gate)
            {
                return !string.IsNullOrWhiteSpace(perkId) &&
                    _state.CreationCompleted &&
                    _state.SelectedPerks.Any(choice => choice != null &&
                        string.Equals(
                            choice.PerkId,
                            perkId,
                            StringComparison.Ordinal));
            }
        }

        internal static bool TrySpendFocus(string skillId)
        {
            EnsureLoaded();
            lock (Gate)
            {
                if (!_state.CreationCompleted || _state.FocusPoints <= 0 ||
                    GetValue(_state.Focus, skillId, 0) >= MaximumFocusPerSkill)
                {
                    return false;
                }
                SetValue(_state.Focus, skillId, GetValue(_state.Focus, skillId, 0) + 1);
                _state.FocusPoints--;
                SaveLocked();
                return true;
            }
        }

        internal static bool TrySpendAttribute(string attributeId)
        {
            EnsureLoaded();
            lock (Gate)
            {
                if (!_state.CreationCompleted || _state.AttributePoints <= 0 ||
                    !AttributeIds.Contains(attributeId, StringComparer.OrdinalIgnoreCase) ||
                    GetValue(_state.Attributes, attributeId, 2) >= MaximumAttribute)
                {
                    return false;
                }
                SetValue(_state.Attributes, attributeId,
                    GetValue(_state.Attributes, attributeId, 2) + 1);
                _state.AttributePoints--;
                SaveLocked();
                return true;
            }
        }

        internal static bool TryApplyDevelopmentPlan(
            IReadOnlyDictionary<string, int> focusIncreases,
            IReadOnlyDictionary<string, int> attributeIncreases,
            IReadOnlyDictionary<string, string> perkChoices)
        {
            EnsureLoaded();
            lock (Gate)
            {
                if (!_state.CreationCompleted || focusIncreases == null ||
                    attributeIncreases == null || perkChoices == null)
                {
                    return false;
                }

                var validatedFocus = new Dictionary<string, int>(
                    StringComparer.Ordinal);
                var validatedAttributes = new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);
                var validatedPerks = new List<DikozemieHeroPerkChoice>();
                long focusCost = 0;
                long attributeCost = 0;

                foreach (KeyValuePair<string, int> increase in focusIncreases)
                {
                    string skillId = AllSkillIds.FirstOrDefault(candidate =>
                        string.Equals(
                            candidate,
                            increase.Key,
                            StringComparison.OrdinalIgnoreCase));
                    if (skillId == null || increase.Value <= 0 ||
                        validatedFocus.ContainsKey(skillId) ||
                        GetValue(_state.Focus, skillId, 0) +
                            (long)increase.Value > MaximumFocusPerSkill)
                    {
                        return false;
                    }
                    validatedFocus.Add(skillId, increase.Value);
                    focusCost += increase.Value;
                }

                foreach (KeyValuePair<string, int> increase in attributeIncreases)
                {
                    string attributeId = AttributeIds.FirstOrDefault(candidate =>
                        string.Equals(
                            candidate,
                            increase.Key,
                            StringComparison.OrdinalIgnoreCase));
                    if (attributeId == null || increase.Value <= 0 ||
                        validatedAttributes.ContainsKey(attributeId) ||
                        GetValue(_state.Attributes, attributeId, 2) +
                            (long)increase.Value > MaximumAttribute)
                    {
                        return false;
                    }
                    validatedAttributes.Add(attributeId, increase.Value);
                    attributeCost += increase.Value;
                }

                foreach (KeyValuePair<string, string> choice in perkChoices)
                {
                    int separator = choice.Key?.LastIndexOf(':') ?? -1;
                    if (separator <= 0 ||
                        !int.TryParse(
                            choice.Key.Substring(separator + 1),
                            out int requiredLevel))
                    {
                        return false;
                    }
                    string requestedSkillId = choice.Key.Substring(0, separator);
                    string skillId = AllSkillIds.FirstOrDefault(candidate =>
                        string.Equals(
                            candidate,
                            requestedSkillId,
                            StringComparison.OrdinalIgnoreCase));
                    if (skillId == null ||
                        GetValue(_state.Skills, skillId, 0) < requiredLevel ||
                        !DikozemieNativePerkCatalog.IsPerkChoice(
                            skillId,
                            requiredLevel,
                            choice.Value))
                    {
                        return false;
                    }

                    DikozemieHeroPerkChoice committed =
                        _state.SelectedPerks.FirstOrDefault(existing =>
                            existing != null &&
                            existing.RequiredLevel == requiredLevel &&
                            string.Equals(
                                existing.SkillId,
                                skillId,
                                StringComparison.OrdinalIgnoreCase));
                    if (committed != null && !string.Equals(
                        committed.PerkId,
                        choice.Value,
                        StringComparison.Ordinal))
                    {
                        // Like the single-player developer, a confirmed perk
                        // is permanent until a dedicated respec mechanic exists.
                        return false;
                    }
                    if (committed == null)
                    {
                        validatedPerks.Add(new DikozemieHeroPerkChoice
                        {
                            SkillId = skillId,
                            RequiredLevel = requiredLevel,
                            PerkId = choice.Value
                        });
                    }
                }

                foreach (DikozemieHeroPerkChoice choice in validatedPerks)
                {
                    int previousLevel =
                        DikozemieNativePerkCatalog.GetPreviousRequiredLevel(
                            choice.SkillId,
                            choice.RequiredLevel);
                    if (previousLevel > 0 && !HasSelectedPerkAtLevel(
                        choice.SkillId,
                        previousLevel,
                        validatedPerks))
                    {
                        // Native CharacterDeveloper does not allow skipping a
                        // perk tier even when the raw skill level is high enough.
                        return false;
                    }
                }

                if ((focusCost + attributeCost <= 0 && validatedPerks.Count == 0) ||
                    focusCost > _state.FocusPoints ||
                    attributeCost > _state.AttributePoints)
                {
                    return false;
                }

                foreach (KeyValuePair<string, int> increase in validatedFocus)
                {
                    SetValue(
                        _state.Focus,
                        increase.Key,
                        GetValue(_state.Focus, increase.Key, 0) + increase.Value);
                }
                foreach (KeyValuePair<string, int> increase in validatedAttributes)
                {
                    SetValue(
                        _state.Attributes,
                        increase.Key,
                        GetValue(_state.Attributes, increase.Key, 2) + increase.Value);
                }
                _state.FocusPoints -= (int)focusCost;
                _state.AttributePoints -= (int)attributeCost;
                _state.SelectedPerks.AddRange(validatedPerks);
                SaveLocked();
                return true;
            }
        }

        private static bool HasSelectedPerkAtLevel(
            string skillId,
            int requiredLevel,
            IEnumerable<DikozemieHeroPerkChoice> pendingChoices)
        {
            return _state.SelectedPerks.Any(choice =>
                    choice != null &&
                    choice.RequiredLevel == requiredLevel &&
                    string.Equals(
                        choice.SkillId,
                        skillId,
                        StringComparison.OrdinalIgnoreCase)) ||
                pendingChoices.Any(choice =>
                    choice != null &&
                    choice.RequiredLevel == requiredLevel &&
                    string.Equals(
                        choice.SkillId,
                        skillId,
                        StringComparison.OrdinalIgnoreCase));
        }

        internal static bool TryEquipOwnedItem(string itemId)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }
            lock (Gate)
            {
                if (!_state.CreationCompleted ||
                    !_state.OwnedItems.Contains(itemId, StringComparer.Ordinal))
                {
                    return false;
                }

                ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
                if (item == null)
                {
                    return false;
                }

                int targetSlot = -1;
                // Fill a compatible empty slot first. This matters for the
                // four weapon slots: equipping a second sword must not
                // silently replace the first one while slots 1-3 are free.
                for (int slot = 0; slot < 12; slot++)
                {
                    bool occupied = _state.Equipment.Any(saved => saved.Slot == slot);
                    if (!occupied && Equipment.IsItemFitsToSlot((EquipmentIndex)slot, item))
                    {
                        targetSlot = slot;
                        break;
                    }
                }
                if (targetSlot < 0)
                {
                    foreach (DikozemieHeroItem saved in _state.Equipment)
                    {
                        ItemObject equipped = MBObjectManager.Instance
                            .GetObject<ItemObject>(saved.ItemId);
                        if (equipped != null &&
                            equipped.ItemType == item.ItemType &&
                            Equipment.IsItemFitsToSlot((EquipmentIndex)saved.Slot, item))
                        {
                            targetSlot = saved.Slot;
                            break;
                        }
                    }
                }
                if (targetSlot < 0)
                {
                    for (int slot = 0; slot < 12; slot++)
                    {
                        if (Equipment.IsItemFitsToSlot((EquipmentIndex)slot, item))
                        {
                            targetSlot = slot;
                            break;
                        }
                    }
                }
                if (targetSlot < 0)
                {
                    return false;
                }

                _state.Equipment.RemoveAll(saved => saved.Slot == targetSlot);
                _state.Equipment.Add(new DikozemieHeroItem
                {
                    Slot = targetSlot,
                    ItemId = itemId,
                    ModifierId = string.Empty
                });
                SynchronizeMountedStateLocked();
                SaveLocked();
                return true;
            }
        }

        internal static bool TryEquipOwnedItemAtSlot(string itemId, int targetSlot)
        {
            return TryEquipOwnedItemAtSlot(
                itemId,
                targetSlot,
                out string displacedItemId);
        }

        internal static bool TryEquipOwnedItemAtSlot(
            string itemId,
            int targetSlot,
            out string displacedItemId)
        {
            displacedItemId = string.Empty;
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(itemId) ||
                targetSlot < 0 || targetSlot >= 12)
            {
                return false;
            }

            lock (Gate)
            {
                if (!_state.CreationCompleted ||
                    !_state.OwnedItems.Contains(itemId, StringComparer.Ordinal))
                {
                    return false;
                }

                ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
                if (item == null ||
                    !Equipment.IsItemFitsToSlot((EquipmentIndex)targetSlot, item))
                {
                    return false;
                }

                int ownedCount = _state.OwnedItems.Count(owned =>
                    string.Equals(owned, itemId, StringComparison.Ordinal));
                int equippedElsewhereCount = _state.Equipment.Count(saved =>
                    saved != null &&
                    saved.Slot != targetSlot &&
                    string.Equals(saved.ItemId, itemId, StringComparison.Ordinal));
                if (ownedCount <= equippedElsewhereCount)
                {
                    return false;
                }

                DikozemieHeroItem displaced = _state.Equipment.FirstOrDefault(saved =>
                    saved != null && saved.Slot == targetSlot);
                displacedItemId = displaced?.ItemId ?? string.Empty;

                _state.Equipment.RemoveAll(saved =>
                    saved != null && saved.Slot == targetSlot);
                _state.Equipment.Add(new DikozemieHeroItem
                {
                    Slot = targetSlot,
                    ItemId = itemId,
                    ModifierId = string.Empty
                });
                SynchronizeMountedStateLocked();
                SaveLocked();
                return true;
            }
        }

        internal static bool TryMoveEquippedItem(
            int sourceSlot,
            int targetSlot,
            out string movedItemId,
            out string displacedItemId,
            out bool swapped)
        {
            movedItemId = string.Empty;
            displacedItemId = string.Empty;
            swapped = false;
            EnsureLoaded();
            if (sourceSlot < 0 || sourceSlot >= 12 ||
                targetSlot < 0 || targetSlot >= 12)
            {
                return false;
            }

            lock (Gate)
            {
                if (!_state.CreationCompleted)
                {
                    return false;
                }

                DikozemieHeroItem source = _state.Equipment.FirstOrDefault(saved =>
                    saved != null && saved.Slot == sourceSlot);
                if (source == null)
                {
                    return false;
                }

                movedItemId = source.ItemId ?? string.Empty;
                ItemObject movedItem = MBObjectManager.Instance
                    .GetObject<ItemObject>(movedItemId);
                if (movedItem == null ||
                    !Equipment.IsItemFitsToSlot((EquipmentIndex)targetSlot, movedItem))
                {
                    return false;
                }
                if (sourceSlot == targetSlot)
                {
                    return true;
                }

                DikozemieHeroItem target = _state.Equipment.FirstOrDefault(saved =>
                    saved != null && saved.Slot == targetSlot);
                displacedItemId = target?.ItemId ?? string.Empty;
                if (target != null)
                {
                    ItemObject displacedItem = MBObjectManager.Instance
                        .GetObject<ItemObject>(target.ItemId);
                    swapped = displacedItem != null &&
                        Equipment.IsItemFitsToSlot(
                            (EquipmentIndex)sourceSlot,
                            displacedItem);
                }

                _state.Equipment.RemoveAll(saved => saved != null &&
                    (saved.Slot == sourceSlot || saved.Slot == targetSlot));
                _state.Equipment.Add(new DikozemieHeroItem
                {
                    Slot = targetSlot,
                    ItemId = movedItemId,
                    ModifierId = source.ModifierId ?? string.Empty
                });
                if (swapped)
                {
                    _state.Equipment.Add(new DikozemieHeroItem
                    {
                        Slot = sourceSlot,
                        ItemId = displacedItemId,
                        ModifierId = target.ModifierId ?? string.Empty
                    });
                }
                SynchronizeMountedStateLocked();
                SaveLocked();
                return true;
            }
        }

        internal static bool TryUnequipItemAtSlot(int slot)
        {
            EnsureLoaded();
            lock (Gate)
            {
                if (!_state.CreationCompleted || slot < 0 || slot >= 12)
                {
                    return false;
                }

                int removed = _state.Equipment.RemoveAll(saved =>
                    saved != null && saved.Slot == slot);
                if (removed <= 0)
                {
                    return false;
                }

                SynchronizeMountedStateLocked();
                SaveLocked();
                return true;
            }
        }

        internal static List<string> OwnedItemsSnapshot()
        {
            EnsureLoaded();
            lock (Gate)
            {
                return _state?.OwnedItems?.ToList() ?? new List<string>();
            }
        }

        internal static bool OwnsItem(string itemId)
        {
            EnsureLoaded();
            lock (Gate)
            {
                return !string.IsNullOrWhiteSpace(itemId) &&
                    _state.OwnedItems.Contains(itemId, StringComparer.Ordinal);
            }
        }

        internal static bool TryAddOwnedItem(string itemId)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            lock (Gate)
            {
                if (!_state.CreationCompleted ||
                    MBObjectManager.Instance.GetObject<ItemObject>(itemId) == null)
                {
                    return false;
                }

                // The inventory is a stack list. Repeated ids are intentional:
                // ammunition, food and other merchandise can be bought more
                // than once while old one-item profiles remain compatible.
                _state.OwnedItems.Add(itemId);
                SaveLocked();
                return true;
            }
        }

        internal static bool TryRemoveOwnedItem(string itemId)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            lock (Gate)
            {
                if (!_state.CreationCompleted ||
                    !_state.OwnedItems.Remove(itemId))
                {
                    return false;
                }

                // Selling one copy keeps the equipped copy valid. Selling the
                // last copy clears it from every slot so the next arena spawn
                // cannot receive equipment the profile no longer owns.
                if (!_state.OwnedItems.Contains(itemId, StringComparer.Ordinal))
                {
                    _state.Equipment.RemoveAll(saved =>
                        saved != null && string.Equals(
                            saved.ItemId,
                            itemId,
                            StringComparison.Ordinal));
                }
                SynchronizeMountedStateLocked();
                SaveLocked();
                return true;
            }
        }

        internal static string RecordBattleResult(bool victory, int defeatedOpponents = 1)
        {
            return RecordBattleProgress(victory, defeatedOpponents).Summary;
        }

        internal static DikozemieHeroBattleProgress RecordBattleProgress(
            bool victory,
            int defeatedOpponents = 1)
        {
            EnsureLoaded();
            lock (Gate)
            {
                var rawExperience = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (string skillId in GetBattleTrainingSkillsLocked())
                {
                    int baseExperience = victory ? 30 : 12;
                    rawExperience[skillId] = baseExperience;
                }
                int heroExperience = rawExperience.Values.Sum() +
                    Math.Max(0, defeatedOpponents - 1) * 10;
                string summary = ApplyArenaProgressLocked(
                    victory,
                    heroExperience,
                    rawExperience);
                return new DikozemieHeroBattleProgress(
                    heroExperience,
                    summary);
            }
        }

        internal static string ApplyArenaProgress(
            bool victory,
            int heroExperience,
            string skillExperiencePayload)
        {
            EnsureLoaded();
            lock (Gate)
            {
                var rawExperience = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (string encoded in (skillExperiencePayload ?? string.Empty)
                             .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] parts = encoded.Split('=');
                    if (parts.Length != 2 ||
                        !DikozemieArenaCombatProgression.IsCombatSkill(parts[0]) ||
                        !int.TryParse(parts[1], out int amount) || amount <= 0)
                    {
                        continue;
                    }
                    rawExperience[parts[0]] = Math.Min(5000, amount);
                }
                return ApplyArenaProgressLocked(
                    victory,
                    Math.Max(0, Math.Min(50000, heroExperience)),
                    rawExperience);
            }
        }

        internal static int ExperienceForNextLevel(int level)
        {
            int safeLevel = Math.Max(1, level);
            long gain = 1000;
            for (int currentLevel = 1; currentLevel < safeLevel; currentLevel++)
            {
                gain += 1000 + gain / 5;
                if (gain >= int.MaxValue)
                {
                    return int.MaxValue;
                }
            }
            return (int)gain;
        }

        internal static BasicCharacterObject ResolveCharacter(BasicCharacterObject fallback)
        {
            EnsureLoaded();
            string id;
            lock (Gate)
            {
                id = _state.CreationCompleted ? _state.StartingCharacterId : string.Empty;
            }
            return string.IsNullOrWhiteSpace(id)
                ? fallback
                : MBObjectManager.Instance.GetObject<BasicCharacterObject>(id) ?? fallback;
        }

        // What the hero takes to battle. A horse without fodder for the day
        // stays in the stable (DikozemieHorseUpkeep).
        internal static Equipment CreateBattleEquipment(BasicCharacterObject character)
        {
            Equipment equipment = CreateDisplayEquipment(character);
            if (equipment != null && DikozemieHorseUpkeep.IsHungry &&
                equipment[EquipmentIndex.Horse].Item != null)
            {
                equipment = new Equipment(equipment);
                equipment[EquipmentIndex.Horse] = default(EquipmentElement);
                equipment[EquipmentIndex.HorseHarness] = default(EquipmentElement);
            }
            return equipment;
        }

        // What the hero wears on the hero screens, the horse included.
        internal static Equipment CreateDisplayEquipment(BasicCharacterObject character)
        {
            EnsureLoaded();
            Equipment fallback = character?.FirstBattleEquipment ?? character?.Equipment;
            lock (Gate)
            {
                if (!_state.CreationCompleted || _state.Equipment.Count == 0)
                {
                    return fallback;
                }

                var equipment = new Equipment(Equipment.EquipmentType.Battle);
                foreach (DikozemieHeroItem saved in _state.Equipment)
                {
                    if (saved == null || saved.Slot < 0 || saved.Slot >= 12)
                    {
                        continue;
                    }
                    ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(saved.ItemId);
                    if (item == null || !Equipment.IsItemFitsToSlot((EquipmentIndex)saved.Slot, item))
                    {
                        continue;
                    }
                    ItemModifier modifier = string.IsNullOrWhiteSpace(saved.ModifierId)
                        ? null
                        : MBObjectManager.Instance.GetObject<ItemModifier>(saved.ModifierId);
                    equipment[saved.Slot] = new EquipmentElement(item, modifier);
                }
                // The inventory/equipment screen is the source of truth. A
                // stale stance flag used to erase slots 10/11 here, so the
                // server never received a horse that visibly appeared equipped.
                if (equipment[EquipmentIndex.Horse].Item == null)
                {
                    equipment[EquipmentIndex.HorseHarness] =
                        default(EquipmentElement);
                }
                return equipment;
            }
        }

        internal static BasicCharacterObject ResolveCreationPreviewCharacter(
            BasicCharacterObject fallback)
        {
            EnsureLoaded();
            lock (Gate)
            {
                string id = GetDominantCharacterIdLocked();
                return MBObjectManager.Instance.GetObject<BasicCharacterObject>(id) ?? fallback;
            }
        }

        internal static BasicCharacterObject ResolveCreationChoicePreviewCharacter(
            int step,
            int choiceIndex,
            BasicCharacterObject fallback)
        {
            EnsureLoaded();
            lock (Gate)
            {
                int safeStep = Math.Max(0, Math.Min(CreationStepCount - 1, step));
                int selectedIndex = choiceIndex;
                IReadOnlyList<DikozemieHeroBackgroundChoice> choices =
                    DikozemieNativeCreationCatalog.GetChoices(
                        safeStep,
                        _state.CultureId,
                        ResolveParentOccupationLocked());
                if (selectedIndex < 0 || selectedIndex >= choices.Count)
                {
                    selectedIndex = GetSelectedCreationChoiceIndex(safeStep);
                }
                if (selectedIndex < 0 || selectedIndex >= choices.Count)
                {
                    selectedIndex = 0;
                }
                return MBObjectManager.Instance.GetObject<BasicCharacterObject>(
                    HeroTemplateCharacterId) ?? fallback;
            }
        }

        internal static AgentBuildData ApplyToPlayerBuild(
            AgentBuildData buildData,
            BasicCharacterObject character)
        {
            EnsureLoaded();
            buildData.Equipment(CreateBattleEquipment(character));
            lock (Gate)
            {
                if (BodyProperties.FromString(_state.BodyProperties, out BodyProperties body))
                {
                    buildData.BodyProperties(body).IsFemale(_state.IsFemale);
                }
            }
            return buildData;
        }

        internal static void ApplyHealthBonus(Agent agent)
        {
            if (agent == null || !IsCreated)
            {
                return;
            }
            float multiplier = 1f + Math.Max(0, GetAttribute("endurance") - 2) * 0.04f +
                Math.Max(0, Level - 1) * 0.005f;
            agent.HealthLimit = Math.Max(1f, agent.HealthLimit * multiplier);
            agent.Health = agent.HealthLimit;
        }

        internal static string BuildInventoryText()
        {
            EnsureLoaded();
            lock (Gate)
            {
                if (_state.Equipment.Count == 0)
                {
                    return "Снаряжение появится после завершения предыстории.";
                }
                var names = new List<string>();
                foreach (DikozemieHeroItem saved in _state.Equipment)
                {
                    ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(saved.ItemId);
                    if (item != null)
                    {
                        names.Add("• " + item.Name);
                    }
                }
                return names.Count == 0 ? "Снаряжение загружается…" : string.Join("\n", names);
            }
        }

        internal static string BuildSkillsText()
        {
            return "Одноручное: " + GetEffectiveSkill("OneHanded", 0) +
                "    Двуручное: " + GetEffectiveSkill("TwoHanded", 0) +
                "\nДревковое: " + GetEffectiveSkill("Polearm", 0) +
                "    Лук: " + GetEffectiveSkill("Bow", 0) +
                "\nМетательное: " + GetEffectiveSkill("Throwing", 0) +
                "    Верховая езда: " + GetEffectiveSkill("Riding", 0) +
                "\nАтлетика: " + GetEffectiveSkill("Athletics", 0) +
                "    Тактика: " + GetEffectiveSkill("Tactics", 0);
        }

        internal static DikozemieArenaCharacterProfile CreateNetworkProfile(
            string platformPlayerId,
            string displayName,
            string fallbackBodyProperties,
            bool fallbackIsFemale,
            int fallbackRace)
        {
            EnsureLoaded();
            lock (Gate)
            {
                if (!_state.CreationCompleted ||
                    string.IsNullOrWhiteSpace(_state.StartingCharacterId) ||
                    !IsAdultBodyProperties(_state.BodyProperties))
                {
                    return null;
                }
                BasicCharacterObject character = MBObjectManager.Instance
                    .GetObject<BasicCharacterObject>(_state.StartingCharacterId);
                if (character == null)
                {
                    return null;
                }
                Equipment battleEquipment = CreateBattleEquipment(character);
                return new DikozemieArenaCharacterProfile
                {
                    SchemaVersion = 1,
                    CharacterId = _state.HeroId,
                    ProgressionResetToken = _state.ProgressionResetToken ?? string.Empty,
                    PlatformPlayerId = platformPlayerId ?? string.Empty,
                    EconomyKeyHash = DikozemieEconomy.KeyHash,
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? _state.Name : displayName,
                    CultureId = string.IsNullOrWhiteSpace(_state.CultureId)
                        ? "empire"
                        : _state.CultureId,
                    BodyProperties = string.IsNullOrWhiteSpace(_state.BodyProperties)
                        ? fallbackBodyProperties ?? string.Empty
                        : _state.BodyProperties,
                    IsFemale = string.IsNullOrWhiteSpace(_state.BodyProperties)
                        ? fallbackIsFemale
                        : _state.IsFemale,
                    Race = string.IsNullOrWhiteSpace(_state.BodyProperties) ? fallbackRace : _state.Race,
                    Level = _state.Level,
                    Rating = Math.Max(0, DikozemieArmoryProgression.PlayerRating),
                    // A battle of the shared world (stage 5) names who must
                    // fight in it; one battle, not a series.
                    DuelOfferId = DikozemieWorldBattle.IsPending
                        ? DikozemieWorldBattle.DuelOfferId
                        : DikozemieDuelOfferController.ActiveOfferId,
                    DuelOpponentId = DikozemieWorldBattle.IsPending
                        ? DikozemieWorldBattle.DuelOpponentId
                        : DikozemieDuelOfferController.ActiveOpponentId,
                    BattleGroupId = DikozemieWorldBattle.BattleGroupId,
                    BattleGroupSize = DikozemieWorldBattle.BattleGroupSize,
                    // Stage 6: a siege's unequal sides.
                    BattleSideSize = DikozemieWorldBattle.SideSize,
                    BattleSideRating = DikozemieWorldBattle.SideRating,
                    BattleFoeSize = DikozemieWorldBattle.FoeSize,
                    BattleFoeRating = DikozemieWorldBattle.FoeRating,
                    RequestedBattleCount = DikozemieWorldBattle.IsPending
                        ? 1
                        : DikozemieArenaSeriesSelection.BattleCount,
                    PartyLeaderId = DikozemieWorldBattle.IsPending
                        ? DikozemieWorldBattle.PartyLeaderId
                        : NetworkMain.GameClient?.PlayersInParty
                            ?.Find(player => player.IsPartyLeader)
                            ?.PlayerId.ToString() ?? string.Empty,
                    RequestedTeamSize = DikozemieArenaMatchSelection.TeamSize,
                    RequestsImmediateBattle = DikozemieWorldBattle.IsPending
                        ? DikozemieWorldBattle.RequestsImmediate
                        : DikozemieLobbyShellIntegration.IsRoadBattlePending,
                    RequestedDiscipline = (int)DikozemieArenaMatchSelection.Discipline,
                    RequestedComposition =
                        (int)DikozemieArenaMatchSelection.Composition,
                    RequestedNavalHullId = DikozemieArenaMatchSelection.Venue ==
                        DikozemieBattleVenue.Naval
                            ? DikozemieArenaMatchSelection.SelectedNavalBattle.HullId
                            : string.Empty,
                    StartingCharacterId = _state.StartingCharacterId,
                    // The observer of the watch comes to look, not to fight.
                    Observer = DikozemieWatch.IsWatching,
                    Attributes = AttributeIds.Select(attribute =>
                        new DikozemieArenaDevelopmentValue
                        {
                            Id = attribute,
                            Value = GetValue(_state.Attributes, attribute, 2)
                        }).ToArray(),
                    Focus = AllSkillIds.Select(skill =>
                        new DikozemieArenaDevelopmentValue
                        {
                            Id = skill,
                            Value = GetValue(_state.Focus, skill, 0)
                        }).ToArray(),
                    Skills = AllSkillIds.Select(skill => new DikozemieArenaSkillValue
                    {
                        SkillId = skill,
                        Value = GetEffectiveSkill(skill, 0)
                    }).ToArray(),
                    SkillExperience = AllSkillIds.Select(skill =>
                        new DikozemieArenaSkillValue
                        {
                            SkillId = skill,
                            Value = GetValue(_state.SkillExperience, skill, 0)
                        }).ToArray(),
                    SelectedPerks = _state.SelectedPerks.Select(
                        choice => choice.PerkId).ToArray(),
                    Equipment = SerializeEquipment(battleEquipment)
                };
            }
        }

        private static DikozemieArenaEquipmentValue[] SerializeEquipment(Equipment equipment)
        {
            if (equipment == null)
            {
                return Array.Empty<DikozemieArenaEquipmentValue>();
            }
            var values = new List<DikozemieArenaEquipmentValue>();
            for (int slot = 0; slot < 12; slot++)
            {
                EquipmentElement element = equipment[slot];
                if (element.Item == null)
                {
                    continue;
                }
                values.Add(new DikozemieArenaEquipmentValue
                {
                    Slot = slot,
                    ItemId = element.Item.StringId,
                    ModifierId = element.ItemModifier?.StringId ?? string.Empty
                });
            }
            return values.ToArray();
        }

        private static bool HasEquippedHorse(
            IEnumerable<DikozemieHeroItem> equipment)
        {
            return equipment?.Any(saved =>
                saved != null &&
                saved.Slot == (int)EquipmentIndex.Horse &&
                !string.IsNullOrWhiteSpace(saved.ItemId)) == true;
        }

        private static void SynchronizeMountedStateLocked()
        {
            _state.BattleMounted = HasEquippedHorse(_state.Equipment);
        }

        private static void CompleteCreationLocked()
        {
            _state.Level = 1;
            _state.Experience = 0;
            _state.AttributePoints = 0;
            _state.FocusPoints = 0;
            _state.CreationRenown = 0;
            _state.Traits.Clear();
            foreach (string attribute in AttributeIds)
            {
                SetValue(_state.Attributes, attribute, 2);
            }
            foreach (string skill in AllSkillIds)
            {
                SetValue(_state.Focus, skill, 0);
                SetValue(_state.Skills, skill, 0);
                SetValue(_state.SkillExperience, skill, 0);
            }

            foreach (DikozemieHeroBackgroundChoice selected in
                     GetSelectedCreationChoicesLocked())
            {
                if (!string.IsNullOrWhiteSpace(selected.Attribute))
                {
                    SetValue(_state.Attributes, selected.Attribute,
                        GetValue(_state.Attributes, selected.Attribute, 2) + 1);
                }
                foreach (string skill in selected.Skills)
                {
                    SetValue(_state.Focus, skill,
                        Math.Min(MaximumFocusPerSkill,
                            GetValue(_state.Focus, skill, 0) + 1));
                    SetValue(_state.Skills, skill,
                        Math.Min(DikozemieArenaCombatProgression.MaximumSkillLevel,
                            GetValue(_state.Skills, skill, 0) + 10));
                }
                _state.FocusPoints += selected.UnspentFocusPoints;
                _state.AttributePoints += selected.UnspentAttributePoints;
                _state.CreationRenown += selected.Renown;
                foreach (KeyValuePair<string, int> trait in selected.Traits)
                {
                    SetValue(_state.Traits, trait.Key,
                        GetValue(_state.Traits, trait.Key, 0) + trait.Value);
                }
            }

            if (_state.FocusPoints == 0 && _state.AttributePoints == 0)
            {
                _state.FocusPoints = StartingFocusPoints;
                _state.AttributePoints = StartingAttributePoints;
            }

            DikozemieHeroBackgroundChoice ageChoice =
                _state.BackstoryChoices.Select(DikozemieNativeCreationCatalog.FindChoice)
                    .FirstOrDefault(choice => choice != null && choice.PreviewAge >= 20 &&
                        choice.UnspentFocusPoints > 0);
            _state.StartingAge = ageChoice?.PreviewAge ?? 20;
            if (BodyProperties.FromString(_state.BodyProperties, out BodyProperties body))
            {
                body = MBBodyProperties.GetBodyPropertiesWithAge(
                    ref body,
                    _state.StartingAge);
                _state.BodyProperties = body.ToString();
            }

            _state.StartingCharacterId = HeroTemplateCharacterId;
            _state.ArchetypeName = "Уникальный герой";

            _state.CreationCompleted = true;
            _state.CreationStep = CreationStepCount;
            BuildStarterEquipmentLocked();
        }

        private static string GetDominantCharacterIdLocked()
        {
            return HeroTemplateCharacterId;
        }

        private static string ResolveCultureCharacterId(string cultureId, string track)
        {
            string culture = NormalizeCultureId(cultureId);
            if (culture == "nords")
            {
                culture = "sturgia";
            }
            switch (track)
            {
                case "archer":
                    return "mp_heavy_ranged_" + culture + "_troop";
                case "rider":
                    return "mp_light_cavalry_" + culture + "_troop";
                case "skirmish":
                    if (culture == "empire" || culture == "aserai" ||
                        culture == "sturgia" || culture == "battania")
                    {
                        return "mp_skirmisher_" + culture + "_troop";
                    }
                    return "mp_light_infantry_" + culture + "_troop";
                case "shock":
                    if (culture == "khuzait")
                    {
                        return "mp_heavy_infantry_khuzait_troop";
                    }
                    return "mp_shock_infantry_" + culture + "_troop";
                default:
                    if (culture == "aserai")
                    {
                        return "mp_light_infantry_aserai_troop";
                    }
                    return "mp_heavy_infantry_" + culture + "_troop";
            }
        }

        private static string NormalizeCultureId(string cultureId)
        {
            switch ((cultureId ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "vlandia": return "vlandia";
                case "sturgia": return "sturgia";
                case "aserai": return "aserai";
                case "khuzait": return "khuzait";
                case "battania": return "battania";
                case "nords": return "nords";
                case "empire": return "empire";
                default: return string.Empty;
            }
        }

        private static string GetDominantTrack(Dictionary<string, int> scores)
        {
            return scores.OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key)
                .FirstOrDefault() ?? "guard";
        }

        private static IEnumerable<DikozemieHeroBackgroundChoice>
            GetSelectedCreationChoicesLocked()
        {
            int count = Math.Min(CreationStepCount, _state.BackstoryChoices.Count);
            for (int step = 0; step < count; step++)
            {
                string selectedId = _state.BackstoryChoices[step];
                DikozemieHeroBackgroundChoice selected =
                    DikozemieNativeCreationCatalog.FindChoice(selectedId);
                if (selected != null)
                {
                    yield return selected;
                }
            }
        }

        private static void BuildStarterEquipmentLocked()
        {
            _state.Equipment.Clear();
            _state.OwnedItems.Clear();

            // Single-player does not synthesize a generic multiplayer loadout.
            // Its age page copies the exact culture/title/gender creation
            // roster selected during youth. Keep that same roster as both the
            // equipped set and the initial owned inventory.
            string rosterId =
                DikozemieNativeCreationCatalog.ResolvePreviewEquipmentId(
                    5,
                    _state.IsFemale,
                    _state.CultureId,
                    ResolveParentOccupationLocked(),
                    _state.SelectedTitleType,
                    null);
            MBEquipmentRoster roster = MBObjectManager.Instance
                .GetObject<MBEquipmentRoster>(rosterId);
            Equipment nativeEquipment = roster?.DefaultEquipment ??
                roster?.AllEquipments?.FirstOrDefault();
            nativeEquipment = nativeEquipment ??
                ResolveCreationEquipmentFromCatalog(rosterId);
            if (nativeEquipment == null)
            {
                MBEquipmentRoster defaultRoster = MBObjectManager.Instance
                    .GetObject<MBEquipmentRoster>(
                        "player_char_creation_default");
                nativeEquipment = defaultRoster?.DefaultEquipment ??
                    defaultRoster?.AllEquipments?.FirstOrDefault();
            }
            if (nativeEquipment != null)
            {
                for (int slot = 0; slot < 12; slot++)
                {
                    // Every newcomer starts on foot; a horse is bought with
                    // coins (customer 2026-11-05).
                    if (slot == (int)EquipmentIndex.Horse ||
                        slot == (int)EquipmentIndex.HorseHarness)
                    {
                        continue;
                    }
                    EquipmentElement element = nativeEquipment[slot];
                    ItemObject item = element.Item;
                    if (item == null ||
                        !Equipment.IsItemFitsToSlot((EquipmentIndex)slot, item))
                    {
                        continue;
                    }
                    _state.Equipment.Add(new DikozemieHeroItem
                    {
                        Slot = slot,
                        ItemId = item.StringId,
                        ModifierId = element.ItemModifier?.StringId ?? string.Empty
                    });
                    if (!_state.OwnedItems.Contains(
                            item.StringId,
                            StringComparer.Ordinal))
                    {
                        _state.OwnedItems.Add(item.StringId);
                    }
                }
            }
            EnsureStarterWeaponsLocked();
            if (!_state.OwnedItems.Contains(
                    DikozemieItemStore.GrainItemId,
                    StringComparer.Ordinal))
            {
                _state.OwnedItems.Add(DikozemieItemStore.GrainItemId);
            }
            SynchronizeMountedStateLocked();
            // A new hero: the treasury's list of his things starts over too.
            DikozemieEconomy.ReportItemsReset(_state.OwnedItems);
        }

        private static void EnsureStarterWeaponsLocked()
        {
            if (HasStarterCombatWeaponLocked())
            {
                return;
            }

            string[] combatSkills =
            {
                "Bow", "Crossbow", "Throwing", "Polearm", "TwoHanded",
                "OneHanded"
            };
            string skill = combatSkills
                .OrderByDescending(candidate =>
                    GetValue(_state.Focus, candidate, 0))
                .ThenByDescending(candidate =>
                    GetValue(_state.Skills, candidate, 0))
                .First();
            switch (skill)
            {
                case "Bow":
                    TryEquipStarterItemLocked(0, "mp_hunting_bow");
                    TryEquipStarterItemLocked(1, "mp_arrows_barbed");
                    TryEquipStarterItemLocked(2, "mp_vlandian_short_sword");
                    break;
                case "Crossbow":
                    TryEquipStarterItemLocked(0, "mp_light_crossbow");
                    TryEquipStarterItemLocked(1, "mp_bolts_western");
                    TryEquipStarterItemLocked(2, "mp_vlandian_short_sword");
                    break;
                case "Throwing":
                    TryEquipStarterItemLocked(0, "mp_javelin");
                    TryEquipStarterItemLocked(1, "mp_vlandian_short_sword");
                    TryEquipStarterItemLocked(2, "mp_battania_targe_b");
                    break;
                case "Polearm":
                    TryEquipStarterItemLocked(0, "mp_western_short_spear");
                    TryEquipStarterItemLocked(1, "mp_vlandian_short_sword");
                    break;
                case "TwoHanded":
                    TryEquipStarterItemLocked(0, "mp_empire_short_twohandedaxe");
                    break;
                default:
                    TryEquipStarterItemLocked(0, "mp_vlandian_short_sword");
                    TryEquipStarterItemLocked(1, "mp_battania_targe_b");
                    break;
            }

            // Never allow a newly created hero to enter the arena unarmed even
            // if a game update renamed one of the preferred culture items.
            if (!HasStarterCombatWeaponLocked())
            {
                TryEquipStarterItemLocked(0, "mp_battania_cleaver");
            }
        }

        private static bool HasStarterCombatWeaponLocked()
        {
            return _state.Equipment.Any(saved =>
            {
                if (saved == null || saved.Slot < 0 || saved.Slot > 3 ||
                    string.IsNullOrWhiteSpace(saved.ItemId))
                {
                    return false;
                }
                ItemObject item = MBObjectManager.Instance
                    .GetObject<ItemObject>(saved.ItemId);
                if (item == null ||
                    DikozemieItemStore.GetCategory(item) !=
                        DikozemieStoreCategory.Weapons)
                {
                    return false;
                }
                string type = item.ItemType.ToString();
                return type != "Arrows" && type != "Bolts";
            });
        }

        private static bool TryEquipStarterItemLocked(int slot, string itemId)
        {
            if (_state.Equipment.Any(saved => saved != null && saved.Slot == slot))
            {
                return false;
            }
            ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
            if (item == null ||
                !Equipment.IsItemFitsToSlot((EquipmentIndex)slot, item))
            {
                return false;
            }

            _state.Equipment.Add(new DikozemieHeroItem
            {
                Slot = slot,
                ItemId = itemId,
                ModifierId = string.Empty
            });
            if (!_state.OwnedItems.Contains(itemId, StringComparer.Ordinal))
            {
                _state.OwnedItems.Add(itemId);
            }
            return true;
        }

        private static string ResolveParentOccupationLocked()
        {
            if (!string.IsNullOrWhiteSpace(_state.ParentOccupation))
            {
                return _state.ParentOccupation;
            }
            if (_state.BackstoryChoices.Count > 0)
            {
                DikozemieHeroBackgroundChoice parent =
                    DikozemieNativeCreationCatalog.FindChoice(
                        _state.BackstoryChoices[0]);
                if (parent != null)
                {
                    return parent.ParentOccupation;
                }
            }
            return string.Empty;
        }

        private static void MigrateNativeTroopProfileLocked()
        {
            if (!_state.CreationCompleted ||
                _state.BackstoryChoices == null ||
                _state.BackstoryChoices.Count != CreationStepCount)
            {
                return;
            }
            _state.StartingCharacterId = HeroTemplateCharacterId;
            _state.ArchetypeName = "Уникальный герой";
            BuildStarterEquipmentLocked();
        }

        private static bool IsNativeTroopProfile(DikozemieHeroProfileState loaded)
        {
            return loaded != null && loaded.CreationCompleted &&
                loaded.SchemaVersion < 6 &&
                loaded.BackstoryChoices != null &&
                loaded.BackstoryChoices.Count == CreationStepCount;
        }

        private static string GetSkillAttribute(string skillId)
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
                case "Smithing":
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

        private static float GetLearningRateLocked(string skillId)
        {
            int attribute = GetValue(
                _state.Attributes,
                GetSkillAttribute(skillId),
                2);
            int focus = GetValue(_state.Focus, skillId, 0);
            int skill = GetValue(_state.Skills, skillId, 0);
            return DikozemieArenaCombatProgression.GetLearningRate(
                attribute,
                focus,
                skill);
        }

        internal static int GetLearningLimit(string skillId)
        {
            EnsureLoaded();
            lock (Gate)
            {
                int attribute = GetValue(
                    _state.Attributes,
                    GetSkillAttribute(skillId),
                    2);
                int focus = GetValue(_state.Focus, skillId, 0);
                return Math.Max(0, (attribute - 1) * 10 + focus * 30);
            }
        }

        private static void AddSkillExperienceLocked(string skillId, int amount)
        {
            int level = GetValue(_state.Skills, skillId, 0);
            int experience = GetValue(_state.SkillExperience, skillId, 0) +
                Math.Max(0, amount);
            while (level < DikozemieArenaCombatProgression.MaximumSkillLevel &&
                experience >= SkillExperienceForNextLevel(level))
            {
                experience -= SkillExperienceForNextLevel(level);
                level++;
            }
            if (level >= DikozemieArenaCombatProgression.MaximumSkillLevel)
            {
                experience = 0;
            }
            SetValue(_state.Skills, skillId, level);
            SetValue(_state.SkillExperience, skillId, experience);
        }

        private static string ApplyArenaProgressLocked(
            bool victory,
            int heroExperience,
            IReadOnlyDictionary<string, int> rawExperience)
        {
            int gained = Math.Max(0, heroExperience);
            _state.Experience = (int)Math.Min(
                int.MaxValue,
                (long)_state.Experience + gained);
            int oldLevel = _state.Level;
            while (_state.Level < MaximumHeroLevel &&
                _state.Experience >= ExperienceForNextLevel(_state.Level))
            {
                _state.Experience -= ExperienceForNextLevel(_state.Level);
                _state.Level++;
                _state.FocusPoints += FocusPointsPerLevel;
                if (_state.Level % LevelsPerAttributePoint == 0)
                {
                    _state.AttributePoints++;
                }
            }

            var trainedSkills = new List<string>();
            foreach (KeyValuePair<string, int> award in rawExperience)
            {
                int oldSkillLevel = GetValue(_state.Skills, award.Key, 0);
                int learnedExperience = Math.Max(0,
                    (int)Math.Round(award.Value * GetLearningRateLocked(award.Key)));
                AddSkillExperienceLocked(award.Key, learnedExperience);
                int newSkillLevel = GetValue(_state.Skills, award.Key, 0);
                int skillExperience = GetValue(
                    _state.SkillExperience,
                    award.Key,
                    0);
                int requiredExperience = newSkillLevel >=
                    DikozemieArenaCombatProgression.MaximumSkillLevel
                        ? 0
                        : SkillExperienceForNextLevel(newSkillLevel);
                trainedSkills.Add(
                    GetSkillDisplayName(award.Key) + " " + oldSkillLevel +
                    (newSkillLevel > oldSkillLevel
                        ? " → " + newSkillLevel
                        : string.Empty) +
                    ": +" + learnedExperience + " опыта" +
                    (requiredExperience > 0
                        ? " (" + skillExperience + "/" +
                            requiredExperience + ")"
                        : " (максимум)"));
            }
            SaveLocked();
            return "+" + gained + " опыта героя" +
                (_state.Level > oldLevel ? ". Новый уровень: " + _state.Level + "!" : ".") +
                (trainedSkills.Count > 0
                    ? " Навыки: " + string.Join(", ", trainedSkills) + "."
                    : string.Empty);
        }

        private static IEnumerable<string> GetBattleTrainingSkillsLocked()
        {
            var skills = new HashSet<string>(StringComparer.Ordinal);
            foreach (DikozemieHeroItem saved in _state.Equipment)
            {
                if (saved == null || saved.Slot < 0 || saved.Slot > 3)
                {
                    continue;
                }
                ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(saved.ItemId);
                if (item == null)
                {
                    continue;
                }
                switch (item.ItemType)
                {
                    case ItemObject.ItemTypeEnum.OneHandedWeapon:
                        skills.Add("OneHanded");
                        break;
                    case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                        skills.Add("TwoHanded");
                        break;
                    case ItemObject.ItemTypeEnum.Polearm:
                        skills.Add("Polearm");
                        break;
                    case ItemObject.ItemTypeEnum.Bow:
                        skills.Add("Bow");
                        break;
                    case ItemObject.ItemTypeEnum.Crossbow:
                        skills.Add("Crossbow");
                        break;
                    case ItemObject.ItemTypeEnum.Thrown:
                        skills.Add("Throwing");
                        break;
                }
            }
            if (skills.Count == 0)
            {
                skills.Add("OneHanded");
            }
            skills.Add(HasEquippedHorse(_state.Equipment)
                ? "Riding"
                : "Athletics");
            return skills;
        }

        private static string GetSkillDisplayName(string skillId)
        {
            switch (skillId)
            {
                case "OneHanded": return "Одноручное";
                case "TwoHanded": return "Двуручное";
                case "Polearm": return "Древковое";
                case "Bow": return "Лук";
                case "Crossbow": return "Арбалет";
                case "Throwing": return "Метательное";
                case "Riding": return "Верховая езда";
                case "Athletics": return "Атлетика";
                case "Crafting": return "Кузнечное дело";
                case "Scouting": return "Разведка";
                case "Tactics": return "Тактика";
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
                default: return skillId ?? string.Empty;
            }
        }

        private static int GetValue(List<DikozemieHeroValue> values, string id, int fallback)
        {
            DikozemieHeroValue value = values?.FirstOrDefault(item => item != null &&
                string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
            return value == null ? fallback : Math.Max(0, value.Value);
        }

        private static void SetValue(List<DikozemieHeroValue> values, string id, int value)
        {
            DikozemieHeroValue existing = values.FirstOrDefault(item => item != null &&
                string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                values.Add(new DikozemieHeroValue { Id = id, Value = Math.Max(0, value) });
            }
            else
            {
                existing.Value = Math.Max(0, value);
            }
        }

        private static void EnsureLoaded()
        {
            if (_state != null)
            {
                return;
            }
            lock (Gate)
            {
                if (_state != null)
                {
                    return;
                }
                if (!DikozemieArenaStateFiles.TryRead(StatePath, out DikozemieHeroProfileState loaded) ||
                    loaded == null)
                {
                    loaded = new DikozemieHeroProfileState();
                }
                if (loaded.SchemaVersion < 5)
                {
                    // Schema 5 deliberately starts the multiplayer test from a
                    // clean, adult hero created through the full flow.
                    loaded = new DikozemieHeroProfileState();
                }
                int sourceSchemaVersion = loaded.SchemaVersion;
                bool rebuildStarterEquipment = IsNativeTroopProfile(loaded);
                // Items are not loaded yet: the free creation horse is taken
                // back from TickMigrations, and only then the file says 14.
                bool takeBackFreeHorse = sourceSchemaVersion < 14 &&
                    loaded.CreationCompleted;
                loaded.SchemaVersion = takeBackFreeHorse ? 13 : CurrentSchemaVersion;
                loaded.HeroId = Guid.TryParse(loaded.HeroId, out Guid heroId) && heroId != Guid.Empty
                    ? loaded.HeroId
                    : Guid.NewGuid().ToString();
                loaded.ProgressionResetToken =
                    Guid.TryParse(loaded.ProgressionResetToken, out Guid resetToken) &&
                    resetToken != Guid.Empty
                        ? resetToken.ToString()
                        : string.Empty;
                loaded.Name = string.IsNullOrWhiteSpace(loaded.Name) ? "Герой арены" : loaded.Name;
                loaded.CultureId = NormalizeCultureId(loaded.CultureId);
                loaded.Level = Math.Max(1, loaded.Level);
                loaded.Experience = Math.Max(0, loaded.Experience);
                loaded.AttributePoints = Math.Max(0, loaded.AttributePoints);
                loaded.FocusPoints = Math.Max(0, loaded.FocusPoints);
                loaded.CreationStep = Math.Max(0, Math.Min(CreationStepCount, loaded.CreationStep));
                loaded.BackstoryChoices = loaded.BackstoryChoices ?? new List<string>();
                loaded.Attributes = loaded.Attributes ?? new List<DikozemieHeroValue>();
                loaded.Focus = loaded.Focus ?? new List<DikozemieHeroValue>();
                loaded.Skills = loaded.Skills ?? new List<DikozemieHeroValue>();
                loaded.SkillExperience = loaded.SkillExperience ??
                    new List<DikozemieHeroValue>();
                loaded.SelectedPerks = loaded.SelectedPerks ??
                    new List<DikozemieHeroPerkChoice>();
                loaded.Traits = loaded.Traits ?? new List<DikozemieHeroValue>();
                loaded.ParentOccupation = loaded.ParentOccupation ?? string.Empty;
                loaded.SelectedTitleType = string.IsNullOrWhiteSpace(loaded.SelectedTitleType)
                    ? "guard"
                    : loaded.SelectedTitleType;
                loaded.StartingAge = loaded.StartingAge < 20 ? 20 : loaded.StartingAge;
                if (sourceSchemaVersion < 13 && !loaded.CreationCompleted)
                {
                    // The previous prototype used generic, non-native answer IDs.
                    // Keep culture/appearance but restart the six narrative pages
                    // so a mixed old/new character can never be produced.
                    loaded.BackstoryChoices.Clear();
                    loaded.ParentOccupation = string.Empty;
                    loaded.SelectedTitleType = "guard";
                    loaded.StartingAge = 20;
                    loaded.CreationStep = 0;
                }
                loaded.SelectedPerks.RemoveAll(choice =>
                    choice == null ||
                    !DikozemieNativePerkCatalog.IsPerkChoice(
                        choice.SkillId,
                        choice.RequiredLevel,
                        choice.PerkId));
                // Older builds stored the smithing focus under a non-native
                // id.  Fold it into Bannerlord's real Crafting id so existing
                // profiles retain their points and the native icon resolves.
                DikozemieHeroValue legacySmithing = loaded.Focus.FirstOrDefault(item =>
                    item != null && string.Equals(
                        item.Id,
                        "Smithing",
                        StringComparison.OrdinalIgnoreCase));
                if (legacySmithing != null)
                {
                    DikozemieHeroValue crafting = loaded.Focus.FirstOrDefault(item =>
                        item != null && string.Equals(
                            item.Id,
                            "Crafting",
                            StringComparison.OrdinalIgnoreCase));
                    if (crafting == null)
                    {
                        loaded.Focus.Add(new DikozemieHeroValue
                        {
                            Id = "Crafting",
                            Value = Math.Max(0, legacySmithing.Value)
                        });
                    }
                    else
                    {
                        crafting.Value = Math.Max(crafting.Value, legacySmithing.Value);
                    }
                    loaded.Focus.RemoveAll(item => item != null && string.Equals(
                        item.Id,
                        "Smithing",
                        StringComparison.OrdinalIgnoreCase));
                }
                MigrateSkillId(loaded.Skills, "Smithing", "Crafting");
                MigrateSkillId(loaded.SkillExperience, "Smithing", "Crafting");
                foreach (string skillId in AllSkillIds)
                {
                    if (!loaded.Skills.Any(item => item != null && string.Equals(
                            item.Id,
                            skillId,
                            StringComparison.OrdinalIgnoreCase)))
                    {
                        loaded.Skills.Add(new DikozemieHeroValue
                        {
                            Id = skillId,
                            Value = 0
                        });
                    }
                    if (!loaded.SkillExperience.Any(item => item != null && string.Equals(
                            item.Id,
                            skillId,
                            StringComparison.OrdinalIgnoreCase)))
                    {
                        loaded.SkillExperience.Add(new DikozemieHeroValue
                        {
                            Id = skillId,
                            Value = 0
                        });
                    }
                }
                bool progressionMigrated = sourceSchemaVersion < 7 &&
                    MigrateDevelopmentProgression(loaded);
                loaded.OwnedItems = loaded.OwnedItems ?? new List<string>();
                loaded.Equipment = loaded.Equipment ?? new List<DikozemieHeroItem>();
                bool mountedStateMigrated = sourceSchemaVersion < 11 ||
                    loaded.BattleMounted != HasEquippedHorse(loaded.Equipment);
                loaded.BattleMounted = HasEquippedHorse(loaded.Equipment);
                bool grainMigrated = sourceSchemaVersion < 8 &&
                    loaded.CreationCompleted &&
                    !loaded.OwnedItems.Contains(
                        DikozemieItemStore.GrainItemId,
                        StringComparer.Ordinal);
                if (grainMigrated)
                {
                    loaded.OwnedItems.Add(DikozemieItemStore.GrainItemId);
                }
                bool developmentSchemaMigrated = sourceSchemaVersion < 9;
                bool developmentPointsReconciled = developmentSchemaMigrated &&
                    ReconcileDevelopmentPoints(loaded);
                bool perkSchemaMigrated = sourceSchemaVersion < 10;
                bool heroResetSchemaMigrated = sourceSchemaVersion < 12;
                bool nativeCreationSchemaMigrated = sourceSchemaVersion < 13;
                _state = loaded;
                _starterEquipmentMigrationPending = rebuildStarterEquipment;
                _freeHorseMigrationPending = takeBackFreeHorse;
                if (progressionMigrated || grainMigrated ||
                    developmentSchemaMigrated ||
                    developmentPointsReconciled ||
                    perkSchemaMigrated ||
                    heroResetSchemaMigrated ||
                    nativeCreationSchemaMigrated ||
                    mountedStateMigrated)
                {
                    SaveLocked();
                }
            }
        }

        private static void SaveLocked()
        {
            DikozemieArenaStateFiles.WriteAtomic(StatePath, _state);
        }

        private static bool IsAdultBodyProperties(string value)
        {
            return BodyProperties.FromString(value, out BodyProperties body) &&
                body.Age >= 18f;
        }

        private static void MigrateSkillId(
            List<DikozemieHeroValue> values,
            string oldId,
            string newId)
        {
            DikozemieHeroValue legacy = values?.FirstOrDefault(item => item != null &&
                string.Equals(item.Id, oldId, StringComparison.OrdinalIgnoreCase));
            if (legacy == null)
            {
                return;
            }
            DikozemieHeroValue current = values.FirstOrDefault(item => item != null &&
                string.Equals(item.Id, newId, StringComparison.OrdinalIgnoreCase));
            if (current == null)
            {
                values.Add(new DikozemieHeroValue
                {
                    Id = newId,
                    Value = Math.Max(0, legacy.Value)
                });
            }
            else
            {
                current.Value = Math.Max(current.Value, legacy.Value);
            }
            values.RemoveAll(item => item != null &&
                string.Equals(item.Id, oldId, StringComparison.OrdinalIgnoreCase));
        }

        private static bool MigrateDevelopmentProgression(
            DikozemieHeroProfileState loaded)
        {
            if (loaded == null || !loaded.CreationCompleted)
            {
                return false;
            }

            loaded.AttributePoints = Math.Max(0, loaded.AttributePoints) +
                StartingAttributePoints;
            loaded.FocusPoints = Math.Max(0, loaded.FocusPoints) +
                StartingFocusPoints;
            for (int step = 0; step < Math.Min(CreationStepCount, loaded.BackstoryChoices.Count); step++)
            {
                DikozemieHeroBackgroundChoice selected =
                    DikozemieNativeCreationCatalog.FindChoice(loaded.BackstoryChoices[step]);
                if (selected != null)
                {
                    foreach (string skill in selected.Skills)
                    {
                        SetValue(
                            loaded.Skills,
                            skill,
                            Math.Min(DikozemieArenaCombatProgression.MaximumSkillLevel,
                                GetValue(loaded.Skills, skill, 0) + 10));
                    }
                }
            }
            return true;
        }

        private static bool ReconcileDevelopmentPoints(
            DikozemieHeroProfileState loaded)
        {
            if (loaded == null || !loaded.CreationCompleted)
            {
                return false;
            }

            int validChoiceCount = 0;
            int creationFocusBudget = 0;
            var creationFocus = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int step = 0;
                 step < Math.Min(CreationStepCount, loaded.BackstoryChoices.Count);
                 step++)
            {
                DikozemieHeroBackgroundChoice selected =
                    DikozemieNativeCreationCatalog.FindChoice(loaded.BackstoryChoices[step]);
                if (selected == null)
                {
                    continue;
                }

                validChoiceCount++;
                foreach (string skill in selected.Skills)
                {
                    creationFocus.TryGetValue(skill, out int invested);
                    if (invested < MaximumFocusPerSkill)
                    {
                        creationFocus[skill] = invested + 1;
                        creationFocusBudget++;
                    }
                }
            }

            int expectedFocusBudget = creationFocusBudget + StartingFocusPoints +
                Math.Max(0, loaded.Level - 1) * FocusPointsPerLevel;
            int possessedFocusBudget = Math.Max(0, loaded.FocusPoints) +
                AllSkillIds.Sum(skillId => Math.Min(
                    MaximumFocusPerSkill,
                    GetValue(loaded.Focus, skillId, 0)));
            int expectedAttributeBudget = AttributeIds.Length * 2 +
                validChoiceCount + StartingAttributePoints +
                Math.Max(0, loaded.Level) / LevelsPerAttributePoint;
            int possessedAttributeBudget = Math.Max(0, loaded.AttributePoints) +
                AttributeIds.Sum(attributeId => Math.Min(
                    MaximumAttribute,
                    GetValue(loaded.Attributes, attributeId, 2)));

            bool changed = false;
            if (possessedFocusBudget < expectedFocusBudget)
            {
                loaded.FocusPoints += expectedFocusBudget - possessedFocusBudget;
                changed = true;
            }
            if (possessedAttributeBudget < expectedAttributeBudget)
            {
                loaded.AttributePoints +=
                    expectedAttributeBudget - possessedAttributeBudget;
                changed = true;
            }
            return changed;
        }
    }
}
