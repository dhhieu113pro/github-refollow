using GitHubRefollow.Refollowing;
using GitHubRefollow.GitHub;

namespace GitHubRefollow.Tests.Refollowing;

public sealed class RecoveryQueueTests
{
    [Fact]
    public async Task QueueAsync_AddsMissingUserOnceWithoutChangingGitHub()
    {
        var client = new FakeClient(["alice"], "quinn");
        var store = new FakeStore();
        var queue = new RefollowRecoveryQueue(client, store);

        var first = await queue.QueueAsync("rua-den", default);
        var second = await queue.QueueAsync("RUA-DEN", default);

        Assert.Equal(2, first.TargetCount);
        Assert.Equal(1, first.FollowingCount);
        Assert.Equal(1, first.RecoveryCount);
        Assert.Equal(["rua-den"], first.RecoveryUsers);
        Assert.Equal(2, second.TargetCount);
        Assert.Equal(["rua-den"], store.Pending);
        Assert.Empty(client.Mutations);
    }

    [Fact]
    public async Task QueueAsync_RejectsInvalidAndSelfLoginsWithoutWriting()
    {
        var client = new FakeClient(["alice"], "quinn");
        var store = new FakeStore();
        var queue = new RefollowRecoveryQueue(client, store);

        await Assert.ThrowsAsync<ArgumentException>(() => queue.QueueAsync("../user", default));
        await Assert.ThrowsAsync<ArgumentException>(() => queue.QueueAsync("quinn", default));
        Assert.Empty(store.Pending);
        Assert.Empty(client.Mutations);
    }

    [Fact]
    public async Task QueueAsync_DoesNotQueueAnAlreadyFollowedAccount()
    {
        var client = new FakeClient(["alice"], "quinn");
        var store = new FakeStore();
        var queue = new RefollowRecoveryQueue(client, store);

        var result = await queue.QueueAsync("Alice", default);

        Assert.Equal(1, result.TargetCount);
        Assert.Empty(store.Pending);
    }

    [Fact]
    public async Task GetStatusAsync_MergesPendingAndFollowingWithoutMutations()
    {
        var client = new FakeClient(["alice", "bob"], "quinn");
        var store = new FakeStore { Pending = ["rua-den", "ALICE"] };
        var queue = new RefollowRecoveryQueue(client, store);

        var result = await queue.GetStatusAsync(default);

        Assert.Equal(3, result.TargetCount);
        Assert.Equal(2, result.FollowingCount);
        Assert.Equal(["rua-den"], result.RecoveryUsers);
        Assert.Empty(client.Mutations);
    }

    private sealed class FakeStore : IRefollowSnapshotStore
    {
        public string[] Pending { get; set; } = [];
        public Task<IReadOnlyList<string>> LoadPendingAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>(Pending);
        public Task SaveAsync(IReadOnlyList<string> users, CancellationToken ct) => Task.CompletedTask;
        public Task SavePendingAsync(IReadOnlyList<string> users, CancellationToken ct) { Pending = users.ToArray(); return Task.CompletedTask; }
    }

    private sealed class FakeClient(IReadOnlyList<string> following, string owner) : IGitHubFollowingClient
    {
        public List<string> Mutations { get; } = [];
        public Task<string> GetAuthenticatedLoginAsync(CancellationToken ct) => Task.FromResult(owner);
        public Task<string> GetUserLoginAsync(string login, CancellationToken ct) => Task.FromResult(login.ToLowerInvariant());
        public Task<IReadOnlyList<string>> GetFollowingAsync(CancellationToken ct) => Task.FromResult(following);
        public Task UnfollowAsync(string login, CancellationToken ct) { Mutations.Add("unfollow:" + login); return Task.CompletedTask; }
        public Task FollowAsync(string login, CancellationToken ct) { Mutations.Add("follow:" + login); return Task.CompletedTask; }
    }
}
