using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-10-02-3f06c: which drafts of a proposal have their cited files read, and the finding a
/// citation that does not resolve becomes.
/// </summary>
public sealed class ProposalEvidenceReviewTests
{
    private const string Repo = "repo-a";

    private readonly EvidenceTreeSandbox _repo = new(new Dictionary<string, string> { ["src/a.cs"] = "1\n2\n" });

    [Fact]
    public async Task ProposalEvidenceReview_FactCitingAMissingFile_AddsAFinding()
    {
        var proposal = new PhaseOutcome(Draft("p9001", ("the handler exists", $"{Repo}/src/gone.cs:3"),
            ("the api file has two lines", $"{Repo}/src/a.cs:1-2")));

        var findings = await Findings(proposal);

        findings.Should().ContainSingle().Which.Should().Be(new ProposalFinding(
            "p9001", ProposalEvidenceReview.Problem,
            $"not a file (missing, or a directory): {Repo}/src/gone.cs", "the handler exists"));
    }

    [Fact]
    public async Task ProposalEvidenceReview_TemplateQualifiedPath_IsNotChecked()
    {
        var template = new EvidenceTreeSandbox(new Dictionary<string, string>());
        var proposal = new PhaseOutcome(Draft("p9001", ("the template has it", "template:sample/src/gone.cs:3")));

        var findings = await Findings(proposal, ("template:sample", template));

        findings.Should().BeEmpty();
        template.Ran.Should().BeEmpty("a template is not one of the turn's repositories");
        _repo.Ran.Should().BeEmpty("a qualified path is not routed into a repository either");
    }

    [Fact]
    public async Task ProposalEvidenceReview_LaterEpicChild_IsNotChecked()
    {
        var later = Draft("p9000b", ("b's file", $"{Repo}/src/made-by-a.cs")) with { Requires = ["p9000a"] };
        var first = Draft("p9000a", ("a's file", $"{Repo}/src/gone.cs"));
        var proposal = new EpicOutcome(Draft("p9000"), [later, first]);

        var findings = await Findings(proposal);

        findings.Select(f => f.PhaseId).Should().Equal(["p9000a"],
            "the first child in filing order describes today; the one after it what 'a' leaves");
    }

    [Fact]
    public async Task ProposalEvidenceReview_PhaseRequiringAPhase_IsNotChecked()
    {
        var proposal = new PhaseOutcome(
            Draft("p9001", ("made earlier", $"{Repo}/src/gone.cs")) with { Requires = ["p9000"] });

        (await Findings(proposal)).Should().BeEmpty();
    }

    private async Task<IReadOnlyList<ProposalFinding>> Findings(
        OutcomeProposal proposal, params (string Name, ISandbox Sandbox)[] more)
    {
        var map = new Dictionary<string, ISandbox> { [Repo] = _repo };
        foreach (var (name, sandbox) in more) map[name] = sandbox;
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandboxes, (IReadOnlyDictionary<string, ISandbox>)map);
        return await ProposalEvidenceTestReview.Create().FindingsAsync(pipeline, proposal, CancellationToken.None);
    }

    private static PhaseDraft Draft(string phaseId, params (string Claim, string Evidence)[] facts) =>
        new(phaseId, $"goal of {phaseId}", $"spec: {phaseId}", [])
        {
            Facts = [.. facts.Select(f => new PhaseFact(f.Claim, f.Evidence))],
        };
}
