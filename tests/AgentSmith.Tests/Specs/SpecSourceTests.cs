using AgentSmith.Application.Services.PhaseExecution;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0393a: the fixed source precedence — branch artifact, then a spec embedded in the
/// ticket DESCRIPTION, then derivation. A ticket COMMENT is never a source: after the
/// first run the ticket carries the derived spec as a comment, so a rule reading "a
/// ticket carrying a spec skips derivation" would feed the run its own echo.
/// </summary>
public sealed class SpecSourceTests
{
    private const string EmbeddedSpec = """
        Please add the widget endpoint.

        ```yaml
        phase: p9999
        goal: "Add a widget endpoint to the sample service"
        steps:
          - id: impl
            action: "Add the widget endpoint + handler"
        done:
          - "GET /widget returns the widget"
        ```
        """;

    private readonly SpecSourceResolver _sut = new(
        new PhaseSpecFromTicket(
            new SpecDraftValidator(new PhaseSpecSchemaProvider()), new PhaseDraftReader()),
        NullLogger<SpecSourceResolver>.Instance);

    [Fact]
    public void SpecSource_BranchArtifactPresent_WinsOverTheTicketDescription()
    {
        var branch = new SpecSetReadResult(SetOnBranch(), "sha-1");

        var decision = _sut.Decide(branch, Ticket(EmbeddedSpec), SpecRevisionCause.Resume, "azdo-1");

        decision.Source.Should().Be(SpecSource.BranchArtifact);
        decision.Set.Should().BeSameAs(branch.Set);
        decision.NeedsModel.Should().BeFalse("a resume works from the artifact, unamended");
    }

    [Fact]
    public void SpecSource_TicketCarriesTheDerivedSpecAsAComment_StillDerivesFromTheBranchArtifact()
    {
        // The ticket's DESCRIPTION is ordinary prose; the spec yaml sits in a COMMENT,
        // which the resolver never reads — the run's own echo is not an input.
        var ticket = Ticket("The endpoint returns 500 on empty payloads.");
        var branch = new SpecSetReadResult(SetOnBranch(), "sha-1");

        var decision = _sut.Decide(branch, ticket, SpecRevisionCause.Retrigger, "azdo-1");

        decision.Source.Should().Be(SpecSource.BranchArtifact);
        decision.NeedsModel.Should().BeTrue(
            "a re-trigger amends the existing set with the new comment instead of re-reading prose");
    }

    [Fact]
    public void SpecSource_TicketDescriptionCarriesASpec_SkipsDerivation()
    {
        var decision = _sut.Decide(
            branchArtifact: null, Ticket(EmbeddedSpec), SpecRevisionCause.Initial, "azdo-1");

        decision.Source.Should().Be(SpecSource.TicketDescription);
        decision.NeedsModel.Should().BeFalse("an authored spec is not re-derived");
        decision.Set!.Phases.Should().ContainSingle().Which.PhaseId.Should().Be("p9999");
    }

    [Fact]
    public void SpecSource_OrdinaryTicket_Derives()
    {
        var decision = _sut.Decide(
            branchArtifact: null, Ticket("Fix the boundary check."), SpecRevisionCause.Initial, "azdo-1");

        decision.Source.Should().Be(SpecSource.Derived);
        decision.NeedsModel.Should().BeTrue();
        decision.Set.Should().BeNull();
    }

    [Fact]
    public void SpecSource_MalformedEmbeddedSpec_FailsInsteadOfSilentlyDeriving()
    {
        var decision = _sut.Decide(
            branchArtifact: null,
            Ticket("```yaml\nphase: nope\ngoal: 3\n```"),
            SpecRevisionCause.Initial, "azdo-1");

        decision.Error.Should().NotBeNull(
            "shipping a spec and getting it wrong must not degrade into 'no spec, derive one'");
    }

    private static SpecSet SetOnBranch() => new(
        "azdo-1",
        [new SpecPhase(
            new Contracts.Models.PhaseDraft("p0001a", "On the branch", "phase: p0001a", []),
            "on-the-branch", string.Empty, [])],
        SpecAccounting.Empty,
        [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
        SpecSource.BranchArtifact);

    private static Ticket Ticket(string description) => new(
        new TicketId("1"), "A ticket", description, null, "open", "azdo", []);
}
