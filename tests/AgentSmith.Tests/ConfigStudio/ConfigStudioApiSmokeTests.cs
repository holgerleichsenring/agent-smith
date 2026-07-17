using System.Net;
using System.Net.Http.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Server.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.ConfigStudio;

// p0345 LIVE SMOKE: self-hosts the real ConfigStudioEndpoints over the real
// FileConfigStore on a loopback port and drives them with a real HttpClient.
// This verifies the actual wire seam between the C# backend and the dashboard's
// configApi client — route paths, camelCase JSON on GET, camelCase body on POST,
// the attributed audit feed, and referential integrity surfacing as HTTP 400 —
// none of which the store-level unit tests exercise. No Redis / DB / full Program
// boot: only the config endpoints + store are mounted.
public sealed class ConfigStudioApiSmokeTests
{
    private sealed record FixedLocation(string ConfigPath) : IConfigStoreLocation;

    private const string Yaml = """
        agents:
          claude-default:
            type: claude
            model: sonnet-4
        repos:
          test-repo:
            type: github
            url: https://github.com/test/repo
            auth: token
        trackers:
          test-ado:
            type: azure_devops
            organization: testorg
            project: TestProject
            auth: token
        projects:
          testproject:
            agent: claude-default
            tracker: test-ado
            repos: [test-repo]
            pipeline: fix-bug
        secrets:
          github_token: ${AGENTSMITH_TEST_GH_TOKEN}
        """;

    // p0345b: the operator's REAL config shape — discovery connections +
    // connection-scoped project repos, NO legacy repos block (p0281a).
    private const string OperatorShapedYaml = """
        agents:
          claude-default:
            type: claude
            model: sonnet-4
        connections:
          sample-cloud:
            type: azure_devops
            organization: sample-org
            project: SampleProject
            auth: ado_token
            default_branch: develop
        trackers:
          sample-ado:
            type: azure_devops
            organization: sample-org
            project: SampleProject
            auth: ado_token
        projects:
          sample:
            agent: claude-default
            tracker: sample-ado
            repos: [sample-cloud/Sample.Api.Server]
            pipeline: fix-bug
        secrets:
          ado_token: ${AGENTSMITH_TEST_ADO_TOKEN}
        """;

