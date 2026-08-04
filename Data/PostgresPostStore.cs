using Microsoft.EntityFrameworkCore;
using Npgsql;
using twttr.Models;
using twttr.Storage;

namespace twttr.Data;

public class PostgresPostStore(AppDbContext db) : IPostStore
{
    private static async Task<IReadOnlyList<T>> Pageify<T>(IQueryable<T> queryable, int skip, int take, CancellationToken ct = default)
        => await queryable
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 0, 100))
            .ToListAsync(ct);

    public Task<Post?> GetById(Guid id, CancellationToken ct = default)
        => db.Posts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<IReadOnlyList<Post>> GetPage(int skip, int take, CancellationToken ct = default)
        => Pageify(db.Posts.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id), skip, take, ct);

    public Task<IReadOnlyList<Post>> GetPageByAuthor(Guid authorId, int skip, int take, CancellationToken ct = default)
        => Pageify(db.Posts.Where(p => p.AuthorId == authorId).OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id), skip, take, ct);

    public Task<IReadOnlyList<Post>> GetReplies(Guid id, int skip, int take, CancellationToken ct = default)
        => Pageify(db.Posts.Where(p => p.InReplyToId == id).OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id), skip, take, ct);

    public async Task<Post?> AddOne(NewPost data, CancellationToken ct = default)
    {
        try
        {
            var timestamp = DateTimeOffset.UtcNow;
            var post = new Post
            {
                Id = Guid.CreateVersion7(timestamp),
                Content = data.Content,
                CreatedAt = timestamp,
                AuthorId = data.AuthorId,
                InReplyToId = data.InReplyToId,
            };

            db.Posts.Add(post);
            await db.SaveChangesAsync(ct);
            return post;

        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
        {
            return null;
        }
    }

    public async Task<bool> UpdatePost(UpdatePost data, CancellationToken ct = default)
    {
        if (data.Content == null)
        {
            return await db.Posts.AnyAsync(p => p.Id == data.Id && p.AuthorId == data.AuthorId, ct);
        }

        var rows = await db.Posts.Where(p => p.Id == data.Id && p.AuthorId == data.AuthorId).ExecuteUpdateAsync(s =>
        {
            if (data.Content != null)
            {
                s.SetProperty(p => p.Content, data.Content);
            }

            s.SetProperty(p => p.UpdatedAt, DateTimeOffset.UtcNow);
        }, ct);

        return rows > 0;
    }

    public async Task<bool> DeleteOne(Guid id, Guid authorId, CancellationToken ct = default)
        => await db.Posts.Where(p => p.Id == id && p.AuthorId == authorId).ExecuteDeleteAsync(ct) > 0;
}
