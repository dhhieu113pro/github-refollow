using GitHubRefollow.Configuration;
using GitHubRefollow.GitHub;
using GitHubRefollow.Refollowing;
using Microsoft.Extensions.Options;

namespace GitHubRefollow.Tests.Refollowing;

public sealed class RefollowServiceTests
{
    [Fact]
    public async Task RunAsync_WhenDryRun_VerifiesIdentityThenSnapshotsFollowingWithoutMutatingGitHub()
    {
        var events = new List<string>();
        var client = new FakeFollowingClient(["alice", "bob"], events);
        var store = new FakeSnapshotStore(events);
        var service = CreateService(client, store, dryRun: true);

        await service.RunAsync(default);

        Assert.Equal(
            [
                "authenticated:quinn",
                "snapshot:alice,bob"
            ],
            events);
    }

    [Fact]
    public async Task RunAsync_WhenEnabled_VerifiesIdentityThenRefollowsFrozenSnapshotInOrder()
    {
        var events = new List<string>();
        var client = new FakeFollowingClient(["alice", "bob"], events);
        var store = new FakeSnapshotStore(events);
        var service = CreateService(client, store, dryRun: false);

        await service.RunAsync(default);

        Assert.Equal(
            [
                "authenticated:quinn",
                "snapshot:alice,bob",
                "unfollow:alice",
                "follow:alice",
                "unfollow:bob",
                "follow:bob"
            ],
            events);
    }

    private static RefollowService CreateService(
        IGitHubFollowingClient client,
        IRefollowSnapshotStore store,
        bool dryRun)
    {
        var options = Options.Create(new RefollowOptions
        {
            DryRun = dryRun,
            DelaySeconds = 0
        });

        return new RefollowService(client, store, options);
    }

    private sealed class FakeFollowingClient(
        IReadOnlyList<string> following,
        List<string> events) : IGitHubFollowingClient
    {
        public Task<string> GetAuthenticatedLoginAsync(CancellationToken cancellationToken)
        {
            events.Add("authenticated:quinn");
            return Task.FromResult("quinn");
        }

        public Task<IReadOnlyList<string>> GetFollowingAsync(CancellationToken cancellationToken) =>
            Task.FromResult(following);

        public Task UnfollowAsync(string login, CancellationToken cancellationToken)
        {
            events.Add($"unfollow:{login}");
            return Task.CompletedTask;
        }

        public Task FollowAsync(string login, CancellationToken cancellationToken)
        {
            events.Add($"follow:{login}");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSnapshotStore(List<string> events) : IRefollowSnapshotStore
    {
        public Task SaveAsync(
            IReadOnlyList<string> following,
            CancellationToken cancellationToken)
        {
            events.Add($"snapshot:{string.Join(',', following)}");
            return Task.CompletedTask;
        }
    }
}
