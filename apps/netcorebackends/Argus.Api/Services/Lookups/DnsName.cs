using System.Text.RegularExpressions;

namespace Argus.Api.Services.Lookups;

/// <summary>
/// A DNS endpoint is stored as a host name and nothing else — <c>helpdesk.demo.example</c>, not
/// <c>https://helpdesk.demo.example/api/</c>. The two name the same endpoint, and accepting both means the
/// same machine appears twice in every dropdown built from the lookup, with only one of the two
/// matching what a colleague filters by.
/// </summary>
/// <remarks>
/// The value is pasted out of a browser's address bar more often than it is typed, so a URL is
/// normalized to its host rather than rejected. Deliberately not <see cref="Uri"/>: the common
/// input here carries no scheme, which <c>Uri</c> refuses outright, and prefixing one to get past
/// that turns a typo into a plausible-looking host.
///
/// The front end applies the same rule as the field is left, purely so the change is visible
/// before saving. This is the copy that decides what is stored.
/// </remarks>
public static partial class DnsName
{
    public static string Normalize(string value)
    {
        var rest = value.Trim();

        if (rest.Length == 0)
        {
            return string.Empty;
        }

        rest = SchemeRegex().Replace(rest, string.Empty);

        // Anything before an "@" is a user-info section, which is not part of the host.
        rest = UserInfoRegex().Replace(rest, string.Empty);

        var cut = rest.IndexOfAny(['/', '?', '#']);
        if (cut >= 0)
        {
            rest = rest[..cut];
        }

        rest = PortRegex().Replace(rest, string.Empty);

        // A fully-qualified name may be written with the root's trailing dot; the lookup stores it
        // without, so "host.local." and "host.local" do not become two rows.
        rest = rest.TrimEnd('.');

        // DNS is case-insensitive, so a capital letter would otherwise create a second row for a
        // host the lookup already has.
        return rest.ToLowerInvariant();
    }

    [GeneratedRegex(@"^[a-z][a-z0-9+.\-]*://", RegexOptions.IgnoreCase)]
    private static partial Regex SchemeRegex();

    [GeneratedRegex(@"^[^/@]*@")]
    private static partial Regex UserInfoRegex();

    [GeneratedRegex(@":\d+$")]
    private static partial Regex PortRegex();
}
