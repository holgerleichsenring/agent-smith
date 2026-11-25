using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Migration.Services.ConfigDocuments;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>2026-11-25-9992d: a store holding the pre-9992d registries singleton beside two projects, one of which
/// has feeds of its own.</summary>
internal static class ProjectRegistriesMigrationFixture
{
    public const string Feeds =
        """[{"Host":"pkgs.example.com","Username":"any","Token":"${feed_token}"},{"Host":"npm.example.com","Username":"ci","Token":"${npm_token}"}]""";

    public static DbConfigTestHarness Seeded(bool withProjects = true)
    {
        var harness = new DbConfigTestHarness();
        if (withProjects) harness.Import(Yaml);
        harness.DocStore.Save(new ConfigDocWrite(
            ConfigDocTypes.Registries, ConfigDocTypes.SingletonId, Feeds, null, [], "test", "the pre-9992d singleton"));
        return harness;
    }

    public static ProjectRegistriesMigration Mover(DbConfigTestHarness harness) =>
        new(harness.Assembler, NullLogger<ProjectRegistriesMigration>.Instance);

    public static JsonElement ProjectJson(DbConfigTestHarness harness, string id) =>
        JsonDocument.Parse(Row(harness, id).Doc).RootElement.Clone();

    public static int Version(DbConfigTestHarness harness, string id) => Row(harness, id).Version;

    private static ConfigDocRow Row(DbConfigTestHarness harness, string id) =>
        harness.DocStore.LoadAll().Single(r => r.Type == ConfigDocTypes.Project && r.Id == id);

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
          main:
            type: github
            auth: token
        projects:
          bare:
            agent: claude-default
            tracker: main
            repos: [test-repo]
          own:
            agent: claude-default
            tracker: main
            repos: [test-repo]
            registries:
              - host: own.example.com
                username: any
                token: ${own_token}
        """;
}
