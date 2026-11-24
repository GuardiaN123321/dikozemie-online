using System;
using System.Collections.Generic;
using System.Linq;

namespace Dikozemie.Online
{
    internal enum DikozemieArenaTeamOutcome
    {
        InProgress,
        AttackerVictory,
        DefenderVictory,
        Draw
    }

    internal static class DikozemieArenaTeamRules
    {
        internal const int MaximumStandardTeamSize = 5;
        internal const int MaximumTeamSize = 100;
        internal const int VictoryCoinReward = 1;
        internal const int HigherRatedTeamRewardStep = 1000;

        internal static bool IsTeamSizeSupported(
            int teamSize,
            DikozemieArenaDiscipline discipline)
        {
            if (teamSize >= 1 && teamSize <= MaximumStandardTeamSize)
            {
                return true;
            }
            return teamSize == 6 || teamSize == 10 || teamSize == 15 ||
                teamSize == 20 || teamSize == 25 || teamSize == 50 ||
                teamSize == 75 || teamSize == MaximumTeamSize;
        }

        internal static int NormalizeTeamSize(
            int requestedTeamSize,
            DikozemieArenaDiscipline discipline)
        {
            if (IsTeamSizeSupported(requestedTeamSize, discipline))
            {
                return requestedTeamSize;
            }
            if (requestedTeamSize >= MaximumTeamSize)
            {
                return MaximumTeamSize;
            }
            if (requestedTeamSize >= 75)
            {
                return 75;
            }
            if (requestedTeamSize >= 50)
            {
                return 50;
            }
            if (requestedTeamSize >= 25)
            {
                return 25;
            }
            if (requestedTeamSize >= 20)
            {
                return 20;
            }
            if (requestedTeamSize >= 15)
            {
                return 15;
            }
            if (requestedTeamSize >= 10)
            {
                return 10;
            }
            if (requestedTeamSize >= 6)
            {
                return 6;
            }
            return Math.Max(1, requestedTeamSize);
        }

        internal static DikozemieArenaTeamOutcome Resolve(
            int attackersAlive,
            int defendersAlive)
        {
            int attackers = Math.Max(0, attackersAlive);
            int defenders = Math.Max(0, defendersAlive);
            if (attackers > 0 && defenders > 0)
            {
                return DikozemieArenaTeamOutcome.InProgress;
            }
            if (attackers > 0)
            {
                return DikozemieArenaTeamOutcome.AttackerVictory;
            }
            if (defenders > 0)
            {
                return DikozemieArenaTeamOutcome.DefenderVictory;
            }
            return DikozemieArenaTeamOutcome.Draw;
        }

        internal static int CalculateVictoryCoinReward(
            int friendlyAverageRating,
            int enemyAverageRating)
        {
            int ratingGap = Math.Max(
                0,
                Math.Max(0, enemyAverageRating) -
                    Math.Max(0, friendlyAverageRating));
            return VictoryCoinReward +
                ratingGap / HigherRatedTeamRewardStep;
        }

        internal static int GetLossCoinPenalty(int teamSize)
        {
            teamSize = Math.Max(1, teamSize);
            if (teamSize >= 100) return 10;
            if (teamSize >= 75) return 9;
            if (teamSize >= 50) return 8;
            if (teamSize >= 25) return 7;
            if (teamSize >= 20) return 6;
            if (teamSize >= 15) return 5;
            if (teamSize >= 10) return 4;
            if (teamSize >= 6) return 3;
            if (teamSize >= 5) return 2;
            return 1;
        }

        // Stage 5 (plan «Общий мир»): a battle group fights alone. Two
        // players belong to the same battle when both have no group or
        // both carry the same one.
        internal static bool SameBattleGroup(string first, string second) =>
            string.Equals(first ?? string.Empty, second ?? string.Empty, StringComparison.Ordinal);

        // The group still waits for its members while some are missing and
        // its two minutes are not over.
        internal static bool IsBattleGroupWaiting(int present, int expected, bool beforeDeadline) =>
            beforeDeadline && present < Math.Max(1, expected);

