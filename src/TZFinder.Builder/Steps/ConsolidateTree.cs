using Spectre.Builder;

namespace TZFinder.Builder.Steps;

/// <summary>
/// A conversion step that consolidates the nodes of a <see cref="TimeZoneBuilderTree"/> by resolving overlaps and exclusions
/// using the provided <see cref="TimeZoneContext"/>. This step updates the tree to reflect the most accurate set of time zones
/// for each geographic region, and produces a consolidated time zone tree as output.
/// The step takes the current <see cref="TimeZoneContext"/> and <see cref="TimeZoneBuilderTree"/>
/// as inputs, and produces a consolidated tree as output. The consolidation process traverses the tree, updating each node's
/// time zone indices to account for included and excluded regions, and optionally reports progress.
/// </summary>
public class ConsolidateTree : ConversionStep<Context>
{
    /// <inheritdoc/>
    public override string Name => "Consolidating nodes";

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
        TimeZoneBuilderTree timeZoneTree = context.TimeZoneTree ?? throw new InvalidOperationException();
        TimeZoneContext timeZoneContext = context.TimeZoneContext ?? throw new InvalidOperationException();

        context.SetTotal(this, context.NodeCount);

        timeZoneContext.Consolidate(timeZoneTree, new ProgressSlim<int>(nodes => context.SetProgress(this, nodes)), cancellationToken);

        return Task.CompletedTask;
    }
}
