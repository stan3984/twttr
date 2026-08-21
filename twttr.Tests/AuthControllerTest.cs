using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using twttr.Tests.Infrastructure;

namespace twttr.Tests;

public class AuthControllerTest(PostgresFixture fixture) : WebTest(fixture)
{
    private const string SessionCookie = ".AspNetCore.Cookies";

    private static string SetCookieHeader(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var values), "expected a Set-Cookie header");
        return Assert.Single(values!, v => v.StartsWith(SessionCookie));
    }

    private static bool HasSetCookie(HttpResponseMessage response)
        => response.Headers.TryGetValues("Set-Cookie", out var values)
           && values.Any(v => v.StartsWith(SessionCookie));

    // Register

    [Fact]
    public async Task Register_creates_the_user()
    {
        var response = await Register(HttpClient());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var user = await UserStore.GetByUsername(ValidUsername);
        Assert.NotNull(user);
        Assert.Equal(ValidEmail, user.Email);
        Assert.Equal(ValidUsername, user.DisplayName);
    }

    [Fact]
    public async Task Register_signs_the_user_in()
    {
        var client = HttpClient();

        var response = await Register(client);
        Assert.True(HasSetCookie(response));

        // The cookie from registering must be enough to reach an authorized endpoint.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(API_AUTH_ME)).StatusCode);
    }

    [Fact]
    public async Task Register_stores_a_hash_not_the_password()
    {
        await Register(HttpClient());

        var user = await UserStore.GetByUsername(ValidUsername);
        Assert.NotNull(user);
        Assert.NotEqual(ValidPassword, user.PasswordHash);
        Assert.Equal(
            PasswordVerificationResult.Success,
            PasswordService.Verify(user.PasswordHash, ValidPassword));
    }

    [Fact]
    public async Task Register_duplicate_username_returns_409()
    {
        await Register(HttpClient());

        var response = await Register(HttpClient(), email: "other@example.com");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_duplicate_email_returns_409()
    {
        await Register(HttpClient());

        var response = await Register(HttpClient(), username: "bobbytables");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_short_username()
    {
        var response = await Register(HttpClient(), username: "alicesm");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_accepts_a_minimum_length_username()
    {
        var response = await Register(HttpClient(), username: "alicesmi");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_long_username()
    {
        var response = await Register(HttpClient(), username: new string('a', 25));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_non_alphanumeric_username()
    {
        var response = await Register(HttpClient(), username: "alice_smith");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_short_password()
    {
        var response = await Register(HttpClient(), password: new string('p', 11));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_long_password()
    {
        var response = await Register(HttpClient(), password: new string('p', 65));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_invalid_email()
    {
        var response = await Register(HttpClient(), email: "not-an-email");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_missing_fields()
    {
        var response = await HttpClient().PostAsJsonAsync(API_AUTH_REGISTER, new { Username = ValidUsername });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_does_not_persist_on_invalid_input()
    {
        await Register(HttpClient(), username: "bad");

        Assert.Empty(await UserStore.GetPage(0, 100));
    }

    // Login

    [Fact]
    public async Task Login_with_valid_credentials_returns_204()
    {
        await SeedUser();

        var response = await Login(HttpClient());

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Login_sets_an_httponly_secure_strict_cookie()
    {
        await SeedUser();

        var cookie = SetCookieHeader(await Login(HttpClient()));

        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_with_unknown_username_returns_401()
    {
        var response = await Login(HttpClient(), username: "nobodyhere");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        await SeedUser();

        var response = await Login(HttpClient(), password: "wrong password here");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_does_not_set_a_cookie_on_failure()
    {
        await SeedUser();

        var response = await Login(HttpClient(), password: "wrong password here");

        Assert.False(HasSetCookie(response));
    }

    [Fact]
    public async Task Login_is_case_sensitive_on_username()
    {
        await SeedUser();

        var response = await Login(HttpClient(), username: "AliceSmith");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ignores_the_password_policy()
    {
        await SeedUser(password: "short");

        var response = await Login(HttpClient(), password: "short");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // Logout

    [Fact]
    public async Task Logout_clears_the_session_cookie()
    {
        var client = await SignedInClient();

        var response = await client.PostAsync(API_AUTH_LOGOUT, null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains("expires=Thu, 01 Jan 1970", SetCookieHeader(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Logout_makes_me_return_401()
    {
        var client = await SignedInClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(API_AUTH_ME)).StatusCode);

        await client.PostAsync(API_AUTH_LOGOUT, null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(API_AUTH_ME)).StatusCode);
    }

    [Fact]
    public async Task Logout_without_a_session_returns_204()
    {
        var response = await HttpClient().PostAsync(API_AUTH_LOGOUT, null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // Identify

    [Fact]
    public async Task Identify_returns_the_current_user()
    {
        var user = await SeedUser();
        var client = HttpClient();
        await Login(client);

        var body = await client.GetFromJsonAsync<IdentifyResponseDto>(API_AUTH_ME);

        Assert.NotNull(body);
        Assert.Equal(user.Id, body.Id);
        Assert.Equal(ValidUsername, body.Username);
        Assert.Equal(ValidUsername, body.DisplayName);
    }

    [Fact]
    public async Task Identify_without_a_cookie_returns_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await HttpClient().GetAsync(API_AUTH_ME)).StatusCode);
    }

    [Fact]
    public async Task Identify_with_a_tampered_cookie_returns_401()
    {
        var client = HttpClient(handleCookies: false);
        client.DefaultRequestHeaders.Add("Cookie", $"{SessionCookie}=not-a-real-ticket");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(API_AUTH_ME)).StatusCode);
    }

    [Fact]
    public async Task Identify_returns_401_after_the_user_is_deleted()
    {
        var user = await SeedUser();
        var client = HttpClient();
        await Login(client);

        Assert.True(await UserStore.DeleteOne(user.Id));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(API_AUTH_ME)).StatusCode);
    }

    [Fact]
    public async Task Identify_401_is_not_a_redirect()
    {
        var response = await HttpClient().GetAsync(API_AUTH_ME);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    // Routing

    [Fact]
    public async Task Login_is_not_reachable_by_get()
    {
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await HttpClient().GetAsync(API_AUTH_LOGIN)).StatusCode
        );
    }
}
