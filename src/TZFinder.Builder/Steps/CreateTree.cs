using Spectre.Builder;

namespace TZFinder.Builder.Steps;

/// <summary>
/// A conversion step that creates the time zone tree structure from loaded time zone sources.
/// This step is responsible for building the hierarchical tree of time zone nodes using the sources
/// loaded in the <see cref="TimeZoneContext"/>. It reports progress as each source is processed.
/// </summary>
public class CreateTree : ConversionStep
{
    /// <inheritdoc/>
    public override string Name => "Processing time zones";

    /// <inheritdoc/>
    protected override bool ShowProgressAsDataSize => false;

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<IResource> GetInputsAsync(BuilderContext context)
    {
        yield return ((Context)context).SourceFile;
    }

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<IResource> GetOutputsAsync(BuilderContext context)
    {
        yield return ((Context)context).TimeZoneCalculation;
    }

    /// <inheritdoc/>
    protected override Task ExecuteAsync(BuilderContext builderContext, DateTime timestamp)
    {
        Context context = (Context)builderContext;

        TimeZoneContext timeZoneContext = context.TimeZoneContext ?? throw new InvalidOperationException();

        context.SetTotal(this, timeZoneContext.Sources.Count);

        (context.TimeZoneTree, context.NodeCount) = timeZoneContext.CreateTree(new ProgressSlim<int>(sources => context.SetProgress(this, sources)));

        return Task.CompletedTask;
    }
}
