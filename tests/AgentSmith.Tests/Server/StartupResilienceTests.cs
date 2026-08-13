using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Startup;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0391a — THE TEETH. Prose did not stop p0391: the spec sentence that produced a
/// boot-time throw was written by the same author who then reviewed it. So the rule is a
/// test. Each case boots the REAL composition (<see cref="ServerHostFactory"/>, the same
/// call Program.cs makes) with a dependency broken, and asserts the server answers and
/// names the cause. A throw reintroduced into a startup path fails these.
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class StartupResilienceTests : IDisposable
{
    private const string UnreachableEndpoint = "127.0.0.1:1";
    private const string HealthyRedisPlaceholder = "127.0.0.1:1";

    private readonly List<string> _tempFiles = [];
    private readonly string? _configPathBefore = Environment.GetEnvironmentVariable("CONFIG_PATH");
    private readonly string? _redisUrlBefore = Environment.GetEnvironmentVariable("REDIS_URL");

    [Fact]
    public async Task Startup_DatabaseUnreachable_ServerStartsAndReportsIt()
    {
        var configPath = WriteConfig(Bootstrap(
            "postgresql", $"Host=127.0.0.1;Port=1;Database=agentsmith;Username=x;Password=y"));

        var findings = await BootAndReadFindingsAsync(configPath);

        findings.Degraded.Should().BeTrue();
        findings.Findings.Should().Contain(f =>
            f.Severity == "blocking" && f.Subsystem == "database");
    }

    [Fact]
    public async Task Startup_RedisUnreachable_ServerStartsAndReportsIt()
    {
        var configPath = WriteConfig(Bootstrap("sqlite", $"Data Source={NewDbPath()}"));

        var findings = await BootAndReadFindingsAsync(configPath);

        findings.Findings.Should().Contain(f =>
            f.Severity == "blocking" && f.Subsystem == "redis" && f.Reason.Contains("queue"));
    }

    [Fact]
    public async Task Startup_ConfigCannotBeLoaded_ServerStartsAndReportsIt()
    {
        // A migrated store whose stored configuration does not materialize: the trigger's
        // resolution shorthand names a strategy that does not exist, which the raw-to-typed
        // pipeline rejects outright. The whole config is unusable, and the server says so.
        var dbPath = NewDbPath();
        var configPath = WriteConfig(
            Bootstrap("sqlite", $"Data Source={dbPath}") + UnresolvableProjectYaml);
        Seed(dbPath, configPath);

        var findings = await BootAndReadFindingsAsync(configPath);

        findings.Degraded.Should().BeTrue();
        findings.Findings.Should().Contain(f =>
            f.Severity == "blocking" && f.Subsystem == "configuration");
    }

    [Fact]
    public async Task Startup_ParkingPresetWithoutStatus_ServerStartsAndDisablesOnlyThatTrigger()
    {
        // The 2026-07-31 configuration: ONE trigger of ONE project without a park status.
        // It used to refuse the whole process; it now refuses itself.
        var dbPath = NewDbPath();
        var configPath = WriteConfig(Bootstrap("sqlite", $"Data Source={dbPath}") + TwoProjectsYaml);
        Seed(dbPath, configPath);

        var findings = await BootAndReadFindingsAsync(configPath);

        var blocking = findings.Findings.Where(f => f.Severity == "blocking").ToList();
        blocking.Should().ContainSingle(f =>
            f.Project == "alpha"
            && f.Trigger == "github_trigger"
            && f.Field == "needs_clarification_status");
    }

    [Fact]
    public async Task Startup_OtherProjects_KeepRunningWhenOneTriggerIsBlocked()
    {
        var dbPath = NewDbPath();
        var configPath = WriteConfig(Bootstrap("sqlite", $"Data Source={dbPath}") + TwoProjectsYaml);
        Seed(dbPath, configPath);

        var findings = await BootAndReadFindingsAsync(configPath);

        findings.Findings.Should().NotContain(f => f.Project == "beta");
    }

    [Fact]
    public async Task FindingsEndpoint_ReturnsUnitFieldSeverityAndReason()
    {
        var dbPath = NewDbPath();
        var configPath = WriteConfig(Bootstrap("sqlite", $"Data Source={dbPath}") + TwoProjectsYaml);
        Seed(dbPath, configPath);

        var findings = await BootAndReadFindingsAsync(configPath);

        var finding = findings.Findings.First(f => f.Project == "alpha");
        finding.Subsystem.Should().Be("configuration");
        finding.Trigger.Should().Be("github_trigger");
        finding.Field.Should().Be("needs_clarification_status");
        finding.Severity.Should().Be("blocking");
        finding.Reason.Should().Contain("needs_clarification_status");
    }

    // p0391b — the converted paths. Each of these used to be a throw that escaped startup:
    // the server died on a typo and the operator got a container that never answered.

    [Fact]
    public async Task Startup_RedisUrlUnusable_ServerStartsAndReportsIt()
    {
        // REDIS_URL="" parses to no endpoint, so Connect() threw ArgumentException out of a
        // lazy factory that three unguarded startup paths resolve. AbortOnConnectFail=false
        // never covered this: the endpoint was not down, it was absent.
        var configPath = WriteConfig(Bootstrap("sqlite", $"Data Source={NewDbPath()}"));

        var findings = await BootAndReadFindingsAsync(configPath, redisUrl: "");

        findings.Findings.Should().Contain(f =>
            f.Severity == "blocking" && f.Subsystem == "redis" && f.Field == "REDIS_URL");
    }

    [Fact]
    public async Task Startup_BootstrapConfigUnreadable_ServerStartsAndReportsIt()
    {
        // The bootstrap reader caught YamlException only, so a file that exists but does not
        // deserialize into the config shape threw out of the DbContext factory — on the first
        // scope, before anything could report it.
        var configPath = WriteConfig("- this is a sequence, not a configuration\n");

        var findings = await BootAndReadFindingsAsync(configPath);

        findings.Findings.Should().Contain(f =>
            f.Severity == "blocking" && f.Subsystem == "config-file");
    }

    [Fact]
    public async Task Startup_UnmaterializableProject_DisablesOnlyThatProject()
    {
        // p0391b's headline. An unknown resolution shorthand threw out of the raw-to-typed
        // pipeline, the loader caught it as "the stored configuration is not usable", and the
        // server ran with an EMPTY config — one typo in one project silenced every other one.
        // Now the broken project is the only casualty and the finding names its field.
        var dbPath = NewDbPath();
        var configPath = WriteConfig(
            Bootstrap("sqlite", $"Data Source={dbPath}") + OneBrokenOneHealthyYaml);
        Seed(dbPath, configPath);

        var findings = await BootAndReadFindingsAsync(configPath);

        findings.Findings.Should().Contain(f =>
            f.Project == "broken" && f.Field == "resolution" && f.Severity == "blocking");
        findings.Findings.Should().NotContain(f => f.Project == "healthy");
        findings.Findings.Should().NotContain(f => f.Field == "configuration");
    }

    // Every case above boots the whole server: /health answering is the claim under test,
    // and the findings body is how the server says what it is missing.
    private async Task<StartupFindingsResponse> BootAndReadFindingsAsync(
        string configPath, string redisUrl = HealthyRedisPlaceholder)
    {
        Environment.SetEnvironmentVariable("CONFIG_PATH", configPath);
        Environment.SetEnvironmentVariable("REDIS_URL", redisUrl);

        var app = await ServerHostFactory.CreateAsync([]);
        var started = false;
        try
        {
            app.Urls.Add("http://127.0.0.1:0");
            await app.StartAsync();
            started = true;
            using var client = NewClient(app);

            var health = await client.GetAsync("/health");
            health.StatusCode.Should().Be(HttpStatusCode.OK, "a degraded server must still answer");

            // The `!` here used to assert non-null and lie: when the endpoint answered with
            // something that did not deserialize, every caller got a null Findings list and
            // blew up later as an opaque "ArgumentNullException: source" with no clue what the
            // server had actually said. This failure mode is currently CI-only (green on macOS
            // in Debug and Release, filtered and full-suite), so the next red run has to carry
            // its own diagnosis.
            var raw = await client.GetStringAsync("/api/config/findings");
            var parsed = JsonSerializer.Deserialize<StartupFindingsResponse>(
                raw, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            parsed.Should().NotBeNull($"/api/config/findings must return a findings document — got: {raw}");
            parsed!.Findings.Should().NotBeNull($"the document must carry a findings array — got: {raw}");
            return parsed;
        }
        finally
        {
            // Only stop what actually started. Host.StopAsync reverses its hosted-service
            // list, and that list is null when StartAsync never completed — so an unstarted
            // host threw "ArgumentNullException: source" out of this finally block and
            // REPLACED the real startup exception with a meaningless one. That is why these
            // tests were undiagnosable in CI: the failure we were reading was the cleanup's,
            // not the run's.
            if (started) await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    private static HttpClient NewClient(WebApplication app)
    {
        var baseUrl = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    private static string Bootstrap(string provider, string connectionString) => $"""
        persistence:
          provider: {provider}
          connection_string: {connectionString}

        """;

    private const string UnresolvableProjectYaml = """
        agents:
          claude-default:
            type: claude
            model: sonnet-4
        repos:
          shared-repo:
            type: github
            url: https://github.com/test/repo
            auth: token
        trackers:
          gh:
            type: github
            organization: testorg
            auth: token
        projects:
          orphan:
            agent: claude-default
            tracker: gh
            repos: [shared-repo]
            pipeline: fix-bug
            resolution:
              not_a_strategy: whatever
            github_trigger:
              trigger_statuses: [open]
              done_status: closed
              needs_clarification_status: question
        """;

    private const string OneBrokenOneHealthyYaml = """
        agents:
          claude-default:
            type: claude
            model: sonnet-4
        repos:
          shared-repo:
            type: github
            url: https://github.com/test/repo
            auth: token
        trackers:
          gh:
            type: github
            organization: testorg
            auth: token
        projects:
          healthy:
            agent: claude-default
            tracker: gh
            repos: [shared-repo]
            pipeline: code
            resolution:
              tag: healthy
            github_trigger:
              trigger_statuses: [open]
              done_status: closed
              needs_clarification_status: question
          broken:
            agent: claude-default
            tracker: gh
            repos: [shared-repo]
            pipeline: code
            resolution:
              not_a_strategy: whatever
            github_trigger:
              trigger_statuses: [open]
              done_status: closed
              needs_clarification_status: question
        """;

    private const string TwoProjectsYaml = """
        agents:
          claude-default:
            type: claude
            model: sonnet-4
        repos:
          shared-repo:
            type: github
            url: https://github.com/test/repo
            auth: token
        trackers:
          gh:
            type: github
            organization: testorg
            auth: token
        projects:
          alpha:
            agent: claude-default
            tracker: gh
            repos: [shared-repo]
            pipeline: fix-bug
            resolution:
              tag: alpha
            github_trigger:
              trigger_statuses: [open]
              done_status: closed
          beta:
            agent: claude-default
            tracker: gh
            repos: [shared-repo]
            pipeline: fix-bug
            resolution:
              tag: beta
            github_trigger:
              trigger_statuses: [open]
              done_status: closed
              needs_clarification_status: question
        """;

    private static void Seed(string dbPath, string configPath)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AgentSmithDbContext>(
            b => b.UseSqlite($"Data Source={dbPath}"), ServiceLifetime.Scoped);
        services.AddScoped<ConfigDocumentRepository>();
        services.AddScoped<ConfigImportRepository>();
        services.AddSingleton<ConfigDocumentAssembler>();
        services.AddSingleton<IConfigDocumentStore, EfConfigDocumentStore>();
        using var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
            scope.ServiceProvider.GetRequiredService<AgentSmithDbContext>().Database.Migrate();

        var raw = new RawConfigYaml().Deserialize(File.ReadAllText(configPath));
        var writes = provider.GetRequiredService<ConfigDocumentAssembler>().Decompose(raw)
            .Select(d => new ConfigDocWrite(d.Type, d.Id, d.Doc, null, d.Edges, "startup-resilience"))
            .ToList();
        provider.GetRequiredService<IConfigDocumentStore>().Import(writes, force: true);
    }

    private string NewDbPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-boot-{Guid.NewGuid():N}.db");
        _tempFiles.Add(path);
        _tempFiles.Add(path + "-wal");
        _tempFiles.Add(path + "-shm");
        return path;
    }

    private string WriteConfig(string yaml)
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-boot-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, yaml);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("CONFIG_PATH", _configPathBefore);
        Environment.SetEnvironmentVariable("REDIS_URL", _redisUrlBefore);
        foreach (var file in _tempFiles)
            if (File.Exists(file)) File.Delete(file);
    }
}
