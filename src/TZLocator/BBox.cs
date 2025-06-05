namespace TZLocator;

/// <summary>
/// Represents a rectangular bounding box defined by southwest and northeast positions.
/// </summary>
/// <param name="SouthWest">The southwest corner of the bounding box.</param>
/// <param name="NorthEast">The northeast corner of the bounding box.</param>
public readonly record struct BBox(Position SouthWest, Position NorthEast)
{
    /// <summary>
    /// Gets a bounding box that represents the entire world.
    /// </summary>
    public static BBox World { get; } = new(new Position(-180, -90), new Position(180, 90));

    /// <summary>
    /// Splits the bounding box into two halves along the current axis determined by the level.
    /// </summary>
    /// <param name="level">
    /// The current level of splitting. Even levels split along longitude, odd levels split along latitude.
    /// The value is incremented after each call.
    /// </param>
    /// <returns>
    /// A tuple containing the two resulting bounding boxes: <c>hi</c> (higher half) and <c>lo</c> (lower half).
    /// </returns>
    public (BBox hi, BBox lo) Split(ref int level)
    {
        float center;
        if (level++ % 2 == 0)
        {
            center = (NorthEast.Longitude + SouthWest.Longitude) / 2;
            return (
                this with { SouthWest = new(center, SouthWest.Latitude) },
                this with { NorthEast = new(center, NorthEast.Latitude) });
        }
        else
        {
            center = (NorthEast.Latitude + SouthWest.Latitude) / 2;
            return (
                this with { SouthWest = new(SouthWest.Longitude, center) },
                this with { NorthEast = new(NorthEast.Longitude, center) });
        }
    }
}
