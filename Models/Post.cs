namespace twttr.Models;

public class Post
{
    public required Guid Id { get; set; }
    public required string Content { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid AuthorId { get; set; }
    public User? Author { get; set; } = null;

    public Guid? InReplyToId { get; set; }
    public Post? InReplyTo { get; set; }

    public ICollection<Post> Replies { get; } = [];
}

public class NewPost
{
    public required Guid AuthorId { get; set; }
    public required string Content { get; set; }
    public Guid? InReplyToId { get; set; }
}

public class UpdatePost
{
    public required Guid Id { get; set; }
    public required Guid AuthorId { get; set; }
    public string? Content { get; set; }
}
