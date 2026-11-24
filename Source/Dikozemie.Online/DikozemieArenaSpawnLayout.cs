using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;

namespace Dikozemie.Online
{
    internal readonly struct DikozemieArenaSpawnLayout
    {
        internal DikozemieArenaSpawnLayout(
            Vec3 attackerGate,
            Vec3 defenderGate,
            float footSpacing,
            float mountedSpacing,
            float minimumOpposingTeamDistance,
            float wallInset,
            IReadOnlyList<Vec3> safeBoundary,
            IReadOnlyList<Vec3> attackerWall,
            IReadOnlyList<Vec3> defenderWall)
        {
            AttackerGate = attackerGate;
            DefenderGate = defenderGate;
            FootSpacing = footSpacing;
            MountedSpacing = mountedSpacing;
            MinimumOpposingTeamDistance = minimumOpposingTeamDistance;
            WallInset = wallInset;
            SafeBoundary = safeBoundary;
            AttackerWall = attackerWall;
            DefenderWall = defenderWall;
        }

        internal Vec3 AttackerGate { get; }

        internal Vec3 DefenderGate { get; }

        internal float FootSpacing { get; }

        internal float MountedSpacing { get; }

        internal float MinimumOpposingTeamDistance { get; }

        internal IReadOnlyList<Vec3> SafeBoundary { get; }

        internal IReadOnlyList<Vec3> AttackerWall { get; }

        internal IReadOnlyList<Vec3> DefenderWall { get; }

        internal float WallInset { get; }

        internal float FootBoundaryClearance => 0.75f;

        internal float MountedBoundaryClearance => 1.50f;
    }

    internal static class DikozemieArenaSpawnLayouts
    {
        private const int MaximumFormationSearchRows = 64;

        private static readonly DikozemieArenaSpawnLayout Imperial =
            new DikozemieArenaSpawnLayout(
                // Boundary-safe practice spawns authored beside the west/east
                // arena walls, not the four tournament points near the centre.
                new Vec3(330.370f, 436.102f, 0.000f),
                new Vec3(444.395f, 436.433f, 0.000f),
                footSpacing: 2.00f,
                mountedSpacing: 2.75f,
                minimumOpposingTeamDistance: 80f,
                // The authored soft border sits closer to the masonry than
                // the walkable navigation edge.  Four metres keeps a standing
                // fighter and an approaching AI clear of the solid wall while
                // preserving the requested wall-side entrance line.
                wallInset: 4.00f,
                // Closed border_soft contour authored around the arena floor,
                // ordered clockwise.  Using the full curved side walls gives
                // 20-a-side foot formations enough width without pushing a
                // fighter through the stands.
                safeBoundary: new[]
                {
                    new Vec3(341.499f, 417.264f, 0f),
                    new Vec3(429.227f, 416.959f, 0f),
                    new Vec3(435.381f, 419.097f, 0f),
                    new Vec3(439.468f, 423.242f, 0f),
                    new Vec3(443.372f, 431.414f, 0f),
                    new Vec3(442.968f, 440.667f, 0f),
                    new Vec3(440.639f, 447.698f, 0f),
                    new Vec3(436.241f, 452.806f, 0f),
                    new Vec3(429.971f, 455.558f, 0f),
                    new Vec3(343.332f, 455.279f, 0f),
                    new Vec3(338.437f, 453.927f, 0f),
                    new Vec3(334.576f, 450.547f, 0f),
                    new Vec3(331.585f, 446.005f, 0f),
                    new Vec3(329.595f, 439.597f, 0f),
                    new Vec3(330.322f, 429.346f, 0f),
                    new Vec3(334.097f, 422.440f, 0f)
                },
                attackerWall: new[]
                {
                    new Vec3(334.097f, 422.440f, 0f),
                    new Vec3(330.322f, 429.346f, 0f),
                    new Vec3(329.595f, 439.597f, 0f),
                    new Vec3(331.585f, 446.005f, 0f),
                    new Vec3(334.576f, 450.547f, 0f),
                    new Vec3(338.437f, 453.927f, 0f),
                    new Vec3(343.332f, 455.279f, 0f)
                },
                defenderWall: new[]
                {
                    new Vec3(429.971f, 455.558f, 0f),
                    new Vec3(436.241f, 452.806f, 0f),
                    new Vec3(440.639f, 447.698f, 0f),
                    new Vec3(442.968f, 440.667f, 0f),
                    new Vec3(443.372f, 431.414f, 0f),
                    new Vec3(439.468f, 423.242f, 0f),
                    new Vec3(435.381f, 419.097f, 0f),
                    new Vec3(429.227f, 416.959f, 0f)
                });

