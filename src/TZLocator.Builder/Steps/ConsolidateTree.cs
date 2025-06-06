using Spectre.Builder;

namespace TZLocator.Builder.Steps;

/// <summary>
/// A conversion step that consolidates the nodes of a <see cref="TimeZoneBuilderTree"/> by resolving overlaps and exclusions
/// using the provided <see cref="TimeZoneContext"/>. This step updates the tree to reflect the most accurate set of time zones
/// for each geographic region, and produces a consolidated time zone tree as output.
/// The step takes the current <see cref="TimeZoneContext"/> and <see cref="TimeZoneBuilderTree"/>
/// as inputs, and produces a consolidated tree as output. The consolidation process traverses the tree, updating each node's
/// time zone indices to account for included and excluded regions, and optionally reports progress.
/// </summary>
public class ConsolidateTree : ConversionStep
{
    /// <inheritdoc/>
    public override string Name => "Consolidate nodes";

    /// <inheritdoc/>
    public override ProgressType Type => ProgressType.ValueRaw;

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<IResource> GetInputsAsync(BuilderContext context)
    {
        yield return ((Context)context).TimeZoneTree;
    }

    /// <inheritdoc/>
    protected override async IAsyncEnumerable<IResource> GetOutputsAsync(BuilderContext context)
    {
        yield return ((Context)context).InitConsolidatedTimeZoneTree();
    }

    /// <inheritdoc/>
    protected override Task ExecuteAsync(BuilderContext context, DateTime timestamp)
    {
        TimeZoneBuilderTree timeZoneTree = ((Context)context).TimeZoneTree.Value ?? throw new InvalidOperationException();
        TimeZoneContext timeZoneContext = ((Context)context).TimeZoneContext.Value ?? throw new InvalidOperationException();

        context.SetTotal(this, timeZoneContext.NodeCount);

        timeZoneContext.Consolidate(timeZoneTree, new Progress<int>(nodes => context.SetProgress(this, nodes)));
        ((Context)context).ConsolidatedTimeZoneTree.Set(timeZoneTree);

        return Task.CompletedTask;
    }
}
