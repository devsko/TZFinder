// Copyright (c) devsko. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace TZLocator;

/// <summary>
/// Represents a node in a time zone tree structure, holding a <see cref="TimeZoneIndex"/> and references to child nodes.
/// </summary>
public sealed class TimeZoneNode
{
    /// <summary>
    /// Gets the <see cref="TimeZoneIndex"/> associated with this node.
    /// </summary>
    public TimeZoneIndex Index { get; }

    /// <summary>
    /// Gets or sets the child node representing the higher partition.
    /// </summary>
    public TimeZoneNode? Hi { get; }

    /// <summary>
    /// Gets or sets the child node representing the lower partition.
    /// </summary>
    public TimeZoneNode? Lo { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneNode"/> class with the specified index.
    /// </summary>
    /// <param name="index">The <see cref="TimeZoneIndex"/> to associate with this node.</param>
    public TimeZoneNode(TimeZoneIndex index)
    {
        Index = index;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneNode"/> class with the specified index and child nodes.
    /// </summary>
    /// <param name="index">The <see cref="TimeZoneIndex"/> to associate with this node.</param>
    /// <param name="hi">The child node representing the higher partition.</param>
    /// <param name="lo">The child node representing the lower partition.</param>
    internal TimeZoneNode(TimeZoneIndex index, TimeZoneNode? hi, TimeZoneNode? lo)
    {
        Index = index;
        Hi = hi;
        Lo = lo;
    }
}
