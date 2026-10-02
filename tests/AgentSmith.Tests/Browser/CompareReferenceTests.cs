using AgentSmith.Application.Services.Browser;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.References;
using FluentAssertions;

namespace AgentSmith.Tests.Browser;

/// <summary>
/// 2026-10-01-283di: compare_reference — the exact level lists every computed-style difference by
/// name with both values, a selector matching nothing is a named difference, the observed level is
/// a pixel ratio with a diff image, and a run keeps both for result.md without gating on either.
/// </summary>
public sealed class CompareReferenceTests
{
    private static readonly IReadOnlyList<string> Properties = ["font-size", "color"];
    private readonly StyleDifferenceComparer _comparer = new();
    private readonly BrowserRenderFixture _fixture = new();

    [Fact]
    public void StyleDifferenceComparer_SameComputedValues_ReportsNothing() =>
        _comparer.Compare([new SelectorPair("h1", ".title")], [Row("h1", "16px")], [Row(".title", "16px")], Properties)
            .Should().BeEmpty();

    [Fact]
    public void StyleDifferenceComparer_FontSize16pxVs15px_NamesPropertyAndBothValues() =>
        _comparer.Compare([new SelectorPair("h1", ".title")], [Row("h1", "16px")], [Row(".title", "15px")], Properties)
            .Should().Equal(new StyleDifference("h1", ".title", "font-size", "16px", "15px"));

    [Fact]
    public void StyleDifferenceComparer_SelectorMatchesNothing_IsANamedDifference() =>
        _comparer.Compare([new SelectorPair(".hero", ".hero")], [Row(".hero", "16px")],
                [new BrowserStyleRow(".hero", 0, null)], Properties)
            .Should().Equal(new StyleDifference(".hero", ".hero", StyleDifference.MatchProperty, "1 element", StyleDifference.NoMatch));

    [Fact]
    public async Task CompareReference_31Pairs_IsRefusedNamingTheLimit()
    {
        var text = await Host().CompareReference("https://example.test/a", "https://example.test/b",
            [.. Enumerable.Range(0, 31).Select(i => $"#a{i} => #b{i}")], ct: CancellationToken.None);

        text.Should().StartWith("Error:").And.Contain("at most 30");
        _fixture.Spawned.Should().BeEmpty();
    }

    [Fact]
    public async Task CompareReference_TwoSites_LoadsBothInOneInvocationAndReportsBothLevels()
    {
        _fixture.Hosts["example.test"] = [System.Net.IPAddress.Parse("93.184.215.14")];

        var text = await Host().CompareReference("https://example.test/reference", "https://example.test/built",
            ["h1", "missing => main"], "both", CancellationToken.None);

        var browser = _fixture.Spawned.Single();
        browser.Runs.Should().ContainSingle("both sides load in one run of the script");
        var job = browser.Compares.Single();
        job.GetProperty("reference").GetProperty("url").GetString().Should().Be("https://example.test/reference");
        job.GetProperty("candidate").GetProperty("url").GetString().Should().Be("https://example.test/built");
        text.Should().Contain("h1 ⇄ h1: font-size — reference 16px, candidate 15px")
            .And.Contain($"missing ⇄ main: {StyleDifference.MatchProperty} — reference {StyleDifference.NoMatch}, candidate 1 element")
            .And.Contain("35.70% of pixels differ").And.Contain("padded with magenta to 1400 px")
            .And.Contain("not a verdict");
        _fixture.Deposited.Should().HaveCount(2, "one diff image per viewport");
    }

    [Fact]
    public async Task CompareReference_InARun_KeepsItForResultMdAndWritesAtMostFourDiffImages()
    {
        _fixture.Hosts["example.test"] = [System.Net.IPAddress.Parse("93.184.215.14")];
        var repo = new InMemoryFileSandbox();
        var run = new PipelineContext();
        run.Set(ContextKeys.RunId, "run-7");
        var host = new CompareReferenceToolHost(_fixture.CompareServices(), new RenderReferenceScope(BrowserRenderFixture.Project,
            null, new Dictionary<string, ISandbox>(), [], new Dictionary<string, ISandbox> { ["sample-api"] = repo }, run));

        for (var i = 0; i < 3; i++)
            await host.CompareReference("https://example.test/a", "https://example.test/b", ["h1"], "both", CancellationToken.None);

        var kept = VisualComparisonRecorder.Recorded(run);
        kept.Should().HaveCount(3);
        kept.SelectMany(c => c.Images).Should().Equal(
            ".agentsmith/runs/run-7/compare/01-desktop.jpg", ".agentsmith/runs/run-7/compare/01-mobile.jpg",
            ".agentsmith/runs/run-7/compare/02-desktop.jpg", ".agentsmith/runs/run-7/compare/02-mobile.jpg");
        repo.Files.Keys.Should().Contain("/work/.agentsmith/runs/run-7/compare/01-desktop.jpg");
        repo.Files["/work/.agentsmith/runs/run-7/compare/01-desktop.jpg"].Should().Equal(BrowserFakeSandbox.Jpeg);
    }

    [Fact]
    public void CompareReference_RunResult_HasVisualComparisonSectionAndNoGate()
    {
        var run = new PipelineContext();
        run.Set<IReadOnlyList<VisualComparison>>(ContextKeys.VisualComparisons, [new VisualComparison("reference:site", "dist/index.html",
            [new ViewportComparison("desktop", 0.357, 900, 1400, 1400, [new StyleDifference("h1", "h1", "font-size", "16px", "15px")])],
            [".agentsmith/runs/run-7/compare/01-desktop.jpg"])]);

        var section = VisualComparisonSection.Build(run);

        section.Should().Contain(VisualComparisonSection.Heading).And.Contain("never gated")
            .And.Contain("h1 ⇄ h1: font-size — reference 16px, candidate 15px")
            .And.Contain("`.agentsmith/runs/run-7/compare/01-desktop.jpg`");
        VisualComparisonSection.Build(new PipelineContext()).Should().BeEmpty("a run that compared nothing says nothing");
    }

    [Fact]
    public void ComparePairs_Viewport_IsDesktopMobileOrBoth()
    {
        ComparePairs.Viewports(null).Should().Equal("desktop");
        ComparePairs.Viewports("both").Should().Equal("desktop", "mobile");
        ComparePairs.Viewports("tablet").Should().BeNull();
    }

    private CompareReferenceToolHost Host() =>
        new(_fixture.CompareServices(), new RenderReferenceScope(BrowserRenderFixture.Project, BrowserRenderFixture.Conversation,
            new Dictionary<string, ISandbox>(), []));

    private static BrowserStyleRow Row(string selector, string fontSize) =>
        new(selector, 1, new Dictionary<string, string> { ["font-size"] = fontSize, ["color"] = "rgb(1, 2, 3)" });
}
