using AgentSmith.Tests.Architecture;
using FluentAssertions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// The booted-server rig was made fast, and the one way to fake that is to assert less.
/// So the assertions themselves are the golden: every one the booted tests made before the
/// rig changed, read out of the commit the change started from. A line that is no longer
/// there is a claim the suite has stopped making, whatever its runtime says.
/// <para>
/// Adding an assertion is not a regression and is not checked. Removing or rewording one
/// is, and that is the whole rule.
/// </para>
/// </summary>
public sealed class BootedAssertionRatchetTests
{
    private const string GoldenFile = "booted-assertions-baseline.tsv";
    private const string StartupResilience = "Server/StartupResilienceTests.cs";
    private const string AuthDirectory = "Server/Auth/";

    [Fact]
    public void StartupResilience_EveryExistingAssertion_HoldsUnchanged() =>
        Missing(file => file == StartupResilience).Should().BeEmpty(
            "the startup-resilience cases assert on dependencies that are not there, and a "
            + "rig that stops pointing at nothing must not stop asserting on it either.\n  "
            + string.Join("\n  ", Missing(file => file == StartupResilience)));

    [Fact]
    public void Auth_EveryExistingAssertion_HoldsUnchanged() =>
        Missing(file => file.StartsWith(AuthDirectory, StringComparison.Ordinal)).Should().BeEmpty(
            "a booted authorization case that got faster by asking less is not faster.\n  "
            + string.Join("\n  ", Missing(f => f.StartsWith(AuthDirectory, StringComparison.Ordinal))));

    private static IReadOnlyList<string> Missing(Func<string, bool> inScope) =>
        [.. Golden()
            .Where(entry => inScope(entry.File))
            .Where(entry => !Assertions(entry.File).Contains(entry.Assertion))
            .Select(entry => $"{entry.File}: {entry.Assertion}")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)];

    private static IReadOnlyCollection<string> Assertions(string file)
    {
        var path = Path.Combine(ArchitectureSources.TestSourceRoot, file);
        return File.Exists(path)
            ? new HashSet<string>(
                File.ReadLines(path).Select(l => l.Trim()).Where(l => l.Contains(".Should(")),
                StringComparer.Ordinal)
            : [];
    }

    private static IEnumerable<(string File, string Assertion)> Golden() =>
        File.ReadAllLines(Path.Combine(ArchitectureSources.TestSourceRoot, "Server", GoldenFile))
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('\t', 2))
            .Select(parts => (parts[0].Replace('\\', '/'), parts[1]));
}
