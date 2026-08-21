using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace twttr.Tests.Infrastructure;

public class AppFactory(string connectionString, params (string, string)[] settings) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:twttr", connectionString);

        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }
    }
}
