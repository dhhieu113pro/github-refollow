# GitHub Re-follow Service Implementation Plan

> **Current implementation plan.** This file supersedes the initial API-key-protected manual-run design. The service now relies on localhost binding plus dry-run safety and does not define `GITHUB_REFOLLOW_API_KEY`.

**Goal:** Maintain a tested .NET 10 service that safely re-follows the authenticated user's GitHub **Following** list every Monday at 09:00 Vietnam time in a persistent WSLC container, with an optional manual run from the local dashboard.

**Architecture:** An ASP.NET Core app composes a GitHub following client, persistent snapshot/state stores, mutually exclusive run coordinator, and time-zone-aware hosted scheduler. Minimal API endpoints expose sanitized status and keyless manual execution. Docker and GitHub Actions deliver the OCI image.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, HttpClient, TimeProvider, xUnit, Microsoft.AspNetCore.Mvc.Testing, Docker/Compose, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-09-06-github-refollow-design.md`

## Global Constraints

- Target `net10.0`; enable nullable references and implicit usings.
- Schedule Monday 09:00 in `Asia/Ho_Chi_Minh`; never replay a missed run immediately.
- Default `GITHUB_REFOLLOW_DRY_RUN` to `true`.
- Read the PAT only from `GITHUB_REFOLLOW_TOKEN`; never log or serialize it.
- Verify the authenticated GitHub identity before reading or mutating the following list.
- Freeze the complete paginated **Following** list before mutation.
- Process sequentially and stop on GitHub API failures that make continuing unsafe.
- Reject overlapping manual and scheduled runs.
- Store recovery state below `/data`.
- Keep the supplied Compose port mapping bound to `127.0.0.1` because the manual endpoint is intentionally keyless.
- Make the dashboard explicitly state that the target is **Following**, not **Followers**.
- Publish the container to `ghcr.io/dhhieu113pro/github-refollow`.

## File Map

- `src/GitHubRefollow/Program.cs`: dependency composition, dashboard, status endpoint, and keyless `POST /api/run`.
- `src/GitHubRefollow/Configuration/RefollowOptions.cs`: token, dry-run, delay, data path, and time-zone configuration.
- `src/GitHubRefollow/GitHub/GitHubFollowingClient.cs`: authenticated user, paginated following list, unfollow/follow operations, and classified failures.
- `src/GitHubRefollow/Refollowing/*`: frozen snapshot, run state, coordination, and execution.
- `src/GitHubRefollow/Scheduling/*`: next-run calculation and hosted scheduler.
- `tests/GitHubRefollow.Tests/*`: unit and in-memory HTTP tests.
- `Dockerfile`, `compose.yml`, `.env.example`, `.github/workflows/*`, `README.md`: delivery and operations.

---

### Task 1: Configuration and schedule

- [x] Keep `DryRun=true`, `DelaySeconds=2`, `DataPath=/data`, and `TimeZoneId=Asia/Ho_Chi_Minh` defaults.
- [x] Keep the PAT in `Token` only; there is no secondary application credential.
- [x] Calculate the next Monday 09:00 occurrence using the configured time zone.
- [x] Cover defaults and schedule calculations with tests.

### Task 2: GitHub API client

- [x] Implement `GET user` for authenticated identity verification.
- [x] Implement paginated `GET user/following?per_page=100`.
- [x] Implement escaped `DELETE` and `PUT user/following/{login}` mutations.
- [x] Send GitHub media type, user agent, bearer PAT, and pinned API version headers.
- [x] Classify authentication, rate limit, abuse detection, transient, and permanent failures without leaking bodies or credentials.

### Task 3: Safe re-follow execution

- [x] Verify the authenticated identity before loading the following list.
- [x] Freeze the full list and save the recovery snapshot before any mutation.
- [x] Return immediately after the snapshot when dry-run is enabled.
- [x] In live mode, process each login as DELETE → delay → PUT → delay.
- [x] Stop the current run on GitHub API failure.
- [x] Prevent scheduled/manual overlap through the coordinator.

### Task 4: HTTP API and dashboard

- [x] Provide `GET /` dashboard.
- [x] Provide `GET /api/status` with dry-run/live mode, delay, next scheduled run, and sanitized last-run state.
- [x] Provide keyless `POST /api/run` for local manual execution.
- [x] Return `409 Conflict` when another run is active.
- [x] Show **Following** as the explicit target and explain that **Followers** are not modified.
- [x] Remove credential input and browser storage from the dashboard.
- [x] Keep Compose bound to `127.0.0.1:8787` by default; users exposing the service remotely must add their own authentication layer.

### Task 5: Container configuration

Supported environment variables are exactly:

```text
GITHUB_REFOLLOW_TOKEN
GITHUB_REFOLLOW_DRY_RUN
GITHUB_REFOLLOW_DELAY_SECONDS
TZ
```

- [x] Declare these variables in runtime image metadata so Quay can suggest them.
- [x] Do not bake PAT values into the image.
- [x] Use `restart: unless-stopped` and a persistent `/data` volume.
- [x] Keep dry-run enabled by default.
- [x] Ensure removed configuration variables do not reappear in image metadata.

### Task 6: Tests and CI

- [x] Test pagination and mutation ordering.
- [x] Test authenticated-user verification before snapshot/mutation.
- [x] Test dry-run behavior.
- [x] Test mutual exclusion and persisted run state.
- [x] Test dashboard Following-vs-Followers messaging.
- [x] Test that manual run succeeds without a secondary credential.
- [x] Build the Docker image in CI and inspect runtime environment metadata.
- [x] Publish GHCR images from the default branch and version tags.

### Task 7: Acceptance verification

Before completion:

```bash
dotnet restore GitHubRefollow.slnx
dotnet build GitHubRefollow.slnx --configuration Release --no-restore
dotnet test GitHubRefollow.slnx --configuration Release --no-build
docker build --tag github-refollow:ci .
```

Then inspect image metadata and verify it contains the four supported variables above and no removed manual-run credential variable.

For a deployment smoke test:

1. Start with `GITHUB_REFOLLOW_DRY_RUN=true`.
2. Confirm the dashboard says **Dry run** and target **Following**.
3. Run manually and verify the frozen following count/snapshot.
4. Set `GITHUB_REFOLLOW_DRY_RUN=false`, recreate the container, and verify the dashboard says **Live** before performing mutations.
5. Check the GitHub **Following** tab when validating behavior; the **Followers** tab is not the target.
