using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    // Ranked opponents are owned by the arena, not the native automatic bot
    // pool. Never decrement that pool for agents it did not create. This also
    // makes repeated removal notifications harmless, without private-field
    // reflection or keeping dead Agent objects in a registry.
    public sealed class DikozemieArenaSpawnComponent : SpawnComponent
    {
        public DikozemieArenaSpawnComponent(
            SpawnFrameBehaviorBase frames,
            DikozemieArenaSpawningBehavior spawning) : base(frames, spawning)
        {
        }

        public override void OnAgentRemoved(
            Agent affectedAgent,
            Agent affectorAgent,
            AgentState agentState,
            KillingBlow blow)
        {
            if (affectedAgent.IsHuman &&
                affectedAgent.MissionPeer == null &&
                affectedAgent.OwningAgentMissionPeer == null)
            {
                SpawnFrameBehavior.OnAgentRemoved(
                    affectedAgent, affectorAgent, agentState, blow);
                return;
            }

            base.OnAgentRemoved(
                affectedAgent, affectorAgent, agentState, blow);
        }
    }
}
