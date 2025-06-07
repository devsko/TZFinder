// Copyright (c) devsko. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace TZLocator;

/// <summary>
/// Represents a tree structure for efficiently locating time zones based on geographic coordinates.
/// </summary>
public class TimeZoneTree
{
    private readonly TimeZoneNode _root;
    private readonly string[] _timeZoneNames;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneTree"/> class with the specified time zone names and root node.
    /// </summary>
    /// <param name="timeZoneNames">An array of time zone names.</param>
    /// <param name="root">The root node of the time zone tree.</param>
    protected internal TimeZoneTree(string[] timeZoneNames, TimeZoneNode root)
    {
        _timeZoneNames = timeZoneNames;
        _root = root;
    }

    /// <summary>
    /// Gets the array of time zone names.
    /// </summary>
    protected internal string[] TimeZoneNames => _timeZoneNames;

    /// <summary>
    /// Gets the root node of the time zone tree.
    /// </summary>
    protected TimeZoneNode Root => _root;

    /// <summary>
    /// Finds the time zone index, bounding box, and tree level for the specified geographic coordinates.
    /// </summary>
    /// <param name="longitude">The longitude in degrees.</param>
    /// <param name="latitude">The latitude in degrees.</param>
    /// <returns>
    /// A tuple containing the <see cref="TimeZoneIndex"/>, <see cref="BBox"/>, and the tree level.
    /// </returns>
    public (TimeZoneIndex Index, BBox Box, int Level) Get(float longitude, float latitude)
    {
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

            return Get(node, longitude, latitude, box, level + 1);
        }
    }

    /// <summary>
    /// Gets the time zone names corresponding to the specified <see cref="TimeZoneIndex"/>.
    /// </summary>
    /// <param name="index">The time zone index.</param>
    /// <returns>
    /// An enumerable collection of up to 2 time zone names.
    /// </returns>
    public IEnumerable<string> GetNames(TimeZoneIndex index)
    {
        if (!index.IsEmpty)
        {
            yield return _timeZoneNames[index.First - 1];
            if (index.Second != 0)
            {
                yield return _timeZoneNames[index.Second - 1];
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
