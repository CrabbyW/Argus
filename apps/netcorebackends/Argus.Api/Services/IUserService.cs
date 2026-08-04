using Argus.Api.WebApiPoco.Users;

namespace Argus.Api.Services;

public interface IUserService
{
    /// <summary>Enabled users only unless <paramref name="includeDisabled"/> is set.</summary>
    Task<IReadOnlyList<UserDto>> GetAllAsync(bool includeDisabled);

    /// <summary>Finds a user regardless of <c>IsEnabled</c>, so a disabled one can be restored.</summary>
    Task<UserDto?> GetByIdAsync(int id);

    /// <exception cref="ArgumentException">Username taken, or the password is too short.</exception>
    Task<UserDto> CreateAsync(UserUpsertDto dto);

    /// <exception cref="ArgumentException">Username taken by another user.</exception>
    Task<UserDto?> UpdateAsync(int id, UserUpsertDto dto);

    /// <exception cref="ArgumentException">The password is too short.</exception>
    Task<bool> SetPasswordAsync(int id, string password);

    /// <summary>
    /// Soft delete. <paramref name="actingUsername"/> is the caller, so the service can refuse to
    /// let anyone disable their own account.
    /// </summary>
    /// <exception cref="ArgumentException">Disabling yourself, or the last enabled user.</exception>
    Task<bool> DisableAsync(int id, string actingUsername);

    Task<bool> RestoreAsync(int id);
}
