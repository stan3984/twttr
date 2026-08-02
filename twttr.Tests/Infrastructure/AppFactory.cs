using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace twttr.Tests.Infrastructure;

public class AppFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:twttr", connectionString)
               // todo: add tests for rate limit filters
               .UseSetting("RateLimiting:Login:Permits", "1000000")
               .UseSetting("RateLimiting:Register:Permits", "1000000")
               .UseSetting("RateLimiting:Post:Permits", "1000000")
               .UseSetting("RateLimiting:Global:Permits", "1000000");
    }
}
