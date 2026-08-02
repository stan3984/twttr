using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace twttr.Tests.Infrastructure;

public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("twttr-test")
        .Build();

    private AppFactory? _app;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    public AppDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_container.GetConnectionString()).Options);


    public AppFactory App
        => _app ??= new AppFactory(_container.GetConnectionString());
}
