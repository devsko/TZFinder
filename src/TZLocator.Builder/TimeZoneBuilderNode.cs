// Copyright (c) devsko. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace TZLocator.Builder;

/// <summary>
/// Represents a mutable node in a time zone tree structure for building or modifying the tree.
/// Inherits from <see cref="TimeZoneNode"/> and provides additional mutability for the <see cref="TimeZoneIndex"/>.
/// </summary>
public sealed class TimeZoneBuilderNode(TimeZoneIndex index) : TimeZoneNode(index)
{
    /// <summary>
    /// Gets a reference to the <see cref="TimeZoneIndex"/> associated with this node.
    /// This allows direct manipulation of the underlying <see cref="TimeZoneIndex"/> value.
    /// </summary>
    public new ref TimeZoneIndex IndexRef => ref base.IndexRef;

    /// <summary>
    /// Ensures that both child nodes (<see cref="TimeZoneNode.Hi"/> and <see cref="TimeZoneNode.Lo"/>) are initialized.
    /// If either child node is <see langword="null"/>, both are set to new <see cref="TimeZoneBuilderNode"/> instances
    /// initialized with the current <see cref="TimeZoneIndex"/>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if either child node was <see langword="null"/> and has been initialized; otherwise, <see langword="false"/>.
    /// </returns>
    public bool EnsureChildNodes()
    {
        if (Hi is null || Lo is null)
        {
            Hi = new TimeZoneBuilderNode(Index);
            Lo = new TimeZoneBuilderNode(Index);
            return true;
        }

        return false;
    }
}
