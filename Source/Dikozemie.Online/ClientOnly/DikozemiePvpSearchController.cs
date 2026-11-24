using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.Diamond;

namespace Dikozemie.Online
{
    internal static class DikozemiePvpSearchController
    {
        private const string ArenaGameType = "DikozemieArena";
        private const string NavalServerName = "Арена — морской бой";
        private const string ArenaModuleId = "DikozemieOnline";
        private const string ArenaModuleVersion = "v0.1.0.0";
        private static readonly TimeSpan RefreshDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan JoinStateDelay =
            TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan JoinStatePollDelay =
            TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan JoinCancellationDelay =
            TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan CustomGameJoinTimeout =
            TimeSpan.FromSeconds(12);
        private static readonly TimeSpan MaxArenaRetryDelay =
            TimeSpan.FromSeconds(2);
        private static readonly object Gate = new object();
        private static readonly Dictionary<Guid, ArenaRetryState>
            ArenaRetryStates = new Dictionary<Guid, ArenaRetryState>();

        private static int _generation;
        private static int _attempt;
        private static bool _isSearching;
        private static bool _isJoinPending;
        private static DateTimeOffset _startedAt;
        private static LobbyClient _activeClient;
        private static SearchKind _searchKind = SearchKind.Arena;
        private static string _status = "Нажмите «Найти бой», чтобы искать игрока";

        private enum SearchKind
        {
            Arena,
            Naval
        }

        internal static bool IsSearching
        {
            get
            {
                lock (Gate)
                {
                    return _isSearching;
                }
            }
        }

        internal static bool IsJoinPending
        {
            get
            {
                lock (Gate)
                {
                    return _isSearching && _isJoinPending;
                }
            }
        }

        internal static string Status
        {
            get
            {
                lock (Gate)
                {
                    return !_isSearching
                        ? _status
                        : _status + " • время ожидания " +
                          FormatElapsed(DateTimeOffset.UtcNow - _startedAt) +
                          " • проверка серверов каждую 1 с";
                }
            }
        }

        internal static string SearchElapsedText
        {
            get
            {
                lock (Gate)
                {
                    return _isSearching
                        ? FormatElapsed(DateTimeOffset.UtcNow - _startedAt)
                        : "00:00";
                }
            }
        }

        internal static bool Start(LobbyClient client) =>
            Start(client, SearchKind.Arena);

        internal static bool StartNaval(LobbyClient client) =>
            Start(client, SearchKind.Naval);

        private static bool Start(LobbyClient client, SearchKind searchKind)
        {
            // The observer closed the Arena to this hero: the battle servers
            // would put him out again at once (plan, «Античит»).
            if (DikozemieEconomy.IsBanned)
            {
                SetStatus(DikozemieEconomy.StatusText);
                InformationManager.DisplayMessage(new InformationMessage(
                    DikozemieEconomy.StatusText,
                    new Color(0.94f, 0.38f, 0.27f, 1f)));
                return false;
            }
            if (!DikozemieHeroProfile.IsCreated)
            {
                SetStatus("Сначала создайте героя");
                DikozemieLobbyShellIntegration.SelectTopTab(4);
                return false;
            }
            // Without War Sails in this game session the sea scene crashes
            // the client while loading (customer, 2026-11-02 and 11-04).
            if (searchKind == SearchKind.Naval &&
                !DikozemieLaunchGuard.IsNavalAvailable)
            {
                SetStatus(DikozemieLaunchGuard.NavalUnavailableText);
                return false;
            }
            if (client == null || !client.Connected || !client.AtLobby)
            {
                SetStatus("Сетевое лобби ещё не готово. Подождите несколько секунд");
                return false;
            }
            // The lobby service brings the whole party to the server its
            // leader joins, so a member searching alone would only split it.
            if (client.IsInParty && !client.IsPartyLeader && !DikozemieWorldBattle.IsPending)
            {
                SetStatus("В бой пати ведёт её лидер. Дождитесь, когда он нажмёт «Войти на арену»");
                return false;
            }

            int generation;
            lock (Gate)
            {
                if (_isSearching)
                {
                    return true;
                }

                _isSearching = true;
                _isJoinPending = false;
                _attempt = 0;
                ArenaRetryStates.Clear();
                _startedAt = DateTimeOffset.UtcNow;
                _activeClient = client;
                _searchKind = searchKind;
                _status = searchKind == SearchKind.Naval
                    ? "Ищем морской dedicated-сервер для " +
                      FormatMode(searchKind) + "…"
                    : "Ищем арену " + FormatMode(searchKind) + "…";
                generation = ++_generation;
            }

            Console.WriteLine(
                "DikozemieOnline: endless " +
                (searchKind == SearchKind.Naval ? "naval" : "arena") +
                " multiplayer search started");
            TraceSearch(
                searchKind,
                "multiplayer-search started mode=" + FormatMode(searchKind));
            // Create the cancel layer before the first asynchronous lobby
            // request can raise TaleWorlds' input-blocking busy curtain.
            DikozemiePvpCancelOverlayController.Show();
            _ = SearchForeverAsync(client, generation, searchKind);
            return true;
        }

