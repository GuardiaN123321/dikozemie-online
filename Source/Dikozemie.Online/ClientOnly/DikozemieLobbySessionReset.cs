using System;
using TaleWorlds.MountAndBlade.Diamond;

namespace Dikozemie.Online
{
    /// <summary>
    /// A finalized LobbyState removes the LobbyClient handler without closing
    /// its Diamond session. The replacement LobbyState only attaches a handler
    /// from TryLogin when the client is Idle. Require a completed disconnect
    /// before creating that replacement; Connected/AtLobby is insufficient.
    /// </summary>
    internal sealed class DikozemieLobbySessionReset
    {
        private DateTimeOffset _nextDisconnectAt;
        private bool _disconnectRequested;

        internal void Reset()
        {
            _nextDisconnectAt = default;
            _disconnectRequested = false;
        }

        internal bool TryPrepareFreshLobby(
            LobbyClient client,
            DateTimeOffset now,
            Action<string> trace)
        {
            if (client == null || client.IsIdle)
            {
                if (_disconnectRequested)
                {
                    trace?.Invoke("lobby session reset complete idle=true");
                }
                Reset();
                return true;
            }

            if (!_disconnectRequested || now >= _nextDisconnectAt)
            {
                // The native OnDisconnected callback sets IsIdle even when the
                // previous LobbyState has already removed its session handler.
                client.Logout(null);
                _disconnectRequested = true;
                _nextDisconnectAt = now + TimeSpan.FromSeconds(5d);
                trace?.Invoke("lobby session reset requested state=" +
                    client.CurrentState);
            }

            return false;
        }
    }
}
