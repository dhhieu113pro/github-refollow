using GitHubRefollow.Configuration;
using GitHubRefollow.GitHub;
using GitHubRefollow.Refollowing;
using GitHubRefollow.Scheduling;
using Microsoft.Extensions.Options;

const string DashboardHtml = """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <title>GitHub Re-follow</title>
  <style>
    :root { color-scheme: light dark; font-family: Inter, system-ui, sans-serif; }
    body { margin: 0; min-height: 100vh; display: grid; place-items: center; background: Canvas; color: CanvasText; }
    main { box-sizing: border-box; width: min(900px, calc(100% - 32px)); margin: 24px 0; border: 1px solid color-mix(in srgb, CanvasText 18%, transparent); border-radius: 18px; padding: 24px; box-shadow: 0 18px 60px color-mix(in srgb, CanvasText 10%, transparent); }
    h1 { margin: 0 0 8px; font-size: 1.6rem; }
    h2 { font-size: 1.05rem; margin: 0 0 8px; }
    .muted { opacity: .7; }
    .notice { margin-top: 14px; padding: 12px 14px; border: 1px solid color-mix(in srgb, CanvasText 16%, transparent); border-radius: 12px; line-height: 1.45; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit,minmax(170px,1fr)); gap: 12px; margin: 20px 0; }
    .card { border: 1px solid color-mix(in srgb, CanvasText 14%, transparent); border-radius: 14px; padding: 14px; }
    .label { font-size: .8rem; opacity: .65; }
    .value { margin-top: 6px; font-weight: 650; overflow-wrap: anywhere; }
    .actions, .recovery-form { display: flex; gap: 10px; flex-wrap: wrap; align-items: center; }
    button, input { font: inherit; border-radius: 10px; border: 1px solid color-mix(in srgb, CanvasText 20%, transparent); padding: 10px 14px; }
    button { cursor: pointer; font-weight: 650; }
    button:disabled { cursor: wait; opacity: .5; }
    input { min-width: 0; flex: 1 1 180px; background: Canvas; color: CanvasText; }
    input:focus-visible, button:focus-visible { outline: 2px solid Highlight; outline-offset: 2px; }
    section { margin-top: 24px; padding-top: 20px; border-top: 1px solid color-mix(in srgb, CanvasText 14%, transparent); }
    #message, #recovery-message { min-height: 1.5em; margin-top: 12px; }
    .count { font-size: 1.15rem; font-weight: 650; margin-top: 6px; }
    .small { font-size: .85rem; line-height: 1.5; }
    #recovery-users { overflow-wrap: anywhere; }
    @media (max-width: 480px) { main { padding: 18px; } .grid { grid-template-columns: 1fr 1fr; } .card { padding: 12px; } }
  </style>
</head>
<body>
<main>
  <h1>GitHub Re-follow</h1>
  <div class="muted">Weekly Monday 09:00 · Asia/Ho_Chi_Minh</div>
  <div class="notice"><strong>Target:</strong> this service re-follows accounts you follow in your <strong>Following</strong> list, not your Followers.</div>
  <div class="grid">
    <div class="card"><div class="label">Mode</div><div class="value" id="mode">Loading…</div></div>
    <div class="card"><div class="label">Target</div><div class="value">Following</div></div>
    <div class="card"><div class="label">Next run</div><div class="value" id="next">Loading…</div></div>
    <div class="card"><div class="label">Last run</div><div class="value" id="last">Loading…</div></div>
  </div>
  <div class="actions">
    <button id="run" type="button">Run now</button>
    <button id="refresh" type="button">Refresh</button>
  </div>
  <div id="message" class="muted" role="status" aria-live="polite"></div>
  <section aria-labelledby="recovery-title">
    <h2 id="recovery-title">Recover missing user</h2>
    <p class="muted small">Add an account that was lost after an interrupted re-follow. This only queues recovery; it does not change GitHub until a live run. Accounts are not permanently pinned.</p>
    <div class="label">Authenticated account</div>
    <div id="authenticated" class="value">Loading…</div>
    <div id="targetCount" class="count" aria-live="polite">Loading target count…</div>
    <div id="recovery-users" class="muted small"></div>
    <form id="recovery-form" class="recovery-form" style="margin-top:14px">
      <input id="recovery-login" name="login" aria-label="GitHub username" placeholder="GitHub username, e.g. rua-den" maxlength="39" autocomplete="off" spellcheck="false" required>
      <button id="recover" type="submit">Add to recovery</button>
    </form>
    <div id="recovery-message" class="muted small" role="status" aria-live="polite"></div>
  </section>
</main>
<script>
const mode = document.querySelector('#mode');
const next = document.querySelector('#next');
const last = document.querySelector('#last');
const message = document.querySelector('#message');
const recoveryMessage = document.querySelector('#recovery-message');
const recoveryLogin = document.querySelector('#recovery-login');
const recoverButton = document.querySelector('#recover');
const runButton = document.querySelector('#run');
const refreshButton = document.querySelector('#refresh');

async function readResponse(response) {
  if (!response.ok) {
    let error = 'Request failed (' + response.status + ').';
    try {
      const body = await response.json();
      error = body.error || body.title || error;
    } catch { }
    throw new Error(error);
  }
  return response.json();
}

async function refreshRecovery() {
  const status = await readResponse(await fetch('/api/recovery'));
  document.querySelector('#authenticated').textContent = status.authenticatedLogin;
  document.querySelector('#targetCount').textContent =
    `${status.followingCount} Following + ${status.recoveryCount} Recovery = ${status.targetCount} targets`;
  document.querySelector('#recovery-users').textContent = status.recoveryUsers.length
    ? `Missing recovery: ${status.recoveryUsers.join(', ')}` : 'No missing accounts queued.';
}

async function refresh() {
  const status = await readResponse(await fetch('/api/status'));
  mode.textContent = status.dryRun ? `Dry run · ${status.delaySeconds}s delay` : `Live · ${status.delaySeconds}s delay`;
  next.textContent = new Date(status.nextRun).toLocaleString();
  if (!status.lastRun) {
    last.textContent = 'Never';
  } else {
    const outcome = status.lastRun.succeeded ? 'Success' : 'Failed';
    const count = status.lastRun.followingCount ?? '—';
    last.textContent = `${outcome} · ${count} users · ${new Date(status.lastRun.completedAt).toLocaleString()}`;
  }
  await refreshRecovery();
}

async function refreshSafely() {
  try { await refresh(); }
  catch (error) { message.textContent = error.message; }
}
refreshButton.addEventListener('click', refreshSafely);
runButton.addEventListener('click', async () => {
  runButton.disabled = true;
  recoverButton.disabled = true;
  message.textContent = 'Running…';
  try {
    const result = await readResponse(await fetch('/api/run', { method: 'POST' }));
    message.textContent = `Completed: ${result.followingCount} users${result.dryRun ? ' (dry run — no GitHub changes)' : ' (live)'}.`;
    await refresh();
  } catch (error) { message.textContent = error.message; }
  finally { runButton.disabled = false; recoverButton.disabled = false; }
});

document.querySelector('#recovery-form').addEventListener('submit', async event => {
  event.preventDefault();
  recoverButton.disabled = true;
  recoveryMessage.textContent = 'Checking GitHub account…';
  try {
    const status = await readResponse(await fetch('/api/recovery', {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ login: recoveryLogin.value.trim() })
    }));
    recoveryMessage.textContent = `Recovery list saved. ${status.targetCount} targets. No GitHub changes made.`;
    recoveryLogin.value = '';
    await refresh();
  } catch (error) { recoveryMessage.textContent = error.message; }
  finally { recoverButton.disabled = false; }
});
refreshSafely();
</script>
</body>
</html>
""";

