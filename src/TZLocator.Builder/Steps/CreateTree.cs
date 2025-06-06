using Spectre.Builder;

namespace TZLocator.Builder.Steps;

/// <summary>
/// A conversion step that creates the time zone tree structure from loaded time zone sources.
/// This step is responsible for building the hierarchical tree of time zone nodes using the sources
/// loaded in the <see cref="TimeZoneContext"/>. It reports progress as each source is processed.
/// </summary>
public class CreateTree : ConversionStep
{
    /// <inheritdoc/>
    public override string Name => "Creating nodes for time zones";

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
    protected override Task ExecuteAsync(BuilderContext context, DateTime timestamp)
    {
        TimeZoneContext timeZoneContext = ((Context)context).TimeZoneContext ?? throw new InvalidOperationException();

        context.SetTotal(this, timeZoneContext.Sources.Count);

        ((Context)context).TimeZoneTree = timeZoneContext.CreateTree(new Progress<int>(sources => context.SetProgress(this, sources)));

        return Task.CompletedTask;
    }
}
