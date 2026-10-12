using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.MultiRepo;

/// <summary>
/// 2026-10-12-f5f7b: a reviewer's edit is any repository of the series that moved since this system
/// wrote it; a pointer written before the per-repository map reads as {CarryingRepo: RevisionSha}.
/// </summary>
public sealed class SeriesShaEdgesTests
{
    [Fact]
    public void SpecRevisionCause_LegacyRowUnchangedBranch_IsRetrigger() =>
        SpecRevisionCause.For(Read(("primary", "sha-1")), new SpecSetPointer("azdo-1", "primary", "sha-1", 1),
                Ticket(), new PipelineContext())
            .Should().Be(SpecRevisionCause.Retrigger, "a row without the map back-fills the one sha it holds");

    [Fact]
    public void SpecRevisionCause_OneRepoMoved_IsReviewerEdit() =>
        SpecRevisionCause.For(Read(("Server", "s1"), ("Client", "c2")), Pointer(("Server", "s1"), ("Client", "c1")),
                Ticket(), new PipelineContext())
            .Should().Be(SpecRevisionCause.ReviewerEdit);

    [Fact]
    public void Moved_MemberReadButAbsentFromTheMap_HasMoved() =>
        SeriesShaEdges.Moved(Read(("Server", "s1"), ("Client", "c1")), Pointer(("Server", "s1")))
            .Should().BeTrue("a repository the map never recorded holds a sub-series nobody here wrote");

    [Fact]
    public void Moved_MapRepoNotReached_IsNotComparedAndNamed()
    {
        var read = Read(("Server", "s1"));
        var pointer = Pointer(("Server", "s1"), ("Client", "c1"));

        SeriesShaEdges.Moved(read, pointer).Should().BeFalse();
        SeriesShaEdges.NotCompared(read, pointer).Should().Equal("Client");
    }

    [Fact]
    public void Moved_ReadWithoutShaMap_ComparesTheOneSha()
    {
        var read = new SpecSetReadResult(Set(), "sha-1");

        SeriesShaEdges.Moved(read, new SpecSetPointer("azdo-1", "primary", "sha-1", 1)).Should().BeFalse();
        SeriesShaEdges.Moved(read, new SpecSetPointer("azdo-1", "primary", "sha-2", 1)).Should().BeTrue();
        SeriesShaEdges.NotCompared(read, new SpecSetPointer("azdo-1", "primary", "sha-2", 1)).Should().BeEmpty();
    }

    [Fact]
    public void ShaByRepo_WithoutAMap_BackFillsTheCarryingRepository() =>
        new SpecSetPointer("azdo-1", "primary", "sha-1", 1).ShaByRepo
            .Should().BeEquivalentTo(new Dictionary<string, string> { ["primary"] = "sha-1" });

    private static SpecSetReadResult Read(params (string Repo, string Sha)[] shas) =>
        new(Set(), shas[0].Sha) { RevisionShas = shas.ToDictionary(s => s.Repo, s => s.Sha) };

    private static SpecSetPointer Pointer(params (string Repo, string Sha)[] shas) =>
        new("azdo-1", shas[0].Repo, shas[0].Sha, 1, RevisionShas: shas.ToDictionary(s => s.Repo, s => s.Sha));

    private static SpecSet Set() => new(
        "azdo-1", [new DerivedSpec(new SpecDraft("x1a", "g", "spec: x1a", []), "x1a", string.Empty, [])],
        SpecAccounting.Empty, [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
        SpecSource.BranchArtifact);

    private static Ticket Ticket() =>
        new(new TicketId("1"), "Title", "Body", null, "open", "azdo", []);
}
