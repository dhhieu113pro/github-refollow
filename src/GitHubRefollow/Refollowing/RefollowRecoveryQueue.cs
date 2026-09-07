using System.Text.RegularExpressions;
using GitHubRefollow.GitHub;

namespace GitHubRefollow.Refollowing;

public sealed record RefollowRecoveryStatus(
    string AuthenticatedLogin,
    int FollowingCount,
    int RecoveryCount,
    int TargetCount,
    IReadOnlyList<string> RecoveryUsers);

public interface IRefollowRecoveryQueue
{
    Task<RefollowRecoveryStatus> GetStatusAsync(CancellationToken cancellationToken);
    Task<RefollowRecoveryStatus> QueueAsync(string login, CancellationToken cancellationToken);
}

public sealed partial class RefollowRecoveryQueue(
    IGitHubFollowingClient client,
    IRefollowSnapshotStore snapshotStore) : IRefollowRecoveryQueue
{
    [GeneratedRegex(@"\A[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,37}[a-zA-Z0-9])?\z", RegexOptions.CultureInvariant)]
    private static partial Regex LoginPattern();

    public async Task<RefollowRecoveryStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var owner = await client.GetAuthenticatedLoginAsync(cancellationToken);
        return await ReadStatusAsync(owner, cancellationToken);
    }

    public async Task<RefollowRecoveryStatus> QueueAsync(string login, CancellationToken cancellationToken)
    {
        login = login?.Trim() ?? string.Empty;
        if (!LoginPattern().IsMatch(login))
        {
            throw new ArgumentException("Enter a valid GitHub login (not a profile URL).", nameof(login));
        }

        var owner = await client.GetAuthenticatedLoginAsync(cancellationToken);
        var canonical = await client.GetUserLoginAsync(login, cancellationToken);
        if (string.Equals(owner, canonical, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("You cannot add your own account to recovery.", nameof(login));
        }

        var following = await client.GetFollowingAsync(cancellationToken);
        var pending = await snapshotStore.LoadPendingAsync(cancellationToken);
        var comparer = StringComparer.OrdinalIgnoreCase;
        if (!following.Contains(canonical, comparer) && !pending.Contains(canonical, comparer))
        {
            await snapshotStore.SavePendingAsync(
                pending.Append(canonical).Distinct(comparer).ToArray(), cancellationToken);
        }

        return await ReadStatusAsync(owner, cancellationToken);
    }

    private async Task<RefollowRecoveryStatus> ReadStatusAsync(
        string owner, CancellationToken cancellationToken)
    {
        var following = (await client.GetFollowingAsync(cancellationToken))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var pending = await snapshotStore.LoadPendingAsync(cancellationToken);
        var missing = pending.Except(following, StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new RefollowRecoveryStatus(
            owner, following.Length, missing.Length, following.Length + missing.Length, missing);
    }
}
