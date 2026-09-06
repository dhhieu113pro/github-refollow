namespace GitHubRefollow.Scheduling;

public interface IScheduleCalculator
{
    DateTimeOffset GetNextOccurrence(DateTimeOffset now);
}

public sealed class ScheduleCalculator(TimeZoneInfo timeZone) : IScheduleCalculator
{
    private const int ScheduledHour = 9;

    public DateTimeOffset GetNextOccurrence(DateTimeOffset now)
    {
        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)localNow.DayOfWeek + 7) % 7;
        var candidate = localNow.Date.AddDays(daysUntilMonday).AddHours(ScheduledHour);

        if (candidate <= localNow.DateTime)
        {
            candidate = candidate.AddDays(7);
        }

        var unspecifiedCandidate = DateTime.SpecifyKind(candidate, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(unspecifiedCandidate, timeZone);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }
}
