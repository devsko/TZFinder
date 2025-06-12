// Copyright (c) devsko. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using GeoJson;

using static GeoJson.Geo<GeoJson.Position2D<float>, float>;

namespace TZFinder.Builder;

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

    private static readonly Position Outside = GetOutside();

    private readonly Dictionary<TimeZoneNode, TimeZoneIndex> _multipleTimeZones = [];
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
    /// Gets the <see cref="TimeZoneSource"/> associated with the specified time zone identifier.
    /// </summary>
    /// <param name="id">The identifier of the time zone.</param>
    /// <returns>The <see cref="TimeZoneSource"/> for the given time zone identifier.</returns>
    public TimeZoneSource GetSource(string id) => _sources[_indices[id]];

    /// <summary>
    /// Removes all loaded <see cref="TimeZoneSource"/> objects from the current context.
    /// This method clears the internal dictionary that maps time zone indices to their corresponding sources.
    /// After calling this method, the <see cref="Sources"/> collection will be empty.
    /// </summary>
    public void ClearSources() => _sources.Clear();

    private TimeZoneContext()
    { }

    /// <summary>
    /// Asynchronously loads a <see cref="TimeZoneContext"/> from a stream containing GeoJSON data.
    /// </summary>
    /// <param name="stream">The input <see cref="Stream"/> containing GeoJSON time zone data.</param>
    /// <param name="minRingDistance">
    /// The minimum distance in meters between consecutive points in a ring. Points closer than this distance will be filtered out.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task{TimeZoneContext}"/> representing the asynchronous operation, with the loaded <see cref="TimeZoneContext"/> as the result.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the GeoJSON data cannot be deserialized into a <see cref="FeatureCollection{TimeZoneProperties}"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown if the geometry type in the GeoJSON is not supported (i.e., not <see cref="Polygon"/> or <see cref="MultiPolygon"/>).
    /// </exception>
    public static async Task<TimeZoneContext> LoadAsync(Stream stream, int minRingDistance, CancellationToken cancellationToken)
    {
        GeoSingle2D geo = new(JsonContext.Default, typeof(TimeZoneProperties));
        FeatureCollection<TimeZoneProperties> collection = await geo.DeserializeAsync<FeatureCollection<TimeZoneProperties>>(stream, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException();

        TimeZoneContext context = new();
        foreach (Feature<TimeZoneProperties> feature in collection.Features)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<Position>[] included;
            List<Position>[] excluded;
            if (feature.Geometry is Polygon polygon)
            {
                included = [GetReducedPositionList(polygon.Coordinates[0], minRingDistance)];
                if (polygon.Coordinates.Length == 1)
                {
                    excluded = [];
                }
                else
                {
                    excluded = new List<Position>[polygon.Coordinates.Length - 1];
                    for (int i = 0; i < excluded.Length; i++)
                    {
                        excluded[i] = GetReducedPositionList(polygon.Coordinates[i + 1], minRingDistance);
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
                    included[i] = GetReducedPositionList(multiPolygon.Coordinates[i][0], minRingDistance);
                    for (int j = 1; j < multiPolygon.Coordinates[i].Length; j++)
                    {
                        excluded[excludesIndex++] = GetReducedPositionList(multiPolygon.Coordinates[i][j], minRingDistance);
                    }
                }
            }
            else
            {
                throw new NotSupportedException();
            }
            string id = feature.Properties.tzid;
            short index = (short)(context._sources.Count + 1);
            context._indices.Add(id, index);
            context._sources.Add(index, new TimeZoneSource { Index = index, Id = id, Included = included, Excluded = excluded });
        }

        return context;
    }

    private struct Creation
    {
        public TimeZoneNode Node;
        public short Index;
        public List<Position> Ring;
        public BBox Box;
        public int Level;
    }

    private sealed class CreationComparer : IComparer<Creation>
    {
        public int Compare(Creation x, Creation y) => x.Index - y.Index;
    }

    /// <summary>
    /// <para>
    /// Asynchronously creates a <see cref="TimeZoneBuilderTree"/> representing the spatial partitioning of time zones.
    /// </para>
    /// <para>
    /// This method builds a tree structure by recursively subdividing the world bounding box into smaller regions (nodes),
    /// assigning time zone indices to each node based on the inclusion rings of the loaded time zone sources.
    /// </para>
    /// <para>
    /// The process is parallelized across multiple threads for efficiency. Each node is processed by checking if its bounding box
    /// is fully contained within, overlaps with, or is outside the relevant time zone polygons. If a node is not fully contained
    /// and the maximum tree depth (<paramref name="maxLevel"/>) has not been reached, the node is split and its children are processed recursively.
    /// </para>
    /// <para>
    /// Progress is reported via the <paramref name="progress"/> callback after each completed node.
    /// </para>
    /// </summary>
    /// <param name="maxLevel">The maximum depth of the tree. Nodes will not be subdivided beyond this level.</param>
    /// <param name="progress">A progress reporter that receives an increment for each completed node.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A <see cref="Task{TimeZoneBuilderTree}"/> representing the asynchronous operation, with the constructed <see cref="TimeZoneBuilderTree"/> as the result.
    /// </returns>
    public async Task<TimeZoneBuilderTree> CreateTreeAsync(int maxLevel, IProgress<int> progress, CancellationToken cancellationToken)
    {
        TimeZoneBuilderTree tree = new([.. Sources.Select(source => source.Id)]);
        TimeZoneNode root = tree.Root;

        Channel<Creation> creations = Channel.CreateUnboundedPrioritized<Creation>(new() { Comparer = new CreationComparer() });
        ConcurrentDictionary<short, int> indexCreations = new(
            Environment.ProcessorCount,
            Sources.Select(source => KeyValuePair.Create(source.Index, source.Included.Length)),
            comparer: null);

        foreach (TimeZoneSource source in Sources)
        {
            foreach (List<Position> ring in source.Included)
            {
                await creations.Writer.WriteAsync(new() { Node = root, Index = source.Index, Ring = ring, Box = BBox.World }, cancellationToken).ConfigureAwait(false);
            }
        }

        int totalCreations = creations.Reader.Count;

        Task[] threads = [.. Enumerable
            .Range(0, Environment.ProcessorCount)
            .Select(_ => Task.Run(ProcessCreationAsync))];

        await Task.WhenAll(threads).ConfigureAwait(false);

        return tree;

        async Task ProcessCreationAsync()
        {
            while (await creations.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (creations.Reader.TryRead(out Creation creation))
                {
                    await AddAsync(creation.Node, creation.Index, creation.Ring, creation.Box, creation.Level).ConfigureAwait(false);
                    if (IncrementIndexCreations(creation.Index, -1) == 0)
                    {
                        progress.Report(1);
                    }
                    if (Interlocked.Decrement(ref totalCreations) == 0)
                    {
                        creations.Writer.Complete();
                    }
                }
            }
        }

        async ValueTask AddAsync(TimeZoneNode node, short index, List<Position> ring, BBox box, int level)
        {
            (bool isSubset, bool isOverlapping) = CheckRelation(CollectionsMarshal.AsSpan(ring), box);

            bool addToChildren = false;

            lock (node)
            {
                if (isSubset)
                {
                    AddIndex(node, index, _multipleTimeZones);
                }
                else if (isOverlapping)
                {
                    if (level == maxLevel)
                    {
                        AddIndex(node, index, _multipleTimeZones);
                    }
                    else
                    {
                        addToChildren = true;
                        if (Unsafe.As<TimeZoneBuilderNode>(node).EnsureChildNodes())
                        {
                            Interlocked.Add(ref tree.NodeCountRef, 2);
                        }
                    }
                }
            }

            if (addToChildren)
            {
                (BBox hi, BBox lo) = box.Split(ref level);
                await creations.Writer.WriteAsync(new() { Node = node.Hi!, Index = index, Ring = ring, Box = hi, Level = level }, cancellationToken).ConfigureAwait(false);
                await creations.Writer.WriteAsync(new() { Node = node.Lo!, Index = index, Ring = ring, Box = lo, Level = level }, cancellationToken).ConfigureAwait(false);
                IncrementIndexCreations(index, 2);
                Interlocked.Add(ref totalCreations, 2);
            }

            static void AddIndex(TimeZoneNode node, short index, Dictionary<TimeZoneNode, TimeZoneIndex> multiples)
            {
                if (!Unsafe.As<TimeZoneBuilderNode>(node).IndexRef.Add(index))
                {
                    ref TimeZoneIndex indexRef = ref Unsafe.NullRef<TimeZoneIndex>();
                    lock (multiples)
                    {
                        indexRef = ref CollectionsMarshal.GetValueRefOrAddDefault(multiples, node, out _);
                    }
                    indexRef.Add(index);
                }
            }
        }

        int IncrementIndexCreations(short index, int amount) => indexCreations.AddOrUpdate(index, 1, (_, count) => count + amount);
    }

    private struct Consolidation
    {
        public TimeZoneNode Node;
        public TimeZoneIndex2 Index;
        public BBox Box;
        public int Level;
    }

    private sealed class ConsolidationComparer : IComparer<Consolidation>
    {
        public int Compare(Consolidation x, Consolidation y) => y.Level - x.Level;
    }

    /// <summary>
    /// <para>
    /// Asynchronously consolidates the time zone indices in a <see cref="TimeZoneBuilderTree"/> by traversing its nodes and resolving overlaps and exclusions.
    /// </para>
    /// <para>
    /// This method processes each node in the tree, determining the final <see cref="TimeZoneIndex"/> for each leaf node based on the included and excluded regions
    /// of the associated <see cref="TimeZoneSource"/> objects. It uses a prioritized channel to distribute work across multiple threads for parallel processing.
    /// </para>
    /// <para>
    /// For each node, the method checks which time zone indices are relevant, applies exclusion logic, and, for leaf nodes, samples a grid of points within the node's bounding box
    /// to determine the dominant time zone index. The result is written back to the node. Progress is reported via the <paramref name="progress"/> callback.
    /// </para>
    /// </summary>
    /// <param name="tree">The <see cref="TimeZoneBuilderTree"/> to consolidate.</param>
    /// <param name="progress">A progress reporter that receives an increment for each processed node.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task ConsolidateAsync(TimeZoneBuilderTree tree, IProgress<int> progress, CancellationToken cancellationToken)
    {
        Channel<Consolidation> consolidations = Channel.CreateUnboundedPrioritized<Consolidation>(new() { Comparer = new ConsolidationComparer() });
        await consolidations.Writer.WriteAsync(new Consolidation { Node = tree.Root, Box = BBox.World }, cancellationToken).ConfigureAwait(false);

        int remainingNodes = tree.NodeCount;

        Task[] threads = [.. Enumerable
            .Range(0, Environment.ProcessorCount)
            .Select(_ => Task.Run(ProcessConsolidationAsync))];

        await Task.WhenAll(threads).ConfigureAwait(false);

        async Task ProcessConsolidationAsync()
        {
            TimeZoneIndex[] indices = new TimeZoneIndex[25];
            while (await consolidations.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (consolidations.Reader.TryRead(out Consolidation consolidation))
                {
                    await ConsolidateAsync(consolidation.Node, consolidation.Index, consolidation.Box, consolidation.Level, indices).ConfigureAwait(false);
                    progress.Report(1);
                    if (Interlocked.Decrement(ref remainingNodes) == 0)
                    {
                        consolidations.Writer.Complete();
                    }
                }
            }
        }

        async ValueTask ConsolidateAsync(TimeZoneNode node, TimeZoneIndex2 index, BBox box, int level, TimeZoneIndex[] indices)
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
                Unsafe.As<TimeZoneBuilderNode>(node).IndexRef = default;
                (BBox hi, BBox lo) = box.Split(ref level);
                await consolidations.Writer.WriteAsync(new() { Node = node.Hi, Index = index, Box = hi, Level = level }, cancellationToken).ConfigureAwait(false);
                await consolidations.Writer.WriteAsync(new() { Node = node.Lo, Index = index, Box = lo, Level = level }, cancellationToken).ConfigureAwait(false);
            }
            else if (index.Second != 0)
            {
                Array.Clear(indices);
                foreach (short nodeIndex in index)
                {
                    GetArea(indices, sources[nodeIndex], box);
                }
                Unsafe.As<TimeZoneBuilderNode>(node).IndexRef = GetFinalIndex(indices);
            }
            else if (index.First != 0)
            {
                Unsafe.As<TimeZoneBuilderNode>(node).IndexRef = new TimeZoneIndex(index.First);
            }
        }

        // Retrieves all time zone indices associated with a given TimeZoneBuilderNode,
        // including both the primary and secondary indices stored directly in the node, as well as any
        // additional indices present in the internal dictionary for nodes
        // representing multiple time zones.
        static IEnumerable<short> GetMultipleTimeZones(TimeZoneNode node, Dictionary<TimeZoneNode, TimeZoneIndex> multiples)
        {
            if (node.Index.First != 0)
            {
                yield return node.Index.First;
                if (node.Index.Second != 0)
                {
                    yield return node.Index.Second;

                    if (multiples.TryGetValue(node, out TimeZoneIndex value))
                    {
                        if (value.First != 0)
                        {
                            yield return value.First;
                            if (value.Second != 0)
                            {
                                yield return value.Second;
                            }
                        }
                    }
                }
            }
        }

        // Determines whether the specified bounding box is fully excluded by any of the exclusion rings
        // in the given TimeZoneSource. This is used to check if a region represented by the box
        // parameter should be excluded from the time zone due to the presence of exclusion polygons.
        static bool IsExcluded(TimeZoneSource source, BBox box)
        {
            foreach (List<Position> ring in source.Excluded)
            {
                if (CheckRelation(CollectionsMarshal.AsSpan(ring), box).IsSubset)
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
                float longitude = Lerp(box.SouthWest.Longitude, box.NorthEast.Longitude, .1f + (float)x / 5);
                for (int y = 0; y < 5; y++)
                {
                    float latitude = Lerp(box.SouthWest.Latitude, box.NorthEast.Latitude, .1f + (float)y / 5);
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
    /// <param name="minRingDistance">
    /// The minimum distance in meters between consecutive points in a ring. Points closer than this distance will be filtered out.
    /// </param>
    /// <returns>
    /// A <see cref="List{Position}"/> containing the reduced and padded positions for efficient geometric processing.
    /// </returns>
    private static List<Position> GetReducedPositionList(ImmutableArray<Position2D<float>> ring, int minRingDistance)
    {
        List<Position> list = [.. ReduceRingPositions(ring, minRingDistance)];
        list.Insert(0, list[^1]);
        list.Add(list[1]);
        list.Add(list[2]);

        return list;

        static IEnumerable<Position> ReduceRingPositions(ImmutableArray<Position2D<float>> ring, int minRingDistance)
        {
            Position2D<float> lastPosition;
            yield return Unsafe.BitCast<Position2D<float>, Position>(lastPosition = ring[0]);
            for (int i = 1; i < ring.Length - 1; i++)
            {
                Position2D<float> position = ring[i];
                if ((Math.Abs(position.Latitude) > 70f && position != lastPosition) ||
                    Distance(lastPosition, position) > minRingDistance)
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
    /// A tuple where <c>IsSubset</c> is <see langword="true"/> if the box is fully contained within the ring,
    /// and <c>IsOverlapping</c> is <see langword="true"/> if the box is not fully outside of the ring.
    /// </returns>
    private static (bool IsSubset, bool IsOverlapping) CheckRelation(ReadOnlySpan<Position> ring, BBox box)
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

        return (
            allCornersInside && !edgeCrossing && !isOnEdge,
            allCornersInside || edgeCrossing || isOnEdge || BoxContains(ring[0]));

        bool BoxContains(Position q)
        {
            bool isCrossing = false;
            bool isOnEdge = false;
            for (RingDataWindow box = new([northWest, southWest, southEast, northEast, northWest, southWest, southEast]); box.HasMore; box.Increment())
            {
                if (Crossing(ref box, q, Outside, ref isOnEdge))
                {
                    isCrossing = !isCrossing;
                }
                if (isOnEdge)
                {
                    return true;
                }
            }

            return isCrossing;
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
    /// <remarks>See https://youtu.be/PvUK52xiFZs?t=1270 for further explanation.</remarks>
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
