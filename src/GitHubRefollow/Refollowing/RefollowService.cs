using GitHubRefollow.Configuration;
using GitHubRefollow.GitHub;
using Microsoft.Extensions.Options;

namespace GitHubRefollow.Refollowing;

public interface IRefollowSnapshotStore
{
    Task<IReadOnlyList<string>> LoadPendingAsync(CancellationToken cancellationToken);

    Task SaveAsync(
        IReadOnlyList<string> following,
        CancellationToken cancellationToken);

    Task SavePendingAsync(
        IReadOnlyList<string> pending,
        CancellationToken cancellationToken);
}

public sealed record RefollowRunResult(int FollowingCount, bool DryRun);

public interface IRefollowRunner
{
    Task<RefollowRunResult> RunAsync(CancellationToken cancellationToken);
}

public sealed class RefollowService : IRefollowRunner
{
    private readonly IGitHubFollowingClient client;
    private readonly IRefollowSnapshotStore snapshotStore;
    private readonly RefollowOptions options;

    public RefollowService(
        IGitHubFollowingClient client,
        IRefollowSnapshotStore snapshotStore,
        IOptions<RefollowOptions> options)
    {
        this.client = client;
        this.snapshotStore = snapshotStore;
        this.options = options.Value;
    }

    public async Task<RefollowRunResult> RunAsync(CancellationToken cancellationToken)
    {
        _ = await client.GetAuthenticatedLoginAsync(cancellationToken);

        var following = await client.GetFollowingAsync(cancellationToken);
        var pending = await snapshotStore.LoadPendingAsync(cancellationToken);
        var frozen = pending
            .Concat(following)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        await snapshotStore.SaveAsync(frozen, cancellationToken);

        if (options.DryRun)
        {
            return new RefollowRunResult(frozen.Length, DryRun: true);
        }

        await snapshotStore.SavePendingAsync(frozen, cancellationToken);

        var delay = TimeSpan.FromSeconds(Math.Max(0, options.DelaySeconds));

        for (var index = 0; index < frozen.Length; index++)
        {
            var login = frozen[index];
            await client.UnfollowAsync(login, cancellationToken);

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            await client.FollowAsync(login, cancellationToken);

            var remaining = frozen[(index + 1)..];
            await snapshotStore.SavePendingAsync(remaining, CancellationToken.None);

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }

        return new RefollowRunResult(frozen.Length, DryRun: false);
    }
}
