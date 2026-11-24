using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization.Json;
using System.Text;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;

namespace Dikozemie.Online
{
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromClient)]
    public sealed class DikozemieArenaProfileChunkMessage : GameNetworkMessage
    {
        public string TransferId { get; private set; }
        public int ChunkIndex { get; private set; }
        public int ChunkCount { get; private set; }
        public string Payload { get; private set; }

        public DikozemieArenaProfileChunkMessage()
        {
        }

        public DikozemieArenaProfileChunkMessage(
            string transferId,
            int chunkIndex,
            int chunkCount,
            string payload)
        {
            TransferId = transferId;
            ChunkIndex = chunkIndex;
            ChunkCount = chunkCount;
            Payload = payload;
        }

        protected override void OnWrite()
        {
            WriteStringToPacket(TransferId ?? string.Empty);
            // PlayerCompressionInfo is one-based (1..60), while transfer chunks
            // are indexed from zero in memory.
            WriteIntToPacket(ChunkIndex + 1, CompressionBasic.PlayerCompressionInfo);
            WriteIntToPacket(ChunkCount, CompressionBasic.PlayerCompressionInfo);
            WriteStringToPacket(Payload ?? string.Empty);
        }

        protected override bool OnRead()
        {
            bool valid = true;
            TransferId = ReadStringFromPacket(ref valid);
            ChunkIndex = ReadIntFromPacket(CompressionBasic.PlayerCompressionInfo, ref valid) - 1;
            ChunkCount = ReadIntFromPacket(CompressionBasic.PlayerCompressionInfo, ref valid);
            Payload = ReadStringFromPacket(ref valid);
            return valid && Guid.TryParse(TransferId, out _) &&
                ChunkCount > 0 && ChunkCount <= 16 &&
                ChunkIndex >= 0 && ChunkIndex < ChunkCount &&
                (Payload?.Length ?? 0) <= 240;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter() =>
            MultiplayerMessageFilter.GameMode;

        protected override string OnGetLogFormat() =>
            "Dikozemie arena campaign profile chunk";
    }

    internal static class DikozemieArenaProfileTransport
    {
        // Bannerlord's dedicated module-event buffer rejects a 600-character
        // string payload before OnRead reaches the handler.  Keep the encoded
        // profile pieces below the reliable packet ceiling; a normal hero
        // profile remains well inside the existing 16-chunk transfer cap.
        private const int ChunkSize = 200;
        private static readonly Dictionary<NetworkCommunicator, IncomingTransfer> Incoming =
            new Dictionary<NetworkCommunicator, IncomingTransfer>();

        public static void Send(DikozemieArenaCharacterProfile profile)
        {
            if (!GameNetwork.IsClient || profile == null)
            {
                return;
            }

            string encoded = Encode(profile);
            int count = Math.Max(1, (encoded.Length + ChunkSize - 1) / ChunkSize);
            if (count > 16)
            {
                throw new InvalidOperationException("campaign arena profile is too large");
            }

            string transferId = Guid.NewGuid().ToString();
            for (int index = 0; index < count; index++)
            {
                int start = index * ChunkSize;
                string payload = encoded.Substring(
                    start,
                    Math.Min(ChunkSize, encoded.Length - start));
                GameNetwork.BeginModuleEventAsClient();
                GameNetwork.WriteMessage(new DikozemieArenaProfileChunkMessage(
                    transferId,
                    index,
                    count,
                    payload));
                GameNetwork.EndModuleEventAsClient();
            }
        }

        public static bool Receive(
            NetworkCommunicator peer,
            DikozemieArenaProfileChunkMessage message,
            out bool profileAccepted)
        {
            profileAccepted = false;
            if (!GameNetwork.IsServer || peer == null || message == null ||
                !Guid.TryParse(message.TransferId, out _) ||
                message.ChunkCount <= 0 || message.ChunkCount > 16 ||
                message.ChunkIndex < 0 || message.ChunkIndex >= message.ChunkCount ||
                (message.Payload?.Length ?? 0) > 240)
            {
                return false;
            }

            if (!Incoming.TryGetValue(peer, out IncomingTransfer transfer) ||
                !string.Equals(transfer.TransferId, message.TransferId, StringComparison.Ordinal) ||
                transfer.Chunks.Length != message.ChunkCount)
            {
                transfer = new IncomingTransfer(message.TransferId, message.ChunkCount);
                Incoming[peer] = transfer;
            }

            transfer.Chunks[message.ChunkIndex] = message.Payload ?? string.Empty;
            for (int index = 0; index < transfer.Chunks.Length; index++)
            {
                if (transfer.Chunks[index] == null)
                {
                    return true;
                }
            }

            Incoming.Remove(peer);
            try
            {
                var joined = new StringBuilder();
                foreach (string chunk in transfer.Chunks)
                {
                    joined.Append(chunk);
                }
                DikozemieArenaCharacterProfile profile = Decode(joined.ToString());
                profileAccepted =
                    DikozemieArenaServerRegistration.AcceptClientProfile(
                        peer,
                        profile);
                // The network packet was valid even when application-level
                // profile validation rejected its contents. Returning false
                // here makes Bannerlord report a corrupt UDP buffer.
                return true;
            }
            catch (Exception error)
            {
                Console.WriteLine(
                    "DikozemieOnline: rejected campaign profile transfer: " + error.Message);
                return false;
            }
        }

        public static void Forget(NetworkCommunicator peer)
        {
            if (peer != null)
            {
                Incoming.Remove(peer);
            }
        }

        private static string Encode(DikozemieArenaCharacterProfile profile)
        {
            using (var json = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(DikozemieArenaCharacterProfile))
                    .WriteObject(json, profile);
                json.Position = 0;
                using (var compressed = new MemoryStream())
                {
                    using (var deflate = new DeflateStream(
                        compressed,
                        CompressionLevel.Fastest,
                        leaveOpen: true))
                    {
                        json.CopyTo(deflate);
                    }
                    return Convert.ToBase64String(compressed.ToArray());
                }
            }
        }

        private static DikozemieArenaCharacterProfile Decode(string encoded)
        {
            byte[] bytes = Convert.FromBase64String(encoded ?? string.Empty);
            if (bytes.Length > 8192)
            {
                throw new InvalidOperationException("compressed profile exceeds the limit");
            }

            using (var compressed = new MemoryStream(bytes))
            using (var deflate = new DeflateStream(compressed, CompressionMode.Decompress))
            using (var json = new MemoryStream())
            {
                deflate.CopyTo(json);
                if (json.Length > 32768)
                {
                    throw new InvalidOperationException("profile exceeds the limit");
                }
                json.Position = 0;
                return new DataContractJsonSerializer(typeof(DikozemieArenaCharacterProfile))
                    .ReadObject(json) as DikozemieArenaCharacterProfile;
            }
        }

        private sealed class IncomingTransfer
        {
            public IncomingTransfer(string transferId, int chunkCount)
            {
                TransferId = transferId;
                Chunks = new string[chunkCount];
            }

            public string TransferId { get; }
            public string[] Chunks { get; }
        }
    }
}
