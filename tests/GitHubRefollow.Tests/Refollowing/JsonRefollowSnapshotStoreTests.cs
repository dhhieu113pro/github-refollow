using System.Text.Json;
using GitHubRefollow.Configuration;
using GitHubRefollow.Refollowing;
using Microsoft.Extensions.Options;

namespace GitHubRefollow.Tests.Refollowing;

public sealed class JsonRefollowSnapshotStoreTests : IDisposable
{
    private readonly string dataPath = Path.Combine(
        Path.GetTempPath(),
        $"github-refollow-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveAsync_WritesFollowingSnapshotInsideConfiguredDataPath()
    {
        var options = Options.Create(new RefollowOptions { DataPath = dataPath });
        var store = new JsonRefollowSnapshotStore(options);

        await store.SaveAsync(["alice", "bob"], default);

        var snapshotPath = Path.Combine(dataPath, "following.json");
        Assert.True(File.Exists(snapshotPath));

        var json = await File.ReadAllTextAsync(snapshotPath);
        Assert.Equal(["alice", "bob"], JsonSerializer.Deserialize<string[]>(json));
    }

    public void Dispose()
    {
        if (Directory.Exists(dataPath))
        {
            Directory.Delete(dataPath, recursive: true);
        }
    }
}
