using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Handlers;
using AgentSmith.Tests.Specs;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-10-14-da5ee: verify_series reviews the specs of a ticket's series that have not run, against the code at the
/// ticket branch — an unstarted spec may cite a file an executed spec created there.
/// </summary>
public sealed class SeriesVerificationTests
{
    private const string Repo = "api";

    private readonly RevisionScopes _scopes = new(new Dictionary<string, string> { ["src/made-by-a.cs"] = "1\n2\n" });
    private readonly Mock<ISpecCutReviewer> _reviewer = new();
    private IReadOnlyList<SpecDraft> _reviewed = [];

    public SeriesVerificationTests() =>
        _reviewer.Setup(r => r.ReviewAsync(It.IsAny<IReadOnlyList<SpecDraft>>(), It.IsAny<string>(), null,
                It.IsAny<DerivationLook?>(), It.IsAny<AgentConfig>(), It.IsAny<PipelineCostTracker>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyList<SpecDraft> drafts, string _, string? _, DerivationLook? _, AgentConfig _,
                PipelineCostTracker _, CancellationToken _) => _reviewed = drafts)
            .ReturnsAsync(SpecCutReview.Clean);

    [Fact]
    public async Task VerifySeries_ReviewsOnlyUnstartedSpecs_AtTheTicketBranch()
    {
        var ran = Draft("p9000a", ("a's own file", $"{Repo}/src/gone.cs"));
        var next = Draft("p9000b", ("a created it", $"{Repo}/src/made-by-a.cs:1-2")) with { Requires = ["p9000a"] };
        var later = Draft("p9000c", ("b creates it", $"{Repo}/src/made-by-b.cs")) with { Requires = ["p9000b"] };
        var series = Reading(new Dictionary<string, string> { [Repo] = "branch-sha" }, (ran, true), (next, false), (later, false));

        var answer = await VerifyAsync(series);

        answer.Should().Contain("Verified 2 unstarted spec(s) at the ticket branch (api@branch-sha)").And.EndWith("No findings.");
        _scopes.Revisions.Should().Equal("branch-sha");
        _reviewed.Select(d => d.SpecId).Should().Equal("p9000b", "p9000c");
        _scopes.Opened.Single().Ran.Should().ContainSingle("only the spec whose predecessors all ran describes the branch");
    }

    [Fact]
    public async Task VerifySeries_CitationMissingAtTheBranch_IsAFinding()
    {
        var next = Draft("p9000b", ("the handler exists", $"{Repo}/src/gone.cs"));
        var series = Reading(new Dictionary<string, string> { [Repo] = "branch-sha" }, (next, false));

        var answer = await VerifyAsync(series);

        answer.Should().Contain("1 finding(s)").And.Contain("[p9000b] cited evidence does not resolve");
    }

    [Fact]
    public async Task VerifySeries_NoPublishedRevision_ReadsTheDefaultBranch_SkipsEvidence()
    {
        var series = Reading(new Dictionary<string, string>(), (Draft("p9000b", ("x", $"{Repo}/src/gone.cs")), false));

        var answer = await VerifyAsync(series);

        answer.Should().StartWith(Application.Services.SpecDialog.SeriesVerification.DefaultBranch).And.EndWith("No findings.");
        _scopes.Revisions.Should().BeEmpty();
        _reviewed.Should().ContainSingle();
    }

    [Fact]
    public async Task VerifySeries_EverySpecRan_NothingToVerify()
    {
        var answer = await VerifyAsync(Reading(new Dictionary<string, string> { [Repo] = "sha" }, (Draft("p9000a"), true)));

        answer.Should().Contain("nothing is left to verify");
        _reviewer.VerifyNoOtherCalls();
    }

    private async Task<string> VerifyAsync(SeriesReading reading)
    {
        var port = new Mock<ISeriesRead>();
        port.Setup(p => p.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(reading);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecDialogProject, new ResolvedProject
        {
            Name = "alpha", Repos = [new RepoConnection { Name = Repo, DefaultBranch = "main" }],
        });
        pipeline.Set(ContextKeys.DialogueJobId, "s-1");
        var tool = new VerifySeriesToolHost(port.Object, MasterHandlerFixture.Verification(_scopes, _reviewer.Object),
            pipeline, new AgentConfig()).GetTools(null, null).Single();
        tool.Name.Should().Be("verify_series");
        return (await tool.InvokeAsync(new AIFunctionArguments()))!.ToString()!;
    }

    private static SeriesReading Reading(IReadOnlyDictionary<string, string> shas, params (SpecDraft Draft, bool Ran)[] specs) =>
        new(null, "p9000", [.. specs.Select(s => new SeriesEntry(s.Draft, s.Ran ? "done" : "not_started", s.Ran))], shas);

    private static SpecDraft Draft(string specId, params (string Claim, string Evidence)[] facts) =>
        new(specId, $"goal of {specId}", $"spec: {specId}", [])
        {
            Facts = [.. facts.Select(f => new SpecFact(f.Claim, f.Evidence))],
        };

    /// <summary>Opens a tree scope per repository, recording the revision each was asked at.</summary>
    private sealed class RevisionScopes(IReadOnlyDictionary<string, string> files) : ISourceScopeSandboxFactory
    {
        public List<string?> Revisions { get; } = [];
        public List<EvidenceTreeSandbox> Opened { get; } = [];

        public ISourceScopeSandbox Create(
            ResolvedProject project, RepoConnection repo, string? revision = null, string? conversationId = null)
        {
            Revisions.Add(revision);
            var scope = new EvidenceTreeSandbox(files);
            Opened.Add(scope);
            return scope;
        }
    }
}
