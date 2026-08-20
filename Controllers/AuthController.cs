using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using twttr.Infrastructure;
using twttr.Models;
using twttr.Storage;

namespace twttr.Controllers;

[ApiController]
[Route("/api/auth")]
public partial class AuthController(
    IUserStore store,
    IPasswordService passwordService,
    ILogger<AuthController> logger
) : ControllerBase
{
    private readonly ILogger _logger = logger;

    [LoggerMessage(LogLevel.Warning, "Failed login attempt for user '{Username}' from IP {ClientIp}")]
    private partial void LogFailedLogin(string username, string? ClientIp);
    [LoggerMessage(LogLevel.Information, "User '{Username}' signed in")]
    private partial void LogSignedIn(string username);
    [LoggerMessage(LogLevel.Information, "User '{Username}' registered")]
    private partial void LogRegistered(string username);
    [LoggerMessage(LogLevel.Information, "Rehashed password for user '{Username}'")]
    private partial void LogRehash(string username);

    const int UsernameMinLength = 8;
    const int UsernameMaxLength = 24;
    const int PasswordMinLength = 12;
    const int PasswordMaxLength = 64;

    private static bool IsValidPassword(string password)
    {
        return password.Length >= PasswordMinLength
               && password.Length <= PasswordMaxLength;
    }

    private static bool IsValidUsername(string username)
    {
        if (username.Length < UsernameMinLength || username.Length > UsernameMaxLength)
            return false;

        // Usernames must contains only ASCII letters and digits.
        if (!username.All(char.IsAsciiLetterOrDigit))
            return false;

        return true;
    }

    private static async Task SignIn(HttpContext context, User user)
    {
        var claims = new List<Claim> {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.AuthenticationInstant, DateTimeOffset.UtcNow.ToString()),
        };
        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(
                new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme
                )
            )
        );
    }

    private static async Task SignOut(HttpContext context)
        => await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Register)]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request, CancellationToken ct = default)
    {
        var username = request.Username;
        var email = request.Email;
        var password = request.Password;

        // Email validation is part of the DTO.
        if (!IsValidUsername(username) || !IsValidPassword(password))
        {
            return BadRequest();
        }

        var newUser = await store.AddOne(new NewUser
        {
            Username = username,
            Email = email,
            PasswordHash = passwordService.Hash(password),
        }, ct);

        if (newUser is null)
        {
            return Conflict();
        }

        await SignIn(HttpContext, newUser);
        return Created();
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken ct = default)
    {
        var username = request.Username;
        var password = request.Password;

        // Do not run IsValid{Username,Email,Password} functions as that might prevent existing users
        // from logging in.

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var user = await store.GetByUsername(username, ct);

        // If `user` is null, then a dummy hash will be processed.
        var result = passwordService.Verify(user?.PasswordHash, password);
        if (user == null || result == PasswordVerificationResult.Failed)
        {
            LogFailedLogin(username, clientIp);
            return Unauthorized();
        }

        switch (result)
        {
            case PasswordVerificationResult.Success:
                await SignIn(HttpContext, user);
                LogSignedIn(username);
                return NoContent();
            case PasswordVerificationResult.SuccessRehashNeeded:
                await store.UpdateOne(new UpdateUser { Id = user.Id, PasswordHash = passwordService.Hash(password) }, ct);
                LogRehash(username);
                await SignIn(HttpContext, user);
                LogSignedIn(username);
                return NoContent();
            default:
                throw new UnreachableException();
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await SignOut(HttpContext);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<IdentifyResponseDto>> Identify(CancellationToken ct = default)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        {
            return Unauthorized();
        }

        var user = await store.GetById(id, ct);
        return user is null
            ? Unauthorized()
            : Ok(new IdentifyResponseDto { Id = id, Username = user.Username, DisplayName = user.DisplayName });
    }
}

public class IdentifyResponseDto
{
    public required Guid Id { get; set; }
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
}

public class LoginRequestDto
{
    public required string Username { get; set; }
    public required string Password { get; set; }
}

public class RegisterRequestDto
{
    [MinLength(8)]
    public required string Username { get; set; }
    [MinLength(12)]
    public required string Password { get; set; }
    [EmailAddress]
    public required string Email { get; set; }
}
