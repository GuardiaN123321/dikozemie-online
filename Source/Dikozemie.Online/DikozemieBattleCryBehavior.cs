using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;
#if CLIENT
using NetworkMessages.FromClient;
using TaleWorlds.Engine.Options;
using TaleWorlds.Library;
#endif

namespace Dikozemie.Online
{
    internal enum DikozemieBattleCryLevel
    {
        None = 0,
        Shout = 1,
        Roar = 2,
        Berserk = 3
    }

    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromClient)]
    public sealed class DikozemieBattleCryRequest : GameNetworkMessage
    {
        public int Level { get; private set; }

        public DikozemieBattleCryRequest()
        {
        }

        internal DikozemieBattleCryRequest(DikozemieBattleCryLevel level)
        {
            Level = (int)level;
        }

        protected override void OnWrite() =>
            WriteIntToPacket(Level, CompressionMatchmaker.ScoreCompressionInfo);

        protected override bool OnRead()
        {
            bool valid = true;
            Level = ReadIntFromPacket(
                CompressionMatchmaker.ScoreCompressionInfo,
                ref valid);
            return valid &&
                Level >= (int)DikozemieBattleCryLevel.Shout &&
                Level <= (int)DikozemieBattleCryLevel.Berserk;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter() =>
            MultiplayerMessageFilter.GameMode;

        protected override string OnGetLogFormat() =>
            "Dikozemie battle cry request";
    }

    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class DikozemieBattleCryState : GameNetworkMessage
    {
        public int AgentIndex { get; private set; }
        public int Level { get; private set; }

        public DikozemieBattleCryState()
        {
        }

        internal DikozemieBattleCryState(
            int agentIndex,
            DikozemieBattleCryLevel level)
        {
            AgentIndex = agentIndex;
            Level = (int)level;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket(AgentIndex, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(Level, CompressionMatchmaker.ScoreCompressionInfo);
        }

        protected override bool OnRead()
        {
            bool valid = true;
            AgentIndex = ReadIntFromPacket(
                CompressionMatchmaker.ScoreCompressionInfo,
                ref valid);
            Level = ReadIntFromPacket(
                CompressionMatchmaker.ScoreCompressionInfo,
                ref valid);
            return valid;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter() =>
            MultiplayerMessageFilter.GameMode;

        protected override string OnGetLogFormat() =>
            "Dikozemie battle cry state";
    }

    /// <summary>
    /// The player's real shouting drives the hero's battle cries. Only loudness
    /// leaves the machine as a cry, so words never reach the enemy and need no
    /// profanity filter; the words themselves go to the player's own team
    /// through the native voice chat.
    /// </summary>
    public sealed class DikozemieBattleCryBehavior : MissionNetwork
    {
        private static readonly TimeSpan MinimumCryInterval =
            TimeSpan.FromMilliseconds(900);
        private static readonly TimeSpan BerserkCooldown =
            TimeSpan.FromSeconds(20);
        private const float BerserkHealing = 15f;

        private readonly Dictionary<NetworkCommunicator, DateTimeOffset> _nextCry =
            new Dictionary<NetworkCommunicator, DateTimeOffset>();
        private readonly Dictionary<NetworkCommunicator, DateTimeOffset> _nextBerserk =
            new Dictionary<NetworkCommunicator, DateTimeOffset>();

        internal static event Action<Agent, DikozemieBattleCryLevel> CryReceived;

        protected override void AddRemoveMessageHandlers(
            GameNetwork.NetworkMessageHandlerRegistererContainer registerer)
        {
            if (GameNetwork.IsServer)
            {
                registerer.RegisterBaseHandler<DikozemieBattleCryRequest>(
                    HandleCryRequest);
            }
#if CLIENT
            if (GameNetwork.IsClient)
            {
                registerer.Register<DikozemieBattleCryState>(HandleCryState);
            }
#endif
        }

        private bool HandleCryRequest(
            NetworkCommunicator peer,
            GameNetworkMessage baseMessage)
        {
            var request = (DikozemieBattleCryRequest)baseMessage;
            Agent agent = peer.GetComponent<MissionPeer>()?.ControlledAgent;
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (agent == null || agent.State != AgentState.Active ||
                !agent.IsHuman ||
                (_nextCry.TryGetValue(peer, out DateTimeOffset nextCry) &&
                 now < nextCry))
            {
                return true;
            }
            _nextCry[peer] = now.Add(MinimumCryInterval);

            var level = (DikozemieBattleCryLevel)request.Level;
            if (level == DikozemieBattleCryLevel.Berserk)
            {
                if (_nextBerserk.TryGetValue(peer, out DateTimeOffset nextBerserk) &&
                    now < nextBerserk)
                {
                    level = DikozemieBattleCryLevel.Roar;
                }
                else
                {
                    _nextBerserk[peer] = now.Add(BerserkCooldown);
                    agent.Health = Math.Min(
                        agent.HealthLimit,
                        agent.Health + BerserkHealing);
                }
            }
            // Shout and roar are screams; only the berserk adds the spoken
            // «В атаку!» (the charge order was a calm sentence for a roar).
            agent.MakeVoice(
                level == DikozemieBattleCryLevel.Berserk
                    ? SkinVoiceManager.VoiceType.Charge
                    : SkinVoiceManager.VoiceType.Yell,
                SkinVoiceManager.CombatVoiceNetworkPredictionType.NoPrediction);
            GameNetwork.BeginBroadcastModuleEvent();
            GameNetwork.WriteMessage(
                new DikozemieBattleCryState(agent.Index, level));
            GameNetwork.EndBroadcastModuleEvent(
                GameNetwork.EventBroadcastFlags.None);
            return true;
        }

        // ---- Bot battle cries (server) --------------------------------------
        // Bots never raised their voice, so a battle with one live player and
        // sixty bots was silent apart from blows (customer, 2026-11-04: «не
        // кричал никто»). A voice played on the server reaches every client
        // through the engine's own voice event, so the server only decides
        // who yells and when. A budget keeps a mass charge from becoming
        // a wall of sixty voices in one frame.
        private const float BotYellBudgetPerSecond = 16f;
        private const float BotYellBudgetMax = 6f;
        private const float MeleeYellInterval = 2.5f;
        private static readonly List<KeyValuePair<Agent, float>> PendingBotYells =
            new List<KeyValuePair<Agent, float>>();
        private static readonly Random YellRandom = new Random();
        private float _botYellBudget = BotYellBudgetMax;
        private float _meleeYellElapsed;

        internal static void QueueBotYells(
            IEnumerable<Agent> agents,
            float chance,
            float spreadSeconds)
        {
            Mission mission = Mission.Current;
            if (!GameNetwork.IsServer || mission == null || agents == null)
            {
                return;
            }
            float now = mission.CurrentTime;
            foreach (Agent agent in agents)
            {
                if (agent == null || !agent.IsHuman || agent.MissionPeer != null ||
                    agent.State != AgentState.Active ||
                    YellRandom.NextDouble() > chance)
                {
                    continue;
                }
                PendingBotYells.Add(new KeyValuePair<Agent, float>(
                    agent,
                    now + (float)(YellRandom.NextDouble() * spreadSeconds)));
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (!GameNetwork.IsServer)
            {
                return;
            }
            long started = DikozemieServerPerf.Start();
            _botYellBudget = Math.Min(
                BotYellBudgetMax,
                _botYellBudget + BotYellBudgetPerSecond * Math.Max(0f, dt));
            float now = Mission.CurrentTime;
            for (int index = PendingBotYells.Count - 1;
                index >= 0 && _botYellBudget >= 1f;
                index--)
            {
                KeyValuePair<Agent, float> pending = PendingBotYells[index];
                if (pending.Value > now)
                {
                    continue;
                }
                PendingBotYells.RemoveAt(index);
                if (pending.Key != null && pending.Key.State == AgentState.Active)
                {
                    pending.Key.MakeVoice(
                        SkinVoiceManager.VoiceType.Yell,
                        SkinVoiceManager.CombatVoiceNetworkPredictionType.NoPrediction);
                    _botYellBudget -= 1f;
                }
            }

            // Now and then a bot in a sword fight shouts.
            _meleeYellElapsed += Math.Max(0f, dt);
            if (_meleeYellElapsed >= MeleeYellInterval)
            {
                _meleeYellElapsed = 0f;
                int shouted = 0;
                foreach (Agent agent in Mission.Agents)
                {
                    if (shouted >= 2)
                    {
                        break;
                    }
                    if (agent == null || !agent.IsHuman || agent.MissionPeer != null ||
                        agent.State != AgentState.Active || YellRandom.NextDouble() > 0.15)
                    {
                        continue;
                    }
                    Agent enemy = agent.ImmediateEnemy;
                    if (enemy != null && enemy.State == AgentState.Active &&
                        enemy.Position.DistanceSquared(agent.Position) < 16f)
                    {
                        PendingBotYells.Add(new KeyValuePair<Agent, float>(agent, now));
                        shouted++;
                    }
                }
            }
            if (PendingBotYells.Count > 400)
            {
                PendingBotYells.RemoveRange(0, PendingBotYells.Count - 400);
            }
            DikozemieServerPerf.Stop(DikozemieServerPerf.Section.CryTick, started);
        }

        public override void OnRemoveBehavior()
        {
            PendingBotYells.Clear();
#if CLIENT
            if (_micOpen)
            {
                SoundManager.StopVoiceRecording();
                _micOpen = false;
            }
#endif
            base.OnRemoveBehavior();
        }

#if CLIENT
        private const int VoiceFrameBytes = 1440;
        private const int RecordBufferBytes = 72000;
        private const int CompressedBufferBytes = 8640;
        private const int PushToTalkGameKey = 33;
        private const float TalkGateDb = -45f;
        private const float TalkHoldSeconds = 0.4f;
        // Real headsets peaked lower than the first guesses: a loud shout
        // into an ordinary gaming headset sits around -28..-20 dBFS.
        private const float ShoutDb = -30f;
        private const float RoarDb = -18f;
        private const float BerserkDb = -10f;
        private const float BerserkHoldSeconds = 1.2f;
        private const float RequestIntervalSeconds = 0.5f;
        private const float RecorderRestartSeconds = 0.3f;
        // Only a sound well above this machine's own background counts. A PC
        // without a microphone hands back a steady level that read as a roar.
        private const float CryAboveFloorDb = 12f;
        private const float TalkAboveFloorDb = 8f;
        private const float FloorRiseDbPerSecond = 1f;
        private const float FloorSettleSeconds = 2f;
        private const float MicTraceSeconds = 10f;
        // A shout lasts. A PC with no microphone hands back stray buffers, one
        // frame at a time, and those read as a roar (2026-10-31).
        private const float CrySustainSeconds = 0.25f;

        private readonly byte[] _recordBuffer = new byte[RecordBufferBytes];
        private readonly Queue<byte> _voiceToSend = new Queue<byte>();
        private bool _micOpen;
        private float _talkHold;
        private float _berserkHeld;
        private float _sinceRequest;
        private float _smokeElapsed;
        private float _noiseFloor;
        private float _micListened;
        private float _micTrace;
        private float _micPeak = -120f;
        private float _crySustained;
        private int _restartRecordingInFrames;
        private float _recorderSilent;

        public override void AfterStart()
        {
            base.AfterStart();
            if (!GameNetwork.IsClient)
            {
                return;
            }
            OpenMic();
        }

        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        private static extern uint waveInGetNumDevs();

        // With no capture device at all the engine still hands back audio, as
        // loud as the battle itself: that read as shouting, and it would have
        // gone to the team as voice (2026-10-31, a PC with no microphone).
        private static bool HasMicrophone()
        {
            try
            {
                return waveInGetNumDevs() > 0;
            }
            catch (Exception error)
            {
                MissionMultiplayerDikozemieArena.Trace(
                    "mic check failed: " + error.Message);
                return false;
            }
        }

        private void OpenMic()
        {
            bool hasMicrophone = HasMicrophone();
            _micOpen = BannerlordConfig.EnableVoiceChat && hasMicrophone;
            MissionMultiplayerDikozemieArena.Trace(
                "mic open=" + _micOpen + " device=" + hasMicrophone);
            if (_micOpen)
            {
                SoundManager.StartVoiceRecording();
            }
        }

        public override void OnPreDisplayMissionTick(float dt)
        {
            base.OnPreDisplayMissionTick(dt);
            if (!GameNetwork.IsClient)
            {
                return;
            }
            _sinceRequest += dt;
            if (string.Equals(
                    Environment.GetEnvironmentVariable(
                        "DIKOZEMIE_BATTLE_CRY_SMOKE"),
                    "1",
                    StringComparison.Ordinal))
            {
                TickCrySmoke(dt);
            }
            if (!_micOpen)
            {
                return;
            }
            // The native push-to-talk empties the shared recorder while V is
            // held and stops it on the frame AFTER the release. Restarting on
            // the release frame itself was undone a frame later, so one press
            // of V left the microphone off for the rest of the battle: no
            // cries, no team voice (customer, 2026-11-04 «не кричал никто»).
            bool pushToTalkHeld = Mission.InputManager != null &&
                Mission.InputManager.IsGameKeyDown(PushToTalkGameKey);
            if (Mission.InputManager != null &&
                Mission.InputManager.IsGameKeyReleased(PushToTalkGameKey))
            {
                _restartRecordingInFrames = 2;
            }
            if (_restartRecordingInFrames > 0 && --_restartRecordingInFrames == 0)
            {
                SoundManager.StartVoiceRecording();
            }

            SoundManager.GetVoiceData(
                _recordBuffer,
                RecordBufferBytes,
                out int readBytes);
            if (readBytes < 2)
            {
                // A running recorder always hands back something; silence
                // here means it was stopped behind our back.
                _recorderSilent += dt;
                if (!pushToTalkHeld && _recorderSilent >= RecorderRestartSeconds)
                {
                    _recorderSilent = 0f;
                    SoundManager.StartVoiceRecording();
                }
                return;
            }
            _recorderSilent = 0f;
            float loudness = MeasureLoudnessDb(readBytes);
            UpdateNoiseFloor(loudness, dt);
            TraceMic(readBytes, loudness, dt);
            if (_micListened < FloorSettleSeconds)
            {
                return;
            }
            SendTeamVoice(readBytes, loudness, dt);
            UpdateCry(loudness, dt);
        }

        private float MeasureLoudnessDb(int readBytes)
        {
            double sum = 0d;
            int samples = readBytes / 2;
            for (int index = 0; index < samples; index++)
            {
                short sample = BitConverter.ToInt16(_recordBuffer, index * 2);
                sum += sample * (double)sample;
            }
            double rms = Math.Sqrt(sum / Math.Max(1, samples)) / short.MaxValue;
            return rms <= 1e-6d ? -120f : (float)(20d * Math.Log10(rms));
        }

        private void UpdateNoiseFloor(float loudness, float dt)
        {
            _noiseFloor = _micListened <= 0f || loudness < _noiseFloor
                ? loudness
                : _noiseFloor + (FloorRiseDbPerSecond * dt);
            _micListened += dt;
        }

        private void TraceMic(int readBytes, float loudness, float dt)
        {
            _micTrace += dt;
            _micPeak = Math.Max(_micPeak, loudness);
            if (_micTrace < MicTraceSeconds)
            {
                return;
            }
            _micTrace = 0f;
            MissionMultiplayerDikozemieArena.Trace(
                "mic level=" + loudness.ToString("0.0") +
                " floor=" + _noiseFloor.ToString("0.0") +
                " peak=" + _micPeak.ToString("0.0") +
                " bytes=" + readBytes);
            _micPeak = -120f;
        }

        // The server passes this to the player's own team only: native rule.
        private void SendTeamVoice(int readBytes, float loudness, float dt)
        {
            _talkHold = loudness >= TalkGateDb &&
                    loudness >= _noiseFloor + TalkAboveFloorDb
                ? TalkHoldSeconds
                : Math.Max(0f, _talkHold - dt);
            if (_talkHold <= 0f)
            {
                _voiceToSend.Clear();
                return;
            }
            for (int index = 0; index < readBytes; index++)
            {
                _voiceToSend.Enqueue(_recordBuffer[index]);
            }
            while (_voiceToSend.Count >= VoiceFrameBytes)
            {
                byte[] frame = new byte[VoiceFrameBytes];
                for (int index = 0; index < VoiceFrameBytes; index++)
                {
                    frame[index] = _voiceToSend.Dequeue();
                }
                byte[] compressed = new byte[CompressedBufferBytes];
                SoundManager.CompressData(
                    0,
                    frame,
                    VoiceFrameBytes,
                    compressed,
                    out int compressedLength);
                GameNetwork.BeginModuleEventAsClientUnreliable();
                GameNetwork.WriteMessage(
                    new SendVoiceRecord(compressed, compressedLength));
                GameNetwork.EndModuleEventAsClientUnreliable();
            }
        }

        private void UpdateCry(float loudness, float dt)
        {
            if (loudness < _noiseFloor + CryAboveFloorDb || loudness < ShoutDb)
            {
                // A breath inside a shout must not start the count over:
                // the shout fades out over twice its own speed instead.
                _berserkHeld = Math.Max(0f, _berserkHeld - dt * 2f);
                _crySustained = Math.Max(0f, _crySustained - dt * 2f);
                return;
            }
            _crySustained += dt;
            if (_crySustained < CrySustainSeconds)
            {
                return;
            }
            _berserkHeld = loudness >= BerserkDb
                ? _berserkHeld + dt
                : 0f;
            DikozemieBattleCryLevel level = _berserkHeld >= BerserkHoldSeconds
                ? DikozemieBattleCryLevel.Berserk
                : loudness >= RoarDb
                    ? DikozemieBattleCryLevel.Roar
                    : loudness >= ShoutDb
                        ? DikozemieBattleCryLevel.Shout
                        : DikozemieBattleCryLevel.None;
            if (level != DikozemieBattleCryLevel.None)
            {
                RequestCry(level);
            }
        }

        private void RequestCry(DikozemieBattleCryLevel level)
        {
            if (_sinceRequest < RequestIntervalSeconds ||
                Mission.MainAgent == null)
            {
                return;
            }
            _sinceRequest = 0f;
            GameNetwork.BeginModuleEventAsClient();
            GameNetwork.WriteMessage(new DikozemieBattleCryRequest(level));
            GameNetwork.EndModuleEventAsClient();
        }

        // A test machine has nobody shouting at it: walk through the levels.
        private void TickCrySmoke(float dt)
        {
            _smokeElapsed += dt;
            int step = (int)(_smokeElapsed / 2f) % 4;
            if (step > 0)
            {
                RequestCry((DikozemieBattleCryLevel)step);
            }
        }

        private void HandleCryState(DikozemieBattleCryState message)
        {
            Agent agent = Mission.FindAgentWithIndex(message.AgentIndex);
            if (agent != null)
            {
                CryReceived?.Invoke(
                    agent,
                    (DikozemieBattleCryLevel)message.Level);
            }
        }
#endif
    }
}
