# GitHub Re-follow Service Design

## Goal

Build a small .NET 10 service that runs in a WSLC container and safely re-follows every GitHub account currently followed by the authenticated user. It runs automatically once a week during the user's work week and can also be started manually.

The target is the authenticated user's **Following** list. Re-following does not modify the user's **Followers** list.

## Schedule

- Run every Monday at 09:00 in `Asia/Ho_Chi_Minh`.
- Calculate the next occurrence with an explicit time zone so host and container UTC settings cannot shift the schedule.
- Do not run a missed job immediately after a long outage; wait for the next scheduled occurrence.
- Prevent scheduled and manual runs from overlapping.

## Application

Use an ASP.NET Core 10 application with three internal components:

1. `GitHubFollowingClient` lists followed users and performs follow/unfollow requests.
2. `RefollowService` owns backup creation, sequential processing, throttling, cancellation, and run summaries.
3. `ScheduledRefollowWorker` triggers `RefollowService` at the configured local time.

The web application provides:

- a compact status page showing dry-run/live mode, last run, next run, and the explicit **Following** target;
- `GET /api/status` for sanitized runtime status;
- `POST /api/run` to start a manual run.

The manual endpoint does not require a separate application key. The provided Compose configuration binds the dashboard to `127.0.0.1` by default; deployments that expose it beyond localhost should add their own authentication/reverse proxy.

## Configuration and secrets

Configuration is supplied through environment variables:

| Variable | Required | Default | Purpose |
| --- | --- | --- | --- |
| `GITHUB_REFOLLOW_TOKEN` | Yes | None | Fine-grained PAT with `Followers: Read and write`, or classic PAT with `user:follow` |
| `GITHUB_REFOLLOW_DRY_RUN` | No | `true` | Lists and backs up users without changing follow state |
| `GITHUB_REFOLLOW_DELAY_SECONDS` | No | `2` | Delay between GitHub mutation requests |
| `TZ` | No | `Asia/Ho_Chi_Minh` | Container and schedule time zone |

The PAT is read at runtime, excluded from logs, and never committed. Deployment examples use an ignored `.env` file while recommending container secrets where WSLC supports them.

## Run flow

1. Acquire a process-wide non-blocking run lock. Return `409 Conflict` for a competing manual request.
2. Verify the PAT by reading the authenticated GitHub user.
3. Fetch the complete following list with pagination before making any mutation.
4. Write the frozen following snapshot under `/data`.
5. In dry-run mode, finish with a summary without mutations.
6. Otherwise, process the frozen list sequentially. For each login:
   - unfollow the user;
   - wait for the configured delay;
   - follow the user again;
   - wait before processing the next user.
7. Persist a sanitized run summary under `/data`.

Stop the entire run on authentication failure, rate limiting, GitHub abuse detection, or another GitHub API failure. Never refetch pagination during a run.

## Persistence and logging

- Mount a WSLC volume at `/data`.
- Keep the frozen following-list recovery snapshot and latest-run summary.
- Emit structured console logs for container tooling.
- Never log the PAT or authorization headers.

## Container delivery

- Use a multi-stage Dockerfile and an unprivileged runtime user.
- Expose HTTP port `8080`.
- Provide Compose configuration with `restart: unless-stopped`, the `/data` volume, environment configuration, and host-only port mapping.
- Publish OCI images to `ghcr.io/dhhieu113pro/github-refollow` from GitHub Actions.
- Expose supported environment-variable names in image metadata so Quay can suggest them, without embedding secrets.

## Testing and CI

Unit and integration tests cover:

- pagination and frozen-list behavior;
- authenticated-user verification before snapshots or mutations;
- dry-run behavior;
- successful unfollow/follow ordering;
- authentication, throttling, and anti-abuse failures;
- mutual exclusion for scheduled and manual runs;
- time-zone-aware next-run calculation;
- keyless manual endpoint behavior;
- dashboard clarity around Following versus Followers;
- container environment metadata, including ensuring the removed application-key variable does not return.

CI restores, builds, tests, builds the container, and validates its environment metadata. Image publication occurs only from the default branch and version tags.

## First deployment

The first deployment starts in dry-run mode. The user verifies the detected GitHub identity, **Following** count, backup, next scheduled run, and logs. Mutation is enabled only by setting `GITHUB_REFOLLOW_DRY_RUN=false` and restarting the container. The dashboard must show **Live** before a live manual run is started.
