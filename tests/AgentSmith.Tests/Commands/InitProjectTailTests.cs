using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// p0490: the init pipeline's tail — where completion sits in the preset, and what the
/// completion step is built from.
/// </summary>
public sealed class InitProjectTailTests
{
    [Fact]
    public void InitComplete_RunsAfterTheCrossLinkPass()
    {
        var preset = PipelinePresets.Effective("init-project").ToList();

        var crossLink = preset.IndexOf(CommandNames.PrCrossLink);
        var complete = preset.IndexOf(CommandNames.InitComplete);

        complete.Should().BeGreaterThan(crossLink,
            "PrCrossLink PATCHes each sibling's body in a second pass — completing first "
            + "would either lose the cross-link or fail against a closed pull request");
        complete.Should().Be(preset.Count - 1, "completion is the last thing init does");
        preset.IndexOf(CommandNames.InitCommit).Should().BeLessThan(crossLink);
    }

    [Fact]
    public void InitComplete_TheBuilder_CarriesTheWorkBranchAndRepos_AndNoConsentFlag()
    {
        // 2026-11-17-713ea: every init completes its pull requests — the launch carries no
        // auto-accept, so the builder reads none; a stored request that still names the old
        // key changes nothing.
        var request = new PipelineRequest(
            "sample", PipelinePresets.InitProjectName, Headless: true, RunId: "run-1",
            Context: new Dictionary<string, object> { ["AutoCompletePullRequests"] = false });

        var context = BuildFrom(Requeue(request));

        context.SourceBranch.Value.Should().Be("agentsmith/init");
        context.Configs.Should().ContainSingle().Which.Name.Should().Be("a");
    }

    private static PipelineRequest Requeue(PipelineRequest request) =>
        JsonSerializer.Deserialize<PipelineRequest>(JsonSerializer.Serialize(request))!;

    private static InitCompleteContext BuildFrom(PipelineRequest request)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName("agentsmith/init"), "https://x/a.git"));
        pipeline.Set<IReadOnlyList<RepoConnection>>(
            ContextKeys.Repos, [new RepoConnection { Name = "a" }]);
        foreach (var (key, value) in request.Context ?? []) pipeline.Set(key, value);

        return (InitCompleteContext)new InitCompleteContextBuilder().Build(
            PipelineCommand.Simple(CommandNames.InitComplete), new ResolvedProject(), pipeline);
    }
}
