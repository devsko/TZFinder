using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json;
using Spectre.Builder;

namespace TZFinder.Builder;

/// <summary>
/// Provides a context for managing file and resource dependencies during the time zone builder process.
/// </summary>
public class Context : BuilderContext
{
    /// <summary>
    /// The GitHub repository for the timezone boundary builder.
    /// </summary>
    public const string TimeZoneRepository = "evansiroky/timezone-boundary-builder";

    /// <summary>
    /// The file name for the time zone GeoJSON file.
    /// </summary>
    public const string TimeZoneFileName = "timezones.geojson";

    /// <summary>
    /// Gets the <see cref="HttpClient"/> used for HTTP requests.
    /// </summary>
    public HttpClient Client { get; } = CreateClient();

    /// <summary>
    /// Gets the tag name of the latest time zone release.
    /// </summary>
    public string? TimeZoneRelease { get; private set; }

    /// <summary>
    /// Gets or sets the <see cref="TimeZoneContext"/> for the current run.
    /// </summary>
    public TimeZoneContext? TimeZoneContext { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="TimeZoneBuilderTree"/> for the current run.
    /// </summary>
    public TimeZoneBuilderTree? TimeZoneTree { get; set; }

    /// <summary>
    /// Gets or sets the number of nodes in the time zone builder tree.
    /// </summary>
    public int NodeCount { get; set; }

    /// <summary>
    /// Gets the <see cref="FileResource"/> representing the source file.
    /// </summary>
    [AllowNull]
    public FileResource SourceFile { get; private set; }

    /// <summary>
    /// Gets the <see cref="FileResource"/> representing the time zone file.
    /// </summary>
    [AllowNull]
    public FileResource TimeZoneFile { get; private set; }

    /// <summary>
    /// Gets the <see cref="CalculationResource"/> representing the time zone calculation resource.
    /// </summary>
    [AllowNull]
    public CalculationResource TimeZoneCalculation { get; private set; }

    /// <inheritdoc/>
    protected override async Task InitializeAsync()
    {
        TimeZoneRelease = await GetLatestReleaseAsync();

        string baseAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TZFinder");
        Directory.CreateDirectory(baseAppDataPath);

        SourceFile = new FileResource(Path.Combine(baseAppDataPath, $"{TimeZoneRelease}{TimeZoneFileName}"));
        TimeZoneFile = new FileResource(Path.Combine(baseAppDataPath, "TimeZones.tree"));
        TimeZoneCalculation = new CalculationResource(TimeZoneFile);

        async Task<string> GetLatestReleaseAsync()
        {
            JsonElement latestRelease = await Client.GetFromJsonAsync<JsonElement>($"https://api.github.com/repos/{TimeZoneRepository}/releases/latest");

            return latestRelease.GetProperty("tag_name").GetString()!;
        }
    }

    private static HttpClient CreateClient()
    {
        HttpClient client = new();
        client.DefaultRequestHeaders.Add("User-Agent", "Nbrounter.Map");

        return client;
    }
}
