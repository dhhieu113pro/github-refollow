using GitHubRefollow.Configuration;
using GitHubRefollow.GitHub;
using Microsoft.Extensions.Options;

namespace GitHubRefollow.Refollowing;

public interface IRefollowSnapshotStore
{
    Task SaveAsync(
        IReadOnlyList<string> following,
        CancellationToken cancellationToken);
}

public sealed class RefollowService
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

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var following = await client.GetFollowingAsync(cancellationToken);
        var frozen = following.ToArray();

        await snapshotStore.SaveAsync(frozen, cancellationToken);

        if (options.DryRun)
        {
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Max(0, options.DelaySeconds));

        foreach (var login in frozen)
        {
            await client.UnfollowAsync(login, cancellationToken);

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            await client.FollowAsync(login, cancellationToken);

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
