using twttr.Tests.Infrastructure;

namespace twttr.Tests;

public class PostgresUserStoreTest(PostgresFixture fixture) : PostgresTest(fixture)
{
    private static NewUser NewUserData(string username = "user") => new()
    {
        Username = username,
        Email = $"{username}@example.com",
        PasswordHash = "not-a-real-hash",
    };

    private static User UserAt(DateTimeOffset createdAt, string username, Guid? id = null) => new()
    {
        Id = id ?? Guid.CreateVersion7(),
        Username = username,
        DisplayName = username,
        Email = $"{username}@example.com",
        PasswordHash = "not-a-real-hash",
        CreatedAt = createdAt,
    };

    // AddOne

    [Fact]
    public async Task AddOne_persists_and_returns_the_user()
    {
        var created = await UserStore.AddOne(NewUserData("alice"));

        Assert.NotNull(created);

        var found = await UserStore.GetById(created.Id);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
        Assert.Equal("alice", found.Username);
        Assert.Equal("alice@example.com", found.Email);
        Assert.Equal("not-a-real-hash", found.PasswordHash);
    }

    [Fact]
    public async Task AddOne_assigns_a_version_7_id()
    {
        var created = await UserStore.AddOne(NewUserData());

        Assert.NotNull(created);
        Assert.Equal(7, created.Id.Version);
    }

    [Fact]
    public async Task AddOne_defaults_DisplayName_to_Username()
    {
        var created = await UserStore.AddOne(NewUserData("alice"));
        Assert.NotNull(created);

        var found = await UserStore.GetById(created.Id);
        Assert.NotNull(found);
        Assert.Equal("alice", found.DisplayName);
    }

    [Fact]
    public async Task AddOne_sets_CreatedAt_to_utc_now()
    {
        var before = DateTimeOffset.UtcNow;
        var created = await UserStore.AddOne(NewUserData());
        var after = DateTimeOffset.UtcNow;

        Assert.NotNull(created);

        var found = await UserStore.GetById(created.Id);

        Assert.NotNull(found);
        Assert.InRange(found.CreatedAt, before, after);
        Assert.Equal(TimeSpan.Zero, found.CreatedAt.Offset);
    }

    [Fact]
    public async Task AddOne_duplicate_username_returns_null()
    {
        Assert.NotNull(await UserStore.AddOne(NewUserData("alice")));

        var duplicate = new NewUser
        {
            Username = "alice",
            Email = "someone-else@example.com",
            PasswordHash = "not-a-real-hash",
        };

        Assert.Null(await UserStore.AddOne(duplicate));
    }

    [Fact]
    public async Task AddOne_duplicate_email_returns_null()
    {
        Assert.NotNull(await UserStore.AddOne(NewUserData("alice")));

        var duplicate = new NewUser
        {
            Username = "bob",
            Email = "alice@example.com",
            PasswordHash = "not-a-real-hash",
        };

        Assert.Null(await UserStore.AddOne(duplicate));
    }

    [Fact]
    public async Task AddOne_duplicate_does_not_persist()
    {
        await UserStore.AddOne(NewUserData("alice"));
        await UserStore.AddOne(NewUserData("alice"));

        var all = await UserStore.GetPage(0, 100);

        Assert.Single(all);
    }

