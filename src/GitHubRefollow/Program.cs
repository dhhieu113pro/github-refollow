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
    main { width: min(760px, calc(100% - 32px)); border: 1px solid color-mix(in srgb, CanvasText 18%, transparent); border-radius: 18px; padding: 24px; box-shadow: 0 18px 60px color-mix(in srgb, CanvasText 10%, transparent); }
    h1 { margin: 0 0 8px; font-size: 1.6rem; }
    .muted { opacity: .7; }
    .notice { margin-top: 14px; padding: 12px 14px; border: 1px solid color-mix(in srgb, CanvasText 16%, transparent); border-radius: 12px; line-height: 1.45; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit,minmax(170px,1fr)); gap: 12px; margin: 20px 0; }
    .card { border: 1px solid color-mix(in srgb, CanvasText 14%, transparent); border-radius: 14px; padding: 14px; }
    .label { font-size: .8rem; opacity: .65; }
    .value { margin-top: 6px; font-weight: 650; overflow-wrap: anywhere; }
    .actions { display: flex; gap: 10px; flex-wrap: wrap; }
    button { font: inherit; cursor: pointer; font-weight: 650; border-radius: 10px; border: 1px solid color-mix(in srgb, CanvasText 20%, transparent); padding: 10px 14px; }
    #message { min-height: 1.5em; margin-top: 14px; }
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
    <button id="run">Run now</button>
    <button id="refresh">Refresh</button>
  </div>
  <div id="message" class="muted"></div>
</main>
<script>
const mode = document.querySelector('#mode');
const next = document.querySelector('#next');
const last = document.querySelector('#last');
const message = document.querySelector('#message');

async function refresh() {
  const response = await fetch('/api/status');
  const status = await response.json();
  mode.textContent = status.dryRun ? `Dry run · ${status.delaySeconds}s delay` : `Live · ${status.delaySeconds}s delay`;
  next.textContent = new Date(status.nextRun).toLocaleString();
  if (!status.lastRun) {
    last.textContent = 'Never';
  } else {
    const outcome = status.lastRun.succeeded ? 'Success' : 'Failed';
    const count = status.lastRun.followingCount ?? '—';
    last.textContent = `${outcome} · ${count} users · ${new Date(status.lastRun.completedAt).toLocaleString()}`;
  }
}

document.querySelector('#refresh').addEventListener('click', refresh);
document.querySelector('#run').addEventListener('click', async () => {
  message.textContent = 'Running…';
  const response = await fetch('/api/run', { method: 'POST' });
  if (response.ok) {
    const result = await response.json();
    message.textContent = `Completed: ${result.followingCount} users${result.dryRun ? ' (dry run — no GitHub changes)' : ' (live)'}.`;
  } else {
    message.textContent = `Run failed (${response.status}).`;
  }
  await refresh();
});
refresh();
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
