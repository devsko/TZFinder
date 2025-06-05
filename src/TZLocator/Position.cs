namespace TZLocator;

/// <summary>
/// Represents a geographic position with longitude and latitude.
/// </summary>
public readonly struct Position : IEquatable<Position>
{
    /// <summary>
    /// Gets the longitude of the position.
    /// </summary>
    public float Longitude { get; }

    /// <summary>
    /// Gets the latitude of the position.
    /// </summary>
    public float Latitude { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Position"/> struct.
    /// </summary>
    /// <param name="longitude">The longitude, in degrees. Must be between -180 and 180.</param>
    /// <param name="latitude">The latitude, in degrees. Must be between -90 and 90.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="longitude"/> or <paramref name="latitude"/> is out of range or NaN.
    /// </exception>
    public Position(float longitude, float latitude)
    {
        if (float.IsNaN(longitude) || longitude > 180 || longitude < -180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude));
        }
        if (float.IsNaN(latitude) || latitude > 90 || latitude < -90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude));
        }

        Longitude = longitude;
        Latitude = latitude;
    }

    /// <summary>
    /// Determines whether the specified <see cref="Position"/> is equal to the current <see cref="Position"/>.
    /// </summary>
    /// <param name="other">The <see cref="Position"/> to compare with the current <see cref="Position"/>.</param>
    /// <returns><see langword="true"/> if the specified <see cref="Position"/> is equal to the current <see cref="Position"/>; otherwise, <see langword="false"/>.</returns>
    public bool Equals(Position other) => Longitude == other.Longitude && Latitude == other.Latitude;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Position position && Equals(position);

    /// <inheritdoc/>
    public override int GetHashCode() => Longitude.GetHashCode() ^ Latitude.GetHashCode();

    /// <inheritdoc/>
    public override string ToString() => $"[Lon={Longitude} Lat={Latitude}]";

    /// <summary>
    /// Determines whether two specified <see cref="Position"/> instances are equal.
    /// </summary>
    /// <param name="left">The first <see cref="Position"/> to compare.</param>
    /// <param name="right">The second <see cref="Position"/> to compare.</param>
    /// <returns><see langword="true"/> if the two <see cref="Position"/> instances are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(Position left, Position right) => left.Equals(right);

    /// <summary>
    /// Determines whether two specified <see cref="Position"/> instances are not equal.
    /// </summary>
    /// <param name="left">The first <see cref="Position"/> to compare.</param>
    /// <param name="right">The second <see cref="Position"/> to compare.</param>
    /// <returns><see langword="true"/> if the two <see cref="Position"/> instances are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(Position left, Position right) => !left.Equals(right);
}
