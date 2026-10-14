using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>
/// 2026-10-13-8762e: a round's evidence check over ONE sandbox per repository — a missing citation named by
/// its fact, every citation it could not check counted, templates never probed, and nothing checked for a
/// spec that does not describe today.
/// </summary>
public sealed class SpecRoundEvidenceTests
{
    private const string Claim = "The handler reads the store.";

    [Fact]
    public async Task SpecRoundEvidence_OneRepositoryTwoToolchainSandboxes_ChecksTheCitation()
    {
        var tree = new Dictionary<string, string> { ["src/A.cs"] = "1\n2\n" };
        var pipeline = Pipeline(("shop-dotnet", "shop", new EvidenceTreeSandbox(tree)),
            ("shop-node", "shop", new EvidenceTreeSandbox(tree)));

        var result = await Check(pipeline, Spec(Claim, "src/A.cs:2; src/Missing.cs:4"));

        result.Problems.Should().ContainSingle().Which.Problem.Should().Contain("src/Missing.cs");
        (result.Unchecked, result.Total).Should().Be((0, 2), "both citations were checked against the one tree");
    }

    [Fact]
    public async Task SpecRoundEvidence_UnroutableCitation_IsCountedUnchecked()
    {
        var tree = new Dictionary<string, string> { ["src/A.cs"] = "1\n" };
        var pipeline = Pipeline(("api", "api", new EvidenceTreeSandbox(tree)), ("web", "web", new EvidenceTreeSandbox(tree)));

        var result = await Check(pipeline, Spec(Claim, "src/A.cs:1"));

        result.Problems.Should().BeEmpty();
        (result.Unchecked, result.Total).Should().Be((1, 1), "two repositories hold the bare path, so neither answers");
    }

    [Fact]
    public async Task SpecRoundEvidence_TemplateKeyInSandboxRepos_IsNotProbed()
    {
        var template = new EvidenceTreeSandbox(new Dictionary<string, string> { ["src/A.cs"] = "1\n" });
        var shop = new EvidenceTreeSandbox(new Dictionary<string, string>());
        var pipeline = Pipeline(("template:ui", "ui-template", template), ("shop", "shop", shop));

        var result = await Check(pipeline, Spec(Claim, "src/A.cs:1"));

        template.Ran.Should().BeEmpty("a template is not the run's repository");
        result.Problems.Should().ContainSingle("the one repository does not hold the file");
    }

    [Fact]
    public async Task SpecRoundEvidence_DerivedSpecMintedFacts_AreCountedUnchecked()
    {
        var pipeline = Pipeline(("shop", "shop", new EvidenceTreeSandbox(new Dictionary<string, string>())));

        var result = await Check(pipeline, Spec(Claim, "[L3] shop: the derivation ran 'read src/A.cs' exited 0"));

        result.Problems.Should().BeEmpty();
        (result.Unchecked, result.Total).Should().Be((1, 1), "no probe can route a minted look line");
    }

    [Fact]
    public async Task SpecRoundEvidence_MissingCitedFile_IsAProblemNamingTheClaim()
    {
        var pipeline = Pipeline(("shop", "shop", new EvidenceTreeSandbox(new Dictionary<string, string>())));

        var result = await Check(pipeline, Spec(Claim, "src/Gone.cs:7"));

        result.Problems.Should().ContainSingle().Which.Should().Be(
            new Contracts.Events.SpecEvidenceProblem(Claim, "not a file (missing, or a directory): src/Gone.cs"));
    }

    [Fact]
    public async Task SpecRoundEvidence_SpecNotDescribingToday_IsNotChecked()
    {
        var shop = new EvidenceTreeSandbox(new Dictionary<string, string>());
        var draft = Spec(Claim, "src/Gone.cs:7") with { Requires = ["2026-10-14-1234b"] };
        var set = new SpecSet("k", [new DerivedSpec(draft, "a", "", []),
            new DerivedSpec(draft with { SpecId = "2026-10-14-1234b" }, "b", "", [])], SpecAccounting.Empty, [], SpecSource.Derived);

        var result = await Evidence().CheckAsync(Pipeline(("shop", "shop", shop)), draft, set, CancellationToken.None);

        result.Should().Be(SpecRoundEvidenceResult.None);
        shop.Ran.Should().BeEmpty();
    }

    private static SpecDraft Spec(string claim, string evidence) =>
        new("2026-10-14-1234a", "goal", "spec: 2026-10-14-1234a", []) { Facts = [new SpecFact(claim, evidence)] };

    private static Task<SpecRoundEvidenceResult> Check(PipelineContext pipeline, SpecDraft draft) =>
        Evidence().CheckAsync(pipeline, draft,
            new SpecSet("k", [new DerivedSpec(draft, "a", "", [])], SpecAccounting.Empty, [], SpecSource.Derived),
            CancellationToken.None);

    private static SpecRoundEvidence Evidence() =>
        new(new EvidenceCheck(new EvidenceReferences()), new EvidenceReferences(), TurnActivityRecorder.Silent());

    private static PipelineContext Pipeline(params (string Key, string Name, ISandbox Sandbox)[] sandboxes)
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            sandboxes.ToDictionary(s => s.Key, s => s.Sandbox, StringComparer.Ordinal));
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos,
            sandboxes.ToDictionary(s => s.Key, s => s.Name, StringComparer.Ordinal));
        return pipeline;
    }
}
