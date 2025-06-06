using Spectre.Builder;

namespace TZLocator.Builder.Steps;

/// <summary>
/// Represents a conversion step that loads the source data file and creates a <see cref="TimeZoneContext"/>.
/// This step reads the source file, tracks progress, and loads the time zone context into a resource.
/// </summary>
public class LoadSource : ConversionStep
{
    /// <inheritdoc/>
    public override string Name => "Loading source data";

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
    protected override async Task ExecuteAsync(BuilderContext context, DateTime timestamp)
    {
        FileResource sourceFile = ((Context)context).SourceFile;

        await using ProgressStream content = new(sourceFile.OpenRead(0), bytes => context.IncrementProgress(this, bytes));

        context.SetTotal(this, content.Length);

        ((Context)context).TimeZoneContext = await TimeZoneContext.LoadAsync(content);
    }
}
