using System.Text.Json;
using GitHubRefollow.Configuration;
using GitHubRefollow.Refollowing;
using Microsoft.Extensions.Options;

namespace GitHubRefollow.Tests.Refollowing;

public sealed class JsonRefollowSnapshotStoreTests : IDisposable
{
    private readonly string dataPath = Path.Combine(
        Path.GetTempPath(), $"github-refollow-tests-{Guid.NewGuid():N}");

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

    [Fact]
    public async Task SavePendingAsync_WhenCancelled_PreservesLastCompleteJournal()
    {
        var store = new JsonRefollowSnapshotStore(
            Options.Create(new RefollowOptions { DataPath = dataPath }));
        await store.SavePendingAsync(["rua-den"], default);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.SavePendingAsync(["alice"], cancellation.Token));
        Assert.Equal(["rua-den"], await store.LoadPendingAsync(default));
    }

    public void Dispose()
    {
        if (Directory.Exists(dataPath))
            Directory.Delete(dataPath, recursive: true);
    }
}
