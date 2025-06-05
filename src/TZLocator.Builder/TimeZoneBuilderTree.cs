// Copyright (c) devsko. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace TZLocator;

/// <summary>
/// Represents a tree structure for efficiently locating time zones based on geographic coordinates.
/// </summary>
public class TimeZoneBuilderTree
{
    private readonly TimeZoneBuilderNode _root;
    internal readonly string[] _timeZoneNames;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneBuilderTree"/> class with the specified time zone names.
    /// </summary>
    /// <param name="timeZoneNames">An array of time zone names.</param>
    public TimeZoneBuilderTree(string[] timeZoneNames)
    {
        _timeZoneNames = timeZoneNames;
        _root = new(default);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneBuilderTree"/> class with the specified time zone names and root node.
    /// </summary>
    /// <param name="timeZoneNames">An array of time zone names.</param>
    /// <param name="root">The root node of the time zone tree.</param>
    internal TimeZoneBuilderTree(string[] timeZoneNames, TimeZoneBuilderNode root)
    {
        _timeZoneNames = timeZoneNames;
        _root = root;
    }

    /// <summary>
    /// Gets the array of time zone names.
    /// </summary>
    internal string[] TimeZoneNames => _timeZoneNames;

    /// <summary>
    /// Gets the root node of the time zone tree.
    /// </summary>
    internal TimeZoneBuilderNode Root => _root;
}
