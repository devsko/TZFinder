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
    public const string SourceRepository = "evansiroky/timezone-boundary-builder";

    /// <summary>
    /// Gets the <see cref="HttpClient"/> used for HTTP requests.
    /// </summary>
    public HttpClient Client { get; }

    /// <summary>
    /// Gets the tag name of the latest timezone boundary builder release.
    /// </summary>
    public string SourceRelease { get; }

    /// <summary>
    /// Gets the name of the source file used for time zone data.
    /// </summary>
    public string SourceFileName { get; }

    /// <summary>
    /// Gets the maximum depth level for the time zone builder tree.
    /// </summary>
    public int MaxLevel { get; }

    /// <summary>
    /// Gets the minimum distance in meters between consecutive points in a ring.
    /// </summary>
    public int MinRingDistance { get; }

    /// <summary>
    /// Gets the <see cref="FileResource"/> representing the source file.
    /// </summary>
    public FileResource SourceFile { get; }

    /// <summary>
    /// Gets the <see cref="FileResource"/> representing the time zone file.
    /// </summary>
    public FileResource TimeZoneDataFile { get; }

    /// <summary>
    /// Gets the <see cref="CalculationResource"/> representing the time zone calculation resource.
    /// </summary>
    public CalculationResource TimeZoneCalculation { get; }

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
    /// Initializes a new instance of the <see cref="Context"/> class with the specified parameters.
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> used for HTTP requests.</param>
    /// <param name="sourceRelease">The tag name of the latest timezone boundary builder release.</param>
    /// <param name="includeOceans">If <see langword="true"/>, use the time zone data file without oceans; otherwise, include oceans.</param>
    /// <param name="maxLevel">The maximum depth level for the time zone builder tree.</param>
    /// <param name="minRingDistance">The minimum distance in meters between consecutive points in a ring.</param>
    public Context(HttpClient client, string sourceRelease, bool includeOceans, int maxLevel, int minRingDistance)
    {
        Client = client;
        SourceRelease = sourceRelease;
        MaxLevel = maxLevel;
        MinRingDistance = minRingDistance;
        SourceFileName = includeOceans ? "timezones-with-oceans.geojson" : "timezones.geojson";

        string baseAppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TZFinder");
        Directory.CreateDirectory(baseAppDataPath);

        SourceFile = new FileResource(Path.Combine(baseAppDataPath, $"{SourceRelease}_{SourceFileName}"));
        TimeZoneDataFile = new FileResource(Path.Combine(baseAppDataPath, $"{MaxLevel}_{MinRingDistance}_{(includeOceans ? "No" : "With")}_{Lookup.DataFileName}"));
        TimeZoneCalculation = new CalculationResource(TimeZoneDataFile);
    }
}
