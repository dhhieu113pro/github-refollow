namespace GitHubRefollow.Refollowing;

public sealed record RefollowLastRun(
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    bool Succeeded,
    int? FollowingCount,
    bool? DryRun,
    string? Error);

public interface IRefollowRunStateStore
{
    Task<RefollowLastRun?> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(RefollowLastRun state, CancellationToken cancellationToken);
}

public interface IRefollowCoordinator
{
    Task<RefollowRunResult> RunAsync(CancellationToken cancellationToken);
}

public sealed class RefollowCoordinator(
    IRefollowRunner runner,
    IRefollowRunStateStore stateStore,
    TimeProvider timeProvider) : IRefollowCoordinator
{
    public async Task<RefollowRunResult> RunAsync(CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetUtcNow();

        try
        {
            var result = await runner.RunAsync(cancellationToken);
            var completedAt = timeProvider.GetUtcNow();

            await stateStore.SaveAsync(
                new RefollowLastRun(
                    startedAt,
                    completedAt,
                    Succeeded: true,
                    result.FollowingCount,
                    result.DryRun,
                    Error: null),
                cancellationToken);

            return result;
        }
        catch
        {
            var completedAt = timeProvider.GetUtcNow();
            await stateStore.SaveAsync(
                new RefollowLastRun(
                    startedAt,
                    completedAt,
                    Succeeded: false,
                    FollowingCount: null,
                    DryRun: null,
                    Error: "Refollow run failed."),
                CancellationToken.None);
            throw;
        }
    }
}
