using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    public sealed class DikozemieArenaAgentApplyDamageModel :
        MultiplayerAgentApplyDamageModel
    {
        public override float ApplyDamageAmplifications(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            float baseDamage)
        {
            return DikozemieArenaNativeCombat.ApplyDamage(
                in attackInformation,
                in collisionData,
                base.ApplyDamageAmplifications(
                    in attackInformation,
                    in collisionData,
                    baseDamage));
        }

        public override float ApplyDamageReductions(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            float baseDamage)
        {
            return DikozemieArenaNativeCombat.ApplyDamageTaken(
                in attackInformation,
                in collisionData,
                base.ApplyDamageReductions(
                    in attackInformation,
                    in collisionData,
                    baseDamage));
        }

        public override float GetDismountPenetration(
            Agent attackerAgent,
            WeaponComponentData attackerWeapon,
            in Blow blow,
            in AttackCollisionData attackCollisionData)
        {
            return base.GetDismountPenetration(
                    attackerAgent,
                    attackerWeapon,
                    in blow,
                    in attackCollisionData) +
                DikozemieArenaNativeCombat.GetDismountPenetration(
                    attackerAgent,
                    attackerWeapon);
        }

        public override void DecideMissileWeaponFlags(
            Agent attackerAgent,
            in MissionWeapon missileWeapon,
            ref WeaponFlags missileWeaponFlags)
        {
            base.DecideMissileWeaponFlags(
                attackerAgent,
                in missileWeapon,
                ref missileWeaponFlags);
            DikozemieArenaNativeCombat.DecideMissileWeaponFlags(
                attackerAgent,
                in missileWeapon,
                ref missileWeaponFlags);
        }
    }
}
