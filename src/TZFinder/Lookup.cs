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
    public const string DataFileName = "TimeZoneData.bin";

    private static readonly Lazy<TimeZoneTree> _timeZoneTree = new(Load);
    private static string? _timeZoneDataPath;
    private static Stream? _timeZoneDataStream;
    private static ReadOnlyCollection<string>? _timeZoneIds;

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

    /// <summary>
    /// Gets a read-only collection of all available time zone identifiers.
    /// </summary>
    public static ReadOnlyCollection<string> TimeZoneIds => _timeZoneIds ??= new(_timeZoneTree.Value.TimeZoneIds);

    /// <summary>
    /// Gets the time zone index (1-based) for the specified time zone identifier.
    /// </summary>
    /// <param name="timeZoneId">The time zone identifier to look up.</param>
    /// <returns>The 1-based index of the specified time zone identifier.</returns>
    /// <exception cref="ArgumentException">Thrown if the time zone identifier is unknown.</exception>
    public static short GetTimeZoneIndex(string timeZoneId)
    {
#if NET10_0
        short index = (short)((ReadOnlySpan<string>)_timeZoneTree.Value.TimeZoneIds).IndexOf(timeZoneId, StringComparer.OrdinalIgnoreCase);
#else
        short index = (short)Array.FindIndex(_timeZoneTree.Value.TimeZoneIds, item => string.Equals(item, timeZoneId, StringComparison.OrdinalIgnoreCase));
#endif

        return index is not -1 ? ++index : throw new ArgumentException($"Unknown time zone '{timeZoneId}'.", nameof(timeZoneId));
    }

    /// <summary>
    /// Gets the time zone identifier for the specified index.
    /// </summary>
    /// <param name="index">The time zone index (1-based).</param>
    /// <returns>The time zone identifier corresponding to the index.</returns>
    public static string GetTimeZoneId(short index)
    {
        return _timeZoneTree.Value.TimeZoneIds[index - 1];
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
    /// Gets the <see cref="TimeZoneIndex"/> for the specified longitude and latitude, and outputs the bounding box containing the coordinates.
    /// </summary>
    /// <param name="longitude">The longitude in degrees.</param>
    /// <param name="latitude">The latitude in degrees.</param>
    /// <param name="box">When this method returns, contains the bounding box that includes the specified coordinates.</param>
    /// <returns>The <see cref="TimeZoneIndex"/> for the specified coordinates.</returns>
    public static TimeZoneIndex GetTimeZoneIndex(float longitude, float latitude, out BBox box)
    {
        (TimeZoneIndex index, box, _) = _timeZoneTree.Value.Get(longitude, latitude);
        return index;
    }

    /// <summary>
    /// Gets the time zone identifier for the specified longitude and latitude.
    /// </summary>
    /// <param name="longitude">The longitude in degrees.</param>
    /// <param name="latitude">The latitude in degrees.</param>
    /// <returns>The time zone identifier for the specified coordinates.</returns>
    public static string GetTimeZoneId(float longitude, float latitude)
    {
        return _timeZoneTree.Value.TimeZoneIds[GetTimeZoneIndex(longitude, latitude).First];
    }

    /// <summary>
    /// Gets the time zone identifier for the specified longitude and latitude, and outputs the bounding box containing the coordinates.
    /// </summary>
    /// <param name="longitude">The longitude in degrees.</param>
    /// <param name="latitude">The latitude in degrees.</param>
    /// <param name="box">When this method returns, contains the bounding box that includes the specified coordinates.</param>
    /// <returns>The time zone identifier for the specified coordinates.</returns>
    public static string GetTimeZoneId(float longitude, float latitude, out BBox box)
    {
        return _timeZoneTree.Value.TimeZoneIds[GetTimeZoneIndex(longitude, latitude, out box).First];
    }

    /// <summary>
    /// Gets all time zone identifiers for the specified longitude and latitude.
    /// </summary>
    /// <param name="longitude">The longitude in degrees.</param>
    /// <param name="latitude">The latitude in degrees.</param>
    /// <returns>An enumerable of all time zone identifiers for the specified coordinates.</returns>
    public static IEnumerable<string> GetAllTimeZoneIds(float longitude, float latitude)
    {
        TimeZoneIndex index = GetTimeZoneIndex(longitude, latitude);
        if (index.First == 0)
        {
            yield return CalculateEtcTimeZoneId(longitude);
        }
        else
        {
            yield return GetTimeZoneId(index.First);
            if (index.Second != 0)
            {
                yield return GetTimeZoneId(index.Second);
            }
        }
    }

    /// <summary>
    /// Calculates the etcetera time zone identifier for the specified longitude.
    /// </summary>
    /// <param name="longitude">The longitude in degrees.</param>
    /// <returns>The etcetera time zone identifier (e.g., "Etc/GMT", "Etc/GMT+2").</returns>
    public static string CalculateEtcTimeZoneId(float longitude)
    {
        int offset = (int)Math.Round(-longitude / 15.0);

        return "Etc/GMT" + offset switch
        {
            0 => "",
            > 0 => FormattableString.Invariant($"+{offset}"),
            < 0 => FormattableString.Invariant($"{offset}"),
        };
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
    /// <param name="timeZoneId">The time zone identifier.</param>
    /// <param name="action">The action to invoke for each bounding box.</param>
    /// <exception cref="ArgumentException">Thrown if the time zone identifier is unknown.</exception>
    public static void Traverse(string timeZoneId, Action<BBox> action)
    {
        short timeZoneIndex = GetTimeZoneIndex(timeZoneId);
        _timeZoneTree.Value.Traverse((index, box) =>
        {
            if (index.Contains(timeZoneIndex))
            {
                action(box);
            }
        });
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

}
