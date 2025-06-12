// Copyright (c) devsko. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Runtime.CompilerServices;

namespace TZFinder;

/// <summary>
/// Represents an index for time zones, supporting up to two short values.
/// </summary>
public struct TimeZoneIndex : IEquatable<TimeZoneIndex>, IEnumerable<short>
{
    private uint _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneIndex"/> struct with the specified index.
    /// </summary>
    /// <param name="index">The time zone index to add.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is 0.</exception>
    public TimeZoneIndex(short index)
    {
        Add(index);
    }

    /// <summary>
    /// Gets a value indicating whether the index is empty.
    /// </summary>
    public readonly bool IsEmpty => _value == 0;

    /// <summary>
    /// Gets the first time zone index.
    /// </summary>
    public readonly short First => Unsafe.Add(ref Unsafe.As<uint, short>(ref Unsafe.AsRef(in _value)), 0);

    /// <summary>
    /// Gets the second time zone index.
    /// </summary>
    public readonly short Second => Unsafe.Add(ref Unsafe.As<uint, short>(ref Unsafe.AsRef(in _value)), 1);

    /// <summary>
    /// Adds a time zone index to the structure.
    /// </summary>
    /// <param name="index">The time zone index to add.</param>
    /// <returns>
    /// <see langword="true"/> if the index was added or already present; <see langword="false"/> if the structure is full and the index is not present.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is less than or equal 0.</exception>
    public bool Add(short index)
    {
        if (index <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (_value == 0)
        {
            Unsafe.Add(ref Unsafe.As<uint, short>(ref _value), 0) = index;
        }
        else if (First == index || Second == index)
        {
            return true;
        }
        else if (Second != 0)
        {
            return false;
        }
        else
        {
            Unsafe.Add(ref Unsafe.As<uint, short>(ref _value), 1) = index;
        }

        return true;
    }

    /// <summary>
    /// Determines whether the specified <see cref="TimeZoneIndex"/> is equal to the current <see cref="TimeZoneIndex"/>.
    /// </summary>
    /// <param name="other">The <see cref="TimeZoneIndex"/> to compare with the current <see cref="TimeZoneIndex"/>.</param>
    /// <returns><see langword="true"/> if the specified <see cref="TimeZoneIndex"/> is equal to the current <see cref="TimeZoneIndex"/>; otherwise, <see langword="false"/>.</returns>
    public readonly bool Equals(TimeZoneIndex other) => _value.Equals(other._value);

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is TimeZoneIndex index && Equals(index);

    /// <inheritdoc/>
    public override readonly int GetHashCode() => _value.GetHashCode();

    /// <summary>
    /// Determines whether two <see cref="TimeZoneIndex"/> instances are equal.
    /// </summary>
    /// <param name="left">The first <see cref="TimeZoneIndex"/> to compare.</param>
    /// <param name="right">The second <see cref="TimeZoneIndex"/> to compare.</param>
    /// <returns>
    /// <see langword="true"/> if the two <see cref="TimeZoneIndex"/> instances are equal; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool operator ==(TimeZoneIndex left, TimeZoneIndex right) => left.Equals(right);

    /// <summary>
    /// Determines whether two <see cref="TimeZoneIndex"/> instances are not equal.
    /// </summary>
    /// <param name="left">The first <see cref="TimeZoneIndex"/> to compare.</param>
    /// <param name="right">The second <see cref="TimeZoneIndex"/> to compare.</param>
    /// <returns>
    /// <see langword="true"/> if the two <see cref="TimeZoneIndex"/> instances are not equal; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool operator !=(TimeZoneIndex left, TimeZoneIndex right) => !left.Equals(right);

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
            }
        }
    }

    /// <inheritdoc/>
    readonly IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<short>)this).GetEnumerator();
}
