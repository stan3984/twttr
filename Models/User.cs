namespace twttr.Models;

public class User
{
    public required Guid Id { get; set; }
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }

    public ICollection<Post> Posts { get; } = [];

    public User Clone() => new()
    {
        Id = Id,
        Username = Username,
        DisplayName = DisplayName,
        Email = Email,
        PasswordHash = PasswordHash,
        CreatedAt = CreatedAt,
    };
}

public class NewUser
{
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
}

public class UpdateUser
{
    public required Guid Id { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? PasswordHash { get; set; }
}
