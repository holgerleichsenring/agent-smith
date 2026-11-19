using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-10-02-3f06c: the finding a citation that does not resolve becomes. 2026-11-19-6c6d: every draft of a
/// proposal is read, whatever it requires.
/// </summary>
public sealed class ProposalEvidenceReviewTests
{
    private const string Repo = "repo-a";

    private readonly EvidenceTreeSandbox _repo = new(new Dictionary<string, string> { ["src/a.cs"] = "1\n2\n" });

    [Fact]
    public async Task ProposalEvidenceReview_FactCitingAMissingFile_AddsAFinding()
    {
        var proposal = new SpecOutcome(Draft("p9001", ("the handler exists", $"{Repo}/src/gone.cs:3"),
            ("the api file has two lines", $"{Repo}/src/a.cs:1-2")));

        var findings = await Findings(proposal);

        findings.Should().ContainSingle().Which.Should().Be(new ProposalFinding(
            "p9001", ProposalEvidenceReview.Problem,
            $"not a file (missing, or a directory): {Repo}/src/gone.cs — {ProposalEvidenceReview.CiteWhatExists}",
            "the handler exists"));
    }

    [Fact]
    public async Task ProposalEvidenceReview_TemplateQualifiedPath_IsNotChecked()
    {
        var template = new EvidenceTreeSandbox(new Dictionary<string, string>());
        var proposal = new SpecOutcome(Draft("p9001", ("the template has it", "template:sample/src/gone.cs:3")));

        var findings = await Findings(proposal, ("template:sample", template));

        findings.Should().BeEmpty();
        template.Ran.Should().BeEmpty("a template is not one of the turn's repositories");
        _repo.Ran.Should().BeEmpty("a qualified path is not routed into a repository either");
    }

    [Fact]
    public async Task ProposalEvidenceReview_LaterEpicChild_IsChecked()
    {
        var later = Draft("p9000b", ("b's file", $"{Repo}/src/made-by-a.cs")) with { Requires = ["p9000a"] };
        var first = Draft("p9000a", ("a's file", $"{Repo}/src/gone.cs"));
        var proposal = new EpicOutcome(Draft("p9000"), [later, first]);

        var findings = await Findings(proposal);

        findings.Select(f => f.SpecId).Should().BeEquivalentTo(["p9000b", "p9000a"],
            "what 'a' will create is an assumption of 'b', not evidence");
    }

    [Fact]
    public async Task ProposalEvidenceReview_SpecRequiringASpec_IsChecked()
    {
        var proposal = new SpecOutcome(
            Draft("p9001", ("made earlier", $"{Repo}/src/gone.cs")) with { Requires = ["p9000"] });

        (await Findings(proposal)).Should().ContainSingle().Which.SpecId.Should().Be("p9001");
    }

    [Fact]
    public async Task ProposalEvidenceReview_SpecOfATicketRequiringATicket_MissingCitation_SaysCiteWhatExists()
    {
        var proposal = TicketList(Draft("p9002a", ("the api ticket adds it", $"{Repo}/src/made-by-api.cs:4")));

        var findings = await Findings(proposal);

        findings.Should().ContainSingle().Which.Should().Be(new ProposalFinding(
            "p9002a", ProposalEvidenceReview.Problem,
            $"not a file (missing, or a directory): {Repo}/src/made-by-api.cs — cite what exists now; "
            + "what an earlier spec or ticket creates is an assumption",
            "the api ticket adds it"));
    }

    [Fact]
    public async Task ProposalEvidenceReview_SpecOfATicketRequiringATicket_ResolvingCitation_IsNoFinding()
    {
        var proposal = TicketList(Draft("p9002a", ("the api file has two lines", $"{Repo}/src/a.cs:1-2")));

        (await Findings(proposal)).Should().BeEmpty();
        _repo.Ran.Should().NotBeEmpty("the spec of a ticket that requires another is read too");
    }

    [Fact]
    public async Task ProposalEvidenceReview_SpecOfATicketRequiringATicket_UploadCitation_Resolves()
    {
        var upload = new EvidenceTreeSandbox(new Dictionary<string, string> { ["spec-doc/prd.md"] = "1\n2\n3\n" });
        var proposal = TicketList(
            Draft("p9002a", ("the prd states it", "reference:site/spec-doc/prd.md:3")),
            Draft("p9002b", ("the prd is longer", "reference:site/spec-doc/prd.md:9")));

        var findings = await Findings(proposal, ("reference:site", upload));

        findings.Should().ContainSingle().Which.SpecId.Should().Be("p9002b");
        upload.Ran.Should().NotBeEmpty();
    }

    private static TicketGraphOutcome TicketList(params SpecDraft[] uiSpecs) => new(
    [
        new GraphTicket("order-api", "API", "r", ["d"], [], []),
        new GraphTicket("order-ui", "UI", "r", ["d"], ["order-api"], []) { Specs = uiSpecs },
    ]);

    private async Task<IReadOnlyList<ProposalFinding>> Findings(
        OutcomeProposal proposal, params (string Name, ISandbox Sandbox)[] more)
    {
        var map = new Dictionary<string, ISandbox> { [Repo] = _repo };
        foreach (var (name, sandbox) in more) map[name] = sandbox;
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandboxes, (IReadOnlyDictionary<string, ISandbox>)map);
        return await ProposalEvidenceTestReview.Create().FindingsAsync(pipeline, proposal, CancellationToken.None);
    }

    private static SpecDraft Draft(string specId, params (string Claim, string Evidence)[] facts) =>
        new(specId, $"goal of {specId}", $"spec: {specId}", [])
        {
            Facts = [.. facts.Select(f => new SpecFact(f.Claim, f.Evidence))],
        };
}
