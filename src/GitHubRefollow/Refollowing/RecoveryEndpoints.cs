using System.Net;
using GitHubRefollow.GitHub;

namespace GitHubRefollow.Refollowing;

public sealed record RecoveryRequest(string? Login);

public static class RecoveryEndpoints
{
    public static IEndpointRouteBuilder MapRecoveryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/recovery", async (
            IRefollowCoordinator coordinator, CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await coordinator.GetRecoveryStatusAsync(cancellationToken));
            }
            catch (GitHubApiException error)
            {
                return GitHubFailure(error);
            }
            catch
            {
                return Results.Problem(statusCode: 500, title: "Could not load recovery status.");
            }
        });

        app.MapPost("/api/recovery", async (
            RecoveryRequest request, IRefollowCoordinator coordinator, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Login))
            {
                return Results.BadRequest(new { error = "Enter a GitHub login." });
            }

            try
            {
                return Results.Ok(await coordinator.QueueRecoveryAsync(request.Login, cancellationToken));
            }
            catch (ArgumentException error)
            {
                return Results.BadRequest(new { error = error.Message });
            }
            catch (RefollowAlreadyRunningException)
            {
                return Results.Conflict(new { error = "A re-follow run is already in progress." });
            }
            catch (GitHubApiException error)
            {
                return GitHubFailure(error);
            }
            catch
            {
                return Results.Problem(statusCode: 500, title: "Could not queue recovery.");
            }
        });

        return app;
    }

    private static IResult GitHubFailure(GitHubApiException error)
    {
        if (error.StatusCode == HttpStatusCode.NotFound)
        {
            return Results.NotFound(new { error = "GitHub user was not found." });
        }

        return Results.Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: "GitHub API request failed.",
            detail: error.Kind.ToString());
    }
}
