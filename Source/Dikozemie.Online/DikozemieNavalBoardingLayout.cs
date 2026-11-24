using TaleWorlds.Library;

namespace Dikozemie.Online
{
    internal readonly struct DikozemieNavalBoardingLayout
    {
        // Keep the whole test encounter inside the authored 0..3200 physics
        // world.  The previous 4000/4000 center put every runtime collider
        // outside the physics broadphase, so agents fell through the decks.
        internal const float SeaCenterX = 1600f;
        internal const float SeaCenterY = 1600f;
        internal const float InitialSeparation = 70f;
        // Where the ships meet, off the middle of the deck plane so the enemy
        // ship still starts over it.
        private const float DockShift = 15f;
        // Rails nearly touch, so nobody crosses a visible gap on thin air.
        private const float BridgeGap = 1f;
        // The enemy comes alongside this far off; the hooks close the rest.
        internal const float HaulDistance = 7f;

        internal DikozemieNavalBoardingLayout(
            float dockedSeparation,
            float deckHeight,
            float shipScale,
            float approachDuration,
            float centerX = SeaCenterX,
            float centerY = SeaCenterY)
        {
            DockedSeparation = dockedSeparation;
            DeckHeight = deckHeight;
            ShipScale = shipScale;
            ApproachDuration = approachDuration;
            CenterX = centerX;
            CenterY = centerY;
        }

        internal float DockedSeparation { get; }
        internal float DeckHeight { get; }
        internal float ShipScale { get; }
        internal float ApproachDuration { get; }
        // Where this battle stands: each hull has its own deck plane in the sea.
        internal float CenterX { get; }
        internal float CenterY { get; }

        internal float AttackerStartX =>
            AttackerDockX;

        internal float DefenderStartX =>
            DefenderDockX + HaulDistance;

        // The attacker never sails: a player on a moving deck is teleported every
        // frame and the camera shakes with it. The enemy ship comes to him.
        internal float AttackerStartY => CenterY;

        internal float DefenderStartY =>
            CenterY + InitialSeparation;

        internal float AttackerDockX =>
            CenterX - (DockedSeparation * 0.5f);

        internal float DefenderDockX =>
            CenterX + (DockedSeparation * 0.5f);

        internal float AttackerDockY => CenterY;

        internal float DefenderDockY => CenterY;

        internal Vec3 GetStartDeckCenter(bool attacker) => new Vec3(
            attacker ? AttackerStartX : DefenderStartX,
            attacker ? AttackerStartY : DefenderStartY,
            DeckHeight);

        internal Vec3 GetDockedDeckCenter(bool attacker) => new Vec3(
            attacker ? AttackerDockX : DefenderDockX,
            attacker ? AttackerDockY : DefenderDockY,
            DeckHeight);

        // The ships dock rail to rail with room for the bridges, whatever the hull.
        internal DikozemieNavalBoardingLayout ForHull(
            DikozemieNavalHullMetrics hull) =>
            new DikozemieNavalBoardingLayout(
                (2f * hull.HalfBeam) + BridgeGap,
                hull.DeckPlaneHeight,
                ShipScale,
                ApproachDuration,
                hull.SeaX,
                hull.SeaY - DockShift);

        internal static DikozemieNavalBoardingLayout Resolve(int shipClass)
        {
            switch ((DikozemieNavalShipClass)shipClass)
            {
                case DikozemieNavalShipClass.Medium:
                    return new DikozemieNavalBoardingLayout(
                        12.5f,
                        DikozemieNavalHullMetrics.Resolve(null).DeckPlaneHeight,
                        2.70f,
                        12f);
                case DikozemieNavalShipClass.Heavy:
                    return new DikozemieNavalBoardingLayout(
                        14f,
                        DikozemieNavalHullMetrics.Resolve(null).DeckPlaneHeight,
                        3.20f,
                        14f);
                default:
                    return new DikozemieNavalBoardingLayout(
                        10.5f,
                        DikozemieNavalHullMetrics.Resolve(null).DeckPlaneHeight,
                        2.268f,
                        10f);
            }
        }
    }
}
