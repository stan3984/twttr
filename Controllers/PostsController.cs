using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using twttr.Infrastructure;
using twttr.Models;
using twttr.Storage;

namespace twttr.Controllers;

public class GetPostByIdResponseDto
{
    public required Guid Id { get; set; }
    public required string Content { get; set; }
    public required Guid AuthorId { get; set; }
    public Guid? InReplyToId { get; set; }
}

[ApiController]
[Route("/api/posts")]
public class PostsController(IPostStore store) : ControllerBase
{
    const int ContentMinLength = 2;
    const int ContentMaxLength = 280;

    private static string NormalizeContent(string content)
        => content.ReplaceLineEndings("\n").Normalize();

    private static bool IsValidContent(string content)
    {
        return content.Length >= ContentMinLength
               && content.Length <= ContentMaxLength
               && content.IsNormalized()
               // ban control characters except newline
               && !content.Any(c => char.IsControl(c) && c != '\n')
               && !char.IsWhiteSpace(content.First())
               && !char.IsWhiteSpace(content.Last());
    }

    private Guid? TryGetUserId()
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : null;
    }

    [HttpGet("{postId:guid}")]
    public async Task<ActionResult<PostResponseDto>> GetById(Guid postId, CancellationToken ct = default)
    {
        var post = await store.GetById(postId, ct);
        return post == null
            ? NotFound()
            : Ok(PostResponseDto.From(post));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PostResponseDto>>> GetPage(
        [FromQuery] Guid? author = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default
    )
    {
        var posts = author is Guid authorId
            ? await store.GetPageByAuthor(authorId, skip, take, ct)
            : await store.GetPage(skip, take, ct);
        return Ok(posts.Select(PostResponseDto.From).ToList());
    }

    [HttpGet("{post:guid}/replies")]
    public async Task<ActionResult<IReadOnlyList<PostResponseDto>>> GetReplies(
        Guid post,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default
    )
    {
        if (await store.GetById(post, ct) == null)
        {
            return NotFound();
        }

        var replies = await store.GetReplies(post, skip, take, ct);
        return Ok(replies.Select(PostResponseDto.From).ToList());
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Post)]
    public async Task<ActionResult<PostResponseDto>> Create([FromBody] CreatePostRequestDto request, CancellationToken ct = default)
    {
        var userId = TryGetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        request.Content = NormalizeContent(request.Content);
        if (!IsValidContent(request.Content))
        {
            return BadRequest();
        }

        var post = await store.AddOne(new NewPost
        {
            AuthorId = (Guid)userId,
            Content = request.Content,
            InReplyToId = request.InReplyToId,
        }, ct);

        return post == null
            ? NotFound()
            : CreatedAtAction(nameof(GetById), new { postId = post.Id }, PostResponseDto.From(post));
    }

    [HttpPatch("{postId:guid}")]
    public async Task<ActionResult<PostResponseDto>> Update(Guid postId, [FromBody] UpdatePostRequestDto request, CancellationToken ct = default)
    {
        var userId = TryGetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var post = await store.GetById(postId, ct);
        if (post == null)
        {
            return NotFound();
        }

        if (post.AuthorId != userId)
        {
            return Forbid();
        }

        if (!IsValidContent(request.Content))
        {
            return BadRequest();
        }

        var success = await store.UpdatePost(new UpdatePost
        {
            Id = postId,
            AuthorId = (Guid)userId,
            Content = request.Content
        }, ct);

        return success
            ? NoContent()
            : NotFound();
    }

    [HttpDelete("{postId:guid}")]
    public async Task<IActionResult> Delete(Guid postId, CancellationToken ct = default)
    {
        var userId = TryGetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (await store.DeleteOne(postId, (Guid)userId, ct))
        {
            return NoContent();
        }

        return await store.GetById(postId, ct) == null
            ? NotFound()
            : Forbid();
    }
}

public class CreatePostRequestDto
{
    [MaxLength(280)]
    public required string Content { get; set; }
    public Guid? InReplyToId { get; set; }
}

public class UpdatePostRequestDto
{
    [MaxLength(280)]
    public required string Content { get; set; }
}

public class PostResponseDto
{
    public required Guid Id { get; set; }
    public required Guid AuthorId { get; set; }
    public required string Content { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? InReplyToId { get; set; }

    public static PostResponseDto From(Post post)
        => new()
        {
            Id = post.Id,
            AuthorId = post.AuthorId,
            Content = post.Content,
            CreatedAt = post.CreatedAt,
            UpdatedAt = post.UpdatedAt,
            InReplyToId = post.InReplyToId,
        };
}
