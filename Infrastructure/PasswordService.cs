using Microsoft.AspNetCore.Identity;
using twttr.Models;

namespace twttr.Infrastructure;

public interface IPasswordService
{
    string Hash(string password);

    PasswordVerificationResult Verify(string? hash, string password);
}

public class PasswordService(IPasswordHasher<User> hasher) : IPasswordService
{
    private readonly Lazy<string> _dummyHash = new(
        () => hasher.HashPassword(null!, "this is not a real password"),
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    public string Hash(string password)
    {
        // The default implementation of `IPasswordHasher` does not care about the first argument,
        // so null is fine.
        return hasher.HashPassword(null!, password);
    }

    public PasswordVerificationResult Verify(string? hash, string password)
    {
        // Prevent timing attacks by processing a hash even if there is no hash provided.
        if (hash is null)
        {
            hasher.VerifyHashedPassword(null!, _dummyHash.Value, password);
            return PasswordVerificationResult.Failed;
        }

        return hasher.VerifyHashedPassword(null!, hash, password);
    }
}