    [Fact]
    public async Task GetById_returns_null_when_absent()
    {
        Assert.Null(await UserStore.GetById(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task GetByUsername_returns_the_user()
    {
        var created = await UserStore.AddOne(NewUserData("alice"));

        var found = await UserStore.GetByUsername("alice");

        Assert.NotNull(found);
        Assert.Equal(created!.Id, found.Id);
    }

    [Fact]
    public async Task GetByUsername_returns_null_when_absent()
    {
        Assert.Null(await UserStore.GetByUsername("nobody"));
    }

    [Fact]
    public async Task GetByUsername_is_case_sensitive()
    {
        await UserStore.AddOne(NewUserData("alice"));

        // Postgres '=' on varchar is case-sensitive.
        Assert.Null(await UserStore.GetByUsername("ALICE"));
    }

    [Fact]
    public async Task GetByEmail_returns_the_user()
    {
        var created = await UserStore.AddOne(NewUserData("alice"));

        var found = await UserStore.GetByEmail("alice@example.com");

        Assert.NotNull(found);
        Assert.Equal(created!.Id, found.Id);
    }

    [Fact]
    public async Task GetByEmail_returns_null_when_absent()
    {
        Assert.Null(await UserStore.GetByEmail("nobody@example.com"));
    }

    [Fact]
    public async Task UsernameExists_reflects_stored_users()
    {
        await UserStore.AddOne(NewUserData("alice"));

        Assert.True(await UserStore.UsernameExists("alice"));
        Assert.False(await UserStore.UsernameExists("bob"));
    }

    [Fact]
    public async Task EmailExists_reflects_stored_users()
    {
        await UserStore.AddOne(NewUserData("alice"));

        Assert.True(await UserStore.EmailExists("alice@example.com"));
        Assert.False(await UserStore.EmailExists("bob@example.com"));
    }

    [Fact]
    public async Task GetPage_orders_newest_first()
    {
        var t = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        await SeedUser(
            UserAt(t, "oldest"),
            UserAt(t.AddHours(2), "newest"),
            UserAt(t.AddHours(1), "middle"));

        var page = await UserStore.GetPage(0, 10);

        Assert.Equal(["newest", "middle", "oldest"], page.Select(u => u.Username));
    }

    [Fact]
    public async Task GetPage_breaks_ties_by_id()
    {
        var t = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        var low = Guid.CreateVersion7();
        await Task.Delay(10);
        var high = Guid.CreateVersion7();

        await SeedUser(
            UserAt(t, "high", high),
            UserAt(t, "low", low));

        var page = await UserStore.GetPage(0, 10);

        Assert.Equal(["low", "high"], page.Select(u => u.Username));
    }

    [Fact]
    public async Task GetPage_pages_without_overlap()
    {
        var t = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        await SeedUser([.. Enumerable.Range(0, 5).Select(i => UserAt(t.AddHours(i), $"user{i}"))]);

        var first = await UserStore.GetPage(0, 2);
        var second = await UserStore.GetPage(2, 2);
        var third = await UserStore.GetPage(4, 2);

        Assert.Equal(2, first.Count);
        Assert.Equal(2, second.Count);
        Assert.Single(third);

        var seen = first.Concat(second).Concat(third).Select(u => u.Username).ToList();

        Assert.Equal(5, seen.Distinct().Count());
        Assert.Equal(["user4", "user3", "user2", "user1", "user0"], seen);
    }

    [Fact]
    public async Task GetPage_clamps_take_to_100()
    {
        var t = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        await SeedUser([.. Enumerable.Range(0, 110).Select(i => UserAt(t.AddMinutes(i), $"user{i}"))]);

        var page = await UserStore.GetPage(0, 500);

        Assert.Equal(100, page.Count);
    }

    [Fact]
    public async Task GetPage_with_take_zero_returns_empty()
    {
        await UserStore.AddOne(NewUserData("alice"));
        Assert.Empty(await UserStore.GetPage(0, 0));
    }

    [Fact]
    public async Task UpdateOne_updates_only_supplied_fields()
    {
        var created = await UserStore.AddOne(NewUserData("alice"));
        Assert.NotNull(created);

        var updated = await UserStore.UpdateOne(new UpdateUser
        {
            Id = created!.Id,
            DisplayName = "Alice A.",
        });
        Assert.True(updated);

        var found = await UserStore.GetById(created.Id);
        Assert.NotNull(found);
        Assert.Equal("Alice A.", found.DisplayName);
        Assert.Equal(created.Email, found.Email);
        Assert.Equal(created.PasswordHash, found.PasswordHash);
    }

    [Fact]
    public async Task UpdateOne_with_no_fields_set_returns_true()
    {
        var created = await UserStore.AddOne(NewUserData("alice"));
        var updated = await UserStore.UpdateOne(new UpdateUser { Id = created!.Id });

        Assert.True(updated);

        var found = await UserStore.GetById(created.Id);

        Assert.NotNull(found);
        Assert.Equal(created.DisplayName, found.DisplayName);
        Assert.Equal(created.Email, found.Email);
    }

    [Fact]
    public async Task UpdateOne_returns_false_for_unknown_id()
    {
        var updated = await UserStore.UpdateOne(new UpdateUser
        {
            Id = Guid.CreateVersion7(),
            DisplayName = "Nobody",
        });

        Assert.False(updated);
    }

    [Fact]
    public async Task UpdateOne_returns_false_on_duplicate_email()
    {
        var alice = await UserStore.AddOne(NewUserData("alice"));
        var bob = await UserStore.AddOne(NewUserData("bob"));

        Assert.NotNull(alice);
        Assert.NotNull(bob);

        var updated = await UserStore.UpdateOne(new UpdateUser
        {
            Id = alice.Id,
            Email = bob.Email,
        });

        Assert.False(updated);
    }

    [Fact]
    public async Task DeleteOne_removes_the_user_and_returns_true()
    {
        var created = await UserStore.AddOne(NewUserData("alice"));

        Assert.True(await UserStore.DeleteOne(created!.Id));
        Assert.Null(await UserStore.GetById(created.Id));
    }

    [Fact]
    public async Task DeleteOne_returns_false_for_unknown_id()
    {
        Assert.False(await UserStore.DeleteOne(Guid.CreateVersion7()));
    }
}
