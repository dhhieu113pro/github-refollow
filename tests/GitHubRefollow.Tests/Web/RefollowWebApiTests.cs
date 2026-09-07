using System.Net;
using System.Net.Http.Json;
using GitHubRefollow.GitHub;
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
    public async Task Root_ExplainsThatRefollowTargetsFollowingNotFollowers()
    {
        await using var factory = CreateFactory(new FakeCoordinator());
        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/");
        Assert.Contains("accounts you follow", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Following", html, StringComparison.Ordinal);
        Assert.Contains("not your Followers", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Root_DoesNotAskForApiKey()
    {
        await using var factory = CreateFactory(new FakeCoordinator());
        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/");
        Assert.DoesNotContain("API key", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("X-Api-Key", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Root_ProvidesRecoveryFormAndTargetCount()
    {
        await using var factory = CreateFactory(new FakeCoordinator());
        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/");
        Assert.Contains("Recover missing user", html, StringComparison.Ordinal);
        Assert.Contains("/api/recovery", html, StringComparison.Ordinal);
        Assert.Contains("targetCount", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunNow_WithoutApiKey_RunsCoordinator()
    {
        var coordinator = new FakeCoordinator();
        await using var factory = CreateFactory(coordinator);
        using var client = factory.CreateClient();
        var response = await client.PostAsync("/api/run", content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, coordinator.RunCount);
    }

    [Fact]
    public async Task Recovery_GetAndPost_ReturnCountsWithoutRunningOrMutatingGitHub()
    {
        var coordinator = new FakeCoordinator();
        await using var factory = CreateFactory(coordinator);
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/recovery", new { login = "rua-den" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("rua-den", coordinator.Queued);
        Assert.Equal(0, coordinator.RunCount);
        var status = await client.GetFromJsonAsync<RefollowRecoveryStatus>("/api/recovery");
        Assert.NotNull(status);
        Assert.Equal(20, status.TargetCount);
        Assert.Equal(19, status.FollowingCount);
        Assert.Equal(1, status.RecoveryCount);
    }

    [Fact]
    public async Task Recovery_InvalidLogin_ReturnsBadRequest()
    {
        var coordinator = new FakeCoordinator { QueueError = new ArgumentException("Invalid login.") };
        await using var factory = CreateFactory(coordinator);
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/recovery", new { login = "../user" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(coordinator.Queued);
    }

    [Fact]
    public async Task Recovery_UnknownUser_ReturnsNotFoundWithoutExposingApiResponse()
    {
        var coordinator = new FakeCoordinator
        {
            QueueError = new GitHubApiException(GitHubFailureKind.Permanent, HttpStatusCode.NotFound)
        };
        await using var factory = CreateFactory(coordinator);
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/recovery", new { login = "missing-user" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("test-token", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Recovery_WhileRunning_ReturnsConflict()
    {
        var coordinator = new FakeCoordinator { QueueError = new RefollowAlreadyRunningException() };
        await using var factory = CreateFactory(coordinator);
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/recovery", new { login = "rua-den" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(FakeCoordinator coordinator) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GitHubRefollow:Token"] = "test-token",
                    ["GitHubRefollow:DryRun"] = "true",
                    ["GitHubRefollow:DataPath"] = Path.Combine(
                        Path.GetTempPath(), $"github-refollow-web-tests-{Guid.NewGuid():N}")
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
        public string? Queued { get; private set; }
        public Exception? QueueError { get; init; }
        public Task<RefollowRunResult> RunAsync(CancellationToken cancellationToken)
        {
            RunCount++;
            return Task.FromResult(new RefollowRunResult(2, DryRun: true));
        }
        public Task<RefollowRecoveryStatus> GetRecoveryStatusAsync(CancellationToken ct) =>
            Task.FromResult(new RefollowRecoveryStatus("quinn", 19, Queued is null ? 0 : 1,
                Queued is null ? 19 : 20, Queued is null ? [] : [Queued]));
        public Task<RefollowRecoveryStatus> QueueRecoveryAsync(string login, CancellationToken ct)
        {
            if (QueueError is not null) return Task.FromException<RefollowRecoveryStatus>(QueueError);
            Queued = login;
            return GetRecoveryStatusAsync(ct);
        }
    }
}
