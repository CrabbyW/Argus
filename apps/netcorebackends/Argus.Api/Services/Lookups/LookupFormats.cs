using System.Text.RegularExpressions;

namespace Argus.Api.Services.Lookups;

/// <summary>
/// The formats the lookups with a format are stored in.
///
/// A lookup is the single source of truth for a value that dozens of installations point at, so a
/// row written the wrong way is not a cosmetic problem: <c>C:\Inetpub\CallCenter</c> and
/// <c>c:\inetpub\callcenter</c> are one directory and would become two rows, and only one of them
/// answers the question a colleague filters by. Windows paths and DNS names are case-insensitive,
/// so case is the difference that matters least and duplicates the most.
/// </summary>
/// <remarks>
/// Each format is a pair: normalize turns what a person pasted into the stored form, validate
/// rejects what cannot be turned into it. Normalizing first and validating after means the rules
/// only have to describe the stored form — a trailing backslash or a pasted URL is fixed rather
/// than refused, and only a value that is genuinely not a path or not a host name is an error.
///
/// This is the copy that decides what is stored; it runs for every client. <see cref="DnsName"/>
/// carries the DNS rule itself, which predates this file.
/// </remarks>
public static partial class LookupFormats
{
    /// <summary>
    /// A machine's own name: <c>GAIIS1</c>, or its fully-qualified form. Stored upper case, which
    /// is how the source workbook and every RDP shortcut in the building writes it.
    /// </summary>
    public static string NormalizeMachine(string value) => value.Trim().ToUpperInvariant();

    public static string? ValidateMachine(string name) =>
        MachineRegex().IsMatch(name)
            ? null
            : "A machine name is a host name: letters, digits, hyphens and dots, e.g. GAIIS1.";

    /// <summary>
    /// A DNS endpoint, already reduced to its host by <see cref="DnsName.Normalize"/>. This adds
    /// the check that what is left really is a host name — normalization alone will happily store
    /// "the paha server" unchanged.
    /// </summary>
    public static string? ValidateDnsName(string name) =>
        HostRegex().IsMatch(name)
            ? null
            : "A DNS endpoint is a host name, e.g. paha.ga.local — no spaces, and a label may not "
              + "begin or end with a hyphen.";

    /// <summary>
    /// The path a site is served under: <c>/</c>, <c>/callcenter.rc0</c>. A URL path, so it is
    /// written with forward slashes and no trailing one — <c>/worker/</c> and <c>/worker</c> are
    /// the same application.
    /// </summary>
    public static string NormalizeRootPath(string value)
    {
        var path = value.Trim().Replace('\\', '/');

        if (path.Length == 0)
        {
            return string.Empty;
        }

        // IIS matches these without regard to case, so storing the case someone typed would only
        // let the same application in twice.
        path = path.ToLowerInvariant();

        if (!path.StartsWith('/'))
        {
            path = '/' + path;
        }

        path = DoubleSlashRegex().Replace(path, "/");

        // Everything but the site root itself, which *is* a single slash.
        return path.Length > 1 ? path.TrimEnd('/') : path;
    }

    public static string? ValidateRootPath(string name) =>
        RootPathRegex().IsMatch(name)
            ? null
            : "A root path starts with / and holds no spaces, e.g. /callcenter.rc0.";

    /// <summary>
    /// Where the files sit on disk: <c>c:\inetpub\callcenter.rc0</c>, or a UNC share. Always
    /// absolute — a relative path does not identify a directory on a server someone has to open.
    /// </summary>
    public static string NormalizePhysicalPath(string value)
    {
        // Pasted out of Explorer's address bar, which quotes a path containing spaces.
        var path = value.Trim().Trim('"').Trim();

        if (path.Length == 0)
        {
            return string.Empty;
        }

        // A UNC path's leading pair has to survive the collapse below, so it is taken off first.
        var isUnc = path.StartsWith(@"\\") || path.StartsWith("//");

        path = path.Replace('/', '\\').ToLowerInvariant();
        path = DoubleBackslashRegex().Replace(path, @"\");

        if (isUnc)
        {
            path = @"\" + path;
        }

        // `c:\` is a directory in its own right; `c:\inetpub\` is the same as `c:\inetpub`.
        return path.Length > 3 ? path.TrimEnd('\\') : path;
    }

    public static string? ValidatePhysicalPath(string name) =>
        PhysicalPathRegex().IsMatch(name)
            ? null
            : @"A physical path is absolute, e.g. c:\inetpub\callcenter or \\server\share.";

    /// <summary>
    /// A tag is a label to filter by, so it is one lower-case word or hyphenated words:
    /// <c>web</c>, <c>incoming-postal-web</c>. Spaces and underscores become hyphens rather than
    /// being refused — "incoming postal" is the same tag someone else typed with a hyphen.
    /// </summary>
    public static string NormalizeTag(string value)
    {
        var tag = SeparatorRegex().Replace(value.Trim().ToLowerInvariant(), "-");

        return tag.Trim('-');
    }

    public static string? ValidateTag(string name) =>
        TagRegex().IsMatch(name)
            ? null
            : "A tag is lower-case letters, digits and hyphens, e.g. incoming-postal-web.";

    [GeneratedRegex(@"^[A-Z0-9]([A-Z0-9-]*[A-Z0-9])?(\.[A-Z0-9]([A-Z0-9-]*[A-Z0-9])?)*$")]
    private static partial Regex MachineRegex();

    [GeneratedRegex(@"^[a-z0-9]([a-z0-9-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)*$")]
    private static partial Regex HostRegex();

    [GeneratedRegex(@"^/[^\s]*$")]
    private static partial Regex RootPathRegex();

    [GeneratedRegex(@"^(?:[a-z]:\\|\\\\[^\\]+\\)[^\\]*(?:\\[^\\]+)*$|^[a-z]:\\$")]
    private static partial Regex PhysicalPathRegex();

    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"/{2,}")]
    private static partial Regex DoubleSlashRegex();

    [GeneratedRegex(@"\\{2,}")]
    private static partial Regex DoubleBackslashRegex();

    [GeneratedRegex(@"[\s_]+")]
    private static partial Regex SeparatorRegex();
}
