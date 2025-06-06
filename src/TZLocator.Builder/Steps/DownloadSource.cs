// Copyright (c) devsko. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json;
using Spectre.Builder;

namespace TZLocator.Builder.Steps;

/// <summary>
/// Represents a conversion step that downloads the time zone boundary file from the timezone-boundary-builder GitHub repository.
/// </summary>
public partial class DownloadSource(string path, string release = "latest") : ConversionStep
{
    private const string Repository = "evansiroky/timezone-boundary-builder";

    private readonly HttpClient _client = CreateClient();

    private static HttpClient CreateClient()
    {
        HttpClient client = new();
        client.DefaultRequestHeaders.Add("User-Agent", "Nbrounter.Map");

        return client;
    }

    /// <inheritdoc/>
    public override string Name => "Download time zone file";

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<IResource> GetOutputsAsync(BuilderContext context)
    {
        if (release == "latest")
        {
            JsonElement latestRelease = await _client.GetFromJsonAsync<JsonElement>($"https://api.github.com/repos/{Repository}/releases/latest");
            release = latestRelease.GetProperty("tag_name").GetString()!;
        }

        yield return ((Context)context).InitSourceFile(path.Replace("{release}", release));
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(BuilderContext context, DateTime timestamp)
    {
        FileResource sourceFile = ((Context)context).SourceFile;

        using HttpResponseMessage response = await _client.GetAsync(
            $"https://github.com/{Repository}/releases/download/{release}/{Path.GetFileName(path.Replace("{release}", ""))}.zip",
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
            await entry.ExtractToFileAsync(sourceFile.Path);
        }
    }
}
