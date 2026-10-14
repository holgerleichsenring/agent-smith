using System.Net;
using System.Text;
using AgentSmith.Application.Services.Browser;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.References;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Browser;

/// <summary>
/// 2026-10-13-8762r: write_mock stores a version and renders it through render_reference's real
/// composition, keeping the DESKTOP screenshot by its viewport; read_mock reads a version back whole.
/// </summary>
public sealed class MockWriteToolTests
{
    private const string Page = "<!doctype html><button>Go</button>";
    private readonly BrowserRenderFixture _fixture = new();
    private readonly MemoryMocks _mocks = new();

    [Fact]
    public async Task RenderSourceStager_Inline_WritesUnderMocksRoot()
    {
        var browser = new BrowserFakeSandbox();
        var stager = new RenderSourceStager(new ReferenceSetMaterialiser(_fixture, new SandboxFileReaderFactory(), new SandboxBinaryFileWriter()),
            new RepoRenderSource(new SandboxFileReaderFactory(), new RepoTreeListing()));
        var scope = new RenderReferenceScope(BrowserRenderFixture.Project, BrowserRenderFixture.Conversation, new Dictionary<string, ISandbox>(), []);

        var (staged, _) = await stager.StageAsync(browser, scope,
            RenderSource.OfInline("mock-1", "spec-a.html", [new ReferenceSetFile("spec-a.html", Encoding.UTF8.GetBytes(Page))]), default);

        staged!.SiteDir.Should().Be("/work/mocks/mock-1");
        staged.Page.Should().Be("spec-a.html");
        browser.Files["/work/mocks/mock-1/spec-a.html"].Should().Equal(Encoding.UTF8.GetBytes(Page));
        _fixture.SetReads.Should().Be(0, "an inline page is never read from the upload store");
    }

    [Fact]
    public async Task WriteMock_StoresRendersAndKeepsTheDesktopShot()
    {
        var answer = await Host().WriteMock("spec-a", Page);

        answer.Should().StartWith("Stored spec-a mock v1 (spec-a-mock-v1.html).").And.Contain("desktop screenshot");
        _mocks.Pages.Should().ContainSingle().Which.Html.Should().Be(Page);
        _mocks.Shots["mock-1"].Should().Equal(BrowserFakeSandbox.Jpeg);
        _fixture.Deposited.Should().ContainSingle("the model sees the render as render_reference shows it");
        _fixture.Spawned.Single().Requests[0].GetProperty("siteDir").GetString().Should().Be("/work/mocks/mock-1");
    }

    [Fact]
    public async Task WriteMock_ShotsInAnotherOrder_StillKeepsTheDesktopViewport()
    {
        _fixture.Viewports = ["mobile", "desktop"];

        await Host().WriteMock("spec-a", Page);

        _fixture.Deposited.Should().HaveCount(2);
        _mocks.Shots["mock-1"].Should().Equal(BrowserFakeSandbox.JpegOf("desktop"), "the shot is chosen by viewport, never by position");
    }

    [Fact]
    public async Task WriteMock_RenderFails_KeepsTheVersionAndSaysWhy()
    {
        var inProcess = new BrowserRenderFixture(spawnsContainers: false);

        var answer = await inProcess.MockHost(_mocks, _mocks).WriteMock("spec-a", Page);

        answer.Should().Contain("v1").And.Contain("kept without a screenshot").And.Contain("no browser runtime exists");
        _mocks.Pages.Should().ContainSingle();
        _mocks.Shots.Should().BeEmpty();
    }

    [Theory]
    [InlineData("-leading-dash")]
    [InlineData("has space")]
    [InlineData("../escape")]
    [InlineData("")]
    public async Task WriteMock_BadSpecId_IsAnError_NothingStored(string specId)
    {
        var answer = await Host().WriteMock(specId, Page);

        answer.Should().StartWith("Error:").And.Contain("nothing was stored");
        _mocks.Pages.Should().BeEmpty();
        _fixture.Spawned.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadMock_NoId_ListsVersions_IdReturnsNewestHtmlWithOneHeaderLine()
    {
        await _mocks.AddAsync(BrowserRenderFixture.Conversation, "spec-a", "<p>one</p>", default);
        await _mocks.AddAsync(BrowserRenderFixture.Conversation, "spec-a", "<p>two</p>", default);

        var listing = await Host().ReadMock();
        var newest = await Host().ReadMock("spec-a");
        var first = await Host().ReadMock("spec-a", 1);

        listing.Should().Contain("spec-a v1 (spec-a-mock-v1.html)").And.Contain("spec-a v2 (spec-a-mock-v2.html)");
        newest.Should().Be("spec-a mock v2 (spec-a-mock-v2.html):\n<p>two</p>", "one header line, then the page — not the list");
        first.Should().EndWith("\n<p>one</p>");
        (await Host().ReadMock("spec-a", 3)).Should().StartWith("Error:");
    }

    [Fact]
    public async Task ReadMock_MockAtTheCap_ReadsBackWhole()
    {
        var html = "<p>" + new string('x', IConversationMocks.MaxChars - 7) + "</p>";
        html.Length.Should().Be(IConversationMocks.MaxChars);
        await _mocks.AddAsync(BrowserRenderFixture.Conversation, "spec-a", html, default);
        var read = new AgentSmith.Application.Services.Loop.BoundedResultAIFunction(
            Host().GetTools(null, null).Single(t => t.Name == "read_mock"));

        var result = await read.InvokeAsync(new AIFunctionArguments { ["spec_id"] = "spec-a" });

        result.Should().Be($"spec-a mock v1 (spec-a-mock-v1.html):\n{html}",
            "a mock at the cap stays under the tool-result bound, so nothing is cut from its middle");
    }

    [Fact]
    public void RenderReferenceToolFactory_DesignTurn_HasWriteMock_RunHasNot()
    {
        var turn = new PipelineContext();
        turn.Set(ContextKeys.SpecDialogProject, BrowserRenderFixture.Project);
        turn.Set(ContextKeys.DialogueJobId, BrowserRenderFixture.Conversation);
        var run = new PipelineContext();
        run.Set(ContextKeys.ProjectConfig, new AgentSmith.Contracts.Models.Configuration.ResolvedProject
        {
            Name = "p", Sandbox = new() { Browser = new() { Enabled = true } },
        });

        var factory = _fixture.Factory(_mocks, _mocks);

        factory.Tools(turn, isDesignTurn: true).Select(t => t.Name).Should().Contain(["write_mock", "read_mock"]);
        factory.Tools(run, isDesignTurn: false).Select(t => t.Name).Should().NotContain("write_mock").And.Contain("render_reference");
        _fixture.Factory().Tools(turn, isDesignTurn: true).Select(t => t.Name).Should().NotContain("write_mock", "no store, no tool");
    }

    private MockWriteToolHost Host()
    {
        _fixture.Hosts["example.test"] = [IPAddress.Parse("93.184.215.14")];
        return _fixture.MockHost(_mocks, _mocks);
    }
}
