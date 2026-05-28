using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Application.Services.Handlers;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0176a: result.md per-repo cost section appears alongside the pipeline
/// total when a repoName is passed to the init/result frontmatter writer;
/// stays absent on single-repo / legacy paths.
/// </summary>
public sealed class RunCostSectionWriterPerRepoTests
{
    [Fact]
    public void FormatInitResult_MultiRepoSummary_RendersRepoCostBlockAlongsidePipelineTotal()
    {
        var summary = BuildMultiRepoSummary();
        var rendered = RunResultFormatter.FormatInitResult(
            runId: "2026-05-28T10-00-00-aaaa",
            durationSeconds: 10,
            costSummary: summary,
            trail: null,
            decisions: null,
            dialogueTrail: null,
            perSkillBreakdown: null,
            repoName: "repo-a");

        rendered.Should().Contain("cost:");
        rendered.Should().Contain("total_usd: 15.0000"); // pipeline total
        rendered.Should().Contain("repo_cost:");
        rendered.Should().Contain("repo: repo-a");
        rendered.Should().Contain("total_usd: 5.0000"); // repo-a share
    }

    [Fact]
    public void FormatInitResult_SingleRepoSummary_OmitsRepoCostBlock()
    {
        var summary = new RunCostSummary(
            Phases: new Dictionary<string, PhaseCost>
            {
                ["Plan"] = new("gpt-4.1", 1_000_000, 0, 0, 1, 2.0m),
            },
            TotalCost: 2.0m,
            PerRepo: null);

        var rendered = RunResultFormatter.FormatInitResult(
            runId: "2026-05-28T10-00-00-aaaa",
            durationSeconds: 10,
            costSummary: summary,
            trail: null,
            decisions: null,
            dialogueTrail: null,
            perSkillBreakdown: null,
            repoName: null);

        rendered.Should().NotContain("repo_cost:");
        rendered.Should().Contain("total_usd: 2.0000");
    }

    [Fact]
    public void FormatInitResult_MultiRepoSummaryButRepoNameMissingFromSummary_OmitsRepoBlock()
    {
        var summary = BuildMultiRepoSummary();
        var rendered = RunResultFormatter.FormatInitResult(
            runId: "2026-05-28T10-00-00-aaaa",
            durationSeconds: 10,
            costSummary: summary,
            trail: null,
            decisions: null,
            dialogueTrail: null,
            perSkillBreakdown: null,
            repoName: "repo-z-unknown");

        rendered.Should().NotContain("repo_cost:");
        rendered.Should().Contain("total_usd: 15.0000");
    }

    private static RunCostSummary BuildMultiRepoSummary() =>
        new(
            Phases: new Dictionary<string, PhaseCost>
            {
                ["Bootstrap"] = new("gpt-4.1", 5_000_000, 0, 0, 3, 15.0m),
            },
            TotalCost: 15.0m,
            PerRepo: new Dictionary<string, RepoCost>
            {
                ["repo-a"] = new(
                    Phases: new Dictionary<string, PhaseCost>
                    {
                        ["Bootstrap"] = new("gpt-4.1", 2_500_000, 0, 0, 1, 5.0m),
                    },
                    TotalCost: 5.0m),
                ["repo-b"] = new(
                    Phases: new Dictionary<string, PhaseCost>
                    {
                        ["Bootstrap"] = new("gpt-4.1", 5_000_000, 0, 0, 2, 10.0m),
                    },
                    TotalCost: 10.0m),
            });
}
