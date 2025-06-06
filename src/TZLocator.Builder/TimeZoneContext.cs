// Copyright (c) devsko. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using GeoJson;

using static GeoJson.Geo<GeoJson.Position2D<float>, float>;

namespace TZLocator.Builder;

/// <summary>
/// Provides context and operations for building and managing time zone data structures from GeoJSON sources.
/// </summary>
public sealed partial class TimeZoneContext
{
    [JsonSerializable(typeof(Feature<TimeZoneProperties>))]
    [JsonSerializable(typeof(FeatureCollection<TimeZoneProperties>))]
    private sealed partial class JsonContext : JsonSerializerContext
    { }

    /// <summary>
    /// <para>
    /// Provides a sliding window over a ring of geographic positions for efficient geometric operations.
    /// </para>
    /// <para>
    /// The <see cref="RingDataWindow"/> is a <c>ref struct</c> that maintains a window of four consecutive positions
    /// (I_1, I, J, J_1) over a read-only span of <see cref="Position"/> values representing a closed ring (polygon).
    /// It is used to efficiently iterate over the edges of the ring and perform geometric calculations such as
    /// edge crossings and point-in-polygon tests.
    /// </para>
    /// <para>
    /// The window is advanced by calling <see cref="Increment"/>, which moves the window forward by one position.
    /// The window is valid as long as <see cref="HasMore"/> returns <see langword="true"/>.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The window is initialized with a span of positions, and assumes the ring is closed and contains at least four points
    /// (see <see cref="GetReducedPositionList"/>).
    /// </remarks>
    private ref struct RingDataWindow
    {
        private int _length;
        private ref readonly Position _start;

        public RingDataWindow(ReadOnlySpan<Position> ring)
        {
            _start = ref ring[0];
            _length = ring.Length - 3;
        }

        public readonly bool HasMore => _length > 0;

        public void Increment()
        {
            _start = ref Unsafe.Add(ref Unsafe.AsRef(in _start), 1);
            _length--;
        }

        public readonly Position I_1 => _start;
        public readonly Position I => Unsafe.Add(ref Unsafe.AsRef(in _start), 1);
        public readonly Position J => Unsafe.Add(ref Unsafe.AsRef(in _start), 2);
        public readonly Position J_1 => Unsafe.Add(ref Unsafe.AsRef(in _start), 3);
    }

    private const int MaxLevel = 25;
    private const float ReducedPositionDistance = 500f;

    private static readonly Position Outside = GetOutside();

    private readonly Dictionary<TimeZoneBuilderNode, TimeZoneIndex> _multipleTimeZones = [];
    private readonly Dictionary<string, short> _indices = [];
    private readonly Dictionary<short, TimeZoneSource> _sources = [];

    /// <summary>
    /// Gets a collection of all <see cref="TimeZoneSource"/> objects loaded in this context.
    /// </summary>
    public ICollection<TimeZoneSource> Sources => _sources.Values;

    /// <summary>
    /// Gets the <see cref="TimeZoneSource"/> associated with the specified index.
    /// </summary>
    /// <param name="index">The index of the time zone source.</param>
    /// <returns>The <see cref="TimeZoneSource"/> for the given index.</returns>
    public TimeZoneSource GetSource(short index) => _sources[index];

    /// <summary>
    /// Gets the <see cref="TimeZoneSource"/> associated with the specified time zone name.
    /// </summary>
    /// <param name="timeZone">The name of the time zone.</param>
    /// <returns>The <see cref="TimeZoneSource"/> for the given time zone name.</returns>
    public TimeZoneSource GetSource(string timeZone) => _sources[_indices[timeZone]];

    private TimeZoneContext()
    { }

