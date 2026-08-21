using Microsoft.EntityFrameworkCore;

namespace twttr.Tests.Infrastructure;

[Collection(PostgresCollection.Name)]
public abstract class PostgresTest(PostgresFixture fixture) : IAsyncLifetime
{
    private readonly List<AppDbContext> _contexts = [];

    public async Task InitializeAsync()
    {
        await using var db = Fixture.CreateContext();
        await db.Database.ExecuteSqlRawAsync("""TRUNCATE "Posts", "Users";""");
    }

    public virtual async Task DisposeAsync()
        => _contexts.ForEach(async ctx => await ctx.DisposeAsync());

    protected PostgresFixture Fixture { get; } = fixture;

    protected PostgresUserStore UserStore
    {
        get
        {
            var db = Fixture.CreateContext();
            _contexts.Add(db);
            return new PostgresUserStore(db);
        }
    }

    protected PostgresPostStore PostStore
    {
        get
        {
            var db = Fixture.CreateContext();
            _contexts.Add(db);
            return new PostgresPostStore(db);
        }
    }

    protected async Task SeedUser(params User[] users)
    {
        await using var db = Fixture.CreateContext();
        db.Users.AddRange(users);
        await db.SaveChangesAsync();
    }

    protected async Task SeedPost(params Post[] posts)
    {
        await using var db = Fixture.CreateContext();
        db.Posts.AddRange(posts);
        await db.SaveChangesAsync();
    }
}
