// Copyright (c) devsko. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace TZFinder.Builder;

/// <summary>
/// Represents a fixed-size collection of up to 8 unique time zone indices.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TimeZoneIndex8"/> is a value type that stores up to 8 <c>short</c> values, each representing a time zone index.
/// The structure ensures that each index is unique and provides methods to add new indices and check if the collection is empty.
/// </para>
/// <para>
/// The structure is implemented using the <see cref="System.Runtime.CompilerServices.InlineArrayAttribute"/> to provide efficient, stack-allocated storage.
/// </para>
/// </remarks>
[InlineArray(8)]
public struct TimeZoneIndex8
{
    private short _value;

    /// <summary>
    /// Gets a value indicating whether the index is empty.
    /// </summary>
    public readonly bool IsEmpty => this[0] == 0;


    /// <summary>
    /// Adds a time zone index to the structure.
    /// </summary>
    /// <param name="index">The time zone index to add.</param>
    /// <exception cref="InvalidOperationException">Thrown when the structure is full.</exception>
    public void Add(short index)
    {
        for (int i = 0; i < 8; i++)
        {
            if (this[i] == index) return;
            if (this[i] == 0)
            {
                this[i] = index;
                return;
            }
        }
        throw new InvalidOperationException("TimeZoneIndex8 is full.");
    }
}