        internal static void Cancel()
        {
            LobbyClient client;
            lock (Gate)
            {
                client = _activeClient;
                _generation++;
                _isSearching = false;
                _isJoinPending = false;
                _attempt = 0;
                ArenaRetryStates.Clear();
                _startedAt = default;
                _activeClient = null;
                _searchKind = SearchKind.Arena;
                _status = "Поиск отменён";
            }

            // A cancelled observer's search ends his watch (else every later
            // search would look only for that port, and his hero would go in
            // as an observer). In a mission the observer's view ends it.
            if (DikozemieWatch.IsWatching && TaleWorlds.MountAndBlade.Mission.Current == null)
            {
                DikozemieWatch.StopWatching();
            }
            CancelCustomGameJoin(client);
            Console.WriteLine("DikozemieOnline: PvP search cancelled by player");
        }

        internal static void Reset()
        {
            LobbyClient client;
            lock (Gate)
            {
                client = _activeClient;
                _generation++;
                _isSearching = false;
                _isJoinPending = false;
                _attempt = 0;
                ArenaRetryStates.Clear();
                _startedAt = default;
                _activeClient = null;
                _searchKind = SearchKind.Arena;
                _status = "Нажмите «Найти бой», чтобы искать игрока";
            }

            CancelCustomGameJoin(client);
        }

        internal static void PrepareLobbyRecovery()
        {
            lock (Gate)
            {
                if (!_isSearching)
                {
                    _status = "Восстанавливаем соединение с сетевым лобби…";
                }
            }
        }

        internal static bool IsNavalSearch
        {
            get
            {
                lock (Gate)
                {
                    return _isSearching && _searchKind == SearchKind.Naval;
                }
            }
        }

        internal static void ConfirmLobbyReady()
        {
            lock (Gate)
            {
                if (!_isSearching)
                {
                    _status = "Сетевое лобби готово";
                }
            }
        }

