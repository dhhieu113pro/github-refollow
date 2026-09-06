using System.Text.Json;
using GitHubRefollow.Configuration;
using Microsoft.Extensions.Options;

namespace GitHubRefollow.Refollowing;

public sealed class JsonRefollowSnapshotStore(IOptions<RefollowOptions> options)
    : IRefollowSnapshotStore
{
    private readonly string dataPath = options.Value.DataPath;

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