var builder = WebApplication.CreateBuilder(args);

OverrideOption(builder.Configuration, "GITHUB_REFOLLOW_TOKEN", nameof(RefollowOptions.Token));
OverrideOption(builder.Configuration, "GITHUB_REFOLLOW_DRY_RUN", nameof(RefollowOptions.DryRun));
OverrideOption(builder.Configuration, "GITHUB_REFOLLOW_DELAY_SECONDS", nameof(RefollowOptions.DelaySeconds));
OverrideOption(builder.Configuration, "TZ", nameof(RefollowOptions.TimeZoneId));

builder.Services.Configure<RefollowOptions>(
    builder.Configuration.GetSection(RefollowOptions.SectionName));
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

builder.Services.AddHttpClient("github", client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddSingleton<IGitHubFollowingClient>(services =>
    new GitHubFollowingClient(
        services.GetRequiredService<IHttpClientFactory>().CreateClient("github"),
        services.GetRequiredService<IOptions<RefollowOptions>>()));
builder.Services.AddSingleton<IRefollowSnapshotStore, JsonRefollowSnapshotStore>();
builder.Services.AddSingleton<IRefollowRunStateStore, JsonRefollowRunStateStore>();
builder.Services.AddSingleton<IRefollowRunner, RefollowService>();
builder.Services.AddSingleton<IRefollowRecoveryQueue, RefollowRecoveryQueue>();
builder.Services.AddSingleton<IRefollowCoordinator, RefollowCoordinator>();
builder.Services.AddSingleton<IScheduleCalculator>(services =>
{
    var options = services.GetRequiredService<IOptions<RefollowOptions>>().Value;
    return new ScheduleCalculator(TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId));
});
builder.Services.AddSingleton<IScheduleDelay, SystemScheduleDelay>();
builder.Services.AddHostedService<ScheduledRefollowWorker>();

var app = builder.Build();

app.MapGet("/", () => Results.Content(DashboardHtml, "text/html; charset=utf-8"));

app.MapGet(
    "/api/status",
    async (
        IRefollowRunStateStore stateStore,
        IScheduleCalculator scheduleCalculator,
        TimeProvider timeProvider,
        IOptions<RefollowOptions> options,
        CancellationToken cancellationToken) =>
    {
        var lastRun = await stateStore.LoadAsync(cancellationToken);
        var nextRun = scheduleCalculator.GetNextOccurrence(timeProvider.GetUtcNow());
        return Results.Ok(new
        {
            dryRun = options.Value.DryRun,
            delaySeconds = options.Value.DelaySeconds,
            nextRun,
            lastRun
        });
    });

app.MapRecoveryEndpoints();

app.MapPost(
    "/api/run",
    async (
        IRefollowCoordinator coordinator,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var result = await coordinator.RunAsync(cancellationToken);
            return Results.Ok(result);
        }
        catch (RefollowAlreadyRunningException)
        {
            return Results.Conflict(new { error = "A re-follow run is already in progress." });
        }
        catch (GitHubApiException error)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "GitHub API request failed.",
                detail: error.Kind.ToString());
        }
        catch
        {
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Re-follow run failed.");
        }
    });

app.Run();

static void OverrideOption(
    ConfigurationManager configuration,
    string environmentKey,
    string optionName)
{
    var value = configuration[environmentKey];
    if (!string.IsNullOrWhiteSpace(value))
    {
        configuration[$"{RefollowOptions.SectionName}:{optionName}"] = value;
    }
}

public partial class Program;