        private static async Task SearchForeverAsync(
            LobbyClient client,
            int generation,
            SearchKind searchKind)
        {
            // An observer's search goes to his battle or nowhere: if his watch
            // ends meanwhile (the observer mission closed before it synced),
            // the search ends too instead of going on as a fighter's.
            bool observing = DikozemieWatch.IsWatching;
            while (IsCurrentSearch(generation))
            {
                if (observing && !DikozemieWatch.IsWatching)
                {
                    Cancel();
                    SetStatus("Наблюдение прервано: бой не открылся. Выберите его снова в «БОИ».");
                    return;
                }
                try
                {
                    if (!client.Connected || !client.AtLobby)
                    {
                        SetStatusIfCurrent(
                            generation,
                            "Восстанавливаем соединение с сетевым лобби…");
                    }
                    else if (DikozemieEconomy.IsBanned)
                    {
                        // A ban that came while searching (or a kick for it):
                        // no endless join, kick, join.
                        Cancel();
                        // After Cancel: its «Поиск отменён» must not hide why.
                        SetStatus(DikozemieEconomy.StatusText);
                        return;
                    }
                    else
                    {
                        int attempt = NextAttempt(generation);
                        AvailableCustomGames games = await client.GetCustomGameServerList();
                        if (!IsCurrentSearch(generation))
                        {
                            return;
                        }

                        TraceServerList(games, searchKind, attempt);
                        GameServerEntry arena = SelectArena(games, searchKind);
                        if (arena == null || arena.Id == CustomBattleId.Empty)
                        {
                            SetStatusIfCurrent(
                                generation,
                                (searchKind == SearchKind.Naval
                                    ? "Ищем морской dedicated-сервер "
                                    : "Ищем арену ") +
                                FormatMode(searchKind) +
                                "… попытка " + attempt +
                                ". Поиск не ограничен по времени");
                        }
                        else
                        {
                            SetStatusIfCurrent(
                                generation,
                                arena.PlayerCount > 0
                                    ? "Сервер найден. Подключаемся к сетевому бою…"
                                    : searchKind == SearchKind.Naval
                                        ? "Морской сервер найден. Входим матросом…"
                                        : "Арена найдена. Входим и ждём второго игрока…");

                            // Keep the freshly rendered Cancel button usable
                            // even when the official lobby returns the arena
                            // immediately. Without this small grace window the
                            // loading state can replace the lobby before a
                            // human can release and click the button again.
                            await Task.Delay(JoinCancellationDelay);
                            if (!IsCurrentSearch(generation))
                            {
                                return;
                            }

                            LastJoinedPort = arena.Port;
                            Task<bool> joinRequest = client.RequestJoinCustomGame(
                                new CustomBattleId(arena.Id.Guid),
                                CustomGameJoinType.Player,
                                string.Empty);
                            Task completedJoin = await Task.WhenAny(
                                joinRequest,
                                Task.Delay(CustomGameJoinTimeout));
                            if (completedJoin != joinRequest)
                            {
                                TraceSearch(
                                    searchKind,
                                    "join-reply timeout id=" + arena.Id.Guid);
                                // After a server restart the list still names
                                // the old instance for about a minute: without
                                // this the live one waited through three
                                // timeouts, 45 s (2026-10-31).
                                lock (Gate)
                                {
                                    RegisterArenaRetry(arena.Id.Guid);
                                    ArenaRetryStates[arena.Id.Guid].RetryAfter =
                                        DateTimeOffset.UtcNow.AddSeconds(30);
                                }
                                CancelCustomGameJoin(client);
                                SetStatusIfCurrent(
                                    generation,
                                    "Сервер не ответил на вход. Повторяем поиск…");
                                await Task.Delay(RefreshDelay);
                                continue;
                            }
                            bool joined = await joinRequest;
                            TraceSearch(
                                searchKind,
                                "join-reply accepted=" + joined +
                                " id=" + arena.Id.Guid);
                            if (!IsCurrentSearch(generation))
                            {
                                // Cancel() can run while TaleWorlds is waiting
                                // for the custom-game reply. If the reply won
                                // the race, explicitly leave the reservation
                                // so the cancelled search cannot still push a
                                // loading state a moment later.
                                CancelCustomGameJoin(client);
                                return;
                            }

                            if (joined)
                            {
                                SetJoinPendingIfCurrent(generation, true);
                                SetStatusIfCurrent(
                                    generation,
                                    "Сервер ответил. Загружаем бой…");

                                // A successful lobby response does not prove
                                // that the UDP mission behind that Match Id is
                                // still alive. Keep the search generation open
                                // until the actual mission behavior confirms
                                // initialization. If Bannerlord returns to the
                                // lobby first, reject this stale registration
                                // and try another candidate automatically.
                                await Task.Delay(JoinStateDelay);
                                if (await WaitForMissionOrLobbyReturn(
                                    client,
                                    generation,
                                    arena.Id))
                                {
                                    return;
                                }

                                SetJoinPendingIfCurrent(generation, false);
                                continue;
                            }

                            SetStatusIfCurrent(
                                generation,
                                "Сервер был занят. Продолжаем искать игрока…");
                        }
                    }
                }
                catch (Exception error)
                {
                    Console.WriteLine(
                        "DikozemieOnline: endless PvP search attempt failed: " + error);
                    TraceSearch(
                        searchKind,
                        "search-error type=" + error.GetType().Name +
                        " message=" + error.Message);
                    SetStatusIfCurrent(
                        generation,
                        "Сеть временно не ответила. Поиск продолжается…");
                }

                if (IsCurrentSearch(generation))
                {
                    await Task.Delay(RefreshDelay);
                }
            }
        }

