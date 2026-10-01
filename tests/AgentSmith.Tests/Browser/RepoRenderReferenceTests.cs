using System.Text;
using AgentSmith.Application.Services.Browser;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.References;
using FluentAssertions;

namespace AgentSmith.Tests.Browser;

/// <summary>
/// 2026-10-01-283dh: render_reference renders an .html file out of a repository — the page and the
/// directory tree it sits in are copied into the browser sandbox under the uploaded-website bounds,
/// so a mock beside a spec renders with its own stylesheet and nothing from outside its tree.
/// </summary>
public sealed class RepoRenderReferenceTests
{
    private const string Dir = ".agentsmith/specs/azdo-7";
    private readonly BrowserRenderFixture _fixture = new();
    private readonly InMemoryFileSandbox _repo = new();

    [Fact]
    public async Task RenderReference_RepoPath_CopiesLinkedCssInsideTheTreeOnly()
    {
        Seed($"{Dir}/p1-mock.html", "<link rel=\"stylesheet\" href=\"css/mock.css\"><link href=\"../../../shared/site.css\"><button>Go</button>");
        Seed($"{Dir}/css/mock.css", "button { background: #c0ffee; }");
        _repo.Files[$"/work/{Dir}/logo.png"] = [0x89, 0x50, 0x4E, 0x47, 0xFF, 0xFE];
        Seed("shared/site.css", "body { color: red; }");
        Seed($"{Dir}/node_modules/x/index.css", "skipped");

        var text = await Host().RenderReference($"{Dir}/p1-mock.html", ["button"], CancellationToken.None);

        var browser = _fixture.Spawned.Single();
        var request = browser.Requests.Single();
        var siteDir = request.GetProperty("siteDir").GetString()!;
        siteDir.Should().StartWith("/work/repo/");
        request.GetProperty("page").GetString().Should().Be("p1-mock.html");
        browser.Files.Keys.Where(k => k.StartsWith(siteDir + "/", StringComparison.Ordinal))
            .Select(k => k[(siteDir.Length + 1)..]).Should().BeEquivalentTo(["p1-mock.html", "css/mock.css"],
                "the tree's text files travel; the shared stylesheet outside it and node_modules do not");
        text.Should().Contain("background-color: rgb(192, 255, 238)")
            .And.Contain($"sample-api/{Dir}/logo.png: {RepoRenderSource.NotCopied}");
    }

    [Fact]
    public async Task RenderReference_RepoPathOverTheSetLimit_IsRefusedNamingIt()
    {
        Seed($"{Dir}/p1-mock.html", "<p>big</p>");
        for (var i = 0; i < 6; i++) _repo.Files[$"/work/{Dir}/bundle-{i}.js"] = new byte[(int)(4.5 * 1024 * 1024)];

        var text = await Host().RenderReference($"{Dir}/p1-mock.html", ct: CancellationToken.None);

        text.Should().StartWith("render_reference failed:").And.Contain("25 MB");
        _fixture.Spawned.Single().Requests.Should().BeEmpty("a tree over a bound is refused whole, before any render");
    }

    [Theory]
    [InlineData("../outside.html")]
    [InlineData("/etc/page.html")]
    public async Task RenderReference_PathOutsideTheRepository_IsAnError(string path)
    {
        var text = await Host().RenderReference(path, ct: CancellationToken.None);

        text.Should().StartWith("Error:");
        _fixture.Spawned.Should().BeEmpty();
    }

    [Fact]
    public void RenderSourceParser_RepoPrefixedPath_NamesTheRepository()
    {
        var (source, _) = new RenderSourceParser().Parse("sample-api/dist/index.html", Scope());

        source.Should().Be(RenderSource.OfRepo("sample-api", "dist/index.html"));
    }

    private RenderReferenceToolHost Host() => new(_fixture.Services(), Scope());

    private RenderReferenceScope Scope() => new(BrowserRenderFixture.Project, null, new Dictionary<string, ISandbox>(), [],
        new Dictionary<string, ISandbox> { ["sample-api"] = _repo, ["sample-web"] = new InMemoryFileSandbox() });

    private void Seed(string path, string text) => _repo.Files[$"/work/{path}"] = Encoding.UTF8.GetBytes(text);
}
