using System.Net.Http.Json;
using System.Text.Json;
using Spectre.Builder;
using TZFinder.Builder.Steps;

namespace TZFinder.Builder;

/// <summary>
/// Provides a context for managing file and resource dependencies during the time zone builder process.
/// <remarks>
/// CS9032 prevents making all property initializer private.
/// </remarks>
/// </summary>
public class Context : BuilderContext<Context>
{
    /// <summary>
    /// The GitHub repository for the timezone boundary builder.
    /// </summary>
    public const string SourceRepository = "evansiroky/timezone-boundary-builder";

    /// <summary>
    /// Gets the <see cref="HttpClient"/> used for HTTP requests.
    /// </summary>
    public required HttpClient Client { get; init; }

    /// <summary>
    /// Gets the tag name of the latest timezone boundary builder release.
    /// </summary>
    public required string SourceRelease { get; init; }

    /// <summary>
    /// Gets the name of the source file used for time zone data.
    /// </summary>
    public required string SourceFileName { get; init; }

    /// <summary>
    /// Gets the maximum depth level for the time zone builder tree.
    /// </summary>
    public required int MaxLevel { get; init; }

    /// <summary>
    /// Gets the minimum distance in meters between consecutive points in a ring.
    /// </summary>
    public required int MinRingDistance { get; init; }

    /// <summary>
    /// Gets the <see cref="FileResource"/> representing the source file.
    /// </summary>
    public required FileResource SourceFile { get; init; }

    /// <summary>
    /// Gets the <see cref="FileResource"/> representing the time zone file.
    /// </summary>
    public required FileResource TimeZoneDataFile { get; init; }

    /// <summary>
    /// Gets the <see cref="CalculationResource"/> representing the time zone calculation resource.
    /// </summary>
    public required CalculationResource TimeZoneCalculation { get; init; }

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

    private Context(CancellationToken cancellationToken) : base(cancellationToken)
    { }

    /// <summary>
    /// Runs the builder.
    /// </summary>
    /// <param name="output">-o, Relative or absolute path to the output directory</param>
    /// <param name="maxLevel">-l, Maximum precision level</param>
    /// <param name="minRingDistance">-d, Minimum distance between 2 positions in rings</param>
    /// <param name="release">-r, Release tag of Timezone Boundary Builder</param>
    /// <param name="includeEtc">-e, Include etcetera time zones</param>
    /// <param name="force"></param>
    /// <param name="cancellationToken">The cancellationToken</param>
    /// <returns></returns>
    public static async Task CreateAndRunAsync(
        string output = "",
        int maxLevel = 25,
        int minRingDistance = 500,
        string release = "latest",
        bool includeEtc = false,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Path.IsPathFullyQualified(output))
            {
                if (Path.IsPathRooted(output))
                {
                    throw new ArgumentException("The output directory must be either fully qualified or relative.");
                }
                output = Path.Combine(Environment.CurrentDirectory, output);
            }

            Directory.CreateDirectory(output);
            string outputPath = Path.Combine(output, Lookup.DataFileName);

            HttpClient client = new();
            client.DefaultRequestHeaders.Add("User-Agent", "TZFinder");

            if (release == "latest")
            {
                JsonElement latestRelease = await client.GetFromJsonAsync<JsonElement>($"https://api.github.com/repos/{SourceRepository}/releases/latest", cancellationToken);
                release = latestRelease.GetProperty("tag_name").GetString()!;
            }

            string sourceFileName = includeEtc ? "timezones-with-oceans.geojson" : "timezones.geojson";

            string releasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TZFinder", release);
            Directory.CreateDirectory(releasePath);

            FileResource sourceFile = new(Path.Combine(releasePath, sourceFileName));
            FileResource timeZoneDataFile = new(Path.Combine(releasePath, $"{maxLevel}_{minRingDistance}_{(includeEtc ? "Etc" : "NoEtc")}_{Lookup.DataFileName}"));
            CalculationResource timeZoneCalculation = new(timeZoneDataFile);

            Context context = new(cancellationToken)
            {
                Client = client,
                SourceRelease = release,
                SourceFileName = sourceFileName,
                MaxLevel = maxLevel,
                MinRingDistance = minRingDistance,
                SourceFile = sourceFile,
                TimeZoneDataFile = timeZoneDataFile,
                TimeZoneCalculation = timeZoneCalculation,
            };

            await context.RunAsync(
                Step<Context>.Sequential("Create time zone data",
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
                ]);

            Console.WriteLine();
            if (!force && File.Exists(outputPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Time zone data file already exists: {outputPath}");
                Console.WriteLine("To overwrite the file, run the command with --force option.");
            }
            else
            {
                File.Copy(context.TimeZoneDataFile.Path, outputPath, overwrite: true);
                Console.WriteLine($"Time zone data file created: {outputPath}");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(ex.Message);
        }
    }
}
