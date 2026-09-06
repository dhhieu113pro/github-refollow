namespace GitHubRefollow.Configuration;

public sealed class RefollowOptions
{
    public const string SectionName = "GitHubRefollow";

    public string Token { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;

    public bool DryRun { get; init; } = true;

    public int DelaySeconds { get; init; } = 2;

    public string DataPath { get; init; } = "/data";

    public string TimeZoneId { get; init; } = "Asia/Ho_Chi_Minh";
}
