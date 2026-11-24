using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Diamond;
using TaleWorlds.MountAndBlade.View.CustomBattle;
using TaleWorlds.ObjectSystem;
using TaleWorlds.ScreenSystem;

namespace Dikozemie.Online
{
    /// <summary>
    /// Hands the naval preset to War Sails instead of trying to imitate its
    /// moving ships with ordinary multiplayer scene props. A War Sails ship
    /// owns its hull physics, dynamic navigation mesh, crew and controller as
    /// one mission object; splitting those responsibilities is what caused
    /// overlapping hulls and agents falling through the deck.
    /// </summary>
    internal static class DikozemieNavalBattleLauncher
    {
        private const string NavalProviderType =
            "NavalDLC.CustomBattle.NavalCustomBattleProvider";
        private const string NavalScreenType =
            "NavalDLC.CustomBattle.CustomBattle.NavalCustomBattleScreen";
        private const string NavalViewModelType =
            "NavalDLC.CustomBattle.CustomBattle.NavalCustomBattleVM";
        private const string SmokeHullArgument =
            "/dikozemieNavalSmokeHull=";
        private const string SmokeHullMarkerName =
            "naval-smoke-hull.txt";
        private static DikozemieNavalBattleDefinition _pendingBattle;
        private static DateTimeOffset _phaseStartedAt;
        private static bool _nativeGameRequested;
        private static bool _pendingSmokeResultAcknowledge;
        private static string _status = "Морской бой готов";
        private static bool _smokeRequestHandled;
        private static DateTimeOffset _smokeReadyAt;
        private static ScreenBase _nativeSetupScreen;
        private static bool _nativeSetupSuppressionArmed;
        private static bool _nativeSetupSuppressedBeforeFirstFrame;
        private static NavalReturnPhase _returnPhase;
        private static DateTimeOffset _returnPhaseStartedAt;
        private static DateTimeOffset _nextReturnAttemptAt;
        private static readonly DikozemieLobbySessionReset LobbySessionReset =
            new DikozemieLobbySessionReset();

        internal static bool IsPending => _pendingBattle != null ||
            _returnPhase != NavalReturnPhase.None;

        internal static string Status => _status;

        internal static bool TryStart(
            DikozemieNavalBattleDefinition battle,
            out string errorMessage,
            bool smokeResultAcknowledge = false)
        {
            errorMessage = string.Empty;
            if (battle == null)
            {
                errorMessage = "Не выбран корабль для морского боя.";
                return false;
            }
            if (_pendingBattle != null ||
                _returnPhase != NavalReturnPhase.None)
            {
                errorMessage = "Морской бой уже загружается.";
                return false;
            }

            ICustomBattleProvider provider = FindNavalProvider();
            if (provider == null)
            {
                errorMessage =
                    "War Sails не загружен. Запустите игру через " +
                    "launch_arena_client.cmd из архива арены.";
                return false;
            }

            ArmNativeSetupScreenSuppression();
            _pendingBattle = battle;
            _phaseStartedAt = DateTimeOffset.UtcNow;
            _nativeGameRequested = false;
            _pendingSmokeResultAcknowledge = smokeResultAcknowledge;
            _status = "Завершаем сетевое лобби перед морским боем…";
            try
            {
                DikozemieLobbyShellIntegration.PrepareForTrainingExit();
                Console.WriteLine(
                    "DikozemieOnline: ending multiplayer game before " +
                    "native naval battle hull=" + battle.HullId +
                    " crew=" + battle.CrewCapacity);
                MBGameManager.EndGame();
                return true;
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "DikozemieOnline: multiplayer end before naval failed: " +
                    exception);
                errorMessage = "Не удалось завершить сетевое лобби: " +
                    GetUsefulMessage(exception);
                ClearPending(errorMessage, Colors.Red);
                return false;
            }
        }

        internal static void Tick()
        {
            if (TickReturnToMultiplayer())
            {
                return;
            }

            TryStartSmokeRequest();

            DikozemieNavalBattleDefinition battle = _pendingBattle;
            if (battle == null)
            {
                return;
            }

            if (!_nativeGameRequested)
            {
                // StartCustomBattle creates another Game. It must never run
                // until the multiplayer Game has been fully destroyed and
                // the global state manager is available again.
                if (Game.Current != null || GameStateManager.Current == null)
                {
                    if (DateTimeOffset.UtcNow - _phaseStartedAt >
                        TimeSpan.FromSeconds(20d))
                    {
                        ClearPending(
                            "Сетевое лобби не завершилось за 20 секунд.",
                            Colors.Red);
                    }
                    return;
                }

                TryRequestNativeGame(battle);
                return;
            }

            if (DateTimeOffset.UtcNow - _phaseStartedAt >
                TimeSpan.FromSeconds(60d))
            {
                ClearPending(
                    "War Sails не открыл экран морского боя за 60 секунд.",
                    Colors.Red);
                return;
            }

            object topScreen = ScreenManager.TopScreen;
            if (!string.Equals(
                    topScreen?.GetType().FullName,
                    NavalScreenType,
                    StringComparison.Ordinal))
            {
                return;
            }

            TryApplyPresetAndStart(topScreen, battle);
        }

