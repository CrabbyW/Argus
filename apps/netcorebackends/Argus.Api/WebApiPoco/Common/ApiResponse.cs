namespace Argus.Api.WebApiPoco.Common;

/// <summary>Standard success wrapper for every API response.</summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }

    public T? Data { get; set; }

    public string? Message { get; set; }
}
