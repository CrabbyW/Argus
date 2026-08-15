using Argus.Api.Services.Lookups;

namespace Argus.Api.Tests;

/// <summary>
/// The lookups with a format store one shape of value and refuse the rest.
///
/// A lookup row is pointed at by dozens of installations, so a value written the wrong way is not
/// untidy — it is a second row for something the inventory already has, and half the filters then
/// miss half the answers. These are the cases that produced that: the same path in another case,
/// a trailing separator, a quoted paste out of Explorer, a sentence typed where a host name
/// belongs.
/// </summary>
public class LookupFormatTests
{
    [Theory]
    [InlineData("gaiis1", "GAIIS1")]
    [InlineData("  gaiis1  ", "GAIIS1")]
    [InlineData("Paha.ga.local", "PAHA.GA.LOCAL")]
    public void Machine_names_are_stored_upper_case(string input, string expected) =>
        Assert.Equal(expected, LookupFormats.NormalizeMachine(input));

    [Theory]
    [InlineData("GAIIS1")]
    [InlineData("SERVER6354654")]
    [InlineData("PAHA.GA.LOCAL")]
    public void A_host_name_is_a_valid_machine(string name) =>
        Assert.Null(LookupFormats.ValidateMachine(name));

    [Theory]
    [InlineData("THE OLD SERVER")]
    [InlineData(@"C:\INETPUB")]
    [InlineData("-GAIIS1")]
    public void Anything_that_is_not_a_host_name_is_refused(string name) =>
        Assert.NotNull(LookupFormats.ValidateMachine(name));

    /// <summary>
    /// Normalization strips a URL down to its host, but it cannot tell a host name from a
    /// sentence — "the paha server" comes through unchanged, and only this check refuses it.
    /// </summary>
    [Theory]
    [InlineData("the paha server")]
    [InlineData("paha_ga_local")]
    [InlineData("-paha.ga.local")]
    public void A_dns_endpoint_that_is_not_a_host_name_is_refused(string name) =>
        Assert.NotNull(LookupFormats.ValidateDnsName(DnsName.Normalize(name)));

    [Theory]
    [InlineData("https://PAHA.ga.local:8080/api/")]
    [InlineData("paha.ga.local.")]
    public void A_pasted_address_still_passes(string name) =>
        Assert.Null(LookupFormats.ValidateDnsName(DnsName.Normalize(name)));

    [Theory]
    [InlineData("callcenter.rc0", "/callcenter.rc0")]
    [InlineData("/worker/", "/worker")]
    [InlineData(@"\worker", "/worker")]
    [InlineData("//worker//sub/", "/worker/sub")]
    [InlineData("/CallCenter.RC0", "/callcenter.rc0")]
    public void Root_paths_are_stored_as_one_url_path(string input, string expected) =>
        Assert.Equal(expected, LookupFormats.NormalizeRootPath(input));

    /// <summary>The site root is the one path that is a single slash and keeps it.</summary>
    [Fact]
    public void The_site_root_survives_the_trailing_slash_rule() =>
        Assert.Equal("/", LookupFormats.NormalizeRootPath("/"));

    [Fact]
    public void A_root_path_with_a_space_is_refused() =>
        Assert.NotNull(LookupFormats.ValidateRootPath("/call center"));

    [Theory]
    [InlineData(@"C:\Inetpub\CallCenter", @"c:\inetpub\callcenter")]
    [InlineData(@"c:\inetpub\callcenter\", @"c:\inetpub\callcenter")]
    [InlineData(@"""c:\inetpub\call center""", @"c:\inetpub\call center")]
    [InlineData("c:/inetpub/callcenter", @"c:\inetpub\callcenter")]
    [InlineData(@"\\server\share\app\", @"\\server\share\app")]
    public void Physical_paths_are_stored_one_way(string input, string expected) =>
        Assert.Equal(expected, LookupFormats.NormalizePhysicalPath(input));

    /// <summary>A drive root is a directory in its own right, so it keeps its backslash.</summary>
    [Fact]
    public void A_drive_root_keeps_its_separator() =>
        Assert.Equal(@"c:\", LookupFormats.NormalizePhysicalPath(@"C:\"));

    [Theory]
    [InlineData("inetpub/callcenter")]
    [InlineData("callcenter")]
    public void A_relative_path_is_refused(string input) =>
        Assert.NotNull(LookupFormats.ValidatePhysicalPath(LookupFormats.NormalizePhysicalPath(input)));

    [Theory]
    [InlineData("Incoming Postal Web", "incoming-postal-web")]
    [InlineData("incoming_postal_web", "incoming-postal-web")]
    [InlineData("  WEB  ", "web")]
    public void Tags_are_stored_lower_case_and_hyphenated(string input, string expected) =>
        Assert.Equal(expected, LookupFormats.NormalizeTag(input));

    [Fact]
    public void A_tag_that_normalization_cannot_rescue_is_refused() =>
        Assert.NotNull(LookupFormats.ValidateTag(LookupFormats.NormalizeTag("web/prod")));
}
