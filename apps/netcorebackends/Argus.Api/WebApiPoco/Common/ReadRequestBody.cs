namespace Argus.Api.WebApiPoco.Common;

/// <summary>
/// The body of a read that has no criteria of its own — everything it needs is already in the
/// route. It exists so that such a read still arrives as a POST carrying its
/// <see cref="ReadRequestDto.RequestUrl"/>, which is what keeps the action log readable.
///
/// Nullable at every call site: the endpoint answers the same way whether or not a body was
/// sent, so a caller that omits it gets data rather than a 400.
/// </summary>
public class ReadRequestBody : ReadRequestDto
{
}
