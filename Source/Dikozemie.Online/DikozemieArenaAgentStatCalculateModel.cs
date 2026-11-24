using System;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    /// <summary>
    /// Keeps the network-safe MP character object while replacing the combat
    /// skill values with those captured from the matched campaign character.
    /// </summary>
    public sealed class DikozemieArenaAgentStatCalculateModel :
        MultiplayerAgentStatCalculateModel
    {
        public override void InitializeAgentStats(
            Agent agent,
            Equipment spawnEquipment,
            AgentDrivenProperties agentDrivenProperties,
            AgentBuildData agentBuildData)
        {
            base.InitializeAgentStats(agent, spawnEquipment, agentDrivenProperties, agentBuildData);

            if (agent == null || !agent.IsHuman)
            {
                if (agent?.IsMount == true)
                {
                    // SandboxAgentStatCalculateModel initializes every mount
                    // from Mission's item-derived BaseHealthLimit.  That value
                    // already includes the horse's monster hit points,
                    // extra_health and the mount item modifier.
                    agent.HealthLimit = agent.BaseHealthLimit;
                    agent.Health = agent.HealthLimit;
                    ApplySingleplayerMountStats(agent, agentDrivenProperties);
                }
                return;
            }

            WeaponComponentData activeWeapon = null;
            for (int slot = 0; slot < 4; slot++)
            {
                WeaponComponentData candidate =
                    spawnEquipment?[slot].Item?.PrimaryWeapon;
                if (candidate != null)
                {
                    activeWeapon = candidate;
                    break;
                }
            }
            DikozemieArenaNativeCombat.ApplyDrivenProperties(
                agent,
                activeWeapon,
                agentDrivenProperties);
        }

        public override void InitializeMissionEquipment(Agent agent)
        {
            base.InitializeMissionEquipment(agent);
            DikozemieArenaNativeCombat.ApplyMissionEquipment(agent);
        }

        public override void UpdateAgentStats(
            Agent agent,
            AgentDrivenProperties agentDrivenProperties)
        {
            base.UpdateAgentStats(agent, agentDrivenProperties);
            if (agent == null || !agent.IsHuman)
            {
                if (agent?.IsMount == true)
                {
                    ApplySingleplayerMountStats(agent, agentDrivenProperties);
                }
                return;
            }

            EquipmentIndex index = agent?.GetPrimaryWieldedItemIndex() ??
                EquipmentIndex.None;
            WeaponComponentData weapon = index == EquipmentIndex.None
                ? null
                : agent.Equipment[index].CurrentUsageItem;
            DikozemieArenaNativeCombat.ApplyDrivenProperties(
                agent,
                weapon,
                agentDrivenProperties);
        }

        private void ApplySingleplayerMountStats(
            Agent agent,
            AgentDrivenProperties agentDrivenProperties)
        {
            Equipment spawnEquipment = agent?.SpawnEquipment;
            if (spawnEquipment == null || agentDrivenProperties == null)
            {
                return;
            }

            EquipmentElement mount = spawnEquipment[EquipmentIndex.Horse];
            EquipmentElement harness = spawnEquipment[EquipmentIndex.HorseHarness];
            if (mount.Item?.HorseComponent == null)
            {
                return;
            }

            float armor = 0f;
            for (int slot = 1; slot < (int)EquipmentIndex.NumEquipmentSetSlots; slot++)
            {
                EquipmentElement element = spawnEquipment[slot];
                if (element.Item != null)
                {
                    armor += element.GetModifiedMountBodyArmor();
                }
            }
            agentDrivenProperties.ArmorTorso = armor;

            int modifiedManeuver = mount.GetModifiedMountManeuver(in harness);
            int modifiedSpeed = mount.GetModifiedMountSpeed(in harness) + 1;
            int ridingSkill = agent.RiderAgent == null
                ? 0
                : Math.Max(0, GetEffectiveSkill(agent.RiderAgent, DefaultSkills.Riding));

            if (agent.RiderAgent == null)
            {
                agentDrivenProperties.MountManeuver = modifiedManeuver;
                agentDrivenProperties.MountSpeed =
                    GetEnvironmentSpeedFactor(agent) * 0.22f * (1f + modifiedSpeed);
            }
            else
            {
                // Installed Bannerlord 1.2.12 DefaultSkillEffects:
                // HorseManeuver +0.0004 factor and HorseSpeed +0.002 factor
                // per point of Riding.  Multiplayer's 0.0035/0.0032 preset is
                // intentionally replaced with the native campaign values.
                float maneuverFactor = 1f + ridingSkill * 0.0004f;
                float speedFactor = 1f + ridingSkill * 0.002f;
                agentDrivenProperties.MountManeuver =
                    modifiedManeuver * maneuverFactor;
                agentDrivenProperties.MountSpeed =
                    GetEnvironmentSpeedFactor(agent) * 0.22f *
                    (1f + modifiedSpeed * speedFactor);
            }

            float mountWeight = mount.Weight / 2f +
                (harness.IsEmpty ? 0f : harness.Weight);
            agentDrivenProperties.MountDashAccelerationMultiplier =
                mountWeight > 200f
                    ? (mountWeight < 300f
                        ? 1f - (mountWeight - 200f) / 111f
                        : 0.1f)
                    : 1f;
            agentDrivenProperties.TopSpeedReachDuration =
                Game.Current.BasicModels.RidingModel.CalculateAcceleration(
                    in mount,
                    in harness,
                    ridingSkill);
            agentDrivenProperties.MountChargeDamage =
                mount.GetModifiedMountCharge(in harness) * 0.004f;
            agentDrivenProperties.MountDifficulty = mount.Item.Difficulty;
        }

        public override float GetWeaponDamageMultiplier(
            Agent agent,
            WeaponComponentData weapon)
        {
            return DikozemieArenaNativeCombat.GetWeaponDamageMultiplier(
                agent,
                weapon);
        }

        public override float GetWeaponInaccuracy(
            Agent agent,
            WeaponComponentData weapon,
            int weaponSkill)
        {
            return DikozemieArenaNativeCombat.CalculateWeaponInaccuracy(
                weapon,
                weaponSkill);
        }

        public override float GetKnockBackResistance(Agent agent)
        {
            return agent != null && agent.IsHuman
                ? 0.15f + GetEffectiveSkill(agent, DefaultSkills.Athletics) * 0.001f
                : float.MaxValue;
        }

        public override float GetKnockDownResistance(
            Agent agent,
            StrikeType strikeType = StrikeType.Invalid)
        {
            if (agent == null || !agent.IsHuman)
            {
                return float.MaxValue;
            }
            float result = 0.4f +
                GetEffectiveSkill(agent, DefaultSkills.Athletics) * 0.001f;
            if (agent.HasMount)
            {
                result += 0.1f;
            }
            else if (strikeType == StrikeType.Thrust)
            {
                result += 0.15f;
            }
            return result;
        }

        public override float GetDismountResistance(Agent agent)
        {
            return agent != null && agent.IsHuman
                ? 0.4f + GetEffectiveSkill(agent, DefaultSkills.Riding) * 0.001f
                : float.MaxValue;
        }

        public override float GetInteractionDistance(Agent agent)
        {
            return DikozemieArenaNativeCombat.GetInteractionDistance(agent);
        }

        public override float GetMaxCameraZoom(Agent agent)
        {
            return DikozemieArenaNativeCombat.GetMaxCameraZoom(agent);
        }

        public override int GetEffectiveSkill(Agent agent, SkillObject skill)
        {
            if (TryGetProfile(agent, out DikozemieArenaCharacterProfile profile))
            {
                return GetProfileSkill(profile, skill, base.GetEffectiveSkill(agent, skill));
            }
#if CLIENT
            if (agent != null && agent == Mission.Current?.MainAgent && skill != null)
            {
                return DikozemieHeroProfile.GetEffectiveSkill(
                    skill.StringId,
                    base.GetEffectiveSkill(agent, skill));
            }
#endif
            return base.GetEffectiveSkill(agent, skill);
        }

        public override int GetEffectiveSkillForWeapon(
            Agent agent,
            WeaponComponentData weapon)
        {
            if (weapon?.RelevantSkill != null &&
                TryGetProfile(agent, out DikozemieArenaCharacterProfile profile))
            {
                return GetProfileSkill(
                    profile,
                    weapon.RelevantSkill,
                    base.GetEffectiveSkillForWeapon(agent, weapon));
            }
#if CLIENT
            if (agent != null && agent == Mission.Current?.MainAgent &&
                weapon?.RelevantSkill != null)
            {
                return DikozemieHeroProfile.GetEffectiveSkill(
                    weapon.RelevantSkill.StringId,
                    base.GetEffectiveSkillForWeapon(agent, weapon));
            }
#endif
            return base.GetEffectiveSkillForWeapon(agent, weapon);
        }

        private static bool TryGetProfile(
            Agent agent,
            out DikozemieArenaCharacterProfile profile)
        {
            return DikozemieArenaServerRegistration.TryGetCharacterProfile(
                agent?.MissionPeer?.GetNetworkPeer(),
                out profile);
        }

        private static int GetProfileSkill(
            DikozemieArenaCharacterProfile profile,
            SkillObject skill,
            int fallback)
        {
            if (profile?.Skills == null || skill == null)
            {
                return Math.Max(0, fallback);
            }

            DikozemieArenaSkillValue saved = profile.Skills.FirstOrDefault(candidate =>
                candidate != null && string.Equals(
                    candidate.SkillId,
                    skill.StringId,
                    StringComparison.OrdinalIgnoreCase));
            return saved == null ? Math.Max(0, fallback) : Math.Max(0, saved.Value);
        }
    }
}