        // The UDP port of the last server this client asked to join: the
        // shared world (stage 5) tells the other side of a meeting where to go.
        internal static int LastJoinedPort { get; private set; }

        private static int? _preferredTestPort;

        private static int PreferredTestPort
        {
            get
            {
                if (!_preferredTestPort.HasValue)
                {
                    _preferredTestPort = int.TryParse(
                        Environment.GetEnvironmentVariable(
                            "DIKOZEMIE_ARENA_PREFER_PORT"),
                        out int port) && port > 0 && port < 65536
                        ? port
                        : 0;
                }
                return _preferredTestPort.Value;
            }
        }

        // A fighter's places on an arena server: one place over them is kept
        // for an observer (MissionMultiplayerDikozemieArena.FighterSeats, the
        // same rule on the server). A config of two (from before) keeps both.
        internal static int FighterSeats(GameServerEntry server) =>
            server == null ? 0 :
            server.MaxPlayerCount <= 2 ? server.MaxPlayerCount : server.MaxPlayerCount - 1;

        internal static bool HasFighterSeat(GameServerEntry server) =>
            server != null && server.PlayerCount < FighterSeats(server);

        private static GameServerEntry SelectArena(
            AvailableCustomGames games,
            SearchKind searchKind)
        {
            GameServerEntry best = null;
            int bestFormatRating = int.MinValue;
            bool reservedFormatListed = false;
            if (games?.CustomGameServerInfos == null)
            {
                return null;
            }

            foreach (GameServerEntry server in games.CustomGameServerInfos)
            {
                // The observer of the watch goes to the battle he chose and
                // nowhere else: until it is listed, the search goes on.
                if (DikozemieWatch.IsWatching)
                {
                    if (server != null && server.Port == DikozemieWatch.WatchPort &&
                        !server.PasswordProtected &&
                        string.Equals(server.GameType, ArenaGameType, StringComparison.OrdinalIgnoreCase) &&
                        server.PlayerCount < server.MaxPlayerCount &&
                        HasCompatibleArenaModule(server))
                    {
                        return server;
                    }
                    continue;
                }
                // Developer hook (DIKOZEMIE_ARENA_PREFER_PORT): the test
                // server on that port is taken whatever its name. A test
                // build runs under a name players' clients never pick
                // («Тест — морской бой»), so nobody lands on it by chance.
                if (IsPreferredTestServer(server, searchKind))
                {
                    return server;
                }
                // Stage 5: a battle of the shared world goes where the other
                // side of it already stands.
                if (server != null && DikozemieWorldBattle.PreferredPort > 0 &&
                    server.Port == DikozemieWorldBattle.PreferredPort &&
                    !server.PasswordProtected &&
                    string.Equals(server.GameType, ArenaGameType, StringComparison.OrdinalIgnoreCase) &&
                    HasFighterSeat(server) &&
                    HasCompatibleArenaModule(server))
                {
                    return server;
                }
                if (server == null || server.PasswordProtected ||
                    !string.Equals(
                        server.GameType,
                        ArenaGameType,
                        StringComparison.OrdinalIgnoreCase) ||
                    (searchKind == SearchKind.Naval &&
                     !IsNavalServer(server)) ||
                    (searchKind == SearchKind.Arena &&
                     (IsNavalServer(server) ||
                      !DikozemieArenaSceneRules.IsServerNameForTeamSize(
                          server.ServerName,
                          DikozemieArenaMatchSelection.TeamSize,
                          DikozemieArenaMatchSelection.Discipline))) ||
                    !HasCompatibleArenaModule(server))
                {
                    continue;
                }
                if (searchKind == SearchKind.Arena &&
                    DikozemieArenaSceneRules.RateServerForFormat(
                        server.ServerName,
                        DikozemieArenaMatchSelection.TeamSize) > 0)
                {
                    reservedFormatListed = true;
                }
                if (!HasFighterSeat(server))
                {
                    continue;
                }

                Console.WriteLine(
                    "DikozemieOnline: " +
                    (searchKind == SearchKind.Naval ? "naval" : "arena") +
                    " candidate id=" + server.Id.Guid +
                    " players=" + server.PlayerCount +
                    " ping=" + server.Ping +
                    " host=" + server.HostName +
                    " address=" + server.Address + ":" + server.Port);
                TraceSearch(
                    searchKind,
                    "candidate-selected id=" + server.Id.Guid +
                    " name=" + server.ServerName +
                    " players=" + server.PlayerCount + "/" +
                    server.MaxPlayerCount +
                    " address=" + server.Address + ":" + server.Port);

                if (IsArenaCoolingDown(server.Id.Guid))
                {
                    continue;
                }

                // Test hook for comparing server builds side by side: a
                // developer can point the client at one UDP port. Unset for
                // players, so it changes nothing for them.
                if (PreferredTestPort > 0 && server.Port == PreferredTestPort)
                {
                    return server;
                }

                // Prefer a server reserved for the chosen format (duels or
                // team battles), then the arena where somebody is already
                // waiting. For an equal population prefer the later lobby
                // entry: Bannerlord's lobby can briefly retain an old
                // registration after a server restart, while the fresh
                // registration is appended to the returned list.
                int formatRating = searchKind == SearchKind.Arena
                    ? DikozemieArenaSceneRules.RateServerForFormat(
                        server.ServerName,
                        DikozemieArenaMatchSelection.TeamSize)
                    : 0;
                if (best == null || formatRating > bestFormatRating ||
                    (formatRating == bestFormatRating &&
                     server.PlayerCount >= best.PlayerCount))
                {
                    best = server;
                    bestFormatRating = formatRating;
                }
            }
            // While arenas reserved for this format exist, wait for a free one
            // instead of borrowing the other format's arena: a duellist's bot
            // fight on the team arena would hold every team player waiting.
            return reservedFormatListed && bestFormatRating <= 0 ? null : best;
        }

