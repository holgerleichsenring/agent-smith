using System;
using System.Collections.Generic;
using AgentSmith.Application.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

// p0384: per-repo delivery gate — every repo the scope classifier expects to
// CHANGE must show a committed diff, named in the failure. Absent/empty
// expected_changes preserves the anyCode semantics exactly (fail-open), which
// is what let ticket #19106 go green nine times while an in-scope repo was
// never worked.
public sealed class RunOutcomeKeystonePerRepoTests
{
    private static MasterVerification Green => new(VerificationStatus.Green, true, true, true, true, "ok");

    private static KeystoneVerdict Evaluate(
        IReadOnlyDictionary<string, bool>? perRepo, IReadOnlyList<string>? expected) =>
        RunOutcomeKeystone.Evaluate(
            expectsCodeChanges: true, expectsGreenTests: true,
            gitCommittedChange: true, recordedChange: true, verification: Green,
            ratifiedCriteria: Array.Empty<string>(),
            perRepoCommittedChange: perRepo, expectedChangeRepos: expected);

    [Fact]
    public void Keystone_ExpectedChangeRepoWithoutDiff_RunFailed_RepoNamed()
    {
        var perRepo = new Dictionary<string, bool> { ["server"] = true, ["client"] = false };

        var verdict = Evaluate(perRepo, ["server", "client"]);

        verdict.Satisfied.Should().BeFalse();
        verdict.FailureReason.Should().Contain("client");
        verdict.FailureReason.Should().NotContain("[server");
    }

    [Fact]
    public void Keystone_NoExpectedChanges_AnyCodePreserved()
    {
        // One repo changed, one did not — with no expected_changes the anyCode
        // semantics hold exactly as before p0384.
        var perRepo = new Dictionary<string, bool> { ["server"] = true, ["client"] = false };

        Evaluate(perRepo, expected: null).Satisfied.Should().BeTrue();
        Evaluate(perRepo, expected: []).Satisfied.Should().BeTrue();
    }

    [Fact]
    public void Keystone_AllExpectedChangeReposHaveDiffs_Green()
    {
        var perRepo = new Dictionary<string, bool> { ["server"] = true, ["client"] = true };

        Evaluate(perRepo, ["server", "client"]).Satisfied.Should().BeTrue();
    }

    [Fact]
    public void Keystone_ExpectedChangeRepoMissingFromStagedMap_CountsAsUnchanged()
    {
        // The commit step never even staged the repo — the classifier expected a
        // change there, so the run is a partial delivery.
        var perRepo = new Dictionary<string, bool> { ["server"] = true };

        var verdict = Evaluate(perRepo, ["server", "client"]);

        verdict.Satisfied.Should().BeFalse();
        verdict.FailureReason.Should().Contain("client");
    }

    [Fact]
    public void Keystone_ExpectedChangesWithoutPerRepoTruth_FailsOpen()
    {
        // Callers without git truth (the early result.md verdict) pass no map —
        // the per-repo gate must not fire on missing data.
        Evaluate(perRepo: null, ["server"]).Satisfied.Should().BeTrue();
    }
}
