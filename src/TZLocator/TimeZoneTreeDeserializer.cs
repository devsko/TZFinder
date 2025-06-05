// Copyright (c) devsko. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;

namespace TZLocator;

/// <summary>
/// Provides deserializing <see cref="TimeZoneTree"/> instances from a binary stream.
/// </summary>
public static class TimeZoneTreeDeserializer
{
    /// <summary>
    /// Deserializes a <see cref="TimeZoneTree"/> from the specified <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> to read the serialized data from.</param>
    /// <returns>The deserialized <see cref="TimeZoneTree"/> instance.</returns>
    public static TimeZoneTree Deserialize(Stream stream)
    {
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: false);

        short capacity = reader.ReadInt16();
        string[] timeZones = new string[capacity];
        for (int i = 0; i < capacity; i++)
        {
            timeZones[i] = reader.ReadString();
        }

        return new TimeZoneTree(timeZones, Read(reader.ReadInt16()));

        TimeZoneNode Read(short first)
        {
            Debug.Assert(first is not -1);

            TimeZoneIndex index = first switch
            {
                < 0 => new TimeZoneIndex((short)~first) { reader.ReadInt16() },
                not 0 => new TimeZoneIndex(first),
                0 => default
            };

            short hiIndex = reader.ReadInt16();

            return hiIndex is -1
                ? new TimeZoneNode(index, null, null)
                : new TimeZoneNode(index, Read(hiIndex), Read(reader.ReadInt16()));
        }
    }
}
