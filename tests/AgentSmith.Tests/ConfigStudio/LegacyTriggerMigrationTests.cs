using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Server.Services.Startup;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-10-29-6bd3a: the stored per-project trigger blocks are written onto their projects and
/// trackers once, so the studio shows the routing that runs. What is NOT written matters as much:
/// a save bumps a version, and a project's block must outlive a tracker save that did not land.
/// </summary>
public sealed class LegacyTriggerMigrationTests
{
    [Fact]
    public void Migration_StoredBlock_IsFoldedAndResavedOnce()
    {
        using var harness = new DbConfigTestHarness();
        harness.Import(Yaml);

        Migration(harness.DocStore, harness).Run().Should().Be(1);

        var tracker = Json(harness, ConfigDocTypes.Tracker, "board");
        tracker.GetProperty("CommentKeyword").GetString().Should().Be("@agent-smith");
        tracker.GetProperty("AssigneeName").GetString().Should().Be("Bot");
        var project = Json(harness, ConfigDocTypes.Project, "alpha");
        project.GetProperty("JiraTrigger").ValueKind.Should().Be(JsonValueKind.Null);
        project.GetProperty("Resolution").GetProperty("tag").GetString().Should().Be("alpha");

        Migration(harness.DocStore, harness).Run().Should().Be(0, "nothing carries a block any more");
        Version(harness, ConfigDocTypes.Project, "alpha").Should().Be(2);
        Version(harness, ConfigDocTypes.Tracker, "board").Should().Be(2);
    }

    [Fact]
    public void Migration_NoBlock_SavesNothing()
    {
        using var harness = new DbConfigTestHarness();
        harness.Import(Yaml.Replace("""
                jira_trigger:
                  project_resolution: { strategy: tag, value: alpha }
                  comment_keyword: "@agent-smith"
                  assignee_name: Bot
            """, "    resolution: { tag: alpha }\n"));

        Migration(harness.DocStore, harness).Run().Should().Be(0);

        Version(harness, ConfigDocTypes.Tracker, "board").Should().Be(1,
            "a tracker the fold did not touch differs from its stored document only in fields added since");
        Version(harness, ConfigDocTypes.Project, "alpha").Should().Be(1);
    }

    [Fact]
    public void Migration_StaleTracker_LeavesItsProjectsBlocks()
    {
        using var harness = new DbConfigTestHarness();
        harness.Import(Yaml);
        var store = new Mock<IConfigDocumentStore>();
        store.Setup(s => s.LoadAll()).Returns(() => harness.DocStore.LoadAll());
        var saved = new List<string>();
        store.Setup(s => s.Save(It.IsAny<ConfigDocWrite>())).Callback<ConfigDocWrite>(write =>
        {
            if (write.Type == ConfigDocTypes.Tracker) throw new StaleConfigVersionException(ConfigDocTypes.Tracker, "board", 1, 2);
            saved.Add(write.Id);
        });

        Migration(store.Object, harness).Run().Should().Be(0);

        saved.Should().BeEmpty("clearing the block without the tracker holding its values would lose them");
    }

    private static LegacyTriggerMigration Migration(IConfigDocumentStore store, DbConfigTestHarness harness) =>
        new(store, harness.Assembler, new StartupFindings(), NullLogger<LegacyTriggerMigration>.Instance);

    private static JsonElement Json(DbConfigTestHarness harness, string type, string id) =>
        JsonDocument.Parse(harness.DocStore.LoadAll().Single(r => r.Type == type && r.Id == id).Doc).RootElement.Clone();

    private static int Version(DbConfigTestHarness harness, string type, string id) =>
        harness.DocStore.LoadAll().Single(r => r.Type == type && r.Id == id).Version;

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
          board:
            type: jira
            url: https://example.atlassian.net
            project: ALP
            auth: token
            open_states: [To Do]
        projects:
          alpha:
            agent: claude-default
            tracker: board
            repos: [test-repo]
            jira_trigger:
              project_resolution: { strategy: tag, value: alpha }
              comment_keyword: "@agent-smith"
              assignee_name: Bot
        """;
}
