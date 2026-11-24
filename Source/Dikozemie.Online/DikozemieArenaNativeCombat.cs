using System;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    /// <summary>
    /// Multiplayer-safe counterparts of the personal combat calculations in
    /// SandboxAgentStatCalculateModel and SandboxAgentApplyDamageModel.  The
    /// dedicated server cannot reference CampaignSystem/SandBox, so the
    /// installed 1.2.12 coefficients and personal Throwing branch live here.
    /// Party, captain and governor effects are intentionally excluded from a
    /// one-on-one arena profile.
    /// </summary>
    internal static class DikozemieArenaNativeCombat
    {
        private sealed class SkillFactor
        {
            internal SkillFactor(string skillId, float value)
            {
                SkillId = skillId;
                Value = value;
            }

            internal string SkillId { get; }
            internal float Value { get; }
        }

        private static readonly SkillFactor[] WeaponSpeedFactors =
        {
            new SkillFactor("OneHanded", 0.0007f),
            new SkillFactor("TwoHanded", 0.0006f),
            new SkillFactor("Polearm", 0.0006f),
            new SkillFactor("Throwing", 0.0007f),
            new SkillFactor("Crossbow", 0.0007f)
        };

        private static readonly SkillFactor[] WeaponDamageFactors =
        {
            new SkillFactor("OneHanded", 0.0015f),
            new SkillFactor("TwoHanded", 0.0016f),
            new SkillFactor("Polearm", 0.0007f),
            new SkillFactor("Bow", 0.0011f),
            new SkillFactor("Throwing", 0.0006f)
        };

        internal static void ApplyDrivenProperties(
            Agent agent,
            WeaponComponentData weapon,
            AgentDrivenProperties properties)
        {
            if (agent == null || !agent.IsHuman || properties == null)
            {
                return;
            }

            // Sandbox starts these personal weapon multipliers at one and
            // then applies only the native effect belonging to the weapon.
            properties.SwingSpeedMultiplier = 1f;
            properties.ThrustOrRangedReadySpeedMultiplier = 1f;
            properties.ReloadSpeed = 1f;
            properties.MissileSpeedMultiplier = 1f;

            if (weapon != null && weapon.RelevantSkill != null)
            {
                string skillId = weapon.RelevantSkill.StringId;
                int skill = GetSkill(
                    agent,
                    skillId,
                    agent.Character?.GetSkillValue(weapon.RelevantSkill) ?? 0);
                float speedFactor = FindFactor(WeaponSpeedFactors, skillId);

                if (string.Equals(skillId, "OneHanded", StringComparison.Ordinal) ||
                    string.Equals(skillId, "TwoHanded", StringComparison.Ordinal) ||
                    string.Equals(skillId, "Polearm", StringComparison.Ordinal))
                {
                    float speed = 1f + speedFactor * skill;
                    properties.SwingSpeedMultiplier = speed;
                    properties.ThrustOrRangedReadySpeedMultiplier = speed;
                }
                else if (string.Equals(skillId, "Crossbow", StringComparison.Ordinal))
                {
                    properties.ReloadSpeed = 1f + speedFactor * skill;
                }
                else if (string.Equals(skillId, "Throwing", StringComparison.Ordinal))
                {
                    properties.ThrustOrRangedReadySpeedMultiplier =
                        1f + speedFactor * skill;

                    if (HasPerk(agent, "ThrowingQuickDraw"))
                    {
                        properties.ReloadSpeed *= 1.2f;
                    }
                    if (HasPerk(agent, "ThrowingPerfectTechnique"))
                    {
                        properties.MissileSpeedMultiplier *= 1.25f;
                    }
                    if (HasPerk(agent, "ThrowingUnstoppableForce"))
                    {
                        properties.MissileSpeedMultiplier *=
                            1f + Math.Max(0, skill - 200) * 0.002f;
                    }
                    if (agent.HasMount &&
                        HasPerk(agent, "ThrowingMountedSkirmisher"))
                    {
                        properties.WeaponMaxMovementAccuracyPenalty *= 0.8f;
                        properties.WeaponMaxUnsteadyAccuracyPenalty *= 0.8f;
                    }
                }

                properties.WeaponInaccuracy =
                    CalculateWeaponInaccuracy(weapon, skill);
            }

            properties.CrouchedSpeedMultiplier =
                1f + GetSkill(
                    agent,
                    "Roguery",
                    agent.Character?.GetSkillValue(DefaultSkills.Roguery) ?? 0) *
                0.0005f;
            int ridingSkill = GetSkill(
                agent,
                "Riding",
                agent.Character?.GetSkillValue(DefaultSkills.Riding) ?? 0);
            properties.AttributeRiding = ridingSkill;

            if (agent.HasMount && weapon != null)
            {
                float speedPenalty = Math.Min(
                    0f,
                    -0.3f + ridingSkill * 0.003f);
                float speedMultiplier = Math.Max(0f, 1f + speedPenalty);
                properties.SwingSpeedMultiplier *= speedMultiplier;
                properties.ThrustOrRangedReadySpeedMultiplier *= speedMultiplier;
                properties.ReloadSpeed *= speedMultiplier;
                properties.WeaponBestAccuracyWaitTime *= 1f - speedPenalty;

                float inaccuracyPenalty = Math.Max(
                    0f,
                    5f - ridingSkill * 0.05f);
                properties.WeaponInaccuracy *= 1f + inaccuracyPenalty;
            }
        }

        internal static float GetWeaponDamageMultiplier(
            Agent agent,
            WeaponComponentData weapon)
        {
            string skillId = weapon?.RelevantSkill?.StringId;
            float factor = FindFactor(WeaponDamageFactors, skillId);
            int fallback = agent?.Character == null || weapon?.RelevantSkill == null
                ? 0
                : agent.Character.GetSkillValue(weapon.RelevantSkill);
            return Math.Max(
                0f,
                1f + factor * GetSkill(agent, skillId, fallback));
        }

        internal static float CalculateWeaponInaccuracy(
            WeaponComponentData weapon,
            int weaponSkill)
        {
            if (weapon == null)
            {
                return 0f;
            }
            if (weapon.IsRangedWeapon)
            {
                float factor = 1f;
                if (weapon.WeaponClass == WeaponClass.Sling)
                {
                    factor = Math.Max(1f - 0.007f * weaponSkill, 0.2f);
                }
                else if (weapon.RelevantSkill == DefaultSkills.Bow)
                {
                    factor = 1f - 0.0009f * weaponSkill;
                }
                else if (weapon.RelevantSkill == DefaultSkills.Crossbow)
                {
                    factor = 1f - 0.0005f * weaponSkill;
                }
                else if (weapon.RelevantSkill == DefaultSkills.Throwing)
                {
                    factor = 1f - 0.0006f * weaponSkill;
                }
                return Math.Max(0f,
                    (100f - weapon.Accuracy) * factor * 0.001f);
            }
            if (weapon.WeaponFlags.HasAllFlags(WeaponFlags.WideGrip))
            {
                return Math.Max(0f, 1f - weaponSkill * 0.01f);
            }
            return 0f;
        }

        internal static void ApplyMissionEquipment(Agent agent)
        {
            if (agent == null || !agent.IsHuman)
            {
                return;
            }

            int bonus = 0;
            if (HasPerk(agent, "ThrowingWellPrepared"))
            {
                bonus += 1;
            }
            if (HasPerk(agent, "ThrowingResourceful"))
            {
                bonus += 2;
            }
            if (agent.HasMount && HasPerk(agent, "ThrowingSaddlebags"))
            {
                bonus += 2;
            }
            if (bonus <= 0)
            {
                return;
            }

            for (int slot = 0; slot < 5; slot++)
            {
                var equipmentIndex = (EquipmentIndex)slot;
                MissionWeapon missionWeapon = agent.Equipment[equipmentIndex];
                WeaponComponentData usage = missionWeapon.CurrentUsageItem;
                if (!missionWeapon.IsEmpty && usage != null && usage.IsConsumable &&
                    usage.RelevantSkill == DefaultSkills.Throwing)
                {
                    agent.Equipment.SetAmountOfSlot(
                        equipmentIndex,
                        (short)Math.Min(short.MaxValue, missionWeapon.Amount + bonus),
                        true);
                }
            }
        }

        internal static float ApplyDamage(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            float baseDamage)
        {
            Agent attacker = attackInformation.AttackerAgent;
            WeaponComponentData weapon =
                attackInformation.AttackerWeapon.CurrentUsageItem;
            if (attacker == null || weapon?.RelevantSkill != DefaultSkills.Throwing)
            {
                return baseDamage;
            }

            float factor = 0f;
            bool missile = collisionData.IsMissile;
            if (!missile && HasPerk(attacker, "ThrowingFlexibleFighter"))
            {
                factor += 0.1f;
            }
            if (missile && collisionData.AttackBlockedWithShield)
            {
                if (HasPerk(attacker, "ThrowingShieldBreaker"))
                {
                    factor += 0.4f;
                }
                if (weapon.WeaponClass == WeaponClass.ThrowingAxe &&
                    HasPerk(attacker, "ThrowingSplinters"))
                {
                    factor += 3f;
                }
            }
            if (missile && attackInformation.IsVictimAgentMount &&
                HasPerk(attacker, "ThrowingHunter"))
            {
                factor += 0.4f;
            }
            if (missile && attackInformation.VictimHitPointRate <= 0.5f &&
                HasPerk(attacker, "ThrowingLastHit"))
            {
                factor += 0.5f;
            }
            if (missile && attackInformation.IsHeadShot &&
                HasPerk(attacker, "ThrowingHeadHunter"))
            {
                factor += 0.5f;
            }
            if (missile && HasPerk(attacker, "ThrowingRunningThrow"))
            {
                factor += Math.Max(0f,
                    collisionData.MovementSpeedDamageModifier) * 0.25f;
            }
            if (missile && HasPerk(attacker, "ThrowingSlingingCompetitions"))
            {
                factor += ArmorPenetrationDamageFactor(
                    attackInformation.ArmorAmountFloat,
                    0.2f);
            }
            if (missile && HasPerk(attacker, "ThrowingWeakSpot"))
            {
                factor += ArmorPenetrationDamageFactor(
                    attackInformation.ArmorAmountFloat,
                    0.3f);
            }
            if (missile && HasPerk(attacker, "ThrowingUnstoppableForce"))
            {
                int fallback = attacker.Character?.GetSkillValue(
                    DefaultSkills.Throwing) ?? 0;
                factor += Math.Max(
                        0,
                        GetSkill(attacker, "Throwing", fallback) - 200) *
                    0.005f;
            }
            return Math.Max(0f, baseDamage * (1f + factor));
        }

        internal static float ApplyDamageTaken(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            float baseDamage)
        {
            Agent victim = attackInformation.VictimAgent;
            baseDamage = ApplyArmorToMissile(in attackInformation, in collisionData, baseDamage);
            if (victim == null || !collisionData.IsMissile ||
                !HasPerk(victim, "ThrowingSkirmisher"))
            {
                return baseDamage;
            }

            WeaponComponentData heldWeapon =
                attackInformation.VictimMainHandWeapon.CurrentUsageItem;
            return heldWeapon?.RelevantSkill == DefaultSkills.Throwing
                ? Math.Max(0f, baseDamage * 0.9f)
                : baseDamage;
        }

        // Customer 2026-10-31: «нахрена ты тогда одевал всё это, чтобы тебя как
        // ватного прошибали стрелой». A shirt changes nothing, mail stops about
        // half, plate turns an arrow into a scratch, as in Kingdom Come. A bolt
        // goes through more than an arrow, and an unarmored spot stays deadly,
        // so archers keep their worth. Thrown weapons stay as the game has them.
        private const float ArrowArmorScale = 35f;
        private const float BoltArmorScale = 56f;

        internal static float MissileArmorFactor(float armor, bool bolt)
        {
            float scaled = Math.Max(0f, armor) / (bolt ? BoltArmorScale : ArrowArmorScale);
            return 1f / (1f + scaled * scaled);
        }

        private static float ApplyArmorToMissile(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            float baseDamage)
        {
            if (!collisionData.IsMissile || !attackInformation.IsVictimAgentHuman)
            {
                return baseDamage;
            }
            WeaponClass weaponClass = attackInformation.AttackerWeapon.CurrentUsageItem?.WeaponClass ??
                WeaponClass.Undefined;
            bool arrow = weaponClass == WeaponClass.Arrow || weaponClass == WeaponClass.Bow;
            bool bolt = weaponClass == WeaponClass.Bolt || weaponClass == WeaponClass.Crossbow;
            if (!arrow && !bolt)
            {
                return baseDamage;
            }
            return baseDamage * MissileArmorFactor(attackInformation.ArmorAmountFloat, bolt);
        }

        internal static float GetDismountPenetration(
            Agent attacker,
            WeaponComponentData weapon)
        {
            return weapon?.RelevantSkill == DefaultSkills.Throwing &&
                HasPerk(attacker, "ThrowingKnockOff")
                    ? 0.25f
                    : 0f;
        }

        internal static void DecideMissileWeaponFlags(
            Agent attacker,
            in MissionWeapon missileWeapon,
            ref WeaponFlags missileWeaponFlags)
        {
            if (missileWeapon.CurrentUsageItem?.WeaponClass == WeaponClass.Javelin &&
                HasPerk(attacker, "ThrowingImpale"))
            {
                missileWeaponFlags |= WeaponFlags.CanPenetrateShield;
            }
        }

        internal static float GetInteractionDistance(Agent agent)
        {
            return agent != null && agent.HasMount &&
                HasPerk(agent, "ThrowingLongReach")
                    ? 3f
                    : 1.5f;
        }

        internal static float GetMaxCameraZoom(Agent agent)
        {
            return HasPerk(agent, "ThrowingFocus") ? 1.25f : 1f;
        }

        internal static int GetSkill(Agent agent, string skillId, int fallback)
        {
            if (string.IsNullOrWhiteSpace(skillId))
            {
                return Math.Max(0, fallback);
            }
            if (TryGetProfile(agent, out DikozemieArenaCharacterProfile profile))
            {
                DikozemieArenaSkillValue saved = profile.Skills?.FirstOrDefault(
                    candidate => candidate != null && string.Equals(
                        candidate.SkillId,
                        skillId,
                        StringComparison.OrdinalIgnoreCase));
                return saved == null ? Math.Max(0, fallback) : Math.Max(0, saved.Value);
            }
#if CLIENT
            if (DikozemieRoadFight.TryGetSkill(agent, skillId, out int roadSkill))
            {
                return Math.Max(0, roadSkill);
            }
            if (agent != null && agent == Mission.Current?.MainAgent)
            {
                return DikozemieHeroProfile.GetEffectiveSkill(skillId, fallback);
            }
#endif
            return Math.Max(0, fallback);
        }

        internal static bool HasPerk(Agent agent, string perkId)
        {
            if (string.IsNullOrWhiteSpace(perkId))
            {
                return false;
            }
            if (TryGetProfile(agent, out DikozemieArenaCharacterProfile profile))
            {
                return profile.SelectedPerks != null &&
                    profile.SelectedPerks.Contains(perkId, StringComparer.Ordinal);
            }
#if CLIENT
            return agent != null && agent == Mission.Current?.MainAgent &&
                DikozemieHeroProfile.HasSelectedPerk(perkId);
#else
            return false;
#endif
        }

        private static bool TryGetProfile(
            Agent agent,
            out DikozemieArenaCharacterProfile profile)
        {
            return DikozemieArenaServerRegistration.TryGetCharacterProfile(
                agent?.MissionPeer?.GetNetworkPeer(),
                out profile);
        }

        private static float FindFactor(
            SkillFactor[] factors,
            string skillId)
        {
            SkillFactor factor = factors.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.SkillId,
                    skillId,
                    StringComparison.Ordinal));
            return factor?.Value ?? 0f;
        }

        private static float ArmorPenetrationDamageFactor(
            float armor,
            float penetration)
        {
            // Armor reduction follows this amplification stage. Scaling the
            // compensation by armor preserves the native perk's dependency on
            // armor rather than turning penetration into a flat damage bonus.
            float armorShare = Math.Max(0f, armor) /
                (Math.Max(0f, armor) + 100f);
            return penetration * armorShare;
        }
    }
}
