using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services.Structure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Structure;

public sealed class StructureBaselineSentenceTests
{
    [Fact]
    public void InitPullRequestBody_BaselineWritten_NamesTheCounts()
    {
        var outcome = new BootstrapRoundOutcome("repo", "api", PrinciplesMode.Transferred, [],
            StructureBaseline: new StructureBaselineResult(true, 3, 2, false));

        var body = InitPullRequestBody.Compose([outcome], "repo", "<!-- siblings -->");

        body.Should().Contain("3 unit(s) already exceed the Limits")
            .And.Contain("2 file(s) could not be read and are not covered");
    }

    [Fact]
    public void Sentence_Refreshed_SaysAGrownUnitSurfacesAtTheGate()
    {
        new StructureBaselineResult(true, 1, 0, true).Sentence()
            .Should().Contain("a unit grown past an unchanged limit surfaces at the gate");
    }

    [Fact]
    public void Sentence_Skipped_SaysWhy()
    {
        StructureBaselineResult.Skipped("the source is not a git checkout").Sentence()
            .Should().Be("no structure baseline — the source is not a git checkout");
    }

    [Fact]
    public void AddStructureJudging_ResolvedFromTheContainer_BuildsTheBaseline()
    {
        using var provider = new ServiceCollection().AddLogging().AddStructureEngine().AddStructureJudging()
            .AddSingleton<ISandboxFileReaderFactory, SandboxFileReaderFactory>().BuildServiceProvider();

        provider.GetRequiredService<StructureBaseline>().Should().NotBeNull();
    }
}
