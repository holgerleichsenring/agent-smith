using System.Text;
using AgentSmith.Application.Services.Browser;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Browser;

/// <summary>
/// 2026-10-01-283df: render_reference in a RUN — the project's config decides whether it exists, an
/// address names a set the run carries (read from the store under the APPROVAL's conversation), and
/// the browser is spawned for the render and disposed after it, because a run has nothing to hold it for.
/// </summary>
public sealed class RunRenderReferenceTests
{
    private static readonly CarriedReferenceSet Carried =
        new("set-1", "site", "reference:site", "sample-api", ".agentsmith/reference/set-1", "approval-conv", 1);

    private readonly BrowserRenderFixture _fixture = new();

    [Fact]
    public async Task RenderReference_InARun_RendersACarriedSetAndDisposesTheBrowser()
    {
        _fixture.Set.Add(new ReferenceSetFile("site/index.html", Encoding.UTF8.GetBytes("<button>Go</button>")));

        var first = await _fixture.RunHost([Carried]).RenderReference("reference:site", ["button"], CancellationToken.None);
        await _fixture.RunHost([Carried]).RenderReference("reference:site", ["button"], CancellationToken.None);

        first.Should().Contain("background-color: rgb(192, 255, 238)");
        _fixture.SessionsRead.Should().AllBe("approval-conv", "a run reads the set where the approval's conversation stored it");
        _fixture.Spawned.Should().HaveCount(2).And.OnlyContain(b => b.Disposed, "nothing holds a run's browser");
        _fixture.Specs.Should().OnlyContain(s => s.ConversationId == null);
    }

    [Fact]
    public async Task RenderReference_InARun_AnAddressTheRunDoesNotCarry_IsAnError()
    {
        var text = await _fixture.RunHost([Carried]).RenderReference("reference:other", ct: CancellationToken.None);

        text.Should().StartWith("Error:").And.Contain("this run");
        _fixture.Spawned.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void RenderReferenceToolFactory_Run_FollowsTheProjectsBrowserConfig(bool enabled, bool expectHost)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject
        {
            Name = "p", Sandbox = new SandboxConfig { Browser = new ProjectBrowserConfig { Enabled = enabled } },
        });

        var host = new RenderReferenceToolFactory(_fixture.Services()).Create(pipeline, isDesignTurn: false);

        (host is not null).Should().Be(expectHost);
    }

    [Fact]
    public void RenderReferenceToolFactory_DesignTurnWithoutItsSeeds_GetsNoHost()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject
        {
            Name = "p", Sandbox = new SandboxConfig { Browser = new ProjectBrowserConfig { Enabled = true } },
        });

        new RenderReferenceToolFactory(_fixture.Services()).Create(pipeline, isDesignTurn: true).Should().BeNull();
    }

    [Fact]
    public void RenderSourceParser_CarriedAddress_NamesTheSetAndItsSession()
    {
        var (source, refusal) = new RenderSourceParser().Parse("reference:site/index.html",
            new RenderReferenceScope(BrowserRenderFixture.Project, null, new Dictionary<string, ISandbox>(), [Carried]));

        refusal.Should().BeNull();
        source!.Should().Be(RenderSource.OfSet("set-1", "index.html", "approval-conv"));
    }
}
