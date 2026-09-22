using AgentSmith.Application.Services.PhaseExecution;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Commands;
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
/// <para>
/// 2026-09-22-6ad7: the branch is the only SET, and the reader says WHY it found none — nothing
/// at the path, or something it could not read. The resolver also computes the revision cause,
/// so each case builds the pointer and the pipeline the cause is read from.
/// </para>
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
        new ApprovedSetHandoff(NullLogger<ApprovedSetHandoff>.Instance),
        new FiledTicketSpecGate(NullLogger<FiledTicketSpecGate>.Instance),
        NullLogger<SpecSourceResolver>.Instance);

    [Fact]
    public void SpecSource_BranchArtifactPresent_WinsOverTheTicketDescription()
    {
        var branch = Read(SetOnBranch());

        var decision = _sut.Decide(branch, Ticket(EmbeddedSpec), Pointer(), Resuming(), "azdo-1");

        decision.Source.Should().Be(SpecSource.BranchArtifact);
        decision.Cause.Should().Be(SpecRevisionCause.Resume);
        decision.Set.Should().BeSameAs(branch.Set);
        decision.NeedsModel.Should().BeFalse("a resume works from the artifact, unamended");
    }

    [Fact]
    public void SpecSource_TicketCarriesTheDerivedSpecAsAComment_StillDerivesFromTheBranchArtifact()
    {
        // The ticket's DESCRIPTION is ordinary prose; the spec yaml sits in a COMMENT,
        // which the resolver never reads — the run's own echo is not an input.
        var ticket = Ticket("The endpoint returns 500 on empty payloads.");
        var branch = Read(SetOnBranch());

        var decision = _sut.Decide(branch, ticket, Pointer(), new PipelineContext(), "azdo-1");

        decision.Source.Should().Be(SpecSource.BranchArtifact);
        decision.Cause.Should().Be(SpecRevisionCause.Retrigger);
        decision.NeedsModel.Should().BeTrue(
            "a re-trigger amends the existing set with the new comment instead of re-reading prose");
    }

    [Fact]
    public void SpecSource_ATicketEdit_AmendsTheSetWithTheModel()
    {
        var branch = Read(SetOnBranch() with { TicketFingerprint = "cut-from-other-text" });

        var decision = _sut.Decide(
            branch, Ticket("The endpoint returns 500."), Pointer(), new PipelineContext(), "azdo-1");

        decision.Source.Should().Be(SpecSource.BranchArtifact);
        decision.Cause.Should().Be(SpecRevisionCause.TicketEdit);
        decision.Set.Should().BeSameAs(branch.Set, "the branch set is what the model amends");
        decision.NeedsModel.Should().BeTrue("an edited ticket is input the model has not seen");
    }

    [Fact]
    public void SpecSource_AComment_AmendsTheSetWithTheModel()
    {
        var branch = Read(SetOnBranch());

        var decision = _sut.Decide(
            branch, Ticket("The endpoint returns 500."), Pointer(), Commented(), "azdo-1");

        decision.Source.Should().Be(SpecSource.BranchArtifact);
        decision.Cause.Should().Be(SpecRevisionCause.Comment);
        decision.Set.Should().BeSameAs(branch.Set, "the branch set is what the model amends");
        decision.NeedsModel.Should().BeTrue("a comment is input the model has not seen");
    }

    [Fact]
    public void SpecSource_ARetriggerOfASetInFlight_ContinuesWithoutTheModel()
    {
        var cut = SetOnBranch();
        var inFlight = Read(cut with { Executed = [cut.Phases[0].PhaseId] });

        var decision = _sut.Decide(
            inFlight, Ticket("The endpoint returns 500."), Pointer(), new PipelineContext(), "azdo-1");

        decision.Set.Should().BeSameAs(inFlight.Set);
        decision.NeedsModel.Should().BeFalse(
            "an executed head is work on the branch, and a bare re-trigger brings nothing the model has not seen");
    }

    [Fact]
    public void SpecSource_TicketDescriptionCarriesASpec_SkipsDerivation()
    {
        var decision = _sut.Decide(
            SpecSetOnBranch.Nothing, Ticket(EmbeddedSpec), null, new PipelineContext(), "azdo-1");

        decision.Source.Should().Be(SpecSource.TicketDescription);
        decision.Cause.Should().Be(SpecRevisionCause.Initial);
        decision.NeedsModel.Should().BeFalse("an authored spec is not re-derived");
        decision.Set!.Phases.Should().ContainSingle().Which.PhaseId.Should().Be("p9999");
    }

    [Fact]
    public void SpecSource_OrdinaryTicket_Derives()
    {
        var decision = _sut.Decide(
            SpecSetOnBranch.Nothing, Ticket("Fix the boundary check."), null, new PipelineContext(), "azdo-1");

        decision.Source.Should().Be(SpecSource.Derived);
        decision.NeedsModel.Should().BeTrue();
        decision.Set.Should().BeNull();
    }

    [Fact]
    public void SpecSource_MalformedEmbeddedSpec_FailsInsteadOfSilentlyDeriving()
    {
        var decision = _sut.Decide(
            SpecSetOnBranch.Nothing,
            Ticket("```yaml\nphase: nope\ngoal: 3\n```"),
            null, new PipelineContext(), "azdo-1");

        decision.Error.Should().NotBeNull(
            "shipping a spec and getting it wrong must not degrade into 'no spec, derive one'");
    }

    private static SpecSetOnBranch Read(SpecSet set) =>
        SpecSetOnBranch.Answered(new SpecSetReadResult(set, "sha-1"));

    private static SpecSet SetOnBranch() => new(
        "azdo-1",
        [new SpecPhase(
            new Contracts.Models.PhaseDraft("p0001a", "On the branch", "phase: p0001a", []),
            "on-the-branch", string.Empty, [])],
        SpecAccounting.Empty,
        [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
        SpecSource.BranchArtifact);

    private static SpecSetPointer Pointer() => new("azdo-1", "sample", "sha-1", 1);

    private static PipelineContext Resuming()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResumeCheckpoint, "{}");
        return pipeline;
    }

    private static PipelineContext Commented()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<TicketComment>>(
            ContextKeys.TicketComments,
            [new TicketComment("a.reviewer", DateTimeOffset.UtcNow, "This misses the retry path.")]);
        return pipeline;
    }

    private static Ticket Ticket(string description) => new(
        new TicketId("1"), "A ticket", description, null, "open", "azdo", []);
}
