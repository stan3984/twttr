using System.Net;
using twttr.Tests.Infrastructure;

namespace twttr.Tests;

public class UsersControllerTest(PostgresFixture fixture) : WebTest(fixture)
{
    protected static Task<HttpResponseMessage> GetUser(HttpClient client, Guid userId)
        => client.GetAsync($"/api/users/{userId}");

    protected static Task<HttpResponseMessage> DeleteUser(HttpClient client, Guid userId)
        => client.DeleteAsync($"/api/users/{userId}");

    [Fact]
    public async Task GetById_without_cookie_returns_401()
    {
        var user = await SeedUser();
        var response = await GetUser(HttpClient(), user.Id);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_without_cookie_returns_401()
    {
        var user = await SeedUser();
        var response = await DeleteUser(HttpClient(), user.Id);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_without_cookie_does_not_delete()
    {
        var user = await SeedUser();
        await DeleteUser(HttpClient(), user.Id);

        Assert.NotNull(await UserStore.GetById(user.Id));
    }

    [Fact]
    public async Task GetById_returns_current_user()
    {
        var client = HttpClient();
        var user = await SeedUser();
        await Login(client);

        var response = await GetUser(client, user.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(user.Id.ToString(), await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetById_returns_another_user()
    {
        await SeedUser("alice");
        var user2 = await SeedUser("bob");
        var client = HttpClient();

        await Login(client, "alice");

        var response = await GetUser(client, user2.Id);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(user2.Id.ToString(), content);
        Assert.Contains(user2.Username, content);
        Assert.Contains(user2.DisplayName, content);
    }

    [Fact]
    public async Task GetById_does_not_expose_password()
    {
        var password = "bob's password";
        await SeedUser();
        var user2 = await SeedUser("bob", password);
        var client = HttpClient();

        await Login(client);

        var response = await GetUser(client, user2.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(password, content);
        Assert.DoesNotContain(user2.PasswordHash, content);
    }

    [Fact]
    public async Task GetById_does_not_expose_email()
    {
        await SeedUser();
        var user2 = await SeedUser("bob");
        var client = HttpClient();
        await Login(client);

        var response = await GetUser(client, user2.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(user2.Email, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetById_with_unknown_id_returns_404()
    {
        await SeedUser();
        var client = HttpClient();
        await Login(client);

        var needle = Guid.CreateVersion7();
        var response = await GetUser(client, needle);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(needle.ToString(), await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetById_with_malformed_id_returns_404()
    {
        await SeedUser();
        var client = HttpClient();
        await Login(client);

        var response = await client.GetAsync("/api/users/12345XnotXaXguidX67890");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_with_deleted_user_returns_404()
    {
        var client1 = HttpClient();
        var user1 = await SeedUser("alice");
        await Login(client1, "alice");
        await DeleteUser(client1, user1.Id);

        var client2 = HttpClient();
        await SeedUser("bob");
        await Login(client2, "bob");

        var response = await GetUser(client2, user1.Id);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_self_returns_404()
    {
        var client = HttpClient();
        var user = await SeedUser("alice");
        await Login(client, "alice");
        var response = await DeleteUser(client, user.Id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_other_user_returns_403()
    {
        var client1 = HttpClient();
        var user1 = await SeedUser("alice");
        await Login(client1, "alice");
        await DeleteUser(client1, user1.Id);

        var client2 = HttpClient();
        await SeedUser("bob");
        await Login(client2, "bob");

        var response = await DeleteUser(client2, user1.Id);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_other_user_does_not_delete()
    {
        var client1 = HttpClient();
        var user1 = await SeedUser("alice");
        await Login(client1, "alice");
        await DeleteUser(client1, user1.Id);

        var client2 = HttpClient();
        await SeedUser("bob");
        await Login(client2, "bob");

        await DeleteUser(client2, user1.Id);
        Assert.NotNull(UserStore.GetById(user1.Id));
    }

    [Fact]
    public async Task Delete_user_deletes_posts()
    {
        var user = await SeedUser("alice");
        var post1 = await SeedPost(user.Id);
        var post2 = await SeedPost(user.Id);
        var client = HttpClient();
        await Login(client, "alice");
        await DeleteUser(client, user.Id);

        Assert.Null(await PostStore.GetById(post1.Id));
        Assert.Null(await PostStore.GetById(post2.Id));
    }

    [Fact]
    public async Task Delete_keeps_other_posts()
    {
        var user1 = await SeedUser("alice");
        var user2 = await SeedUser("bob");
        var post1 = await SeedPost(user2.Id);
        var post2 = await SeedPost(user2.Id);

        var client1 = HttpClient();
        await Login(client1, "alice");
        await DeleteUser(client1, user1.Id);

        Assert.NotNull(await PostStore.GetById(post1.Id));
        Assert.NotNull(await PostStore.GetById(post2.Id));
    }
}