        internal static void NotifyNativeMissionEnded()
        {
            RequestReturnToMultiplayer("mission ended");
        }

        private static void TryRequestNativeGame(
            DikozemieNavalBattleDefinition battle)
        {
            try
            {
                ICustomBattleProvider provider = FindNavalProvider();
                if (provider == null)
                {
                    ClearPending(
                        "War Sails пропал из списка загруженных модулей.",
                        Colors.Red);
                    return;
                }

                _nativeGameRequested = true;
                _phaseStartedAt = DateTimeOffset.UtcNow;
                _status = "Готовим корабль «" + battle.DisplayName + "»…";
                Console.WriteLine(
                    "DikozemieOnline: multiplayer game ended; starting " +
                    "native naval custom game hull=" + battle.HullId);
                provider.StartCustomBattle();
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "DikozemieOnline: native naval game start failed: " +
                    exception);
                ClearPending(
                    "Не удалось открыть War Sails: " +
                    GetUsefulMessage(exception),
                    Colors.Red);
            }
        }

        private static void TryApplyPresetAndStart(
            object topScreen,
            DikozemieNavalBattleDefinition battle)
        {
            try
            {
                object dataSource = topScreen.GetType()
                    .GetField(
                        "_dataSource",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(topScreen);
                if (dataSource == null || !string.Equals(
                        dataSource.GetType().FullName,
                        NavalViewModelType,
                        StringComparison.Ordinal))
                {
                    return;
                }

                ShipHull hull = Game.Current?.ObjectManager
                    ?.GetObject<ShipHull>(battle.HullId);
                if (hull == null)
                {
                    ClearPending(
                        "War Sails не нашёл корпус «" +
                        battle.DisplayName + "».",
                        Colors.Red);
                    return;
                }

                // One identical native ship per side. War Sails now owns all
                // placement, buoyancy, collision, dynamic navmesh and crew.
                ApplyHullToSide(dataSource, "PlayerSide", hull);
                ApplyHullToSide(dataSource, "EnemySide", hull);

                object canConfirmValue = dataSource.GetType()
                    .GetProperty(
                        "CanConfirm",
                        BindingFlags.Instance | BindingFlags.Public)
                    ?.GetValue(dataSource);
                if (!(canConfirmValue is bool canConfirm) || !canConfirm)
                {
                    ClearPending(
                        "War Sails отклонил выбранный корпус для этого боя.",
                        Colors.Red);
                    return;
                }

                RatingRosterResult roster = PrepareRatingRosterAndBattleData(
                    dataSource,
                    battle);
                DikozemieNavalAutoboardingController.BeginSession(
                    battle.HullId,
                    GetAutoboardingStartDistance(battle.ShipClass),
                    battle.CrewCapacity,
                    roster.Summary,
                    roster.RuntimeCharacterIds,
                    _pendingSmokeResultAcknowledge);
                KeepNativeSetupScreenAsLifecycleOwner(topScreen);
                StartPreparedNativeBattle(dataSource, roster.BattleData);
                DeleteSmokeHullMarker();
                _pendingBattle = null;
                _phaseStartedAt = default;
                _nativeGameRequested = false;
                _pendingSmokeResultAcknowledge = false;
                _status = "Морской бой начался: «" +
                    battle.DisplayName + "», " +
                    battle.CrewCapacity + " на " +
                    battle.CrewCapacity + ".";
                Console.WriteLine(
                    "DikozemieOnline: native naval mission started hull=" +
                    battle.HullId);
            }
            catch (TargetInvocationException exception)
            {
                DikozemieNavalAutoboardingController.CancelSession();
                RequestReturnToMultiplayer("mission launch failed");
                Exception cause = exception.InnerException ?? exception;
                DikozemieNavalAutoboardingController.RecordLaunchFailure(
                    cause);
                Console.WriteLine(
                    "DikozemieOnline: naval preset invocation failed: " +
                    cause);
                ClearPending(
                    "Не удалось применить морской пресет: " +
                    GetUsefulMessage(cause),
                    Colors.Red);
            }
            catch (Exception exception)
            {
                DikozemieNavalAutoboardingController.CancelSession();
                RequestReturnToMultiplayer("mission launch failed");
                DikozemieNavalAutoboardingController.RecordLaunchFailure(
                    exception);
                Console.WriteLine(
                    "DikozemieOnline: naval preset failed: " + exception);
                ClearPending(
                    "Не удалось применить морской пресет: " +
                    GetUsefulMessage(exception),
                    Colors.Red);
            }
        }

        private static void KeepNativeSetupScreenAsLifecycleOwner(
            object topScreen)
        {
            if (!ReferenceEquals(ScreenManager.TopScreen, topScreen))
            {
                throw new InvalidOperationException(
                    "War Sails setup screen changed before preset launch.");
            }

            _nativeSetupScreen = topScreen as ScreenBase ??
                throw new InvalidOperationException(
                    "War Sails setup is not a ScreenBase.");
            if (!_nativeSetupSuppressedBeforeFirstFrame)
            {
                throw new InvalidOperationException(
                    "War Sails setup was not hidden before its first frame.");
            }
            _nativeSetupScreen.DeactivateAllLayers();

            // StartGame pushes the mission over this screen in the same
            // application tick. Keeping it underneath is essential: War
            // Sails needs a valid lifecycle owner when its mission pops.
            // The player never receives an input tick on the setup screen.
            DikozemieNavalAutoboardingController.RecordLockedPresetLaunch();
            Console.WriteLine(
                "DikozemieOnline: native naval setup hidden " +
                "playerConfigurable=false presetLocked=true " +
                "hiddenBeforeFirstFrame=true");
        }

        private static void ArmNativeSetupScreenSuppression()
        {
            DisarmNativeSetupScreenSuppression();
            _nativeSetupScreen = null;
            _nativeSetupSuppressedBeforeFirstFrame = false;
            _nativeSetupSuppressionArmed = true;
            ScreenManager.OnPushScreen += OnScreenPushedDuringNavalLaunch;
        }

        private static void DisarmNativeSetupScreenSuppression()
        {
            ScreenManager.OnPushScreen -= OnScreenPushedDuringNavalLaunch;
            _nativeSetupSuppressionArmed = false;
        }

        private static void OnScreenPushedDuringNavalLaunch(
            ScreenBase pushedScreen)
        {
            if (!_nativeSetupSuppressionArmed ||
                !string.Equals(
                    pushedScreen?.GetType().FullName,
                    NavalScreenType,
                    StringComparison.Ordinal))
            {
                return;
            }

            // ScreenManager raises OnPushScreen synchronously after the
            // native setup layers are created but before it can render its
            // first frame. Keep the screen only as War Sails' lifecycle owner;
            // the player must see loading and then the deck, never its Empire
            // versus Empire configuration form.
            DisarmNativeSetupScreenSuppression();
            _nativeSetupScreen = pushedScreen;
            _nativeSetupScreen.DeactivateAllLayers();
            _nativeSetupSuppressedBeforeFirstFrame = true;
            Console.WriteLine(
                "DikozemieOnline: native naval setup suppressed " +
                "hiddenLoader=true hiddenBeforeFirstFrame=true");
        }

        private static void RequestReturnToMultiplayer(string reason)
        {
            if (_nativeSetupScreen == null ||
                _returnPhase != NavalReturnPhase.None)
            {
                return;
            }

            LobbySessionReset.Reset();
            _returnPhase = NavalReturnPhase.WaitForSetupOwner;
            _returnPhaseStartedAt = DateTimeOffset.UtcNow;
            _nextReturnAttemptAt = default;
            _status = "Возвращаемся в сетевое лобби…";
            DikozemieNavalAutoboardingController.RecordLifecycle(
                "requested reason=" + reason);
        }

        private static bool TickReturnToMultiplayer()
        {
            if (_returnPhase == NavalReturnPhase.None)
            {
                return false;
            }
            if (DateTimeOffset.UtcNow < _nextReturnAttemptAt)
            {
                return true;
            }

            try
            {
                switch (_returnPhase)
                {
                    case NavalReturnPhase.WaitForSetupOwner:
                    {
                        DateTimeOffset now = DateTimeOffset.UtcNow;
                        TimeSpan returnElapsed =
                            now - _returnPhaseStartedAt;
                        Mission activeMission = Mission.Current;
                        if (DikozemieNavalCompletionRules
                            .ShouldForceNativeShutdown(
                                activeMission != null,
                                returnElapsed))
                        {
                            CloseGlobalMenusBeforeShutdown();
                            (ScreenManager.TopScreen ?? _nativeSetupScreen)
                                ?.DeactivateAllLayers();
                            _returnPhase =
                                NavalReturnPhase.WaitForNativeShutdown;
                            _returnPhaseStartedAt = now;
                            DikozemieNavalAutoboardingController
                                .RecordLifecycle(
                                    "native shutdown forced " +
                                    "missionStillOpen=true");
                            activeMission.EndMission();
                            MBGameManager.EndGame();
                            return true;
                        }
                        if (DikozemieNavalCompletionRules
                            .ShouldRetryMissionExit(
                                activeMission != null,
                                returnElapsed))
                        {
                            DikozemieNavalAutoboardingController
                                .RecordLifecycle(
                                    "mission exit watchdog retry elapsed=" +
                                    returnElapsed.TotalSeconds
                                        .ToString("0.0") + "s");
                            activeMission.EndMission();
                            _nextReturnAttemptAt =
                                now + TimeSpan.FromSeconds(1d);
                            return true;
                        }
                        if (activeMission != null)
                        {
                            return true;
                        }

                        bool setupIsTop = ReferenceEquals(
                            ScreenManager.TopScreen,
                            _nativeSetupScreen);
                        if (!setupIsTop &&
                            DateTimeOffset.UtcNow - _returnPhaseStartedAt <
                            TimeSpan.FromSeconds(15d))
                        {
                            return true;
                        }

                        // Prevent even one configurable frame while the
                        // native custom game is being torn down.
                        CloseGlobalMenusBeforeShutdown();
                        (ScreenManager.TopScreen ?? _nativeSetupScreen)
                            ?.DeactivateAllLayers();
                        _returnPhase = NavalReturnPhase.WaitForNativeShutdown;
                        _returnPhaseStartedAt = DateTimeOffset.UtcNow;
                        DikozemieNavalAutoboardingController.RecordLifecycle(
                            "native shutdown requested setupOwner=" +
                            setupIsTop);
                        MBGameManager.EndGame();
                        return true;
                    }
                    case NavalReturnPhase.WaitForNativeShutdown:
                    {
                        // This phase starts right after MBGameManager.EndGame()
                        // of the War Sails custom game.
                        if (DikozemieNavalCompletionRules.IsMultiplayerBackOnItsOwn(
                                customGameEnded: true,
                                multiplayerGameActive: Game.Current?.GameType is MultiplayerGame))
                        {
                            // Bannerlord 1.5.7: the end of the custom game
                            // returns to the initial state, and the multiplayer
                            // module starts a new multiplayer game by itself.
                            // Its LobbyState logs in on its own.
                            LobbySessionReset.Reset();
                            _returnPhase = NavalReturnPhase.WaitForMultiplayer;
                            _returnPhaseStartedAt = DateTimeOffset.UtcNow;
                            DikozemieNavalAutoboardingController.RecordLifecycle(
                                "multiplayer restarted by the game");
                            return true;
                        }
                        if (Game.Current != null ||
                            GameStateManager.Current == null)
                        {
                            return true;
                        }

                        if (!LobbySessionReset.TryPrepareFreshLobby(
                            NetworkMain.GameClient,
                            DateTimeOffset.UtcNow,
                            DikozemieNavalAutoboardingController.RecordLifecycle))
                        {
                            _status = "Переподключаем сетевое лобби…";
                            return true;
                        }

                        MBGameManager.StartNewGame(
                            new MultiplayerGameManager());
                        _returnPhase = NavalReturnPhase.WaitForMultiplayer;
                        _returnPhaseStartedAt = DateTimeOffset.UtcNow;
                        DikozemieNavalAutoboardingController.RecordLifecycle(
                            "multiplayer restart requested");
                        return true;
                    }
                    case NavalReturnPhase.WaitForMultiplayer:
                    {
                        bool lobbyActive =
                            Game.Current?.GameType is MultiplayerGame &&
                            Game.Current.GameStateManager.ActiveState is LobbyState;
                        if (!lobbyActive || NetworkMain.GameClient?.AtLobby != true)
                        {
                            if (!DikozemieNavalCompletionRules.ShouldStopWaitingForLobbyLogin(
                                    lobbyActive,
                                    DateTimeOffset.UtcNow - _returnPhaseStartedAt))
                            {
                                return true;
                            }
                            DikozemieNavalAutoboardingController.RecordLifecycle(
                                "multiplayer lobby shown before its login finished");
                        }

                        _nativeSetupScreen = null;
                        _nativeSetupSuppressedBeforeFirstFrame = false;
                        _returnPhase = NavalReturnPhase.None;
                        _returnPhaseStartedAt = default;
                        _nextReturnAttemptAt = default;
                        _status = "Морской бой завершён";
                        DikozemieNavalAutoboardingController.RecordLifecycle(
                            "multiplayer completed");
                        return false;
                    }
                    default:
                        return true;
                }
            }
            catch (Exception exception)
            {
                _nextReturnAttemptAt = DateTimeOffset.UtcNow +
                    TimeSpan.FromSeconds(2d);
                Console.WriteLine(
                    "DikozemieOnline: naval return retry phase=" +
                    _returnPhase + " error=" + exception);
                return true;
            }
        }

        // The hero menu is a global layer: it survives screen changes and
        // must not stay open while EndGame destroys the objects it shows.
        private static void CloseGlobalMenusBeforeShutdown()
        {
            if (!DikozemieHeroMenuController.IsOpen)
            {
                return;
            }
            try
            {
                DikozemieHeroMenuController.Hide();
                DikozemieNavalAutoboardingController.RecordLifecycle(
                    "hero menu closed before native shutdown");
            }
            catch (Exception error)
            {
                DikozemieNavalAutoboardingController.RecordLifecycle(
                    "hero menu close failed: " + error.Message);
            }
        }

        private static void ApplyHullToSide(
            object dataSource,
            string sidePropertyName,
            ShipHull hull)
        {
            object side = dataSource.GetType()
                .GetProperty(
                    sidePropertyName,
                    BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(dataSource);
            object selectionGroup = side?.GetType()
                .GetProperty(
                    "ShipSelectionGroup",
                    BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(side);
            object selectionItems = selectionGroup?.GetType()
                .GetProperty(
                    "ShipSelectionItems",
                    BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(selectionGroup);
            if (!(selectionItems is IList items) || items.Count == 0)
            {
                throw new InvalidOperationException(
                    "War Sails did not expose ship selection for " +
                    sidePropertyName + ".");
            }

            for (int index = 0; index < items.Count; index++)
            {
                object item = items[index];
                MethodInfo setHull = item?.GetType().GetMethod(
                    "SetHull",
                    BindingFlags.Instance | BindingFlags.Public);
                if (setHull == null)
                {
                    throw new MissingMethodException(
                        item?.GetType().FullName ?? "Naval ship slot",
                        "SetHull");
                }

                setHull.Invoke(
                    item,
                    new object[] { index == 0 ? hull : null });
            }
        }

        private static RatingRosterResult PrepareRatingRosterAndBattleData(
            object dataSource,
            DikozemieNavalBattleDefinition battle)
        {
            EnsureArenaObjectCatalogLoaded();

            MethodInfo prepareBattleData = dataSource.GetType().GetMethod(
                "PrepareBattleData",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (prepareBattleData == null)
            {
                throw new MissingMethodException(
                    dataSource.GetType().FullName,
                    "PrepareBattleData");
            }

            object battleData = prepareBattleData.Invoke(dataSource, null);
            if (battleData == null)
            {
                throw new InvalidOperationException(
                    "War Sails returned no naval battle data.");
            }

            Type dataType = battleData.GetType();
            BasicCharacterObject playerCharacter = dataType
                .GetField("PlayerCharacter", BindingFlags.Instance |
                    BindingFlags.Public)
                ?.GetValue(battleData) as BasicCharacterObject;
            CustomBattleCombatant originalPlayerParty = dataType
                .GetField("PlayerParty", BindingFlags.Instance |
                    BindingFlags.Public)
                ?.GetValue(battleData) as CustomBattleCombatant;
            CustomBattleCombatant originalEnemyParty = dataType
                .GetField("EnemyParty", BindingFlags.Instance |
                    BindingFlags.Public)
                ?.GetValue(battleData) as CustomBattleCombatant;
            if (playerCharacter == null || originalPlayerParty == null ||
                originalEnemyParty == null)
            {
                throw new InvalidOperationException(
                    "War Sails returned an incomplete naval roster.");
            }

            PrepareSavedArenaHero(playerCharacter);

            int playerRating = Math.Max(
                0,
                DikozemieArmoryProgression.PlayerRating);
            int requiredBots = Math.Max(1, battle.TotalCombatants - 1);
            IReadOnlyList<DikozemieArenaOpponent> allOpponents =
                DikozemieArenaOpponentCatalog.Build(playerCharacter);
            IReadOnlyList<DikozemieArenaOpponent> field =
                DikozemieArenaOpponentCatalog.SelectTournamentField(
                    allOpponents,
                    requiredBots,
                    playerRating,
                    mountedOnly: false,
                    fallback: playerCharacter);
            if (field.Count != requiredBots)
            {
                throw new InvalidOperationException(
                    "Для рейтингового экипажа нужно " + requiredBots +
                    " бойцов, доступно " + field.Count + ".");
            }

            List<int> ratings = field
                .Select(DikozemieArenaOpponentCatalog.GetCurrentRating)
                .ToList();
            bool[] friendlyAssignments =
                DikozemieArenaTeamRules.BuildBalancedTeamAssignment(
                    ratings,
                    battle.CrewCapacity - 1,
                    battle.CrewCapacity,
                    playerRating);

            var playerParty = new CustomBattleCombatant(
                originalPlayerParty.Name,
                originalPlayerParty.BasicCulture,
                originalPlayerParty.Banner,
                originalPlayerParty.CurrentBattleEnvironment)
            {
                Side = originalPlayerParty.Side
            };
            playerParty.AddCharacter(playerCharacter, 1);
            playerParty.SetGeneral(playerCharacter);

            var enemyParty = new CustomBattleCombatant(
                originalEnemyParty.Name,
                originalEnemyParty.BasicCulture,
                originalEnemyParty.Banner,
                originalEnemyParty.CurrentBattleEnvironment)
            {
                Side = originalEnemyParty.Side
            };

            long friendlyRatingTotal = playerRating;
            long enemyRatingTotal = 0L;
            BasicCharacterObject enemyGeneral = null;
            int friendlyCount = 1;
            int enemyCount = 0;
            for (int index = 0; index < field.Count; index++)
            {
                DikozemieArenaOpponent selected = field[index];
                BasicCharacterObject character =
                    DikozemieArenaOpponentCatalog.ResolveCharacter(
                        selected,
                        playerCharacter);
                character = PrepareArenaFootCharacter(character);
                int rating = ratings[index];
                if (friendlyAssignments[index])
                {
                    playerParty.AddCharacter(character, 1);
                    friendlyRatingTotal += rating;
                    friendlyCount++;
                }
                else
                {
                    enemyParty.AddCharacter(character, 1);
                    enemyGeneral = enemyGeneral ?? character;
                    enemyRatingTotal += rating;
                    enemyCount++;
                }
            }

            if (friendlyCount != battle.CrewCapacity ||
                enemyCount != battle.CrewCapacity ||
                enemyGeneral == null)
            {
                throw new InvalidOperationException(
                    "Рейтинговая жеребьёвка нарушила размер экипажа: " +
                    friendlyCount + " против " + enemyCount + ".");
            }
            enemyParty.SetGeneral(enemyGeneral);

            dataType.GetField(
                    "PlayerParty",
                    BindingFlags.Instance | BindingFlags.Public)
                ?.SetValue(battleData, playerParty);
            dataType.GetField(
                    "EnemyParty",
                    BindingFlags.Instance | BindingFlags.Public)
                ?.SetValue(battleData, enemyParty);

            int friendlyAverage = (int)(friendlyRatingTotal /
                Math.Max(1, friendlyCount));
            int enemyAverage = (int)(enemyRatingTotal /
                Math.Max(1, enemyCount));
            int minimumRating = ratings.Count > 0 ? ratings.Min() : 0;
            int maximumRating = ratings.Count > 0 ? ratings.Max() : 0;
            int distinctIdentities = field
                .Select(opponent => opponent.CharacterId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            string summary =
                "ratingRoster=true crew=" + battle.CrewCapacity +
                "v" + battle.CrewCapacity +
                " playerProfile=true arenaEquipment=true" +
                " closestRatingReserve=true" +
                " playerRating=" + playerRating +
                " friendlyAverage=" + friendlyAverage +
                " enemyAverage=" + enemyAverage +
                " range=" + minimumRating + ".." + maximumRating +
                " distinct=" + distinctIdentities;
            Console.WriteLine(
                "DikozemieOnline: naval " + summary);
            string[] runtimeCharacterIds = playerParty.Characters
                .Concat(enemyParty.Characters)
                .Select(character => character?.StringId ?? string.Empty)
                .ToArray();
            return new RatingRosterResult(
                battleData,
                summary,
                runtimeCharacterIds);
        }

        private static void EnsureArenaObjectCatalogLoaded()
        {
            MBObjectManager objects = Game.Current?.ObjectManager ??
                throw new InvalidOperationException(
                    "War Sails object catalog is unavailable.");
            List<string> missingEquipment = GetMissingSavedFootEquipment(
                objects);
            int runtimeCharacterCount = CountArenaRuntimeCharacters(objects);
            if (missingEquipment.Count > 0 || runtimeCharacterCount < 2)
            {
                throw new InvalidOperationException(
                    "War Sails не получил стартовый каталог арены: отсутствует " +
                    (missingEquipment.Count > 0
                        ? "снаряжение " + string.Join(",", missingEquipment)
                        : "пул рейтинговых бойцов") + ".");
            }

            Console.WriteLine(
                "DikozemieOnline: naval arena object bridge ready " +
                "savedEquipmentExact=true runtimeCharacters=" +
                runtimeCharacterCount);
        }

        private static List<string> GetMissingSavedFootEquipment(
            MBObjectManager objects)
        {
            DikozemieHeroProfileState profile = DikozemieHeroProfile.Snapshot;
            return profile?.Equipment?
                .Where(saved => saved != null &&
                    saved.Slot >= (int)EquipmentIndex.Weapon0 &&
                    saved.Slot <= (int)EquipmentIndex.Cape &&
                    !string.IsNullOrWhiteSpace(saved.ItemId) &&
                    objects.GetObject<ItemObject>(saved.ItemId) == null)
                .Select(saved => saved.ItemId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
        }

        private static int CountArenaRuntimeCharacters(
            MBObjectManager objects)
        {
            return objects.GetObjectTypeList<BasicCharacterObject>()
                .Count(character => character != null &&
                    character.StringId != null &&
                    character.StringId.StartsWith(
                        "mp_",
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(
                        character.StringId,
                        "mp_character",
                        StringComparison.OrdinalIgnoreCase) &&
                    (character.FirstBattleEquipment != null ||
                        character.Equipment != null));
        }

        private static void PrepareSavedArenaHero(
            BasicCharacterObject playerCharacter)
        {
            DikozemieHeroProfileState profile = DikozemieHeroProfile.Snapshot;
            if (playerCharacter == null || profile == null ||
                !profile.CreationCompleted)
            {
                throw new InvalidOperationException(
                    "Сначала завершите создание героя арены.");
            }

            Equipment equipment = DikozemieHeroProfile
                .CreateBattleEquipment(playerCharacter);
            if (equipment == null)
            {
                throw new InvalidOperationException(
                    "Сохранённое снаряжение героя недоступно.");
            }

            // A sailor fights on foot, exactly like the arena's Foot
            // discipline. The saved inventory is not changed; only this
            // mission copy loses the horse and harness.
            equipment = new Equipment(equipment);
            equipment[EquipmentIndex.Horse] = default(EquipmentElement);
            equipment[EquipmentIndex.HorseHarness] =
                default(EquipmentElement);
            ApplyEquipmentToAllBattleLoadouts(playerCharacter, equipment);

            if (BodyProperties.FromString(
                    profile.BodyProperties,
                    out BodyProperties bodyProperties))
            {
                playerCharacter.UpdatePlayerCharacterBodyProperties(
                    bodyProperties,
                    profile.Race,
                    profile.IsFemale);
                playerCharacter.Age = bodyProperties.Age;
            }
            else
            {
                playerCharacter.IsFemale = profile.IsFemale;
                playerCharacter.Race = profile.Race;
            }
            playerCharacter.Level = Math.Max(1, profile.Level);

            FieldInfo nameField = typeof(BasicCharacterObject).GetField(
                "_basicName",
                BindingFlags.Instance | BindingFlags.NonPublic);
            nameField?.SetValue(
                playerCharacter,
                new TextObject(profile.Name ?? "Герой арены"));
            ApplySavedSkillsToCharacter(playerCharacter, profile);

            Console.WriteLine(
                "DikozemieOnline: naval saved arena hero prepared " +
                "playerProfile=true arenaEquipment=true character=" +
                playerCharacter.StringId + " name=" + profile.Name +
                " equipment=" + DescribeEquipment(equipment));
        }

        private static void ApplySavedSkillsToCharacter(
            BasicCharacterObject playerCharacter,
            DikozemieHeroProfileState profile)
        {
            if (playerCharacter == null || profile?.Skills == null)
            {
                return;
            }

            var savedSkills = new MBCharacterSkills();
            foreach (DikozemieHeroValue saved in profile.Skills)
            {
                if (saved == null || string.IsNullOrWhiteSpace(saved.Id))
                {
                    continue;
                }
                SkillObject skill = Game.Current?.ObjectManager
                    ?.GetObject<SkillObject>(saved.Id) ??
                    MBObjectManager.Instance.GetObject<SkillObject>(saved.Id);
                if (skill != null)
                {
                    savedSkills.Skills.SetPropertyValue(
                        skill,
                        Math.Max(0, saved.Value));
                }
            }

            FieldInfo skillsField = typeof(BasicCharacterObject).GetField(
                "DefaultCharacterSkills",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (skillsField == null)
            {
                throw new MissingFieldException(
                    typeof(BasicCharacterObject).FullName,
                    "DefaultCharacterSkills");
            }
            skillsField.SetValue(playerCharacter, savedSkills);
        }

        private static BasicCharacterObject PrepareArenaFootCharacter(
            BasicCharacterObject character)
        {
            if (character == null)
            {
                return null;
            }

            foreach (Equipment equipment in GetBattleLoadouts(character))
            {
                equipment[EquipmentIndex.Horse] = default(EquipmentElement);
                equipment[EquipmentIndex.HorseHarness] =
                    default(EquipmentElement);
            }
            return character;
        }

        private static void ApplyEquipmentToAllBattleLoadouts(
            BasicCharacterObject character,
            Equipment source)
        {
            List<Equipment> loadouts = GetBattleLoadouts(character);
            if (loadouts.Count == 0)
            {
                throw new InvalidOperationException(
                    "War Sails player character has no battle loadout.");
            }

            foreach (Equipment target in loadouts)
            {
                for (int slot = 0; slot < 12; slot++)
                {
                    target[slot] = source[slot];
                }
            }
        }

        private static List<Equipment> GetBattleLoadouts(
            BasicCharacterObject character)
        {
            var loadouts = character?.BattleEquipments?
                .Where(equipment => equipment != null)
                .Distinct()
                .ToList() ?? new List<Equipment>();
            Equipment fallback = character?.FirstBattleEquipment ??
                character?.Equipment;
            if (fallback != null && fallback.IsBattle &&
                !loadouts.Contains(fallback))
            {
                loadouts.Add(fallback);
            }
            return loadouts;
        }

        private static string DescribeEquipment(Equipment equipment)
        {
            return string.Join(
                ",",
                Enumerable.Range(0, 12)
                    .Select(index => equipment[index].Item?.StringId)
                    .Where(id => !string.IsNullOrWhiteSpace(id)));
        }

        private static void StartPreparedNativeBattle(
            object dataSource,
            object battleData)
        {
            Type helperType = dataSource.GetType().Assembly.GetType(
                "NavalDLC.CustomBattle.CustomBattle." +
                "NavalCustomBattleHelper",
                throwOnError: false,
                ignoreCase: false);
            MethodInfo startGame = helperType?.GetMethod(
                "StartGame",
                BindingFlags.Static | BindingFlags.Public);
            if (startGame == null)
            {
                throw new MissingMethodException(
                    helperType?.FullName ?? "NavalCustomBattleHelper",
                    "StartGame");
            }
            startGame.Invoke(null, new[] { battleData });
        }

        private static float GetAutoboardingStartDistance(
            DikozemieNavalShipClass shipClass)
        {
            switch (shipClass)
            {
                case DikozemieNavalShipClass.Light:
                    return 72f;
                case DikozemieNavalShipClass.Medium:
                    return 90f;
                default:
                    return 118f;
            }
        }

        private sealed class RatingRosterResult
        {
            internal RatingRosterResult(
                object battleData,
                string summary,
                IReadOnlyList<string> runtimeCharacterIds)
            {
                BattleData = battleData;
                Summary = summary ?? string.Empty;
                RuntimeCharacterIds = runtimeCharacterIds ??
                    Array.Empty<string>();
            }

            internal object BattleData { get; }

            internal string Summary { get; }

            internal IReadOnlyList<string> RuntimeCharacterIds { get; }
        }

        private enum NavalReturnPhase
        {
            None,
            WaitForSetupOwner,
            WaitForNativeShutdown,
            WaitForMultiplayer
        }

        private static void TryStartSmokeRequest()
        {
            TryRearmSmokeRequest();
            if (_smokeRequestHandled)
            {
                return;
            }

            string hullId = ResolveSmokeHullId();
            if (string.IsNullOrWhiteSpace(hullId))
            {
                _smokeRequestHandled = true;
                return;
            }
            if (Game.Current == null || ScreenManager.TopScreen == null)
            {
                return;
            }
            if (_smokeReadyAt == default)
            {
                _smokeReadyAt = DateTimeOffset.UtcNow;
                return;
            }
            if (DateTimeOffset.UtcNow - _smokeReadyAt <
                TimeSpan.FromSeconds(3d))
            {
                return;
            }

            _smokeRequestHandled = true;
            DikozemieNavalBattleDefinition battle =
                DikozemieNavalBattleCatalog.Find(hullId.Trim());
            if (TryStart(
                    battle,
                    out string errorMessage,
                    smokeResultAcknowledge: true))
            {
                Console.WriteLine(
                    "DikozemieOnline: native naval smoke request accepted " +
                    "hull=" + battle.HullId);
                return;
            }

            Console.WriteLine(
                "DikozemieOnline: native naval smoke request rejected: " +
                errorMessage);
            DeleteSmokeHullMarker();
            InformationManager.DisplayMessage(
                new InformationMessage(errorMessage, Colors.Red));
        }

        private static void TryRearmSmokeRequest()
        {
            if (!_smokeRequestHandled || _pendingBattle != null ||
                _returnPhase != NavalReturnPhase.None ||
                !(Game.Current?.GameType is MultiplayerGame))
            {
                return;
            }

            try
            {
                if (!File.Exists(GetSmokeHullMarkerPath()))
                {
                    return;
                }

                _smokeRequestHandled = false;
                _smokeReadyAt = default;
                Console.WriteLine(
                    "DikozemieOnline: next naval smoke request queued " +
                    "in current multiplayer lobby");
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "DikozemieOnline: next naval smoke marker failed: " +
                    exception.Message);
            }
        }

        private static string ResolveSmokeHullId()
        {
            string markerPath = GetSmokeHullMarkerPath();
            try
            {
                if (File.Exists(markerPath))
                {
                    return File.ReadAllText(markerPath).Trim();
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "DikozemieOnline: naval smoke marker read failed: " +
                    exception.Message);
            }

            string hullId = Environment.GetEnvironmentVariable(
                "DIKOZEMIE_NAVAL_SMOKE_HULL");
            if (!string.IsNullOrWhiteSpace(hullId))
            {
                return hullId;
            }

            // Steam may replace the directly started process on a cold
            // launch. Command-line arguments survive that hand-off, while
            // process-local environment variables do not.
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (argument != null && argument.StartsWith(
                        SmokeHullArgument,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return argument.Substring(SmokeHullArgument.Length);
                }
            }

            return string.Empty;
        }

        private static string GetSmokeHullMarkerPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "Dikozemie",
                SmokeHullMarkerName);
        }

        private static void DeleteSmokeHullMarker()
        {
            try
            {
                string markerPath = GetSmokeHullMarkerPath();
                if (File.Exists(markerPath))
                {
                    File.Delete(markerPath);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    "DikozemieOnline: naval smoke marker cleanup failed: " +
                    exception.Message);
            }
        }

        private static void ClearPending(string message, Color color)
        {
            DisarmNativeSetupScreenSuppression();
            _pendingBattle = null;
            _phaseStartedAt = default;
            _nativeGameRequested = false;
            _pendingSmokeResultAcknowledge = false;
            _status = message ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(message))
            {
                InformationManager.DisplayMessage(
                    new InformationMessage(message, color));
            }
        }

        private static ICustomBattleProvider FindNavalProvider()
        {
            return CustomBattleFactory.CollectProviders()
                .FirstOrDefault(provider => string.Equals(
                    provider?.GetType().FullName,
                    NavalProviderType,
                    StringComparison.Ordinal));
        }

        private static string GetUsefulMessage(Exception error)
        {
            return string.IsNullOrWhiteSpace(error?.Message)
                ? "неизвестная ошибка"
                : error.Message;
        }
    }
}
