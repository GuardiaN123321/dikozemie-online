using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using TaleWorlds.ModuleManager;

namespace Dikozemie.Online
{
    // Terrain painted on the atlas, built by Tools/build_travel_terrain.py: one
    // byte per cell, read from the picture the player sees, because the game's
    // own navmesh does not match the hand-drawn map.
    internal static class DikozemieTravelTerrain
    {
        internal const byte Water = 1;
        internal const byte Road = 2;
        internal const byte Forest = 4;
        internal const byte Mountain = 8;
        internal const byte Port = 16;
        // 0.5.133: a shallow place of a river (Tools/build_world_terrain.py).
        // Walkers wade across, slowly; boats sail over it as over any water.
        internal const byte Ford = 32;

        private static bool _loaded;
        private static byte[] _cells = Array.Empty<byte>();

        internal static int Width { get; private set; }
        internal static int Height { get; private set; }
        internal static int CellSize { get; private set; } = 8;
        internal static bool IsAvailable
        {
            get
            {
                EnsureLoaded();
                return _cells.Length > 0;
            }
        }

        internal static byte AtCell(int x, int y)
        {
            // Beyond the grid is open sea.
            return x >= 0 && y >= 0 && x < Width && y < Height
                ? _cells[y * Width + x]
                : Water;
        }

        internal static byte AtAtlas(float x, float y)
        {
            EnsureLoaded();
            return AtCell((int)(x / CellSize), (int)(y / CellSize));
        }

        internal static bool IsWalkable(byte cell, bool boat)
        {
            if (boat)
            {
                return (cell & (Water | Port)) != 0;
            }
            // A road crosses rivers by bridges and mountains by passes; a ford
            // lets a walker through the river.
            if ((cell & (Road | Port | Ford)) != 0)
            {
                return true;
            }
            return (cell & (Water | Mountain)) == 0;
        }

        private static int[] _waterBody;
        private static int[] _bridges;

        // A bridge is a run of road cells over water; 0 = not on a bridge.
        internal static int BridgeAt(float x, float y)
        {
            EnsureLoaded();
            if (_cells.Length == 0)
            {
                return 0;
            }
            int cx = (int)(x / CellSize);
            int cy = (int)(y / CellSize);
            if (cx < 0 || cy < 0 || cx >= Width || cy >= Height)
            {
                return 0;
            }
            if (_bridges == null)
            {
                LabelBridges();
            }
            return _bridges[cy * Width + cx];
        }

