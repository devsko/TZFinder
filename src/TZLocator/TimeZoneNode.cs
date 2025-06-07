// Copyright (c) devsko. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace TZLocator;

/// <summary>
/// Represents a node in a time zone tree structure, holding a <see cref="TimeZoneIndex"/> and references to child nodes.
/// </summary>
public class TimeZoneNode
{
    private TimeZoneIndex _index;

    /// <summary>
    /// Gets the <see cref="TimeZoneIndex"/> associated with this node.
    /// </summary>
    public TimeZoneIndex Index
    {
        get => _index;
        protected set => _index = value;
    }

    /// <summary>
    /// Gets a reference to the <see cref="TimeZoneIndex"/> associated with this node.
    /// This allows direct manipulation of the underlying <see cref="TimeZoneIndex"/> value.
    /// </summary>
    protected ref TimeZoneIndex IndexRef => ref _index;

    /// <summary>
    /// Gets the child node representing the higher partition.
    /// </summary>
    public TimeZoneNode? Hi { get; protected set; }

    /// <summary>
    /// Gets the child node representing the lower partition.
    /// </summary>
    public TimeZoneNode? Lo { get; protected set; }

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
