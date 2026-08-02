namespace twttr.Storage;

using twttr.Models;

public interface IPostStore
{
    Task<Post?> GetById(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Post>> GetPage(int skip, int take, CancellationToken ct = default);
    Task<IReadOnlyList<Post>> GetPageByAuthor(Guid authorId, int skip, int take, CancellationToken ct = default);
    Task<IReadOnlyList<Post>> GetReplies(Guid id, int skip, int take, CancellationToken ct = default);

    Task<Post?> AddOne(NewPost data, CancellationToken ct = default);
    Task<bool> UpdatePost(UpdatePost data, CancellationToken ct = default);
    Task<bool> DeleteOne(Guid id, Guid authorId, CancellationToken ct = default);
}
