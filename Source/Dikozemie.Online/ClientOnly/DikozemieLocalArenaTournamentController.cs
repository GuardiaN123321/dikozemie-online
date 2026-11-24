using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Dikozemie.Online
{
    public sealed class DikozemieLocalArenaTournamentController : MissionLogic
    {
        private const int TournamentParticipantCount = 16;
        private const int TournamentOpponentCount = TournamentParticipantCount - 1;
        private const float ResultPresentationDelaySeconds = 3f;

        private static int _lastTournamentNumber;

        private readonly BasicCharacterObject _fighter;
        private readonly Random _random = new Random();
        private readonly List<Agent> _friendlyAgents = new List<Agent>();
        private readonly List<Agent> _enemyAgents = new List<Agent>();
        private readonly List<List<TournamentEntrant>> _roundEntrantHistory =
            new List<List<TournamentEntrant>>();
        private readonly List<List<TournamentEntrant>> _roundWinnerHistory =
            new List<List<TournamentEntrant>>();
        private IReadOnlyList<DikozemieArenaOpponent> _allOpponents;
        private List<TournamentEntrant> _currentRound;
        private List<TournamentEntrant> _roundWinners;
        private List<TournamentEntrant> _playerSide;
        private List<TournamentEntrant> _enemySide;
        private List<GameEntity> _arenaSpawnPoints;
        private TournamentEntrant _playerEntrant;
        private Team _playerTeam;
        private Team _enemyTeam;
        private Agent _playerAgent;
        private TournamentVariant _variant;
        private int _tournamentNumber;
        private int _roundNumber;
        private int _currentTeamSize;
        private int _playerWinnerOffset;
        private float _resultPresentationTime;
        private bool _duelCompleted = true;
        private bool _resultPending;
        private bool _lastPlayerVictory;
        private bool _championshipRewardGranted;
        private bool _tournamentUnavailable;
        private TournamentEntrant _champion;
        private DikozemieTournamentPresentationState _presentationState =
            DikozemieTournamentPresentationState.Loading;
        private int _pendingWager;
        private int _activeWager;
        private int _activeWagerPayout;
        private string _presentationMessage = string.Empty;

        internal event Action TournamentPresentationChanged;

        public DikozemieLocalArenaTournamentController(
            BasicCharacterObject fighter)
        {
            _fighter = fighter;
        }

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            Trace("behavior initialized");
            _playerTeam = Mission.Teams.Add(
                BattleSideEnum.Attacker,
                0xffa96a2d,
                0xffffffff,
                null,
                true,
                false,
                true);
            Trace("player team added");
            _enemyTeam = Mission.Teams.Add(
                BattleSideEnum.Defender,
                0xff6d1717,
                0xffffffff,
                null,
                false,
                false,
                true);
            Trace("enemy team added");
        }

        public override void AfterStart()
        {
            base.AfterStart();
            Trace("after start entered");
            _arenaSpawnPoints = Mission.Scene
                .FindEntitiesWithTag("sp_arena")
                .ToList();
            Trace("arena spawn points found: " + _arenaSpawnPoints.Count);
            if (_arenaSpawnPoints.Count < 2)
            {
                _arenaSpawnPoints = Mission.Scene
                    .FindEntitiesWithTag("mp_spawnpoint")
                    .ToList();
            }
            if (_arenaSpawnPoints.Count < 2)
            {
                GameEntity attacker = FindSpawn(
                    "mp_spawnpoint_attacker",
                    "sp_arena_1");
                GameEntity defender = FindSpawn(
                    "mp_spawnpoint_defender",
                    "sp_arena_2");
                if (attacker != null)
                {
                    _arenaSpawnPoints.Add(attacker);
                }
                if (defender != null && !ReferenceEquals(attacker, defender))
                {
                    _arenaSpawnPoints.Add(defender);
                }
            }
            if (_arenaSpawnPoints.Count < 2)
            {
                throw new InvalidOperationException(
                    "The tournament scene does not contain arena spawn points.");
            }

            _allOpponents = DikozemieArenaOpponentCatalog.Build(_fighter);
            Trace("opponent catalog built: " + _allOpponents.Count);
            if (_allOpponents.Count < TournamentOpponentCount)
            {
                ShowTournamentUnavailable(
                    "Турнир временно недоступен: загружено бойцов " +
                    _allOpponents.Count + " из " + TournamentOpponentCount + ".");
                return;
            }

            Trace("creating tournament");
            CreateNewTournament();
            Trace("tournament created");
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (_resultPending && Mission.CurrentTime >= _resultPresentationTime)
            {
                _resultPending = false;
                ShowRoundResult();
            }
        }

        public override void OnAgentRemoved(
            Agent victim,
            Agent killer,
            AgentState state,
            KillingBlow blow)
        {
            base.OnAgentRemoved(victim, killer, state, blow);
            if (_duelCompleted ||
                (state != AgentState.Unconscious && state != AgentState.Killed))
            {
                return;
            }

            bool victimWasFriendly = _friendlyAgents.Contains(victim);
            bool victimWasEnemy = _enemyAgents.Contains(victim);
            if (!victimWasFriendly && !victimWasEnemy)
            {
                return;
            }

            int livingFriendCount = CountLivingAgents(_friendlyAgents, victim);
            int livingEnemyCount = CountLivingAgents(_enemyAgents, victim);
            if (livingFriendCount > 0 && livingEnemyCount > 0)
            {
                return;
            }

            bool playerSideWon = livingEnemyCount == 0 &&
                (livingFriendCount > 0 || victimWasEnemy);
            StorePlayerMatchWinners(playerSideWon ? _playerSide : _enemySide);
            _lastPlayerVictory = playerSideWon;
            _duelCompleted = true;
            FreezeLivingAgents(_friendlyAgents);
            FreezeLivingAgents(_enemyAgents);
            _resultPresentationTime = Mission.CurrentTime +
                ResultPresentationDelaySeconds;
            _resultPending = true;

            Debug.Print(
                "DikozemieOnline: tournament match completed, tournament=" +
                _tournamentNumber + ", round=" + _roundNumber +
                ", teamSize=" + _currentTeamSize +
                ", playerSideVictory=" + playerSideWon,
                0,
                playerSideWon
                    ? Debug.DebugColor.Green
                    : Debug.DebugColor.Red);
        }

        private void CreateNewTournament()
        {
            Trace("create tournament: cleanup");
            HideActiveAgents(_friendlyAgents);
            HideActiveAgents(_enemyAgents);
            Mission.ClearCorpses(false);
            Mission.RemoveSpawnedItemsAndMissiles();
            Trace("create tournament: cleanup complete");

            _tournamentNumber = ++_lastTournamentNumber;
            _roundNumber = 1;
            _roundWinners = null;
            _playerSide = null;
            _enemySide = null;
            _championshipRewardGranted = false;
            _champion = null;
            _roundEntrantHistory.Clear();
            _roundWinnerHistory.Clear();
            _pendingWager = 0;
            _activeWager = 0;
            _activeWagerPayout = 0;
            _presentationMessage = string.Empty;
            _tournamentUnavailable = false;
            _playerEntrant = new TournamentEntrant
            {
                Character = _fighter,
                Name = "ВЫ",
                IsPlayer = true
            };
            Trace("create tournament: player entrant ready");

            _variant = (TournamentVariant)_random.Next(3);
            bool mountedOnly = _variant == TournamentVariant.MountedDuel;
            IReadOnlyList<DikozemieArenaOpponent> selectedOpponents =
                DikozemieArenaOpponentCatalog.SelectTournamentField(
                    _allOpponents,
                    TournamentOpponentCount,
                    DikozemieArmoryProgression.PlayerRating,
                    mountedOnly,
                    _fighter);
            Trace("create tournament: opponents selected: " + selectedOpponents.Count);
            if (mountedOnly &&
                (selectedOpponents.Count < TournamentOpponentCount ||
                 !CharacterHasMount(_fighter)))
            {
                _variant = (TournamentVariant)_random.Next(2);
                selectedOpponents =
                    DikozemieArenaOpponentCatalog.SelectTournamentField(
                        _allOpponents,
                        TournamentOpponentCount,
                        DikozemieArmoryProgression.PlayerRating,
                        false,
                        _fighter);
            }
            if (selectedOpponents.Count < TournamentOpponentCount)
            {
                ShowTournamentUnavailable(
                    "Турнир временно недоступен: в диапазоне рейтинга " +
                    "недостаточно бойцов.");
                return;
            }

            _currentRound = new List<TournamentEntrant>
            {
                _playerEntrant
            };
            for (int index = 0; index < selectedOpponents.Count; index++)
            {
                DikozemieArenaOpponent opponent = selectedOpponents[index];
                BasicCharacterObject character =
                    DikozemieArenaOpponentCatalog.ResolveCharacter(
                        opponent,
                        _fighter);
                _currentRound.Add(new TournamentEntrant
                {
                    Opponent = opponent,
                    Character = character,
                    Name = BuildEntrantName(opponent, character)
                });
            }
            Trace("create tournament: entrants resolved: " + _currentRound.Count);
            Shuffle(_currentRound);
            Trace("create tournament: bracket shuffled");

            _roundEntrantHistory.Add(
                new List<TournamentEntrant>(_currentRound));
            _roundWinnerHistory.Add(null);

            _duelCompleted = true;
            _resultPending = false;
            _presentationState = DikozemieTournamentPresentationState.Opening;
            _presentationMessage =
                "Сетка сформирована. Выберите ставку и вступайте в турнир.";
            InformationManager.DisplayMessage(
                new InformationMessage(
                    "Турнир №" + _tournamentNumber + ": " +
                    GetVariantName() + ", рейтинг " +
                    FormatRatingWindow() + ".",
                    Colors.Yellow));
            NotifyTournamentPresentationChanged();
            Trace("create tournament: opening presented");
        }

        private static void Trace(string message)
        {
            Debug.Print(
                "DikozemieOnline tournament: " + message,
                0,
                Debug.DebugColor.Green);
        }

        private void ShowTournamentUnavailable(string message)
        {
            _tournamentUnavailable = true;
            _duelCompleted = true;
            _resultPending = false;
            _presentationState = DikozemieTournamentPresentationState.Finished;
            _presentationMessage = message;
            InformationManager.DisplayMessage(
                new InformationMessage(message, Colors.Red));
            NotifyTournamentPresentationChanged();
            Trace("unavailable: " + message);
        }

        private void ShowTournamentOpening()
        {
            _presentationState = DikozemieTournamentPresentationState.Opening;
            NotifyTournamentPresentationChanged();
        }

        private void BeginCurrentRound()
        {
            PrepareRound();
            if (_presentationState == DikozemieTournamentPresentationState.Opening &&
                !CommitPendingWager())
            {
                _presentationMessage =
                    "Баланс изменился: выберите ставку ещё раз.";
                NotifyTournamentPresentationChanged();
                return;
            }

            _presentationState = DikozemieTournamentPresentationState.Fighting;
            _presentationMessage = string.Empty;
            NotifyTournamentPresentationChanged();
            HideActiveAgents(_friendlyAgents);
            HideActiveAgents(_enemyAgents);
            Mission.ClearCorpses(false);
            Mission.RemoveSpawnedItemsAndMissiles();

            _friendlyAgents.Clear();
            _enemyAgents.Clear();
            _playerAgent = null;
            for (int index = 0; index < _playerSide.Count; index++)
            {
                TournamentEntrant entrant = _playerSide[index];
                AgentControllerType controller = entrant.IsPlayer
                    ? AgentControllerType.Player
                    : AgentControllerType.AI;
                Agent agent = SpawnEntrant(
                    entrant,
                    _playerTeam,
                    controller,
                    GetSpawnPoint(index, false));
                _friendlyAgents.Add(agent);
                if (entrant.IsPlayer)
                {
                    _playerAgent = agent;
                    Mission.MainAgent = agent;
                }
            }
            for (int index = 0; index < _enemySide.Count; index++)
            {
                Agent agent = SpawnEntrant(
                    _enemySide[index],
                    _enemyTeam,
                    AgentControllerType.AI,
                    GetSpawnPoint(index, true));
                _enemyAgents.Add(agent);
            }

            _duelCompleted = false;
            InformationManager.DisplayMessage(
                new InformationMessage(
                    "Турнир №" + _tournamentNumber + ", " +
                    GetRoundName(_currentRound.Count) + ", " +
                    _currentTeamSize + "×" + _currentTeamSize +
                    ". Противники: " + BuildTeamName(_enemySide) + ".",
                    Colors.Yellow));
        }

        private void PrepareRound()
        {
            _currentTeamSize = GetTeamSize(_currentRound.Count);
            int matchWidth = _currentTeamSize * 2;
            _roundWinners = CreateEmptyEntrantList(_currentRound.Count / 2);
            for (int start = 0; start < _currentRound.Count; start += matchWidth)
            {
                List<TournamentEntrant> left =
                    _currentRound.GetRange(start, _currentTeamSize);
                List<TournamentEntrant> right =
                    _currentRound.GetRange(
                        start + _currentTeamSize,
                        _currentTeamSize);
                int winnerOffset = start / 2;
                if (ContainsPlayer(left) || ContainsPlayer(right))
                {
                    _playerSide = ContainsPlayer(left) ? left : right;
                    _enemySide = ContainsPlayer(left) ? right : left;
                    _playerWinnerOffset = winnerOffset;
                }
                else
                {
                    StoreWinners(
                        _roundWinners,
                        winnerOffset,
                        ChooseBotTeamWinner(left, right));
                }
            }
        }

        private void ShowRoundResult()
        {
            RecordCurrentRoundResult();
            string experienceResult = " " +
                DikozemieHeroProfile.RecordBattleResult(_lastPlayerVictory);
            if (_lastPlayerVictory && _currentRound.Count > 2)
            {
                _presentationState =
                    DikozemieTournamentPresentationState.RoundComplete;
                _presentationMessage = "Этап пройден. Следующий: " +
                    GetRoundName(_roundWinners.Count) +
                    ". Ставка остаётся на победу в турнире." +
                    experienceResult;
                NotifyTournamentPresentationChanged();
                return;
            }

            _champion = _lastPlayerVictory
                ? _playerEntrant
                : SimulateRemainingTournament(_roundWinners);
            string resultText;
            if (_lastPlayerVictory)
            {
                int earnedResources = 0;
                int totalResources = DikozemieArmoryProgression.HeroResources;
                if (!_championshipRewardGranted)
                {
                    earnedResources =
                        DikozemieArmoryProgression.AwardTournamentVictory(
                            out totalResources);
                    _championshipRewardGranted = true;
                }
                resultText = "Вы — чемпион турнира. Монеты: +" +
                    earnedResources + ". Всего: " + totalResources + ".";
            }
            else
            {
                resultText = "Вы выбыли на этапе «" +
                    GetRoundName(_currentRound.Count) +
                    "». Награда выдаётся только чемпиону.";
            }

            string wagerResult = SettleTournamentWager(_lastPlayerVictory);
            _presentationState = DikozemieTournamentPresentationState.Finished;
            _presentationMessage = resultText + wagerResult +
                experienceResult + " Чемпион: " + _champion.Name + ".";
            NotifyTournamentPresentationChanged();
        }

        private void AdvanceToNextRound()
        {
            _currentRound = new List<TournamentEntrant>(_roundWinners);
            _roundWinners = null;
            _roundNumber++;
            _roundEntrantHistory.Add(
                new List<TournamentEntrant>(_currentRound));
            _roundWinnerHistory.Add(null);
            _presentationState =
                DikozemieTournamentPresentationState.BetweenRounds;
            _presentationMessage = GetRoundName(_currentRound.Count) +
                ": ставка уже принята на исход всего турнира.";
            NotifyTournamentPresentationChanged();
        }

        private TournamentEntrant SimulateRemainingTournament(
            List<TournamentEntrant> entrants)
        {
            var remaining = new List<TournamentEntrant>(entrants);
            while (remaining.Count > 1)
            {
                _roundEntrantHistory.Add(
                    new List<TournamentEntrant>(remaining));
                int teamSize = GetTeamSize(remaining.Count);
                int matchWidth = teamSize * 2;
                var winners = new List<TournamentEntrant>(remaining.Count / 2);
                for (int start = 0; start < remaining.Count; start += matchWidth)
                {
                    List<TournamentEntrant> left =
                        remaining.GetRange(start, teamSize);
                    List<TournamentEntrant> right =
                        remaining.GetRange(start + teamSize, teamSize);
                    winners.AddRange(ChooseBotTeamWinner(left, right));
                }
                _roundWinnerHistory.Add(
                    new List<TournamentEntrant>(winners));
                remaining = winners;
            }
            return remaining[0];
        }

        internal bool IsPresentationVisible =>
            _presentationState != DikozemieTournamentPresentationState.Loading &&
            _presentationState != DikozemieTournamentPresentationState.Fighting;

        internal DikozemieTournamentPresentationState PresentationState =>
            _presentationState;

        internal string TournamentTitle =>
            "ТУРНИР №" + _tournamentNumber;

        internal string TournamentSubtitle =>
            GetVariantName() + "  •  РЕЙТИНГ " + FormatRatingWindow();

        internal string PresentationMessage => _presentationMessage;

        internal string CurrentStageName => _currentRound == null
            ? "ТУРНИР"
            : GetRoundName(_currentRound.Count);

        internal string PrimaryActionText
        {
            get
            {
                switch (_presentationState)
                {
                    case DikozemieTournamentPresentationState.Opening:
                        return "ВСТУПИТЬ В ТУРНИР";
                    case DikozemieTournamentPresentationState.BetweenRounds:
                        return "НАЧАТЬ ЭТАП";
                    case DikozemieTournamentPresentationState.RoundComplete:
                        return "К СЛЕДУЮЩЕМУ ЭТАПУ";
                    case DikozemieTournamentPresentationState.Finished:
                        return _tournamentUnavailable
                            ? "ВЫЙТИ В ЛОББИ"
                            : "НОВЫЙ ТУРНИР";
                    default:
                        return string.Empty;
                }
            }
        }

        internal int ResourceBalance =>
            DikozemieArmoryProgression.HeroResources;

        internal int PendingWager => _pendingWager;

        internal int MaximumWager => CanPlaceWager
            ? DikozemieArmoryProgression.HeroResources
            : 0;

        internal bool CanPlaceWager =>
            _presentationState == DikozemieTournamentPresentationState.Opening &&
            _currentRound != null &&
            ContainsPlayer(_currentRound);

        internal double CurrentWagerMultiplier => 2d;

        internal int ExpectedWagerPayout =>
            CalculateExpectedPayout(_pendingWager);

        internal void ExecutePrimaryPresentationAction()
        {
            switch (_presentationState)
            {
                case DikozemieTournamentPresentationState.Opening:
                case DikozemieTournamentPresentationState.BetweenRounds:
                    BeginCurrentRound();
                    break;
                case DikozemieTournamentPresentationState.RoundComplete:
                    AdvanceToNextRound();
                    break;
                case DikozemieTournamentPresentationState.Finished:
                    if (_tournamentUnavailable)
                    {
                        ExitArena();
                    }
                    else
                    {
                        CreateNewTournament();
                    }
                    break;
            }
        }

        internal void ExecuteExitPresentationAction()
        {
            ExitArena();
        }

        internal bool TrySetPendingWager(int wager)
        {
            if (!CanPlaceWager)
            {
                return false;
            }

            _pendingWager = Math.Max(0, Math.Min(MaximumWager, wager));
            NotifyTournamentPresentationChanged();
            return true;
        }

        internal List<DikozemieTournamentMatchData> BuildRoundMatches(
            int roundIndex)
        {
            var matches = new List<DikozemieTournamentMatchData>();
            int expectedParticipants = 16 >> Math.Max(0, roundIndex);
            int expectedTeamSize = GetTeamSize(expectedParticipants);
            int expectedMatchCount = Math.Max(
                1,
                expectedParticipants / (expectedTeamSize * 2));
            if (roundIndex < 0 || roundIndex >= _roundEntrantHistory.Count)
            {
                for (int index = 0; index < expectedMatchCount; index++)
                {
                    matches.Add(DikozemieTournamentMatchData.CreatePlaceholder());
                }
                return matches;
            }

            List<TournamentEntrant> entrants = _roundEntrantHistory[roundIndex];
            List<TournamentEntrant> winners =
                roundIndex < _roundWinnerHistory.Count
                    ? _roundWinnerHistory[roundIndex]
                    : null;
            int teamSize = GetTeamSize(entrants.Count);
            int matchWidth = teamSize * 2;
            for (int start = 0; start < entrants.Count; start += matchWidth)
            {
                List<TournamentEntrant> left =
                    entrants.GetRange(start, teamSize);
                List<TournamentEntrant> right =
                    entrants.GetRange(start + teamSize, teamSize);
                bool leftWon = TeamWon(winners, start / 2, left);
                bool rightWon = TeamWon(winners, start / 2, right);
                matches.Add(new DikozemieTournamentMatchData
                {
                    LeftName = BuildCompactTeamName(left),
                    LeftRating = BuildTeamRatingText(left),
                    RightName = BuildCompactTeamName(right),
                    RightRating = BuildTeamRatingText(right),
                    LeftResult = leftWon ? "ПОБЕДА" : string.Empty,
                    RightResult = rightWon ? "ПОБЕДА" : string.Empty,
                    IsCurrent = roundIndex == _roundNumber - 1 &&
                        (ContainsPlayer(left) || ContainsPlayer(right)) &&
                        _presentationState !=
                            DikozemieTournamentPresentationState.Finished,
                    IsPlayerMatch = ContainsPlayer(left) || ContainsPlayer(right)
                });
            }
            return matches;
        }

        private static string BuildEntrantName(
            DikozemieArenaOpponent opponent,
            BasicCharacterObject character)
        {
            if (opponent != null && character != null &&
                string.Equals(
                    opponent.CharacterId,
                    character.StringId,
                    StringComparison.OrdinalIgnoreCase))
            {
                string nativeName = character.Name?.ToString();
                if (!string.IsNullOrWhiteSpace(nativeName))
                {
                    return nativeName;
                }
            }

            int fighterNumber = Math.Max(
                1,
                (opponent?.StartingRating ?? 100) /
                    DikozemieArenaOpponentCatalog.RatingStep);
            return "Боец №" + fighterNumber.ToString("0000");
        }

        private void RecordCurrentRoundResult()
        {
            int index = Math.Max(0, _roundNumber - 1);
            while (_roundWinnerHistory.Count <= index)
            {
                _roundWinnerHistory.Add(null);
            }
            _roundWinnerHistory[index] =
                new List<TournamentEntrant>(_roundWinners);
        }

        private bool CommitPendingWager()
        {
            _activeWager = 0;
            _activeWagerPayout = 0;
            if (_pendingWager <= 0)
            {
                return true;
            }

            int wager = Math.Min(_pendingWager, MaximumWager);
            int payout = CalculateExpectedPayout(wager);
            if (!DikozemieArmoryProgression.TryPlaceTournamentWager(
                    wager,
                    out _))
            {
                _pendingWager = 0;
                return false;
            }

            _activeWager = wager;
            _activeWagerPayout = payout;
            _pendingWager = 0;
            return true;
        }

        private string SettleTournamentWager(bool playerWonTournament)
        {
            if (_activeWager <= 0)
            {
                return string.Empty;
            }

            int wager = _activeWager;
            int payout = _activeWagerPayout;
            _activeWager = 0;
            _activeWagerPayout = 0;
            if (!playerWonTournament)
            {
                return " Ставка на победу в турнире проиграна: -" + wager + ".";
            }

            DikozemieArmoryProgression.AwardTournamentWager(
                payout,
                out int totalResources);
            return " Ставка на победу в турнире сыграла: выплата +" + payout +
                ". Всего монет: " + totalResources + ".";
        }

        private double CalculateWagerMultiplier()
        {
            return 2d;
        }

        private int CalculateExpectedPayout(int wager)
        {
            if (wager <= 0)
            {
                return 0;
            }
            return wager * 2;
        }

        private bool TryGetPlayerMatch(
            out List<TournamentEntrant> playerSide,
            out List<TournamentEntrant> enemySide)
        {
            playerSide = null;
            enemySide = null;
            if (_currentRound == null)
            {
                return false;
            }

            int teamSize = GetTeamSize(_currentRound.Count);
            int matchWidth = teamSize * 2;
            for (int start = 0; start < _currentRound.Count; start += matchWidth)
            {
                List<TournamentEntrant> left =
                    _currentRound.GetRange(start, teamSize);
                List<TournamentEntrant> right =
                    _currentRound.GetRange(start + teamSize, teamSize);
                if (ContainsPlayer(left))
                {
                    playerSide = left;
                    enemySide = right;
                    return true;
                }
                if (ContainsPlayer(right))
                {
                    playerSide = right;
                    enemySide = left;
                    return true;
                }
            }
            return false;
        }

        private static bool TeamWon(
            List<TournamentEntrant> winners,
            int winnerOffset,
            List<TournamentEntrant> team)
        {
            if (winners == null ||
                winnerOffset < 0 ||
                winnerOffset + team.Count > winners.Count)
            {
                return false;
            }
            for (int index = 0; index < team.Count; index++)
            {
                if (!ReferenceEquals(winners[winnerOffset + index], team[index]))
                {
                    return false;
                }
            }
            return true;
        }

        private static string BuildCompactTeamName(
            List<TournamentEntrant> team)
        {
            var text = new StringBuilder();
            for (int index = 0; index < team.Count; index++)
            {
                if (index > 0)
                {
                    text.Append(" / ");
                }
                text.Append(team[index].Name);
            }
            return text.ToString();
        }

        private static string BuildTeamRatingText(
            List<TournamentEntrant> team)
        {
            int total = 0;
            for (int index = 0; index < team.Count; index++)
            {
                total += GetEntrantRating(team[index]);
            }
            return "РЕЙТИНГ " + Math.Max(1, total / Math.Max(1, team.Count));
        }

        private static int GetEntrantRating(TournamentEntrant entrant)
        {
            return entrant?.Opponent == null
                ? Math.Max(1, DikozemieArmoryProgression.PlayerRating)
                : DikozemieArenaOpponentCatalog.GetCurrentRating(
                    entrant.Opponent);
        }

        private void NotifyTournamentPresentationChanged()
        {
            TournamentPresentationChanged?.Invoke();
        }

        private List<TournamentEntrant> ChooseBotTeamWinner(
            List<TournamentEntrant> left,
            List<TournamentEntrant> right)
        {
            double leftStrength = GetTeamStrength(left);
            double rightStrength = GetTeamStrength(right);
            return _random.NextDouble() <
                leftStrength / (leftStrength + rightStrength)
                    ? left
                    : right;
        }

        private static double GetTeamStrength(List<TournamentEntrant> team)
        {
            double strength = 0d;
            for (int index = 0; index < team.Count; index++)
            {
                DikozemieArenaOpponent opponent = team[index].Opponent;
                if (opponent == null)
                {
                    strength += Math.Max(
                        1,
                        DikozemieArmoryProgression.PlayerRating);
                }
                else
                {
                    strength += Math.Max(
                        1d,
                        opponent.Strength) * Math.Max(
                            1,
                            DikozemieArenaOpponentCatalog.GetCurrentRating(opponent));
                }
            }
            return Math.Max(1d, strength);
        }

        private void StorePlayerMatchWinners(List<TournamentEntrant> winners)
        {
            StoreWinners(_roundWinners, _playerWinnerOffset, winners);
        }

        private static void StoreWinners(
            List<TournamentEntrant> destination,
            int offset,
            List<TournamentEntrant> winners)
        {
            for (int index = 0; index < winners.Count; index++)
            {
                destination[offset + index] = winners[index];
            }
        }

        private Agent SpawnEntrant(
            TournamentEntrant entrant,
            Team team,
            AgentControllerType controller,
            GameEntity spawnPoint)
        {
            Equipment equipment = GetOwnEquipment(
                entrant.Character,
                _variant == TournamentVariant.MountedDuel);
            Agent agent = Mission.SpawnAgent(
                entrant.IsPlayer
                    ? DikozemieHeroProfile.ApplyToPlayerBuild(
                        new AgentBuildData(new BasicBattleAgentOrigin(entrant.Character))
                            .Team(team)
                            .Controller(controller)
                            .Equipment(equipment)
                            .FixedEquipment(true)
                            .NoHorses(false)
                            .InitialFrameFromSpawnPointEntity(spawnPoint),
                        entrant.Character)
                    : new AgentBuildData(new BasicBattleAgentOrigin(entrant.Character))
                    .Team(team)
                    .Controller(controller)
                    .Equipment(equipment)
                    .FixedEquipment(true)
                    .NoHorses(false)
                    .InitialFrameFromSpawnPointEntity(spawnPoint),
                false);
            if (entrant.IsPlayer)
            {
                DikozemieHeroProfile.ApplyHealthBonus(agent);
            }
            if (controller == AgentControllerType.AI)
            {
                agent.SetWatchState(Agent.WatchState.Alarmed);
                agent.SetAutomaticTargetSelection(true);
            }
            return agent;
        }

        private static Equipment GetOwnEquipment(
            BasicCharacterObject character,
            bool requireMount)
        {
            if (requireMount)
            {
                foreach (Equipment equipment in character.BattleEquipments)
                {
                    if (equipment?.Horse.Item != null)
                    {
                        return equipment;
                    }
                }
            }
            return character.FirstBattleEquipment ?? character.Equipment;
        }

        private static bool CharacterHasMount(BasicCharacterObject character)
        {
            Equipment equipment = GetOwnEquipment(character, true);
            return equipment != null && equipment.Horse.Item != null;
        }

        private GameEntity GetSpawnPoint(int index, bool enemySide)
        {
            int half = Math.Max(1, _arenaSpawnPoints.Count / 2);
            int start = enemySide ? half : 0;
            int available = enemySide
                ? Math.Max(1, _arenaSpawnPoints.Count - start)
                : half;
            return _arenaSpawnPoints[start + index % available];
        }

        private int GetTeamSize(int participantCount)
        {
            return _variant == TournamentVariant.NativePairs &&
                participantCount > 2
                    ? 2
                    : 1;
        }

        private string GetVariantName()
        {
            switch (_variant)
            {
                case TournamentVariant.NativePairs:
                    return "КОМАНДНЫЙ ТУРНИР 2×2";
                case TournamentVariant.MountedDuel:
                    return "КОННЫЙ ТУРНИР 1×1";
                default:
                    return "ОТКРЫТЫЙ ТУРНИР 1×1";
            }
        }

        private string FormatRatingWindow()
        {
            int playerRating = DikozemieArmoryProgression.PlayerRating;
            int minimumRating = Math.Max(
                DikozemieArenaOpponentCatalog.RatingStep,
                playerRating -
                    DikozemieArenaOpponentCatalog.RatingSelectionRadius);
            int maximumRating = playerRating +
                DikozemieArenaOpponentCatalog.RatingSelectionRadius;
            return minimumRating + "–" + maximumRating;
        }

        private static string BuildBracketText(
            List<TournamentEntrant> entrants,
            List<TournamentEntrant> winners,
            int teamSize)
        {
            var text = new StringBuilder();
            int matchWidth = teamSize * 2;
            int matchNumber = 0;
            for (int start = 0; start < entrants.Count; start += matchWidth)
            {
                matchNumber++;
                List<TournamentEntrant> left =
                    entrants.GetRange(start, teamSize);
                List<TournamentEntrant> right =
                    entrants.GetRange(start + teamSize, teamSize);
                text.Append(matchNumber)
                    .Append(". ")
                    .Append(BuildTeamName(left))
                    .Append("  —  ")
                    .Append(BuildTeamName(right));
                int winnerOffset = start / 2;
                if (winners != null && winners[winnerOffset] != null)
                {
                    text.Append("  →  ")
                        .Append(BuildTeamName(
                            winners.GetRange(winnerOffset, teamSize)));
                }
                if (start + matchWidth < entrants.Count)
                {
                    text.AppendLine();
                }
            }
            return text.ToString();
        }

        private static string BuildTeamName(List<TournamentEntrant> team)
        {
            var text = new StringBuilder();
            for (int index = 0; index < team.Count; index++)
            {
                if (index > 0)
                {
                    text.Append(" + ");
                }
                text.Append(team[index].Name);
                if (!team[index].IsPlayer && team[index].Opponent != null)
                {
                    text.Append(" [")
                        .Append(
                            DikozemieArenaOpponentCatalog.GetCurrentRating(
                                team[index].Opponent))
                        .Append(']');
                }
            }
            return text.ToString();
        }

        private static string GetRoundName(int participantCount)
        {
            switch (participantCount)
            {
                case 16:
                    return "1/8 ФИНАЛА";
                case 8:
                    return "ЧЕТВЕРТЬФИНАЛ";
                case 4:
                    return "ПОЛУФИНАЛ";
                default:
                    return "ФИНАЛ";
            }
        }

        private void Shuffle(List<TournamentEntrant> entrants)
        {
            for (int index = entrants.Count - 1; index > 0; index--)
            {
                int swapIndex = _random.Next(index + 1);
                TournamentEntrant temporary = entrants[index];
                entrants[index] = entrants[swapIndex];
                entrants[swapIndex] = temporary;
            }
        }

        private void ExitArena()
        {
            DikozemieLobbyShellIntegration.PrepareForTrainingExit();
            if (!Mission.MissionEnded && !Mission.IsMissionEnding)
            {
                Mission.EndMission();
            }
        }

        private static int CountLivingAgents(List<Agent> agents, Agent removed)
        {
            int count = 0;
            for (int index = 0; index < agents.Count; index++)
            {
                Agent agent = agents[index];
                if (agent != null && agent != removed && agent.IsActive())
                {
                    count++;
                }
            }
            return count;
        }

        private static void FreezeLivingAgents(List<Agent> agents)
        {
            for (int index = 0; index < agents.Count; index++)
            {
                if (agents[index]?.IsActive() == true)
                {
                    agents[index].Controller = AgentControllerType.None;
                }
            }
        }

        private static void HideActiveAgents(List<Agent> agents)
        {
            for (int index = 0; index < agents.Count; index++)
            {
                Agent agent = agents[index];
                if (agent?.IsActive() == true)
                {
                    agent.Controller = AgentControllerType.None;
                    agent.FadeOut(hideInstantly: true, hideMount: true);
                }
            }
            agents.Clear();
        }

        private static bool ContainsPlayer(List<TournamentEntrant> entrants)
        {
            for (int index = 0; index < entrants.Count; index++)
            {
                if (entrants[index].IsPlayer)
                {
                    return true;
                }
            }
            return false;
        }

        private static List<TournamentEntrant> CreateEmptyEntrantList(int count)
        {
            var entrants = new List<TournamentEntrant>(count);
            for (int index = 0; index < count; index++)
            {
                entrants.Add(null);
            }
            return entrants;
        }

        private GameEntity FindSpawn(string primaryTag, string arenaTag)
        {
            return Mission.Scene.FindEntityWithTag(primaryTag) ??
                Mission.Scene.FindEntityWithTag(arenaTag) ??
                Mission.Scene.FindEntityWithTag("mp_spawnpoint") ??
                Mission.Scene.FindEntityWithTag("spawnpoint") ??
                Mission.Scene.FindEntityWithTag("sp_arena");
        }

        private sealed class TournamentEntrant
        {
            internal DikozemieArenaOpponent Opponent;
            internal BasicCharacterObject Character;
            internal string Name;
            internal bool IsPlayer;
        }

        private enum TournamentVariant
        {
            OpenDuel,
            NativePairs,
            MountedDuel
        }
    }
}
