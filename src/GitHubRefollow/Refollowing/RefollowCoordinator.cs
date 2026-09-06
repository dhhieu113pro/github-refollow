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

public sealed class RefollowAlreadyRunningException()
    : InvalidOperationException("A re-follow run is already in progress.");

public sealed class RefollowCoordinator(
    IRefollowRunner runner,
    IRefollowRunStateStore stateStore,
    TimeProvider timeProvider) : IRefollowCoordinator
{
    private readonly SemaphoreSlim runGate = new(1, 1);

    public async Task<RefollowRunResult> RunAsync(CancellationToken cancellationToken)
    {
        if (!await runGate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            throw new RefollowAlreadyRunningException();
        }

        var startedAt = timeProvider.GetUtcNow();

        try
        {
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
        finally
        {
            runGate.Release();
        }
    }
}
