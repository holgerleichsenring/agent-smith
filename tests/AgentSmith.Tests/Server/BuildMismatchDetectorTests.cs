using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.Startup;
using FluentAssertions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-08-25-8c97: the incident this rule exists for. Two merges shipped on 2026-08-13
/// while version.txt last moved on 08-03 and next moved on 08-17 — so both images reported
/// the same release string and a version comparison would have said MATCH. The revision is
/// what tells two builds of one release apart, and these cases hold that line.
/// </summary>
public sealed class BuildMismatchDetectorTests
{
    private const string ServerRevision = "1111111111111111111111111111111111111111";
    private const string OtherRevision = "2222222222222222222222222222222222222222";
    private const string Release = "0.129.0";

    private readonly MutableTimeProvider _clock = new() { Now = DateTimeOffset.UnixEpoch };

    [Fact]
    public void BuildIdentity_TwoBuildsOfOneRelease_AreDistinguishable()
    {
        var earlier = new BuildIdentity(ServerRevision, Release);
        var later = new BuildIdentity(OtherRevision, Release);

        earlier.Version.Should().Be(later.Version, "release-please moves the version on a "
            + "release commit, and both of these published between two of them");
        earlier.DiffersFrom(later).Should().BeTrue(
            "the revision is the identity — a comparison that only read the version would "
            + "have called these two images the same build");
    }

    [Fact]
    public void Mismatch_IsReportedAsAFinding()
    {
        var findings = AfterTheRolloutWindow().Compare(OtherRevision);

        findings.Should().ContainSingle().Which.Subsystem.Should().Be(StartupSubsystems.Build);
        findings[0].Severity.Should().Be(StartupFindingSeverity.Advisory,
            "coexistence is what an upgrade looks like, not a fault");
        findings[0].IsBlocking.Should().BeFalse();
    }

    [Fact]
    public void Mismatch_NamesBothBuildsAndOffersAReload_WithoutClaimingIncompatibility()
    {
        var reason = AfterTheRolloutWindow().Compare(OtherRevision)[0].Reason;

        reason.Should().Contain(OtherRevision[..12]).And.Contain(ServerRevision[..12]);
        reason.Should().Contain("Reload").And.Contain("different builds");
        reason.ToLowerInvariant().Should().NotContain("incompat",
            "whether two builds can talk is a property of the contract between them, and "
            + "nothing generates that contract from the server yet");
    }

    [Fact]
    public void Match_ProducesNoFinding()
        => AfterTheRolloutWindow().Compare(ServerRevision).Should().BeEmpty();

    [Fact]
    public void Match_IgnoresSurroundingWhitespaceAndCase()
        => AfterTheRolloutWindow().Compare($"  {ServerRevision.ToUpperInvariant()} ")
            .Should().BeEmpty();

    [Fact]
    public void MissingIdentity_IsNotReportedAsAMismatch()
    {
        AfterTheRolloutWindow().Compare(null).Should().BeEmpty("a caller that was never "
            + "stamped says nothing, and silence is not a difference");
        AfterTheRolloutWindow().Compare("").Should().BeEmpty();
        AfterTheRolloutWindow().Compare("   ").Should().BeEmpty();
    }

    [Fact]
    public void MissingIdentity_OnTheServerSide_IsNotReportedAsAMismatch()
    {
        var unstamped = new BuildMismatchDetector(new BuildIdentity(null, null), _clock);
        _clock.Now += BuildMismatchDetector.RolloutWindow * 2;

        unstamped.Compare(OtherRevision).Should().BeEmpty(
            "an image built by hand carries no revision, and every local run would "
            + "otherwise raise a finding about itself");
    }

    [Fact]
    public void Mismatch_WithinTheRolloutWindow_IsNotRaised()
    {
        var detector = Detector();
        _clock.Now += BuildMismatchDetector.RolloutWindow - TimeSpan.FromSeconds(1);

        detector.Compare(OtherRevision).Should().BeEmpty(
            "both halves are separate deployments rolling two replicas each — a difference "
            + "while this process is itself new is the upgrade, not a stale caller");

        _clock.Now += TimeSpan.FromSeconds(2);
        detector.Compare(OtherRevision).Should().ContainSingle(
            "a difference that outlives the rollout is a caller holding a bundle nobody "
            + "replaced, and a reload fixes that");
    }

    private BuildMismatchDetector AfterTheRolloutWindow()
    {
        var detector = Detector();
        _clock.Now += BuildMismatchDetector.RolloutWindow * 2;
        return detector;
    }

    private BuildMismatchDetector Detector() =>
        new(new BuildIdentity(ServerRevision, Release), _clock);

    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
