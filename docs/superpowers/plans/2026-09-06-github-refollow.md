# GitHub Re-follow Service Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a tested .NET 10 service that safely re-follows the authenticated user's GitHub following list every Monday at 09:00 Vietnam time in a persistent WSLC container.

**Architecture:** An ASP.NET Core app composes a typed GitHub client, atomic file state store, mutually exclusive orchestrator, and time-zone-aware hosted scheduler. Minimal API endpoints expose health, sanitized status, and API-key-protected manual execution; Docker and GitHub Actions deliver amd64/arm64 images.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, HttpClient, TimeProvider, xUnit, Microsoft.AspNetCore.Mvc.Testing, Docker/Compose, GitHub Actions

**Spec:** `docs/superpowers/specs/2026-09-06-github-refollow-design.md`

## Global Constraints

- Target `net10.0`; enable nullable references and implicit usings.
- Schedule Monday 09:00 in `Asia/Ho_Chi_Minh`; never replay a missed run immediately.
- Default `GITHUB_REFOLLOW_DRY_RUN` to `true`.
- Read the PAT only from `GITHUB_REFOLLOW_TOKEN`; never log or serialize secrets.
- Freeze and atomically back up all paginated results before mutation.
- Process sequentially; stop on authentication, rate limiting, abuse detection, or exhausted follow retries.
- Reject overlapping manual and scheduled runs.
- Store state below `/data`, retaining the newest 12 backups.
- Publish unprivileged `linux/amd64` and `linux/arm64` images to `ghcr.io/dhhieu113pro/github-refollow`.

## File Map

- `src/GitHubRefollow/Program.cs`: dependency composition and endpoint mapping.
- `src/GitHubRefollow/Configuration/RefollowOptions.cs`: validated configuration.
- `src/GitHubRefollow/GitHub/*`: REST client and classified failures.
- `src/GitHubRefollow/Refollow/*`: models, atomic state, and orchestration.
- `src/GitHubRefollow/Scheduling/*`: next-run calculation and hosted scheduler.
- `src/GitHubRefollow/Web/ApiKeyFilter.cs`: constant-time API-key validation.
- `src/GitHubRefollow/wwwroot/index.html`: status and manual-run dashboard.
- `tests/GitHubRefollow.Tests/*`: unit and in-memory HTTP tests.
- `Dockerfile`, `compose.yaml`, `.github/workflows/*`, `README.md`: delivery and operations.

---

### Task 1: Solution, configuration, and schedule

**Files:** Create `GitHubRefollow.slnx`, `src/GitHubRefollow/GitHubRefollow.csproj`, `src/GitHubRefollow/Configuration/RefollowOptions.cs`, `src/GitHubRefollow/Scheduling/ScheduleCalculator.cs`, `tests/GitHubRefollow.Tests/GitHubRefollow.Tests.csproj`, and corresponding configuration/schedule tests.

**Interfaces:** Produces `RefollowOptions` and `IScheduleCalculator.GetNextOccurrence(DateTimeOffset now)`.

- [ ] **Step 1: Scaffold projects**

```bash
dotnet new sln -n GitHubRefollow --format slnx
dotnet new web -n GitHubRefollow -o src/GitHubRefollow --framework net10.0
dotnet new xunit -n GitHubRefollow.Tests -o tests/GitHubRefollow.Tests --framework net10.0
dotnet sln GitHubRefollow.slnx add src/GitHubRefollow/GitHubRefollow.csproj tests/GitHubRefollow.Tests/GitHubRefollow.Tests.csproj
dotnet add tests/GitHubRefollow.Tests/GitHubRefollow.Tests.csproj reference src/GitHubRefollow/GitHubRefollow.csproj
```

- [ ] **Step 2: Write failing tests**

```csharp
[Fact]
public void NextOccurrence_IsMondayNineVietnam()
{
    var sut = new ScheduleCalculator(TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"));
    var actual = sut.GetNextOccurrence(new DateTimeOffset(2026, 9, 6, 3, 0, 0, TimeSpan.Zero));
    Assert.Equal(new DateTimeOffset(2026, 9, 7, 2, 0, 0, TimeSpan.Zero), actual);
}

[Fact]
public void Options_DefaultToDryRun() => Assert.True(new RefollowOptions().DryRun);
```

- [ ] **Step 3: Run `dotnet test tests/GitHubRefollow.Tests/GitHubRefollow.Tests.csproj --filter "ScheduleCalculatorTests|RefollowOptionsTests"`; expect missing-type failures.**
- [ ] **Step 4: Implement options (`Token`, `ApiKey`, `DryRun=true`, `DelaySeconds=2`, `DataPath=/data`, `TimeZoneId=Asia/Ho_Chi_Minh`) and calculate the next Monday 09:00 strictly after now using `TimeZoneInfo`.**
- [ ] **Step 5: Run `dotnet test GitHubRefollow.slnx`; expect PASS.**
- [ ] **Step 6: Run `git add GitHubRefollow.slnx src tests && git commit -m "feat: scaffold refollow service and schedule"`.**

