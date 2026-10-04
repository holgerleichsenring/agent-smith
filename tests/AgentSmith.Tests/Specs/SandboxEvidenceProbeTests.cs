using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-02-3f06c: a design turn's sandboxes as an evidence probe — routed as the tools route a
/// path, a whole file read and counted, and every uncertain answer left unchecked.
/// </summary>
public sealed class SandboxEvidenceProbeTests
{
    [Fact]
    public async Task SandboxEvidenceProbe_PrefixedPath_ResolvesInThatRepository()
    {
        var api = Tree(("src/a.cs", "1\n2\n3\n"));
        var web = Tree(("src/a.cs", "1\n"));
        var probe = Probe(("team/api", api), ("team/web", web));

        var result = await probe.ProbeAsync(null, "team/api/src/a.cs", CancellationToken.None);

        result.Should().Be(new EvidenceProbeResult.File(3));
        web.Ran.Should().BeEmpty("the longest repository name the path starts with is the one read");
    }

    [Fact]
    public async Task SandboxEvidenceProbe_SingleRepoFolderNamedLikeTheRepo_IsNotMissing()
    {
        var probe = Probe(("api", Tree(("api/notes.md", "one\n"))));

        var result = await probe.ProbeAsync(null, "api/notes.md", CancellationToken.None);

        result.Should().Be(new EvidenceProbeResult.File(1),
            "stripped it is missing, unstripped it is a file — a file found either way stands");
    }

    [Fact]
    public async Task SandboxEvidenceProbe_LinePastTheEnd_IsAProblem()
    {
        var probe = Probe(("api", Tree(("src/a.cs", "1\n2\n3\n"))));

        var problems = await new EvidenceCheck(new EvidenceReferences())
            .CheckAsync("api/src/a.cs:2-4", EvidencePolicy.Product, probe, CancellationToken.None);

        problems.Should().ContainSingle().Which.Should().Be(
            new EvidenceProblem("past the end of a 3-line file", "api/src/a.cs:2-4"));
    }

    [Fact]
    public async Task SandboxEvidenceProbe_UnmaterialisedScope_IsNotCheckedAndNotStepped()
    {
        var closed = new EvidenceTreeSandbox(new Dictionary<string, string>(), materialized: false);
        var probe = Probe(("api", closed));

        var result = await probe.ProbeAsync(null, "src/missing.cs", CancellationToken.None);

        result.Should().BeOfType<EvidenceProbeResult.NotChecked>();
        closed.Ran.Should().BeEmpty("a scope the turn never opened is not cloned for a check");
    }

    [Fact]
    public async Task SandboxEvidenceProbe_TrailingNewline_IsNotALine()
    {
        var probe = Probe(("api", Tree(("src/a.cs", "a\nb\n"))));

        var result = await probe.ProbeAsync(null, "src/a.cs", CancellationToken.None);

        result.Should().Be(new EvidenceProbeResult.File(2));
    }

    [Fact]
    public async Task SandboxEvidenceProbe_BarePathInTwoRepositories_IsNotChecked()
    {
        var probe = Probe(("api", Tree(("src/a.cs", "1\n"))), ("web", Tree(("src/a.cs", "1\n2\n"))));

        var result = await probe.ProbeAsync(null, "src/a.cs", CancellationToken.None);

        result.Should().BeOfType<EvidenceProbeResult.NotChecked>("which repository was meant is a guess");
    }

    [Fact]
    public async Task SandboxEvidenceProbe_SameFileTwice_IsReadOnceAndReported()
    {
        var accessor = TurnActivityRecorder.Silent();
        var recorder = new TurnActivityRecorder();
        using var observing = accessor.Observe(recorder);
        var api = Tree(("src/a.cs", "1\n"));
        var probe = new SandboxEvidenceProbe(new Dictionary<string, ISandbox> { ["api"] = api }, accessor);

        await probe.ProbeAsync(null, "src/a.cs", CancellationToken.None);
        await probe.ProbeAsync(null, "api/src/a.cs", CancellationToken.None);

        api.Ran.Should().ContainSingle("'api/src/a.cs' strips to the cached 'src/a.cs', a file already");
        recorder.Lines.Should().Equal("tool read_file api/src/a.cs");
    }

    [Fact]
    public async Task SandboxEvidenceProbe_QualifiedPath_IsNotChecked()
    {
        var api = Tree(("src/a.cs", "1\n"));

        var result = await Probe(("api", api)).ProbeAsync("template", "src/a.cs", CancellationToken.None);

        result.Should().BeOfType<EvidenceProbeResult.NotChecked>();
        api.Ran.Should().BeEmpty();
    }

    private static EvidenceTreeSandbox Tree(params (string Path, string Content)[] files) =>
        new(files.ToDictionary(f => f.Path, f => f.Content, StringComparer.Ordinal));

    private static SandboxEvidenceProbe Probe(params (string Name, ISandbox Sandbox)[] repositories) =>
        new(repositories.ToDictionary(r => r.Name, r => r.Sandbox, StringComparer.Ordinal),
            TurnActivityRecorder.Silent());
}
