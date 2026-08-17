using System.Text.RegularExpressions;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0435: a guard with an escape hatch is not a guard.
/// <para>
/// The dashboard installed with <c>pnpm install --frozen-lockfile || pnpm install</c>. The
/// flag DECLARES that the image contains exactly what the lockfile says and that a lockfile
/// disagreeing with package.json stops the build; the fallback guaranteed that stop could
/// never happen, so a drifted lockfile silently installed whatever resolved that day.
/// </para>
/// <para>
/// p0169-pre's spec said <c>pnpm install --frozen-lockfile</c>, full stop. The fallback
/// arrived with the scaffold's first commit and stayed, because nothing could contradict
/// the specification. Removing it fixes today; this rule is what stops the same two
/// characters being typed again by anyone who wants a red build to go away.
/// </para>
/// </summary>
public sealed class DependencyPinningRuleTests
{
    private static readonly string[] InstallSites =
        ["src/dashboard/Dockerfile", ".github/workflows/dashboard.yml"];

    // A frozen-lockfile install followed by anything that lets the command succeed anyway.
    private static readonly Regex EscapeHatch = new(
        @"--frozen-lockfile[^\r\n]*(\|\||;|\btrue\b)", RegexOptions.Compiled);

    [Fact]
    public void DashboardInstall_HasNoFallbackAroundTheFrozenLockfile()
    {
        var offenders = InstallSites
            .Select(relative => (relative, text: Read(relative)))
            .SelectMany(f => Lines(f.relative, f.text))
            .Where(line => EscapeHatch.IsMatch(line.Text))
            .Select(line => $"{line.Where}: {line.Text.Trim()}")
            .ToList();

        offenders.Should().BeEmpty(
            "--frozen-lockfile declares that the build installs exactly what the lockfile "
            + "says; a fallback means nothing can enforce it, and CI then goes green on a "
            + "dependency set the image does not ship.\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// The second half of the same hole: a globbed lockfile is an OPTIONAL lockfile. Absent,
    /// the COPY still succeeds and the install proceeds unpinned.
    /// </summary>
    [Fact]
    public void DashboardImage_RequiresTheLockfile_NotAGlob()
        => Read("src/dashboard/Dockerfile").Should().NotContain("pnpm-lock.yaml*",
            "a missing lockfile must stop the build, not be installed around");

    [Fact]
    public void Rule_HasTeeth_TheLineThatShippedForElevenMonths_IsFlagged()
        => EscapeHatch.IsMatch("    pnpm install --frozen-lockfile || pnpm install")
            .Should().BeTrue();

    [Fact]
    public void Rule_DoesNotFlag_APlainFrozenInstall()
        => EscapeHatch.IsMatch("    pnpm install --frozen-lockfile").Should().BeFalse();

    private static IEnumerable<(string Where, string Text)> Lines(string relative, string text) =>
        text.Split('\n').Select((line, i) => ($"{relative}:{i + 1}", line));

    private static string Read(string relative)
    {
        var path = Path.Combine(RepoRoot(), relative);
        File.Exists(path).Should().BeTrue($"{relative} must exist for this rule to mean anything");
        return File.ReadAllText(path);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "dashboard")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the test must find the repository root");
        return dir!.FullName;
    }
}
