using System.Net;
using GitHubRefollow.Configuration;
using GitHubRefollow.GitHub;
using GitHubRefollow.Tests.TestSupport;
using Microsoft.Extensions.Options;

namespace GitHubRefollow.Tests.GitHub;

public sealed class GitHubFollowingClientTests
{
    [Fact]
    public async Task GetAuthenticatedLoginAsync_ReturnsLoginAndSendsBearerToken()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            StubHttpMessageHandler.Json("{\"login\":\"quinn\"}"));
        var client = CreateClient(handler);

        var login = await client.GetAuthenticatedLoginAsync(default);

        Assert.Equal("quinn", login);
        Assert.Equal("Bearer test-token", handler.Requests.Single().Authorization);
    }

    [Fact]
    public async Task GetFollowingAsync_FollowsNextLinkUntilAllUsersAreFrozen()
    {
        var handler = new StubHttpMessageHandler((_, index) => index switch
        {
            0 => StubHttpMessageHandler.Json(
                "[{\"login\":\"alice\"}]",
                link: "<https://api.github.com/user/following?per_page=100&page=2>; rel=\"next\""),
            1 => StubHttpMessageHandler.Json("[{\"login\":\"bob\"}]"),
            _ => throw new InvalidOperationException("Unexpected request")
        });
        var client = CreateClient(handler);

        var users = await client.GetFollowingAsync(default);

        Assert.Equal(["alice", "bob"], users);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task UnfollowAsync_UsesDeleteAndEscapesLogin()
    {
        var handler = NoContentHandler();
        var client = CreateClient(handler);

        await client.UnfollowAsync("octo cat", default);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.EndsWith("/user/following/octo%20cat", request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task FollowAsync_UsesPutAndEscapesLogin()
    {
        var handler = NoContentHandler();
        var client = CreateClient(handler);

        await client.FollowAsync("octo cat", default);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.EndsWith("/user/following/octo%20cat", request.RequestUri!.AbsoluteUri);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "bad credentials", GitHubFailureKind.Authentication)]
    [InlineData(HttpStatusCode.TooManyRequests, "slow down", GitHubFailureKind.RateLimit)]
    [InlineData(HttpStatusCode.UnprocessableEntity, "endpoint has been spammed", GitHubFailureKind.AbuseDetection)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "maintenance", GitHubFailureKind.Transient)]
    [InlineData(HttpStatusCode.NotFound, "missing", GitHubFailureKind.Permanent)]
    public async Task FailedRequest_ReportsSanitizedFailureKind(
        HttpStatusCode statusCode,
        string responseBody,
        GitHubFailureKind expectedKind)
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            StubHttpMessageHandler.Json(responseBody, statusCode));
        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<GitHubApiException>(
            () => client.GetAuthenticatedLoginAsync(default));

        Assert.Equal(expectedKind, error.Kind);
        Assert.Equal(statusCode, error.StatusCode);
        Assert.DoesNotContain(responseBody, error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("test-token", error.Message, StringComparison.Ordinal);
    }

    private static GitHubFollowingClient CreateClient(StubHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.github.com/")
        };
        var options = Options.Create(new RefollowOptions { Token = "test-token" });
        return new GitHubFollowingClient(httpClient, options);
    }

    private static StubHttpMessageHandler NoContentHandler() =>
        new((_, _) => new HttpResponseMessage(HttpStatusCode.NoContent));
}
