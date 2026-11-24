using TaleWorlds.MountAndBlade.Multiplayer.GauntletUI;
using TaleWorlds.MountAndBlade.Multiplayer.ViewModelCollection.Lobby.Armory;
using TaleWorlds.ScreenSystem;

namespace Dikozemie.Online
{
    internal static class DikozemieArmoryLobbyIntegration
    {
        private static MPArmoryVM _heroArmory;
        private static int _nativeLoot;
        private static string _nativeStatsText;

        internal static void Tick()
        {
            MultiplayerLobbyGauntletScreen lobbyScreen =
                ScreenManager.TopScreen as MultiplayerLobbyGauntletScreen;
            MPArmoryVM armory = lobbyScreen?.DataSource?.Armory;
            if (armory?.ClassFilter?.Factions == null)
            {
                return;
            }

            // The RPG hero has its own page. Never repurpose or relabel the
            // native armory: its cosmetics, balance and permanent purchases
            // keep using the game's original service-backed state.
            RestoreNativeArmory();

            // Keep the official armory completely native. Hero progression is
            // presented on the Hero page and must not disable base classes or
            // interfere with service-backed cosmetics.
        }

        internal static void Reset()
        {
            RestoreNativeArmory();
        }

        private static void ApplyHeroProgress(MPArmoryVM armory)
        {
            if (!ReferenceEquals(_heroArmory, armory))
            {
                RestoreNativeArmory();
                _heroArmory = armory;
                _nativeLoot = armory.Cosmetics?.Loot ?? 0;
                _nativeStatsText = armory.StatsText;
            }

            if (armory.Cosmetics != null)
            {
                armory.Cosmetics.Loot = DikozemieArmoryProgression.HeroResources;
            }
            armory.StatsText =
                "Рейтинг героя: " + DikozemieArmoryProgression.PlayerRating;
        }

        private static void RestoreNativeArmory()
        {
            if (_heroArmory == null)
            {
                return;
            }

            if (_heroArmory.Cosmetics != null)
            {
                _heroArmory.Cosmetics.Loot = _nativeLoot;
            }
            _heroArmory.StatsText = _nativeStatsText;
            _heroArmory = null;
            _nativeStatsText = null;
        }
    }
}
