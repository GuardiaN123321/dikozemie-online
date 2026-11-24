using System;

namespace Dikozemie.Online
{
    internal enum DikozemieArenaDuelOutcome
    {
        Pending,
        AttackerVictory,
        DefenderVictory,
        Draw
    }

    // A second knockout may change the provisional result, but never moves
    // the first knockout's deadline. Resolution and duplicate deaths are idempotent.
    internal sealed class DikozemieArenaKnockoutWindow
    {
        private bool _attackerDead;
        private bool _defenderDead;
        private bool _resolved;
        internal bool IsPending { get; private set; }
        internal DateTimeOffset ResolveAt { get; private set; }

        internal bool RegisterDeath(bool attacker, DateTimeOffset now, TimeSpan presentation)
        {
            if (_resolved || (IsPending && now > ResolveAt)) return false;
            if (attacker ? _attackerDead : _defenderDead) return false;
            if (attacker) _attackerDead = true;
            else _defenderDead = true;
            if (!IsPending)
            {
                ResolveAt = now.Add(presentation);
                IsPending = true;
            }
            return true;
        }

        internal bool TryResolve(DateTimeOffset now, out DikozemieArenaDuelOutcome outcome)
        {
            outcome = DikozemieArenaDuelOutcome.Pending;
            if (!IsPending || _resolved || now < ResolveAt) return false;
            outcome = _attackerDead && _defenderDead
                ? DikozemieArenaDuelOutcome.Draw
                : _defenderDead ? DikozemieArenaDuelOutcome.AttackerVictory
                : DikozemieArenaDuelOutcome.DefenderVictory;
            IsPending = false;
            _resolved = true;
            return true;
        }

        internal void Reset()
        {
            _attackerDead = false;
            _defenderDead = false;
            _resolved = false;
            IsPending = false;
            ResolveAt = default;
        }
    }
}
