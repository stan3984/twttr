namespace twttr.Storage;

using twttr.Models;

public interface IUserStore
{
    Task<User?> GetById(Guid id, CancellationToken ct = default);
    Task<User?> GetByUsername(string username, CancellationToken ct = default);
    Task<User?> GetByEmail(string email, CancellationToken ct = default);

    Task<bool> UsernameExists(string username, CancellationToken ct = default);
    Task<bool> EmailExists(string email, CancellationToken ct = default);

    Task<IReadOnlyList<User>> GetPage(int skip, int take, CancellationToken ct = default);

    Task<User?> AddOne(NewUser data, CancellationToken ct = default);
    Task<bool> UpdateOne(UpdateUser data, CancellationToken ct = default);
    Task<bool> DeleteOne(Guid id, CancellationToken ct = default);
}