        private static readonly DikozemieArenaSpawnLayout Desert =
            new DikozemieArenaSpawnLayout(
                // Opposite boundary-safe practice spawns beside the south and
                // north walls of arena_aserai_a.
                new Vec3(103.087f, 33.140f, 0.000f),
                new Vec3(79.896f, 134.551f, -0.053f),
                footSpacing: 2.00f,
                mountedSpacing: 2.75f,
                minimumOpposingTeamDistance: 40f,
                wallInset: 1.00f,
                // Exact closed arena_sound_path contour authored in
                // SceneObj/arena_aserai_a/scene.xscene. It follows the inner
                // face of the physical arena wall clockwise.
                safeBoundary: new[]
                {
                    new Vec3(71.440f, 134.480f, 0f),
                    new Vec3(88.610f, 142.400f, 0f),
                    new Vec3(100.370f, 145.660f, 0f),
                    new Vec3(117.360f, 142.600f, 0f),
                    new Vec3(126.860f, 131.970f, 0f),
                    new Vec3(155.150f, 67.920f, 0f),
                    new Vec3(154.970f, 51.440f, 0f),
                    new Vec3(147.810f, 40.600f, 0f),
                    new Vec3(136.560f, 34.760f, 0f),
                    new Vec3(109.400f, 27.750f, 0f),
                    new Vec3(95.320f, 31.670f, 0f),
                    new Vec3(84.930f, 44.850f, 0f),
                    new Vec3(64.000f, 87.590f, 0f),
                    new Vec3(53.130f, 113.920f, 0f)
                },
                // Use the complete authored wall arcs. A straight chord through
                // the gate anchor wastes most of the arena width and creates a
                // dense central block in 50/75/100-a-side battles.
                attackerWall: new[]
                {
                    new Vec3(147.810f, 40.600f, 0f),
                    new Vec3(136.560f, 34.760f, 0f),
                    new Vec3(109.400f, 27.750f, 0f),
                    new Vec3(95.320f, 31.670f, 0f),
                    new Vec3(84.930f, 44.850f, 0f)
                },
                defenderWall: new[]
                {
                    new Vec3(53.130f, 113.920f, 0f),
                    new Vec3(71.440f, 134.480f, 0f),
                    new Vec3(88.610f, 142.400f, 0f),
                    new Vec3(100.370f, 145.660f, 0f),
                    new Vec3(117.360f, 142.600f, 0f),
                    new Vec3(126.860f, 131.970f, 0f)
                });

        private static readonly DikozemieArenaSpawnLayout Field =
            new DikozemieArenaSpawnLayout(
                // The native battle_terrain_010 base is a sparse 1.2 km
                // desert plain.  It is more than twice as level as the old
                // steppe base and its complete border_soft contour leaves
                // enough width for 100 cavalry in one row on each side.
                new Vec3(629.279f, 213.278f, 1.073f),
                new Vec3(590.277f, 934.630f, 4.141f),
                footSpacing: 2.25f,
                mountedSpacing: 3.75f,
                minimumOpposingTeamDistance: 500f,
                wallInset: 1.00f,
                safeBoundary: new[]
                {
                    new Vec3(262.462f, 480.567f, 0.372f),
                    new Vec3(333.725f, 363.512f, 4.994f),
                    new Vec3(416.019f, 301.418f, 0.933f),
                    new Vec3(503.915f, 250.269f, 10.851f),
                    new Vec3(629.279f, 213.278f, 1.073f),
                    new Vec3(722.750f, 204.459f, 7.654f),
                    new Vec3(850.204f, 259.300f, 2.293f),
                    new Vec3(936.777f, 380.669f, 10.439f),
                    new Vec3(938.012f, 507.686f, 8.173f),
                    new Vec3(933.747f, 646.831f, 3.164f),
                    new Vec3(899.819f, 747.027f, 7.639f),
                    new Vec3(855.799f, 849.439f, 3.303f),
                    new Vec3(767.811f, 951.082f, 0.520f),
                    new Vec3(590.277f, 934.630f, 4.141f),
                    new Vec3(479.232f, 920.789f, 0.884f),
                    new Vec3(354.384f, 851.030f, 15.297f),
                    new Vec3(265.264f, 707.268f, 12.180f)
                },
                attackerWall: new[]
                {
                    new Vec3(416.019f, 301.418f, 0.933f),
                    new Vec3(503.915f, 250.269f, 10.851f),
                    new Vec3(629.279f, 213.278f, 1.073f),
                    new Vec3(722.750f, 204.459f, 7.654f),
                    new Vec3(850.204f, 259.300f, 2.293f)
                },
                defenderWall: new[]
                {
                    new Vec3(855.799f, 849.439f, 3.303f),
                    new Vec3(767.811f, 951.082f, 0.520f),
                    new Vec3(590.277f, 934.630f, 4.141f),
                    new Vec3(479.232f, 920.789f, 0.884f),
                    new Vec3(354.384f, 851.030f, 15.297f)
                });

