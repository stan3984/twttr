using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;

namespace twttr.Tests.Infrastructure;

public abstract class WebTest(PostgresFixture fixture) : PostgresTest(fixture)
{
    public const string API_AUTH_LOGIN = "/api/auth/login";
    public const string API_AUTH_LOGOUT = "/api/auth/logout";
    public const string API_AUTH_ME = "/api/auth/me";
    public const string API_AUTH_REGISTER = "/api/auth/register";
    public const string API_POSTS = "/api/posts";

    protected static readonly PasswordHasher<User> Hasher = new();

    protected const string ValidUsername = "sherlockholmes";
    protected const string ValidPassword = "JBfyct38vf61hdk8rg7d";
    protected const string ValidEmail = "sherlock@example.com";

    private readonly List<HttpClient> _clients = [];

    public override async Task DisposeAsync()
    {
        _clients.ForEach(c => c.Dispose());
        await base.DisposeAsync();
    }

    protected HttpClient HttpClient(bool handleCookies = true)
    {
        var client = Fixture.App.CreateClient(new WebApplicationFactoryClientOptions
        {
            // the session cookie is configured CookieSecurePolicy.Always and HttpClient refuses
            // to send a secure cookie over http, so this has to be https.
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = handleCookies,
        });
        _clients.Add(client);
        return client;
    }

    protected static Task<HttpResponseMessage> Register(HttpClient client, string username = ValidUsername, string password = ValidPassword, string email = ValidEmail)
        => client.PostAsJsonAsync(API_AUTH_REGISTER, new { Username = username, Password = password, Email = email });

    protected static Task<HttpResponseMessage> Login(HttpClient client, string username = ValidUsername, string password = ValidPassword)
        => client.PostAsJsonAsync(API_AUTH_LOGIN, new { Username = username, Password = password });

    protected async Task<User> SeedUser(string username = ValidUsername, string password = ValidPassword, string? passwordHash = null)
    {
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Username = username,
            DisplayName = username,
            Email = $"{username}@example.com",
            PasswordHash = passwordHash ?? Hasher.HashPassword(null!, password),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await SeedUser(user);
        return user;
    }

    protected async Task<Post> SeedPost(Guid authorId, string content = "hello world", Guid? inReplyToId = null)
    {
        var post = new Post
        {
            Id = Guid.CreateVersion7(),
            Content = content,
            CreatedAt = DateTimeOffset.UtcNow,
            AuthorId = authorId,
            InReplyToId = inReplyToId,
        };

        await SeedPost(post);
        return post;
    }

    // logs in a user and returns the client carrying the session cookie
    protected async Task<HttpClient> SignedInClient(string username = ValidUsername, string password = ValidPassword)
        => (await SignedInClientUser(username, password)).Client;

    // logs in a user and returns the client together with the user
    protected async Task<(HttpClient Client, User User)> SignedInClientUser(string username = ValidUsername, string password = ValidPassword)
    {
        var user = await SeedUser(username, password);
        var client = HttpClient();
        var response = await Login(client, username, password);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return (client, user);
    }
}
