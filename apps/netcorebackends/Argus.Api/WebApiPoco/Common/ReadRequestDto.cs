using System.ComponentModel.DataAnnotations;

namespace Argus.Api.WebApiPoco.Common;

/// <summary>
/// Base of every read that is sent as a POST rather than a GET.
///
/// Reads carry their criteria in the body, so nothing about the request is left in the query
/// string. That makes <see cref="RequestUrl"/> necessary rather than decorative: without it the
/// action log would record `POST /api/installations/search` for every search ever run and the
/// file would say what kind of thing happened but never which one. The client therefore sends the
/// whole address the view corresponds to, and the log records the body verbatim.
/// </summary>
public abstract class ReadRequestDto
{
    /// <summary>
    /// The full URL this read belongs to, as the client sees it — scheme, host, path and query.
    /// Recorded, never dereferenced or trusted: it is a label the caller supplies about itself,
    /// so nothing on the server may route, redirect or fetch based on it.
    /// </summary>
    [StringLength(2048)]
    public string? RequestUrl { get; set; }
}
