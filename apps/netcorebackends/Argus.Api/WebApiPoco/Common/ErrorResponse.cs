namespace Argus.Api.WebApiPoco.Common;

/// <summary>Standard error wrapper for every failed API response.</summary>
public class ErrorResponse
{
    public bool Success { get; set; }

    public string ErrorCode { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    /// <summary>Correlates a client-visible error with the server log entry.</summary>
    public string? TraceId { get; set; }
}
