using System.Text.Json;
using GitHubRefollow.Configuration;
using Microsoft.Extensions.Options;

namespace GitHubRefollow.Refollowing;

public sealed class JsonRefollowRunStateStore(IOptions<RefollowOptions> options)
    : IRefollowRunStateStore
{
    private readonly string dataPath = options.Value.DataPath;

    public async Task<RefollowLastRun?> LoadAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(dataPath, "last-run.json");
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<RefollowLastRun>(
            stream,
            cancellationToken: cancellationToken);
    }

    public async Task SaveAsync(
        RefollowLastRun state,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(dataPath);

        var path = Path.Combine(dataPath, "last-run.json");
        await using var stream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);

        await JsonSerializer.SerializeAsync(
            stream,
            state,
            cancellationToken: cancellationToken);
    }
}
