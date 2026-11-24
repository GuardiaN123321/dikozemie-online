using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    // 0.5.134: a bot sent to close in on its enemy takes its side arm, as a
    // player would: an archer walking up with a drawn bow only stands and shoots.
    // Kept apart from DikozemieArenaSpawningBehavior so the spawning test
    // harness (Tools/tests/SpawningRuntime) needs no MissionEquipment fakes.
    internal static class DikozemieBotArms
    {
        internal static void WieldSideArm(Agent bot)
        {
            EquipmentIndex wielded = bot.GetPrimaryWieldedItemIndex();
            if (wielded != EquipmentIndex.None)
            {
                WeaponComponentData current = bot.Equipment[wielded].CurrentUsageItem;
                if (current != null && current.IsMeleeWeapon && !current.IsShield)
                {
                    return;
                }
            }
            for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot;
                slot < EquipmentIndex.NumAllWeaponSlots; slot++)
            {
                MissionWeapon weapon = bot.Equipment[slot];
                WeaponComponentData data = weapon.IsEmpty ? null : weapon.Item?.PrimaryWeapon;
                if (data != null && data.IsMeleeWeapon && !data.IsShield)
                {
                    bot.TryToWieldWeaponInSlot(slot, Agent.WeaponWieldActionType.WithAnimation, false);
                    return;
                }
            }
        }
    }
}