        internal static DikozemieArenaSpawnLayout Resolve(string scene)
        {
            if (string.Equals(
                scene,
                DikozemieArenaSceneRules.FieldBattleScene,
                StringComparison.OrdinalIgnoreCase))
            {
                return Field;
            }
            return string.Equals(
                scene,
                DikozemieArenaSceneRules.DesertArenaScene,
                StringComparison.OrdinalIgnoreCase)
                    ? Desert
                    : Imperial;
        }

        internal static IReadOnlyList<Vec3> BuildFormation(
            DikozemieArenaSpawnLayout layout,
            bool attacker,
            int count,
            float spacing)
        {
            var positions = new List<Vec3>(Math.Max(0, count));
            if (count <= 0)
            {
                return positions;
            }

            Vec3 gate = attacker ? layout.AttackerGate : layout.DefenderGate;
            Vec3 opposite = attacker ? layout.DefenderGate : layout.AttackerGate;
            Vec3 inward = opposite - gate;
            inward.z = 0f;
            float gateDistance = inward.Normalize();
            if (gateDistance <= 0.001f)
            {
                return positions;
            }

            float clearance = spacing >= layout.MountedSpacing - 0.01f
                ? layout.MountedBoundaryClearance
                : layout.FootBoundaryClearance;
            IReadOnlyList<Vec3> wall = attacker
                ? layout.AttackerWall
                : layout.DefenderWall;
            float maximumDepth = Math.Max(
                layout.WallInset,
                (gateDistance - layout.MinimumOpposingTeamDistance) * 0.5f);

            for (int row = 0;
                row < MaximumFormationSearchRows && positions.Count < count;
                row++)
            {
                float depth = Math.Max(
                    layout.WallInset,
                    clearance + 0.10f) + (row * spacing);
                if (depth > maximumDepth + 0.01f)
                {
                    break;
                }

                int remaining = count - positions.Count;
                List<Vec3> candidates = BuildWallRowCandidates(
                    layout,
                    wall,
                    inward,
                    depth,
                    spacing,
                    clearance,
                    positions);
                int take = Math.Min(remaining, candidates.Count);
                if (take <= 0)
                {
                    continue;
                }

                // Even the last partial row occupies the full available wall;
                // never pack its few remaining fighters back into the centre.
                for (int column = 0; column < take; column++)
                {
                    int candidateIndex = take == 1
                        ? candidates.Count / 2
                        : (int)Math.Round(
                            column * (candidates.Count - 1d) / (take - 1d));
                    positions.Add(candidates[candidateIndex]);
                }
            }

            if (positions.Count != count)
            {
                throw new InvalidOperationException(
                    "Arena wall-safe formation has capacity " +
                    positions.Count + " for requested " + count + ".");
            }
            return positions;
        }

        private static List<Vec3> BuildWallRowCandidates(
            DikozemieArenaSpawnLayout layout,
            IReadOnlyList<Vec3> wall,
            Vec3 inward,
            float depth,
            float spacing,
            float clearance,
            IReadOnlyList<Vec3> existing)
        {
            var candidates = new List<Vec3>();
            if (wall == null || wall.Count < 2)
            {
                return candidates;
            }

            var insetWall = new List<Vec3>(wall.Count);
            foreach (Vec3 point in wall)
            {
                Vec3 inset = point + (inward * depth);
                inset.z = point.z;
                insetWall.Add(inset);
            }

            float length = GetPolylineLength(insetWall);
            float endMargin = spacing * 0.55f;
            float usableLength = length - (endMargin * 2f);
            if (usableLength < 0f)
            {
                return candidates;
            }
            int sampleCount = Math.Max(
                1,
                (int)Math.Floor(usableLength / (spacing * 1.02f)) + 1);
            for (int index = 0; index < sampleCount; index++)
            {
                float distance = sampleCount == 1
                    ? length * 0.5f
                    : endMargin +
                        (usableLength * index / (sampleCount - 1f));
                Vec3 position = SamplePolyline(insetWall, distance);
                if (!IsInsideSafeBoundary(layout, position, clearance))
                {
                    continue;
                }
                bool overlaps = existing.Any(previous =>
                    previous.Distance(position) < spacing - 0.01f) ||
                    candidates.Any(previous =>
                        previous.Distance(position) < spacing - 0.01f);
                if (!overlaps)
                {
                    candidates.Add(position);
                }
            }
            return candidates;
        }

        private static float GetPolylineLength(IReadOnlyList<Vec3> points)
        {
            float length = 0f;
            for (int index = 1; index < points.Count; index++)
            {
                length += points[index - 1].Distance(points[index]);
            }
            return length;
        }

