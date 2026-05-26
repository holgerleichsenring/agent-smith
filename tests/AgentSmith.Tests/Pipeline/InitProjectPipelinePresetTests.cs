using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Pipeline;

/// <summary>
/// p0130c: InitProject preset migrates from BootstrapProjectHandler to a
/// SkillRound dispatch via the new BootstrapDispatch step. The bootstrap
/// producer skills (csharp/node/python/generic) ship in agent-smith-skills
/// 2.6.0 and activate on (pipeline_name = "init-project" AND project_language).
/// </summary>
public sealed class InitProjectPipelinePresetTests
{
    [Fact]
    public void InitProject_StepSequence_MatchesP0130cShape()
    {
        var preset = PipelinePresets.InitProject;

        preset.Should().BeEquivalentTo(new[]
        {
            CommandNames.PipelineNameInitializer,
            CommandNames.CheckoutSource,
            CommandNames.AnalyzeCode,
            CommandNames.PublishProjectLanguage,
            CommandNames.LoadSkills,
            CommandNames.BootstrapDiscover, // p0161d: read-only component discovery
            CommandNames.BootstrapDispatch,
            CommandNames.WriteRunResult,
            CommandNames.InitCommit,
            CommandNames.PrCrossLink, // p0158c
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void InitProject_BootstrapDiscoverRunsBeforeBootstrapDispatch()
    {
        // p0161d: Discover produces DiscoveredComponents that Dispatch fans out on.
        var list = PipelinePresets.InitProject.ToList();
        var discoverIdx = list.IndexOf(CommandNames.BootstrapDiscover);
        var dispatchIdx = list.IndexOf(CommandNames.BootstrapDispatch);

        discoverIdx.Should().BeGreaterThan(0);
        dispatchIdx.Should().BeGreaterThan(discoverIdx);
    }

    [Fact]
    public void InitProject_DoesNotIncludeBootstrapProject()
    {
        // p0130c gap-fix: BootstrapProjectHandler is NOT reachable via init-project
        // anymore. Other presets still use it; that retirement is p0131b.
        PipelinePresets.InitProject.Should().NotContain(CommandNames.BootstrapProject);
    }

    [Fact]
    public void InitProject_PublishProjectLanguageRunsBeforeLoadSkillsAndDispatch()
    {
        var list = PipelinePresets.InitProject.ToList();
        var publishIdx = list.IndexOf(CommandNames.PublishProjectLanguage);
        var loadIdx = list.IndexOf(CommandNames.LoadSkills);
        var dispatchIdx = list.IndexOf(CommandNames.BootstrapDispatch);

        publishIdx.Should().BeGreaterThan(0);
        loadIdx.Should().BeGreaterThan(publishIdx);
        dispatchIdx.Should().BeGreaterThan(loadIdx,
            "BootstrapDispatch needs both project_language (from PublishProjectLanguage) " +
            "and AvailableRoles (from LoadSkills) before it can route");
    }
}
