# GitHub Re-follow

A small .NET 10 service that freezes your current GitHub **Following** list, then unfollows and follows each account again in sequence. It is designed to run unattended in a WSLC/OCI container while keeping recovery snapshots and last-run state on a persistent volume.

> **Important:** this service re-follows accounts **you follow** (`?tab=following`). It does not modify the people shown in your **Followers** tab (`?tab=followers`).

## Behavior

- Scheduled every Monday at **09:00 Asia/Ho_Chi_Minh**.
- **Dry-run is enabled by default**: the following list is captured, but GitHub is not mutated.
- Every run verifies the authenticated GitHub account before reading or changing the following list.
- Live runs are sequential and throttled with a configurable delay.
- A GitHub API failure stops the current run instead of continuing after rate-limit or anti-abuse responses.
- Scheduled and manual runs cannot overlap. Adding a recovery account also uses the same run lock.
- `/data/following.json` keeps the frozen snapshot and `/data/pending.json` keeps unfinished recovery targets. Snapshot writes are atomic.
- `/data/last-run.json` keeps sanitized last-run status across container restarts.
- A local dashboard provides status, a **Run now** action, and a **Recover missing user** form.

### Interrupted runs and missing accounts

A previous version could lose an account if the process stopped after an unfollow request but before the matching follow request. The next run would read the shorter GitHub Following list and no longer include that account.

The recovery journal prevents this in new live runs. Before any mutation, the service persists the complete target list. After each successful follow, it removes that account from the pending journal. On the next run, it merges remaining pending users with the current Following list, case-insensitively, before making any changes. Dry runs preserve pending recovery entries.

For an account already missing from the Following list, open **Recover missing user** and enter its GitHub username (not a profile URL). The service verifies that the account exists, prevents self-following, and adds it to the persistent pending journal only if it is not already followed or queued. This operation does **not** call GitHub's follow/unfollow mutation APIs. It is idempotent and does not permanently pin accounts: after a successful live re-follow, the entry is removed from the journal.

The dashboard shows the authenticated account and a live breakdown such as `19 Following + 1 Recovery = 20 targets`. Recovery counts represent pending accounts missing from the current Following list, not a second copy of accounts already followed. The actual count is read from GitHub and the journal; it is never hard-coded.

To repair an existing missing account safely:

1. Update/recreate the container using the new image, preserving the existing `/data` volume. Do not delete the recovery data.
2. Keep dry-run enabled and enter the missing username in **Recover missing user**.
3. Confirm the target count and authenticated identity, then run a dry-run to inspect the frozen snapshot.
4. When ready, set `GITHUB_REFOLLOW_DRY_RUN=false`, recreate/restart the container, confirm **Live**, and start the run. A live run re-follows every account in the frozen target set, not just the recovery account.
5. Verify the account is present on GitHub and the recovery count returns to zero. If GitHub rejects a mutation, the service stops and retains unfinished targets for another attempt.

Do not restore an entire old snapshot just to recover one account: that could unintentionally re-follow users you intentionally removed. Add only the specific missing account. An old snapshot is useful for identifying a missing login, but an empty or overwritten snapshot cannot prove which accounts were previously followed.

## GitHub token

Use `GITHUB_REFOLLOW_TOKEN` with a personal access token belonging to the GitHub account you want to re-follow from.

Recommended fine-grained PAT permission:

- **Followers: Read and write**

Classic PAT alternative:

- `user:follow`

Do **not** use an Actions `GITHUB_TOKEN`; this service operates on the authenticated user's following list. Never commit or paste a real PAT into logs or public files. Revoke and replace a token if it has been exposed.

## Container / WSLC

Copy the environment template and set the token:

```bash
cp .env.example .env
```

At minimum set:

```dotenv
GITHUB_REFOLLOW_TOKEN=github_pat_...
GITHUB_REFOLLOW_DRY_RUN=true
```

Start it with a Compose-compatible OCI runtime:

```bash
docker compose up -d
```

The dashboard is intentionally bound to the host only:

```text
http://127.0.0.1:8787
```

The manual run and recovery endpoints do not require a separate application key. Keep the dashboard bound to localhost unless you place your own authentication/reverse proxy in front of it. These endpoints are administrative operations and must not be exposed publicly without access control.

The container uses:

```text
ghcr.io/dhhieu113pro/github-refollow:latest
```

and `restart: unless-stopped`, so it resumes automatically with the container runtime. Preserve the `/data` volume when upgrading or recreating the container.

### Safe first run

1. Leave `GITHUB_REFOLLOW_DRY_RUN=true`.
2. Open the dashboard and confirm it shows **Dry run** and target **Following**.
3. Use **Run now** and inspect the detected user count plus `/data/following.json` in the persistent volume.
4. Only after the dry run looks correct, set `GITHUB_REFOLLOW_DRY_RUN=false` and recreate/restart the container.
5. Confirm the dashboard now shows **Live** before running manually.

The default delay is two seconds between mutation calls. Increase `GITHUB_REFOLLOW_DELAY_SECONDS` if you want a more conservative pace.

## Environment variables

| Variable | Default | Purpose |
| --- | --- | --- |
| `GITHUB_REFOLLOW_TOKEN` | required for GitHub calls | PAT used for following APIs |
| `GITHUB_REFOLLOW_DRY_RUN` | `true` | Disables unfollow/follow mutations when true |
| `GITHUB_REFOLLOW_DELAY_SECONDS` | `2` | Delay between sequential mutation calls |
| `TZ` | `Asia/Ho_Chi_Minh` | Time zone used for the Monday 09:00 schedule |

## HTTP endpoints

- `GET /` — status dashboard.
- `GET /api/status` — dry-run mode, delay, next scheduled run, and persisted last-run state.
- `GET /api/recovery` — authenticated login, current Following count, missing recovery count, total targets, and missing recovery usernames.
- `POST /api/recovery` — validates and queues one missing username without changing GitHub.
- `POST /api/run` — starts one run directly.
- Concurrent run or recovery-queue writes return HTTP `409` while another operation is active.

Example: queue a missing account, inspect the target list, then run a dry-run:

```bash
curl -X POST http://127.0.0.1:8787/api/recovery \
  -H 'Content-Type: application/json' \
  -d '{"login":"rua-den"}'
curl http://127.0.0.1:8787/api/recovery
curl -X POST http://127.0.0.1:8787/api/run
```

The recovery endpoint returns `400` for invalid/self logins, `404` for an unknown GitHub user, and `502` for other GitHub API failures. It does not expose the PAT or upstream response body.

## Local development

```bash
dotnet restore GitHubRefollow.slnx
dotnet test GitHubRefollow.slnx
dotnet run --project src/GitHubRefollow/GitHubRefollow.csproj
```

Configuration can also use normal ASP.NET Core keys such as `GitHubRefollow__DryRun`, but the container-friendly `GITHUB_REFOLLOW_*` variables above are the recommended interface.

## CI and publishing

Pull requests run restore, Release build, tests, container validation, and verify the OCI image exposes only the supported runtime environment variables. Main-branch and `v*` tag pushes publish the OCI image to `ghcr.io/dhhieu113pro/github-refollow`.
