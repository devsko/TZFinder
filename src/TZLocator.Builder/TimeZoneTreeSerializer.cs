// Copyright (c) devsko. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace TZLocator.Builder;

/// <summary>
/// Provides serializing <see cref="TimeZoneBuilderTree"/> instances to a binary stream.
/// </summary>
public static class TimeZoneTreeSerializer
{
    /// <summary>
    /// Serializes the specified <see cref="TimeZoneTree"/> to the given <see cref="Stream"/>.
    /// </summary>
    /// <param name="tree">The <see cref="TimeZoneTree"/> to serialize.</param>
    /// <param name="stream">The <see cref="Stream"/> to write the serialized data to.</param>
    /// <param name="progress">An optional progress reporter for the number of nodes written.</param>
    public static void Serialize(TimeZoneBuilderTree tree, Stream stream, IProgress<int>? progress = null)
    {
        using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write((short)tree.TimeZoneNames.Length);
        foreach (string timeZone in tree.TimeZoneNames)
        {
            writer.Write(timeZone);
        }

        int written = 0;
        Write(tree.Root);

        void Write(TimeZoneBuilderNode node)
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
