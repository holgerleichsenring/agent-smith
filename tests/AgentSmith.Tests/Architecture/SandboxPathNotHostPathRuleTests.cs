using System.Text.RegularExpressions;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-09-19-c511a: the repository root of a run is the sandbox mount <c>/work</c>, which exists
/// inside the sandbox and nowhere else. A file in <c>AgentSmith.Application</c> or
/// <c>AgentSmith.Infrastructure.Core</c> that names that path and ALSO calls System.IO is one
/// refactor away from doing to the host what FileDecisionLogger did: compose
/// <c>/work/.agentsmith/decisions/…</c> and hand it to Directory.CreateDirectory in the server
/// process, where it threw UnauthorizedAccessException and ended a coding run that had already done
/// its work (run 2026-09-17T14-41-51-a3e2, step 21).
/// <para>
/// What this rule is NOT: the guard against the bug above. It would not have caught it — neither
/// half of that failure matches, because FileDecisionLogger RECEIVED the path (it named no /work
/// token) and LogDecisionToolHost named the token but called no System.IO. The guard is the
/// CONTRACT: IDecisionLogger takes a file surface now, so a path cannot be passed at all. This is
/// a coarse tripwire under the next file that tries to hold both, and a file that legitimately
/// does goes on the exemption list WITH ITS REASON — a sentence somebody has to write.
/// </para>
/// <para>
/// Not in scope: <c>AgentSmith.Sandbox.Agent</c>, which runs INSIDE the sandbox and for which
/// /work is an ordinary local directory.
/// </para>
/// </summary>
public sealed class SandboxPathNotHostPathRuleTests
{
    private static readonly string[] JudgedProjects =
    [
        "AgentSmith.Application",
        "AgentSmith.Infrastructure.Core",
    ];

    private static readonly Dictionary<string, string> Exempt = new(StringComparer.Ordinal)
    {
        ["AcquireSourceHandler.cs"] =
            "reads a document from the HOST's processing folder (a real host path from the inbox) "
            + "and writes it to /work through the sandbox file surface — the two paths are different "
            + "filesystems and the handler keeps them apart.",
    };

    private static readonly Regex SandboxWorkPath = new(
        @"Repository\.LocalPath|SandboxWorkPath|""/work""", RegexOptions.Compiled);

    private static readonly Regex HostFileSystem = new(
        @"\bDirectory\.(CreateDirectory|Exists|EnumerateFiles|GetFiles|Delete)\(|"
        + @"\bFile\.(WriteAllText|WriteAllBytes|ReadAllText|ReadAllBytes|Exists|AppendAllText|Delete|Open|Move|Copy)",
        RegexOptions.Compiled);

    [Fact]
    public void NoHostFileSystemCall_InApplicationOrInfrastructureCore_ComposesAPathFromTheSandboxWorkPath()
    {
        var offenders = new List<string>();
        foreach (var path in ArchitectureSources.HandWrittenBackendFiles())
        {
            if (!JudgedProjects.Any(p => path.Contains(
                    $"{Path.DirectorySeparatorChar}{p}{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
                continue;
            var fileName = Path.GetFileName(path);
            if (Exempt.ContainsKey(fileName)) continue;

            var source = File.ReadAllText(path);
            var workPath = SandboxWorkPath.Match(source);
            var hostIo = HostFileSystem.Match(source);
            if (workPath.Success && hostIo.Success)
                offenders.Add($"{fileName}: names {workPath.Value} and calls {hostIo.Value}…");
        }

        offenders.Should().BeEmpty(
            "the sandbox mount is not a directory of this process — write through the repository's "
            + "ISandboxFileReader, or add the file to the exemption list with the reason the two "
            + "filesystems are kept apart");
    }

    [Fact]
    public void EveryExemption_StatesWhyTheTwoFilesystemsAreKeptApart()
    {
        Exempt.Values.Should().OnlyContain(reason => reason.Length > 40,
            "an exemption without a reason is a hole nobody can review");
    }
}
