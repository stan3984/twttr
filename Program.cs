
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using twttr.Configuration;
using twttr.Data;
using twttr.Infrastructure;
using twttr.Models;
using twttr.Storage;

namespace twttr;

public class Program
{
    private const string STORAGE_PROVIDER = "Storage:Provider";

    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var storageProvider = builder.Configuration[STORAGE_PROVIDER] ?? "postgres";
        switch (storageProvider.ToLower())
        {
            case "postgres":
                var connectionString = builder.Configuration.GetConnectionString("twttr")
                    ?? throw new InvalidOperationException("Missing ConnectionString for 'twttr'");
                builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
                builder.Services.AddScoped<IUserStore, PostgresUserStore>();
                builder.Services.AddScoped<IPostStore, PostgresPostStore>();
                break;
            default:
                throw new InvalidOperationException($"unknown {STORAGE_PROVIDER} '{storageProvider}'");
        }

        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        var rateLimits = builder.Configuration
            .GetSection(RateLimitingOptions.Section)
            .Get<RateLimitingOptions>() ?? new RateLimitingOptions();
        builder.Services.AddRateLimiter(options => RateLimitPolicies.Configure(options, rateLimits));

        builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        builder.Services.AddSingleton<IPasswordService, PasswordService>();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromDays(1);
                options.SlidingExpiration = true;

                options.Events.OnRedirectToLogin = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };

                options.Events.OnRedirectToAccessDenied = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });
        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build()
            );
        builder.Services.AddControllers();
        builder.Services.AddOpenApi();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            await using (var scope = app.Services.CreateAsyncScope())
            {
                // set up tables and run migrations
                await scope
                    .ServiceProvider
                    .GetRequiredService<AppDbContext>()
                    .Database
                    .MigrateAsync();
            }

            app.MapOpenApi().AllowAnonymous().DisableRateLimiting();
        }

        app.UseExceptionHandler();
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}
