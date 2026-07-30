namespace Argus.Api.Configuration;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "Argus";

    public string Audience { get; set; } = "ArgusClients";

    /// <summary>
    /// Symmetric signing key. Supplied via configuration/environment, never committed —
    /// see `CLAUDE-dotnet.md` (appsettings are gitignored) and `secrets/`.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    public int TokenLifetimeMinutes { get; set; } = 480;
}
