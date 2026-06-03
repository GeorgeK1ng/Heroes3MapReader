namespace Heroes3MapReader.Logic.MapSpecificationLogic;

/// <summary>
/// Represents the specification for a given map format, detailing version flags, byte sizes, counts, and invalid identifiers.
/// </summary>
public sealed class MapSpecification
{
    /// <summary>
    /// Gets the version flags that indicate which optional sections are present in the map file.
    /// </summary>
    public MapVersionFlags VersionFlags { get; init; } = null!;
    /// <summary>
    /// Gets the byte sizes for various structures within the map file.
    /// </summary>
    public MapByteSizes ByteSizes { get; init; } = null!;
    /// <summary>
    /// Gets the expected counts for different collections in the map file.
    /// </summary>
    public MapCounts Counts { get; init; } = null!;
    /// <summary>
    /// Gets the values used to represent invalid or unset identifiers.
    /// </summary>
    public MapInvalidIdentifiers InvalidIdentifiers { get; init; } = null!;
}
