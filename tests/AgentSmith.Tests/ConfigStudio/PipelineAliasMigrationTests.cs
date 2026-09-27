using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Server.Services.Startup;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-09-25-e5b1: an installation's live configuration names its pipelines, and three document
/// types carry such a name. With the alias map deleted, one left behind is not a cosmetic leftover
/// — <c>pipeline_triggers</c> and a project's <c>pipelines</c> list reject an unknown name as
/// BLOCKING, which takes the installation off the air. So every field moves, and it moves before
/// the first request is answered.
/// <para>
/// The other half is what is NOT written: a save bumps the document's version and drops the
/// standing unmoved-ticket facts recorded against it, so a document naming nothing retired must
/// come out of this untouched.
/// </para>
/// </summary>
public sealed class PipelineAliasMigrationTests
{
    [Fact]
    public void Migration_EachConfigDocumentType_IsRewrittenOnce()
    {
        using var harness = new DbConfigTestHarness();
        harness.Import(LegacyYaml);

        Migration(harness).Run().Should().Be(3, "the global trigger, the tracker and the project");

        Doc(harness, ConfigDocTypes.PipelineTrigger, "bug").Should().Be("\"code\"");
        var tracker = Json(harness, ConfigDocTypes.Tracker, "legacy-tracker");
        tracker.GetProperty("PipelineFromLabel").GetProperty("bug").GetString().Should().Be("code");
        tracker.GetProperty("DefaultPipeline").GetString().Should().Be("code");

        var project = Json(harness, ConfigDocTypes.Project, "legacy");
        project.GetProperty("Pipeline").GetString().Should().Be("code");
        project.GetProperty("DefaultPipeline").GetString().Should().Be("code");
        project.GetProperty("Pipelines")[0].GetProperty("Name").GetString().Should().Be("code",
            "a default that is not in the project's own pipelines list disables the project, so "
            + "the list and the default move together or not at all");
        var trigger = project.GetProperty("GithubTrigger");
        trigger.GetProperty("PipelineFromLabel").GetProperty("phase").GetString().Should().Be("code");
        trigger.GetProperty("DefaultPipeline").GetString().Should().Be("code");
    }

    [Fact]
    public void Migration_ADocumentNamingNoAlias_IsNotReSaved()
    {
        using var harness = new DbConfigTestHarness();
        harness.Import(LegacyYaml);

        Migration(harness).Run();

        Version(harness, ConfigDocTypes.Agent, "claude-default").Should().Be(1,
            "an agent names no pipeline, and re-saving it would drop that installation's "
            + "standing unmoved-ticket facts for nothing");
        Version(harness, ConfigDocTypes.Repo, "test-repo").Should().Be(1);
        Version(harness, ConfigDocTypes.Project, "legacy").Should().Be(2, "this one did carry them");
    }

    [Fact]
    public void Migration_RunTwice_WritesNothingTheSecondTime()
    {
        using var harness = new DbConfigTestHarness();
        harness.Import(LegacyYaml);

        Migration(harness).Run();

        Migration(harness).Run().Should().Be(0,
            "a restart re-runs this, and a migration that rewrote on every boot would bump every "
            + "version on every boot");
        Version(harness, ConfigDocTypes.Project, "legacy").Should().Be(2);
    }

    [Fact]
    public void Migration_StoreUnreachable_RecordsAFindingAndNamesTheReplacement()
    {
        var findings = new StartupFindings();

        new PipelineAliasMigration(
            new UnreachableDocuments(), new ConfigDocumentAssembler(), findings,
            NullLogger<PipelineAliasMigration>.Instance).Run().Should().Be(0);

        findings.All.Should().ContainSingle()
            .Which.Reason.Should().Contain("fix-bug").And.Contain("does not exist");
    }

    private static PipelineAliasMigration Migration(DbConfigTestHarness harness) =>
        new(harness.DocStore, harness.Assembler, new StartupFindings(),
            NullLogger<PipelineAliasMigration>.Instance);

    private static string Doc(DbConfigTestHarness harness, string type, string id) =>
        harness.DocStore.LoadAll().Single(r => r.Type == type && r.Id == id).Doc;

    private static JsonElement Json(DbConfigTestHarness harness, string type, string id) =>
        JsonDocument.Parse(Doc(harness, type, id)).RootElement.Clone();

    private static int Version(DbConfigTestHarness harness, string type, string id) =>
        harness.DocStore.LoadAll().Single(r => r.Type == type && r.Id == id).Version;

    private const string LegacyYaml = """
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
          legacy-tracker:
            type: github
            auth: token
            pipeline_from_label:
              bug: fix-bug
            default_pipeline: fix-no-test
        pipeline_triggers:
          bug: add-feature
        projects:
          legacy:
            agent: claude-default
            tracker: legacy-tracker
            repos: [test-repo]
            pipeline: fix-bug
            pipelines:
              - name: fix-bug
            default_pipeline: fix-bug
            github_trigger:
              pipeline_from_label:
                phase: phase-execution
              default_pipeline: add-feature
        """;
}
