using Spectre.Builder;

namespace TZLocator.Builder.Steps;

/// <summary>
/// A conversion step that serializes the consolidated time zone tree to a file.
/// This step takes the consolidated <see cref="TimeZoneBuilderTree"/> from the build context and serializes it
/// to a file at the specified path.
/// </summary>
/// <param name="path">The file path where the serialized time zone tree will be written.</param>
public class SerializeTree(string path) : ConversionStep
{
    /// <inheritdoc/>
    public override string Name => "Serialize nodes";

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<IResource> GetInputsAsync(BuilderContext context)
    {
        yield return ((Context)context).ConsolidatedTimeZoneTree;
    }

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<IResource> GetOutputsAsync(BuilderContext context)
    {
        yield return ((Context)context).InitTimeZoneFile(path);
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(BuilderContext context, DateTime timestamp)
    {
        TimeZoneBuilderTree timeZoneTree = ((Context)context).ConsolidatedTimeZoneTree.Value ?? throw new InvalidOperationException();
        TimeZoneContext timeZoneContext = ((Context)context).TimeZoneContext.Value ?? throw new InvalidOperationException();
        FileResource timeZoneFile = ((Context)context).TimeZoneFile;

        context.SetTotal(this, timeZoneContext.NodeCount);

        await using PreliminaryFileStream stream = timeZoneFile.OpenCreate(1024 * 1024, timestamp);
        TimeZoneTreeSerializer.Serialize(timeZoneTree, stream, new Progress<int>(nodes => context.SetProgress(this, nodes)));
        stream.Persist();
    }
}
