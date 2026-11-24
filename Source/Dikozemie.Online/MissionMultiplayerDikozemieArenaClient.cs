using System;
using NetworkMessages.FromServer;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Dikozemie.Online
{
    public sealed class MissionMultiplayerDikozemieArenaClient : MissionMultiplayerGameModeBaseClient
    {
#if CLIENT
        private bool _localPeerInitialized;
        private bool _profileSent;
        private bool _profileAcknowledged;
        private float _nextProfileSendAt;
        private int _profileSendAttempts;
        private bool _profileUnavailableReported;
        private bool _representativeUnavailableReported;
#endif

        public override bool IsGameModeUsingGold => true;
        public override bool IsGameModeTactical => false;
        public override bool IsGameModeUsingRoundCountdown => false;
        public override bool IsGameModeUsingAllowCultureChange => false;
        public override bool IsGameModeUsingAllowTroopChange => false;
        public override MultiplayerGameType GameType => MultiplayerGameType.TeamDeathmatch;

#if CLIENT
        // The observer of the watch flies freely over the field (plan,
        // «Античит»: «дежурный входит в бой невидимым и летает по полю»).
        public override SpectatorCameraTypes GetMissionCameraLockMode(bool lockedToMainPlayer)
        {
            if (DikozemieWatch.IsWatching && !lockedToMainPlayer)
            {
                return SpectatorCameraTypes.Free;
            }
            return base.GetMissionCameraLockMode(lockedToMainPlayer);
        }
#endif

        public DikozemieArenaRepresentative MyRepresentative { get; private set; }
        public event Action MyRepresentativeAssigned;
#if CLIENT
        public event Action<DikozemieArenaMatchResultMessage> MatchSeriesCompleted;
        public event Action<DikozemieArenaTrainingResultMessage> TrainingSeriesCompleted;
        public event Action<DikozemieArenaKillProgressMessage> KillProgressUpdated;
        public event Action<int> DeathSummaryReceived;
        public event Action<string, int> SkillProgressUpdated;

        internal int LastCoinChange { get; private set; }
#endif

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            MissionNetworkComponent.OnMyClientSynchronized += OnMyClientSynchronized;
        }

        public override void OnRemoveBehavior()
        {
            MissionNetworkComponent.OnMyClientSynchronized -= OnMyClientSynchronized;
#if CLIENT
            if (MyRepresentative != null)
            {
                MyRepresentative.ProgressUpdated -= OnProgressUpdated;
            }
#endif
            MyRepresentative?.AddRemoveMessageHandlers(GameNetwork.NetworkMessageHandlerRegisterer.RegisterMode.Remove);
            base.OnRemoveBehavior();
        }

        public override void AfterStart()
        {
            Mission.SetMissionMode(MissionMode.Battle, true);
        }

#if CLIENT
        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);
            if (agent == null || !agent.IsHuman || !agent.IsMine)
            {
                return;
            }

            try
            {
                Equipment equipment = agent.SpawnEquipment;
                var items = new System.Text.StringBuilder();
                for (int index = 0; index < (int)EquipmentIndex.NumEquipmentSetSlots; index++)
                {
                    ItemObject item = equipment?[index].Item;
                    if (item == null)
                    {
                        continue;
                    }
                    if (items.Length > 0)
                    {
                        items.Append(',');
                    }
                    items.Append(item.StringId).Append('#').Append(item.Id.InternalValue)
                        .Append(item.IsInitialized ? string.Empty : "(uninitialized)");
                }
                Debug.Print("DikozemieOnline: own agent built equipment=" + items +
                    " female=" + agent.IsFemale + " character=" + agent.Character?.StringId);
            }
            catch (Exception error)
            {
                Debug.Print("DikozemieOnline: own agent equipment trace failed: " + error.Message);
            }
        }
#endif

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
#if CLIENT
            // On a fast local join the synchronization event can fire before
            // this behavior has subscribed to it. Polling the authoritative
            // peer state gives us an idempotent fallback and prevents the
            // server's 15-second profile-admission timeout.
            if (GameNetwork.IsClient &&
                GameNetwork.MyPeer?.IsSynchronized == true)
            {
                // Search is complete only after the real UDP mission peer is
                // synchronized.  Until then the global Cancel action remains
                // authoritative and can still revoke a pending custom join.
                DikozemiePvpSearchController.ConfirmMissionStarted();
                InitializeLocalPeerAndSendProfile();
            }