        private static void LabelBridges()
        {
            var labels = new int[_cells.Length];
            var stack = new Stack<int>();
            int next = 0;
            for (int start = 0; start < _cells.Length; start++)
            {
                if (labels[start] != 0 || (_cells[start] & (Road | Water)) != (Road | Water))
                {
                    continue;
                }
                next++;
                labels[start] = next;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    int cell = stack.Pop();
                    int x = cell % Width;
                    int y = cell / Width;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= Width || ny >= Height)
                            {
                                continue;
                            }
                            int index = ny * Width + nx;
                            if (labels[index] == 0 && (_cells[index] & (Road | Water)) == (Road | Water))
                            {
                                labels[index] = next;
                                stack.Push(index);
                            }
                        }
                    }
                }
            }
            _bridges = labels;
        }

        // Land and sea within a few cells, for the kind of bandits: hillmen
        // near mountains, sea raiders on the coast.
        internal static bool Near(float x, float y, byte flag, int radiusCells)
        {
            EnsureLoaded();
            int cx = (int)(x / CellSize);
            int cy = (int)(y / CellSize);
            for (int dy = -radiusCells; dy <= radiusCells; dy += 2)
            {
                for (int dx = -radiusCells; dx <= radiusCells; dx += 2)
                {
                    if ((AtCell(cx + dx, cy + dy) & flag) != 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // Which body of water a place lies on: ships sail only within one
        // (an inland lake is not connected to the sea). 0 = no water near.
        internal static int WaterBodyNear(float x, float y)
        {
            EnsureLoaded();
            if (_cells.Length == 0 ||
                !DikozemieTravelRouter.TrySnap(x, y, true, 80, out int cx, out int cy))
            {
                return 0;
            }
            if (_waterBody == null)
            {
                LabelWater();
            }
            return _waterBody[cy * Width + cx];
        }

        private static void LabelWater()
        {
            var labels = new int[_cells.Length];
            var queue = new int[_cells.Length];
            int next = 0;
            for (int start = 0; start < _cells.Length; start++)
            {
                if (labels[start] != 0 || !IsWalkable(_cells[start], true))
                {
                    continue;
                }
                next++;
                int head = 0;
                int tail = 0;
                queue[tail++] = start;
                labels[start] = next;
                while (head < tail)
                {
                    int cell = queue[head++];
                    int x = cell % Width;
                    int y = cell / Width;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= Width || ny >= Height)
                            {
                                continue;
                            }
                            int index = ny * Width + nx;
                            // As in the router: no slipping between two
                            // blocked cells on a diagonal.
                            if (dx != 0 && dy != 0 &&
                                !IsWalkable(_cells[y * Width + nx], true) &&
                                !IsWalkable(_cells[ny * Width + x], true))
                            {
                                continue;
                            }
                            if (labels[index] == 0 && IsWalkable(_cells[index], true))
                            {
                                labels[index] = next;
                                queue[tail++] = index;
                            }
                        }
                    }
                }
            }
            _waterBody = labels;
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }
            _loaded = true;
            try
            {
                string path = Path.Combine(
                    ModuleHelper.GetModuleFullPath(DikozemieSubModule.ModuleId),
                    "ModuleData",
                    "dikozemie_travel_terrain.bin");
                byte[] file = File.ReadAllBytes(path);
                if (file.Length < 18 || file[0] != 'D' || file[1] != 'T' || file[2] != 'T')
                {
                    return;
                }
                int width = BitConverter.ToInt32(file, 4);
                int height = BitConverter.ToInt32(file, 8);
                int cellSize = BitConverter.ToInt32(file, 12);
                var cells = new byte[width * height];
                // zlib: two header bytes, then a raw deflate stream.
                using (var memory = new MemoryStream(file, 18, file.Length - 18))
                using (var deflate = new DeflateStream(memory, CompressionMode.Decompress))
                {
                    int offset = 0;
                    while (offset < cells.Length)
                    {
                        int read = deflate.Read(cells, offset, cells.Length - offset);
                        if (read <= 0)
                        {
                            break;
                        }
                        offset += read;
                    }
                    if (offset != cells.Length)
                    {
                        return;
                    }
                }
                Width = width;
                Height = height;
                CellSize = cellSize;
                _cells = cells;
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: travel terrain failed: " + error.Message);
            }
        }
    }

    internal sealed class DikozemieTravelRoute
    {
        internal readonly List<float> Xs = new List<float>();
        internal readonly List<float> Ys = new List<float>();
        internal float Length;
        internal float Seconds;
        internal float RoadShare;
        internal bool Boat;
        // The goal itself cannot be reached; the route ends at the closest place.
        internal bool Snapped;
    }

    internal static class DikozemieTravelRouter
    {
        // Atlas pixels per second. «По дороге X3 от обычной скорости».
        internal const float PlainSpeed = 28f;
        internal const float RoadSpeed = 84f;
        internal const float ForestSpeed = 18f;
        // Wading a ford: slower than a forest.
        internal const float FordSpeed = 10f;
        // On water it is the hero's vessel: a small boat, a longship or
        // a passenger's seat (DikozemieTravelState.VesselSpeed).
        internal static float BoatSpeed => DikozemieTravelState.VesselSpeed;

        private static float[] _cost;
        private static int[] _parent;
        private static int[] _stamp;
        private static int _generation;
        private static int[] _heapCell = new int[4096];
        private static float[] _heapKey = new float[4096];
        private static int _heapCount;

        internal static float SpeedOf(byte cell, bool boat)
        {
            if (boat)
            {
                return BoatSpeed;
            }
            if ((cell & DikozemieTravelTerrain.Road) != 0)
            {
                return RoadSpeed;
            }
            if ((cell & DikozemieTravelTerrain.Ford) != 0)
            {
                return FordSpeed;
            }
            return (cell & DikozemieTravelTerrain.Forest) != 0 ? ForestSpeed : PlainSpeed;
        }

        internal static bool TrySnap(float x, float y, bool boat, int maxCells, out int cx, out int cy)
        {
            int size = DikozemieTravelTerrain.CellSize;
            int ox = (int)(x / size);
            int oy = (int)(y / size);
            for (int radius = 0; radius <= maxCells; radius++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                        {
                            continue;
                        }
                        int nx = ox + dx;
                        int ny = oy + dy;
                        if (nx >= 0 && ny >= 0 &&
                            nx < DikozemieTravelTerrain.Width &&
                            ny < DikozemieTravelTerrain.Height &&
                            DikozemieTravelTerrain.IsWalkable(
                                DikozemieTravelTerrain.AtCell(nx, ny), boat))
                        {
                            cx = nx;
                            cy = ny;
                            return true;
                        }
                    }
                }
            }
            cx = ox;
            cy = oy;
            return false;
        }

        internal static DikozemieTravelRoute Find(
            float fromX, float fromY, float toX, float toY, bool boat)
        {
            if (!DikozemieTravelTerrain.IsAvailable ||
                !TrySnap(fromX, fromY, boat, 80, out int startX, out int startY) ||
                !TrySnap(toX, toY, boat, 120, out int goalX, out int goalY))
            {
                return null;
            }
            int width = DikozemieTravelTerrain.Width;
            int height = DikozemieTravelTerrain.Height;
            int total = width * height;
            if (_cost == null || _cost.Length != total)
            {
                _cost = new float[total];
                _parent = new int[total];
                _stamp = new int[total];
            }
            _generation++;
            _heapCount = 0;
            int start = startY * width + startX;
            int goal = goalY * width + goalX;
            float fastest = boat ? BoatSpeed : RoadSpeed;
            _cost[start] = 0f;
            _parent[start] = -1;
            _stamp[start] = _generation;
            Push(start, 0f);
            bool found = false;
            while (_heapCount > 0)
            {
                int current = Pop(out float key);
                if (current == goal)
                {
                    found = true;
                    break;
                }
                int cx = current % width;
                int cy = current / width;
                float reached = _cost[current];
                float heuristic = Distance(cx, cy, goalX, goalY) / fastest;
                if (key > reached + heuristic + 0.0001f)
                {
                    continue;
                }
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0)
                        {
                            continue;
                        }
                        int nx = cx + dx;
                        int ny = cy + dy;
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                        {
                            continue;
                        }
                        byte cell = DikozemieTravelTerrain.AtCell(nx, ny);
                        if (!DikozemieTravelTerrain.IsWalkable(cell, boat))
                        {
                            continue;
                        }
                        // A diagonal may not slip between two blocked cells.
                        if (dx != 0 && dy != 0 &&
                            !DikozemieTravelTerrain.IsWalkable(
                                DikozemieTravelTerrain.AtCell(cx + dx, cy), boat) &&
                            !DikozemieTravelTerrain.IsWalkable(
                                DikozemieTravelTerrain.AtCell(cx, cy + dy), boat))
                        {
                            continue;
                        }
                        float step = (dx != 0 && dy != 0 ? 1.4142f : 1f) / SpeedOf(cell, boat);
                        float next = reached + step;
                        int index = ny * width + nx;
                        if (_stamp[index] == _generation && next >= _cost[index])
                        {
                            continue;
                        }
                        _stamp[index] = _generation;
                        _cost[index] = next;
                        _parent[index] = current;
                        Push(index, next + Distance(nx, ny, goalX, goalY) / fastest);
                    }
                }
            }
            if (!found)
            {
                return null;
            }

            var cells = new List<int>();
            for (int index = goal; index >= 0; index = _parent[index])
            {
                cells.Add(index);
            }
            cells.Reverse();
            return BuildRoute(cells, fromX, fromY, toX, toY, goalX, goalY, boat);
        }

        private static DikozemieTravelRoute BuildRoute(
            List<int> cells, float fromX, float fromY, float toX, float toY,
            int goalX, int goalY, bool boat)
        {
            int width = DikozemieTravelTerrain.Width;
            float size = DikozemieTravelTerrain.CellSize;
            var route = new DikozemieTravelRoute { Boat = boat };
            int ox = (int)(toX / size);
            int oy = (int)(toY / size);
            route.Snapped = Math.Max(Math.Abs(ox - goalX), Math.Abs(oy - goalY)) > 2;
            var xs = new float[cells.Count];
            var ys = new float[cells.Count];
            for (int i = 0; i < cells.Count; i++)
            {
                xs[i] = (cells[i] % width + 0.5f) * size;
                ys[i] = (cells[i] / width + 0.5f) * size;
            }
            // The grid path has steps; the figure should not.
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 1; i < cells.Count - 1; i++)
                {
                    xs[i] = (xs[i - 1] + xs[i] * 2f + xs[i + 1]) * 0.25f;
                    ys[i] = (ys[i - 1] + ys[i] * 2f + ys[i + 1]) * 0.25f;
                }
            }
            route.Xs.Add(fromX);
            route.Ys.Add(fromY);
            for (int i = 1; i < cells.Count - 1; i += 2)
            {
                route.Xs.Add(xs[i]);
                route.Ys.Add(ys[i]);
            }
            float endX = route.Snapped ? (goalX + 0.5f) * size : toX;
            float endY = route.Snapped ? (goalY + 0.5f) * size : toY;
            route.Xs.Add(endX);
            route.Ys.Add(endY);

            float roadLength = 0f;
            for (int i = 1; i < route.Xs.Count; i++)
            {
                float dx = route.Xs[i] - route.Xs[i - 1];
                float dy = route.Ys[i] - route.Ys[i - 1];
                float length = (float)Math.Sqrt(dx * dx + dy * dy);
                byte cell = DikozemieTravelTerrain.AtAtlas(route.Xs[i], route.Ys[i]);
                route.Length += length;
                route.Seconds += length / SpeedOf(cell, boat);
                if (!boat && (cell & DikozemieTravelTerrain.Road) != 0)
                {
                    roadLength += length;
                }
            }
            route.RoadShare = route.Length > 0f ? roadLength / route.Length : 0f;
            return route;
        }

        private static float Distance(int ax, int ay, int bx, int by)
        {
            float dx = ax - bx;
            float dy = ay - by;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static void Push(int cell, float key)
        {
            if (_heapCount == _heapCell.Length)
            {
                Array.Resize(ref _heapCell, _heapCount * 2);
                Array.Resize(ref _heapKey, _heapCount * 2);
            }
            int i = _heapCount++;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (_heapKey[parent] <= key)
                {
                    break;
                }
                _heapCell[i] = _heapCell[parent];
                _heapKey[i] = _heapKey[parent];
                i = parent;
            }
            _heapCell[i] = cell;
            _heapKey[i] = key;
        }

        private static int Pop(out float key)
        {
            int top = _heapCell[0];
            key = _heapKey[0];
            _heapCount--;
            int moved = _heapCell[_heapCount];
            float movedKey = _heapKey[_heapCount];
            int i = 0;
            while (true)
            {
                int left = i * 2 + 1;
                if (left >= _heapCount)
                {
                    break;
                }
                int child = left + 1 < _heapCount && _heapKey[left + 1] < _heapKey[left]
                    ? left + 1
                    : left;
                if (_heapKey[child] >= movedKey)
                {
                    break;
                }
                _heapCell[i] = _heapCell[child];
                _heapKey[i] = _heapKey[child];
                i = child;
            }
            _heapCell[i] = moved;
            _heapKey[i] = movedKey;
            return top;
        }
    }

    [DataContract]
    internal sealed class DikozemieTravelSave
    {
        [DataMember(Name = "heroToken")] public string HeroToken = string.Empty;
        [DataMember(Name = "x")] public float X;
        [DataMember(Name = "y")] public float Y;
        [DataMember(Name = "boat")] public bool Boat;
        [DataMember(Name = "nodeId")] public string NodeId = string.Empty;
        [DataMember(Name = "hasTarget")] public bool HasTarget;
        [DataMember(Name = "targetX")] public float TargetX;
        [DataMember(Name = "targetY")] public float TargetY;
        [DataMember(Name = "targetNodeId")] public string TargetNodeId = string.Empty;
        [DataMember(Name = "roadSinceEvent")] public float RoadSinceEvent;
        [DataMember(Name = "wildSinceEvent")] public float WildSinceEvent;
        // Customer 2026-11-05: no boat for free, «лодку в порте можешь
        // купить». A deleted hero loses it with the rest of his things.
        // Kept for saves of 0.5.124: such a boat was the 240-coin one.
        [DataMember(Name = "ownsBoat")] public bool OwnsBoat;
        // What the hero sails his own: 0 nothing, 1 a small boat, 2 a longship
        // (DikozemieTravelState.SmallBoat / Ship).
        [DataMember(Name = "vessel")] public int Vessel;
        // Set only by a purchase in a port (0.5.133). A vessel without it was
        // never paid for: see DikozemieTravelState.Load.
        [DataMember(Name = "vesselPaid")] public bool VesselPaid;
        // Sailing as a paying passenger to this port; the ship is not his.
        [DataMember(Name = "passengerTo")] public string PassengerTo = string.Empty;
        // Castles whose guards were paid: they let the hero in from then on.
        [DataMember(Name = "castlePasses")] public List<string> CastlePasses = new List<string>();
        // The last day the horses were fed (yyyy-MM-dd, local time).
        [DataMember(Name = "fodderDay")] public string FodderDay = string.Empty;
        // No coins for fodder that day: the horse does not carry the hero.
        [DataMember(Name = "horseHungry")] public bool HorseHungry;

        // ---- Stage 2, roads (DikozemieRoads) ----
        // Game hours since the hero set out; time runs while he travels.
        [DataMember(Name = "hours")] public float Hours;
        // 0 healthy, 1 bruised, 2 badly wounded; hours until one level heals.
        [DataMember(Name = "wound")] public int Wound;
        [DataMember(Name = "woundHours")] public float WoundHours;
        // What happened while the player looked elsewhere.
        [DataMember(Name = "roadNote")] public string RoadNote = string.Empty;
        // Sold by man-catchers to the arena of this town.
        [DataMember(Name = "captiveTown")] public string CaptiveTown = string.Empty;
        // Stage 5: who sold the hero to the arena (empty: the man-catchers).
        [DataMember(Name = "captiveBy")] public string CaptiveBy = string.Empty;
        [DataMember(Name = "captiveFight")] public bool CaptiveFight;
        [DataMember(Name = "escapeDay")] public int EscapeDay;
        // A wanted man taken from a town's captain of the guard.
        [DataMember(Name = "bountyTown")] public string BountyTown = string.Empty;
        [DataMember(Name = "bountyName")] public string BountyName = string.Empty;
        [DataMember(Name = "bountyRole")] public string BountyRole = string.Empty;
        [DataMember(Name = "bountyGangRole")] public string BountyGangRole = string.Empty;
        [DataMember(Name = "bountyCulture")] public string BountyCulture = string.Empty;
        [DataMember(Name = "bountyGang")] public int BountyGang;
        [DataMember(Name = "bountyReward")] public int BountyReward;
        [DataMember(Name = "bountySeed")] public int BountySeed;
        [DataMember(Name = "bountyX")] public float BountyX;
        [DataMember(Name = "bountyY")] public float BountyY;
        // A bound bandit walking behind the hero to an arena.
        [DataMember(Name = "prisonerName")] public string PrisonerName = string.Empty;
        [DataMember(Name = "prisonerReward")] public int PrisonerReward;
        [DataMember(Name = "prisonerTown")] public string PrisonerTown = string.Empty;
        // Pilgrims on their way or a hired caravan guard.
        [DataMember(Name = "fellowRole")] public string FellowRole = string.Empty;
        [DataMember(Name = "fellowName")] public string FellowName = string.Empty;
        [DataMember(Name = "fellowTo")] public string FellowTo = string.Empty;
        [DataMember(Name = "fellowCulture")] public string FellowCulture = string.Empty;
        [DataMember(Name = "fellowSeed")] public int FellowSeed;
        [DataMember(Name = "fellowCount")] public int FellowCount;
        [DataMember(Name = "fellowUntil")] public float FellowUntil;
        [DataMember(Name = "fellowPay")] public int FellowPay;
        // Tolls: the town gate paid today, the bridge crossed last.
        [DataMember(Name = "lastGate")] public string LastGate = string.Empty;
        [DataMember(Name = "lastBridge")] public int LastBridge;

        internal bool IsPassenger => !string.IsNullOrEmpty(PassengerTo);

        // The serializer does not run field initialisers: a save of an
        // older version leaves the new strings null.
        internal void FillDefaults()
        {
            RoadNote = RoadNote ?? string.Empty;
            CaptiveTown = CaptiveTown ?? string.Empty;
            CaptiveBy = CaptiveBy ?? string.Empty;
            BountyTown = BountyTown ?? string.Empty;
            BountyName = BountyName ?? string.Empty;
            BountyRole = BountyRole ?? string.Empty;
            BountyGangRole = BountyGangRole ?? string.Empty;
            BountyCulture = BountyCulture ?? string.Empty;
            PrisonerName = PrisonerName ?? string.Empty;
            PrisonerTown = PrisonerTown ?? string.Empty;
            FellowRole = FellowRole ?? string.Empty;
            FellowName = FellowName ?? string.Empty;
            FellowTo = FellowTo ?? string.Empty;
            FellowCulture = FellowCulture ?? string.Empty;
            LastGate = LastGate ?? string.Empty;
            if (Hours <= 0f)
            {
                Hours = DikozemieRoads.StartHour;
            }
            Wound = Math.Max(0, Math.Min(2, Wound));
            // A fight for freedom does not survive a restart of the game.
            CaptiveFight = false;
        }
    }

    // Where the hero is on the map. Local like the rest of the profile, and
    // kept per hero: a new hero starts in his own homeland.
    internal static class DikozemieTravelState
    {
        internal const int NoVessel = 0;
        internal const int SmallBoat = 1;
        internal const int Ship = 2;

        // Customer, 2026-11-05 evening: «если из трёх досок, то она 220», but
        // «у нас же ладья ... для обычного бедолаги это должна быть ...
        // нереальная сумма». A small boat with oars and a sail is a pound of
        // silver; a longship is the price of a manor
        // (Docs/ARENA_PRICES_HISTORY_RU.md, «Корабли»).
        internal const int SmallBoatPrice = 240;
        internal const int ShipPrice = 48000;
        // Kept for older callers: the cheapest own boat.
        internal const int BoatPrice = SmallBoatPrice;

        // Atlas pixels per second on water.
        internal const float SmallBoatSpeed = 45f;
        internal const float ShipSpeed = 95f;
        internal const float PassageSpeed = 70f;

        private static DikozemieTravelSave _save;

        internal static float VesselSpeed
        {
            get
            {
                DikozemieTravelSave save = _save;
                if (save == null)
                {
                    return PassageSpeed;
                }
                if (save.IsPassenger)
                {
                    return PassageSpeed;
                }
                return save.Vessel == Ship ? ShipSpeed : SmallBoatSpeed;
            }
        }

        internal static string VesselName(int vessel)
        {
            switch (vessel)
            {
                case SmallBoat: return "челн";
                case Ship: return "ладья";
                default: return "лодка";
            }
        }

        // A passenger pays for the way. Crossing the Channel cost 6d a man
        // and 2s with a horse (statute of 1330), a river ferry ½–1d: here
        // a coin per ~120 atlas pixels of sea, at least 2, four times as
        // much for a hero who takes his horses along.
        internal static int PassageFare(float routeLength, bool withHorse)
        {
            int fare = Math.Max(2, (int)Math.Round(routeLength / 120f));
            return withHorse ? fare * 4 : fare;
        }

        internal static DikozemieTravelSave Current => _save;

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Dikozemie",
            "travel.json");

        // The town, the market and the horse's fodder need the hero's place
        // before the map has been opened once.
        internal static DikozemieTravelSave EnsureLoadedForHero()
        {
            if (!DikozemieHeroProfile.IsCreated)
            {
                return null;
            }
            string token = DikozemieHeroProfile.TravelToken;
            if (_save != null && string.Equals(_save.HeroToken, token, StringComparison.Ordinal))
            {
                return _save;
            }
            DikozemieLobbyMapNode home = DikozemieLobbyMapCatalog.FindHomeNode(
                DikozemieHeroProfile.CultureId);
            return Load(
                token,
                home?.Position?.X ?? 3370f,
                home?.Position?.Y ?? 2384f,
                home?.Id);
        }

        internal static DikozemieTravelSave Load(string heroToken, float homeX, float homeY, string homeNodeId)
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    using (FileStream stream = File.OpenRead(FilePath))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(DikozemieTravelSave));
                        var loaded = serializer.ReadObject(stream) as DikozemieTravelSave;
                        if (loaded != null &&
                            string.Equals(loaded.HeroToken, heroToken, StringComparison.Ordinal))
                        {
                            if (loaded.CastlePasses == null)
                            {
                                loaded.CastlePasses = new List<string>();
                            }
                            if (loaded.PassengerTo == null)
                            {
                                loaded.PassengerTo = string.Empty;
                            }
                            if (loaded.FodderDay == null)
                            {
                                loaded.FodderDay = string.Empty;
                            }
                            loaded.FillDefaults();
                            _save = loaded;
                            TakeBackUnpaidVessel(loaded);
                            PutAshoreIfStranded(loaded);
                            return _save;
                        }
                    }
                }
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: travel state unreadable: " + error.Message);
            }
            _save = new DikozemieTravelSave
            {
                HeroToken = heroToken ?? string.Empty,
                X = homeX,
                Y = homeY,
                NodeId = homeNodeId ?? string.Empty
            };
            _save.FillDefaults();
            Save();
            return _save;
        }

        // Customer 2026-11-15: «почему-то у меня есть лодка». Up to 0.5.132 a
        // save that had sat in the free boat of 0.5.119-0.5.123 was handed a
        // small boat on every load, and a party that broke up at sea left its
        // members afloat with no vessel, which the same rule then made theirs.
        // The live treasury has no "vessel" spend at all (checked 2026-11-16),
        // so every vessel without the paid mark is such a gift and goes back.
        private static void TakeBackUnpaidVessel(DikozemieTravelSave save)
        {
            if (save.VesselPaid || (save.Vessel == NoVessel && !save.OwnsBoat))
            {
                return;
            }
            string name = VesselName(save.Vessel == NoVessel ? SmallBoat : save.Vessel);
            save.Vessel = NoVessel;
            save.OwnsBoat = false;
            save.RoadNote = (string.IsNullOrEmpty(save.RoadNote) ? string.Empty : save.RoadNote + "\n") +
                "Ваш " + name + " достался вам по ошибке старой версии и вернулся в порт. " +
                "Своё судно покупают в порту, место на корабле — там же.";
            Save();
        }

        // Afloat with nothing to sail and not a passenger: the boatman of the
        // party or of the old gift sets the hero on the nearest shore.
        internal static bool PutAshoreIfStranded(DikozemieTravelSave save)
        {
            if (save == null || !save.Boat || save.IsPassenger || save.Vessel != NoVessel ||
                DikozemieWorld.IsFollower)
            {
                return false;
            }
            save.Boat = false;
            save.HasTarget = false;
            // A save is read before the map is opened: the terrain may not be
            // loaded yet, and TrySnap would find no shore at all (seen live).
            if (DikozemieTravelTerrain.IsAvailable &&
                DikozemieTravelRouter.TrySnap(save.X, save.Y, false, 200, out int cx, out int cy))
            {
                save.X = (cx + 0.5f) * DikozemieTravelTerrain.CellSize;
                save.Y = (cy + 0.5f) * DikozemieTravelTerrain.CellSize;
            }
            save.NodeId = string.Empty;
            Save();
            return true;
        }

        internal static void Save()
        {
            if (_save == null)
            {
                return;
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string temporary = FilePath + ".tmp";
                using (FileStream stream = File.Create(temporary))
                {
                    new DataContractJsonSerializer(typeof(DikozemieTravelSave))
                        .WriteObject(stream, _save);
                }
                File.Copy(temporary, FilePath, true);
                File.Delete(temporary);
            }
            catch (Exception error)
            {
                Console.WriteLine("DikozemieOnline: travel state not saved: " + error.Message);
            }
        }
    }

    internal enum DikozemieTravelEventKind
    {
        Fight,
        Naval,
        Toll,
        Gift,
        Nothing
    }

    internal sealed class DikozemieTravelEvent
    {
        internal DikozemieTravelEventKind Kind;
        internal string Title;
        internal string Text;
        internal int TeamSize = 1;
        internal int Coins;
    }

    // Cadence of the map's events and the card of the sea. The land cards are
    // DikozemieRoads (stage 2): bandits, man-catchers, tolls, travellers.
    internal static class DikozemieTravelEvents
    {
        // «Если идёшь по дороге, то обязательно тянешь карту события».
        // Tests: DIKOZEMIE_TEST_ROAD_EVENTS=1 draws one every 60 map pixels.
        internal static readonly float RoadEventEvery =
            Environment.GetEnvironmentVariable("DIKOZEMIE_TEST_ROAD_EVENTS") == "1" ? 60f : 900f;
        // Off the road a check every 900 pixels (DikozemieRoads.DrawWild).
        internal const float WildCheckEvery = 900f;
        internal const float SeaChance = 0.2f;

        private static readonly Random Dice = new Random();

        private static readonly DikozemieTravelEvent SeaCard = new DikozemieTravelEvent
        {
            Kind = DikozemieTravelEventKind.Naval,
            Title = "ПИРАТЫ",
            Text = "Из-за мыса показался чужой корабль и идёт на сближение."
        };

        internal static DikozemieTravelEvent DrawSea() => SeaCard;

        internal static bool Roll(float chance) => Dice.NextDouble() < chance;

        // «Когда навык прокачиваешь, ты чаще первый обнаруживаешь кого-то».
        internal static float SpotFirstChance(int scouting) =>
            Math.Min(0.8f, 0.15f + scouting / 200f);
    }
}
