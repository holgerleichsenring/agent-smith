using System.Text.RegularExpressions;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-11-17-a0b6c: the ticket branch is the only spec text. A backend file that reads an approval record and
/// touches its set's specs or goal is a second reader of a copy, unless it is the write that puts the set on a
/// branch — those are listed with their reason in approval-set-readers-baseline.tsv. No file may join; a file that
/// stops reading the set must leave.
/// </summary>
public sealed partial class ApprovalSetReadersRatchetTests
{
    private const string BaselineFile = "approval-set-readers-baseline.tsv";

    [Fact]
    public void ApprovalSet_ANewReader_IsRefused()
    {
        var baseline = Baseline();
        var offenders = Readers().Where(r => !baseline.ContainsKey(r)).ToList();

        offenders.Should().BeEmpty(
            "spec text is read off the ticket branch through TicketBranchSpecs (server) or TicketBranchSeries "
            + "(application), never out of the approval record; do not add a baseline row.\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void ApprovalSet_AFileThatStopped_LeavesTheBaseline()
    {
        var readers = Readers().ToHashSet(StringComparer.Ordinal);
        var stale = Baseline().Keys.Where(k => !readers.Contains(k)).ToList();

        stale.Should().BeEmpty($"a file that no longer reads the record's set must leave {BaselineFile}.\n  " + string.Join("\n  ", stale));
    }

    [Fact]
    public void ApprovalSet_EveryListedFile_SaysWhy()
    {
        Baseline().Where(row => string.IsNullOrWhiteSpace(row.Value)).Select(row => row.Key)
            .Should().BeEmpty("each listed reader names the write it feeds or the block that retires it");
    }

    private static IEnumerable<string> Readers() =>
        ArchitectureSources.HandWrittenBackendFiles()
            .Where(p => File.ReadAllText(p) is var text && ApprovalRecord().IsMatch(text) && SetText().IsMatch(text))
            .Select(p => Path.GetRelativePath(ArchitectureSources.BackendRoot, p).Replace('\\', '/'));

    private static IReadOnlyDictionary<string, string> Baseline() =>
        File.ReadAllLines(Path.Combine(ArchitectureSources.TestSourceRoot, "Architecture", BaselineFile))
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('\t', 2))
            .ToDictionary(parts => parts[0], parts => parts.Length > 1 ? parts[1] : string.Empty, StringComparer.Ordinal);

    [GeneratedRegex(@"\b(SpecApprovalRecord|ISpecApprovalStore|ApprovedSpecSetRecorder|ApprovedSpecSetResolver)\b")]
    private static partial Regex ApprovalRecord();

    [GeneratedRegex(@"\bSet\.(Specs|Goal)\b")]
    private static partial Regex SetText();
}