        internal static int CalculateLossCoinPenalty(
            int teamSize,
            int seriesLosses)
        {
            return GetLossCoinPenalty(teamSize) * Math.Max(0, seriesLosses);
        }

        internal static int GetPersonalKillRewardMultiplier(
            DikozemieArenaDiscipline discipline,
            DikozemieArenaComposition composition,
            bool killerMounted,
            bool victimMounted,
            bool killWasMelee,
            bool victimHadBowOrCrossbow) =>
            (discipline == DikozemieArenaDiscipline.Open &&
                !killerMounted && victimMounted) ||
            (composition == DikozemieArenaComposition.Mixed &&
                killWasMelee && victimHadBowOrCrossbow)
                ? 2
                : 1;

        internal static bool[] BuildBalancedTeamAssignment(
            IReadOnlyList<int> ratings,
            int friendlyBotCapacity,
            int enemyBotCapacity,
            int playerRating)
        {
            return BuildBalancedTeamAssignment(
                ratings,
                null,
                friendlyBotCapacity,
                enemyBotCapacity,
                playerRating);
        }

        internal static bool[] BuildBalancedTeamAssignment(
            IReadOnlyList<int> ratings,
            IReadOnlyList<bool> rangedRoles,
            int friendlyBotCapacity,
            int enemyBotCapacity,
            int playerRating)
        {
            return BuildBalancedTeamAssignment(
                ratings,
                rangedRoles,
                friendlyBotCapacity,
                enemyBotCapacity,
                playerRating,
                0);
        }

        // Live players already stand on both sides: their summed ratings are
        // the starting totals, and the bots only top up the weaker side.
        internal static bool[] BuildBalancedTeamAssignment(
            IReadOnlyList<int> ratings,
            IReadOnlyList<bool> rangedRoles,
            int friendlyBotCapacity,
            int enemyBotCapacity,
            long friendlyHumanRating,
            long enemyHumanRating)
        {
            int count = ratings?.Count ?? 0;
            var friendly = new bool[count];
            int friendlyCapacity = Math.Max(0, friendlyBotCapacity);
            int enemyCapacity = Math.Max(0, enemyBotCapacity);
            int friendlyCount = 0;
            int enemyCount = 0;
            long friendlyTotal = Math.Max(0, friendlyHumanRating);
            long enemyTotal = Math.Max(0, enemyHumanRating);

            bool balanceRoles = rangedRoles != null &&
                rangedRoles.Count == count && count > 1;
            int rangedCount = balanceRoles
                ? rangedRoles.Count(value => value)
                : 0;
            int meleeCount = count - rangedCount;
            int friendlyRangedCapacity = 0;
            int friendlyMeleeCapacity = friendlyCapacity;
            int enemyRangedCapacity = 0;
            int enemyMeleeCapacity = enemyCapacity;
            int friendlyRangedCount = 0;
            int friendlyMeleeCount = 0;
            int enemyRangedCount = 0;
            int enemyMeleeCount = 0;
            if (balanceRoles)
            {
                int totalCapacity = Math.Max(
                    1,
                    friendlyCapacity + enemyCapacity);
                int minimumFriendlyRanged = Math.Max(
                    Math.Max(0, rangedCount - enemyCapacity),
                    friendlyCapacity - meleeCount);
                int maximumFriendlyRanged = Math.Min(
                    friendlyCapacity,
                    rangedCount);
                int idealFriendlyRanged = (int)Math.Round(
                    rangedCount * (friendlyCapacity / (double)totalCapacity));
                friendlyRangedCapacity = Math.Max(
                    minimumFriendlyRanged,
                    Math.Min(maximumFriendlyRanged, idealFriendlyRanged));
                friendlyMeleeCapacity =
                    friendlyCapacity - friendlyRangedCapacity;
                enemyRangedCapacity = rangedCount - friendlyRangedCapacity;
                enemyMeleeCapacity = meleeCount - friendlyMeleeCapacity;
            }

            IEnumerable<int> strongestFirst = Enumerable.Range(0, count)
                .OrderByDescending(index => Math.Max(0, ratings[index]));
            foreach (int index in strongestFirst)
            {
                bool assignFriendly;
                if (friendlyCount >= friendlyCapacity)
                {
                    assignFriendly = false;
                }
                else if (enemyCount >= enemyCapacity)
                {
                    assignFriendly = true;
                }
                else if (balanceRoles)
                {
                    bool ranged = rangedRoles[index];
                    int friendlyRoleCount = ranged
                        ? friendlyRangedCount
                        : friendlyMeleeCount;
                    int enemyRoleCount = ranged
                        ? enemyRangedCount
                        : enemyMeleeCount;
                    int friendlyRoleCapacity = ranged
                        ? friendlyRangedCapacity
                        : friendlyMeleeCapacity;
                    int enemyRoleCapacity = ranged
                        ? enemyRangedCapacity
                        : enemyMeleeCapacity;
                    if (friendlyRoleCount >= friendlyRoleCapacity)
                    {
                        assignFriendly = false;
                    }
                    else if (enemyRoleCount >= enemyRoleCapacity)
                    {
                        assignFriendly = true;
                    }
                    else
                    {
                        assignFriendly = friendlyTotal <= enemyTotal;
                    }
                }
                else
                {
                    // Both completed teams have the same head count. Draft the
                    // next strongest fighter to the currently weaker total so
                    // a shuffled run cannot put every elite on one side.
                    assignFriendly = friendlyTotal <= enemyTotal;
                }

                friendly[index] = assignFriendly;
                if (assignFriendly)
                {
                    friendlyCount++;
                    friendlyTotal += Math.Max(0, ratings[index]);
                    if (balanceRoles)
                    {
                        if (rangedRoles[index]) friendlyRangedCount++;
                        else friendlyMeleeCount++;
                    }
                }
                else
                {
                    enemyCount++;
                    enemyTotal += Math.Max(0, ratings[index]);
                    if (balanceRoles)
                    {
                        if (rangedRoles[index]) enemyRangedCount++;
                        else enemyMeleeCount++;
                    }
                }
            }
            return friendly;
        }

