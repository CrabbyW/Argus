using Argus.Api.Services;

namespace Argus.Api.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void The_right_password_verifies()
    {
        var (hash, salt) = PasswordHasher.HashPassword("correct horse battery staple");

        Assert.True(PasswordHasher.Verify("correct horse battery staple", hash, salt));
    }

    [Fact]
    public void The_wrong_password_does_not()
    {
        var (hash, salt) = PasswordHasher.HashPassword("correct horse battery staple");

        Assert.False(PasswordHasher.Verify("Correct horse battery staple", hash, salt));
        Assert.False(PasswordHasher.Verify(string.Empty, hash, salt));
    }

    /// <summary>The salt is per password, so the same input never produces the same hash twice.</summary>
    [Fact]
    public void The_same_password_hashes_differently_every_time()
    {
        var first = PasswordHasher.HashPassword("same input");
        var second = PasswordHasher.HashPassword("same input");

        Assert.NotEqual(first.Hash, second.Hash);
        Assert.NotEqual(first.Salt, second.Salt);
    }

    /// <summary>A corrupt row must fail the login, not throw its way out to a 500.</summary>
    [Fact]
    public void A_malformed_stored_hash_fails_instead_of_throwing()
    {
        Assert.False(PasswordHasher.Verify("anything", "not base64!", "also not base64!"));
    }
}
