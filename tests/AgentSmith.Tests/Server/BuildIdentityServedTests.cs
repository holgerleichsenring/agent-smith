using System.Net;
using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Startup;
using AgentSmith.Tests.Server.Auth;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-08-25-8c97: the build difference over HTTP, on the REAL composition. It rides the
/// findings request because that is the channel an operator already watches — and because a
/// per-request header would have meant editing every call site and could not have reached
/// the hub at all, whose browser websocket cannot set one.
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class BuildIdentityServedTests : IDisposable
{
    private const string ServerRevision = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string CallerRevision = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private readonly MutableTimeProvider _clock = new() { Now = DateTimeOffset.UnixEpoch };
    private readonly List<string> _tempFiles = [];
    private readonly (string Name, string? Value)[] _restore;

    public BuildIdentityServedTests()
    {
        _restore =
        [
            (BuildIdentity.RevisionVariable,
                Environment.GetEnvironmentVariable(BuildIdentity.RevisionVariable)),
            (BuildIdentity.VersionVariable,
                Environment.GetEnvironmentVariable(BuildIdentity.VersionVariable)),
        ];
        Environment.SetEnvironmentVariable(BuildIdentity.RevisionVariable, ServerRevision);
        Environment.SetEnvironmentVariable(BuildIdentity.VersionVariable, "0.129.0");
    }

    [Fact]
    public async Task Mismatch_TheApiKeepsAnswering()
    {
        await using var server = await BootAsync();
        PassTheRolloutWindow(server);

        var response = await server.Client.GetAsync($"/api/config/findings?build={CallerRevision}");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "a difference in builds is advisory — nothing refuses to serve because of it");
        var findings = Parse(await response.Content.ReadAsStringAsync());
        findings.Findings.Should().Contain(f =>
            f.Subsystem == StartupSubsystems.Build && f.Severity == "advisory",
            "advisory, not blocking — old and new coexisting is what an upgrade looks "
            + "like, and the only action offered is a reload");
    }

    [Fact]
    public async Task Match_ProducesNoFinding()
    {
        await using var server = await BootAsync();
        PassTheRolloutWindow(server);

        var findings = Parse(
            await server.Client.GetStringAsync($"/api/config/findings?build={ServerRevision}"));

        findings.Findings.Should().NotContain(f => f.Subsystem == StartupSubsystems.Build);
    }

    [Fact]
    public async Task MissingIdentity_IsNotReportedAsAMismatch()
    {
        await using var server = await BootAsync();
        PassTheRolloutWindow(server);

        var findings = Parse(await server.Client.GetStringAsync("/api/config/findings"));

        findings.Findings.Should().NotContain(f => f.Subsystem == StartupSubsystems.Build,
            "a caller that names no build — every client written before this phase — is "
            + "told nothing about one");
    }

    // The detector reads its start instant when the container first builds it, so resolving
    // it before the clock moves is what makes "how long since this process started" mean
    // what the assertion says it means.
    private void PassTheRolloutWindow(BootedServer server)
    {
        server.Services.GetRequiredService<IBuildMismatchDetector>();
        _clock.Now += BuildMismatchDetector.RolloutWindow * 2;
    }

    private Task<BootedServer> BootAsync() =>
        BootedServer.StartAsync(new BootPlan(WriteConfig()) { Clock = _clock });

    private static StartupFindingsResponse Parse(string raw)
    {
        var parsed = JsonSerializer.Deserialize<StartupFindingsResponse>(
            raw, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        parsed.Should().NotBeNull($"the findings endpoint must answer a document — got: {raw}");
        return parsed!;
    }

    private string WriteConfig()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"buildid-{Guid.NewGuid():N}.db");
        var configPath = Path.Combine(Path.GetTempPath(), $"buildid-{Guid.NewGuid():N}.yml");
        File.WriteAllText(configPath, $"""
            persistence:
              provider: sqlite
              connection_string: Data Source={dbPath}

            """);
        _tempFiles.Add(configPath);
        _tempFiles.Add(dbPath);
        return configPath;
    }

    public void Dispose()
    {
        foreach (var (name, value) in _restore) Environment.SetEnvironmentVariable(name, value);
        foreach (var path in _tempFiles.Where(File.Exists)) File.Delete(path);
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
