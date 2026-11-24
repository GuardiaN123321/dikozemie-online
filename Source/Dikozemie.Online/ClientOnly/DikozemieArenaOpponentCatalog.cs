using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Dikozemie.Online
{
    internal sealed class DikozemieArenaOpponent
    {
        internal string CharacterId;
        internal string DisplayName;
        internal int StartingRating;
        internal double Strength;
        internal BasicCharacterObject Character;
        internal bool IsMounted;
    }

    [DataContract]
    internal sealed class DikozemieArenaOpponentCatalogEntry
    {
        [DataMember(Name = "characterId")]
        public string CharacterId = string.Empty;

        [DataMember(Name = "rating")]
        public int Rating = 0;

        [DataMember(Name = "strength")]
        public double Strength = 0;
    }

    [DataContract]
    internal sealed class DikozemieArenaOpponentCatalogFile
    {
        [DataMember(Name = "schemaVersion")]
        public int SchemaVersion = 0;

        [DataMember(Name = "selectionRadius")]
        public int SelectionRadius = 0;

        [DataMember(Name = "opponents")]
        public List<DikozemieArenaOpponentCatalogEntry> Opponents =
            new List<DikozemieArenaOpponentCatalogEntry>();
    }

    internal static class DikozemieArenaOpponentCatalog
    {
        internal const int RatingStep = 100;
        internal const int RatingSelectionRadius = 10000;
        private const int SmallFieldSize = 12;
        internal const int StarterRatingSelectionRadius = 2000;
        internal const int MinimumMountedOpponentRating = 10000;
        private const int NativeOpponentRatingStep = 500;
        private const string CatalogFileName = "dikozemie_arena_opponents.json";
        private static readonly object RandomGate = new object();
        private static readonly Random OpponentRandom = new Random();
#if !CLIENT
        private static bool _leaderboardSeeded;
#endif

        private static string CatalogPath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..",
            "..",
            "Modules",
            "DikozemieOnline",
            "ModuleData",
            CatalogFileName);

        internal static IReadOnlyList<DikozemieArenaOpponent> Build(
            BasicCharacterObject fallback)
        {
            EnsureLeaderboardSeeded();
            var opponents = new List<DikozemieArenaOpponent>();
            if (TryLoadCatalog(out DikozemieArenaOpponentCatalogFile catalog))
            {
                List<BasicCharacterObject> runtimeCharacters =
                    OrderRuntimeCharactersByStrength(
                        GetNativeMultiplayerCharacters(fallback));
                int maximumConfiguredRating = catalog.Opponents
                    .Where(entry => entry != null && entry.Rating > 0)
                    .Select(entry => entry.Rating)
                    .DefaultIfEmpty(RatingStep)
                    .Max();
                foreach (DikozemieArenaOpponentCatalogEntry entry in catalog.Opponents)
                {
                    if (entry == null ||
                        entry.Rating < 1 ||
                        !IsEligibleCharacterId(entry.CharacterId))
                    {
                        continue;
                    }

                    BasicCharacterObject catalogCharacter =
                        MBObjectManager.Instance.GetObject<BasicCharacterObject>(
                            entry.CharacterId);
                    BasicCharacterObject runtimeCharacter = catalogCharacter ??
                        SelectRuntimeCharacter(
                            runtimeCharacters,
                            entry.CharacterId,
                            entry.Rating,
                            maximumConfiguredRating,
                            fallback);
                    if (runtimeCharacter == null)
                    {
                        continue;
                    }

                    opponents.Add(new DikozemieArenaOpponent
                    {
                        CharacterId = entry.CharacterId,
                        DisplayName = catalogCharacter?.Name?.ToString() ??
                            entry.CharacterId,
                        StartingRating = GetStartingRating(entry.Rating),
                        Strength = entry.Strength,
                        Character = runtimeCharacter,
                        IsMounted = CharacterHasMount(runtimeCharacter)
                    });
                }
            }

            // The checked-in catalog is the authoritative 1620-character
            // ladder.  Native multiplayer classes are only an emergency
            // fallback when that catalog yielded no usable combatants.
            // Appending them unconditionally made GetCurrentRating persist 54
            // extra bots and inflated the physical table to 1675 rows.
            if (opponents.Count == 0)
            {
                AddNativeMultiplayerOpponents(opponents, fallback);
            }

            opponents.Sort((left, right) =>
                left.StartingRating.CompareTo(right.StartingRating));
            if (opponents.Count == 0 && fallback != null)
            {
                opponents.Add(new DikozemieArenaOpponent
                {
                    CharacterId = fallback.StringId,
                    DisplayName = fallback.Name?.ToString() ?? fallback.StringId,
                    StartingRating = 1,
                    Strength = 1,
                    Character = fallback,
                    IsMounted = CharacterHasMount(fallback)
                });
            }
            return opponents;
        }

        private static List<DikozemieArenaOpponent> WithinRating(
            IEnumerable<DikozemieArenaOpponent> opponents,
            int minimumRating,
            int maximumRating)
        {
            var result = new List<DikozemieArenaOpponent>();
            foreach (DikozemieArenaOpponent opponent in opponents)
            {
                int opponentRating = GetCurrentRating(opponent);
                if (opponentRating >= minimumRating &&
                    opponentRating <= maximumRating)
                {
                    result.Add(opponent);
                }
            }
            return result;
        }

        internal static DikozemieArenaOpponent SelectForPlayer(
            IReadOnlyList<DikozemieArenaOpponent> opponents,
            int playerRating,
            string previousCharacterId = null)
        {
            if (opponents == null || opponents.Count == 0)
            {
                return null;
            }

            int ratingCenter = Math.Max(RatingStep, playerRating);
            // Customer, 2026-11-02: opponents within ±10,000 of the player's
            // rating, for everyone. New heroes still meet no archers before
            // 2,000 and no riders before 10,000 (the filter below).
            int selectionRadius = RatingSelectionRadius;
            int minimumEligibleRating = Math.Max(
                RatingStep,
                ratingCenter - selectionRadius);
            int maximumEligibleRating = ratingCenter > int.MaxValue -
                selectionRadius
                    ? int.MaxValue
                    : ratingCenter + selectionRadius;
            // New heroes start in a 2000-point learning bracket against foot
            // melee opponents. Ranged opponents unlock after that bracket;
            // the full +/-10000 range and cavalry unlock at 10000 rating.
            List<DikozemieArenaOpponent> tierOpponents = opponents
                .Where(opponent =>
                    (playerRating >= MinimumMountedOpponentRating ||
                        !HasMount(opponent, null)) &&
                    (playerRating >= StarterRatingSelectionRadius ||
                        !HasRangedWeapon(opponent, null)))
                .ToList();
            if (tierOpponents.Count == 0)
            {
                tierOpponents = opponents.ToList();
            }

            List<DikozemieArenaOpponent> eligible = WithinRating(
                tierOpponents,
                minimumEligibleRating,
                maximumEligibleRating);
            if (eligible.Count == 0)
            {
                // The learning filters must not push a newcomer outside the
                // ±10,000 window (2026-11-05: in a melee duel a 142 hero met
                // a 28,800 bandit, because every foot fighter near him also
                // carried stones or javelins). Keep the window, drop the
                // filters: first the ranged one, then the riders (a bot below
                // 10,000 loses its horse at spawn anyway).
                List<DikozemieArenaOpponent> onFoot = opponents
                    .Where(opponent =>
                        playerRating >= MinimumMountedOpponentRating ||
                        !HasMount(opponent, null))
                    .ToList();
                eligible = WithinRating(
                    onFoot,
                    minimumEligibleRating,
                    maximumEligibleRating);
                if (eligible.Count == 0)
                {
                    eligible = WithinRating(
                        opponents,
                        minimumEligibleRating,
                        maximumEligibleRating);
                }
            }

            if (eligible.Count == 0)
            {
                long nearestDistance = tierOpponents.Min(opponent => Math.Abs(
                    (long)GetCurrentRating(opponent) - ratingCenter));
                eligible.AddRange(tierOpponents.Where(opponent => Math.Abs(
                    (long)GetCurrentRating(opponent) - ratingCenter) == nearestDistance));
            }

            if (eligible.Count > 1 &&
                !string.IsNullOrWhiteSpace(previousCharacterId))
            {
                var withoutPrevious = eligible.Where(opponent => !string.Equals(
                    opponent.CharacterId,
                    previousCharacterId,
                    StringComparison.OrdinalIgnoreCase)).ToList();
                if (withoutPrevious.Count > 0)
                {
                    eligible = withoutPrevious;
                }
            }

            lock (RandomGate)
            {
                return eligible[OpponentRandom.Next(eligible.Count)];
            }
        }

        internal static int EnsureLeaderboardSeeded()
        {
#if CLIENT
            return 0;
#else
            if (_leaderboardSeeded ||
                !TryLoadCatalog(out DikozemieArenaOpponentCatalogFile catalog))
            {
                return 0;
            }

            int seeded = 0;
            var catalogBotIds = new HashSet<string>(
                catalog.Opponents
                    .Where(entry => entry != null &&
                        !string.IsNullOrWhiteSpace(entry.CharacterId) &&
                        entry.Rating >= 1)
                    .Select(entry => "bot:" + entry.CharacterId.Trim()),
                StringComparer.Ordinal);
            int pruned = DikozemieArenaRatingStore.PruneBotsExcept(catalogBotIds);
            foreach (DikozemieArenaOpponentCatalogEntry entry in catalog.Opponents)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.CharacterId) ||
                    entry.Rating < 1)
                {
                    continue;
                }

                BasicCharacterObject character = MBObjectManager.Instance
                    .GetObject<BasicCharacterObject>(entry.CharacterId);
                DikozemieArenaRatingStore.GetBot(
                    entry.CharacterId,
                    character?.Name?.ToString() ?? entry.CharacterId,
                    GetStartingRating(entry.Rating));
                seeded++;
            }
            DikozemieArenaRatingStore.Flush();
            _leaderboardSeeded = true;
            Console.WriteLine(
                "DikozemieOnline: seeded arena leaderboard bots=" + seeded +
                " staleRemoved=" + pruned);
            return seeded;
