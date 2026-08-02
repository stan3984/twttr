using twttr.Models;

namespace twttr.Storage;

public class MemoryUserStore : IUserStore
{
    // todo: switch to System.Threading.Lock instead of `lock (_dict)`
    private readonly Dictionary<Guid, User> _dict = [];

    public Task<User?> GetById(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_dict)
        {
            _dict.TryGetValue(id, out var user);
            return Task.FromResult(user?.Clone());
        }
    }

    public Task<User?> GetByUsername(string username, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_dict)
        {
            var user = _dict.Values.FirstOrDefault(u => u.Username == username);
            return Task.FromResult(user?.Clone());
        }
    }

    public Task<User?> GetByEmail(string email, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_dict)
        {
            var user = _dict.Values.FirstOrDefault(u => u.Email == email);
            return Task.FromResult(user?.Clone());
        }
    }

    public Task<bool> UsernameExists(string username, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_dict)
        {
            return Task.FromResult(_dict.Values.Any(u => u.Username == username));
        }
    }

    public Task<bool> EmailExists(string email, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_dict)
        {
            return Task.FromResult(_dict.Values.Any(u => u.Email == email));
        }
    }

    public Task<IReadOnlyList<User>> GetPage(int skip, int take, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_dict)
        {
            var list = (IReadOnlyList<User>)[.. _dict.Values
                .Select(u => u.Clone())
                .OrderByDescending(u => u.CreatedAt)
                .ThenBy(u => u.Id)
                .Skip(Math.Max(0, skip))
                .Take(Math.Clamp(take, 0, 100))];

            return Task.FromResult(list);
        }
    }

    public Task<User?> AddOne(NewUser data, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var newUser = new User
        {
            Id = Guid.Empty,
            Username = data.Username,
            DisplayName = data.Username,
            Email = data.Email,
            PasswordHash = data.PasswordHash,
            CreatedAt = DateTimeOffset.UnixEpoch,
        };

        lock (_dict)
        {
            if (_dict.Values.Any(u => u.Username == data.Username || u.Email == data.Email))
            {
                return Task.FromResult<User?>(null);
            }

            // TryAdd can only fail if there is a duplicate ID, so we loop until we find a unique ID.
            // As long as there are fewer than billions of users, it is unfathomably unlikely that
            // a collision occurs, so looping is fine.
            while (true)
            {
                newUser.Id = Guid.CreateVersion7();
                newUser.CreatedAt = DateTimeOffset.UtcNow;

                if (_dict.TryAdd(newUser.Id, newUser))
                {
                    return Task.FromResult<User?>(newUser.Clone());
                }
            }
        }
    }

    public Task<bool> UpdateOne(UpdateUser data, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_dict)
        {
            if (_dict.Values.Any(u => u.Email == data.Email))
            {
                return Task.FromResult(false);
            }

            if (_dict.TryGetValue(data.Id, out var user))
            {
                user.DisplayName = data.DisplayName ?? user.DisplayName;
                user.Email = data.Email ?? user.Email;
                user.PasswordHash = data.PasswordHash ?? user.PasswordHash;
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }

    public Task<bool> DeleteOne(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_dict)
        {
            return Task.FromResult(_dict.Remove(id));
        }
    }
}
