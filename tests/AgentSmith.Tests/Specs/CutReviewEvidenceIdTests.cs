using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using FluentAssertions;
using static AgentSmith.Tests.Specs.DerivationTestLooks;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-15-ffa7: a look has a holder — its own allowance, its own id letter, its own
/// name on the lines it mints — and the reviewer's look opens no template.
/// </summary>
public sealed class CutReviewEvidenceIdTests
{
    [Fact]
    public void Budget_TwoLooksWithDifferentAllowances_RefuseAtTheirOwn()
    {
        var derivation = new DerivationLookBudget(DerivationLookTerms.Derivation);
        var review = new DerivationLookBudget(DerivationLookTerms.CutReview);

        Taken(derivation).Should().Be(DerivationLookTerms.DerivationAllowance);
        Taken(review).Should().Be(DerivationLookTerms.CutReviewAllowance);
        review.Exhausted.Should().StartWith("No look left — a cut review may take 6.");
        derivation.Exhausted.Should().StartWith("No look left — a derivation may take 12. Write the work order");
    }

    [Fact]
    public void ReviewerLook_OverAProjectWithTemplates_CreatesNoTemplateScope()
    {
        var scopes = new CountingScopes();
        var pipeline = PipelineWithTemplate();

        var review = Factory(scopes: scopes).ForCutReview(pipeline);

        scopes.Created.Should().Be(0, "a second look must not clone every template again");
        review!.Templates.Should().BeEmpty();
        review.Repositories.Should().Equal(Repo);
        review.Terms.Should().Be(DerivationLookTerms.CutReview);
        Factory(scopes: scopes).Create(pipeline)!.Templates.Should().ContainSingle(
            "the derivation's own look still opens the declared template");
    }

    [Fact]
    public void Evidence_ReviewerAndDeriver_MintDisjointIds()
    {
        var deriver = new DerivationEvidence();
        var reviewer = new DerivationEvidence("R", "the cut review");

        var ids = new[] { deriver.Remember(Repo, "read a", 0, true), reviewer.Remember(Repo, "read a", 0, true) };

        ids.Should().Equal("L1", "R1");
        DerivationEvidence.IndexById([.. deriver.Lines, .. reviewer.Lines]).Keys.Should().HaveCount(2);
    }

    [Fact]
    public void Evidence_MintedLine_NamesItsActor()
    {
        var reviewer = new DerivationEvidence("R", "the cut review");

        reviewer.Remember(Repo, "read a", 1, ran: true);

        reviewer.Lines.Single().Should().Be($"[R1] {Repo}: the cut review ran 'read a' exited 1");
    }

    [Fact]
    public void Rejection_FalsePremise_CarriesTheMintedEvidenceLine()
    {
        var line = $"[R1] {Repo}: the cut review ran 'grep -E 'NewBus' .' exited 1";
        var review = new SpecCutReview([new CutFinding(
            "p1a", "the senders already reference the new bus", SpecCutVerdicts.FalsePremise,
            "no reference exists", Cites: "[r1]")]);

        var rejection = SpecCutRejection.For(review, [line]);

        rejection.Should().Contain("p1a").And.Contain("false-premise").And.EndWith($"(evidence: {line})");
    }

    private static int Taken(DerivationLookBudget budget)
    {
        var taken = 0;
        while (budget.TryTake()) taken++;
        return taken;
    }

    private static PipelineContext PipelineWithTemplate()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandboxes, (IReadOnlyDictionary<string, ISandbox>)
            new Dictionary<string, ISandbox> { [Repo] = new CountingSandbox(0) });
        pipeline.Set(ContextKeys.SandboxDiscoveries,
            (IReadOnlyDictionary<string, RemoteContextDiscovery>)new Dictionary<string, RemoteContextDiscovery>());
        pipeline.Set(ContextKeys.ProjectConfig, Handlers.CodingMasterTemplateTests.ProjectWithTemplate());
        return pipeline;
    }

    private sealed class CountingScopes : ISourceScopeSandboxFactory
    {
        public int Created { get; private set; }

        public ISourceScopeSandbox Create(ResolvedProject project, RepoConnection repo, string? revision = null)
        {
            Created++;
            return new Handlers.CodingMasterTemplateTests.RecordingScope();
        }
    }
}
