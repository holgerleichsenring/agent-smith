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
/// p0490: the init pipeline's tail — where completion sits in the preset, and how the
/// operator's auto-accept gets from the launch request to the step that reads it.
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
    public void InitComplete_TheLaunchFlag_SurvivesTheJobQueue_AndReachesTheStep()
    {
        // The launch rides the Redis job queue as JSON, and a Dictionary<string, object>
        // comes back out of System.Text.Json as JsonElement — the round-trip p0327's
        // resume payload works around by riding as a string. Seed the context the way
        // ExecutePipelineUseCase does (one Set per request-context entry) and let the
        // REAL builder read it, because that is where a silently-false flag would hide.
        var request = new PipelineRequest(
            "sample", "init-project", IsInit: true, Headless: true, RunId: "run-1",
            Context: new Dictionary<string, object>
            {
                [ContextKeys.AutoCompletePullRequests] = true,
            });

        var context = BuildFrom(Requeue(request));

        context.AutoComplete.Should().BeTrue();
        context.SourceBranch.Value.Should().Be("agentsmith/init");
    }

    [Fact]
    public void InitComplete_ALaunchThatSaidNothing_DoesNotAutoComplete()
    {
        var request = new PipelineRequest("sample", "init-project", IsInit: true, RunId: "run-1");

        var context = BuildFrom(Requeue(request));

        context.AutoComplete.Should().BeFalse("consent is given, never assumed");
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
