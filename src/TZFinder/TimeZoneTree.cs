// Copyright (c) devsko. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;

namespace TZFinder;

/// <summary>
/// Represents a tree structure for efficiently locating time zones based on geographic coordinates.
/// </summary>
public class TimeZoneTree
{
    private readonly TimeZoneNode _root;
    private readonly string[] _timeZoneIds;
    private int _nodeCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneTree"/> class with the specified time zone identifierss and root node.
    /// </summary>
    /// <param name="timeZoneIds">An array of time zone identifiers.</param>
    /// <param name="root">The root node of the time zone tree.</param>
    protected internal TimeZoneTree(string[] timeZoneIds, TimeZoneNode root)
    {
        _timeZoneIds = timeZoneIds;
        _root = root;
        _nodeCount = 1;
    }

    /// <summary>
    /// Gets the array of time zone identifiers.
    /// </summary>
    protected internal string[] TimeZoneIds => _timeZoneIds;

    /// <summary>
    /// Gets the root node of the time zone tree.
    /// </summary>
    protected TimeZoneNode Root => _root;

    /// <summary>
    /// Gets the number of nodes in the time zone tree.
    /// </summary>
    public int NodeCount => _nodeCount;

    /// <summary>
    /// Gets a reference to the internal node count of the time zone tree.
    /// This property allows direct manipulation of the underlying node count value.
    /// </summary>
    protected ref int NodeCountRef => ref _nodeCount;

    /// <summary>
    /// Deserializes a <see cref="TimeZoneTree"/> from the specified <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> to read the serialized data from.</param>
    /// <returns>The deserialized <see cref="TimeZoneTree"/> instance.</returns>
    public static TimeZoneTree Deserialize(Stream stream)
    {
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: false);

        short capacity = reader.ReadInt16();
        string[] timeZones = new string[capacity];
        for (int i = 0; i < capacity; i++)
        {
            timeZones[i] = reader.ReadString();
        }

        return new TimeZoneTree(timeZones, Read(reader.ReadInt16()));

        TimeZoneNode Read(short first)
        {
            Debug.Assert(first is not -1);

            TimeZoneIndex index = first switch
            {
                < 0 => new TimeZoneIndex((short)~first) { reader.ReadInt16() },
                > 0 => new TimeZoneIndex(first),
                0 => default
            };

            short hiIndex = reader.ReadInt16();

            return hiIndex is -1
                ? new TimeZoneNode(index, null, null)
                : new TimeZoneNode(index, Read(hiIndex), Read(reader.ReadInt16()));
        }
    }

    /// <summary>
    /// Finds the time zone index, bounding box, and tree level for the specified geographic coordinates.
    /// </summary>
    /// <param name="longitude">The longitude in degrees, between -180 and 180.</param>
    /// <param name="latitude">The latitude in degrees, between -90 and 90.</param>
    /// <returns>
    /// A tuple containing the <see cref="TimeZoneIndex"/> for the location, the <see cref="BBox"/> bounding box of the leaf node, and the tree level.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="longitude"/> or <paramref name="latitude"/> is out of range.</exception>
    public (TimeZoneIndex Index, BBox Box, int Level) Get(float longitude, float latitude)
    {
#if NET
        ArgumentOutOfRangeException.ThrowIfLessThan(longitude, -180f);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(longitude, 180f);
        ArgumentOutOfRangeException.ThrowIfLessThan(latitude, -90f);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(latitude, 90f);
#else
        if (longitude is < -180f or > 180f)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude));
        }
        if (latitude is < -90f or > 90f)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude));
        }
#endif

        return Get(_root, longitude, latitude, BBox.World, 0);

        static (TimeZoneIndex Index, BBox Box, int Level) Get(TimeZoneNode node, float longitude, float latitude, BBox box, int level)
        {
            if (node.Lo is null || node.Hi is null)
            {
                return (node.Index, box, level);
            }

            (BBox hi, BBox lo) = box.Split(ref level);

            (node, box) = longitude >= hi.SouthWest.Longitude && latitude >= hi.SouthWest.Latitude
                ? (node.Hi, hi)
                : (node.Lo, lo);

            return Get(node, longitude, latitude, box, level);
        }
    }

    /// <summary>
    /// Gets the time zone identifiers corresponding to the specified <see cref="TimeZoneIndex"/>.
    /// </summary>
    /// <param name="index">The time zone index.</param>
    /// <returns>
    /// An enumerable collection of up to 2 time zone identifiers.
    /// </returns>
    public IEnumerable<string> GetIds(TimeZoneIndex index)
    {
        if (!index.IsEmpty)
        {
            yield return _timeZoneIds[index.First - 1];
            if (index.Second != 0)
            {
                yield return _timeZoneIds[index.Second - 1];
            }
        }
    }

    /// <summary>
    /// Traverses the tree and invokes the specified action for each leaf node.
    /// </summary>
    /// <param name="action">
    /// The action to invoke, which receives the <see cref="TimeZoneIndex"/> and <see cref="BBox"/> of each leaf node.
    /// </param>
    public void Traverse(Action<TimeZoneIndex, BBox> action)
    {
        Traverse(action, _root, BBox.World, 0);

        static void Traverse(Action<TimeZoneIndex, BBox> action, TimeZoneNode node, BBox box, int level)
        {
            if (node.Hi is not null && node.Lo is not null)
            {
                (BBox hi, BBox lo) = box.Split(ref level);
                Traverse(action, node.Hi, hi, level);
                Traverse(action, node.Lo, lo, level);
            }
            else
            {
                action(node.Index, box);
            }
        }
    }
}
