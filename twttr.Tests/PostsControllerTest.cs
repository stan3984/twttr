using System.Net;
using System.Net.Http.Json;
using twttr.Tests.Infrastructure;

namespace twttr.Tests;

public class PostsControllerTest(PostgresFixture fixture) : WebTest(fixture)
{
    private const string OtherUsername = "watsonjohn";

    private static Task<HttpResponseMessage> CreatePost(HttpClient client, string content = "hello world", Guid? inReplyToId = null)
        => client.PostAsJsonAsync(API_POSTS, new { Content = content, InReplyToId = inReplyToId });

    private static Task<HttpResponseMessage> UpdatePost(HttpClient client, Guid postId, string content)
        => client.PatchAsJsonAsync($"{API_POSTS}/{postId}", new { Content = content });

    private static async Task<PostResponseDto> ReadPost(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<PostResponseDto>();
        Assert.NotNull(body);
        return body;
    }

    private static Post PostAt(DateTimeOffset createdAt, Guid authorId, string content) => new()
    {
        Id = Guid.CreateVersion7(),
        Content = content,
        CreatedAt = createdAt,
        AuthorId = authorId,
    };

    // Authorization

    [Fact]
    public async Task GetPage_without_a_cookie_returns_401()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await HttpClient().GetAsync(API_POSTS)).StatusCode);

    [Fact]
    public async Task GetById_without_a_cookie_returns_401()
    {
        var author = await SeedUser();
        var post = await SeedPost(author.Id);

        var response = await HttpClient().GetAsync($"{API_POSTS}/{post.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetReplies_without_a_cookie_returns_401()
    {
        var author = await SeedUser();
        var post = await SeedPost(author.Id);

        var response = await HttpClient().GetAsync($"{API_POSTS}/{post.Id}/replies");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_without_a_cookie_returns_401()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await CreatePost(HttpClient())).StatusCode);

    [Fact]
    public async Task Delete_without_a_cookie_returns_401()
    {
        var author = await SeedUser();
        var post = await SeedPost(author.Id);

        var response = await HttpClient().DeleteAsync($"{API_POSTS}/{post.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_without_a_cookie_does_not_persist()
    {
        await CreatePost(HttpClient());

        Assert.Empty(await PostStore.GetPage(0, 100));
    }

    // Create

    [Fact]
    public async Task Create_returns_201_and_the_post()
    {
        var (client, user) = await SignedInClientUser();

        var response = await CreatePost(client, "hello world");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await ReadPost(response);
        Assert.Equal("hello world", body.Content);
        Assert.Equal(user.Id, body.AuthorId);
        Assert.Null(body.UpdatedAt);
        Assert.Null(body.InReplyToId);
    }

    [Fact]
    public async Task Create_persists_the_post()
    {
        var (client, _) = await SignedInClientUser();

        var body = await ReadPost(await CreatePost(client, "hello world"));

        var stored = await PostStore.GetById(body.Id);

        Assert.NotNull(stored);
        Assert.Equal("hello world", stored.Content);
    }

    [Fact]
    public async Task Create_points_Location_at_the_new_post()
    {
        var (client, _) = await SignedInClientUser();

        var response = await CreatePost(client);
        var body = await ReadPost(response);

        Assert.NotNull(response.Headers.Location);

        // The Location header has to be fetchable with the same session.
        var followed = await client.GetAsync(response.Headers.Location);

        Assert.Equal(HttpStatusCode.OK, followed.StatusCode);
        Assert.Equal(body.Id, (await ReadPost(followed)).Id);
    }

    [Fact]
    public async Task Create_takes_the_author_from_the_session()
    {
        var (client, user) = await SignedInClientUser();

        var body = await ReadPost(await CreatePost(client));

        var stored = await PostStore.GetById(body.Id);

        Assert.NotNull(stored);
        Assert.Equal(user.Id, stored.AuthorId);
    }

    [Fact]
    public async Task Create_ignores_an_author_id_in_the_body()
    {
        var (client, user) = await SignedInClientUser();
        var other = await SeedUser(OtherUsername);

        // AuthorId is not part of the request contract; the session decides authorship.
        var response = await client.PostAsJsonAsync(API_POSTS, new
        {
            Content = "hello world",
            AuthorId = other.Id,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var stored = await PostStore.GetById((await ReadPost(response)).Id);

        Assert.NotNull(stored);
        Assert.Equal(user.Id, stored.AuthorId);
    }

    [Fact]
    public async Task Create_rejects_missing_content()
    {
        var (client, _) = await SignedInClientUser();

        // Content is a required property, so binding fails before the action runs.
        var response = await client.PostAsJsonAsync(API_POSTS, new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_rejects_empty_content()
    {
        var (client, _) = await SignedInClientUser();

        Assert.Equal(HttpStatusCode.BadRequest, (await CreatePost(client, "")).StatusCode);
    }

    [Fact]
    public async Task Create_rejects_single_character_content()
    {
        var (client, _) = await SignedInClientUser();

        // The minimum is two characters.
        Assert.Equal(HttpStatusCode.BadRequest, (await CreatePost(client, "a")).StatusCode);
    }

    [Fact]
    public async Task Create_accepts_minimum_length_content()
    {
        var (client, _) = await SignedInClientUser();

        Assert.Equal(HttpStatusCode.Created, (await CreatePost(client, "hi")).StatusCode);
    }

    [Fact]
    public async Task Create_rejects_whitespace_only_content()
    {
        var (client, _) = await SignedInClientUser();

        Assert.Equal(HttpStatusCode.BadRequest, (await CreatePost(client, "   ")).StatusCode);
    }

    [Fact]
    public async Task Create_rejects_leading_whitespace()
    {
        var (client, _) = await SignedInClientUser();

        Assert.Equal(HttpStatusCode.BadRequest, (await CreatePost(client, " hello")).StatusCode);
    }

    [Fact]
    public async Task Create_rejects_trailing_whitespace()
    {
        var (client, _) = await SignedInClientUser();

        Assert.Equal(HttpStatusCode.BadRequest, (await CreatePost(client, "hello ")).StatusCode);
    }

    [Fact]
    public async Task Create_rejects_control_characters()
    {
        var (client, _) = await SignedInClientUser();

        Assert.Equal(HttpStatusCode.BadRequest, (await CreatePost(client, "hello\0world")).StatusCode);
    }

    [Fact]
    public async Task Create_accepts_newlines()
    {
        var (client, _) = await SignedInClientUser();

        Assert.Equal(HttpStatusCode.Created, (await CreatePost(client, "hello\nworld")).StatusCode);
    }

    [Fact]
    public async Task Create_accepts_content_of_exactly_280_characters()
    {
        var (client, _) = await SignedInClientUser();

        Assert.Equal(HttpStatusCode.Created, (await CreatePost(client, new string('a', 280))).StatusCode);
    }

    [Fact]
    public async Task Create_rejects_content_over_280_characters()
    {
        var (client, _) = await SignedInClientUser();

        Assert.Equal(HttpStatusCode.BadRequest, (await CreatePost(client, new string('a', 281))).StatusCode);
    }

    [Fact]
    public async Task Create_does_not_persist_on_invalid_input()
    {
        var (client, _) = await SignedInClientUser();

        await CreatePost(client, "a");

        Assert.Empty(await PostStore.GetPage(0, 100));
    }

    [Fact]
    public async Task Create_with_a_reply_target_returns_201()
    {
        var (client, user) = await SignedInClientUser();
        var parent = await SeedPost(user.Id, "the parent");

        var response = await CreatePost(client, "the reply", parent.Id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(parent.Id, (await ReadPost(response)).InReplyToId);
    }

    [Fact]
    public async Task Create_can_reply_to_another_users_post()
    {
        var (client, _) = await SignedInClientUser();
        var other = await SeedUser(OtherUsername);
        var parent = await SeedPost(other.Id, "the parent");

        var response = await CreatePost(client, "the reply", parent.Id);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_an_unknown_reply_target_returns_404()
    {
        var (client, _) = await SignedInClientUser();

        var response = await CreatePost(client, "the reply", Guid.CreateVersion7());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // GetById

    [Fact]
    public async Task GetById_returns_the_post()
    {
        var (client, user) = await SignedInClientUser();
        var post = await SeedPost(user.Id, "hello world");

        var body = await client.GetFromJsonAsync<PostResponseDto>($"{API_POSTS}/{post.Id}");

        Assert.NotNull(body);
        Assert.Equal(post.Id, body.Id);
        Assert.Equal("hello world", body.Content);
        Assert.Equal(user.Id, body.AuthorId);
    }

    [Fact]
    public async Task GetById_returns_another_users_post()
    {
        var (client, _) = await SignedInClientUser();
        var other = await SeedUser(OtherUsername);
        var post = await SeedPost(other.Id);

        var response = await client.GetAsync($"{API_POSTS}/{post.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetById_returns_404_for_unknown_id()
    {
        var (client, _) = await SignedInClientUser();

        var response = await client.GetAsync($"{API_POSTS}/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_returns_404_for_a_non_guid_id()
    {
        var (client, _) = await SignedInClientUser();

        // The {postId:guid} route constraint has to keep this off the action.
        var response = await client.GetAsync($"{API_POSTS}/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // GetPage

    [Fact]
    public async Task GetPage_returns_posts_newest_first()
    {
        var (client, user) = await SignedInClientUser();
        var t = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        await SeedPost(
            PostAt(t, user.Id, "oldest"),
            PostAt(t.AddHours(2), user.Id, "newest"),
            PostAt(t.AddHours(1), user.Id, "middle"));

        var body = await client.GetFromJsonAsync<List<PostResponseDto>>(API_POSTS);

        Assert.NotNull(body);
        Assert.Equal(["newest", "middle", "oldest"], body.Select(p => p.Content));
    }

    [Fact]
    public async Task GetPage_includes_other_users_posts()
    {
        var (client, user) = await SignedInClientUser();
        var other = await SeedUser(OtherUsername);

        await SeedPost(user.Id, "mine");
        await SeedPost(other.Id, "theirs");

        var body = await client.GetFromJsonAsync<List<PostResponseDto>>(API_POSTS);

        Assert.NotNull(body);
        Assert.Equal(2, body.Count);
    }

    [Fact]
    public async Task GetPage_filters_by_author()
    {
        var (client, user) = await SignedInClientUser();
        var other = await SeedUser(OtherUsername);

        await SeedPost(user.Id, "mine");
        await SeedPost(other.Id, "theirs");

        var body = await client.GetFromJsonAsync<List<PostResponseDto>>($"{API_POSTS}?author={other.Id}");

        Assert.NotNull(body);
        Assert.Equal(["theirs"], body.Select(p => p.Content));
    }

    [Fact]
    public async Task GetPage_with_an_unknown_author_returns_an_empty_list()
    {
        var (client, user) = await SignedInClientUser();
        await SeedPost(user.Id);

        var body = await client.GetFromJsonAsync<List<PostResponseDto>>($"{API_POSTS}?author={Guid.CreateVersion7()}");

        Assert.NotNull(body);
        Assert.Empty(body);
    }

    [Fact]
    public async Task GetPage_honours_skip_and_take()
    {
        var (client, user) = await SignedInClientUser();
        var t = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        await SeedPost([.. Enumerable.Range(0, 5).Select(i => PostAt(t.AddHours(i), user.Id, $"post{i}"))]);

        var body = await client.GetFromJsonAsync<List<PostResponseDto>>($"{API_POSTS}?skip=1&take=2");

        Assert.NotNull(body);
        Assert.Equal(["post3", "post2"], body.Select(p => p.Content));
    }

    [Fact]
    public async Task GetPage_clamps_take_to_100()
    {
        var (client, user) = await SignedInClientUser();
        var t = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        await SeedPost([.. Enumerable.Range(0, 101).Select(i => PostAt(t.AddMinutes(i), user.Id, $"post{i}"))]);

        var body = await client.GetFromJsonAsync<List<PostResponseDto>>($"{API_POSTS}?take=1000");

        Assert.NotNull(body);
        Assert.Equal(100, body.Count);
    }

    [Fact]
    public async Task GetPage_defaults_to_20_per_page()
    {
        var (client, user) = await SignedInClientUser();
        var t = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        await SeedPost([.. Enumerable.Range(0, 25).Select(i => PostAt(t.AddMinutes(i), user.Id, $"post{i}"))]);

        var body = await client.GetFromJsonAsync<List<PostResponseDto>>(API_POSTS);

        Assert.NotNull(body);
        Assert.Equal(20, body.Count);
    }

    // GetReplies

    [Fact]
    public async Task GetReplies_returns_only_replies()
    {
        var (client, user) = await SignedInClientUser();
        var parent = await SeedPost(user.Id, "parent");
        await SeedPost(user.Id, "reply", parent.Id);
        await SeedPost(user.Id, "unrelated");

        var body = await client.GetFromJsonAsync<List<PostResponseDto>>($"{API_POSTS}/{parent.Id}/replies");

        Assert.NotNull(body);
        Assert.Equal(["reply"], body.Select(p => p.Content));
    }

    [Fact]
    public async Task GetReplies_returns_an_empty_list_when_there_are_none()
    {
        var (client, user) = await SignedInClientUser();
        var post = await SeedPost(user.Id);

        var body = await client.GetFromJsonAsync<List<PostResponseDto>>($"{API_POSTS}/{post.Id}/replies");

        Assert.NotNull(body);
        Assert.Empty(body);
    }

    [Fact]
    public async Task GetReplies_returns_404_for_unknown_post()
    {
        var (client, _) = await SignedInClientUser();

        var response = await client.GetAsync($"{API_POSTS}/{Guid.CreateVersion7()}/replies");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Update

    [Fact]
    public async Task Update_by_the_author_returns_204()
    {
        var (client, user) = await SignedInClientUser();
        var post = await SeedPost(user.Id, "before");

        var response = await UpdatePost(client, post.Id, "after");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Update_changes_the_content()
    {
        var (client, user) = await SignedInClientUser();
        var post = await SeedPost(user.Id, "before");

        await UpdatePost(client, post.Id, "after");

        var stored = await PostStore.GetById(post.Id);

        Assert.NotNull(stored);
        Assert.Equal("after", stored.Content);
    }

    [Fact]
    public async Task Update_sets_UpdatedAt()
    {
        var (client, user) = await SignedInClientUser();
        var post = await SeedPost(user.Id, "before");

        await UpdatePost(client, post.Id, "after");

        var stored = await PostStore.GetById(post.Id);

        Assert.NotNull(stored);
        Assert.NotNull(stored.UpdatedAt);
    }

    [Fact]
    public async Task Update_by_another_user_returns_403()
    {
        var (client, _) = await SignedInClientUser();
        var other = await SeedUser(OtherUsername);
        var post = await SeedPost(other.Id, "before");

        var response = await UpdatePost(client, post.Id, "after");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_by_another_user_leaves_the_content()
    {
        var (client, _) = await SignedInClientUser();
        var other = await SeedUser(OtherUsername);
        var post = await SeedPost(other.Id, "before");

        await UpdatePost(client, post.Id, "after");

        var stored = await PostStore.GetById(post.Id);

        Assert.NotNull(stored);
        Assert.Equal("before", stored.Content);
    }

    [Fact]
    public async Task Update_returns_404_for_unknown_id()
    {
        var (client, _) = await SignedInClientUser();

        var response = await UpdatePost(client, Guid.CreateVersion7(), "after");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_rejects_content_over_280_characters()
    {
        var (client, user) = await SignedInClientUser();
        var post = await SeedPost(user.Id, "before");

        var response = await UpdatePost(client, post.Id, new string('a', 281));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_rejects_whitespace_only_content()
    {
        var (client, user) = await SignedInClientUser();
        var post = await SeedPost(user.Id, "before");

        var response = await UpdatePost(client, post.Id, "   ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_without_a_cookie_returns_401()
    {
        var author = await SeedUser();
        var post = await SeedPost(author.Id, "before");

        var response = await UpdatePost(HttpClient(), post.Id, "after");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Delete

    [Fact]
    public async Task Delete_by_the_author_returns_204()
    {
        var (client, user) = await SignedInClientUser();
        var post = await SeedPost(user.Id);

        var response = await client.DeleteAsync($"{API_POSTS}/{post.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_removes_the_post()
    {
        var (client, user) = await SignedInClientUser();
        var post = await SeedPost(user.Id);

        await client.DeleteAsync($"{API_POSTS}/{post.Id}");

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{API_POSTS}/{post.Id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_by_another_user_returns_403()
    {
        var (client, _) = await SignedInClientUser();
        var other = await SeedUser(OtherUsername);
        var post = await SeedPost(other.Id);

        var response = await client.DeleteAsync($"{API_POSTS}/{post.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_by_another_user_leaves_the_post()
    {
        var (client, _) = await SignedInClientUser();
        var other = await SeedUser(OtherUsername);
        var post = await SeedPost(other.Id);

        await client.DeleteAsync($"{API_POSTS}/{post.Id}");

        Assert.NotNull(await PostStore.GetById(post.Id));
    }

    [Fact]
    public async Task Delete_returns_404_for_unknown_id()
    {
        var (client, _) = await SignedInClientUser();

        var response = await client.DeleteAsync($"{API_POSTS}/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_orphans_replies_instead_of_removing_them()
    {
        var (client, user) = await SignedInClientUser();
        var parent = await SeedPost(user.Id, "parent");
        var reply = await SeedPost(user.Id, "reply", parent.Id);

        await client.DeleteAsync($"{API_POSTS}/{parent.Id}");

        var stored = await PostStore.GetById(reply.Id);

        Assert.NotNull(stored);
        Assert.Null(stored.InReplyToId);
    }
}