        private static Vec3 SamplePolyline(
            IReadOnlyList<Vec3> points,
            float requestedDistance)
        {
            float remaining = Math.Max(0f, requestedDistance);
            for (int index = 1; index < points.Count; index++)
            {
                Vec3 start = points[index - 1];
                Vec3 end = points[index];
                float segmentLength = start.Distance(end);
                if (segmentLength <= 0.0001f)
                {
                    continue;
                }
                if (remaining <= segmentLength)
                {
                    return start + ((end - start) *
                        (remaining / segmentLength));
                }
                remaining -= segmentLength;
            }
            return points[points.Count - 1];
        }

        internal static bool IsInsideSafeBoundary(
            DikozemieArenaSpawnLayout layout,
            Vec3 position,
            float clearance)
        {
            IReadOnlyList<Vec3> boundary = layout.SafeBoundary;
            if (boundary == null || boundary.Count < 3 ||
                !IsInsidePolygon(boundary, position))
            {
                return false;
            }
            return GetBoundaryClearance(layout, position) + 0.001f >=
                Math.Max(0f, clearance);
        }

        internal static float GetBoundaryClearance(
            DikozemieArenaSpawnLayout layout,
            Vec3 position)
        {
            IReadOnlyList<Vec3> boundary = layout.SafeBoundary;
            if (boundary == null || boundary.Count < 2)
            {
                return 0f;
            }
            float minimum = float.MaxValue;
            for (int index = 0; index < boundary.Count; index++)
            {
                Vec3 start = boundary[index];
                Vec3 end = boundary[(index + 1) % boundary.Count];
                minimum = Math.Min(
                    minimum,
                    DistanceToSegment2D(position, start, end));
            }
            return minimum;
        }

        internal static float MeasureWallCoverage(
            DikozemieArenaSpawnLayout layout,
            bool attacker,
            IReadOnlyList<Vec3> positions)
        {
            if (positions == null || positions.Count < 2)
            {
                return 0f;
            }
            float maximum = 0f;
            for (int first = 0; first < positions.Count; first++)
            {
                for (int second = first + 1;
                    second < positions.Count;
                    second++)
                {
                    maximum = Math.Max(
                        maximum,
                        positions[first].Distance(positions[second]));
                }
            }
            return maximum;
        }

        internal static int CountFormationRows(
            DikozemieArenaSpawnLayout layout,
            bool attacker,
            IReadOnlyList<Vec3> positions,
            float spacing)
        {
            if (positions == null || positions.Count == 0)
            {
                return 0;
            }
            Vec3 gate = attacker ? layout.AttackerGate : layout.DefenderGate;
            Vec3 opposite = attacker ? layout.DefenderGate : layout.AttackerGate;
            Vec3 inward = opposite - gate;
            inward.z = 0f;
            if (inward.Normalize() <= 0.001f)
            {
                return 0;
            }

            var depths = new List<float>();
            foreach (Vec3 position in positions)
            {
                Vec3 offset = position - gate;
                float depth = (offset.x * inward.x) +
                    (offset.y * inward.y);
                bool known = false;
                foreach (float existing in depths)
                {
                    if (Math.Abs(existing - depth) <= spacing * 0.25f)
                    {
                        known = true;
                        break;
                    }
                }
                if (!known)
                {
                    depths.Add(depth);
                }
            }
            return depths.Count;
        }

        private static bool IsInsidePolygon(
            IReadOnlyList<Vec3> polygon,
            Vec3 point)
        {
            bool inside = false;
            for (int current = 0, previous = polygon.Count - 1;
                current < polygon.Count;
                previous = current++)
            {
                Vec3 a = polygon[current];
                Vec3 b = polygon[previous];
                bool crosses = (a.y > point.y) != (b.y > point.y);
                if (crosses && point.x <
                    ((b.x - a.x) * (point.y - a.y) /
                        (b.y - a.y)) + a.x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private static float DistanceToSegment2D(
            Vec3 point,
            Vec3 start,
            Vec3 end)
        {
            float dx = end.x - start.x;
            float dy = end.y - start.y;
            float lengthSquared = (dx * dx) + (dy * dy);
            if (lengthSquared <= 0.000001f)
            {
                float pointDx = point.x - start.x;
                float pointDy = point.y - start.y;
                return (float)Math.Sqrt(
                    (pointDx * pointDx) + (pointDy * pointDy));
            }
            float projection = (((point.x - start.x) * dx) +
                ((point.y - start.y) * dy)) / lengthSquared;
            projection = Math.Max(0f, Math.Min(1f, projection));
            float nearestX = start.x + (projection * dx);
            float nearestY = start.y + (projection * dy);
            float distanceX = point.x - nearestX;
            float distanceY = point.y - nearestY;
            return (float)Math.Sqrt(
                (distanceX * distanceX) + (distanceY * distanceY));
        }
    }
}
