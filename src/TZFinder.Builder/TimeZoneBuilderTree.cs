// Copyright (c) devsko. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace TZFinder.Builder;

/// <summary>
/// Represents a mutable tree structure for building or modifying time zone trees.
/// Inherits from <see cref="TimeZoneTree"/> and provides access to a mutable root node of type <see cref="TimeZoneBuilderNode"/>.
/// </summary>
/// <remarks>
/// This class is intended for scenarios where the time zone tree needs to be constructed or altered before being finalized.
/// </remarks>
public sealed class TimeZoneBuilderTree(string[] timeZoneIds) : TimeZoneTree(timeZoneIds, new TimeZoneBuilderNode(default))
{
    /// <summary>
    /// Gets the root node of the builder tree.
    /// </summary>
    public new TimeZoneNode Root => base.Root;

    /// <summary>
    /// Gets a reference to the internal node count of the builder tree.
    /// This property allows direct manipulation of the underlying node count value.
    /// </summary>
    public new ref int NodeCountRef => ref base.NodeCountRef;

    /// <summary>
    /// Serializes the <see cref="TimeZoneBuilderTree"/> to the given <see cref="Stream"/>.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> to write the serialized data to.</param>
    /// <param name="progress">An optional progress reporter for the number of nodes written.</param>
    public void Serialize(Stream stream, IProgress<int>? progress = null)
    {
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write((short)TimeZoneIds.Length);
        foreach (string timeZone in TimeZoneIds)
        {
            writer.Write(timeZone);
        }

        int written = 0;
        Write(Root);

        void Write(TimeZoneNode node)
        {
            if (node.Index.Second != 0)
            {
                writer.Write((short)~node.Index.First);
                writer.Write(node.Index.Second);
            }
            else
            {
                writer.Write(node.Index.First);
            }
            if (node.Hi is null || node.Lo is null)
            {
                writer.Write((short)-1);
            }
            else
            {
                Write(node.Hi);
                Write(node.Lo);
            }
            progress?.Report(++written);
        }
    }
}
