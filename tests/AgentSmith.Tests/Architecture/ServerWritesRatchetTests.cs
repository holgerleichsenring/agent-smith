using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-11-16-3596: everything is a pipeline — work that writes to a tracker or a repository is a
/// step of a run with a run id; the server accepts requests and starts runs. The server files that
/// write directly today are listed in the baseline as debt: no file may join, and a file that no
/// longer writes must leave, so the list moves one direction.
/// </summary>
public sealed class ServerWritesRatchetTests
{
    private const string BaselineFile = "server-writes-baseline.txt";

    [Fact]
    public void ServerProject_ANewFile_DoesNotWriteToATrackerOrRepository()
    {
        var baseline = Baseline();
        var offenders = Writers().Where(f => !baseline.Contains(f)).ToList();

        offenders.Should().BeEmpty(
            "work that writes to a tracker or a repository is a pipeline step with a run id; the server "
            + "starts runs. Move the write into a step instead of adding the file to "
            + BaselineFile + ".\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void ServerProject_AFileThatStoppedWriting_MustLeaveTheBaseline()
    {
        var writers = Writers().ToHashSet(StringComparer.Ordinal);
        var stale = Baseline().Where(f => !writers.Contains(f)).ToList();

        stale.Should().BeEmpty(
            $"a server file that no longer writes (or is gone) must leave {BaselineFile}, so the list "
            + "keeps telling the truth about what is left.\n  " + string.Join("\n  ", stale));
    }

    private static IEnumerable<string> Writers()
    {
        var root = Path.Combine(ArchitectureSources.BackendRoot, "AgentSmith.Server");
        return ArchitectureSources.HandWrittenBackendFiles()
            .Where(p => p.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(p => ServerWritesScanner.Writes(File.ReadAllText(p)))
            .Select(p => Path.GetRelativePath(ArchitectureSources.BackendRoot, p).Replace('\\', '/'))
            .OrderBy(p => p, StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> Baseline() =>
        File.ReadAllLines(Path.Combine(ArchitectureSources.TestSourceRoot, "Architecture", BaselineFile))
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();
}
