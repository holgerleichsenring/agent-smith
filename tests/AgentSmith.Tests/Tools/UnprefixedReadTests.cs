using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// 2026-11-10-14a3b: read_file with a path that names no source, on a host holding several — conversation 43 lost six
/// reads to the refusal although each path existed in exactly one source.
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
    }

    [Fact]
    public async Task ReadFile_UnprefixedPathTwoSourcesHold_NamesBoth()
    {
        var host = Host(("server", "README.md"), ("client", "README.md"));

        var read = await host.ReadFile("README.md");

        read.Should().StartWith("Error: 'README.md' exists in").And.Contain("server").And.Contain("client");
    }

    [Fact]
    public async Task ReadFile_UnprefixedPathNoSourceHolds_KeepsTheRefusal()
    {
        var read = await Host(("server", "a.cs"), ("client", "b.cs")).ReadFile("nowhere.cs");

        read.Should().Contain("must start with a repo name");
    }

    private static FilesystemToolHost Host(params (string Key, string File)[] sources) =>
        new(sources.ToDictionary(s => s.Key, s => (ISandbox)new EvidenceTreeSandbox(
                new Dictionary<string, string> { [s.File] = $"content of {s.File}" })), sources[0].Key);
}