#endif
        }

        private void OnMyClientSynchronized()
        {
#if CLIENT
            InitializeLocalPeerAndSendProfile();
#else
            MyRepresentative = GameNetwork.MyPeer.GetComponent<DikozemieArenaRepresentative>();
            MyRepresentative?.AddRemoveMessageHandlers(GameNetwork.NetworkMessageHandlerRegisterer.RegisterMode.Add);
#endif
            MyRepresentativeAssigned?.Invoke();
        }

#if CLIENT
        private void InitializeLocalPeerAndSendProfile()
        {
            // The campaign profile is what authorizes the real hero spawn.  It
            // must not depend on the representative component: on a dedicated
            // join that component can arrive a few frames after synchronization.
            // Returning for a missing representative here previously meant the
            // profile was never sent at all and the player remained unspawned.
            MissionPeer localMissionPeer =
                GameNetwork.MyPeer?.GetComponent<MissionPeer>();
            if (localMissionPeer?.ControlledAgent != null)
            {
                _profileAcknowledged = true;
            }
            if (!_profileAcknowledged &&
                (!_profileSent || Mission.CurrentTime >= _nextProfileSendAt) &&
                _profileSendAttempts < 12)
            {
                DikozemieArenaCharacterProfile profile =
                    DikozemieRankedHandoffClient.CharacterProfile ??
                    DikozemieArenaLobbyController.CreateDirectLobbyProfile();
                if (profile == null)
                {
                    if (!_profileUnavailableReported)
                    {
                        _profileUnavailableReported = true;
                        Console.WriteLine(
                            "DikozemieOnline: created hero profile is not ready; retrying");
                        DikozemieLobbyShellIntegration.TraceButtonAction(
                            "arena-profile unavailable; retrying");
                    }
                }
                else
                {
                    try
                    {
                        DikozemieArenaProfileTransport.Send(profile);
                        _profileSent = true;
                        _profileSendAttempts++;
                        _nextProfileSendAt = Mission.CurrentTime + 2f;
                        string sent =
                            "arena-profile sent character=" +
                            profile.StartingCharacterId +
                            " attempt=" + _profileSendAttempts;
                        DikozemieLobbyShellIntegration.TraceButtonAction(sent);
                        Console.WriteLine("DikozemieOnline: " + sent);
                    }
                    catch (Exception error)
                    {
                        DikozemieLobbyShellIntegration.TraceButtonAction(
                            "arena-profile send failed: " + error.Message);
                        Console.WriteLine(
                            "DikozemieOnline: created hero profile send failed: " +
                            error);
                    }
                }
            }

            if (!_localPeerInitialized)
            {
                DikozemieArenaRepresentative representative =
                    GameNetwork.MyPeer?.GetComponent<DikozemieArenaRepresentative>();
                if (representative == null)
                {
                    if (!_representativeUnavailableReported)
                    {
                        _representativeUnavailableReported = true;
                        DikozemieLobbyShellIntegration.TraceButtonAction(
                            "arena-representative unavailable; retrying");
                    }
                    return;
                }

                MyRepresentative = representative;
                MyRepresentative.AddRemoveMessageHandlers(
                    GameNetwork.NetworkMessageHandlerRegisterer.RegisterMode.Add);
                MyRepresentative.ProgressUpdated += OnProgressUpdated;
                DikozemieArmoryProgression.ObserveServerWins(
                    MyRepresentative.RatingWins);
                _localPeerInitialized = true;
                DikozemieLobbyShellIntegration.TraceButtonAction(
                    "arena-representative ready");
                MyRepresentativeAssigned?.Invoke();
            }
        }

