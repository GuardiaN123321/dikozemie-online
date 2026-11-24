using System;
using System.IO;

namespace Dikozemie.Online
{
    /// <summary>
    /// Versioned acknowledgement for one-time lobby announcements. A new event
    /// gets a new id and therefore appears once even for returning players.
    /// </summary>
    internal static class DikozemieArenaAnnouncementStore
    {
        internal const string AnnouncementId = "colorado-tournament-2026-10";

        private static string MarkerPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Dikozemie",
            "arena",
            "announcements",
            AnnouncementId + ".seen");

        internal static bool ShouldShow()
        {
            try
            {
                return !File.Exists(MarkerPath);
            }
            catch (Exception error)
            {
                Console.WriteLine(
                    "DikozemieOnline: announcement state read failed: " + error);
                return true;
            }
        }

        internal static void MarkSeen()
        {
            try
            {
                string directory = Path.GetDirectoryName(MarkerPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(
                    MarkerPath,
                    DateTimeOffset.UtcNow.ToString("O"));
            }
            catch (Exception error)
            {
                Console.WriteLine(
                    "DikozemieOnline: announcement state write failed: " + error);
            }
        }
    }
}
