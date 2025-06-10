using Spectre.Builder;

namespace TZFinder.Builder.Steps;

/// <summary>
/// A conversion step that creates the time zone tree structure from loaded time zone sources.
/// This step is responsible for building the hierarchical tree of time zone nodes using the sources
/// loaded in the <see cref="TimeZoneContext"/>. It reports progress as each source is processed.
/// </summary>
public class CreateTree : ConversionStep<Context>
{
    /// <inheritdoc/>
    public override string Name => "Processing time zones";

    /// <inheritdoc/>
    protected override bool ShowProgressAsDataSize => false;

    /// <inheritdoc/>
    protected override IEnumerable<IResource> GetInputs(Context context)
    {
        yield return context.SourceFile;
    }

    /// <inheritdoc/>
    protected override IEnumerable<IResource> GetOutputs(Context context)
    {
        yield return context.TimeZoneCalculation;
    }

    /// <inheritdoc/>
    protected override Task ExecuteAsync(Context context, DateTime timestamp, CancellationToken cancellationToken)
    {
        TimeZoneContext timeZoneContext = context.TimeZoneContext ?? throw new InvalidOperationException();

        context.SetTotal(this, timeZoneContext.Sources.Count);

        context.TimeZoneTree = timeZoneContext.CreateTree(context.MaxLevel, new ProgressSlim<int>(sources => context.SetProgress(this, sources)), cancellationToken);

        return Task.CompletedTask;
    }
}
