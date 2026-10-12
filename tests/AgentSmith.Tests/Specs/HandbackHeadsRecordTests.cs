using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-12-3a36b: the heads stored with a hand-back belong to it — a revision that is still a
/// hand-back keeps them, one that is not clears them — and a branch hand-back with a foreign
/// commit since needs the model even when its set is approved.
/// </summary>
public sealed class HandbackHeadsRecordTests
{
    private const string Key = "azdo-1";
    private static readonly IReadOnlyDictionary<string, string> Heads = new Dictionary<string, string> { ["Server"] = "abc" };

    [Fact]
    public async Task RecordAsync_AHandbackRevision_KeepsTheStoredHeads()
    {
        var pointers = await Seeded();

        await Recorder(pointers).RecordAsync(string.Empty, Server(), Set(handedBack: true), "sha-2", default);

        (await pointers.GetAsync(string.Empty, Key, default))!.HandbackHeads.Should().Equal(Heads);
    }

    [Fact]
    public async Task RecordAsync_ARevisionThatIsNoHandback_ClearsTheStoredHeads()
    {
        var pointers = await Seeded();

        await Recorder(pointers).RecordAsync(string.Empty, Server(), Set(handedBack: false), "sha-2", default);

        (await pointers.GetAsync(string.Empty, Key, default))!.HandbackHeads.Should().BeNull();
    }

    [Fact]
    public void NeedsModel_ApprovedHandbackWithAForeignCommit_IsTrue()
    {
        var approved = Set(handedBack: true) with { Approval = new SpecApproval(DateTimeOffset.UtcNow, "c", "p") };

        SpecAmendmentRule.NeedsModel(SpecRevisionCause.Retrigger, approved).Should().BeFalse();
        SpecAmendmentRule.NeedsModel(SpecRevisionCause.Retrigger, approved, foreignCommitSinceHandback: true)
            .Should().BeTrue();
        SpecAmendmentRule.NeedsModel(SpecRevisionCause.Retrigger,
            Set(handedBack: false) with { Approval = approved.Approval }, foreignCommitSinceHandback: true)
            .Should().BeFalse("only a hand-back expires");
    }

    private static async Task<InMemorySpecSetPointerStore> Seeded()
    {
        var pointers = new InMemorySpecSetPointerStore();
        await pointers.SaveAsync(string.Empty,
            new SpecSetPointer(Key, "Server", "sha-1", 1, HandbackHeads: Heads), default);
        return pointers;
    }

    private static SpecSetPointerRecorder Recorder(InMemorySpecSetPointerStore pointers) =>
        new(pointers, NullLogger<SpecSetPointerRecorder>.Instance);

    private static RepoConnection Server() => new() { Name = "Server" };

    private static SpecSet Set(bool handedBack) => new(
        Key, [], SpecAccounting.Empty, [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
        SpecSource.BranchArtifact,
        handedBack ? new SpecHandback(SpecHandbackCase.RequirementsContradictRepository, "no such field") : null);
}
