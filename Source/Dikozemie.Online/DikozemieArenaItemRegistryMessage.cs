using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace Dikozemie.Online
{
    /// <summary>
    /// The battle server's item registry (count and fingerprint of the
    /// registration order, see DikozemieItemRegistry), sent to every peer
    /// that joins. A client whose items were registered in another order
    /// would draw other players' equipment wrongly; it says so at once
    /// instead of silently dressing heroes in other items.
    /// </summary>
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class DikozemieArenaItemRegistryMessage : GameNetworkMessage
    {
        public int ItemCount { get; private set; }
        public uint Fingerprint { get; private set; }

        public DikozemieArenaItemRegistryMessage()
        {
        }

        internal DikozemieArenaItemRegistryMessage(int itemCount, uint fingerprint)
        {
            ItemCount = itemCount;
            Fingerprint = fingerprint;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket(ItemCount, CompressionMatchmaker.ScoreCompressionInfo);
            WriteUintToPacket(Fingerprint, CompressionBasic.ColorCompressionInfo);
        }

        protected override bool OnRead()
        {
            bool valid = true;
            ItemCount = ReadIntFromPacket(CompressionMatchmaker.ScoreCompressionInfo, ref valid);
            Fingerprint = ReadUintFromPacket(CompressionBasic.ColorCompressionInfo, ref valid);
            return valid && ItemCount >= 0;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter() =>
            MultiplayerMessageFilter.GameMode;

        protected override string OnGetLogFormat() =>
            "Dikozemie arena item registry " + ItemCount + " " + Fingerprint.ToString("x8");
    }
}
