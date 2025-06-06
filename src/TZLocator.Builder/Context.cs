using Spectre.Builder;

namespace TZLocator.Builder;

/// <summary>
/// Provides a context for managing file and resource dependencies during the time zone builder process.
/// </summary>
public class Context : BuilderContext
{
    /// <summary>
    /// Gets the <see cref="FileResource"/> representing the source file.
    /// </summary>
    public FileResource SourceFile => GetFileResource(nameof(SourceFile));

    /// <summary>
    /// Initializes the <see cref="SourceFile"/> resource with the specified path.
    /// </summary>
    /// <param name="path">The path to the source file.</param>
    /// <returns>The initialized <see cref="FileResource"/>.</returns>
    public FileResource InitSourceFile(string path) => AddResource(nameof(SourceFile), new FileResource(path));

    /// <summary>
    /// Gets the <see cref="Resource{TimeZoneContext}"/> representing the time zone context.
    /// </summary>
    public Resource<TimeZoneContext> TimeZoneContext => GetResource<TimeZoneContext>(nameof(TimeZoneContext));

    /// <summary>
    /// Initializes the <see cref="TimeZoneContext"/> resource.
    /// </summary>
    /// <returns>The initialized <see cref="Resource{TimeZoneContext}"/>.</returns>
    public Resource<TimeZoneContext> InitTimeZoneContext() => AddResource(nameof(TimeZoneContext), new Resource<TimeZoneContext>(SourceFile));

    /// <summary>
    /// Gets the <see cref="Resource{TimeZoneBuilderTree}"/> representing the time zone builder tree.
    /// </summary>
    public Resource<TimeZoneBuilderTree> TimeZoneTree => GetResource<TimeZoneBuilderTree>(nameof(TimeZoneTree));

    /// <summary>
    /// Initializes the <see cref="TimeZoneTree"/> resource.
    /// </summary>
    /// <returns>The initialized <see cref="Resource{TimeZoneBuilderTree}"/>.</returns>
    public Resource<TimeZoneBuilderTree> InitTimeZoneTree() => AddResource(nameof(TimeZoneTree), new Resource<TimeZoneBuilderTree>(TimeZoneContext));

    /// <summary>
    /// Gets the <see cref="Resource{TimeZoneBuilderTree}"/> representing the consolidated time zone builder tree.
    /// </summary>
    public Resource<TimeZoneBuilderTree> ConsolidatedTimeZoneTree => GetResource<TimeZoneBuilderTree>(nameof(ConsolidatedTimeZoneTree));

    /// <summary>
    /// Initializes the <see cref="ConsolidatedTimeZoneTree"/> resource.
    /// </summary>
    /// <returns>The initialized <see cref="Resource{TimeZoneBuilderTree}"/>.</returns>
    public Resource<TimeZoneBuilderTree> InitConsolidatedTimeZoneTree() => AddResource(nameof(ConsolidatedTimeZoneTree), new Resource<TimeZoneBuilderTree>(TimeZoneTree));

    /// <summary>
    /// Gets the <see cref="FileResource"/> representing the time zone file.
    /// </summary>
    public FileResource TimeZoneFile => GetFileResource(nameof(TimeZoneFile));

    /// <summary>
    /// Initializes the <see cref="TimeZoneFile"/> resource with the specified path.
    /// </summary>
    /// <param name="path">The path to the time zone file.</param>
    /// <returns>The initialized <see cref="FileResource"/>.</returns>
    public FileResource InitTimeZoneFile(string path) => AddResource(nameof(TimeZoneFile), new FileResource(path));
}
