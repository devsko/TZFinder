using System.Net.Http.Json;
using System.Text.Json;
using Spectre.Builder;
using TZFinder.Builder.Steps;

namespace TZFinder.Builder;

/// <summary>
/// Executes the time zone builder process with the specified configuration options.
/// </summary>
public static class Builder
{
    /// <summary>
    /// Runs the builder.
    /// </summary>
    /// <param name="maxLevel">-l, Maximum precision level</param>
    /// <param name="minRingDistance">-d, Minimum distance between 2 positions in rings</param>
    /// <param name="release">-r, Release tag of Timezone Boundary Builder</param>
    /// <param name="includeOceans">-o, Include etcetera time zones</param>
    /// <param name="cancellationToken">The cancellationToken</param>
    /// <returns></returns>
    public static async Task RunAsync(
        int maxLevel = 25,
        int minRingDistance = 500,
        string release = "latest",
        bool includeOceans = false,
        CancellationToken cancellationToken = default)
    {
        HttpClient client = new();
        client.DefaultRequestHeaders.Add("User-Agent", "TZFinder");

        if (release == "latest")
        {
            JsonElement latestRelease = await client.GetFromJsonAsync<JsonElement>($"https://api.github.com/repos/{Context.SourceRepository}/releases/latest", cancellationToken);
            release = latestRelease.GetProperty("tag_name").GetString()!;
        }

        Context context = new(client, release, includeOceans, maxLevel, minRingDistance);

        await context.RunAsync(
            Step.Sequential("Create time zone data",
            [
                new DownloadSource(),
                new LoadSource(),
                new CreateTree(),
                new ConsolidateTree(),
                new SerializeTree(),
            ]),
            [
                new MemoryInfo(),
                new GCTimeInfo(),
            ], cancellationToken);
    }
}
