using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using TaleWorlds.ModuleManager;

namespace Dikozemie.Online
{
    [DataContract]
    internal sealed class DikozemieLobbyMapDocument
    {
        [DataMember(Name = "nodes")]
        public List<DikozemieLobbyMapNode> Nodes { get; set; } =
            new List<DikozemieLobbyMapNode>();

        [DataMember(Name = "edges")]
        public List<DikozemieLobbyMapEdge> Edges { get; set; } =
            new List<DikozemieLobbyMapEdge>();
    }

    [DataContract]
    internal sealed class DikozemieLobbyMapPosition
    {
        [DataMember(Name = "x")]
        public float X { get; set; }

        [DataMember(Name = "y")]
        public float Y { get; set; }
    }

    [DataContract]
    internal sealed class DikozemieLobbyMapNode
    {
        [DataMember(Name = "id")]
        public string Id { get; set; } = string.Empty;

        [DataMember(Name = "name")]
        public string Name { get; set; } = string.Empty;

        [DataMember(Name = "nameRu")]
        public string NameRu { get; set; }

        [DataMember(Name = "kind")]
        public string Kind { get; set; } = string.Empty;

        [DataMember(Name = "culture")]
        public string Culture { get; set; } = string.Empty;

        [DataMember(Name = "position")]
        public DikozemieLobbyMapPosition Position { get; set; } =
            new DikozemieLobbyMapPosition();

        [DataMember(Name = "hasPort")]
        public bool HasPort { get; set; }
    }

    [DataContract]
    internal sealed class DikozemieLobbyMapEdge
    {
        [DataMember(Name = "id")]
        public string Id { get; set; } = string.Empty;

        [DataMember(Name = "from")]
        public string From { get; set; } = string.Empty;

        [DataMember(Name = "to")]
        public string To { get; set; } = string.Empty;

        [DataMember(Name = "kind")]
        public string Kind { get; set; } = string.Empty;

        [DataMember(Name = "bidirectional")]
        public bool Bidirectional { get; set; }

        [DataMember(Name = "distance")]
        public float Distance { get; set; }
    }

    internal static class DikozemieLobbyMapCatalog
    {
        internal const string WorldMapFileName = "dikozemie_world_map.json";

        private sealed class RouteStep
        {
            internal string PreviousNodeId;
            internal DikozemieLobbyMapEdge Edge;
        }

        private static readonly object Sync = new object();
        private static bool _loaded;
        private static string _loadError = string.Empty;
        private static IReadOnlyList<DikozemieLobbyMapNode> _nodes =
            Array.Empty<DikozemieLobbyMapNode>();
        private static IReadOnlyList<DikozemieLobbyMapEdge> _edges =
            Array.Empty<DikozemieLobbyMapEdge>();
        private static Dictionary<string, DikozemieLobbyMapNode> _nodesById =
            new Dictionary<string, DikozemieLobbyMapNode>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, List<DikozemieLobbyMapEdge>> _edgesByNode =
            new Dictionary<string, List<DikozemieLobbyMapEdge>>(StringComparer.OrdinalIgnoreCase);

        internal static IReadOnlyList<DikozemieLobbyMapNode> Nodes
        {
            get
            {
                EnsureLoaded();
                return _nodes;
            }
        }

        internal static IReadOnlyList<DikozemieLobbyMapEdge> Edges
        {
            get
            {
                EnsureLoaded();
                return _edges;
            }
        }

        internal static string LoadError
        {
            get
            {
                EnsureLoaded();
                return _loadError;
            }
        }

        internal static DikozemieLobbyMapNode Find(string nodeId)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(nodeId) &&
                _nodesById.TryGetValue(nodeId, out DikozemieLobbyMapNode node)
                    ? node
                    : null;
        }

