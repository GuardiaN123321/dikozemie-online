using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace Dikozemie.Online
{
    internal enum DikozemieNavalBoardingPhase
    {
        Prepare = 0,
        Approach = 1,
        Boarded = 2
    }

    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class DikozemieNavalBoardingStateMessage : GameNetworkMessage
    {
        public int Phase { get; private set; }
        public int ShipClass { get; private set; }
        public int HullIndex { get; private set; }

        public DikozemieNavalBoardingStateMessage()
        {
        }

        internal DikozemieNavalBoardingStateMessage(
            DikozemieNavalBoardingPhase phase,
            DikozemieNavalShipClass shipClass,
            int hullIndex)
        {
            Phase = (int)phase;
            ShipClass = (int)shipClass;
            HullIndex = hullIndex;
        }

        protected override void OnWrite()
        {
            WriteIntToPacket(Phase, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(ShipClass, CompressionMatchmaker.ScoreCompressionInfo);
            WriteIntToPacket(HullIndex, CompressionMatchmaker.ScoreCompressionInfo);
        }

        protected override bool OnRead()
        {
            bool valid = true;
            Phase = ReadIntFromPacket(
                CompressionMatchmaker.ScoreCompressionInfo,
                ref valid);
            ShipClass = ReadIntFromPacket(
                CompressionMatchmaker.ScoreCompressionInfo,
                ref valid);
            HullIndex = ReadIntFromPacket(
                CompressionMatchmaker.ScoreCompressionInfo,
                ref valid);
            return valid &&
                Phase >= (int)DikozemieNavalBoardingPhase.Prepare &&
                Phase <= (int)DikozemieNavalBoardingPhase.Boarded &&
                ShipClass >= (int)DikozemieNavalShipClass.Light &&
                ShipClass <= (int)DikozemieNavalShipClass.Heavy;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter() =>
            MultiplayerMessageFilter.GameMode;

        protected override string OnGetLogFormat() =>
            "Dikozemie naval boarding state";
    }
}
