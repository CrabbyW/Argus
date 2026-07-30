namespace Argus.Api.WebApiPoco.Common;

/// <summary>
/// Base for every list filter: paging, sorting and free-text search.
/// </summary>
public abstract class DataViewFilterBase<T>
{
    private const int MaxPageSize = 200;

    private int pageNumber = 1;
    private int pageSize = 25;

    /// <summary>1-based page number.</summary>
    public int PageNumber
    {
        get => pageNumber;
        set => pageNumber = value < 1 ? 1 : value;
    }

    /// <summary>Clamped to 1..200 so a client cannot ask for the whole table.</summary>
    public int PageSize
    {
        get => pageSize;
        set => pageSize = value switch
        {
            < 1 => 1,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    public string? SortBy { get; set; }

    public string? SortDirection { get; set; }

    public string? SearchTerm { get; set; }

    public bool IsDescending =>
        string.Equals(SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

    public int Skip => (PageNumber - 1) * PageSize;
}