### Task 2: GitHub API client

**Files:** Create `GitHub/GitHubApiException.cs`, `GitHub/GitHubFollowingClient.cs`, `GitHubFollowingClientTests.cs`, and `TestSupport/StubHttpMessageHandler.cs`.

**Interfaces:** Produce `IGitHubFollowingClient` with authenticated-login, paginated-list, unfollow, and follow methods.

- [ ] **Step 1: Write failing pagination and mutation tests.**

```csharp
[Fact]
public async Task GetFollowingAsync_FollowsNextLinks()
{
    var handler = StubHttpMessageHandler.Sequence(
        StubHttpMessageHandler.Json("[{\"login\":\"alice\"}]", "<https://api.github.com/user/following?page=2>; rel=\"next\""),
        StubHttpMessageHandler.Json("[{\"login\":\"bob\"}]"));
    var users = await CreateClient(handler).GetFollowingAsync(default);
    Assert.Equal(["alice", "bob"], users);
}
```

- [ ] **Step 2: Run `dotnet test ... --filter GitHubFollowingClientTests`; expect missing-type failures.**
- [ ] **Step 3: Implement `GET user`, `GET user/following?per_page=100`, and escaped `DELETE`/`PUT user/following/{login}`. Send GitHub media type, user agent, bearer PAT, and API version `2026-03-10`.**
- [ ] **Step 4: Classify 401 as authentication; rate-header 403 as rate limit; spam/abuse 403 or 422 as abuse; 408/429/5xx as transient; remaining errors as permanent. Exceptions expose status and classification, never bodies or headers.**
- [ ] **Step 5: Run focused tests and `dotnet test GitHubRefollow.slnx`; expect PASS.**
- [ ] **Step 6: Commit `feat: add GitHub following API client`.**

### Task 3: Atomic persistence

**Files:** Create `Refollow/RefollowModels.cs`, `Refollow/RefollowStateStore.cs`, and `RefollowStateStoreTests.cs`.

**Interfaces:** Produce immutable `RefollowRunSummary`, `RefollowUserResult`, and `IRefollowStateStore` backup/summary methods.

- [ ] **Step 1: Write failing tests that save 13 timestamped backups and assert only the newest 12 remain, and that latest-state replacement leaves no temporary file.**

```csharp
for (var i = 0; i < 13; i++)
    await store.SaveBackupAsync(DateTimeOffset.UnixEpoch.AddMinutes(i), ["alice"], default);
Assert.Equal(12, Directory.GetFiles(Path.Combine(root, "backups"), "*.json").Length);
```

- [ ] **Step 2: Run `dotnet test ... --filter RefollowStateStoreTests`; expect missing-type failures.**
- [ ] **Step 3: Serialize into a unique same-directory temporary file, flush it, then use `File.Move(temp, destination, true)`. Prune only after the new backup exists and use timestamp filenames ordered ordinally.**
- [ ] **Step 4: Run focused/full tests; expect PASS.**
- [ ] **Step 5: Commit `feat: persist refollow backups and state`.**

### Task 4: Safe re-follow orchestration

**Files:** Create `Refollow/RefollowService.cs` and `RefollowServiceTests.cs`.

**Interfaces:** Produce `IRefollowService.TryRunAsync(RefollowTrigger, CancellationToken)` returning `RefollowStartResult`.

- [ ] **Step 1: Write failing dry-run, ordering, follow-retry, classified-stop, cancellation, and overlap tests.**

```csharp
await service.TryRunAsync(RefollowTrigger.Scheduled, default);
Assert.Equal(["DELETE alice", "PUT alice", "DELETE bob", "PUT bob"], github.Mutations);

var second = await busyService.TryRunAsync(RefollowTrigger.Manual, default);
Assert.False(second.Started);
```

- [ ] **Step 2: Run `dotnet test ... --filter RefollowServiceTests`; expect missing-type failures.**
- [ ] **Step 3: Use `SemaphoreSlim(1,1)` with zero-timeout acquisition. Fetch identity and the entire list, save backup, then return in dry-run mode.**
- [ ] **Step 4: For each frozen login call DELETE, configured delay, PUT, configured delay. Retry only transient PUT failures three times after 2/4/8 seconds through `TimeProvider`; persist a sanitized summary in `finally`.**
- [ ] **Step 5: Run focused/full tests; expect PASS.**
- [ ] **Step 6: Commit `feat: orchestrate safe refollow runs`.**

### Task 5: HTTP API and dashboard

**Files:** Modify `Program.cs`; create `Web/ApiKeyFilter.cs`, `wwwroot/index.html`, and `WebApplicationTests.cs`.

**Interfaces:** Produce `GET /health`, `GET /api/status`, `POST /api/refollow`, and `/`.

