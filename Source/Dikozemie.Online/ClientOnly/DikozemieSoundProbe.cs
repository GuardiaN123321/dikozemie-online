using System;
using System.Text;
using TaleWorlds.Engine;

namespace Dikozemie.Online
{
    // Writes once per game whether the engine can resolve the sound events
    // the arena relies on. 2026-11-04: the customer heard no battle sound,
    // and the start of every client log lists 1,803 events, all combat
    // voices among them, as «Could not find the event index».
    internal static class DikozemieSoundProbe
    {
        private static bool _done;

        private static readonly string[] Events =
        {
            "event:/ui/default",
            "event:/voice/combat/male/01/yell",
            "event:/voice/combat/male/01/pain",
            "event:/mission/combat/blunt/med",
            "event:/mission/combat/armor/chainmail",
            "event:/mission/ambient/area/open_sea",
            "event:/mission/movement/vessel/hook_throw",
            "event:/alerts/horns/attack"
        };

        internal static void Run(string where)
        {
            if (_done)
            {
                return;
            }
            _done = true;
            try
            {
                var line = new StringBuilder("sound probe at ")
                    .Append(where)
                    .Append(" total=")
                    .Append(SoundEvent.GetTotalEventCount());
                foreach (string name in Events)
                {
                    line.Append(" | ")
                        .Append(name.Substring("event:/".Length))
                        .Append(" id=")
                        .Append(SoundEvent.GetEventIdFromString(name))
                        .Append(" global=")
                        .Append(SoundManager.GetEventGlobalIndex(name));
                }
                MissionMultiplayerDikozemieArena.Trace(line.ToString());
            }
            catch (Exception error)
            {
                MissionMultiplayerDikozemieArena.Trace(
                    "sound probe failed: " + error.Message);
            }
        }
    }
}
