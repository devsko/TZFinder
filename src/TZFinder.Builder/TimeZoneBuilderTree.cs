// Copyright (c) devsko. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace TZFinder.Builder;

/// <summary>
/// Represents a mutable tree structure for building or modifying time zone trees.
/// Inherits from <see cref="TimeZoneTree"/> and provides access to a mutable root node of type <see cref="TimeZoneBuilderNode"/>.
/// </summary>
/// <remarks>
/// This class is intended for scenarios where the time zone tree needs to be constructed or altered before being finalized.
/// </remarks>
public sealed class TimeZoneBuilderTree(string[] timeZoneNames) : TimeZoneTree(timeZoneNames, new TimeZoneBuilderNode(default))
{
    /// <summary>
    /// Gets the array of time zone names associated with this builder tree.
    /// </summary>
    internal new string[] TimeZoneNames => base.TimeZoneNames;

    /// <summary>
    /// Gets the mutable root node of the builder tree as a <see cref="TimeZoneBuilderNode"/>.
    /// </summary>
    internal new TimeZoneBuilderNode Root => Unsafe.As<TimeZoneBuilderNode>(base.Root);
}
