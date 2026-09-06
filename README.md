# GitHub Re-follow

A small .NET 10 service that freezes your current GitHub **Following** list, then unfollows and follows each account again in sequence. It is designed to run unattended in a WSLC/OCI container while keeping a recovery snapshot and last-run state on a persistent volume.

> **Important:** this service re-follows accounts **you follow** (`?tab=following`). It does not modify the people shown in your **Followers** tab (`?tab=followers`).

## Behavior

- Scheduled every Monday at **09:00 Asia/Ho_Chi_Minh**.
- **Dry-run is enabled by default**: the following list is captured, but GitHub is not mutated.
- Every run verifies the authenticated GitHub account before reading or changing the following list.
- Live runs are sequential and throttled with a configurable delay.
- A GitHub API failure stops the current run instead of continuing after rate-limit or anti-abuse responses.
- Scheduled and manual runs cannot overlap.
- `/data/following.json` keeps the frozen recovery snapshot.
- `/data/last-run.json` keeps sanitized last-run status across container restarts.
- A local dashboard provides status and a **Run now** action.

## GitHub token

Use `GITHUB_REFOLLOW_TOKEN` with a personal access token belonging to the GitHub account you want to re-follow from.

Recommended fine-grained PAT permission:

- **Followers: Read and write**

Classic PAT alternative:

- `user:follow`

Do **not** use an Actions `GITHUB_TOKEN`; this service operates on the authenticated user's following list.

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

The manual run endpoint does not require a separate application key. Keep the dashboard bound to localhost unless you place your own authentication/reverse proxy in front of it.

The container uses:

```text
ghcr.io/dhhieu113pro/github-refollow:latest
```

and `restart: unless-stopped`, so it resumes automatically with the container runtime.

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
- `POST /api/run` — starts one run directly.
- A concurrent `POST /api/run` returns HTTP `409` while another scheduled/manual run is active.

Example:

```bash
curl -X POST http://127.0.0.1:8787/api/run
```

## Local development

```bash
dotnet restore GitHubRefollow.slnx
dotnet test GitHubRefollow.slnx
dotnet run --project src/GitHubRefollow/GitHubRefollow.csproj
```

Configuration can also use normal ASP.NET Core keys such as `GitHubRefollow__DryRun`, but the container-friendly `GITHUB_REFOLLOW_*` variables above are the recommended interface.

## CI and publishing

Pull requests run restore, Release build, tests, container validation, and verify the OCI image exposes only the supported runtime environment variables. Main-branch and `v*` tag pushes publish the OCI image to `ghcr.io/dhhieu113pro/github-refollow`.
