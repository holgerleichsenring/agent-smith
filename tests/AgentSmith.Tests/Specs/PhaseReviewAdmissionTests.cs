using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-17-042eh: a phase-review finding is kept only when it rests on a READ the reviewer
/// itself took of exactly that repository and that path, with the line inside what the read
/// returned and the path inside the diff the review was shown. Each test removes exactly one
/// of those and watches the finding go.
/// </summary>
public sealed class PhaseReviewAdmissionTests
{
    private const string Repo = "api";
    private const string Path = "src/Api/Handler.cs";

    [Fact]
    public void PhaseReview_FindingOnItsOwnReadOfThePath_IsKept()
    {
        var look = Look();
        var id = look.Evidence.Remember(Read(Path, lines: 80));

        Admit(Finding(cites: id), look).Should().ContainSingle();
    }

    [Fact]
    public void PhaseReview_FindingOnAReadOfAnotherPath_IsDiscarded()
    {
        var look = Look();
        var id = look.Evidence.Remember(Read("src/Api/Other.cs", lines: 80));

        Admit(Finding(cites: id), look).Should().BeEmpty(
            "the id is real and names a file the finding is not about — a plausible path "
            + "attached to a real citation is the fabrication the rule exists to stop");
    }

    [Fact]
    public void PhaseReview_FindingOnAReadThatExited1_IsDiscarded()
    {
        var look = Look();
        var logger = new CapturingLogger<PhaseReviewAdmissionTests>();
        var id = look.Evidence.Remember(
            new EvidenceRecord(Repo, EvidenceRecord.Read, $"read {Path}", 1, Ran: true, Path, 40));

        new PhaseReviewAdmission(logger).Admit([Finding(cites: id)], [Diff()], look)
            .Should().BeEmpty("a read that found no file shows nothing");
        logger.Warnings.Should().ContainSingle().Which.Should().Contain("no read of its own",
            "the exit is the refusal — an absent file's read carries no content to be right about");
    }

    [Fact]
    public void PhaseReview_FindingOnASearchOnly_IsDiscarded()
    {
        var look = Look();
        var logger = new CapturingLogger<PhaseReviewAdmissionTests>();
        var id = look.Evidence.Remember(
            new EvidenceRecord(Repo, EvidenceRecord.Search, $"grep -E 'x' {Path}", 0, Ran: true, Path, 0));

        new PhaseReviewAdmission(logger).Admit([Finding(cites: id)], [Diff()], look)
            .Should().BeEmpty("a search says a string is somewhere; it is not having seen the file");
        logger.Warnings.Should().ContainSingle().Which.Should().Contain("no read of its own",
            "the search is refused as NOT A READ — not incidentally, for carrying no line count");
    }

    [Fact]
    public void PhaseReview_FindingCitingAnIdTheDeriverMinted_IsDiscarded()
    {
        var other = new DerivationEvidence();
        var mine = Look();
        other.Remember(Read(Path, lines: 80));

        Admit(Finding(cites: "L1"), mine).Should().BeEmpty(
            "L1 is a look somebody else took — only this reviewer's own evidence admits");
    }

    [Fact]
    public void PhaseReview_FindingOutsideTheReviewedDiff_IsDiscarded()
    {
        var look = Look();
        var id = look.Evidence.Remember(Read("src/Api/Untouched.cs", lines: 80));

        Admit(Finding(path: "src/Api/Untouched.cs", cites: id), look).Should().BeEmpty(
            "a file this phase did not change is not this phase's to answer for");
    }

    [Fact]
    public void PhaseReview_FindingPastTheLinesReturned_IsDiscarded()
    {
        var look = Look();
        var id = look.Evidence.Remember(Read(Path, lines: 12));

        Admit(Finding(line: 40, cites: id), look).Should().BeEmpty(
            "line 40 of a read that returned twelve lines is a number nobody can check");
    }

    [Fact]
    public void PhaseReview_FindingWithNoCitation_IsDiscarded()
    {
        Admit(Finding(cites: null), Look()).Should().BeEmpty();
    }

    private static IReadOnlyList<PhaseFinding> Admit(PhaseFinding finding, DerivationLook look) =>
        new PhaseReviewAdmission(NullLogger.Instance).Admit([finding], [Diff()], look);

    private static PhaseDiff Diff() =>
        new(Repo, "diff --git a/" + Path + " b/" + Path, [Path], [], "the commit this phase started at");

    private static PhaseFinding Finding(string? path = null, int line = 4, string? cites = "P1") =>
        new(Repo, path ?? Path, line, "files stay under 120 lines", "this one is 400", cites);

    private static EvidenceRecord Read(string path, int lines) =>
        new(Repo, EvidenceRecord.Read, $"read {path}", 0, Ran: true, path, lines);

    private static DerivationLook Look() => new(
        new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [Repo] = new StubSandbox() },
        new StubSandboxFileReaderFactory(), new NoEcosystems(), NullLogger.Instance,
        templates: null, DerivationLookTerms.PhaseReview);

    private sealed class NoEcosystems : IPackageEcosystemDetector
    {
        public Task<AgentSmith.Contracts.Models.PackageEcosystem?> DetectAsync(
            ISandboxFileReader reader, string repoPath, CancellationToken cancellationToken) =>
            Task.FromResult<AgentSmith.Contracts.Models.PackageEcosystem?>(null);
    }
}
