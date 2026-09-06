using System.Net;
using GitHubRefollow.Refollowing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GitHubRefollow.Tests.Web;

public sealed class RefollowWebApiTests
{
    [Fact]
    public async Task Root_ReturnsDashboard()
    {
        await using var factory = CreateFactory(new FakeCoordinator());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("GitHub Re-follow", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunNow_WithoutApiKey_ReturnsUnauthorized()
    {
        var coordinator = new FakeCoordinator();
        await using var factory = CreateFactory(coordinator);
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/run", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, coordinator.RunCount);
    }

    [Fact]
    public async Task RunNow_WithApiKey_RunsCoordinator()
    {
        var coordinator = new FakeCoordinator();
        await using var factory = CreateFactory(coordinator);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/run");
        request.Headers.Add("X-Api-Key", "test-api-key");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, coordinator.RunCount);
    }

    private static WebApplicationFactory<Program> CreateFactory(FakeCoordinator coordinator) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GitHubRefollow:Token"] = "test-token",
                    ["GitHubRefollow:ApiKey"] = "test-api-key",
                    ["GitHubRefollow:DryRun"] = "true",
                    ["GitHubRefollow:DataPath"] = Path.Combine(
                        Path.GetTempPath(),
                        $"github-refollow-web-tests-{Guid.NewGuid():N}")
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRefollowCoordinator>();
                services.AddSingleton<IRefollowCoordinator>(coordinator);
            });
        });

    private sealed class FakeCoordinator : IRefollowCoordinator
    {
        public int RunCount { get; private set; }

        public Task<RefollowRunResult> RunAsync(CancellationToken cancellationToken)
        {
            RunCount++;
            return Task.FromResult(new RefollowRunResult(2, DryRun: true));
        }
    }
}
