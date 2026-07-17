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

    [Fact]
    public async Task ConfigStudioApi_LiveHttpRoundTrip_CrudAuditAndIntegrity()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-smoke-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, Yaml);
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = AppContext.BaseDirectory,
                Args = Array.Empty<string>(),
            });

            var store = new FileConfigStore(new FixedLocation(path), new InMemoryConfigAuditStore(),
                NullLogger<FileConfigStore>.Instance);
            store.Load();
            builder.Services.AddSingleton<IConfigStore>(store);

            await using var app = builder.Build();
            app.Urls.Add("http://127.0.0.1:0"); // loopback, OS-assigned free port
            app.MapConfigStudioEndpoints();
            await app.StartAsync();

            var baseUrl = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.First();
            using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };

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
}
