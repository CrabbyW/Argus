using System.ComponentModel.DataAnnotations;

namespace Argus.Api.WebApiPoco.Auth;

public class LoginRequestDto
{
    [Required]
    [StringLength(128, MinimumLength = 1)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(256, MinimumLength = 1)]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresUtc { get; set; }

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}

public class CurrentUserDto
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
}
