namespace JiuMi.Application.WorldClock;

/// <summary>
/// One row of the manage-places result list.
/// </summary>
public sealed record CatalogItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogItem"/> class.
    /// </summary>
    /// <param name="kind">The granularity of the item.</param>
    /// <param name="id">The ISO alpha-2 code or IANA id.</param>
    /// <param name="primaryName">The name in the interface language.</param>
    /// <param name="secondaryName">The name in the other language.</param>
    /// <param name="dialCode">The country calling code.</param>
    /// <param name="groupName">The country group shown above cities, or <see langword="null"/> for countries.</param>
    public CatalogItem(
        PlaceKind kind,
        string id,
        string primaryName,
        string secondaryName,
        string dialCode,
        string? groupName)
    {
        Kind = kind;
        Id = id;
        PrimaryName = primaryName;
        SecondaryName = secondaryName;
        DialCode = dialCode;
        GroupName = groupName;
    }

    /// <summary>
    /// Gets the granularity of the item.
    /// </summary>
    public PlaceKind Kind
    {
        get;
    }

    /// <summary>
    /// Gets the ISO alpha-2 code or IANA id.
    /// </summary>
    public string Id
    {
        get;
    }

    /// <summary>
    /// Gets the stable key of the item.
    /// </summary>
    public string Key => Kind == PlaceKind.Country ? $"country:{Id}" : $"city:{Id}";

    /// <summary>
    /// Gets the name in the interface language.
    /// </summary>
    public string PrimaryName
    {
        get;
    }

    /// <summary>
    /// Gets the name in the other language.
    /// </summary>
    public string SecondaryName
    {
        get;
    }

    /// <summary>
    /// Gets the country calling code.
    /// </summary>
    public string DialCode
    {
        get;
    }

    /// <summary>
    /// Gets the country group name shown above cities, or <see langword="null"/> for countries.
    /// </summary>
    public string? GroupName
    {
        get;
    }
}
