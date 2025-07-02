// Copyright (c) devsko. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using Spectre.Builder;

namespace TZFinder.Builder.Steps;

/// <summary>
/// Represents a conversion step that downloads the time zone boundary file from the timezone-boundary-builder GitHub repository.
/// </summary>
public partial class DownloadSource(Context context) : ConversionStep<Context>(
    inputs: [context.DownloadSource],
    outputs: [context.SourceFile])
{
    /// <inheritdoc/>
    public override string Name => "Download time zone file";

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(Context context, DateTime timestamp, CancellationToken cancellationToken)
    {
        context.SetTotal(this, context.DownloadSource.Length ?? 0);

        await using ZipArchive zip = await ZipArchive.CreateAsync(
            await context.DownloadSource.DownloadAsync(bytes => context.IncrementProgress(this, bytes), cancellationToken),
            ZipArchiveMode.Read,
            leaveOpen: false,
            entryNameEncoding: null,
            cancellationToken);
        await using Stream entryStream = await (zip.Entries[0] ?? throw new InvalidOperationException()).OpenAsync(cancellationToken);
        await using PreliminaryFileStream fileStream = context.SourceFile.OpenCreate(0, timestamp);

        await entryStream.CopyToAsync(fileStream, cancellationToken);

        fileStream.Persist();
    }
}
