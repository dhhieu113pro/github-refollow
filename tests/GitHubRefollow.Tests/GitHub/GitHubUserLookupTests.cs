using System.Net;
using GitHubRefollow.Configuration;
using GitHubRefollow.GitHub;
using GitHubRefollow.Tests.TestSupport;
using Microsoft.Extensions.Options;

namespace GitHubRefollow.Tests.GitHub;

public sealed class GitHubUserLookupTests
{
    [Fact]
    public async Task GetUserLoginAsync_ReturnsCanonicalLoginWithoutMutatingGitHub()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            StubHttpMessageHandler.Json("{\"login\":\"rua-den\"}"));
        var client = CreateClient(handler);
        var login = await client.GetUserLoginAsync("RUA-DEN", default);
        Assert.Equal("rua-den", login);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.github.com/users/RUA-DEN", request.RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer test-token", request.Authorization);
    }

    [Fact]
    public async Task GetUserLoginAsync_WhenMissing_ReportsSanitizedNotFound()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            StubHttpMessageHandler.Json("secret response body", HttpStatusCode.NotFound));
        var client = CreateClient(handler);
        var error = await Assert.ThrowsAsync<GitHubApiException>(() =>
            client.GetUserLoginAsync("missing-user", default));
        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
        Assert.DoesNotContain("secret response body", error.Message);
        Assert.DoesNotContain("test-token", error.Message);
    }

    private static GitHubFollowingClient CreateClient(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") },
            Options.Create(new RefollowOptions { Token = "test-token" }));
}
