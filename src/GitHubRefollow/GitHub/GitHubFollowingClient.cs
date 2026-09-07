using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GitHubRefollow.Configuration;
using Microsoft.Extensions.Options;

namespace GitHubRefollow.GitHub;

public interface IGitHubFollowingClient
{
    Task<string> GetAuthenticatedLoginAsync(CancellationToken cancellationToken);
    Task<string> GetUserLoginAsync(string login, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> GetFollowingAsync(CancellationToken cancellationToken);
    Task UnfollowAsync(string login, CancellationToken cancellationToken);
    Task FollowAsync(string login, CancellationToken cancellationToken);
}

public enum GitHubFailureKind
{
    Authentication,
    RateLimit,
    AbuseDetection,
    Transient,
    Permanent
}

public sealed class GitHubApiException(
    GitHubFailureKind kind,
    HttpStatusCode statusCode)
    : Exception($"GitHub API request failed with status {(int)statusCode} ({statusCode}).")
{
    public GitHubFailureKind Kind { get; } = kind;
    public HttpStatusCode StatusCode { get; } = statusCode;
}

public sealed class GitHubFollowingClient : IGitHubFollowingClient
{
    private readonly HttpClient httpClient;

    public GitHubFollowingClient(
        HttpClient httpClient,
        IOptions<RefollowOptions> options)
    {
        this.httpClient = httpClient;
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", options.Value.Token);
        httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("github-refollow/1.0");
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
    }

    public async Task<string> GetAuthenticatedLoginAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("user", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadLoginAsync(response, cancellationToken);
    }

    public async Task<string> GetUserLoginAsync(string login, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"users/{Uri.EscapeDataString(login)}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadLoginAsync(response, cancellationToken);
    }

    private static async Task<string> ReadLoginAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var user = await response.Content.ReadFromJsonAsync<GitHubUser>(cancellationToken);
        return !string.IsNullOrWhiteSpace(user?.Login)
            ? user.Login
            : throw new InvalidOperationException("GitHub returned an invalid user response.");
    }

    public async Task<IReadOnlyList<string>> GetFollowingAsync(CancellationToken cancellationToken)
    {
        var users = new List<string>();
        Uri? next = new("user/following?per_page=100", UriKind.Relative);

        while (next is not null)
        {
            using var response = await httpClient.GetAsync(next, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            var page = await response.Content.ReadFromJsonAsync<List<GitHubUser>>(cancellationToken)
                ?? [];
            users.AddRange(page.Select(user => user.Login));
            next = GetNextLink(response);
        }

        return users;
    }

    public async Task UnfollowAsync(string login, CancellationToken cancellationToken)
    {
        using var response = await httpClient.DeleteAsync(
            $"user/following/{Uri.EscapeDataString(login)}",
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task FollowAsync(string login, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"user/following/{Uri.EscapeDataString(login)}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static Uri? GetNextLink(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var values))
        {
            return null;
        }

        foreach (var value in values)
        {
            foreach (var segment in value.Split(','))
            {
                var parts = segment.Split(';', StringSplitOptions.TrimEntries);
                if (parts.Length < 2 ||
                    !parts.Skip(1).Any(part =>
                        part.Equals("rel=\"next\"", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var target = parts[0].Trim();
                if (target.Length >= 2 && target[0] == '<' && target[^1] == '>')
                {
                    return new Uri(target[1..^1], UriKind.RelativeOrAbsolute);
                }
            }
        }

        return null;
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken);

        var kind = ClassifyFailure(response.StatusCode, body);
        throw new GitHubApiException(kind, response.StatusCode);
    }

    private static GitHubFailureKind ClassifyFailure(HttpStatusCode statusCode, string body)
    {
        if (statusCode == HttpStatusCode.Unauthorized)
        {
            return GitHubFailureKind.Authentication;
        }

        if (statusCode == HttpStatusCode.TooManyRequests)
        {
            return GitHubFailureKind.RateLimit;
        }

        if (statusCode == HttpStatusCode.UnprocessableEntity &&
            (body.Contains("spam", StringComparison.OrdinalIgnoreCase) ||
             body.Contains("abuse", StringComparison.OrdinalIgnoreCase) ||
             body.Contains("secondary rate", StringComparison.OrdinalIgnoreCase)))
        {
            return GitHubFailureKind.AbuseDetection;
        }

        if ((int)statusCode >= 500 || statusCode == HttpStatusCode.RequestTimeout)
        {
            return GitHubFailureKind.Transient;
        }

        return GitHubFailureKind.Permanent;
    }

    private sealed record GitHubUser(string Login);
}
