// Copyright (c) devsko. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace TZLocator.Builder;

/// <summary>
/// Represents a node in a time zone tree structure, holding a <see cref="TimeZoneIndex"/> and references to child nodes.
/// </summary>
public sealed class TimeZoneBuilderNode
{
    private TimeZoneIndex _index;

    /// <summary>
    /// Gets or sets the child node representing the higher partition.
    /// </summary>
    public TimeZoneBuilderNode? Hi { get; internal set; }

    /// <summary>
    /// Gets or sets the child node representing the lower partition.
    /// </summary>
    public TimeZoneBuilderNode? Lo { get; internal set; }

    /// <summary>
    /// Gets a reference to the <see cref="TimeZoneIndex"/> associated with this node.
    /// </summary>
    public ref TimeZoneIndex Index => ref _index;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneBuilderNode"/> class with the specified index.
    /// </summary>
    /// <param name="index">The <see cref="TimeZoneIndex"/> to associate with this node.</param>
    public TimeZoneBuilderNode(TimeZoneIndex index)
    {
        _index = index;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneBuilderNode"/> class with the specified index and child nodes.
    /// </summary>
    /// <param name="index">The <see cref="TimeZoneIndex"/> to associate with this node.</param>
    /// <param name="hi">The child node representing the higher partition.</param>
    /// <param name="lo">The child node representing the lower partition.</param>
    internal TimeZoneBuilderNode(TimeZoneIndex index, TimeZoneBuilderNode? hi, TimeZoneBuilderNode? lo)
    {
        _index = index;
        Hi = hi;
        Lo = lo;
    }
}
