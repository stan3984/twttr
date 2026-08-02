using Microsoft.EntityFrameworkCore;
using Npgsql;
using twttr.Models;
using twttr.Storage;

namespace twttr.Data;

public class PostgresUserStore(AppDbContext db) : IUserStore
{
    public Task<User?> GetById(Guid id, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByUsername(string username, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);

    public Task<User?> GetByEmail(string email, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<bool> UsernameExists(string username, CancellationToken ct = default)
        => db.Users.AsNoTracking().AnyAsync(u => u.Username == username, ct);

    public Task<bool> EmailExists(string email, CancellationToken ct = default)
        => db.Users.AsNoTracking().AnyAsync(u => u.Email == email, ct);

    public async Task<IReadOnlyList<User>> GetPage(int skip, int take, CancellationToken ct = default)
        => await db.Users.OrderByDescending(u => u.CreatedAt)
                         .ThenBy(u => u.Id)
                         .Skip(Math.Max(0, skip))
                         .Take(Math.Clamp(take, 0, 100))
                         .ToListAsync(ct);

    public async Task<User?> AddOne(NewUser data, CancellationToken ct = default)
    {
        try
        {
            var timestamp = DateTimeOffset.UtcNow;
            var user = new User
            {
                Id = Guid.CreateVersion7(timestamp),
                Username = data.Username,
                DisplayName = data.Username,
                Email = data.Email,
                PasswordHash = data.PasswordHash,
                CreatedAt = timestamp,
            };
            db.Users.Add(user);

            await db.SaveChangesAsync(ct);
            return user;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return null;
        }
    }

    public async Task<bool> UpdateOne(UpdateUser data, CancellationToken ct = default)
    {
        if (data.DisplayName == null && data.Email == null && data.PasswordHash == null)
        {
            return await db.Users.AnyAsync(u => u.Id == data.Id, ct);
        }

        try
        {
            var rows = await db.Users.Where(u => u.Id == data.Id).ExecuteUpdateAsync(s =>
            {
                if (data.DisplayName != null)
                {
                    s.SetProperty(u => u.DisplayName, data.DisplayName);
                }

                if (data.Email != null)
                {
                    s.SetProperty(u => u.Email, data.Email);
                }

                if (data.PasswordHash != null)
                {
                    s.SetProperty(u => u.PasswordHash, data.PasswordHash);
                }
            }, ct);

            return rows > 0;
        }
        catch (PostgresException e) when (e.SqlState is PostgresErrorCodes.UniqueViolation)
        {
            return false;
        }
    }

    public async Task<bool> DeleteOne(Guid id, CancellationToken ct = default)
        => await db.Users.Where(u => u.Id == id).ExecuteDeleteAsync(ct) > 0;
}
