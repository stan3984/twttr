using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using twttr.Configuration;

namespace twttr.Infrastructure;

public static class RateLimitPolicies
{
    public const string Login = "login";
    public const string Register = "register";
    public const string Post = "post";

    private const string FallbackPartition = "unknown";

    private static string? IpPartition(HttpContext context)
        => context.Connection.RemoteIpAddress is IPAddress address
            ? $"ip:{address}"
            : null;

    private static string? UserPartition(HttpContext context)
        => context.User.FindFirstValue(ClaimTypes.NameIdentifier) is string userId
            ? $"user:{userId}"
            : null;

    public static void Configure(RateLimiterOptions options, RateLimitingOptions limits)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.OnRejected = (context, _) =>
        {
            var http = context.HttpContext;
            var policy = http
                .GetEndpoint()?
                .Metadata
                .GetMetadata<EnableRateLimitingAttribute>()?
                .PolicyName ?? "global";

            http.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(RateLimitPolicies))
                .LogWarning(
                    "Rejected {Method} {Path} under policy {Policy} ({User}, {Ip})",
                    http.Request.Method,
                    http.Request.Path,
                    policy,
                    UserPartition(http) ?? "anonymous",
                    IpPartition(http) ?? FallbackPartition
                );

            return ValueTask.CompletedTask;
        };

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
            context => RateLimitPartition.GetFixedWindowLimiter(
                UserPartition(context) ?? IpPartition(context) ?? FallbackPartition,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.Global.Permits,
                    Window = TimeSpan.FromSeconds(limits.Global.Seconds)
                }
            )
        );

        options.AddPolicy(Login, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                IpPartition(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.Login.Permits,
                    Window = TimeSpan.FromSeconds(limits.Login.Seconds),
                }
            )
        );

        options.AddPolicy(Register, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                IpPartition(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.Register.Permits,
                    Window = TimeSpan.FromSeconds(limits.Register.Seconds),
                }
            )
        );

        options.AddPolicy(Post, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                UserPartition(context) ?? IpPartition(context) ?? FallbackPartition,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.Post.Permits,
                    Window = TimeSpan.FromSeconds(limits.Post.Seconds),
                }
            )
        );
    }
}