    [Fact]
    public async Task ConfigStudioApi_LiveHttpRoundTrip_CrudAuditAndIntegrity()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-smoke-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, Yaml);
        try
        {
            await using var app = await StartAppAsync(path);
            using var http = NewClient(app);

            // p0343b: export serves the canonical catalog as YAML the real loader accepts.
            var exportRes = await http.GetAsync("/api/config/export.yml");
            exportRes.StatusCode.Should().Be(HttpStatusCode.OK);
            exportRes.Content.Headers.ContentType!.MediaType.Should().Be("text/yaml");
            (await exportRes.Content.ReadAsStringAsync()).Should().Contain("claude-default");

            // GET agents — the exact camelCase wire shape the TS configApi client reads.
            var agentsJson = await http.GetStringAsync("/api/config/agents");
            agentsJson.Should().Contain("claude-default")
                .And.Contain("\"provider\"")
                .And.Contain("\"models\"")
                .And.NotContain("\"Provider\""); // never PascalCase on the wire

            // POST a repo (camelCase body via the same serializer the dashboard uses), read it back.
            var post = await http.PostAsJsonAsync("/api/config/repos",
                new RepoEntity("smoke-repo", "https://github.com/x/y", "main"));
            post.StatusCode.Should().Be(HttpStatusCode.OK);
            (await http.GetStringAsync("/api/config/repos")).Should().Contain("smoke-repo");

            // The mutation is on the attributed change feed.
            (await http.GetStringAsync("/api/config/changes")).Should().Contain("smoke-repo");

            // Referential integrity surfaces as HTTP 400, not a 500 — a project with an unknown agent ref.
            var bad = await http.PostAsJsonAsync("/api/config/projects",
                new ProjectEntity("broken", "no-such-agent", "test-ado", ["test-repo"], "fix-bug", ["fix-bug"]));
            bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            await app.StopAsync();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // p0345b LIVE SMOKE: connections CRUD over the wire + connection-scoped
    // project refs accepted / unknown-connection rejected as 400 — against a
    // config shaped exactly like the operator's (connections + conn-scoped
    // project repos, NO legacy repos block).
    [Fact]
    public async Task ConfigStudioApi_Connections_CrudAndConnectionScopedRefs()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-smoke-conn-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, OperatorShapedYaml);
        try
        {
            await using var app = await StartAppAsync(path);
            using var http = NewClient(app);

            // GET connections — camelCase wire shape, the operator's connection listed.
            var connectionsJson = await http.GetStringAsync("/api/config/connections");
            connectionsJson.Should().Contain("sample-cloud")
                .And.Contain("\"organization\":\"sample-org\"")
                .And.Contain("\"type\":\"azure_devops\"")
                .And.Contain("\"defaultBranch\":\"develop\"")
                .And.NotContain("\"Organization\""); // never PascalCase on the wire

            // POST a new connection, read it back.
            var post = await http.PostAsJsonAsync("/api/config/connections",
                new ConnectionEntity("gh-org", "github", "acme", null, "gh_token", "main"));
            post.StatusCode.Should().Be(HttpStatusCode.OK);
            (await http.GetStringAsync("/api/config/connections")).Should().Contain("gh-org");

            // PUT updates in place (route id wins), DELETE removes.
            var put = await http.PutAsJsonAsync("/api/config/connections/gh-org",
                new ConnectionEntity("ignored", "github", "acme-2", null, "gh_token", "main"));
            put.StatusCode.Should().Be(HttpStatusCode.OK);
            (await http.GetStringAsync("/api/config/connections")).Should().Contain("acme-2");
            (await http.DeleteAsync("/api/config/connections/gh-org")).StatusCode
                .Should().Be(HttpStatusCode.NoContent);
            (await http.GetStringAsync("/api/config/connections")).Should().NotContain("gh-org");

            // The mutations are on the attributed change feed.
            (await http.GetStringAsync("/api/config/changes")).Should().Contain("gh-org");

            // A connection-scoped project repo ref is VALID when the connection exists…
            var scoped = await http.PostAsJsonAsync("/api/config/projects",
                new ProjectEntity("p2", "claude-default", "sample-ado",
                    ["sample-cloud/Sample.Worker"], "fix-bug", ["fix-bug"]));
            scoped.StatusCode.Should().Be(HttpStatusCode.OK);
            (await http.GetStringAsync("/api/config/projects")).Should().Contain("sample-cloud/Sample.Worker");

            // …and an unknown connection is a 400, not a silent pass or a 500.
            var badConn = await http.PostAsJsonAsync("/api/config/projects",
                new ProjectEntity("broken", "claude-default", "sample-ado",
                    ["ghost-conn/Sample.Api.Server"], "fix-bug", ["fix-bug"]));
            badConn.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await badConn.Content.ReadAsStringAsync()).Should().Contain("unknown connection 'ghost-conn'");

            await app.StopAsync();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static async Task<WebApplication> StartAppAsync(string configPath)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            Args = Array.Empty<string>(),
        });

        var store = new FileConfigStore(new FixedLocation(configPath), new InMemoryConfigAuditStore(),
            NullLogger<FileConfigStore>.Instance);
        store.Load();
        builder.Services.AddSingleton<IConfigStore>(store);

        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0"); // loopback, OS-assigned free port
        app.MapConfigStudioEndpoints();
        await app.StartAsync();
        return app;
    }

    private static HttpClient NewClient(WebApplication app)
    {
        var baseUrl = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return new HttpClient { BaseAddress = new Uri(baseUrl) };
    }
}