#endif
        }

        internal static IReadOnlyList<DikozemieArenaOpponent> SelectTournamentField(
            IReadOnlyList<DikozemieArenaOpponent> opponents,
            int participantCount,
            int playerRating,
            bool mountedOnly,
            BasicCharacterObject fallback)
        {
            var field = new List<DikozemieArenaOpponent>();
            if (opponents == null || opponents.Count == 0 || participantCount <= 0)
            {
                return field;
            }

            var uniqueOpponents = new List<DikozemieArenaOpponent>();
            var reserveOpponents = new List<DikozemieArenaOpponent>();
            var characterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int minimumEligibleRating = Math.Max(
                RatingStep,
                Math.Max(0, playerRating) - RatingSelectionRadius);
            int maximumEligibleRating = Math.Max(0, playerRating) +
                RatingSelectionRadius;
            for (int index = 0; index < opponents.Count; index++)
            {
                DikozemieArenaOpponent opponent = opponents[index];
                if (opponent == null ||
                    string.IsNullOrWhiteSpace(opponent.CharacterId) ||
                    (mountedOnly && !HasMount(opponent, fallback)) ||
                    !characterIds.Add(opponent.CharacterId))
                {
                    continue;
                }
                int rating = GetCurrentRating(opponent);
                if (rating >= minimumEligibleRating &&
                    rating <= maximumEligibleRating)
                {
                    uniqueOpponents.Add(opponent);
                }
                else
                {
                    // Large 50/75/100-a-side battles can need more unique
                    // identities than fit inside the normal rating window.
                    // Preserve the close-rating pool first, then widen only
                    // enough to fill every physical fighter slot.
                    reserveOpponents.Add(opponent);
                }
            }

            lock (RandomGate)
            {
                for (int index = uniqueOpponents.Count - 1; index > 0; index--)
                {
                    int swapIndex = OpponentRandom.Next(index + 1);
                    DikozemieArenaOpponent temporary = uniqueOpponents[index];
                    uniqueOpponents[index] = uniqueOpponents[swapIndex];
                    uniqueOpponents[swapIndex] = temporary;
                }
                for (int index = reserveOpponents.Count - 1; index > 0; index--)
                {
                    int swapIndex = OpponentRandom.Next(index + 1);
                    DikozemieArenaOpponent temporary = reserveOpponents[index];
                    reserveOpponents[index] = reserveOpponents[swapIndex];
                    reserveOpponents[swapIndex] = temporary;
                }
            }

            // The normal +/- rating window remains random. If a very large
            // battle needs still more unique bodies, widen from the edge of
            // that window outwards instead of drawing elites from the whole
            // 1620-fighter ladder. The preceding shuffle keeps equal-distance
            // candidates random while OrderBy's stable ordering makes the
            // widening strictly nearest-rating-first.
            int ratingCenter = Math.Max(RatingStep, playerRating);
            reserveOpponents = reserveOpponents
                .OrderBy(opponent => Math.Abs(
                    (long)GetCurrentRating(opponent) - ratingCenter))
                .ToList();

            int selectedCount = Math.Min(participantCount, uniqueOpponents.Count);
            for (int index = 0; index < selectedCount; index++)
            {
                field.Add(uniqueOpponents[index]);
            }
            for (int index = 0;
                index < reserveOpponents.Count &&
                field.Count < participantCount;
                index++)
            {
                field.Add(reserveOpponents[index]);
            }
            return field;
        }

        internal static IReadOnlyList<DikozemieArenaOpponent>
            SelectMixedTournamentField(
                IReadOnlyList<DikozemieArenaOpponent> opponents,
                int participantCount,
                int playerRating,
                DikozemieArenaDiscipline discipline,
                BasicCharacterObject fallback)
        {
            var field = new List<DikozemieArenaOpponent>();
            if (opponents == null || opponents.Count == 0 ||
                participantCount <= 0)
            {
                return field;
            }

            int rangedTarget = participantCount / 2;
            int meleeTarget = participantCount - rangedTarget;
            IReadOnlyList<DikozemieArenaOpponent> melee =
                SelectTournamentField(
                    opponents.Where(opponent => MatchesComposition(
                        opponent,
                        fallback,
                        discipline,
                        DikozemieArenaComposition.Melee)).ToList(),
                    meleeTarget,
                    playerRating,
                    mountedOnly: false,
                    fallback: fallback);
            IReadOnlyList<DikozemieArenaOpponent> ranged =
                SelectTournamentField(
                    opponents.Where(opponent => MatchesComposition(
                        opponent,
                        fallback,
                        discipline,
                        DikozemieArenaComposition.Ranged)).ToList(),
                    rangedTarget,
                    playerRating,
                    mountedOnly: false,
                    fallback: fallback);
            field.AddRange(melee);
            // Archers are rare near low ratings, and the nearest one can be far
            // stronger than anybody in a small battle: a 29700 archer against
            // ratings of 12 and 5974 won the 2×2 alone (2026-10-30). A small
            // field then goes without that archer; large ones need the bodies.
            field.AddRange(participantCount > SmallFieldSize
                ? ranged
                : ranged.Where(opponent =>
                    Math.Abs((long)GetCurrentRating(opponent) - playerRating) <=
                    (long)RatingSelectionRadius));

            if (field.Count < participantCount)
            {
                var selectedIds = new HashSet<string>(
                    field.Where(opponent => opponent != null)
                        .Select(opponent => opponent.CharacterId),
                    StringComparer.OrdinalIgnoreCase);
                IReadOnlyList<DikozemieArenaOpponent> reserve =
                    SelectTournamentField(
                        opponents.Where(opponent => opponent != null &&
                            !selectedIds.Contains(opponent.CharacterId))
                            .ToList(),
                        participantCount - field.Count,
                        playerRating,
                        mountedOnly: false,
                        fallback: fallback);
                field.AddRange(reserve);
            }
            List<DikozemieArenaOpponent> selected =
                field.Take(participantCount).ToList();
            if (participantCount <= SmallFieldSize)
            {
                ReplaceFarOpponents(selected, opponents, playerRating);
            }
            return selected;
        }

        // In a small battle one fighter far above the players decides it alone.
        private static void ReplaceFarOpponents(
            List<DikozemieArenaOpponent> selected,
            IReadOnlyList<DikozemieArenaOpponent> opponents,
            int playerRating)
        {
            long Distance(DikozemieArenaOpponent opponent) =>
                Math.Abs((long)GetCurrentRating(opponent) - playerRating);

            for (int index = 0; index < selected.Count; index++)
            {
                if (selected[index] == null ||
                    Distance(selected[index]) <= (long)RatingSelectionRadius)
                {
                    continue;
                }
                DikozemieArenaOpponent nearer = opponents
                    .Where(opponent => opponent != null &&
                        !selected.Contains(opponent))
                    .OrderBy(Distance)
                    .FirstOrDefault();
                if (nearer != null &&
                    Distance(nearer) < Distance(selected[index]))
                {
                    selected[index] = nearer;
                }
            }
        }

        internal static int GetCurrentRating(DikozemieArenaOpponent opponent)
        {
            if (opponent == null)
            {
                return RatingStep;
            }
            int minimumRating = GetStartingRating(opponent.StartingRating);
#if CLIENT
            return Math.Max(minimumRating, DikozemieArmoryProgression.GetBotRating(
                opponent.CharacterId,
                minimumRating));
#else
            return DikozemieArenaRatingStore.GetBot(
                opponent.CharacterId,
                opponent.DisplayName ?? opponent.CharacterId,
                minimumRating).Rating;
#endif
        }

        internal static bool HasMount(
            DikozemieArenaOpponent opponent,
            BasicCharacterObject fallback)
        {
            if (opponent?.IsMounted == true)
            {
                return true;
            }
            BasicCharacterObject character = ResolveCharacter(opponent, fallback);
            return CharacterHasMount(character);
        }

        internal static bool HasRangedWeapon(
            DikozemieArenaOpponent opponent,
            BasicCharacterObject fallback)
        {
            BasicCharacterObject character = ResolveCharacter(opponent, fallback);
            return CharacterHasRangedWeapon(character);
        }

        internal static bool CharacterHasBowOrCrossbow(
            DikozemieArenaOpponent opponent,
            BasicCharacterObject fallback)
        {
            BasicCharacterObject character = ResolveCharacter(opponent, fallback);
            if (character == null)
            {
                return false;
            }
            // Team spawning uses FirstBattleEquipment. Match the composition
            // filter to the equipment that will actually enter the mission;
            // an unused alternate loadout must not classify a melee spawn as
            // an archer.
            return EquipmentHasBowOrCrossbow(
                character.FirstBattleEquipment ?? character.Equipment);
        }

        internal static bool MatchesComposition(
            DikozemieArenaOpponent opponent,
            BasicCharacterObject fallback,
            DikozemieArenaDiscipline discipline,
            DikozemieArenaComposition composition)
        {
            bool hasBowOrCrossbow = CharacterHasBowOrCrossbow(
                opponent,
                fallback);
            switch (composition)
            {
                case DikozemieArenaComposition.Melee:
                    // Throwing weapons deliberately remain legal: only bows
                    // and crossbows define the ranged-only roster.
                    return !hasBowOrCrossbow;
                case DikozemieArenaComposition.Ranged:
                    return hasBowOrCrossbow;
                default:
                    return true;
            }
        }

        private static bool CharacterHasMount(BasicCharacterObject character)
        {
            if (character == null)
            {
                return false;
            }
            foreach (Equipment equipment in character.BattleEquipments)
            {
                if (equipment?.Horse.Item != null)
                {
                    return true;
                }
            }
            Equipment fallbackEquipment = character.FirstBattleEquipment ??
                character.Equipment;
            return fallbackEquipment != null &&
                fallbackEquipment.Horse.Item != null;
        }

        private static bool CharacterHasRangedWeapon(
            BasicCharacterObject character)
        {
            if (character == null)
            {
                return false;
            }
            foreach (Equipment equipment in character.BattleEquipments)
            {
                if (EquipmentHasRangedWeapon(equipment))
                {
                    return true;
                }
            }
            return EquipmentHasRangedWeapon(
                character.FirstBattleEquipment ?? character.Equipment);
        }

        private static bool EquipmentHasRangedWeapon(Equipment equipment)
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
                if (item == null)
                {
                    continue;
                }
                switch (item.ItemType)
                {
                    case ItemObject.ItemTypeEnum.Bow:
                    case ItemObject.ItemTypeEnum.Crossbow:
                    case ItemObject.ItemTypeEnum.Thrown:
                        return true;
                }
            }
            return false;
        }

        internal static bool EquipmentHasBowOrCrossbow(Equipment equipment)
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
                if (item?.ItemType == ItemObject.ItemTypeEnum.Bow ||
                    item?.ItemType == ItemObject.ItemTypeEnum.Crossbow)
                {
                    return true;
                }
            }
            return false;
        }

        private static int GetStartingRating(int configuredRating)
        {
            // The catalog position is the NPC's permanent identity. Runtime
            // substitution and mount equipment must never rewrite the ladder.
            return Math.Max(RatingStep, configuredRating);
        }

        internal static BasicCharacterObject ResolveCharacter(
            DikozemieArenaOpponent opponent,
            BasicCharacterObject fallback)
        {
            if (opponent == null || string.IsNullOrWhiteSpace(opponent.CharacterId))
            {
                return fallback;
            }

            return opponent.Character ??
                MBObjectManager.Instance.GetObject<BasicCharacterObject>(
                    opponent.CharacterId) ??
                fallback;
        }

        internal static List<DikozemieArenaLeaderboardEntry> BuildLeaderboardSeed()
        {
            var result = new List<DikozemieArenaLeaderboardEntry>();
            if (!TryLoadCatalog(out DikozemieArenaOpponentCatalogFile catalog))
            {
                return result;
            }

            // The rating table is the complete physical catalog, including
            // entries that are not eligible for matchmaking. This deliberately
            // mirrors EnsureLeaderboardSeeded on the dedicated server instead
            // of the filtered opponent list used to choose a combatant.
            foreach (DikozemieArenaOpponentCatalogEntry entry in catalog.Opponents)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.CharacterId) ||
                    entry.Rating < 1)
                {
                    continue;
                }

                BasicCharacterObject character = MBObjectManager.Instance
                    .GetObject<BasicCharacterObject>(entry.CharacterId);
                result.Add(new DikozemieArenaLeaderboardEntry
                {
                    CharacterId = "bot:" + entry.CharacterId.Trim(),
                    DisplayName = character?.Name?.ToString() ?? entry.CharacterId,
                    Rating = GetStartingRating(entry.Rating),
                    Wins = 0,
                    Losses = 0
                });
            }

            int rank = 0;
            return result
                .OrderByDescending(entry => entry.Rating)
                .ThenByDescending(entry => entry.Wins)
                .ThenBy(entry => entry.DisplayName)
                .Select(entry =>
                {
                    entry.Rank = ++rank;
                    return entry;
                })
                .ToList();
        }

        private static void AddNativeMultiplayerOpponents(
            List<DikozemieArenaOpponent> opponents,
            BasicCharacterObject fallback)
        {
            var knownCharacterIds = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < opponents.Count; index++)
            {
                if (!string.IsNullOrWhiteSpace(opponents[index].CharacterId))
                {
                    knownCharacterIds.Add(opponents[index].CharacterId);
                }
            }

            List<BasicCharacterObject> nativeCharacters =
                GetNativeMultiplayerCharacters(fallback);
            int nativeRank = 0;
            for (int index = 0; index < nativeCharacters.Count; index++)
            {
                BasicCharacterObject character = nativeCharacters[index];
                nativeRank++;
                if (!knownCharacterIds.Add(character.StringId))
                {
                    continue;
                }

                int startingRating = Math.Max(
                    RatingStep,
                    nativeRank * NativeOpponentRatingStep);
                bool isMounted = CharacterHasMount(character);
                opponents.Add(new DikozemieArenaOpponent
                {
                    CharacterId = character.StringId,
                    DisplayName = character.Name?.ToString() ?? character.StringId,
                    StartingRating = isMounted
                        ? Math.Max(MinimumMountedOpponentRating, startingRating)
                        : startingRating,
                    Strength = startingRating / (double)RatingStep,
                    Character = character,
                    IsMounted = isMounted
                });
            }
        }

        private static List<BasicCharacterObject> GetNativeMultiplayerCharacters(
            BasicCharacterObject fallback)
        {
            var nativeCharacters = new List<BasicCharacterObject>();
            foreach (BasicCharacterObject character in
                     MBObjectManager.Instance
                         .GetObjectTypeList<BasicCharacterObject>())
            {
                string characterId = character?.StringId;
                if (string.IsNullOrWhiteSpace(characterId) ||
                    !characterId.StartsWith(
                        "mp_",
                        StringComparison.OrdinalIgnoreCase) ||
                    !IsEligibleCharacterId(characterId) ||
                    character.FirstBattleEquipment == null &&
                    character.Equipment == null)
                {
                    continue;
                }

                nativeCharacters.Add(character);
            }

            if (nativeCharacters.Count == 0 && fallback != null)
            {
                nativeCharacters.Add(fallback);
            }
            nativeCharacters.Sort((left, right) => string.Compare(
                left.StringId,
                right.StringId,
                StringComparison.OrdinalIgnoreCase));
            return nativeCharacters;
        }

        private static BasicCharacterObject SelectRuntimeCharacter(
            IReadOnlyList<BasicCharacterObject> runtimeCharacters,
            string catalogCharacterId,
            int configuredRating,
            int maximumConfiguredRating,
            BasicCharacterObject fallback)
        {
            if (runtimeCharacters == null || runtimeCharacters.Count == 0)
            {
                return fallback;
            }

            // Keep the visual mapping stable across runs without relying on
            // string.GetHashCode, whose implementation can vary by runtime.
            int hash = 17;
            string value = catalogCharacterId ?? string.Empty;
            for (int index = 0; index < value.Length; index++)
            {
                hash = unchecked((hash * 31) + value[index]);
            }
            // A missing low-ranked civilian must never turn into a banner
            // knight merely because its id hashes to an elite multiplayer
            // class. Preserve the catalog's strength percentile, then use the
            // stable hash only to vary characters inside the nearby tier.
            double ratingRange = Math.Max(
                1d,
                maximumConfiguredRating - RatingStep);
            double percentile = Math.Max(
                0d,
                Math.Min(
                    1d,
                    (Math.Max(RatingStep, configuredRating) - RatingStep) /
                        ratingRange));
            int center = (int)Math.Round(
                percentile * (runtimeCharacters.Count - 1));
            int radius = Math.Min(2, Math.Max(0,
                (runtimeCharacters.Count - 1) / 20));
            int minimumIndex = Math.Max(0, center - radius);
            int maximumIndex = Math.Min(
                runtimeCharacters.Count - 1,
                center + radius);
            int tierSize = maximumIndex - minimumIndex + 1;
            int runtimeIndex = minimumIndex +
                ((hash & int.MaxValue) % tierSize);
            return runtimeCharacters[runtimeIndex];
        }

        private static List<BasicCharacterObject>
            OrderRuntimeCharactersByStrength(
                IEnumerable<BasicCharacterObject> runtimeCharacters)
        {
            return (runtimeCharacters ??
                    Enumerable.Empty<BasicCharacterObject>())
                .Where(character => character != null)
                .OrderBy(character => character.GetBattleTier())
                .ThenBy(character => character.Level)
                .ThenBy(character => character.IsMounted)
                .ThenBy(character => character.IsRanged)
                .ThenBy(character => character.StringId,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsEligibleCharacterId(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId) ||
                string.Equals(
                    characterId,
                    "mp_character",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string[] excludedFragments =
            {
                "child", "infant", "toddler", "teenager", "dummy",
                "unarmed_ai", "main_hero", "player_char_creation",
                "crazy_man_test", "cutscene", "gear_practice"
            };
            for (int index = 0; index < excludedFragments.Length; index++)
            {
                if (characterId.IndexOf(
                        excludedFragments[index],
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool TryLoadCatalog(
            out DikozemieArenaOpponentCatalogFile catalog)
        {
            if (!DikozemieArenaStateFiles.TryRead(CatalogPath, out catalog) ||
                catalog == null ||
                catalog.SchemaVersion != 1 ||
                catalog.SelectionRadius != RatingSelectionRadius ||
                catalog.Opponents == null ||
                catalog.Opponents.Count == 0)
            {
                catalog = null;
                Console.WriteLine(
                    "DikozemieOnline: arena opponent catalog is unavailable: " +
                    CatalogPath);
                return false;
            }

            return true;
        }
    }
}
