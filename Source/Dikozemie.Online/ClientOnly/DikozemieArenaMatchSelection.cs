using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace Dikozemie.Online
{
    internal enum DikozemieBattleVenue
    {
        Arena,
        Naval
    }

    internal static class DikozemieArenaMatchSelection
    {
        internal const int MinimumTeamSize = 1;
        internal const int MaximumTeamSize =
            DikozemieArenaTeamRules.MaximumTeamSize;
        internal static readonly int[] TeamSizeOptions =
            { 1, 2, 3, 4, 5, 6, 10, 15, 20, 25, 50, 75, 100 };

        private static readonly object Gate = new object();
        private static int _teamSize = MinimumTeamSize;
        private static DikozemieArenaDiscipline _discipline =
            DikozemieArenaDiscipline.Open;
        private static DikozemieArenaComposition _composition =
            DikozemieArenaComposition.Mixed;
        private static DikozemieBattleVenue _venue =
            DikozemieBattleVenue.Arena;
        // The lobby section on screen. The battle rotation can send an Arena
        // tab player to sea, so the venue of the battle (Venue) and the
        // section the lobby shows (MenuVenue) are kept apart.
        private static DikozemieBattleVenue _menuVenue =
            DikozemieBattleVenue.Arena;
        private static readonly Random HullRandom = new Random();
        private static string _selectedNavalHullId =
            DikozemieNavalBattleCatalog
                .FindFirst(DikozemieNavalShipClass.Light)
                .HullId;

        // Where the next or current battle happens; read by the search, the
        // server profile and the battle screen.
        internal static DikozemieBattleVenue Venue
        {
            get
            {
                lock (Gate)
                {
                    return _venue;
                }
            }
        }

        internal static DikozemieBattleVenue MenuVenue
        {
            get
            {
                lock (Gate)
                {
                    return _menuVenue;
                }
            }
        }

        // A rotation battle: the venue changes, the lobby section does not.
        internal static void SelectArenaBattle(
            int teamSize,
            DikozemieArenaDiscipline discipline,
            DikozemieArenaComposition composition)
        {
            lock (Gate)
            {
                _venue = DikozemieBattleVenue.Arena;
                _discipline = NormalizeDiscipline(discipline);
                _teamSize = DikozemieArenaTeamRules.NormalizeTeamSize(
                    teamSize,
                    _discipline);
                _composition = composition;
            }
        }

        internal static void SelectNavalClassForBattle(
            DikozemieNavalShipClass shipClass)
        {
            lock (Gate)
            {
                _venue = DikozemieBattleVenue.Naval;
                IReadOnlyList<DikozemieNavalBattleDefinition> hulls =
                    DikozemieNavalBattleCatalog.FindByClass(shipClass);
                _selectedNavalHullId = hulls.Count > 0
                    ? hulls[HullRandom.Next(hulls.Count)].HullId
                    : DikozemieNavalBattleCatalog.FindFirst(shipClass).HullId;
            }
        }

        // The rotation checks every candidate against the hero's gear.
        internal static bool IsCompositionPlayable(
            DikozemieArenaDiscipline discipline,
            DikozemieArenaComposition composition)
        {
            return IsCompositionAvailableForEquipment(
                GetHeroEquipment(),
                composition,
                NormalizeDiscipline(discipline),
                out _);
        }

        internal static DikozemieNavalBattleDefinition SelectedNavalBattle
        {
            get
            {
                lock (Gate)
                {
                    return DikozemieNavalBattleCatalog.Find(
                        _selectedNavalHullId);
                }
            }
        }

        internal static int TeamSize
        {
            get
            {
                lock (Gate)
                {
                    return _teamSize;
                }
            }
        }

        internal static DikozemieArenaDiscipline Discipline
        {
            get
            {
                lock (Gate)
                {
                    return _discipline;
                }
            }
        }

        internal static DikozemieArenaComposition Composition
        {
            get
            {
                lock (Gate)
                {
                    return _composition;
                }
            }
        }

        // A sea battle charges by ship class, an arena battle by format.
        internal static int CalculateLossCoinPenalty(int seriesLosses)
        {
            return Venue == DikozemieBattleVenue.Naval
                ? DikozemieNavalRewardRules.GetLossCoinPenalty(
                    SelectedNavalBattle.ShipClass) * Math.Max(0, seriesLosses)
                : DikozemieArenaTeamRules.CalculateLossCoinPenalty(
                    TeamSize,
                    seriesLosses);
        }

        internal static void SelectVenue(DikozemieBattleVenue venue)
        {
            lock (Gate)
            {
                _venue = venue == DikozemieBattleVenue.Naval
                    ? DikozemieBattleVenue.Naval
                    : DikozemieBattleVenue.Arena;
                _menuVenue = _venue;
            }
        }

        internal static void SelectNavalClass(
            DikozemieNavalShipClass shipClass)
        {
            lock (Gate)
            {
                _venue = DikozemieBattleVenue.Naval;
                _menuVenue = DikozemieBattleVenue.Naval;
                _selectedNavalHullId = DikozemieNavalBattleCatalog
                    .FindFirst(shipClass)
                    .HullId;
            }
        }

        internal static void SelectNextNavalHull(int direction)
        {
            lock (Gate)
            {
                DikozemieNavalBattleDefinition selected =
                    DikozemieNavalBattleCatalog.Find(_selectedNavalHullId);
                IReadOnlyList<DikozemieNavalBattleDefinition> battles =
                    DikozemieNavalBattleCatalog.FindByClass(
                        selected.ShipClass);
                int index = 0;
                for (int candidate = 0; candidate < battles.Count; candidate++)
                {
                    if (string.Equals(
                            battles[candidate].HullId,
                            selected.HullId,
                            StringComparison.Ordinal))
                    {
                        index = candidate;
                        break;
                    }
                }

                int step = direction < 0 ? -1 : 1;
                index = (index + step + battles.Count) % battles.Count;
                _selectedNavalHullId = battles[index].HullId;
                _venue = DikozemieBattleVenue.Naval;
                _menuVenue = DikozemieBattleVenue.Naval;
            }
        }

        internal static void SelectTeamSize(int teamSize)
        {
            lock (Gate)
            {
                _teamSize = DikozemieArenaTeamRules.NormalizeTeamSize(
                    teamSize,
                    _discipline);
            }
        }

        internal static bool SelectDiscipline(
            DikozemieArenaDiscipline discipline)
        {
            discipline = NormalizeDiscipline(discipline);
            Equipment equipment = GetHeroEquipment();
            if (!DikozemieArenaLoadoutRules.IsDisciplineAvailable(
                    equipment,
                    discipline,
                    out _))
            {
                return false;
            }

            lock (Gate)
            {
                _discipline = discipline;
                _teamSize = DikozemieArenaTeamRules.NormalizeTeamSize(
                    _teamSize,
                    _discipline);
                if (!IsCompositionAvailableForEquipment(
                        equipment,
                        _composition,
                        _discipline,
                        out _))
                {
                    _composition = FindAvailableComposition(
                        equipment,
                        _discipline);
                }
            }
            return true;
        }

        internal static bool SelectComposition(
            DikozemieArenaComposition composition)
        {
            Equipment equipment = GetHeroEquipment();
            lock (Gate)
            {
                if (!IsCompositionAvailableForEquipment(
                        equipment,
                        composition,
                        _discipline,
                        out _))
                {
                    return false;
                }
                _composition = composition;
            }
            return true;
        }

        internal static bool IsCompositionSupported(
            DikozemieArenaComposition composition,
            DikozemieArenaDiscipline discipline)
        {
            return DikozemieArenaCompositionRules.IsSupported(
                composition,
                discipline);
        }

        internal static bool IsTeamSizeSupported(int teamSize)
        {
            lock (Gate)
            {
                return DikozemieArenaTeamRules.IsTeamSizeSupported(
                    teamSize,
                    _discipline);
            }
        }

        internal static bool IsDisciplineAvailable(
            DikozemieArenaDiscipline discipline,
            out string reason)
        {
            return DikozemieArenaLoadoutRules.IsDisciplineAvailable(
                GetHeroEquipment(),
                NormalizeDiscipline(discipline),
                out reason);
        }

        internal static bool IsCompositionAvailable(
            DikozemieArenaComposition composition,
            out string reason)
        {
            DikozemieArenaDiscipline discipline;
            lock (Gate)
            {
                discipline = _discipline;
            }
            return IsCompositionAvailableForEquipment(
                GetHeroEquipment(),
                composition,
                discipline,
                out reason);
        }

        internal static bool TryValidateCurrent(out string reason)
        {
            DikozemieArenaDiscipline discipline;
            DikozemieArenaComposition composition;
            lock (Gate)
            {
                discipline = _discipline;
                composition = _composition;
            }
            return DikozemieArenaLoadoutRules.IsSelectionAvailable(
                GetHeroEquipment(),
                discipline,
                composition,
                out reason);
        }

        internal static void ReconcileWithLoadout()
        {
            Equipment equipment = GetHeroEquipment();
            lock (Gate)
            {
                if (!DikozemieArenaLoadoutRules.IsDisciplineAvailable(
                        equipment,
                        _discipline,
                        out _))
                {
                    _discipline = DikozemieArenaDiscipline.Open;
                }
                _teamSize = DikozemieArenaTeamRules.NormalizeTeamSize(
                    _teamSize,
                    _discipline);
                if (!IsCompositionAvailableForEquipment(
                        equipment,
                        _composition,
                        _discipline,
                        out _))
                {
                    _composition = FindAvailableComposition(
                        equipment,
                        _discipline);
                }
            }
        }

        internal static string BuildLoadoutAvailabilityText()
        {
            Equipment equipment = GetHeroEquipment();
            var unavailable = new List<string>();
            if (!DikozemieArenaLoadoutRules.IsDisciplineAvailable(
                    equipment,
                    DikozemieArenaDiscipline.Mounted,
                    out _))
            {
                // A horse without fodder for the day stays in the stable.
                unavailable.Add(DikozemieHorseUpkeep.IsHungry
                    ? "КОННЫЙ — КОНЬ ГОЛОДЕН, НУЖНО " + DikozemieHorseUpkeep.DailyCost + " МОН. НА КОРМ"
                    : "КОННЫЙ — НЕТ КОНЯ");
            }
            if (!DikozemieArenaLoadoutRules.IsCompositionAvailable(
                    equipment,
                    DikozemieArenaComposition.Melee,
                    out _))
            {
                unavailable.Add("БЛИЖНИЙ — НУЖНО БЛИЖНЕЕ ИЛИ МЕТАТЕЛЬНОЕ ОРУЖИЕ");
            }
            if (!DikozemieArenaLoadoutRules.IsCompositionAvailable(
                    equipment,
                    DikozemieArenaComposition.Ranged,
                    out _))
            {
                unavailable.Add("ДАЛЬНИЙ — НУЖНЫ ЛУК/СТРЕЛЫ ИЛИ АРБАЛЕТ/БОЛТЫ");
            }
            return unavailable.Count == 0
                ? "ЭКИПИРОВКА ПОДХОДИТ ДЛЯ ВСЕХ РЕЖИМОВ"
                : "НЕДОСТУПНО: " + string.Join(" · ", unavailable);
        }

        internal static string FormatTeamSize() =>
            TeamSize + "×" + TeamSize;

        internal static string FormatDiscipline()
        {
            switch (Discipline)
            {
                case DikozemieArenaDiscipline.Foot:
                    return "пеший";
                case DikozemieArenaDiscipline.Mounted:
                    return "конный";
                default:
                    return "открытый";
            }
        }

        internal static string FormatComposition()
        {
            switch (Composition)
            {
                case DikozemieArenaComposition.Melee:
                    return "ближний бой";
                case DikozemieArenaComposition.Ranged:
                    return "дальний бой";
                default:
                    return "смешанный";
            }
        }

        private static Equipment GetHeroEquipment()
        {
            return DikozemieHeroProfile.CreateBattleEquipment(null);
        }

        private static bool IsCompositionAvailableForEquipment(
            Equipment equipment,
            DikozemieArenaComposition composition,
            DikozemieArenaDiscipline discipline,
            out string reason)
        {
            if (!IsCompositionSupported(composition, discipline))
            {
                reason = "ЭТОТ СОСТАВ НЕДОСТУПЕН ДЛЯ ВЫБРАННОГО СТИЛЯ";
                return false;
            }
            return DikozemieArenaLoadoutRules.IsCompositionAvailable(
                equipment,
                composition,
                out reason);
        }

        private static DikozemieArenaComposition FindAvailableComposition(
            Equipment equipment,
            DikozemieArenaDiscipline discipline)
        {
            foreach (DikozemieArenaComposition candidate in new[]
            {
                DikozemieArenaComposition.Mixed,
                DikozemieArenaComposition.Melee,
                DikozemieArenaComposition.Ranged
            })
            {
                if (IsCompositionAvailableForEquipment(
                        equipment,
                        candidate,
                        discipline,
                        out _))
                {
                    return candidate;
                }
            }
            return DikozemieArenaComposition.Mixed;
        }

        private static DikozemieArenaDiscipline NormalizeDiscipline(
            DikozemieArenaDiscipline discipline)
        {
            return discipline == DikozemieArenaDiscipline.Foot ||
                discipline == DikozemieArenaDiscipline.Mounted
                    ? discipline
                    : DikozemieArenaDiscipline.Open;
        }
    }
}
