// Copyright (c) devsko. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace TZFinder.Builder;

/// <summary>
/// Represents a source of time zone data, including included and excluded geographic regions.
/// </summary>
public sealed class TimeZoneSource
{
    /// <summary>
    /// Gets the index of the time zone source.
    /// </summary>
    public required short Index { get; init; }

    /// <summary>
    /// Gets the identifier of the time zone source.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the array of lists of included regions for the time zone.
    /// </summary>
    public required List<Position>[] Included { get; init; }

    /// <summary>
    /// Gets the array of lists of excluded regions for the time zone.
    /// </summary>
    public required List<Position>[] Excluded { get; init; }
}
