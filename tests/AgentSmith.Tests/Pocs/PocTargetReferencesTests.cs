using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs;

/// <summary>
/// 2026-11-23-933da: the pocs block's references are found at the load — an unknown source_repo, project_agent or
/// project_tracker is a finding naming the key; blocking while PoCs are on, advisory while off.
/// </summary>
public sealed class PocTargetReferencesTests
{
    [Fact]
    public void Findings_EveryReferenceResolves_None()
    {
        PocTargetReferences.Findings(Config(PocsConfigTests.Sound())).Should().BeEmpty();

        var catalog = PocsConfigTests.Sound();
        catalog.Targets[0].SourceRepo = "poc-src";
        PocTargetReferences.Findings(Config(catalog)).Should().BeEmpty("a repos-catalog name resolves too");
    }

    [Fact]
    public void Findings_UnknownAgentTrackerAndRepo_EachNamesItsKey()
    {
        var pocs = PocsConfigTests.Sound();
        pocs.ProjectAgent = "ghost";
        pocs.ProjectTracker = "nowhere";
        pocs.Targets[0].SourceRepo = "gitlab/poc-src";

        var findings = PocTargetReferences.Findings(Config(pocs)).ToList();

        findings.Should().HaveCount(3).And.OnlyContain(f => f.IsBlocking && f.Field == "pocs");
        findings.Select(f => f.Reason).Should().Contain(r => r.StartsWith("pocs.project_agent names 'ghost'"))
            .And.Contain(r => r.StartsWith("pocs.project_tracker names 'nowhere'"))
            .And.Contain(r => r.Contains("pocs.targets[].source_repo of target 'main' names 'gitlab/poc-src'"));
    }

    // 2026-11-23-933de
    [Fact]
    public void PocTargetReferences_UnknownPullAuth_FindingNamingKey()
    {
        var pocs = PocsConfigTests.Sound();
        pocs.Targets[0].Registry!.PullAuth = "GHOST_TOKEN";

        PocTargetReferences.Findings(Config(pocs)).Should().ContainSingle().Which.Reason.Should()
            .StartWith("pocs.targets[].registry.pull_auth of target 'main' names 'GHOST_TOKEN'").And.Contain("secrets catalog");
    }

    [Fact]
    public void Findings_WhileOff_AreAdvisory()
    {
        var pocs = PocsConfigTests.Sound();
        pocs.Enabled = false;
        pocs.ProjectAgent = "ghost";

        PocTargetReferences.Findings(Config(pocs)).Should().ContainSingle().Which.IsBlocking.Should().BeFalse();
    }

    private static AgentSmithConfig Config(PocsConfig pocs) => new()
    {
        Pocs = pocs,
        Agents = new() { ["claude"] = new AgentConfig() },
        Trackers = new() { ["issues"] = new TrackerConnection() },
        Connections = new() { ["gh"] = new ResolvedConnection() },
        Repos = new() { ["poc-src"] = new RepoConnection() },
        Secrets = new Dictionary<string, string> { ["poc_pull_token"] = "pull-value", ["poc_argo_token"] = "argo-value" }, // c81ec
    };
}