        internal static void ConfirmMissionStarted()
        {
            lock (Gate)
            {
                if (!_isSearching)
                {
                    return;
                }

                _generation++;
                _isSearching = false;
                _isJoinPending = false;
                _attempt = 0;
                ArenaRetryStates.Clear();
                _startedAt = default;
                _activeClient = null;
                _status = _searchKind == SearchKind.Naval
                    ? "Морской сетевой бой загружен"
                    : "Бой загружен";
                _searchKind = SearchKind.Arena;
            }

            Console.WriteLine(
                "DikozemieOnline: arena mission confirmed after network synchronization");
            DikozemieLobbyShellIntegration.TraceButtonAction(
                "multiplayer mission confirmed after network synchronization");
        }

        private static void TraceServerList(
            AvailableCustomGames games,
            SearchKind searchKind,
            int attempt)
        {
            if (searchKind != SearchKind.Naval ||
                (attempt > 10 && attempt % 30 != 0))
            {
                return;
            }

            int total = 0;
            int naval = 0;
            if (games?.CustomGameServerInfos != null)
            {
                foreach (GameServerEntry server in games.CustomGameServerInfos)
                {
                    total++;
                    if (server == null || !string.Equals(
                            server.GameType,
                            ArenaGameType,
                            StringComparison.OrdinalIgnoreCase) ||
                        !IsNavalServer(server))
                    {
                        continue;
                    }

                    naval++;
                    DikozemieLobbyShellIntegration.TraceButtonAction(
                        "naval-server observed attempt=" + attempt +
                        " id=" + server.Id.Guid +
                        " name=" + server.ServerName +
                        " compatible=" + HasCompatibleArenaModule(server) +
                        " password=" + server.PasswordProtected +
                        " players=" + server.PlayerCount + "/" +
                        server.MaxPlayerCount +
                        " address=" + server.Address + ":" + server.Port);
                }
            }

            DikozemieLobbyShellIntegration.TraceButtonAction(
                "naval-server-list attempt=" + attempt +
                " total=" + total + " naval=" + naval);
        }

