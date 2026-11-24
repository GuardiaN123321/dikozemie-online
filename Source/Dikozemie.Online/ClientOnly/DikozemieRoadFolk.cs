using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.ModuleManager;
using TaleWorlds.ObjectSystem;

namespace Dikozemie.Online
{
    // The people of the roads: real singleplayer troops (bandits, man-catchers,
    // guards, caravan guards, merchants) gathered by Tools/build_road_folk.py
    // into ModuleData/dikozemie_road_folk.json.
    [DataContract]
    internal sealed class DikozemieRoadRole
    {
        [DataMember(Name = "troop")] public string Troop = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "plural")] public string Plural = string.Empty;
        // After «двое», «трое»: «двое лесных разбойников».
        [DataMember(Name = "pluralGen")] public string PluralGenitive = string.Empty;
        [DataMember(Name = "face")] public string Face = string.Empty;
        [DataMember(Name = "level")] public int Level = 1;
        [DataMember(Name = "mounted")] public bool Mounted;
        [DataMember(Name = "skills")] public Dictionary<string, int> Skills = new Dictionary<string, int>();
        [DataMember(Name = "kits")] public List<Dictionary<string, string>> Kits = new List<Dictionary<string, string>>();
    }

    [DataContract]
    internal sealed class DikozemieRoadFolkDocument
    {
        [DataMember(Name = "faces")]
        public Dictionary<string, string> Faces = new Dictionary<string, string>();

        [DataMember(Name = "roles")]
        public Dictionary<string, DikozemieRoadRole> Roles = new Dictionary<string, DikozemieRoadRole>();
    }

    // One person of a road fight, either side.
    internal sealed class DikozemieRoadFighter
    {
        internal string Role = string.Empty;
        internal string Name = string.Empty;
        internal string Culture = "empire";
        internal int Seed;
        internal bool IsLeader;
        // Share of health at the start of a fight: a wounded fugitive.
        internal float Health = 1f;
        internal Dictionary<string, int> Skills = new Dictionary<string, int>();
        internal Equipment Equipment;

        internal bool IsMounted => Equipment != null && Equipment[EquipmentIndex.Horse].Item != null;

        // «топор, праща» — what he carries, for the encounter card.
        internal string Arms
        {
            get
            {
                var parts = new List<string>();
                if (Equipment == null)
                {
                    return string.Empty;
                }
                for (int slot = 0; slot < 4; slot++)
                {
                    ItemObject item = Equipment[slot].Item;
                    string word = DikozemieRoadFolk.ArmsWord(item);
                    if (!string.IsNullOrEmpty(word) && !parts.Contains(word))
                    {
                        parts.Add(word);
                    }
                }
                if (IsMounted)
                {
                    parts.Add("на коне");
                }
                return string.Join(", ", parts);
            }
        }

        // The singleplayer troop a fighter of this role is (for the face base).
        internal BasicCharacterObject FaceCharacter()
        {
            string id = DikozemieRoadFolk.FaceTroop(Culture);
            return MBObjectManager.Instance.GetObject<BasicCharacterObject>(id) ??
                MBObjectManager.Instance.GetObject<BasicCharacterObject>("mp_skirmisher_empire_troop") ??
                MBObjectManager.Instance.GetObject<BasicCharacterObject>("mp_character");
        }
    }

    internal static class DikozemieRoadFolk
    {
        private static DikozemieRoadFolkDocument _document;

        internal static readonly string[] Cultures =
        {
            "empire", "vlandia", "sturgia", "battania", "khuzait", "aserai"
        };

        // Nord villages of the War Sails coast dress like Sturgians.
        internal static string FolkCulture(string culture)
        {
            string key = DikozemieTown.NormalizeCulture(culture);
            return Array.IndexOf(Cultures, key) >= 0 ? key : "sturgia";
        }

        internal static DikozemieRoadRole Role(string role)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(role) && _document.Roles.TryGetValue(role, out DikozemieRoadRole found)
                ? found
                : null;
        }

        internal static string FaceTroop(string culture)
        {
            EnsureLoaded();
            return _document.Faces.TryGetValue(FolkCulture(culture), out string id)
                ? id
                : "mp_skirmisher_empire_troop";
        }

        // A fighter of the role. The same seed gives the same man: the card
        // shows the very bandits the fight then spawns.
        internal static DikozemieRoadFighter Create(string role, string culture, int seed, string name = null)
        {
            DikozemieRoadRole data = Role(role);
            if (data == null || data.Kits.Count == 0)
            {
                return null;
            }
            var random = new Random(seed);
            Dictionary<string, string> kit = data.Kits[random.Next(data.Kits.Count)];
            var equipment = new Equipment(Equipment.EquipmentType.Battle);
            foreach (KeyValuePair<string, string> entry in kit)
            {
                if (!int.TryParse(entry.Key, out int slot) || slot < 0 || slot > 11)
                {
                    continue;
                }
                ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(entry.Value);
                if (item != null)
                {
                    equipment[slot] = new EquipmentElement(item);
                }
                else
                {
                    TaleWorlds.Library.Debug.Print("DikozemieOnline: road kit item missing " + entry.Value +
                        " role=" + role, 0, TaleWorlds.Library.Debug.DebugColor.Red);
                }
            }
            if (equipment[EquipmentIndex.Horse].Item == null)
            {
                equipment[EquipmentIndex.HorseHarness] = default(EquipmentElement);
            }
            return new DikozemieRoadFighter
            {
                Role = role,
                Name = string.IsNullOrEmpty(name) ? data.Name : name,
                Culture = string.IsNullOrEmpty(data.Face) ? FolkCulture(culture) : data.Face,
                Seed = seed,
                Skills = new Dictionary<string, int>(data.Skills ?? new Dictionary<string, int>()),
                Equipment = equipment
            };
        }

        // One word for a weapon on the encounter card.
        internal static string ArmsWord(ItemObject item)
        {
            if (item == null)
            {
                return string.Empty;
            }
            string id = item.StringId ?? string.Empty;
            switch (item.ItemType)
            {
                case ItemObject.ItemTypeEnum.Bow: return "лук";
                case ItemObject.ItemTypeEnum.Crossbow: return "арбалет";
                case ItemObject.ItemTypeEnum.Shield: return "щит";
                case ItemObject.ItemTypeEnum.Thrown:
                    return id.Contains("stone") ? "камни" : id.Contains("axe") ? "метательные топоры" : "дротики";
                case ItemObject.ItemTypeEnum.Sling:
                    return "праща";
                case ItemObject.ItemTypeEnum.Arrows:
                case ItemObject.ItemTypeEnum.Bolts:
                case ItemObject.ItemTypeEnum.SlingStones:
                case ItemObject.ItemTypeEnum.Bullets:
                    return string.Empty;
                case ItemObject.ItemTypeEnum.Polearm:
                    return id.Contains("pitchfork") ? "вилы" : id.Contains("sickle") ? "серп" : "копьё";
                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                    if (id.Contains("pickaxe")) return "кирка";
                    if (id.Contains("sickle")) return "серп";
                    if (id.Contains("hatchet") || id.Contains("axe")) return "топор";
                    if (id.Contains("mace") || id.Contains("club") || id.Contains("hammer") || id.Contains("maul")) return "дубина";
                    if (id.Contains("seax") || id.Contains("dagger") || id.Contains("knife")) return "нож";
                    return "меч";
                default:
                    return string.Empty;
            }
        }

        private static void EnsureLoaded()
        {
            if (_document != null)
            {
                return;
            }
            _document = new DikozemieRoadFolkDocument();
            try
            {
                string path = Path.Combine(
                    ModuleHelper.GetModuleFullPath(DikozemieSubModule.ModuleId),
                    "ModuleData",
                    "dikozemie_road_folk.json");
                var serializer = new DataContractJsonSerializer(
                    typeof(DikozemieRoadFolkDocument),
                    new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText(path, Encoding.UTF8))))
                {
                    _document = (DikozemieRoadFolkDocument)serializer.ReadObject(stream) ??
                        new DikozemieRoadFolkDocument();
                }
                if (_document.Roles == null)
                {
                    _document.Roles = new Dictionary<string, DikozemieRoadRole>();
                }
                if (_document.Faces == null)
                {
                    _document.Faces = new Dictionary<string, string>();
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: road folk unavailable: " + error.Message);
                _document = new DikozemieRoadFolkDocument();
            }
        }
    }
}