- [ ] **Step 1: Add `Microsoft.AspNetCore.Mvc.Testing` 10.0.0 to the test project.**
- [ ] **Step 2: Write failing tests for anonymous health, sanitized status, 401 without API key, 202 accepted run, and 409 overlap.**

```csharp
Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/refollow", null)).StatusCode);
```

- [ ] **Step 3: Bind environment names to `GitHubRefollow__Token`, `__ApiKey`, `__DryRun`, `__DelaySeconds`, `__DataPath`, and `__TimeZoneId`; validate required secrets at startup.**
- [ ] **Step 4: Register the typed client, singleton state/orchestrator, time provider, calculator, static files, and endpoints. Compare API-key UTF-8 bytes with `CryptographicOperations.FixedTimeEquals`.**
- [ ] **Step 5: Build a dependency-free responsive dashboard that fetches status and prompts for an in-memory API key only on Run now. Never persist the key in browser storage.**
- [ ] **Step 6: Run focused/full tests; expect PASS.**
- [ ] **Step 7: Commit `feat: expose refollow dashboard and API`.**

### Task 6: Weekly hosted scheduler

**Files:** Create `Scheduling/WeeklyScheduler.cs`, modify `Program.cs`, and create `WeeklySchedulerTests.cs`.

**Interfaces:** Consume `IScheduleCalculator`, `IRefollowService`, and `TimeProvider`; produce registered `IHostedService`.

- [ ] **Step 1: Add `Microsoft.Extensions.TimeProvider.Testing` 10.0.0 and write a failing fake-time test that advances from 08:59 to 09:00 Vietnam time and observes exactly one scheduled call.**
- [ ] **Step 2: Run `dotnet test ... --filter WeeklySchedulerTests`; expect missing-type failures.**
- [ ] **Step 3: Implement a `BackgroundService` loop: calculate a future occurrence, `Task.Delay(next-now, timeProvider, token)`, invoke once, and recalculate from the current time. Catch cancellation only when stopping.**
- [ ] **Step 4: Register the hosted service and log only timestamps, run ID, counts, and failure classification.**
- [ ] **Step 5: Run full tests; expect PASS.**
- [ ] **Step 6: Commit `feat: schedule weekly refollow runs`.**

### Task 7: WSLC container and CI delivery

**Files:** Create `Dockerfile`, `.dockerignore`, `compose.yaml`, `.env.example`, `.gitignore`, `tests/container-smoke.sh`, `.github/workflows/ci.yml`, `.github/workflows/container.yml`, and `README.md`.

**Interfaces:** Produce WSLC Compose deployment and GHCR amd64/arm64 images.

- [ ] **Step 1: Write `tests/container-smoke.sh` that runs the image with dummy required settings, polls `/health` for 30 seconds, prints logs on failure, and always removes the container.**
- [ ] **Step 2: Add a .NET 10 multi-stage Dockerfile. Run as built-in `app`, expose 8080, create/chown `/data`, set Vietnam TZ, and declare `/data` as a volume.**
- [ ] **Step 3: Add Compose with `restart: unless-stopped`, port 8080, named `/data` volume, `.env` interpolation, and safe dry-run default.**

```yaml
environment:
  GitHubRefollow__Token: ${GITHUB_REFOLLOW_TOKEN}
  GitHubRefollow__ApiKey: ${GITHUB_REFOLLOW_API_KEY}
  GitHubRefollow__DryRun: ${GITHUB_REFOLLOW_DRY_RUN:-true}
```

- [ ] **Step 4: Add CI restore/build/test/container-smoke gates. Add GHCR workflow on `main` and `v*` with packages write, QEMU, Buildx, metadata, and `linux/amd64,linux/arm64`.**
- [ ] **Step 5: Document PAT permissions, `.env`, first dry run, enabling mutations, manual run, backup recovery, logs, health, and upgrades.**
- [ ] **Step 6: Run `dotnet build -c Release`, `dotnet test -c Release`, `docker build`, smoke test, and `docker compose config`; expect every command to exit 0.**
- [ ] **Step 7: Commit `feat: ship WSLC container and GHCR workflow`.**

### Task 8: Acceptance and security verification

**Files:** Modify only files revealed by verification failures.

**Interfaces:** Produce a release-ready default branch.

- [ ] **Step 1: Run `git grep -nE 'github_pat_[A-Za-z0-9_]+|ghp_[A-Za-z0-9]+' -- ':!docs/superpowers/plans/*'`; expect no matches.**
- [ ] **Step 2: Run `dotnet format GitHubRefollow.slnx --verify-no-changes`, Release build/tests, container build/smoke, and Compose validation; expect exit 0.**
- [ ] **Step 3: With a real PAT supplied only in the shell, run a manual dry run. Confirm identity/count, complete backup, next schedule, and absence of secrets from status/logs.**
- [ ] **Step 4: Commit verification fixes as `fix: address acceptance verification`; skip the commit if no files changed.**
