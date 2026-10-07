using AgentSmith.Application.Services.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-02-3f06b: strict resolution under a policy — a missing file, a line past the end and a
/// directory are problems either way; what a caller cannot see is its policy's to call.
/// </summary>
public sealed class EvidenceCheckTests
{
    private readonly EvidenceCheck _check = new(new EvidenceReferences());

    private readonly MapProbe _probe = new(new Dictionary<string, EvidenceProbeResult>
    {
        ["src/a.cs"] = new EvidenceProbeResult.File(10),
        ["src/dir"] = new EvidenceProbeResult.NotAFile(),
    });

    [Fact]
    public async Task EvidenceCheck_PathWithMalformedLines_IsAProblem() =>
        (await Check("src/a.cs:12a")).Should().ContainSingle().Which.Reason.Should().Contain("do not parse");

    [Fact]
    public async Task EvidenceCheck_HostShapedMissingPath_IsNotAReference() =>
        (await Check("ghcr.io/owner/image; mcr.microsoft.com/dotnet/sdk:10.0; src/a.cs:1")).Should().BeEmpty();

    [Fact]
    public async Task EvidenceCheck_NotAFile_IsAProblem() =>
        (await Check("src/dir")).Should().ContainSingle().Which.Path.Should().Be("src/dir");

    [Fact]
    public async Task EvidenceCheck_LinePastTheEnd_LineZero_DescendingRange_AreProblems()
    {
        var problems = await Check("src/a.cs:11; src/a.cs:0; src/a.cs:7-3; src/a.cs:10");

        problems.Select(p => p.Path).Should().Equal("src/a.cs:11", "src/a.cs:0", "src/a.cs:7-3");
    }

    [Fact]
    public async Task EvidenceCheck_OverflowingLineNumber_IsAProblemNotAThrow() =>
        (await Check("src/a.cs:99999999999")).Should().ContainSingle().Which.Reason.Should().Contain("do not parse");

    [Theory]
    [InlineData("observed: a scan of the tree")]
    [InlineData("observed: a scan of the tree, 2026-02-30")]
    public async Task EvidenceCheck_ObservedWithoutOrWithInvalidDate_IsAProblem(string evidence) =>
        (await Check(evidence)).Should().ContainSingle();

    [Fact]
    public async Task EvidenceCheck_ObservedWithADate_Stands() =>
        (await Check("observed: a scan of the tree, 2026-10-02")).Should().BeEmpty();

    [Fact]
    public async Task EvidenceCheck_MintedUnderRepositoryPolicy_IsAProblem()
    {
        (await Check("[L3] api: ran 'ls' exited 0")).Should().ContainSingle();
        (await Check("[L3] api: ran 'ls' exited 0", EvidencePolicy.Repository with { AllowMinted = true }))
            .Should().BeEmpty("the product's policy accepts the derivation's own look lines");
    }

    [Fact]
    public async Task EvidenceCheck_DeclaredQualifier_IsAcceptedUnchecked()
    {
        (await Check("spec-first:skills/x/SKILL.md:900")).Should().BeEmpty();
        _probe.Probed.Should().BeEmpty("a declared qualifier is accepted without resolving");
        (await Check("elsewhere:skills/x/SKILL.md:1")).Should().ContainSingle().Which.Reason.Should().Contain("elsewhere");
    }

    [Fact]
    public async Task EvidenceCheck_PlannedPhasePath_IsRefused() =>
        (await Check(".agentsmith/specs/planned/2026-10-02-0000-x.yaml"))
            .Should().ContainSingle().Which.Reason.Should().Contain("a plan is not evidence");

    /// <summary>2026-10-06-03c7b: the plan directories moved with the noun; the rule moved with them.</summary>
    [Fact]
    public void EvidencePolicy_Repository_RefusesSpecsPlannedAndActive() =>
        EvidencePolicy.Repository.RefusedPathPrefixes
            .Should().Equal(".agentsmith/specs/planned/", ".agentsmith/specs/active/");

    [Fact]
    public async Task EvidenceCheck_NoReference_IsAProblem() =>
        (await Check("the code says so")).Should().ContainSingle().Which.Reason.Should().Contain("cites no path");

    private Task<IReadOnlyList<EvidenceProblem>> Check(string evidence, EvidencePolicy? policy = null) =>
        _check.CheckAsync(evidence, policy ?? EvidencePolicy.Repository, _probe, CancellationToken.None);

    private sealed class MapProbe(IReadOnlyDictionary<string, EvidenceProbeResult> files) : IEvidenceProbe
    {
        public List<string> Probed { get; } = [];

        public Task<EvidenceProbeResult> ProbeAsync(string? qualifier, string path, CancellationToken ct)
        {
            Probed.Add(path);
            return Task.FromResult(files.TryGetValue(path, out var result) ? result : new EvidenceProbeResult.NotAFile());
        }
    }
}
