using System.Text.Json;
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
    public async Task RunAsync_WhenPendingRecoveryExists_IncludesMissingUserInDryRun()
    {
        var dataPath = Path.Combine(
            Path.GetTempPath(),
            $"github-refollow-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataPath);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(dataPath, "pending.json"),
                "[\"rua-den\"]");

            var events = new List<string>();
            var client = new FakeFollowingClient(["alice"], events);
            var options = Options.Create(new RefollowOptions
            {
                DataPath = dataPath,
                DryRun = true,
                DelaySeconds = 0
            });
            var store = new JsonRefollowSnapshotStore(options);
            var service = new RefollowService(client, store, options);

            var result = await service.RunAsync(default);

            Assert.Equal(2, result.FollowingCount);
            Assert.True(result.DryRun);
        }
        finally
        {
            Directory.Delete(dataPath, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_WhenFollowFails_PersistsCurrentUserForRecovery()
    {
        var dataPath = Path.Combine(
            Path.GetTempPath(),
            $"github-refollow-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataPath);

        try
        {
            var events = new List<string>();
            var client = new FakeFollowingClient(["rua-den"], events, failFollow: "rua-den");
            var options = Options.Create(new RefollowOptions
            {
                DataPath = dataPath,
                DryRun = false,
                DelaySeconds = 0
            });
            var store = new JsonRefollowSnapshotStore(options);
            var service = new RefollowService(client, store, options);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunAsync(default));

            var pendingPath = Path.Combine(dataPath, "pending.json");
            Assert.True(File.Exists(pendingPath));
            var pending = JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(pendingPath));
            Assert.NotNull(pending);
            Assert.Equal(["rua-den"], pending);
        }
        finally
        {
            Directory.Delete(dataPath, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_WhenLaterFollowFails_RemovesCompletedUsersFromRecoveryJournal()
    {
        var dataPath = Path.Combine(
            Path.GetTempPath(),
            $"github-refollow-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataPath);

        try
        {
            var events = new List<string>();
            var client = new FakeFollowingClient(["alice", "rua-den"], events, failFollow: "rua-den");
            var options = Options.Create(new RefollowOptions
            {
                DataPath = dataPath,
                DryRun = false,
                DelaySeconds = 0
            });
            var store = new JsonRefollowSnapshotStore(options);
            var service = new RefollowService(client, store, options);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunAsync(default));

            var pending = JsonSerializer.Deserialize<string[]>(
                await File.ReadAllTextAsync(Path.Combine(dataPath, "pending.json")));
            Assert.NotNull(pending);
            Assert.Equal(["rua-den"], pending);
        }
        finally
        {
            Directory.Delete(dataPath, recursive: true);
        }
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
        List<string> events,
        string? failFollow = null) : IGitHubFollowingClient
    {
        public Task<string> GetAuthenticatedLoginAsync(CancellationToken cancellationToken)
        {
            events.Add("authenticated:quinn");
            return Task.FromResult("quinn");
        }

        public Task<string> GetUserLoginAsync(string login, CancellationToken cancellationToken) =>
            Task.FromResult(login);

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
            if (string.Equals(login, failFollow, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Simulated follow failure.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeSnapshotStore(List<string> events) : IRefollowSnapshotStore
    {
        public Task<IReadOnlyList<string>> LoadPendingAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task SaveAsync(
            IReadOnlyList<string> following,
            CancellationToken cancellationToken)
        {
            events.Add($"snapshot:{string.Join(',', following)}");
            return Task.CompletedTask;
        }

        public Task SavePendingAsync(
            IReadOnlyList<string> pending,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
