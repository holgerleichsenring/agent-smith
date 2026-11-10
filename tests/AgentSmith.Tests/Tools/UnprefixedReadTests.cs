using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// 2026-11-10-14a3b: read_file with a path that names no source, on a design turn's host holding several — conversation
/// 43 lost six reads to the refusal although each path existed in exactly one source. 2026-11-10-85e2: a design turn's
/// host only, over sources already open, never a template, and the resolved read is recorded like any other.
/// </summary>
public sealed class UnprefixedReadTests
{
    [Fact]
    public async Task ReadFile_UnprefixedPathOneSourceHolds_ReadsItAndNamesTheSource()
    {
        var host = Host(("server", "src/Program.cs"), ("reference:req", "requirements 1/spec-doc/prd.md"));

        var read = await host.ReadFile("requirements 1/spec-doc/prd.md");

        read.Should().StartWith("[read from reference:req/requirements 1/spec-doc/prd.md")
            .And.Contain("content of requirements 1/spec-doc/prd.md");
        host.ReadPaths.Should().Contain("reference:req/requirements 1/spec-doc/prd.md",
            "a resolved read counts as a read wherever reads are recorded");
    }

    [Fact]
    public async Task ReadFile_UnprefixedPathTwoSourcesHold_NamesBoth()
    {
        var read = await Host(("server", "README.md"), ("client", "README.md")).ReadFile("README.md");

        read.Should().StartWith("Error: 'README.md' exists in").And.Contain("server").And.Contain("client");
    }

    [Fact]
    public async Task ReadFile_UnprefixedPathNoSourceHolds_KeepsTheRefusal()
    {
        var read = await Host(("server", "a.cs"), ("client", "b.cs")).ReadFile("nowhere.cs");

        read.Should().Contain("must start with a repo name");
    }

    [Fact]
    public async Task ReadFile_UnprefixedPathOverUnopenedSources_OpensNothing()
    {
        var closed = new EvidenceTreeSandbox(new Dictionary<string, string> { ["a.cs"] = "x" }, materialized: false);
        var template = new EvidenceTreeSandbox(new Dictionary<string, string> { ["a.cs"] = "x" });
        var host = new FilesystemToolHost(new Dictionary<string, ISandbox>
        {
            ["server"] = closed, ["template:base"] = template,
        }, "server") { ResolvesUnprefixedReads = true };

        var read = await host.ReadFile("a.cs");

        read.Should().Contain("must start with a repo name");
        closed.Ran.Should().BeEmpty("a lazy repository is not cloned to resolve a path");
        template.Ran.Should().BeEmpty("a template is never where a read resolves");
    }

    [Fact]
    public async Task ReadFile_UnprefixedPathOnACodingHost_KeepsTheRefusal()
    {
        var host = new FilesystemToolHost(new Dictionary<string, ISandbox>
        {
            ["server"] = Tree("a.cs"), ["client"] = Tree("b.cs"),
        }, "server");

        var read = await host.ReadFile("a.cs");

        read.Should().Contain("must start with a repo name", "only a design turn resolves an unprefixed path");
        host.ReadPaths.Should().BeEmpty();
    }

    private static EvidenceTreeSandbox Tree(string file) =>
        new(new Dictionary<string, string> { [file] = $"content of {file}" });

    private static FilesystemToolHost Host(params (string Key, string File)[] sources) =>
        new(sources.ToDictionary(s => s.Key, s => (ISandbox)Tree(s.File)), sources[0].Key) { ResolvesUnprefixedReads = true };
}
