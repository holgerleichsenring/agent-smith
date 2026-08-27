using System.Net;
using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Models;
using AgentSmith.Tests.Server.Auth;
using FluentAssertions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-08-27-729e: the read-out over HTTP, on the REAL composition. It is anonymous
/// because an operator asking "which build am I on" is usually asking BECAUSE something
/// is not answering — so the two cases that must hold are the two absences: no authority
/// configured, and a build that was never stamped with a release. Both are also what
/// EnforceSwitchOffTests and TokenAuthorityEnforcementTests boot into when they walk the
/// anonymous routes.
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class InstallationIdentityServedTests : IDisposable
{
    private const string Route = "/api/config/installation";

    private readonly List<string> _tempFiles = [];
    private readonly (string Name, string? Value)[] _restore;

    public InstallationIdentityServedTests()
    {
        _restore =
        [
            (BuildIdentity.RevisionVariable,
                Environment.GetEnvironmentVariable(BuildIdentity.RevisionVariable)),
            (BuildIdentity.VersionVariable,
                Environment.GetEnvironmentVariable(BuildIdentity.VersionVariable)),
        ];
    }

    [Fact]
    public async Task Report_NoAuthorityAndAStampedBuild_Answers200WithTheRelease()
    {
        Environment.SetEnvironmentVariable(BuildIdentity.RevisionVariable, "cafebabecafebabe");
        Environment.SetEnvironmentVariable(BuildIdentity.VersionVariable, "1.2.3");
        await using var server = await BootAsync();

        var response = await server.Client.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Parse(await response.Content.ReadAsStringAsync()).ServerRelease.Should().Be("1.2.3");
    }

    [Fact]
    public async Task Report_ABuildWithNoReleaseVariable_StillAnswers200()
    {
        Environment.SetEnvironmentVariable(BuildIdentity.RevisionVariable, null);
        Environment.SetEnvironmentVariable(BuildIdentity.VersionVariable, null);
        await using var server = await BootAsync();

        var response = await server.Client.GetAsync(Route);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the agent version is underivable on such a build, and that is a LINE in the "
            + "report — never a throw the read-out dies on");
        Parse(await response.Content.ReadAsStringAsync()).ServerRelease.Should().BeNull();
    }

    private Task<BootedServer> BootAsync() => BootedServer.StartAsync(new BootPlan(WriteConfig()));

    private static InstallationIdentityResponse Parse(string raw)
    {
        var parsed = JsonSerializer.Deserialize<InstallationIdentityResponse>(
            raw, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        parsed.Should().NotBeNull($"the route must answer a document — got: {raw}");
        return parsed!;
    }

    private string WriteConfig()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"install-{Guid.NewGuid():N}.db");
        var configPath = Path.Combine(Path.GetTempPath(), $"install-{Guid.NewGuid():N}.yml");
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
}
