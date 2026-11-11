using System.Net;
using TutoringManager.Tests.Infrastructure;

namespace TutoringManager.Tests;

[Collection(ApiCollection.Name)]
public class HealthTests(ApiFactory factory)
{
    [Fact]
    public async Task Health_returns_ok_when_database_is_reachable()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
