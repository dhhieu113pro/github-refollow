using GitHubRefollow.Refollowing;
using GitHubRefollow.Scheduling;

namespace GitHubRefollow.Tests.Scheduling;

public sealed class ScheduledRefollowWorkerTests
{
    [Fact]
    public async Task RunNextAsync_WaitsUntilCalculatedOccurrenceThenRunsCoordinator()
    {
        var now = new DateTimeOffset(2026, 9, 6, 3, 0, 0, TimeSpan.Zero);
        var due = new DateTimeOffset(2026, 9, 7, 2, 0, 0, TimeSpan.Zero);
        var calculator = new FakeScheduleCalculator(due);
        var delay = new FakeScheduleDelay();
        var coordinator = new FakeCoordinator();
        var worker = new ScheduledRefollowWorker(
            calculator, delay, coordinator, new FixedTimeProvider(now));
        await worker.RunNextAsync(default);
        Assert.Equal(due, delay.Due);
        Assert.Equal(1, coordinator.RunCount);
    }

    private sealed class FakeScheduleCalculator(DateTimeOffset due) : IScheduleCalculator
    {
        public DateTimeOffset GetNextOccurrence(DateTimeOffset now) => due;
    }

    private sealed class FakeScheduleDelay : IScheduleDelay
    {
        public DateTimeOffset? Due { get; private set; }
        public Task DelayUntilAsync(DateTimeOffset due, CancellationToken cancellationToken)
        {
            Due = due;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCoordinator : IRefollowCoordinator
    {
        public int RunCount { get; private set; }
        public Task<RefollowRunResult> RunAsync(CancellationToken cancellationToken)
        {
            RunCount++;
            return Task.FromResult(new RefollowRunResult(0, DryRun: true));
        }
        public Task<RefollowRecoveryStatus> GetRecoveryStatusAsync(CancellationToken ct) =>
            Task.FromResult(new RefollowRecoveryStatus("quinn", 0, 0, 0, []));
        public Task<RefollowRecoveryStatus> QueueRecoveryAsync(string login, CancellationToken ct) =>
            GetRecoveryStatusAsync(ct);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
