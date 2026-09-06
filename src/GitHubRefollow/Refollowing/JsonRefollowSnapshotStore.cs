using System.Text.Json;
using GitHubRefollow.Configuration;
using Microsoft.Extensions.Options;

namespace GitHubRefollow.Refollowing;

public sealed class JsonRefollowSnapshotStore(IOptions<RefollowOptions> options)
    : IRefollowSnapshotStore
{
    private readonly string dataPath = options.Value.DataPath;

    public async Task<IReadOnlyList<string>> LoadPendingAsync(
        CancellationToken cancellationToken)
    {
        var pendingPath = Path.Combine(dataPath, "pending.json");
        if (!File.Exists(pendingPath))
        {
            return [];
        }

        await using var stream = new FileStream(
            pendingPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        return await JsonSerializer.DeserializeAsync<string[]>(
            stream,
            cancellationToken: cancellationToken) ?? [];
    }

    public async Task SaveAsync(
        IReadOnlyList<string> following,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(dataPath);

        var snapshotPath = Path.Combine(dataPath, "following.json");
        await using var stream = new FileStream(
            snapshotPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);

        await JsonSerializer.SerializeAsync(
            stream,
            following,
            cancellationToken: cancellationToken);
    }
}