        private static void TraceSearch(SearchKind searchKind, string message)
        {
            if (searchKind == SearchKind.Naval)
            {
                DikozemieLobbyShellIntegration.TraceButtonAction(message);
            }
        }

        private static bool IsPreferredTestServer(
            GameServerEntry server,
            SearchKind searchKind)
        {
            if (server == null || PreferredTestPort <= 0 ||
                server.Port != PreferredTestPort || server.PasswordProtected ||
                (DikozemieWatch.IsWatching
                    ? server.PlayerCount >= server.MaxPlayerCount
                    : !HasFighterSeat(server)) ||
                !string.Equals(
                    server.GameType,
                    ArenaGameType,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            bool naval = (server.ServerName ?? string.Empty).IndexOf(
                "морской",
                StringComparison.OrdinalIgnoreCase) >= 0;
            return naval == (searchKind == SearchKind.Naval);
        }

        private static bool IsNavalServer(GameServerEntry server)
        {
            return server != null && string.Equals(
                server.ServerName,
                NavalServerName,
                StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<bool> WaitForMissionOrLobbyReturn(
            LobbyClient client,
            int generation,
            CustomBattleId arenaId)
        {
            while (IsCurrentSearch(generation))
            {
                if (client.Connected && client.AtLobby)
                {
                    lock (Gate)
                    {
                        if (_isSearching && generation == _generation)
                        {
                            TimeSpan retryDelay = RegisterArenaRetry(arenaId.Guid);
                            _status =
                                "Handshake с ареной не завершился. Повторяем через " +
                                Math.Ceiling(retryDelay.TotalSeconds) + " с…";
                        }
                    }

                    Console.WriteLine(
                        "DikozemieOnline: arena synchronization failed; retry scheduled id=" +
                        arenaId.Guid);
                    return false;
                }

                await Task.Delay(JoinStatePollDelay);
            }

            return true;
        }

        private static bool IsArenaCoolingDown(Guid arenaId)
        {
            lock (Gate)
            {
                if (!ArenaRetryStates.TryGetValue(
                    arenaId,
                    out ArenaRetryState state) ||
                    state.RetryAfter <= DateTimeOffset.UtcNow)
                {
                    return false;
                }

                Console.WriteLine(
                    "DikozemieOnline: arena retry cooling down id=" + arenaId +
                    " until=" + state.RetryAfter.ToString("O"));
                return true;
            }
        }

        private static TimeSpan RegisterArenaRetry(Guid arenaId)
        {
            if (!ArenaRetryStates.TryGetValue(
                arenaId,
                out ArenaRetryState state))
            {
                state = new ArenaRetryState();
                ArenaRetryStates.Add(arenaId, state);
            }

            state.FailureCount++;
            int exponent = Math.Min(5, state.FailureCount);
            int delaySeconds = Math.Min(
                (int)MaxArenaRetryDelay.TotalSeconds,
                1 << exponent);
            TimeSpan delay = TimeSpan.FromSeconds(delaySeconds);
            state.RetryAfter = DateTimeOffset.UtcNow + delay;
            return delay;
        }

        private sealed class ArenaRetryState
        {
            internal int FailureCount { get; set; }

            internal DateTimeOffset RetryAfter { get; set; }
        }

        private static void CancelCustomGameJoin(LobbyClient client)
        {
            if (client == null || !client.Connected)
            {
                return;
            }

            try
            {
                Console.WriteLine(
                    "DikozemieOnline: cancelling custom-game join state=" +
                    client.CurrentState);
                // During RequestJoinCustomGame the state can still report the
                // lobby even though the reservation is already in flight.
                // QuitFromCustomGame is idempotent and must be sent for that
                // window too; restricting it to two visible enum states let a
                // cancelled request finish and open the mission.
                client.QuitFromCustomGame();
            }
            catch (Exception error)
            {
                Console.WriteLine(
                    "DikozemieOnline: custom-game cancellation failed: " + error);
            }
        }

        private static bool HasCompatibleArenaModule(GameServerEntry server)
        {
            if (server?.LoadedModules == null)
            {
                return false;
            }

            foreach (ModuleInfoModel module in server.LoadedModules)
            {
                if (module != null &&
                    string.Equals(
                        module.Id,
                        ArenaModuleId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return string.Equals(
                        module.Version.ToString(),
                        ArenaModuleVersion,
                        StringComparison.OrdinalIgnoreCase);
                }
            }

            return false;
        }

        private static string FormatMode(SearchKind searchKind)
        {
            if (searchKind == SearchKind.Naval)
            {
                DikozemieNavalBattleDefinition battle =
                    DikozemieArenaMatchSelection.SelectedNavalBattle;
                return battle.DisplayName + " / " +
                    battle.CrewCapacity + "×" + battle.CrewCapacity;
            }

            return DikozemieArenaMatchSelection.FormatTeamSize() + " / " +
                DikozemieArenaMatchSelection.FormatDiscipline();
        }

        private static bool IsCurrentSearch(int generation)
        {
            lock (Gate)
            {
                return _isSearching && generation == _generation;
            }
        }

        private static int NextAttempt(int generation)
        {
            lock (Gate)
            {
                if (!_isSearching || generation != _generation)
                {
                    return _attempt;
                }
                return ++_attempt;
            }
        }

        private static void SetStatus(string status)
        {
            lock (Gate)
            {
                _status = status;
            }
        }

        private static void SetStatusIfCurrent(int generation, string status)
        {
            lock (Gate)
            {
                if (_isSearching && generation == _generation)
                {
                    _status = status;
                }
            }
        }

        private static void SetJoinPendingIfCurrent(int generation, bool value)
        {
            lock (Gate)
            {
                if (_isSearching && generation == _generation)
                {
                    _isJoinPending = value;
                }
            }
        }

        private static void Complete(int generation, string status)
        {
            lock (Gate)
            {
                if (generation != _generation)
                {
                    return;
                }
                _isSearching = false;
                _isJoinPending = false;
                _startedAt = default;
                _activeClient = null;
                _status = status;
            }
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            int hours = Math.Min(99, Math.Max(0, (int)elapsed.TotalHours));
            return hours > 0
                ? hours.ToString("00") + ":" + elapsed.Minutes.ToString("00") +
                  ":" + elapsed.Seconds.ToString("00")
                : elapsed.Minutes.ToString("00") + ":" +
                  elapsed.Seconds.ToString("00");
        }
    }
}
