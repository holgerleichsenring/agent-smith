using System.Net;
using System.Text;
using AgentSmith.Application.Services.Browser;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.References;
using AgentSmith.Tests.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Browser;

/// <summary>
/// 2026-10-01-283de: render_reference end to end over the real composition — refusals before any
/// spawn, a set copied once per sandbox life, one held sandbox per conversation, styles in the text
/// and screenshots through the deposit.
/// </summary>
public sealed class RenderReferenceToolTests
{
    private readonly BrowserRenderFixture _fixture = new();

    [Fact]
    public async Task RenderReference_HostResolvingToTen_IsRefusedBeforeSpawning()
    {
        _fixture.Hosts["intranet.example"] = [IPAddress.Parse("10.0.0.1")];

        var text = await _fixture.Host().RenderReference("https://intranet.example/", ct: CancellationToken.None);

        text.Should().StartWith("Refused:").And.Contain("10.0.0.1");
        _fixture.Spawned.Should().BeEmpty("a refused target never costs a spawn");
    }

    [Fact]
    public async Task RenderReference_FtpUrl_IsRefused()
    {
        var text = await _fixture.Host().RenderReference("ftp://files.example/site.html", ct: CancellationToken.None);

        text.Should().Contain("only http and https");
        _fixture.Spawned.Should().BeEmpty();
    }

    [Fact]
    public async Task RenderReference_InProcessBackend_SaysNoBrowserRuntimeExists()
    {
        var inProcess = new BrowserRenderFixture(spawnsContainers: false);
        inProcess.Hosts["example.test"] = [IPAddress.Parse("93.184.215.14")];

        var text = await inProcess.Host().RenderReference("https://example.test/", ct: CancellationToken.None);

        text.Should().Contain("no browser runtime exists");
        inProcess.Spawned.Should().BeEmpty("the server never runs a browser");
    }

    [Fact]
    public async Task RenderReference_ReferenceAddress_CopiesTheSetOncePerSandbox()
    {
        _fixture.Set.AddRange([new ReferenceSetFile("site/index.html", Encoding.UTF8.GetBytes("<button>Go</button>")),
            new ReferenceSetFile("site/logo.png", [0x89, 0x50, 0x4E, 0x47])]);
        var references = new ReferenceSandboxFixture();
        await using var scope = references.Open(Holds.None(), "reference:site");
        var map = new Dictionary<string, ISandbox> { ["reference:site"] = scope };

        await _fixture.Host(map).RenderReference("reference:site", ct: CancellationToken.None);
        var second = await _fixture.Host(map).RenderReference("reference:site/index.html", ["button"], CancellationToken.None);

        _fixture.Spawned.Should().ContainSingle("the conversation's browser sandbox is held between renders");
        _fixture.SetReads.Should().Be(1, "the set is copied once per sandbox life");
        var browser = _fixture.Spawned.Single();
        browser.Files["/work/sets/set-1/site/index.html"].Should().Equal(Encoding.UTF8.GetBytes("<button>Go</button>"));
        browser.Files["/work/sets/set-1/site/logo.png"].Should().Equal([0x89, 0x50, 0x4E, 0x47]);
        browser.Requests[1].GetProperty("siteDir").GetString().Should().Be("/work/sets/set-1");
        browser.Requests[1].GetProperty("page").GetString().Should().Be("index.html");
        browser.Requests[1].GetProperty("url").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        second.Should().Contain("background-color: rgb(192, 255, 238)");
        references.Spawned.Should().BeEmpty("rendering a set never opens its read-only scope");
    }

    [Fact]
    public async Task RenderReference_PublicUrl_ReturnsStylesAndRefusalsAndDepositsTheScreenshot()
    {
        _fixture.Hosts["example.test"] = [IPAddress.Parse("93.184.215.14")];

        var text = await _fixture.Host().RenderReference("https://example.test/", ct: CancellationToken.None);

        text.Should().Contain("h1: no match").And.Contain("127.0.0.1:6379").And.Contain("x is undefined")
            .And.Contain("desktop screenshot of https://example.test/").And.Contain(": shown");
        _fixture.Deposited.Should().ContainSingle().Which.Bytes.Should().Equal(BrowserFakeSandbox.Jpeg);
        var spec = _fixture.Specs.Single();
        spec.ToolchainImage.Should().Be("registry/agent-smith-sandbox-browser:9.9.9");
        spec.TimeoutSeconds.Should().Be(600, "a cold pull of the browser image counts against readiness");
        spec.Resources.Should().Be(new ResourceLimits("500m", "2000m", "1Gi", "2Gi"));
        spec.ConversationId.Should().Be(BrowserRenderFixture.Conversation);
        var args = _fixture.Spawned.Single().Runs.Single().Args!;
        args.Should().HaveCount(2, "node gets the script and one request file, nothing model-given");
        args[0].Should().Be("/opt/agentsmith/render.mjs");
        args[1].Should().StartWith("/work/render/").And.EndWith("/request.json");
    }

    [Theory]
    [InlineData("reference:other")]
    [InlineData("reference:site/../secret")]
    [InlineData("just words")]
    public async Task RenderReference_SourceThatNamesNoRenderableTarget_IsAnError(string source)
    {
        var text = await _fixture.Host().RenderReference(source, ct: CancellationToken.None);

        text.Should().StartWith("Error:");
        _fixture.Spawned.Should().BeEmpty();
    }

    [Fact]
    public async Task RenderReference_TwentyOneSelectors_AreRefused()
    {
        var text = await _fixture.Host().RenderReference("https://example.test/",
            [.. Enumerable.Range(0, 21).Select(i => $"#s{i}")], CancellationToken.None);

        text.Should().StartWith("Error:").And.Contain("20");
    }
}
