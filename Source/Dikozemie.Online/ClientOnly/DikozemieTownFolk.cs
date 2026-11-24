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
    // The people of the town walk: the singleplayer townsfolk of each culture
    // in their own civilian clothes, gathered by Tools/build_town_folk.py into
    // ModuleData/dikozemie_town_folk.json.
    [DataContract]
    internal sealed class DikozemieTownFolkRole
    {
        [DataMember(Name = "source")] public string Source = string.Empty;
        [DataMember(Name = "name")] public string Name = string.Empty;
        [DataMember(Name = "female")] public bool Female;
        [DataMember(Name = "kits")] public List<Dictionary<string, string>> Kits = new List<Dictionary<string, string>>();
    }

    // The face range of a culture's townsmen or townswomen in the
    // singleplayer game (sandboxcore_bodyproperties.xml).
    [DataContract]
    internal sealed class DikozemieTownFaceRange
    {
        [DataMember(Name = "min")] public string Min = string.Empty;
        [DataMember(Name = "max")] public string Max = string.Empty;
        [DataMember(Name = "hair")] public string Hair = string.Empty;
        [DataMember(Name = "beard")] public string Beard = string.Empty;
    }

    [DataContract]
    internal sealed class DikozemieTownFolkDocument
    {
        [DataMember(Name = "faces")]
        public Dictionary<string, string> Faces = new Dictionary<string, string>();

        [DataMember(Name = "faceRanges")]
        public Dictionary<string, Dictionary<string, DikozemieTownFaceRange>> FaceRanges =
            new Dictionary<string, Dictionary<string, DikozemieTownFaceRange>>();

        [DataMember(Name = "roles")]
        public Dictionary<string, DikozemieTownFolkRole> Roles = new Dictionary<string, DikozemieTownFolkRole>();
    }

    // One person of the walk. The same seed gives the same person: the
    // tavern keeper of Varnovopol has the same face every evening.
    internal sealed class DikozemieTownPerson
    {
        internal string Role = string.Empty;
        // «Трактирщик», «Стражник».
        internal string Title = string.Empty;
        // A personal name for those one talks to, empty for the crowd.
        internal string Name = string.Empty;
        internal string Culture = "empire";
        internal bool Female;
        internal int Seed;
        internal Equipment Equipment;

        internal string Label => string.IsNullOrEmpty(Name) ? Title : Title + " " + Name;

        internal BasicCharacterObject Body() =>
            MBObjectManager.Instance.GetObject<BasicCharacterObject>(DikozemieTownFolk.FaceTroop(Culture)) ??
            MBObjectManager.Instance.GetObject<BasicCharacterObject>("mp_skirmisher_empire_troop");
    }

    internal static class DikozemieTownFolk
    {
        private static DikozemieTownFolkDocument _document;

        // The cultures that have their own townsfolk.
        internal static string FolkCulture(string culture)
        {
            string key = DikozemieTown.NormalizeCulture(culture);
            if (key == "nords")
            {
                key = "nord";
            }
            EnsureLoaded();
            return _document.Roles.ContainsKey("townsman_" + key) ? key : "empire";
        }

        internal static string FaceTroop(string culture)
        {
            EnsureLoaded();
            return _document.Faces.TryGetValue(FolkCulture(culture), out string id)
                ? id
                : "mp_skirmisher_empire_troop";
        }

        internal static bool HasRole(string role, string culture)
        {
            EnsureLoaded();
            return _document.Roles.ContainsKey(role + "_" + FolkCulture(culture));
        }

        internal static DikozemieTownPerson Create(string role, string culture, int seed, string name = null,
            int kitOrder = -1)
        {
            EnsureLoaded();
            string folk = FolkCulture(culture);
            if (!_document.Roles.TryGetValue(role + "_" + folk, out DikozemieTownFolkRole data) &&
                !_document.Roles.TryGetValue(role + "_empire", out data))
            {
                return null;
            }
            if (data.Kits == null || data.Kits.Count == 0)
            {
                return null;
            }
            // Neighbouring seeds must not give neighbouring clothes: three
            // robbers of one night looked like brothers (2026-11-09).
            // kitOrder: the n-th of a group that must not dress alike.
            int kitIndex = kitOrder >= 0
                ? kitOrder % data.Kits.Count
                : (int)(Mix(seed) % (uint)data.Kits.Count);
            Dictionary<string, string> kit = data.Kits[kitIndex];
            var equipment = new Equipment(Equipment.EquipmentType.Civilian);
            foreach (KeyValuePair<string, string> entry in kit)
            {
                if (!int.TryParse(entry.Key, out int slot) || slot < 0 || slot > 9)
                {
                    continue;
                }
                ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(entry.Value);
                if (item != null)
                {
                    equipment[slot] = new EquipmentElement(item);
                }
            }
            return new DikozemieTownPerson
            {
                Role = role,
                Title = data.Name,
                Name = name ?? string.Empty,
                Culture = folk,
                Female = data.Female,
                Seed = seed,
                Equipment = equipment
            };
        }

        // The notable of the town screen as a person: the same face and
        // clothes in the menu portrait and in the streets of the walk.
        internal static DikozemieTownPerson NotablePerson(DikozemieLobbyMapNode node, DikozemieNotable notable)
        {
            if (node == null || notable == null)
            {
                return null;
            }
            string role = notable.Female
                ? (DikozemieTown.IsVillage(node) ? "village_woman" : "townswoman")
                : "notable";
            int seed = (int)Mix(DikozemieTown.StableHash(node.Id + "|" + notable.Role));
            int order = 0;
            foreach (DikozemieNotable other in DikozemieTown.Notables(node))
            {
                if (other.Role == notable.Role)
                {
                    break;
                }
                order++;
            }
            // One town's notables never wear the same outfit: a common start
            // for the town, three outfits apart for each of them.
            DikozemieTownPerson person = Create(role, node.Culture, seed, notable.Name,
                (int)(Mix(DikozemieTown.StableHash(node.Id)) % 101u) + order * 3);
            if (person != null)
            {
                person.Title = notable.Role;
                person.Female = notable.Female;
            }
            return person;
        }

        internal static int AgeOf(string role, int seed)
        {
            uint roll = Mix(seed);
            switch (role)
            {
                case "notable":
                case "tavernkeeper":
                case "arena_master":
                case "merchant":
                case "armorer":
                case "weaponsmith":
                case "horse_merchant":
                    return 34 + (int)(roll % 26u);
                case "beggar":
                case "female_beggar":
                    return 40 + (int)(roll % 30u);
                case "wench":
                case "dancer":
                    return 18 + (int)(roll % 10u);
                case "guard":
                case "thug":
                    return 20 + (int)(roll % 18u);
                default:
                    return 18 + (int)(roll % 46u);
            }
        }

        // The face of a townsman: the body range of a multiplayer troop of
        // his people, his own age, no war paint.
        internal static BodyProperties FaceOf(DikozemieTownPerson person)
        {
            BasicCharacterObject body = person.Body();
            // The faces of the people come from the singleplayer townsfolk
            // range of the culture: the multiplayer troop's own range is
            // narrow and the hero creation default is a single face; four
            // notables of a town looked like brothers (2026-11-09).
            BodyProperties min = body.GetBodyPropertiesMin();
            BodyProperties max = body.GetBodyPropertiesMax();
            string hair = body.BodyPropertyRange?.HairTags;
            string beard = body.BodyPropertyRange?.BeardTags;
            EnsureLoaded();
            if (_document.FaceRanges != null &&
                _document.FaceRanges.TryGetValue(FolkCulture(person.Culture), out Dictionary<string, DikozemieTownFaceRange> genders) &&
                genders != null &&
                genders.TryGetValue(person.Female ? "female" : "male", out DikozemieTownFaceRange range) &&
                range != null &&
                BodyProperties.FromString(range.Min, out BodyProperties rangeMin) &&
                BodyProperties.FromString(range.Max, out BodyProperties rangeMax))
            {
                min = rangeMin;
                max = rangeMax;
                hair = string.IsNullOrEmpty(range.Hair) ? hair : range.Hair;
                beard = string.IsNullOrEmpty(range.Beard) ? beard : range.Beard;
            }
            BodyProperties face = BodyProperties.GetRandomBodyProperties(
                body.Race, person.Female, min, max,
                (int)person.Equipment.HairCoverType, person.Seed,
                hair, beard, string.Empty);
            return TaleWorlds.MountAndBlade.MBBodyProperties.GetBodyPropertiesWithAge(
                ref face, AgeOf(person.Role, person.Seed));
        }

        // A well-mixed integer hash (the finaliser of MurmurHash3).
        internal static uint Mix(int seed)
        {
            unchecked
            {
                uint value = (uint)seed;
                value ^= value >> 16;
                value *= 0x85ebca6b;
                value ^= value >> 13;
                value *= 0xc2b2ae35;
                value ^= value >> 16;
                return value;
            }
        }

        private static void EnsureLoaded()
        {
            if (_document != null)
            {
                return;
            }
            _document = new DikozemieTownFolkDocument();
            try
            {
                string path = Path.Combine(
                    ModuleHelper.GetModuleFullPath(DikozemieSubModule.ModuleId),
                    "ModuleData",
                    "dikozemie_town_folk.json");
                var serializer = new DataContractJsonSerializer(
                    typeof(DikozemieTownFolkDocument),
                    new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText(path, Encoding.UTF8))))
                {
                    _document = (DikozemieTownFolkDocument)serializer.ReadObject(stream) ??
                        new DikozemieTownFolkDocument();
                }
                if (_document.Roles == null)
                {
                    _document.Roles = new Dictionary<string, DikozemieTownFolkRole>();
                }
                if (_document.Faces == null)
                {
                    _document.Faces = new Dictionary<string, string>();
                }
                if (_document.FaceRanges == null)
                {
                    _document.FaceRanges = new Dictionary<string, Dictionary<string, DikozemieTownFaceRange>>();
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: town folk unavailable: " + error.Message);
                _document = new DikozemieTownFolkDocument();
            }
        }
    }
}
