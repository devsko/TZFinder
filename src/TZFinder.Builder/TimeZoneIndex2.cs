// Copyright (c) devsko. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Runtime.CompilerServices;

namespace TZFinder.Builder;

/// <summary>
/// Represents an index for time zones, supporting up to four short values.
/// </summary>
public struct TimeZoneIndex2 : IEnumerable<short>
{
    private ulong _value;

    /// <summary>
    /// Gets a value indicating whether the index is empty.
    /// </summary>
    public readonly bool IsEmpty => _value == 0;

    /// <summary>
    /// Gets the first time zone index.
    /// </summary>
    public readonly short First => Unsafe.Add(ref Unsafe.As<ulong, short>(ref Unsafe.AsRef(in _value)), 0);

    /// <summary>
    /// Gets the second time zone index.
    /// </summary>
    public readonly short Second => Unsafe.Add(ref Unsafe.As<ulong, short>(ref Unsafe.AsRef(in _value)), 1);

    /// <summary>
    /// Gets the third time zone index.
    /// </summary>
    public readonly short Third => Unsafe.Add(ref Unsafe.As<ulong, short>(ref Unsafe.AsRef(in _value)), 2);

    /// <summary>
    /// Gets the fourth time zone index.
    /// </summary>
    public readonly short Fourth => Unsafe.Add(ref Unsafe.As<ulong, short>(ref Unsafe.AsRef(in _value)), 3);

    /// <summary>
    /// Adds a time zone index to the structure.
    /// </summary>
    /// <param name="index">The time zone index to add.</param>
    /// <exception cref="InvalidOperationException">Thrown when the structure is full.</exception>
    public void Add(short index)
    {
        if (First == index || Second == index || Third == index || Fourth == index) return;
        else if (First == 0) Unsafe.Add(ref Unsafe.As<ulong, short>(ref _value), 0) = index;
        else if (Second == 0) Unsafe.Add(ref Unsafe.As<ulong, short>(ref _value), 1) = index;
        else if (Third == 0) Unsafe.Add(ref Unsafe.As<ulong, short>(ref _value), 2) = index;
        else if (Fourth == 0) Unsafe.Add(ref Unsafe.As<ulong, short>(ref _value), 3) = index;
        else throw new InvalidOperationException();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the time zone indices.
    /// </summary>
    /// <returns>An enumerator for the time zone indices.</returns>
    readonly IEnumerator<short> IEnumerable<short>.GetEnumerator()
    {
        if (First != 0)
        {
            yield return First;
            if (Second != 0)
            {
                yield return Second;
                if (Third != 0)
                {
                    yield return Third;
                    if (Fourth != 0)
                    {
                        yield return Fourth;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the time zone indices.
    /// </summary>
    /// <returns>An enumerator for the time zone indices.</returns>
    readonly IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<short>)this).GetEnumerator();
}
