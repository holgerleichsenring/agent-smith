using System.Text;
using AgentSmith.Application.Services.Browser;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Browser;

/// <summary>2026-10-01-283de: the render's parts on their own — factory gate, text bounds, the lease's hand-back, the set under a root.</summary>
public sealed class BrowserRenderPartsTests
{
    [Fact]
    public void RenderReferenceToolFactory_TurnWithoutProjectOrConversation_BuildsNoHost()
    {
        var factory = new BrowserRenderFixture().Factory();
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.DialogueJobId, "conv");

        factory.Create(pipeline, isDesignTurn: true).Should().BeNull("an unseeded turn carries no project to spawn from");
        pipeline.Set(ContextKeys.SpecDialogProject, new ResolvedProject { Name = "p" });
        factory.Create(pipeline, isDesignTurn: true).Should().NotBeNull();
    }

    [Fact]
    public void RenderResultText_SixtyRefusals_ListsFiftyAndCountsTheRest()
    {
        var result = new BrowserRenderResult
        {
            Url = "https://example.test/", Title = "t",
            Refused = [.. Enumerable.Range(0, 60).Select(i => new BrowserRequestNote($"http://10.0.0.{i}/", "private"))],
        };

        var text = new RenderResultText().Render(result, ["desktop: shown"]);

        text.Should().Contain("Requests the egress guard refused (60)").And.Contain("… and 10 more")
            .And.Contain("http://10.0.0.49/").And.NotContain("http://10.0.0.50/");
    }

    [Fact]
    public async Task BrowserSandboxLease_Dispose_HandsTheSandboxBackToTheConversation()
    {
        var holds = Holds.Live();
        var sandbox = new BrowserFakeSandbox();
        var hold = new SourceScopeHold(holds, "conv", BrowserSandboxOpener.HeldName, null,
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        await new BrowserSandboxLease(sandbox, hold).DisposeAsync();

        (await holds.TakeAsync(HeldSandbox.KeyFor("conv", "browser", null), CancellationToken.None))
            .Should().BeSameAs(sandbox);
    }

    [Fact]
    public async Task ReferenceSetMaterialiser_PrepareUnder_WritesTheSetAndItsMarkerBelowTheRoot()
    {
        var fixture = new BrowserRenderFixture();
        fixture.Set.Add(new ReferenceSetFile("index.html", Encoding.UTF8.GetBytes("<p>x</p>")));
        var sandbox = new BrowserFakeSandbox();
        var materialiser = new ReferenceSetMaterialiser(fixture, new SandboxFileReaderFactory());

        var hash = await materialiser.PrepareUnderAsync(sandbox, "conv", "s1", "sets/s1", CancellationToken.None);
        var again = await materialiser.PrepareUnderAsync(sandbox, "conv", "s1", "sets/s1", CancellationToken.None);

        sandbox.Files.Keys.Should().Contain(["/work/sets/s1/index.html", "/work/sets/s1/.agentsmith-reference"]);
        again.Should().Be(hash);
        fixture.SetReads.Should().Be(1, "the marker says the set is already there");
    }
}