#endif

        protected override void AddRemoveMessageHandlers(GameNetwork.NetworkMessageHandlerRegistererContainer registerer)
        {
            if (GameNetwork.IsClient)
            {
                registerer.Register<SyncGoldsForSkirmish>(HandleGoldUpdate);
#if CLIENT
                registerer.Register<DikozemieArenaMatchResultMessage>(HandleMatchResult);
                registerer.Register<DikozemieArenaTrainingResultMessage>(HandleTrainingResult);
                registerer.Register<DikozemieArenaKillProgressMessage>(HandleKillProgress);
                registerer.Register<DikozemieArenaDeathSummaryMessage>(HandleDeathSummary);
                registerer.Register<DikozemieArenaSkillProgressMessage>(HandleSkillProgress);
                registerer.Register<DikozemieArenaLeaderboardChunkMessage>(HandleLeaderboardChunk);
                registerer.Register<DikozemieNavalBoardingStateMessage>(HandleNavalBoardingState);
                registerer.Register<DikozemieArenaQueueStatusMessage>(HandleQueueStatus);
                registerer.Register<DikozemieArenaItemRegistryMessage>(HandleItemRegistry);
#endif
            }
        }

#if CLIENT
        private void HandleMatchResult(DikozemieArenaMatchResultMessage message)
        {
            NetworkCommunicator me = GameNetwork.MyPeer;
            bool victory = message.Winner == me;
            if (!victory && message.Loser != me)
            {
                return;
            }
            DikozemieArenaLeaderboardTransport.ApplyMatchResult(message);

            bool seriesComplete = victory
                ? message.WinnerSeriesComplete
                : message.LoserSeriesComplete;
            if (!seriesComplete && !message.IsDraw)
            {
                return;
            }
            int seriesWins = victory
                ? message.WinnerSeriesWins
                : message.LoserSeriesWins;
            int seriesLosses = victory
                ? message.WinnerSeriesLosses
                : message.LoserSeriesLosses;
            int seriesCoins = victory
                ? message.WinnerSeriesCoins
                : message.LoserSeriesCoins;
            int coinPenalty = DikozemieArenaMatchSelection.CalculateLossCoinPenalty(
                seriesLosses);
            int arenaWins = victory ? message.WinnerWins : message.LoserWins;
            int rating = victory ? message.WinnerRating : message.LoserRating;
            LastCoinChange = DikozemieArmoryProgression.ObserveArenaSeriesResult(
                rating,
                arenaWins,
                seriesCoins,
                coinPenalty);
            // The legacy handoff has only a win/loss flag, not a draw outcome.
            // Do not submit a draw as a defeat or an unfinished series.
            if (seriesComplete && !message.IsDraw)
            {
                DikozemieRankedHandoffClient.RecordMatchResult(
                    seriesWins > seriesLosses,
                    seriesWins,
                    seriesCoins,
                    rating,
                    victory ? message.WinnerWins : message.LoserWins,
                    victory ? message.WinnerLosses : message.LoserLosses);
            }
            if (seriesComplete && !message.IsDraw)
            {
                // A captive of the man-catchers fought for his freedom.
                DikozemieRoads.ObserveArenaSeries(seriesWins > seriesLosses);
            }
            if (seriesComplete || message.IsDraw)
            {
                DikozemieWorldBattle.NoteResult(seriesWins > seriesLosses, message.IsDraw);
            }
            if (seriesComplete)
            {
                DikozemieLobbyShellIntegration.NoteRoadBattleResult();
                DikozemieEconomy.NoteBattlePlayed();
            }
            MatchSeriesCompleted?.Invoke(message);
            DikozemieDuelOfferController.NotifyCombatComplete();
        }

        private void HandleTrainingResult(
            DikozemieArenaTrainingResultMessage message)
        {
            DikozemieArenaLeaderboardTransport.ApplyTrainingResult(message);
            LastCoinChange = DikozemieArmoryProgression.ObserveArenaSeriesResult(
                message.PlayerRating,
                message.PlayerWins,
                message.CoinsEarned,
                DikozemieArenaMatchSelection.CalculateLossCoinPenalty(
                    message.SeriesLosses));
            if (!message.IsDraw)
            {
                DikozemieRoads.ObserveArenaSeries(message.Victory);
            }
            DikozemieWorldBattle.NoteResult(message.Victory, message.IsDraw);
            DikozemieLobbyShellIntegration.NoteRoadBattleResult();
            DikozemieEconomy.NoteBattlePlayed();
            TrainingSeriesCompleted?.Invoke(message);
        }

        internal int QueueSecondsLeft { get; private set; }
        internal int QueuePlayers { get; private set; }
        internal int QueueCapacity { get; private set; }
        internal DateTimeOffset QueueStatusAt { get; private set; }

        private void HandleQueueStatus(DikozemieArenaQueueStatusMessage message)
        {
            QueueSecondsLeft = message.SecondsLeft;
            QueuePlayers = message.Players;
            QueueCapacity = message.Capacity;
            QueueStatusAt = DateTimeOffset.UtcNow;
        }

        private void HandleKillProgress(
            DikozemieArenaKillProgressMessage message)
        {
            KillProgressUpdated?.Invoke(message);
        }

        private static uint _itemRegistryWarnedFor;

        private void HandleItemRegistry(DikozemieArenaItemRegistryMessage message)
        {
            bool same = message.ItemCount == DikozemieItemRegistry.Count &&
                message.Fingerprint == DikozemieItemRegistry.Fingerprint;
            Debug.Print("DikozemieOnline: item registry server=" + message.ItemCount + "/" +
                message.Fingerprint.ToString("x8") + " client=" + DikozemieItemRegistry.Count + "/" +
                DikozemieItemRegistry.Fingerprint.ToString("x8") + (same ? " match" : " MISMATCH"));
            if (same || _itemRegistryWarnedFor == message.Fingerprint)
            {
                return;
            }

            _itemRegistryWarnedFor = message.Fingerprint;
            InformationManager.DisplayMessage(new InformationMessage(
                "Предметы арены на этом компьютере не совпадают с сервером: снаряжение в бою " +
                "будет показано неверно. Обновите игру в Steam и переустановите арену.",
                Colors.Red));
        }

        private void HandleDeathSummary(
            DikozemieArenaDeathSummaryMessage message)
        {
            DikozemieLobbyShellIntegration.TraceButtonAction(
                "arena-death-summary heroXp=" + message.HeroExperience);
            DeathSummaryReceived?.Invoke(message.HeroExperience);
        }

        private void HandleSkillProgress(
            DikozemieArenaSkillProgressMessage message)
        {
            string progress = DikozemieHeroProfile.ApplyArenaProgress(
                message.Victory,
                message.HeroExperience,
                message.SkillExperiencePayload);
            DikozemieLobbyShellIntegration.TraceButtonAction(
                "arena-skill-progress heroXp=" + message.HeroExperience +
                " skills=" + message.SkillExperiencePayload +
                " applied=" + progress);
            SkillProgressUpdated?.Invoke(progress, message.HeroExperience);
        }

        private static void OnProgressUpdated(
            NetworkCommunicator peer,
            int roundWins,
            int deaths,
            int rating,
            int ratingWins)
        {
            if (peer == GameNetwork.MyPeer)
            {
                DikozemieArmoryProgression.ObserveServerWins(ratingWins);
            }
        }

        private void HandleLeaderboardChunk(
            DikozemieArenaLeaderboardChunkMessage message)
        {
            DikozemieArenaLeaderboardTransport.Receive(message);
        }

        private void HandleNavalBoardingState(
            DikozemieNavalBoardingStateMessage message)
        {
            _profileAcknowledged = true;
            Mission.GetMissionBehavior<DikozemieNavalBoardingBehavior>()
                ?.ApplyRemoteState(message.Phase, message.ShipClass, message.HullIndex);
        }
#endif

        private void HandleGoldUpdate(SyncGoldsForSkirmish message)
        {
            MissionRepresentativeBase representative = message.VirtualPlayer.GetComponent<MissionRepresentativeBase>();
            OnGoldAmountChangedForRepresentative(representative, message.GoldAmount);
        }

        public override void OnGoldAmountChangedForRepresentative(MissionRepresentativeBase representative, int goldAmount)
        {
            if (representative != null && MissionLobbyComponent.CurrentMultiplayerState != MissionLobbyComponent.MultiplayerGameState.Ending)
            {
                representative.UpdateGold(goldAmount);
            }
        }

        public override int GetGoldAmount()
        {
            return MyRepresentative?.Gold ?? 0;
        }
    }
}
