namespace Argus.Api.WebApiPoco.Common;

/// <summary>One page of results plus the paging metadata the UI needs.</summary>
public class DataViewOutput<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    public int TotalCount { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public int TotalPages =>
        PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
