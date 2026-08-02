using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using twttr.Storage;

namespace twttr.Controllers;

[ApiController]
[Route("/api/users")]
public class UsersController(IUserStore store) : ControllerBase
{
    private Guid? TryGetUserId()
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GetUserByIdResponseDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var user = await store.GetById(id, ct);
        return user == null
            ? NotFound()
            : Ok(new GetUserByIdResponseDto { Id = user.Id, Username = user.Username, DisplayName = user.DisplayName });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        if (TryGetUserId() != id)
        {
            return Forbid();
        }

        return await store.DeleteOne(id, ct) ? NoContent() : NotFound();
    }
}

public class GetUserByIdResponseDto
{
    public required Guid Id { get; set; }
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
}