    /// <summary>
    /// Asynchronously loads a <see cref="TimeZoneContext"/> from a stream containing GeoJSON data.
    /// </summary>
    /// <param name="stream">The input <see cref="Stream"/> containing GeoJSON time zone data.</param>
    /// <returns>
    /// A <see cref="Task{TimeZoneContext}"/> representing the asynchronous operation, with the loaded <see cref="TimeZoneContext"/> as the result.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the GeoJSON data cannot be deserialized into a <see cref="FeatureCollection{TimeZoneProperties}"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown if the geometry type in the GeoJSON is not supported (i.e., not <see cref="Polygon"/> or <see cref="MultiPolygon"/>).
    /// </exception>
    public static async Task<TimeZoneContext> LoadAsync(Stream stream)
    {
        GeoSingle2D geo = new(JsonContext.Default, typeof(TimeZoneProperties));
        FeatureCollection<TimeZoneProperties> collection = await geo.DeserializeAsync<FeatureCollection<TimeZoneProperties>>(stream) ?? throw new InvalidOperationException();

        TimeZoneContext context = new();
        foreach (Feature<TimeZoneProperties> feature in collection.Features)
        {
            List<Position>[] included;
            List<Position>[] excluded;
            if (feature.Geometry is Polygon polygon)
            {
                included = [GetReducedPositionList(polygon.Coordinates[0])];
                if (polygon.Coordinates.Length == 1)
                {
                    excluded = [];
                }
                else
                {
                    excluded = new List<Position>[polygon.Coordinates.Length - 1];
                    for (int i = 0; i < excluded.Length; i++)
                    {
                        excluded[i] = GetReducedPositionList(polygon.Coordinates[i + 1]);
                    }
                }
            }
            else if (feature.Geometry is MultiPolygon multiPolygon)
            {
                included = new List<Position>[multiPolygon.Coordinates.Length];
                excluded = new List<Position>[multiPolygon.Coordinates.Select(rings => rings.Length - 1).Sum()];
                int excludesIndex = 0;
                for (int i = 0; i < included.Length; i++)
                {
                    included[i] = GetReducedPositionList(multiPolygon.Coordinates[i][0]);
                    for (int j = 1; j < multiPolygon.Coordinates[i].Length; j++)
                    {
                        excluded[excludesIndex++] = GetReducedPositionList(multiPolygon.Coordinates[i][j]);
                    }
                }
            }
            else
            {
                throw new NotSupportedException();
            }
            string timeZone = feature.Properties.tzid;
            short index = (short)(context._sources.Count + 1);
            context._indices.Add(timeZone, index);
            context._sources.Add(index, new TimeZoneSource { Index = index, Name = timeZone, Included = included, Excluded = excluded });
        }

        return context;
    }

    /// <summary>
    /// Creates a new <see cref="TimeZoneBuilderTree"/> using the loaded time zone sources.
    /// Each time zone source is added to the tree, incrementing the node count.
    /// Optionally reports progress after each source is added.
    /// </summary>
    /// <param name="progress">
    /// An optional <see cref="IProgress{T}"/> instance to report the number of sources processed.
    /// </param>
    /// <returns>
    /// A <see cref="TimeZoneBuilderTree"/> containing all loaded time zone sources.
    /// </returns>
    public (TimeZoneBuilderTree Tree, int NodeCount) CreateTree(IProgress<int>? progress = null)
    {
        TimeZoneBuilderTree tree = new([.. Sources.Select(source => source.Name)]);
        int count = 0;
        int nodeCount = 1;
        foreach (TimeZoneSource source in Sources)
        {
            nodeCount += Add(tree, source);
            progress?.Report(++count);
        }

        return (tree, nodeCount);
    }

    /// <summary>
    /// Adds the included rings of a <see cref="TimeZoneSource"/> to the specified <see cref="TimeZoneBuilderTree"/>.
    /// Each included ring is recursively inserted into the tree, partitioning the space as needed.
    /// If a node's bounding box is fully contained or overlaps with the ring, the time zone index is added to the node.
    /// Handles cases where a node may represent multiple time zones by updating an internal dictionary.
    /// </summary>
    /// <param name="tree">The <see cref="TimeZoneBuilderTree"/> to which the time zone source will be added.</param>
    /// <param name="source">The <see cref="TimeZoneSource"/> containing the time zone data to add.</param>
    public int Add(TimeZoneBuilderTree tree, TimeZoneSource source)
    {
        int halfNodeCount = 0;
        foreach (List<Position> ring in source.Included)
        {
            Add(tree.Root, source.Index, CollectionsMarshal.AsSpan(ring), BBox.World, 0, _multipleTimeZones, ref halfNodeCount);
        }

        return halfNodeCount * 2;

        static void Add(TimeZoneBuilderNode node, short index, ReadOnlySpan<Position> ring, BBox box, int level, Dictionary<TimeZoneBuilderNode, TimeZoneIndex> multiples, ref int halfNodeCount)
        {
            (bool subset, bool overlapping) = Check(ring, box);

            if (subset)
            {
                AddIndex(node, index, multiples);
            }
            else if (overlapping)
            {
                if (level == MaxLevel)
                {
                    AddIndex(node, index, multiples);
                }
                else
                {
                    (BBox hi, BBox lo) = box.Split(ref level);
                    if (node.Hi is null || node.Lo is null)
                    {
                        node.Hi = new TimeZoneBuilderNode(node.Index);
                        node.Lo = new TimeZoneBuilderNode(node.Index);
                        halfNodeCount++;
                    }

                    Add(node.Hi, index, ring, hi, level, multiples, ref halfNodeCount);
                    Add(node.Lo, index, ring, lo, level, multiples, ref halfNodeCount);
                }
            }

            static void AddIndex(TimeZoneBuilderNode node, short index, Dictionary<TimeZoneBuilderNode, TimeZoneIndex> multiples)
            {
                if (!node.Index.Add(index))
                {
                    CollectionsMarshal.GetValueRefOrAddDefault(multiples, node, out _).Add(index);
                }
            }
        }
    }

