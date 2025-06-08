// Copyright (c) devsko. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using Spectre.Builder;

namespace TZFinder.Builder.Steps;

/// <summary>
/// Represents a conversion step that downloads the time zone boundary file from the timezone-boundary-builder GitHub repository.
/// </summary>
public partial class DownloadSource : ConversionStep
{
    /// <inheritdoc/>
    public override string Name => "Download time zone file";

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<IResource> GetOutputsAsync(BuilderContext context)
    {
        yield return ((Context)context).SourceFile;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(BuilderContext builderContext, DateTime timestamp)
    {
        Context context = (Context)builderContext;

        FileResource sourceFile = context.SourceFile;

        using HttpResponseMessage response = await context.Client.GetAsync(
            $"https://github.com/{Context.SourceRepository}/releases/download/{context.SourceRelease}/{Context.SourceFileName}.zip",
            HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
        {
            context.Fail(this, $"Failed with status code {response.StatusCode}");
        }
        else
        {
            context.SetTotal(this, response.Content.Headers.ContentLength ?? 0);

            await using ZipArchive zip = await ZipArchive.CreateAsync(
                new ProgressStream(
                    await response.Content.ReadAsStreamAsync(),
                    bytes => context.IncrementProgress(this, bytes)),
                ZipArchiveMode.Read, leaveOpen: false, entryNameEncoding: null);

            ZipArchiveEntry? entry = zip.Entries[0] ?? throw new InvalidOperationException();

            await using PreliminaryFileStream fileStream = sourceFile.OpenCreate(0, timestamp);
            await using Stream entryStream = await entry.OpenAsync();

            await entryStream.CopyToAsync(fileStream);

            fileStream.Persist();
        }
    }
}
