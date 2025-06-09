using System.IO.Compression;
using Spectre.Builder;

namespace TZFinder.Builder.Steps;

/// <summary>
/// A conversion step that serializes the consolidated time zone tree to a file.
/// This step takes the consolidated <see cref="TimeZoneBuilderTree"/> from the build context and serializes it
/// to a file at the specified path.
/// </summary>
public class SerializeTree : ConversionStep
{
    /// <inheritdoc/>
    public override string Name => "Writing data file";

    /// <inheritdoc/>
    protected override IEnumerable<IResource> GetInputs(BuilderContext context)
    {
        yield return ((Context)context).SourceFile;
    }

    /// <inheritdoc/>
    protected override IEnumerable<IResource> GetOutputs(BuilderContext context)
    {
        yield return ((Context)context).TimeZoneDataFile;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(BuilderContext builderContext, DateTime timestamp, CancellationToken cancellationToken)
    {
        Context context = (Context)builderContext;

        TimeZoneBuilderTree timeZoneTree = context.TimeZoneTree ?? throw new InvalidOperationException();
        TimeZoneContext timeZoneContext = context.TimeZoneContext ?? throw new InvalidOperationException();
        FileResource timeZoneFile = context.TimeZoneDataFile;

        await using PreliminaryFileStream file = timeZoneFile.OpenCreate(0, timestamp);

        // GZipStream cannot be flushed completely and tries to write to the underlying stream
        // when disposed (happens after Persist() which disposes the file stream).
        await using (GZipStream stream = new GZipStream(
            new ProgressStream(
                file,
                bytes => context.IncrementProgress(this, bytes)),
            CompressionLevel.Optimal, leaveOpen: true))
        {
            timeZoneTree.Serialize(stream);
        }

        file.Persist();
    }
}
