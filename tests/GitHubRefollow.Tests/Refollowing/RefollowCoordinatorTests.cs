using GitHubRefollow.Refollowing;

namespace GitHubRefollow.Tests.Refollowing;

public sealed class RefollowCoordinatorTests
{
    [Fact]
    public async Task RunAsync_OnSuccess_PersistsLastRunState()
    {
        var runner = new FakeRunner(new RefollowRunResult(2, DryRun: true));
        var store = new FakeRunStateStore();
        var coordinator = CreateCoordinator(runner, store);
        var result = await coordinator.RunAsync(default);
        Assert.Equal(2, result.FollowingCount);
        Assert.NotNull(store.Saved);
        Assert.True(store.Saved.Succeeded);
        Assert.Equal(2, store.Saved.FollowingCount);
        Assert.True(store.Saved.DryRun);
        Assert.Null(store.Saved.Error);
        Assert.NotNull(store.Saved.CompletedAt);
    }

    [Fact]
    public async Task RunAsync_OnFailure_PersistsSanitizedFailureAndRethrows()
    {
        var runner = new FakeRunner(new InvalidOperationException("sensitive detail"));
        var store = new FakeRunStateStore();
        var coordinator = CreateCoordinator(runner, store);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.RunAsync(default));
        Assert.Equal("sensitive detail", error.Message);
        Assert.NotNull(store.Saved);
        Assert.False(store.Saved.Succeeded);
        Assert.Equal("Refollow run failed.", store.Saved.Error);
        Assert.DoesNotContain("sensitive detail", store.Saved.Error);
    }

    [Fact]
    public async Task RunAsync_WhenAnotherRunIsActive_RejectsOverlap()
    {
        var runner = new BlockingRunner();
        var coordinator = CreateCoordinator(runner, new FakeRunStateStore());
        var firstRun = coordinator.RunAsync(default);
        await runner.Started;
        await Assert.ThrowsAsync<RefollowAlreadyRunningException>(() => coordinator.RunAsync(default));
        runner.Complete(new RefollowRunResult(1, DryRun: true));
        await firstRun;
    }

    [Fact]
    public async Task QueueRecoveryAsync_WhenRunIsActive_RejectsWithoutCallingQueue()
    {
        var runner = new BlockingRunner();
        var queue = new FakeRecoveryQueue();
        var coordinator = CreateCoordinator(runner, new FakeRunStateStore(), queue);
        var run = coordinator.RunAsync(default);
        await runner.Started;
        await Assert.ThrowsAsync<RefollowAlreadyRunningException>(
            () => coordinator.QueueRecoveryAsync("rua-den", default));
        Assert.Empty(queue.Queued);
        runner.Complete(new RefollowRunResult(1, DryRun: true));
        await run;
        await coordinator.QueueRecoveryAsync("rua-den", default);
        Assert.Equal(["rua-den"], queue.Queued);
    }

    private static RefollowCoordinator CreateCoordinator(
        IRefollowRunner runner, IRefollowRunStateStore store,
        IRefollowRecoveryQueue? queue = null) =>
        new(runner, store, TimeProvider.System, queue ?? new FakeRecoveryQueue());

    private sealed class FakeRecoveryQueue : IRefollowRecoveryQueue
    {
        public List<string> Queued { get; } = [];
        private static readonly RefollowRecoveryStatus Empty = new("quinn", 0, 0, 0, []);
        public Task<RefollowRecoveryStatus> GetStatusAsync(CancellationToken ct) => Task.FromResult(Empty);
        public Task<RefollowRecoveryStatus> QueueAsync(string login, CancellationToken ct)
        {
            Queued.Add(login);
            return Task.FromResult(Empty);
        }
    }

    private sealed class FakeRunner : IRefollowRunner
    {
        private readonly RefollowRunResult? result;
        private readonly Exception? error;
        public FakeRunner(RefollowRunResult result) => this.result = result;
        public FakeRunner(Exception error) => this.error = error;
        public Task<RefollowRunResult> RunAsync(CancellationToken cancellationToken)
        {
            if (error is not null)
                return Task.FromException<RefollowRunResult>(error);
            return Task.FromResult(result!);
        }
    }

    private sealed class BlockingRunner : IRefollowRunner
    {
        private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<RefollowRunResult> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Started => started.Task;
        public async Task<RefollowRunResult> RunAsync(CancellationToken cancellationToken)
        {
            started.TrySetResult();
            return await completion.Task.WaitAsync(cancellationToken);
        }
        public void Complete(RefollowRunResult result) => completion.TrySetResult(result);
    }

    private sealed class FakeRunStateStore : IRefollowRunStateStore
    {
        public RefollowLastRun? Saved { get; private set; }
        public Task<RefollowLastRun?> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult<RefollowLastRun?>(Saved);
        public Task SaveAsync(RefollowLastRun state, CancellationToken cancellationToken)
        {
            Saved = state;
            return Task.CompletedTask;
        }
    }
}
