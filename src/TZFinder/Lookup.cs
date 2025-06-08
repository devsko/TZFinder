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
    /// Gets the 1-based index of the specified time zone identifier.
    /// </summary>
    /// <param name="timeZoneId">The time zone identifier to look up.</param>
    /// <returns>
    /// The 1-based index of the specified time zone identifier.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="timeZoneId"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentException">Thrown if the specified time zone identifier is unknown.</exception>
    public static short GetTimeZoneIndex(string timeZoneId)
    {
#if NET
        ArgumentNullException.ThrowIfNull(timeZoneId);
#else
        if (timeZoneId is null)
        {
            throw new ArgumentNullException(nameof(timeZoneId));
        }
#endif

#if NET10_0_OR_GREATER
        short index = (short)MemoryExtensions.IndexOf(_timeZoneTree.Value.TimeZoneIds, timeZoneId, StringComparer.OrdinalIgnoreCase);
#else
        short index = (short)Array.FindIndex(_timeZoneTree.Value.TimeZoneIds, item => string.Equals(item, timeZoneId, StringComparison.OrdinalIgnoreCase));
#endif

        return index is not -1 ? ++index : throw new ArgumentException($"Unknown time zone '{timeZoneId}'.", nameof(timeZoneId));
    }

    /// <summary>
    /// Gets the time zone identifier for the specified 1-based index.
    /// </summary>
    /// <param name="index">The 1-based index of the time zone identifier to retrieve.</param>
    /// <returns>The time zone identifier corresponding to the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="index"/> is less than or equal to 0, or greater than to the number of available time zone identifiers.</exception>
    public static string GetTimeZoneId(short index)
    {
#if NET
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(index, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _timeZoneTree.Value.TimeZoneIds.Length);
#else
        if (index <= 0 || index > _timeZoneTree.Value.TimeZoneIds.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
#endif

        return _timeZoneTree.Value.TimeZoneIds[index - 1];
    }

    private static string GetTimeZoneId(short index, float longitude)
    {
        Debug.Assert(index >= 0 && index <= _timeZoneTree.Value.TimeZoneIds.Length);
        Debug.Assert(longitude is >= -180f and <= 180f);

        return index == 0 ? CalculateEtcTimeZoneId(longitude) : _timeZoneTree.Value.TimeZoneIds[index - 1];
    }

    /// <summary>
    /// Gets the <see cref="TimeZoneIndex"/> for the specified longitude and latitude.
    /// </summary>
    /// <param name="longitude">The longitude in degrees.</param>
    /// <param name="latitude">The latitude in degrees.</param>
    /// <returns>
    /// The <see cref="TimeZoneIndex"/> corresponding to the specified geographic coordinates.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="longitude"/> or <paramref name="latitude"/> is out of range.</exception>
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
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="longitude"/> or <paramref name="latitude"/> is out of range.</exception>
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
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="longitude"/> or <paramref name="latitude"/> is out of range.</exception>
    public static string GetTimeZoneId(float longitude, float latitude)
    {
        return GetTimeZoneId(GetTimeZoneIndex(longitude, latitude).First, longitude);
    }

    /// <summary>
    /// Gets the time zone identifier for the specified longitude and latitude, and outputs the bounding box containing the coordinates.
    /// </summary>
    /// <param name="longitude">The longitude in degrees.</param>
    /// <param name="latitude">The latitude in degrees.</param>
    /// <param name="box">When this method returns, contains the bounding box that includes the specified coordinates.</param>
    /// <returns>The time zone identifier for the specified coordinates.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="longitude"/> or <paramref name="latitude"/> is out of range.</exception>
    public static string GetTimeZoneId(float longitude, float latitude, out BBox box)
    {
        return GetTimeZoneId(GetTimeZoneIndex(longitude, latitude, out box).First);
    }

    /// <summary>
    /// Gets all time zone identifiers for the specified longitude and latitude.
    /// </summary>
    /// <param name="longitude">The longitude in degrees.</param>
    /// <param name="latitude">The latitude in degrees.</param>
    /// <returns>An enumerable of all time zone identifiers for the specified coordinates.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="longitude"/> or <paramref name="latitude"/> is out of range.</exception>
    public static IEnumerable<string> GetAllTimeZoneIds(float longitude, float latitude)
    {
        TimeZoneIndex index = GetTimeZoneIndex(longitude, latitude);
        yield return GetTimeZoneId(index.First, longitude);
        if (index.Second != 0)
        {
            yield return GetTimeZoneId(index.Second, 0);
        }
    }

    /// <summary>
    /// Calculates the etcetera time zone identifier for the specified longitude.
    /// </summary>
    /// <param name="longitude">The longitude in degrees.</param>
    /// <returns>The etcetera time zone identifier (e.g., "Etc/GMT", "Etc/GMT+2").</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="longitude"/> is out of range.</exception>
    public static string CalculateEtcTimeZoneId(float longitude)
    {
#if NET
        ArgumentOutOfRangeException.ThrowIfLessThan(longitude, -180f);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(longitude, 180f);
#else
        if (longitude is < -180f or > 180f)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude));
        }
#endif

#if NET
        int offset = (int)MathF.Round(-longitude / 15f);
#else
        int offset = (int)Math.Round(-longitude / 15f);
#endif

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
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="longitude"/> or <paramref name="latitude"/> is out of range.</exception>
    public static void Traverse(float longitude, float latitude, Action<BBox> action)
    {
#if NET
        ArgumentNullException.ThrowIfNull(action);
#else
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }
#endif

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
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="timeZoneId"/> is <see langword="null"/></exception>
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
