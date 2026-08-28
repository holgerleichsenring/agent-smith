using AgentSmith.Application.Services.Specs;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0474: a citation is a LIST of whole things.
/// <para>
/// p0473 told the account to join several commands with a semicolon, which is a shell
/// operator: twenty of one live run's commands carried one. The account obeyed, joined two
/// compound commands, and the resolver split the pair into fragments that had never run.
/// The strings here are that run's, anonymised — three attempts at this passed against
/// commands like "grep foo" and failed against the real thing.
/// </para>
/// </summary>
public sealed class CitationListTests
{
    private const string Scan =
        "grep -rn 'MediatR' --include='*.cs' --include='*.csproj' . || true; "
        + "printf '%s\\n' '--- nested projects ---'; "
        + @"find Sample.Server -name '*.csproj' -print -exec cat {} \;";

    private const string TestThenScan =
        "dotnet test Sample.Worker.sln --no-build --no-restore && "
        + "grep -rn 'MediatR' --include='*.cs' --include='*.csproj' . || true";

    private static readonly IReadOnlyList<string> Ran =
    [
        $"Sample.Server: the agent ran '{Scan}' exited 0 — output: PASS",
        $"Sample.Worker: the agent ran '{TestThenScan}' exited 0 — output: PASS",
    ];

    [Fact]
    public void AccountRow_BareCitationString_ReadsAsAOneElementList() =>
        new AccountRow("c", AccountDisposition.Satisfied, Scan).Cited.Should().ContainSingle().Which.Should().Be(Scan);

    [Fact]
    public void AccountRow_CitationsArray_ReadsEveryElement() =>
        new AccountRow("c", AccountDisposition.Satisfied, null, null, [Scan, TestThenScan])
            .Cited.Should().HaveCount(2);

    [Fact]
    public void CitationResolver_TheLiveRefusal_TwoCommandsAsTwoElements_Resolves() =>
        Resolve([Scan, TestThenScan]).IsSatisfied.Should().BeTrue(
            "both commands ran; as two elements there is nothing to split");

    [Fact]
    public void CitationResolver_TwoCommandsJoinedBySemicolonInOneElement_DoesNotResolve() =>
        Resolve([$"{Scan}; {TestThenScan}"]).IsSatisfied.Should().BeFalse(
            "one element is one command, and nothing is split to rescue it");

    [Fact]
    public void CitationResolver_OneElementNamingNothingThatRan_RefusesTheWholeRow() =>
        Resolve([Scan, "dotnet nonsense"]).IsSatisfied.Should().BeFalse(
            "citing one real command and one invented still fails");

    [Fact]
    public void CitationResolver_MixedFileAndCommandElements_Resolve()
    {
        const string diff = """
            diff --git a/src/Sample.cs b/src/Sample.cs
            --- a/src/Sample.cs
            +++ b/src/Sample.cs
            @@ -1,3 +1,4 @@
            +// changed
            """;
        new CitationResolver(CitedFileIndex.FromDiff(diff), Ran)
            .Resolve(new AccountRow("c", AccountDisposition.Satisfied, null, null, ["src/Sample.cs", Scan]))
            .IsSatisfied.Should().BeTrue("a row may cite a file and a command together");
    }

    private static CriterionAccount Resolve(IReadOnlyList<string> citations) =>
        new CitationResolver(CitedFileIndex.FromDiff(string.Empty), Ran)
            .Resolve(new AccountRow("no MediatR references", AccountDisposition.Satisfied, null, null, citations));
}
