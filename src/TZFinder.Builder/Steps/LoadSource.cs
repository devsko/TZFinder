using Spectre.Builder;

namespace TZFinder.Builder.Steps;

/// <summary>
/// Represents a conversion step that loads the source data file and creates a <see cref="TimeZoneContext"/>.
/// This step reads the source file, tracks progress, and loads the time zone context into a resource.
/// </summary>
public class LoadSource : ConversionStep<Context>
{
    /// <inheritdoc/>
    public override string Name => "Loading source data";

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
    protected override async Task ExecuteAsync(Context context, DateTime timestamp, CancellationToken cancellationToken)
    {
        FileResource sourceFile = context.SourceFile;

        await using ProgressStream content = new(sourceFile.OpenRead(0), bytes => context.IncrementProgress(this, bytes));

        context.SetTotal(this, content.Length);

        context.TimeZoneContext = await TimeZoneContext.LoadAsync(content, context.MinRingDistance, cancellationToken);
    }
}