    /// <summary>
    /// Consolidates the time zone indices in the given <see cref="TimeZoneBuilderTree"/> by resolving overlaps and exclusions.
    /// This process traverses the tree, updating each node's <see cref="TimeZoneIndex"/> to reflect the most accurate set of time zones
    /// for the corresponding geographic region, taking into account included and excluded rings from all sources.
    /// The method uses a grid sampling approach to determine the dominant time zone(s) in ambiguous regions.
    /// Optionally reports progress after processing each node.
    /// </summary>
    /// <param name="tree">The <see cref="TimeZoneBuilderTree"/> whose nodes will be consolidated.</param>
    /// <param name="progress">
    /// An optional <see cref="IProgress{T}"/> instance to report the number of nodes processed.
    /// </param>
    public void Consolidate(TimeZoneBuilderTree tree, IProgress<int> progress)
    {
        TimeZoneIndex[] indices = new TimeZoneIndex[25];
        int count = 0;

        Consolidate(tree.Root, default, BBox.World, 0);

        void Consolidate(TimeZoneBuilderNode node, TimeZoneIndex2 index, BBox box, int level)
        {
            Dictionary<short, TimeZoneSource> sources = _sources;

            foreach (short nodeIndex in GetMultipleTimeZones(node, _multipleTimeZones))
            {
                if (!IsExcluded(sources[nodeIndex], box))
                {
                    index.Add(nodeIndex);
                }
            }

            if (node.Hi is not null && node.Lo is not null)
            {
                node.Index = default;
                (BBox hi, BBox lo) = box.Split(ref level);
                Consolidate(node.Hi, index, hi, level);
                Consolidate(node.Lo, index, lo, level);
            }
            else if (index.Second != 0)
            {
                Array.Clear(indices);
                foreach (short nodeIndex in index)
                {
                    GetArea(indices, sources[nodeIndex], box);
                }
                node.Index = GetFinalIndex(indices);
            }
            else if (index.First != 0)
            {
                node.Index = new TimeZoneIndex(index.First);
            }

            progress.Report(++count);
        }

        // Retrieves all time zone indices associated with a given TimeZoneBuilderNode,
        // including both the primary and secondary indices stored directly in the node, as well as any
        // additional indices present in the internal dictionary for nodes
        // representing multiple time zones.
        static IEnumerable<short> GetMultipleTimeZones(TimeZoneBuilderNode node, Dictionary<TimeZoneBuilderNode, TimeZoneIndex> multiples)
        {
            if (node.Index.First == 0) yield break;
            yield return node.Index.First;
            if (node.Index.Second == 0) yield break;
            yield return node.Index.Second;

            if (multiples.TryGetValue(node, out TimeZoneIndex value))
            {
                if (value.First == 0) yield break;
                yield return value.First;
                if (value.Second == 0) yield break;
                yield return value.Second;
            }
        }

        // Determines whether the specified bounding box is fully excluded by any of the exclusion rings
        // in the given TimeZoneSource. This is used to check if a region represented by the box
        // parameter should be excluded from the time zone due to the presence of exclusion polygons.
        static bool IsExcluded(TimeZoneSource source, BBox box)
        {
            foreach (List<Position> ring in source.Excluded)
            {
                if (Check(CollectionsMarshal.AsSpan(ring), box).Subset)
                {
                    return true;
                }
            }

            return false;
        }

        // Populates the provided TimeZoneIndex array with the time zone indices that are present
        // within the specified BBox region for a given TimeZoneSource.
        // The method samples a 5x5 grid of points within the bounding box and, for each point, determines if it
        // lies inside the included regions and outside the excluded regions of the time zone source. If a point
        // is inside, the source's index is added to the corresponding entry in the indices array.
        static void GetArea(TimeZoneIndex[] indices, TimeZoneSource source, BBox box)
        {
            int i = 0;
            for (int x = 0; x < 5; x++)
            {
                float longitude = Lerp(box.SouthWest.Longitude, box.NorthEast.Longitude, (float)x / 4);
                for (int y = 0; y < 5; y++)
                {
                    float latitude = Lerp(box.SouthWest.Latitude, box.NorthEast.Latitude, (float)y / 4);
                    if (IsInside(new Position(longitude, latitude), source))
                    {
                        indices[i].Add(source.Index);
                    }
                    i++;
                }
            }

            static float Lerp(float v0, float v1, float t) => MathF.FusedMultiplyAdd(t, v1 - v0, v0);

            static bool IsInside(Position point, TimeZoneSource source)
            {
                foreach (List<Position> ring in source.Excluded)
                {
                    if (TimeZoneContext.IsInside(CollectionsMarshal.AsSpan(ring), point))
                    {
                        return false;
                    }
                }
                foreach (List<Position> ring in source.Included)
                {
                    if (TimeZoneContext.IsInside(CollectionsMarshal.AsSpan(ring), point))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        // Determines the final TimeZoneIndex to assign to a node based on an array of sampled indices.
        // This method groups the provided indices array by unique TimeZoneIndex values,
        // counts the occurrences of each, and selects the most frequent (dominant) index. If the dominant index contains
        // two time zone indices and the first is greater than the second, the result is normalized to ensure the lower
        // index is first. This is used to resolve ambiguous or overlapping regions by majority sampling.
        static TimeZoneIndex GetFinalIndex(TimeZoneIndex[] indices)
        {
            TimeZoneIndex index = indices
                .GroupBy(index => index)
                .Select(group => (Index: group.Key, Count: group.Count()))
                .MaxBy(t => t.Count)
                .Index;

            return index.Second != 0 && index.First > index.Second
                ? new TimeZoneIndex(index.Second) { index.First }
                : index;
        }
    }

    /// <summary>
    /// <para>
    /// Reduces a ring of geographic positions by filtering out redundant points and returns a list of <see cref="Position"/> values
    /// with additional padding at the start and end for geometric operations.</para>
    /// <para>
    /// The method iterates through the provided <paramref name="ring"/> of <see cref="Position2D{T}"/> values, yielding only those points
    /// that are either sufficiently distant from the previous point (greater than 1,000 meters) or have a latitude greater than 70 degrees
    /// (in absolute value) and are not equal to the last yielded position. The resulting list is then padded by inserting the last point
    /// at the beginning and the first two points after the end, ensuring a minimum of four points for geometric windowing.
    /// </para>
    /// </summary>
    /// <param name="ring">The immutable array of <see cref="Position2D{T}"/> representing the closed ring (polygon).</param>
    /// <returns>
    /// A <see cref="List{Position}"/> containing the reduced and padded positions for efficient geometric processing.
    /// </returns>
    private static List<Position> GetReducedPositionList(ImmutableArray<Position2D<float>> ring)
    {
        List<Position> list = [.. ReduceRingPositions(ring)];
        list.Insert(0, list[^1]);
        list.Add(list[1]);
        list.Add(list[2]);

        return list;

        static IEnumerable<Position> ReduceRingPositions(ImmutableArray<Position2D<float>> ring)
        {
            Position2D<float> lastPosition;
            yield return Unsafe.BitCast<Position2D<float>, Position>(lastPosition = ring[0]);
            for (int i = 1; i < ring.Length - 1; i++)
            {
                Position2D<float> position = ring[i];
                if ((Math.Abs(position.Latitude) > 70f && position != lastPosition) ||
                    Distance(lastPosition, position) > ReducedPositionDistance)
                {
                    yield return Unsafe.BitCast<Position2D<float>, Position>(position);
                    lastPosition = position;
                }
            }
        }
    }

    /// <summary>
    /// Calculates the great-circle distance in meters between two geographic coordinates
    /// specified by their latitudes and longitudes using the Haversine formula.
    /// </summary>
    /// <param name="from">The coordinates of the starting point, in degrees.</param>
    /// <param name="to">The coordinates of the destination point, in degrees.</param>
    /// <returns>The distance between the two points in meters.</returns>
    private static float Distance(Position2D<float> from, Position2D<float> to)
    {
        float lat1 = ToRadians(from.Latitude);
        float lat2 = ToRadians(to.Latitude);
        float lon1 = ToRadians(from.Longitude);
        float lon2 = ToRadians(to.Longitude);

        float havLat = MathF.Pow(MathF.Sin((lat2 - lat1) / 2), 2);
        float havLon = MathF.Pow(MathF.Sin((lon2 - lon1) / 2), 2);

        return 2 * 6371009 *
            MathF.Asin(MathF.Sqrt(havLat + MathF.Cos(lat2) * MathF.Cos(lat1) * havLon));

        static float ToRadians(float degrees) => degrees * MathF.PI / 180;
    }

    private static Position GetOutside()
    {
        Span<float> coordinates = [0f, 200f];
        return Unsafe.As<float, Position>(ref coordinates[0]);
    }

    /// <summary>
    /// <para>
    /// Determines whether a given point is inside a closed geographic ring (polygon).
    /// </para>
    /// <para>
    /// This method uses a winding number algorithm with edge detection to check if the specified <paramref name="point"/>
    /// lies within the polygon defined by <paramref name="ring"/>.
    /// </para>
    /// <para>
    /// The method iterates over the ring using a sliding window, toggling the inside state on each edge crossing.
    /// If the point lies exactly on an edge, the method returns <see langword="true"/> immediately.
    /// </para>
    /// </summary>
    /// <param name="ring">A span of <see cref="Position"/> values representing the closed ring (polygon).</param>
    /// <param name="point">The <see cref="Position"/> to test for inclusion within the ring.</param>
    /// <returns>
    /// <see langword="true"/> if the point is inside the ring or exactly on its edge; otherwise, <see langword="false"/>.
    /// </returns>
    private static bool IsInside(ReadOnlySpan<Position> ring, Position point)
    {
        bool isInside = false;
        bool isOnEdge = false;
        for (RingDataWindow p = new(ring); p.HasMore; p.Increment())
        {
            if (Crossing(ref p, point, Outside, ref isOnEdge))
            {
                isInside = !isInside;
            }
            if (isOnEdge)
            {
                return true;
            }
        }

        return isInside;
    }

    /// <summary>
    /// <para>
    /// Determines whether a bounding box is a subset of, or overlaps with, a closed geographic ring (polygon).</para>
    /// <para>
    /// This method checks if the specified <paramref name="box"/> is fully contained within the polygon defined by <paramref name="ring"/>,
    /// or if it overlaps with the polygon.
    /// </para>
    /// <para>
    /// The method evaluates the four corners of the bounding box to determine
    /// if they are inside the polygon or on its edge. It also checks for edge crossings between the box and the polygon.
    /// The result is a tuple indicating whether the box is a subset of the polygon and whether it overlaps with the polygon.
    /// </para>
    /// </summary>
    /// <param name="ring">A span of <see cref="Position"/> values representing the closed ring (polygon).</param>
    /// <param name="box">The <see cref="BBox"/> to test for containment or overlap with the ring.</param>
    /// <returns>
    /// A tuple where <c>Subset</c> is <see langword="true"/> if the box is fully contained within the ring and does not cross any edge,
    /// and <c>Overlapping</c> is <see langword="true"/> if the box is fully contained, overlaps, or contains the ring's first point.
    /// </returns>
    private static (bool Subset, bool Overlapping) Check(ReadOnlySpan<Position> ring, BBox box)
    {
        Position southWest = box.SouthWest;
        Position northEast = box.NorthEast;
        Position northWest = new(southWest.Longitude, northEast.Latitude);
        Position southEast = new(northEast.Longitude, southWest.Latitude);

        bool edgeCrossing = false;
        bool isOnEdge = false;
        bool southWestInside = false;
        bool northEastInside = false;
        bool northWestInside = false;
        bool southEastInside = false;
        bool southWestOnEdge = false;
        bool northEastOnEdge = false;
        bool northWestOnEdge = false;
        bool southEastOnEdge = false;

        for (RingDataWindow p = new(ring); p.HasMore; p.Increment())
        {
            edgeCrossing |=
                Crossing(ref p, northWest, southWest, ref isOnEdge) ||
                Crossing(ref p, southWest, southEast, ref isOnEdge) ||
                Crossing(ref p, southEast, northEast, ref isOnEdge) ||
                Crossing(ref p, northEast, northWest, ref isOnEdge);

            if (northWestOnEdge || Crossing(ref p, northWest, Outside, ref northWestOnEdge)) northWestInside = !northWestInside;
            if (southWestOnEdge || Crossing(ref p, southWest, Outside, ref southWestOnEdge)) southWestInside = !southWestInside;
            if (northEastOnEdge || Crossing(ref p, northEast, Outside, ref northEastOnEdge)) northEastInside = !northEastInside;
            if (southEastOnEdge || Crossing(ref p, southEast, Outside, ref southEastOnEdge)) southEastInside = !southEastInside;
        }

        bool allCornersInside =
            (southWestOnEdge || southWestInside) &&
            (northEastOnEdge || northEastInside) &&
            (northWestOnEdge || northWestInside) &&
            (southEastOnEdge || southEastInside);

        return (allCornersInside && !edgeCrossing && !isOnEdge, allCornersInside || edgeCrossing || isOnEdge || BoxContains(ring[0]));

        bool BoxContains(Position q)
        {
            int crossings = 0;
            bool dummy = false;
            for (RingDataWindow box = new([northWest, southWest, southEast, northEast, northWest, southWest, southEast]); box.HasMore; box.Increment())
            {
                crossings += Crossing(ref box, q, Outside, ref dummy) ? 1 : 0;
            }

            return crossings % 2 != 0;
        }
    }

    /// <summary>
    /// <para>
    /// Determines whether two line segments cross or if a point lies exactly on an edge of a polygon.</para>
    /// <para>
    /// This method is used in geometric algorithms to detect edge crossings and point-on-edge conditions
    /// when traversing a polygon ring. It evaluates whether the segment from <paramref name="q"/> to <paramref name="r"/>
    /// crosses the current edge of the ring defined by the <paramref name="p"/> window (I_1, I, J, J_1).
    /// </para>
    /// <para>
    /// The method uses determinants to check for intersection and collinearity. If the point <paramref name="q"/>
    /// lies exactly on the edge, the <paramref name="isOnEdge"/> flag is set to <see langword="true"/>.
    /// </para>
    /// </summary>
    /// <remarks>see also https://youtu.be/PvUK52xiFZs?t=1270 for further explanation.</remarks>
    /// <param name="p">
    /// A <see cref="RingDataWindow"/> providing the current four-point window over the polygon ring.
    /// </param>
    /// <param name="q">
    /// The first endpoint of the segment to test (often the test point or a corner of a bounding box).
    /// </param>
    /// <param name="r">
    /// The second endpoint of the segment to test (often the outside reference point or another corner).
    /// </param>
    /// <param name="isOnEdge">
    /// A <see cref="bool"/> passed by reference that is set to <see langword="true"/> if <paramref name="q"/> lies exactly on the edge.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the segment from <paramref name="q"/> to <paramref name="r"/> crosses the current edge of the ring;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    private static bool Crossing(ref RingDataWindow p, Position q, Position r, ref bool isOnEdge)
    {
        float dq = Determinant(q, p.I, p.J);
        float dr = Determinant(r, p.I, p.J);
        if (dq == 0)
        {
            if (p.I.Longitude == q.Longitude && p.I.Latitude == q.Latitude ||
                p.J.Longitude == q.Longitude && p.J.Latitude == q.Latitude ||
                (p.I.Longitude - q.Longitude) * (p.J.Longitude - q.Longitude) < 0 ||
                (p.I.Latitude - q.Latitude) * (p.J.Latitude - q.Latitude) < 0)
            {
                isOnEdge = true;
                if (dr == 0)
                {
                    // (iii) or (iv)
                    return Determinant(p.I_1, q, r) * Determinant(p.J_1, q, r) < 0;
                }
            }
        }

        float dpi = Determinant(p.I, q, r);
        float dpj = Determinant(p.J, q, r);
        if (dpi == 0)
        {
            if (q.Longitude == p.I.Longitude && q.Latitude == p.I.Latitude ||
                (q.Longitude - p.I.Longitude) * (r.Longitude - p.I.Longitude) < 0 ||
                (q.Latitude - p.I.Latitude) * (r.Latitude - p.I.Latitude) < 0)
            {
                isOnEdge = true;
                // (i) or (ii)
                return Determinant(p.I_1, q, r) * dpj < 0;
            }
        }

        return (dr * dq) < 0 && (dpi * dpj) < 0;

        static float Determinant(Position origin, Position p1, Position p2) =>
            (p1.Longitude - origin.Longitude) * (p2.Latitude - origin.Latitude) -
            (p1.Latitude - origin.Latitude) * (p2.Longitude - origin.Longitude);
    }
}

/// <summary>
/// Represents the properties of a time zone feature in GeoJSON data.
/// </summary>
/// <param name="tzid">
/// The time zone identifier (e.g., "Europe/Berlin").
/// </param>
public record struct TimeZoneProperties(string tzid);
