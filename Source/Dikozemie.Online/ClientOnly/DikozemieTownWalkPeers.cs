using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    // Stage 5 in the town walk (the stage 4 note: «каждый гуляет по своей
    // копии города, общий город — этап 5»): the other heroes walking the same
    // scene stand in it, with their faces and what they wear, and walk where
    // they walk. Their words come as the town's talk.
    public sealed partial class DikozemieTownWalkController
    {
        private sealed class WalkPeer
        {
            internal string Id = string.Empty;
            internal string Name = string.Empty;
            internal string Look = string.Empty;
            internal Agent Agent;
            internal DateTime SeenUtc;
            internal Vec3 Target;
            internal float Direction;
            internal bool Waiting;
        }

        private readonly Dictionary<string, WalkPeer> _peers = new Dictionary<string, WalkPeer>(StringComparer.Ordinal);
        private float _peerTick;
        private float _walkReport;

        // The scene a hero walks, the same words on every computer.
        internal string WalkScene => (_setup.Node?.Id ?? string.Empty) + "|" + _setup.Place;

        internal IEnumerable<string> PeerNames =>
            _peers.Values.Where(p => p.Agent != null && p.Agent.IsActive()).Select(p => p.Name);

        private void TickPeers(float dt)
        {
            if (_heroAgent != null && _heroAgent.IsActive())
            {
                _walkReport -= dt;
                if (_walkReport <= 0f)
                {
                    _walkReport = 0.4f;
                    Vec2 look = _heroAgent.LookDirection.AsVec2;
                    DikozemieWorld.ReportWalk(WalkScene, _heroAgent.Position, (float)Math.Atan2(look.y, look.x),
                        _heroAgent.MovementVelocity.LengthSquared > 0.5f ? 1 : 0);
                }
            }
            _peerTick -= dt;
            if (_peerTick > 0f)
            {
                return;
            }
            _peerTick = 0.2f;
            DateTime now = DateTime.UtcNow;
            string scene = WalkScene;
            foreach (DikozemieWorldPlayer p in DikozemieWorld.Players)
            {
                if (p.Place != "walk" || p.Walk == null || p.Walk.Scene != scene)
                {
                    continue;
                }
                if (!_peers.TryGetValue(p.Id, out WalkPeer peer))
                {
                    peer = new WalkPeer { Id = p.Id, Name = p.Name };
                    _peers[p.Id] = peer;
                }
                peer.SeenUtc = now;
                peer.Target = new Vec3(p.Walk.X, p.Walk.Y, p.Walk.Z);
                peer.Direction = p.Walk.Dir;
                if (peer.Agent == null || !peer.Agent.IsActive() || peer.Look != p.Look)
                {
                    SpawnPeer(peer, p);
                }
                else
                {
                    MovePeer(peer);
                }
            }
            foreach (WalkPeer gone in _peers.Values.Where(p => (now - p.SeenUtc).TotalSeconds > 4).ToList())
            {
                if (gone.Agent != null && gone.Agent.IsActive())
                {
                    gone.Agent.FadeOut(false, true);
                }
                _peers.Remove(gone.Id);
                Debug.Print("DikozemieOnline: town walk peer left " + gone.Name, 0, Debug.DebugColor.Green);
            }
        }

        private void SpawnPeer(WalkPeer peer, DikozemieWorldPlayer p)
        {
            DikozemieWorldLook look = DikozemieWorld.LookOf(p.Id);
            if (look == null)
            {
                // The face comes with the next answer of the server.
                if (!peer.Waiting)
                {
                    peer.Waiting = true;
                    Debug.Print("DikozemieOnline: town walk peer waits for a look " + p.Name + " hash=" + p.Look, 0,
                        Debug.DebugColor.Yellow);
                }
                return;
            }
            if (peer.Agent != null && peer.Agent.IsActive())
            {
                peer.Agent.FadeOut(true, true);
            }
            peer.Agent = null;
            peer.Look = p.Look;
            try
            {
                Equipment equipment = null;
                try
                {
                    equipment = string.IsNullOrEmpty(look.Equip) ? null : Equipment.CreateFromEquipmentCode(look.Equip);
                }
                catch (Exception)
                {
                    equipment = null;
                }
                equipment = equipment ?? _hero.Equipment?.Clone() ?? new Equipment();
                equipment[EquipmentIndex.Horse] = default(EquipmentElement);
                equipment[EquipmentIndex.HorseHarness] = default(EquipmentElement);
                BodyProperties face = BodyProperties.FromString(look.Body, out BodyProperties parsed)
                    ? parsed
                    : _hero.GetBodyProperties(equipment);
                Monster monster = TaleWorlds.Core.FaceGen.GetMonsterWithSuffix(Math.Max(0, look.Race), "_settlement") ??
                    TaleWorlds.Core.FaceGen.GetBaseMonsterFromRace(Math.Max(0, look.Race));
                Vec3 position = OnGround(peer.Target);
                Vec2 direction = new Vec2((float)Math.Cos(peer.Direction), (float)Math.Sin(peer.Direction));
                var data = new AgentBuildData(new BasicBattleAgentOrigin(_hero))
                    .Team(Team.Invalid)
                    .Controller(AgentControllerType.AI)
                    .InitialPosition(in position)
                    .InitialDirection(in direction)
                    .Equipment(equipment)
                    .Monster(monster)
                    .NoHorses(true)
                    .IsFemale(look.Female)
                    .BodyProperties(face)
                    .CanSpawnOutsideOfMissionBoundary(true);
                Agent agent = Mission.SpawnAgent(data, false);
                if (agent == null)
                {
                    return;
                }
                agent.SetWatchState(Agent.WatchState.Patrolling);
                agent.SetAutomaticTargetSelection(false);
                agent.SetMortalityState(Agent.MortalityState.Immortal);
                SetName(agent, p.Name);
                peer.Agent = agent;
                Debug.Print("DikozemieOnline: town walk peer " + p.Name + " at " + Describe(position), 0,
                    Debug.DebugColor.Green);
            }
            catch (Exception error)
            {
                Debug.Print("DikozemieOnline: town walk peer not spawned " + p.Name + ": " + error.Message, 0,
                    Debug.DebugColor.Red);
            }
        }

        private void MovePeer(WalkPeer peer)
        {
            Agent agent = peer.Agent;
            Vec3 target = OnGround(peer.Target);
            float distance = agent.Position.AsVec2.Distance(target.AsVec2);
            if (distance > 9f)
            {
                agent.TeleportToPosition(target);
                return;
            }
            if (distance < 0.5f)
            {
                agent.DisableScriptedMovement();
                agent.LookDirection = new Vec3((float)Math.Cos(peer.Direction), (float)Math.Sin(peer.Direction), 0f);
                return;
            }
            var world = new WorldPosition(Mission.Scene, UIntPtr.Zero, target, false);
            agent.SetScriptedPosition(ref world, false, Agent.AIScriptedFrameFlags.NoAttack);
            // A walk keeps pace with the hero walking; far behind, he runs.
            agent.SetMaximumSpeedLimit(distance > 3f ? 1f : 0.6f, true);
        }

        // Is this agent another hero (not one of the town's people)?
        internal bool IsPeer(Agent agent) => agent != null && _peers.Values.Any(p => p.Agent == agent);
    }
}
