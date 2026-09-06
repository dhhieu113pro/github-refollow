using GitHubRefollow.Refollowing;

namespace GitHubRefollow.Scheduling;

public interface IScheduleDelay
{
    Task DelayUntilAsync(DateTimeOffset due, CancellationToken cancellationToken);
}

public sealed class SystemScheduleDelay(TimeProvider timeProvider) : IScheduleDelay
{
    public Task DelayUntilAsync(DateTimeOffset due, CancellationToken cancellationToken)
    {
        var delay = due - timeProvider.GetUtcNow();
        return delay <= TimeSpan.Zero
            ? Task.CompletedTask
            : Task.Delay(delay, timeProvider, cancellationToken);
    }
}

public sealed class ScheduledRefollowWorker(
    IScheduleCalculator scheduleCalculator,
    IScheduleDelay scheduleDelay,
    IRefollowCoordinator coordinator,
    TimeProvider timeProvider) : BackgroundService
{
    public async Task RunNextAsync(CancellationToken cancellationToken)
    {
        var due = scheduleCalculator.GetNextOccurrence(timeProvider.GetUtcNow());
        await scheduleDelay.DelayUntilAsync(due, cancellationToken);
        await coordinator.RunAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // The coordinator persists a sanitized failure state. The next loop
                // recalculates the next scheduled occurrence instead of retrying
                // immediately, which avoids hammering GitHub after rate/abuse errors.
            }
        }
    }
}
