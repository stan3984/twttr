using System.Net;
using twttr.Tests.Infrastructure;

namespace twttr.Tests;

public class RateLimitingTest(PostgresFixture fixture) : WebTest(fixture)
{
    private const int REQUEST_LIMIT = 10;
    private const int GLOBAL_LIMIT = REQUEST_LIMIT * 2;

    protected override (string, string)[] AppSettings => [
        ("RateLimiting:Global:Permits", $"{GLOBAL_LIMIT}"),
        ("RateLimiting:Login:Permits", $"{REQUEST_LIMIT}"),
        ("RateLimiting:Post:Permits", $"{REQUEST_LIMIT}"),
        ("RateLimiting:Register:Permits", $"{REQUEST_LIMIT}"),
    ];

    [Fact]
    public async Task Few_registrations_returns_201()
    {
        var responses = await Task.WhenAll(
            Enumerable.Range(1, REQUEST_LIMIT).Select(i => Register(HttpClient(), $"rateUser{i}", "very_long_password", $"user-{i}@example.com"))
        );

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
    }

    [Fact]
    public async Task Many_registrations_returns_429()
    {
        const int COUNT_SUCCESS = REQUEST_LIMIT;
        const int COUNT_FAILURE = COUNT_SUCCESS * 2;

        // generate `SUCCESS + FAILURE` requests.
        var responses = await Task.WhenAll(
            Enumerable.Range(1, COUNT_SUCCESS + COUNT_FAILURE).Select(i => Register(HttpClient(), $"rateUser{i}", "very_long_password", $"user-{i}@example.com"))
        );

        // count the number of successes and failures.
        var (success, failure) = responses.Aggregate((0, 0), (accumulator, response) =>
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.Created:
                    accumulator.Item1 += 1;
                    break;
                case HttpStatusCode.TooManyRequests:
                    accumulator.Item2 += 1;
                    break;
                default:
                    Assert.Fail($"Unexpected status code: {response.StatusCode}");
                    break;
            }

            return accumulator;
        });

        // verify that the expected number of requests succeed/fail.
        Assert.Equal(COUNT_SUCCESS, success);
        Assert.Equal(COUNT_FAILURE, failure);
    }

    // todo: test Login and Post rate limiting
}
