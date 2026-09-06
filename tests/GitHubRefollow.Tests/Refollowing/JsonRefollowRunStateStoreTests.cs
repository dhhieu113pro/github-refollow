using GitHubRefollow.Configuration;
using GitHubRefollow.Refollowing;
using Microsoft.Extensions.Options;

namespace GitHubRefollow.Tests.Refollowing;

public sealed class JsonRefollowRunStateStoreTests : IDisposable
{
    private readonly string dataPath = Path.Combine(
        Path.GetTempPath(),
        $"github-refollow-state-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsLastRunState()
    {
        var options = Options.Create(new RefollowOptions { DataPath = dataPath });
        var store = new JsonRefollowRunStateStore(options);
        var expected = new RefollowLastRun(
            new DateTimeOffset(2026, 9, 7, 2, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 7, 2, 1, 0, TimeSpan.Zero),
            Succeeded: true,
            FollowingCount: 2,
            DryRun: true,
            Error: null);

        await store.SaveAsync(expected, default);
        var actual = await store.LoadAsync(default);

        Assert.Equal(expected, actual);
        Assert.True(File.Exists(Path.Combine(dataPath, "last-run.json")));
    }

    [Fact]
    public async Task LoadAsync_WhenStateDoesNotExist_ReturnsNull()
    {
        var options = Options.Create(new RefollowOptions { DataPath = dataPath });
        var store = new JsonRefollowRunStateStore(options);

        var actual = await store.LoadAsync(default);

        Assert.Null(actual);
    }

    public void Dispose()
    {
        if (Directory.Exists(dataPath))
        {
            Directory.Delete(dataPath, recursive: true);
        }
    }
}
