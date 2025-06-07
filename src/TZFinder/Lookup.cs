using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Compression;

namespace TZFinder;

/// <summary>
/// Provides static methods and properties for looking up time zones based on geographic coordinates.
/// Handles loading and accessing time zone data from a file or stream, and exposes methods for querying and traversing the time zone tree.
/// </summary>
public static class Lookup
{
    /// <summary>
    /// The default file name for the time zone data file.
    /// </summary>
    public const string DataFileName = "TimeZones.tree";

    private static readonly Lazy<TimeZoneTree> _timeZoneTree = new(Load);
    private static string? _timeZoneDataPath;
    private static Stream? _timeZoneDataStream;

    /// <summary>
    /// Gets a read-only collection of all available time zone names.
    /// </summary>
    public static ReadOnlyCollection<string> TimeZones { get; } = new(_timeZoneTree.Value.TimeZoneNames);

    /// <summary>
    /// Gets or sets the path to the time zone data file.
    /// Must be set before the time zone tree is loaded.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the time zone tree has already been loaded.</exception>
    /// <exception cref="ArgumentNullException">Thrown if the value is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the file does not exist.</exception>
    public static string? TimeZoneDataPath
    {
        get => _timeZoneDataPath;
        set
        {
            if (_timeZoneTree.IsValueCreated)
            {
                throw new InvalidOperationException("Cannot set TimeZoneDataPath after the time zone tree has been loaded.");
            }
#if NET
            ArgumentNullException.ThrowIfNull(value);
#else
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }
#endif
            if (!File.Exists(value))
            {
                throw new ArgumentException("The specified time zone data file does not exist.", nameof(value));
            }

            _timeZoneDataPath = value;
        }
    }

    /// <summary>
    /// Gets or sets the stream containing time zone data.
    /// Must be set before the time zone tree is loaded.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the time zone tree has already been loaded.</exception>
    /// <exception cref="ArgumentNullException">Thrown if the value is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the stream is not readable.</exception>
    public static Stream? TimeZoneDataStream
    {
        get => _timeZoneDataStream;
        set
        {
            if (_timeZoneTree.IsValueCreated)
            {
                throw new InvalidOperationException("Cannot set TimeZoneDataStream after the time zone tree has been loaded.");
            }
#if NET
            ArgumentNullException.ThrowIfNull(value);
#else
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }
#endif
            if (!value.CanRead)
            {
                throw new ArgumentException("The provided stream must be readable.", nameof(value));
            }

            _timeZoneDataStream = value;
        }
    }

    private static TimeZoneTree Load()
    {
        Stream? stream = null;
        string? dataPath = null;

        if (_timeZoneDataStream is not null)
        {
            stream = _timeZoneDataStream;
        }
        else
        {
            if (_timeZoneDataPath is not null)
            {
                dataPath = _timeZoneDataPath;
            }
            else
            {
                string? executablePath = null;

#if NET
                executablePath = Environment.ProcessPath;
#endif
                if ((executablePath ??= Process.GetCurrentProcess().MainModule?.FileName) is not null)
                {
                    dataPath = Path.Combine(Path.GetDirectoryName(executablePath)!, DataFileName);
                }

                if (dataPath is null || !File.Exists(dataPath))
                {
                    throw new InvalidOperationException($"Could not find time zone data file{(executablePath is not null ? $" at '{executablePath}'" : "")}. Consider setting Lookup.TimeZoneDataPath.");
                }
            }
            try
            {
                stream = File.OpenRead(dataPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Could not open time zone data file '{dataPath}'", ex);
            }
        }

        try
        {
            return LoadFromStream(stream);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Could not deserialize time zone data{(dataPath is not null ? $" from '{dataPath}'" : "")}.", ex);
        }

        static TimeZoneTree LoadFromStream(Stream stream)
        {
            using GZipStream zip = new(stream, CompressionMode.Decompress, leaveOpen: false);
            return TimeZoneTree.Deserialize(zip);
        }
    }

    /// <summary>
    /// Gets the <see cref="TimeZoneIndex"/> for the specified longitude and latitude.
    /// </summary>
    /// <param name="longitude">The longitude in degrees.</param>
    /// <param name="latitude">The latitude in degrees.</param>
    /// <returns>The <see cref="TimeZoneIndex"/> for the specified coordinates.</returns>
    public static TimeZoneIndex GetTimeZoneIndex(float longitude, float latitude)
    {
        return _timeZoneTree.Value.Get(longitude, latitude).Index;
    }

    /// <summary>
    /// Gets the time zone name for the specified index.
    /// </summary>
    /// <param name="index">The time zone index (1-based).</param>
    /// <returns>The time zone name corresponding to the index.</returns>
    public static string GetTimeZone(short index)
    {
        return _timeZoneTree.Value.TimeZoneNames[index - 1];
    }

    /// <summary>
    /// Calculates the "Etc" time zone name for the specified longitude.
    /// </summary>
    /// <param name="longitude">The longitude in degrees.</param>
    /// <returns>The "Etc" time zone name (e.g., "Etc/UTC", "Etc/GMT+2").</returns>
    public static string CalculateEtcTimeZone(float longitude)
    {
        int offset = (int)((longitude + 7.5f) / 15);

        return offset switch
        {
            0 => "Etc/UTC",
            < 0 => $"Etc/GMT+{Math.Abs(offset)}",
            > 0 => $"Etc/GMT-{offset}",
        };
        //int offset = (int)Math.Round(-longitude / 15.0);

        //return offset == 0 ? "Etc/UTC" : $"Etc/GMT{(offset > 0 ? "+" : "")}{offset}";
    }

    /// <summary>
    /// Gets the time zone name for the specified longitude and latitude.
    /// </summary>
    /// <param name="longitude">The longitude in degrees.</param>
    /// <param name="latitude">The latitude in degrees.</param>
    /// <returns>The time zone name for the specified coordinates.</returns>
    public static string GetTimeZone(float longitude, float latitude)
    {
        return _timeZoneTree.Value.TimeZoneNames[GetTimeZoneIndex(longitude, latitude).First];
    }

    /// <summary>
    /// Gets all time zone names for the specified longitude and latitude.
    /// </summary>
    /// <param name="longitude">The longitude in degrees.</param>
    /// <param name="latitude">The latitude in degrees.</param>
    /// <returns>An enumerable of all time zone names for the specified coordinates.</returns>
    public static IEnumerable<string> GetAllTimeZones(float longitude, float latitude)
    {
        TimeZoneIndex index = GetTimeZoneIndex(longitude, latitude);
        if (index.First == 0)
        {
            yield return CalculateEtcTimeZone(longitude);
        }
        else
        {
            yield return GetTimeZone(index.First);
            if (index.Second != 0)
            {
                yield return GetTimeZone(index.Second);
            }
        }
    }

    /// <summary>
    /// Traverses all bounding boxes contained in the time zone specified by the coordinates and invokes the provided action for each.
    /// </summary>
    /// <remarks>If the specified coordinates point to an area with overlapping time zones, only boxes with the same combination are traversed.</remarks>
    /// <param name="longitude">The longitude in degrees.</param>
    /// <param name="latitude">The latitude in degrees.</param>
    /// <param name="action">The action to invoke for each bounding box.</param>
    public static void Traverse(float longitude, float latitude, Action<BBox> action)
    {
        TimeZoneIndex timeZoneIndex = GetTimeZoneIndex(longitude, latitude);
        _timeZoneTree.Value.Traverse((index, box) =>
        {
            if (timeZoneIndex.Second == 0 && index.Contains(timeZoneIndex.First) || index.Equals(timeZoneIndex))
            {
                action(box);
            }
        });
    }

    /// <summary>
    /// Traverses the bounding boxes contained in the specified time zone and invokes the provided action for each.
    /// </summary>
    /// <param name="timeZone">The time zone name.</param>
    /// <param name="action">The action to invoke for each bounding box.</param>
    /// <exception cref="ArgumentException">Thrown if the time zone name is unknown.</exception>
    public static void Traverse(string timeZone, Action<BBox> action)
    {
        short timeZoneIndex = (short)Array.FindIndex(_timeZoneTree.Value.TimeZoneNames, item => string.Equals(item, timeZone, StringComparison.OrdinalIgnoreCase));
        if (timeZoneIndex is -1)
        {
            throw new ArgumentException($"Unknown time zone '{timeZone}'.", nameof(timeZone));
        }

        timeZoneIndex++;
        _timeZoneTree.Value.Traverse((index, box) =>
        {
            if (index.Contains(timeZoneIndex))
            {
                action(box);
            }
        });
    }
}
