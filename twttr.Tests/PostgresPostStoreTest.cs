using twttr.Tests.Infrastructure;

namespace twttr.Tests;

public class PostgresPostStoreTest(PostgresFixture fixture) : PostgresTest(fixture)
{
    private static Post PostAt(
        DateTimeOffset createdAt,
        Guid authorId,
        string content,
        Guid? id = null,
        Guid? inReplyToId = null,
        DateTimeOffset? updatedAt = null
    ) => new()
    {
        Id = id ?? Guid.CreateVersion7(),
        Content = content,
        CreatedAt = createdAt,
        UpdatedAt = updatedAt,
        AuthorId = authorId,
        InReplyToId = inReplyToId,
    };

    private static NewPost NewPostData(Guid authorId, string content = "hello world", Guid? inReplyToId = null) => new()
    {
        AuthorId = authorId,
        Content = content,
        InReplyToId = inReplyToId,
    };

    private async Task<User> NewAuthor(string username = "alice")
    {
        var user = new User()
        {
            Id = Guid.CreateVersion7(),
            Username = username,
            DisplayName = username,
            Email = $"{username}@example.com",
            PasswordHash = "not-a-real-hash",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await SeedUser(user);
        return user;
    }


    [Fact]
    public async Task AddOne_saves_and_returns_post()
    {
        var author = await NewAuthor();

        var created = await PostStore.AddOne(NewPostData(author.Id, "hello world"));

        Assert.NotNull(created);

        var found = await PostStore.GetById(created.Id);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
        Assert.Equal("hello world", found.Content);
        Assert.Equal(author.Id, found.AuthorId);
        Assert.Null(found.InReplyToId);
    }

    [Fact]
    public async Task AddOne_assigns_a_version_7_id()
    {
        var author = await NewAuthor();
        var created = await PostStore.AddOne(NewPostData(author.Id));

        Assert.NotNull(created);
        Assert.Equal(7, created.Id.Version);
    }

    [Fact]
    public async Task AddOne_sets_CreatedAt_to_utc_now()
    {
        var author = await NewAuthor();

        var before = DateTimeOffset.UtcNow;
        var created = await PostStore.AddOne(NewPostData(author.Id));
        var after = DateTimeOffset.UtcNow;

        Assert.NotNull(created);

        var found = await PostStore.GetById(created.Id);

        Assert.NotNull(found);
        Assert.InRange(found.CreatedAt, before, after);
    }

    [Fact]
    public async Task AddOne_sets_UpdatedAt_to_null()
    {
        var author = await NewAuthor();

        var created = await PostStore.AddOne(NewPostData(author.Id));
        Assert.NotNull(created);

        var found = await PostStore.GetById(created.Id);

        Assert.NotNull(found);
        Assert.Null(found.UpdatedAt);
    }

    [Fact]
    public async Task AddOne_links_a_reply_to_its_parent()
    {
        var author = await NewAuthor();
        var parent = await PostStore.AddOne(NewPostData(author.Id, "parent"));
        Assert.NotNull(parent);

        var reply = await PostStore.AddOne(NewPostData(author.Id, "reply", parent.Id));

        Assert.NotNull(reply);

        var found = await PostStore.GetById(reply.Id);

        Assert.NotNull(found);
        Assert.Equal(parent.Id, found.InReplyToId);
    }

    [Fact]
    public async Task AddOne_with_unknown_InReplyToId_returns_null()
    {
        var author = await NewAuthor();
        var created = await PostStore.AddOne(NewPostData(author.Id, "orphan", Guid.CreateVersion7()));

        Assert.Null(created);
    }

    [Fact]
    public async Task AddOne_with_unknown_AuthorId_returns_null()
    {
        Assert.Null(await PostStore.AddOne(NewPostData(Guid.CreateVersion7())));
    }

    [Fact]
    public async Task AddOne_rejected_post_does_not_persist()
    {
        var author = await NewAuthor();

        await PostStore.AddOne(NewPostData(author.Id, "orphan", Guid.CreateVersion7()));

        Assert.Empty(await PostStore.GetPage(0, 100));
    }

    [Fact]
    public async Task GetById_returns_null_when_absent()
    {
        Assert.Null(await PostStore.GetById(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task GetPage_orders_newest_firt()
    {
        var author = await NewAuthor();
        var t = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        await SeedPost(
            PostAt(t, author.Id, "oldest"),
            PostAt(t.AddHours(2), author.Id, "newest"),
            PostAt(t.AddHours(1), author.Id, "middle")
        );

        var page = await PostStore.GetPage(0, 10);

        Assert.Equal(["newest", "middle", "oldest"], page.Select(p => p.Content));
    }

    [Fact]
    public async Task GetPage_ignores_UpdatedAt()
    {
        var author = await NewAuthor();
        var t = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        // an edit should not lift an old post to the top.
        await SeedPost(
            PostAt(t, author.Id, "edited", updatedAt: t.AddHours(5)),
            PostAt(t.AddHours(2), author.Id, "untouched")
        );

        var page = await PostStore.GetPage(0, 10);

        Assert.Equal(["untouched", "edited"], page.Select(p => p.Content));
    }

    [Fact]
    public async Task GetPage_breaks_ties_by_id()
    {
        var author = await NewAuthor();
        var t = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        var low = Guid.CreateVersion7();
        await Task.Delay(10);
        var high = Guid.CreateVersion7();

        await SeedPost(
            PostAt(t, author.Id, "high", high),
            PostAt(t, author.Id, "low", low));

        var page = await PostStore.GetPage(0, 10);

        Assert.Equal(["low", "high"], page.Select(p => p.Content));
    }

    [Fact]
    public async Task GetPage_pages_without_overlap()
    {
        var author = await NewAuthor();
        var t = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        await SeedPost([.. Enumerable.Range(0, 5).Select(i => PostAt(t.AddHours(i), author.Id, $"post{i}"))]);

        var first = await PostStore.GetPage(0, 2);
        var second = await PostStore.GetPage(2, 2);
        var third = await PostStore.GetPage(4, 2);

        Assert.Equal(2, first.Count);
        Assert.Equal(2, second.Count);
        Assert.Single(third);

        var seen = first.Concat(second).Concat(third).Select(p => p.Content).ToList();

        Assert.Equal(5, seen.Distinct().Count());
        Assert.Equal(["post4", "post3", "post2", "post1", "post0"], seen);
    }

    [Fact]
    public async Task GetPageByAuthor_excludes_other_authors()
    {
        var alice = await NewAuthor("alice");
        var bob = await NewAuthor("bob");
        var t = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        await SeedPost(
            PostAt(t, alice.Id, "from alice"),
            PostAt(t.AddHours(1), bob.Id, "from bob")
        );

        var page = await PostStore.GetPageByAuthor(alice.Id, 0, 10);

        Assert.Equal(["from alice"], page.Select(p => p.Content));
    }

    [Fact]
    public async Task GetPageByAuthor_orders_newest_first()
    {
        var author = await NewAuthor();
        var t = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        await SeedPost(
            PostAt(t, author.Id, "oldest"),
            PostAt(t.AddHours(2), author.Id, "newest"),
            PostAt(t.AddHours(1), author.Id, "middle")
        );

        var page = await PostStore.GetPageByAuthor(author.Id, 0, 10);

        Assert.Equal(["newest", "middle", "oldest"], page.Select(p => p.Content));
    }

    [Fact]
    public async Task GetPageByAuthor_returns_empty_for_unknown_author()
        => Assert.Empty(await PostStore.GetPageByAuthor(Guid.CreateVersion7(), 0, 10));

    [Fact]
    public async Task GetReplies_returns_only_direct_replies()
    {
        var author = await NewAuthor();
        var t = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        var parent = PostAt(t, author.Id, "parent");
        var reply = PostAt(t.AddMinutes(1), author.Id, "reply", inReplyToId: parent.Id);
        await SeedPost(parent);
        await SeedPost(reply, PostAt(t.AddMinutes(2), author.Id, "unrelated"));

        // A reply to the reply must not show up under the parent.
        await SeedPost(PostAt(t.AddMinutes(3), author.Id, "nested", inReplyToId: reply.Id));

        var replies = await PostStore.GetReplies(parent.Id, 0, 10);

        Assert.Equal(["reply"], replies.Select(p => p.Content));
    }

    [Fact]
    public async Task GetReplies_returns_empty_when_there_are_no_replies()
    {
        var author = await NewAuthor();
        var created = await PostStore.AddOne(NewPostData(author.Id));

        Assert.Empty(await PostStore.GetReplies(created!.Id, 0, 10));
    }

    [Fact]
    public async Task GetReplies_returns_empty_for_unknown_post()
        => Assert.Empty(await PostStore.GetReplies(Guid.CreateVersion7(), 0, 10));

    [Fact]
    public async Task UpdatePost_changes_the_content()
    {
        var author = await NewAuthor();
        var created = await PostStore.AddOne(NewPostData(author.Id, "before"));
        Assert.NotNull(created);

        var updated = await PostStore.UpdatePost(new UpdatePost
        {
            Id = created.Id,
            AuthorId = author.Id,
            Content = "after",
        });

        Assert.True(updated);

        var found = await PostStore.GetById(created.Id);

        Assert.NotNull(found);
        Assert.Equal("after", found.Content);
    }

    [Fact]
    public async Task UpdatePost_sets_UpdatedAt()
    {
        var author = await NewAuthor();
        var created = await PostStore.AddOne(NewPostData(author.Id, "before"));
        Assert.NotNull(created);

        var before = DateTimeOffset.UtcNow;
        await PostStore.UpdatePost(new UpdatePost
        {
            Id = created.Id,
            AuthorId = author.Id,
            Content = "after",
        });
        var after = DateTimeOffset.UtcNow;

        var found = await PostStore.GetById(created.Id);

        Assert.NotNull(found);
        Assert.NotNull(found.UpdatedAt);
        Assert.InRange(found.UpdatedAt.Value, before, after);
    }

    [Fact]
    public async Task UpdatePost_leaves_CreatedAt_alone()
    {
        var author = await NewAuthor();
        var created = await PostStore.AddOne(NewPostData(author.Id, "before"));
        Assert.NotNull(created);

        var before = await PostStore.GetById(created.Id);
        Assert.NotNull(before);

        await PostStore.UpdatePost(new UpdatePost
        {
            Id = created.Id,
            AuthorId = author.Id,
            Content = "after",
        });

        var found = await PostStore.GetById(created.Id);

        Assert.NotNull(found);
        Assert.Equal(before.CreatedAt, found.CreatedAt);
    }

    [Fact]
    public async Task UpdatePost_returns_false_for_another_author()
    {
        var alice = await NewAuthor("alice");
        var bob = await NewAuthor("bob");
        var created = await PostStore.AddOne(NewPostData(alice.Id, "aaa"));
        Assert.NotNull(created);

        var updated = await PostStore.UpdatePost(new UpdatePost
        {
            Id = created.Id,
            AuthorId = bob.Id,
            Content = "bbb",
        });

        Assert.False(updated);
    }

    [Fact]
    public async Task UpdatePost_with_another_author_leaves_the_content_unchanged()
    {
        var alice = await NewAuthor("alice");
        var bob = await NewAuthor("bob");
        var created = await PostStore.AddOne(NewPostData(alice.Id, "alice was here"));
        Assert.NotNull(created);

        await PostStore.UpdatePost(new UpdatePost
        {
            Id = created.Id,
            AuthorId = bob.Id,
            Content = "bob was here",
        });

        var found = await PostStore.GetById(created.Id);

        Assert.NotNull(found);
        Assert.Equal("alice was here", found.Content);
    }

    [Fact]
    public async Task UpdatePost_returns_false_for_unknown_post_id()
    {
        var author = await NewAuthor();

        var updated = await PostStore.UpdatePost(new UpdatePost
        {
            Id = Guid.CreateVersion7(),
            AuthorId = author.Id,
            Content = "update",
        });

        Assert.False(updated);
    }

    [Fact]
    public async Task UpdatePost_with_no_fields_set_returns_true()
    {
        var author = await NewAuthor();
        var created = await PostStore.AddOne(NewPostData(author.Id, "before"));
        var updated = await PostStore.UpdatePost(new UpdatePost
        {
            Id = created!.Id,
            AuthorId = author.Id,
        });

        Assert.True(updated);

        var found = await PostStore.GetById(created.Id);

        Assert.NotNull(found);
        Assert.Equal("before", found.Content);
    }

    [Fact]
    public async Task UpdatePost_with_no_fields_set_returns_false_for_another_author()
    {
        var alice = await NewAuthor("alice");
        var bob = await NewAuthor("bob");
        var created = await PostStore.AddOne(NewPostData(alice.Id, "before"));

        // The short-circuit still has to respect ownership.
        var updated = await PostStore.UpdatePost(new UpdatePost
        {
            Id = created!.Id,
            AuthorId = bob.Id,
        });

        Assert.False(updated);
    }

    [Fact]
    public async Task DeleteOne_deletes_the_post_and_returns_true()
    {
        var author = await NewAuthor();
        var created = await PostStore.AddOne(NewPostData(author.Id));

        Assert.True(await PostStore.DeleteOne(created!.Id, author.Id));
        Assert.Null(await PostStore.GetById(created.Id));
    }

    [Fact]
    public async Task DeleteOne_returns_false_for_another_author()
    {
        var alice = await NewAuthor("alice");
        var bob = await NewAuthor("bob");
        var created = await PostStore.AddOne(NewPostData(alice.Id));

        Assert.False(await PostStore.DeleteOne(created!.Id, bob.Id));
    }

    [Fact]
    public async Task DeleteOne_by_another_author_does_nothing()
    {
        var alice = await NewAuthor("alice");
        var bob = await NewAuthor("bob");
        var created = await PostStore.AddOne(NewPostData(alice.Id));

        await PostStore.DeleteOne(created!.Id, bob.Id);

        Assert.NotNull(await PostStore.GetById(created.Id));
    }

    [Fact]
    public async Task DeleteOne_returns_false_for_unknown_id()
    {
        var author = await NewAuthor();

        Assert.False(await PostStore.DeleteOne(Guid.CreateVersion7(), author.Id));
    }

    [Fact]
    public async Task Deleting_a_user_deletes_their_posts()
    {
        var author = await NewAuthor();
        var created = await PostStore.AddOne(NewPostData(author.Id));

        Assert.NotNull(created);
        Assert.True(await UserStore.DeleteOne(author.Id));
        Assert.Null(await PostStore.GetById(created.Id));
    }
}
