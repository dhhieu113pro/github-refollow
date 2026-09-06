using GitHubRefollow.Scheduling;

namespace GitHubRefollow.Tests.Scheduling;

public sealed class ScheduleCalculatorTests
{
    private static readonly TimeZoneInfo VietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    [Fact]
    public void GetNextOccurrence_ReturnsNextMondayAtNineVietnamTime()
    {
        var calculator = new ScheduleCalculator(VietnamTimeZone);
        var sundayAtTenVietnam = new DateTimeOffset(2026, 9, 6, 3, 0, 0, TimeSpan.Zero);

        var next = calculator.GetNextOccurrence(sundayAtTenVietnam);

        Assert.Equal(new DateTimeOffset(2026, 9, 7, 2, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void GetNextOccurrence_DoesNotReplayMondayAfterScheduledTime()
    {
        var calculator = new ScheduleCalculator(VietnamTimeZone);
        var mondayAtTenVietnam = new DateTimeOffset(2026, 9, 7, 3, 0, 0, TimeSpan.Zero);

        var next = calculator.GetNextOccurrence(mondayAtTenVietnam);

        Assert.Equal(new DateTimeOffset(2026, 9, 14, 2, 0, 0, TimeSpan.Zero), next);
    }

    [Fact]
    public void GetNextOccurrence_WhenExactlyScheduled_ReturnsFollowingWeek()
    {
        var calculator = new ScheduleCalculator(VietnamTimeZone);
        var mondayAtNineVietnam = new DateTimeOffset(2026, 9, 7, 2, 0, 0, TimeSpan.Zero);

        var next = calculator.GetNextOccurrence(mondayAtNineVietnam);

        Assert.Equal(new DateTimeOffset(2026, 9, 14, 2, 0, 0, TimeSpan.Zero), next);
    }
}
