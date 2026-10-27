using System.Net;
using StoryCups.IntegrationTests.Infrastructure;

namespace StoryCups.IntegrationTests;

public sealed class RateLimitedAppFactory : StoryCupsAppFactory
{
    protected override int AuthPermitsPerMinute => 3;
}

public sealed class RateLimitTests(RateLimitedAppFactory app) : IClassFixture<RateLimitedAppFactory>
{
    [Fact]
    public async Task AuthEndpoints_OverLimit_Return429()
    {
        var client = app.CreateClient();
        var email = ApiClient.NewEmail();

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.LoginAsync(email)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.LoginAsync(email)).StatusCode);
    }
}