        internal static DikozemieLobbyMapNode FindHomeNode(string cultureId)
        {
            EnsureLoaded();
            string culture = NormalizeCulture(cultureId);
            IEnumerable<DikozemieLobbyMapNode> candidates = _nodes.Where(
                node => node != null &&
                    string.Equals(NormalizeCulture(node.Culture), culture,
                        StringComparison.OrdinalIgnoreCase));
            DikozemieLobbyMapNode result = candidates
                .OrderBy(NodeKindOrder)
                .ThenBy(node => node.Id, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            return result ?? _nodes
                .Where(node => node != null)
                .OrderBy(NodeKindOrder)
                .ThenBy(node => node.Id, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        internal static DikozemieLobbyMapNode FindNearestNode(float sourceX, float sourceY)
        {
            EnsureLoaded();
            DikozemieLobbyMapNode nearest = null;
            float nearestDistanceSquared = float.MaxValue;
            foreach (DikozemieLobbyMapNode node in _nodes)
            {
                if (node?.Position == null)
                {
                    continue;
                }
                float dx = node.Position.X - sourceX;
                float dy = node.Position.Y - sourceY;
                float distanceSquared = (dx * dx) + (dy * dy);
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearest = node;
                    nearestDistanceSquared = distanceSquared;
                }
            }
            return nearest;
        }

        internal static bool TryBuildShortestRoute(
            string fromNodeId,
            string toNodeId,
            out IReadOnlyList<DikozemieLobbyMapNode> route,
            out float distance,
            out bool usesSea)
        {
            EnsureLoaded();
            route = Array.Empty<DikozemieLobbyMapNode>();
            distance = 0f;
            usesSea = false;
            if (string.IsNullOrWhiteSpace(fromNodeId) ||
                string.IsNullOrWhiteSpace(toNodeId) ||
                !_nodesById.ContainsKey(fromNodeId) ||
                !_nodesById.ContainsKey(toNodeId))
            {
                return false;
            }
            if (string.Equals(fromNodeId, toNodeId, StringComparison.OrdinalIgnoreCase))
            {
                route = new[] { _nodesById[fromNodeId] };
                return true;
            }

            var distances = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            var previous = new Dictionary<string, RouteStep>(StringComparer.OrdinalIgnoreCase);
            var unvisited = new HashSet<string>(_nodesById.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (string nodeId in unvisited)
            {
                distances[nodeId] = float.MaxValue;
            }
            distances[fromNodeId] = 0f;

            while (unvisited.Count > 0)
            {
                string currentId = null;
                float currentDistance = float.MaxValue;
                foreach (string candidateId in unvisited)
                {
                    float candidateDistance = distances[candidateId];
                    if (candidateDistance < currentDistance)
                    {
                        currentId = candidateId;
                        currentDistance = candidateDistance;
                    }
                }
                if (currentId == null || currentDistance == float.MaxValue)
                {
                    break;
                }
                unvisited.Remove(currentId);
                if (string.Equals(currentId, toNodeId, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                if (!_edgesByNode.TryGetValue(currentId, out List<DikozemieLobbyMapEdge> adjacent))
                {
                    continue;
                }
                foreach (DikozemieLobbyMapEdge edge in adjacent)
                {
                    string nextId = string.Equals(edge.From, currentId,
                        StringComparison.OrdinalIgnoreCase) ? edge.To : edge.From;
                    if (!unvisited.Contains(nextId))
                    {
                        continue;
                    }
                    float weight = edge.Distance > 0f ? edge.Distance : 1f;
                    float alternative = currentDistance + weight;
                    if (alternative >= distances[nextId])
                    {
                        continue;
                    }
                    distances[nextId] = alternative;
                    previous[nextId] = new RouteStep
                    {
                        PreviousNodeId = currentId,
                        Edge = edge
                    };
                }
            }

            if (!previous.ContainsKey(toNodeId))
            {
                return false;
            }
            var reversed = new List<DikozemieLobbyMapNode>();
            string cursor = toNodeId;
            reversed.Add(_nodesById[cursor]);
            while (!string.Equals(cursor, fromNodeId, StringComparison.OrdinalIgnoreCase))
            {
                if (!previous.TryGetValue(cursor, out RouteStep step))
                {
                    return false;
                }
                usesSea |= string.Equals(step.Edge?.Kind, "sea-lane",
                    StringComparison.OrdinalIgnoreCase);
                cursor = step.PreviousNodeId;
                reversed.Add(_nodesById[cursor]);
            }
            reversed.Reverse();
            route = reversed;
            distance = distances[toNodeId];
            return true;
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }
            lock (Sync)
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
                        WorldMapFileName);
                    using (FileStream stream = File.OpenRead(path))
                    {
                        var serializer = new DataContractJsonSerializer(
                            typeof(DikozemieLobbyMapDocument),
                            new DataContractJsonSerializerSettings
                            {
                                MaxItemsInObjectGraph = 100000
                            });
                        var document =
                            (DikozemieLobbyMapDocument)serializer.ReadObject(stream);
                        // War Sails settlements in the far north have no painted
                        // land on this atlas: they would stand on nothing.
                        string hiddenPath = Path.Combine(
                            Path.GetDirectoryName(path),
                            "dikozemie_travel_terrain.hidden.txt");
                        var hidden = new HashSet<string>(
                            File.Exists(hiddenPath)
                                ? File.ReadAllLines(hiddenPath)
                                : Array.Empty<string>(),
                            StringComparer.OrdinalIgnoreCase);
                        _nodes = (document?.Nodes ?? new List<DikozemieLobbyMapNode>())
                            .Where(node => node != null &&
                                !string.IsNullOrWhiteSpace(node.Id) &&
                                node.Position != null &&
                                !hidden.Contains(node.Id))
                            .ToArray();
                        _edges = (document?.Edges ?? new List<DikozemieLobbyMapEdge>())
                            .Where(edge => edge != null &&
                                !string.IsNullOrWhiteSpace(edge.From) &&
                                !string.IsNullOrWhiteSpace(edge.To))
                            .ToArray();
                    }
                    _nodesById = _nodes.ToDictionary(
                        node => node.Id,
                        StringComparer.OrdinalIgnoreCase);
                    _edgesByNode = new Dictionary<string, List<DikozemieLobbyMapEdge>>(
                        StringComparer.OrdinalIgnoreCase);
                    foreach (DikozemieLobbyMapEdge edge in _edges)
                    {
                        RegisterEdge(edge.From, edge);
                        if (edge.Bidirectional)
                        {
                            RegisterEdge(edge.To, edge);
                        }
                    }
                    Console.WriteLine(
                        "DikozemieOnline: lobby world map loaded nodes=" +
                        _nodes.Count + " edges=" + _edges.Count);
                }
                catch (Exception error)
                {
                    _loadError = error.Message;
                    _nodes = Array.Empty<DikozemieLobbyMapNode>();
                    _edges = Array.Empty<DikozemieLobbyMapEdge>();
                    Console.WriteLine(
                        "DikozemieOnline: lobby world map failed: " + error);
                }
            }
        }

        private static void RegisterEdge(string nodeId, DikozemieLobbyMapEdge edge)
        {
            if (!_nodesById.ContainsKey(nodeId))
            {
                return;
            }
            if (!_edgesByNode.TryGetValue(nodeId, out List<DikozemieLobbyMapEdge> adjacent))
            {
                adjacent = new List<DikozemieLobbyMapEdge>();
                _edgesByNode[nodeId] = adjacent;
            }
            adjacent.Add(edge);
        }

        private static int NodeKindOrder(DikozemieLobbyMapNode node)
        {
            if (string.Equals(node?.Kind, "town", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }
            if (string.Equals(node?.Kind, "castle", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }
            return 2;
        }

        private static string NormalizeCulture(string cultureId)
        {
            string culture = (cultureId ?? string.Empty).Trim().ToLowerInvariant();
            return culture == "nords" ? "nord" : culture;
        }
    }
}