        // Splits the live players of one team battle between the two sides:
        // strongest group first, each to the side with fewer people (then the
        // lower rating total), never more than one side's capacity. A group is
        // one lobby party, which stays on one side while it fits; a player
        // without a party (empty key) is a group alone. Returns true for the
        // attacker side.
        internal static bool[] AssignHumanSides(
            IReadOnlyList<int> ratings,
            int capacityPerSide,
            IReadOnlyList<string> partyKeys = null)
        {
            int count = ratings?.Count ?? 0;
            var attacker = new bool[count];
            int capacity = Math.Max(1, capacityPerSide);
            int attackers = 0;
            int defenders = 0;
            long attackerTotal = 0;
            long defenderTotal = 0;
            IEnumerable<List<int>> groups = Enumerable.Range(0, count)
                .GroupBy(index =>
                    string.IsNullOrEmpty(partyKeys?[index])
                        ? "#" + index
                        : partyKeys[index])
                .Select(group => group.ToList())
                .OrderByDescending(group =>
                    group.Sum(index => (long)Math.Max(0, ratings[index])))
                .ThenBy(group => group[0]);
            foreach (List<int> group in groups)
            {
                bool toAttacker = attackers < defenders ||
                    (attackers == defenders && attackerTotal <= defenderTotal);
                // A party prefers one side where it fits whole.
                if (toAttacker && attackers + group.Count > capacity &&
                    defenders + group.Count <= capacity)
                {
                    toAttacker = false;
                }
                else if (!toAttacker && defenders + group.Count > capacity &&
                    attackers + group.Count <= capacity)
                {
                    toAttacker = true;
                }
                foreach (int index in group)
                {
                    bool attackerPlace = attackers < capacity &&
                        (toAttacker || defenders >= capacity);
                    attacker[index] = attackerPlace;
                    if (attackerPlace)
                    {
                        attackers++;
                        attackerTotal += Math.Max(0, ratings[index]);
                    }
                    else
                    {
                        defenders++;
                        defenderTotal += Math.Max(0, ratings[index]);
                    }
                }
            }
            return attacker;
        }
    }
}
