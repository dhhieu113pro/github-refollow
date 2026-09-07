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
            return [];

        await using var stream = new FileStream(
            pendingPath, FileMode.Open, FileAccess.Read,
            FileShare.Read | FileShare.Delete, bufferSize: 4096, useAsync: true);
        return await JsonSerializer.DeserializeAsync<string[]>(
            stream, cancellationToken: cancellationToken) ?? [];
    }

    public Task SaveAsync(
        IReadOnlyList<string> following, CancellationToken cancellationToken) =>
        WriteAsync("following.json", following, cancellationToken);

    public Task SavePendingAsync(
        IReadOnlyList<string> pending, CancellationToken cancellationToken) =>
        WriteAsync("pending.json", pending, cancellationToken);

    private async Task WriteAsync(
        string fileName, IReadOnlyList<string> users, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(dataPath);
        var path = Path.Combine(dataPath, fileName);
        var temporary = Path.Combine(dataPath, $".{fileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, users, cancellationToken: cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
