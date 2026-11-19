using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Tools;

/// <summary>2026-11-15-06fc: read_file and directory_tree on draft: paths read the design turn's draft, not a sandbox.
/// 2026-11-19-b2c1: find_files and grep_in_tree search it too.</summary>
public sealed class FilesystemToolHostDraftReadTests
{
    private static readonly IReadOnlyList<DraftFile> Files =
        [new("epic/p0.yaml", "spec: p0\ngoal: g\n"), new("specs/p1.yaml", "spec: p1\ngoal: one\nrequires: [p0x]\n")];

    [Fact]
    public async Task ReadFile_DraftPath_ReturnsTheFileNumbered()
    {
        var (host, sandbox) = Host();

        (await host.ReadFile("draft:specs/p1.yaml")).Should().Be("1\tspec: p1\n2\tgoal: one\n3\trequires: [p0x]");
        (await host.ReadFile("draft:specs/p1.yaml", start_line: 2, line_count: 1, with_line_numbers: false)).Should().Be("goal: one");
        sandbox.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReadFile_DraftPathOfNoFile_NamesWhatTheDraftHolds()
    {
        var (host, _) = Host();

        (await host.ReadFile("draft:specs/p9.yaml")).Should()
            .Be("Error: draft:specs/p9.yaml is no file of your draft. It holds: draft:epic/p0.yaml, draft:specs/p1.yaml.");
    }

    [Fact]
    public async Task DirectoryTree_Draft_ListsItsFiles()
    {
        var (host, _) = Host();

        (await host.DirectoryTree("draft:")).Should().Be("draft:epic/p0.yaml\ndraft:specs/p1.yaml");
        (await host.DirectoryTree("draft:specs")).Should().Be("draft:specs/p1.yaml");
    }

    [Fact]
    public async Task FindFiles_Draft_MatchesItsPaths()
    {
        var (host, sandbox) = Host();

        (await host.FindFiles("*.yaml", "draft:")).Should().Be("draft:epic/p0.yaml\ndraft:specs/p1.yaml");
        (await host.FindFiles("p1", "draft:specs")).Should().Be("draft:specs/p1.yaml");
        (await host.FindFiles("*.md", "draft:")).Should().StartWith("No file of the draft under draft:");
        sandbox.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GrepInTree_Draft_FindsLinesFilesAndCounts()
    {
        var (host, sandbox) = Host();

        (await host.GrepInTree("^goal", "draft:")).Should().Be("draft:epic/p0.yaml:2:goal: g\ndraft:specs/p1.yaml:2:goal: one");
        (await host.GrepInTree("requires", "draft:", output_mode: "files_with_matches")).Should().Be("draft:specs/p1.yaml");
        (await host.GrepInTree("p", "draft:specs", output_mode: "count")).Should().Be("draft:specs/p1.yaml:2");
        (await host.GrepInTree("[", "draft:")).Should().StartWith("Error: '[' is not a valid regular expression");
        sandbox.VerifyNoOtherCalls();
    }

    private static (FilesystemToolHost Host, Mock<ISandbox> Sandbox) Host()
    {
        var sandbox = new Mock<ISandbox>();
        return (new FilesystemToolHost(sandbox.Object) { DraftReader = () => Files }, sandbox);
    }
}
