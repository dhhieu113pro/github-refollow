# GitHub Re-follow Service Design

## Goal

Build a small .NET 10 service that runs in a WSLC container and safely re-follows every GitHub account currently followed by the authenticated user. It runs automatically once a week during the user's work week and can also be started manually.

## Schedule

- Run every Monday at 09:00 in `Asia/Ho_Chi_Minh`.
- Calculate the next occurrence with an explicit time zone so host and container UTC settings cannot shift the schedule.
- Do not run a missed job immediately after a long outage; wait for the next scheduled occurrence.
- Prevent scheduled and manual runs from overlapping.

## Application

Use an ASP.NET Core 10 application with three internal components:

1. `GitHubFollowingClient` lists followed users and performs follow/unfollow requests.
2. `RefollowService` owns backup creation, sequential processing, throttling, cancellation, and run summaries.
3. `WeeklyScheduler` triggers `RefollowService` at the configured local time.

The web application provides:

- a compact status page showing configuration validity, last run, next run, and the latest summary;
- `POST /api/refollow` to start a manual run;
- `GET /health` for container health checks.

Manual mutation requests require an API key. Status and health responses never expose credentials or full GitHub API responses.

## Configuration and secrets

Configuration is supplied through environment variables:

| Variable | Required | Default | Purpose |
| --- | --- | --- | --- |
| `GITHUB_REFOLLOW_TOKEN` | Yes | None | Fine-grained PAT with `Followers: Read and write`, or classic PAT with `user:follow` |
| `GITHUB_REFOLLOW_API_KEY` | Yes | None | Protects the manual-run endpoint |
| `GITHUB_REFOLLOW_DRY_RUN` | No | `true` | Lists and backs up users without changing follow state |
| `GITHUB_REFOLLOW_DELAY_SECONDS` | No | `2` | Delay between GitHub mutation requests |
| `TZ` | No | `Asia/Ho_Chi_Minh` | Container and schedule time zone |
| `GITHUB_REFOLLOW_DATA_PATH` | No | `/data` | Persistent state, backups, and summaries |

The application will fail readiness when required secrets are absent. Secrets are read at runtime, excluded from logs, and never committed. Deployment examples use an ignored `.env` file while recommending container secrets where WSLC supports them.

## Run flow

1. Acquire a process-wide non-blocking run lock. Return `409 Conflict` for a competing manual request.
2. Verify the token by reading the authenticated GitHub user.
3. Fetch the complete following list with pagination before making any mutation.
4. Write a timestamped JSON backup atomically under `/data/backups`.
5. In dry-run mode, finish with a summary without mutations.
6. Otherwise, process the frozen list sequentially. For each login:
   - unfollow the user;
   - wait for the configured delay;
   - follow the user again;
   - wait before processing the next user.
7. Persist a sanitized run summary atomically under `/data/state`.

If unfollow succeeds but follow fails transiently, retry only the follow operation with bounded exponential backoff. Stop the entire run on authentication failure, rate limiting, GitHub abuse detection, or exhausted retries. Record the affected login so a later manual run can recover it. Never refetch pagination during a run.

## Persistence and logging

- Mount a WSLC volume at `/data`.
- Keep timestamped following-list backups and a small latest-run summary.
- Emit structured console logs for container tooling.
- Retain the newest 12 backups and remove older backups only after a successful new backup.
- Never log the PAT, API key, or authorization headers.

## Container delivery

- Use a multi-stage Dockerfile and an unprivileged runtime user.
- Expose HTTP port `8080`.
- Include a health check against `/health`.
- Provide Compose configuration with `restart: unless-stopped`, the `/data` volume, environment configuration, and port mapping.
- Publish multi-platform `linux/amd64` and `linux/arm64` images to `ghcr.io/dhhieu113pro/github-refollow` from GitHub Actions.

## Testing and CI

Unit and integration tests cover:

- pagination and frozen-list behavior;
- dry-run behavior;
- successful unfollow/follow ordering;
- partial failure and follow-only retry;
- authentication, throttling, and anti-abuse failures;
- mutual exclusion for scheduled and manual runs;
- time-zone-aware next-run calculation;
- backup retention and secret-safe status responses;
- protected manual endpoint and health endpoint.

CI restores, builds, tests, and builds the container. Image publication occurs only from the default branch and version tags.

## First deployment

The first deployment starts in dry-run mode. The user verifies the detected GitHub identity, following count, backup, next scheduled run, and logs. Mutation is enabled only by setting `GITHUB_REFOLLOW_DRY_RUN=false` and restarting the container.
