using System;
using System.Collections.Generic;
using System.Linq;

namespace Dikozemie.Online
{
    /// <summary>
    /// Shared arena progression rules. The dedicated server advances its
    /// in-memory profile before the next duel, while the client persists the
    /// same server-authoritative raw experience in the hero save.
    /// </summary>
    internal static class DikozemieArenaCombatProgression
    {
        // DefaultCharacterDevelopmentModel in Bannerlord 1.4.5 builds its
        // skill XP table through index 1024. Keep multiplayer persistence and
        // the authoritative server on the same native ceiling.
        internal const int MaximumSkillLevel = 1024;

        private static readonly HashSet<string> CombatSkillIds =
            new HashSet<string>(new[]
            {
                "OneHanded", "TwoHanded", "Polearm",
                "Bow", "Crossbow", "Throwing",
                "Riding", "Athletics"
            }, StringComparer.Ordinal);

        internal static bool IsCombatSkill(string skillId) =>
            !string.IsNullOrWhiteSpace(skillId) && CombatSkillIds.Contains(skillId);

        internal static int ExperienceForNextLevel(int level)
        {
            int safeLevel = Math.Max(0, level);
            return 30 + 10 * safeLevel + safeLevel * (safeLevel + 1) / 2;
        }

        internal static float GetLearningRate(int attribute, int focus, int skill)
        {
            int safeAttribute = Math.Max(1, Math.Min(10, attribute));
            int safeFocus = Math.Max(0, Math.Min(5, focus));
            int safeSkill = Math.Max(0, skill);
            int learningLimit = Math.Max(
                0,
                (safeAttribute - 1) * 10 + safeFocus * 30);
            float factor = 1f + 0.4f * safeAttribute + safeFocus;
            if (safeSkill > learningLimit)
            {
                factor -= 1f + 0.1f * (safeSkill - learningLimit);
            }
            return Math.Max(0f, 1.25f * factor);
        }

        internal static void Apply(
            DikozemieArenaCharacterProfile profile,
            IReadOnlyDictionary<string, int> rawExperience)
        {
            if (profile == null || rawExperience == null)
            {
                return;
            }

            var skills = (profile.Skills ?? Array.Empty<DikozemieArenaSkillValue>())
                .Where(value => value != null)
                .ToList();
            var experience =
                (profile.SkillExperience ?? Array.Empty<DikozemieArenaSkillValue>())
                .Where(value => value != null)
                .ToList();

            foreach (KeyValuePair<string, int> award in rawExperience)
            {
                if (!IsCombatSkill(award.Key) || award.Value <= 0)
                {
                    continue;
                }

                DikozemieArenaSkillValue skillValue = FindOrAdd(skills, award.Key);
                DikozemieArenaSkillValue experienceValue =
                    FindOrAdd(experience, award.Key);
                int attribute = GetDevelopmentValue(
                    profile.Attributes,
                    GetAttributeId(award.Key),
                    2);
                int focus = GetDevelopmentValue(profile.Focus, award.Key, 0);
                int learned = Math.Max(
                    0,
                    (int)Math.Round(
                        Math.Min(5000, award.Value) *
                        GetLearningRate(attribute, focus, skillValue.Value)));
                long accumulated = (long)Math.Max(0, experienceValue.Value) + learned;
                while (skillValue.Value < MaximumSkillLevel &&
                    accumulated >= ExperienceForNextLevel(skillValue.Value))
                {
                    accumulated -= ExperienceForNextLevel(skillValue.Value);
                    skillValue.Value++;
                }
                experienceValue.Value = skillValue.Value >= MaximumSkillLevel
                    ? 0
                    : (int)Math.Min(int.MaxValue, accumulated);
            }

            profile.Skills = skills.ToArray();
            profile.SkillExperience = experience.ToArray();
        }

        private static DikozemieArenaSkillValue FindOrAdd(
            List<DikozemieArenaSkillValue> values,
            string skillId)
        {
            DikozemieArenaSkillValue value = values.FirstOrDefault(candidate =>
                string.Equals(candidate.SkillId, skillId, StringComparison.Ordinal));
            if (value != null)
            {
                value.Value = Math.Max(0, value.Value);
                return value;
            }
            value = new DikozemieArenaSkillValue
            {
                SkillId = skillId,
                Value = 0
            };
            values.Add(value);
            return value;
        }

        private static int GetDevelopmentValue(
            IEnumerable<DikozemieArenaDevelopmentValue> values,
            string id,
            int fallback)
        {
            DikozemieArenaDevelopmentValue value = values?.FirstOrDefault(candidate =>
                candidate != null && string.Equals(
                    candidate.Id,
                    id,
                    StringComparison.OrdinalIgnoreCase));
            return value == null ? fallback : Math.Max(0, value.Value);
        }

        private static string GetAttributeId(string skillId)
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
                default:
                    return "endurance";
            }
        }
    }
}
