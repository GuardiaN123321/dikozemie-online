using System;
namespace Dikozemie.Online
{
    internal static class DikozemieArenaDuelReservation
    {
        internal static bool CanPair(string firstId, string firstOffer, string firstOpponent,
            string secondId, string secondOffer, string secondOpponent)
        {
            if (string.Equals(firstId, secondId, StringComparison.Ordinal)) return false;
            if (string.IsNullOrEmpty(firstOffer) && string.IsNullOrEmpty(secondOffer))
                return string.IsNullOrEmpty(firstOpponent) && string.IsNullOrEmpty(secondOpponent);
            return Guid.TryParse(firstOffer, out Guid offer) && offer != Guid.Empty &&
                string.Equals(firstOffer, secondOffer, StringComparison.Ordinal) &&
                string.Equals(firstOpponent, secondId, StringComparison.Ordinal) &&
                string.Equals(secondOpponent, firstId, StringComparison.Ordinal);
        }
    }
}
