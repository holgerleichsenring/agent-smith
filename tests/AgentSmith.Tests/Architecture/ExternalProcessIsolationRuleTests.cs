using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-08-30-f590: a test that starts a real external process stays out of the fast lane.
/// <para>
/// The isolation only holds while somebody remembers it, and nobody remembers a convention
/// they did not write. This asserts it instead: a class that reaches for a real tool, a real
/// process or a real sandbox belongs to <see cref="ExternalProcessCollection"/>, and the
/// build says so on the day it is added rather than on the day CI turns red for a reason
/// that looks like a defect and is not.
/// </para>
/// </summary>
public sealed class ExternalProcessIsolationRuleTests
{
    // "new InProcessSandbox(", not "InProcessSandbox(": two composition-root tests merely
    // REGISTER the sandbox and start nothing, and a rule that cries wolf gets switched off.
    private static readonly Regex StartsAProcess = new(
        @"SandboxToolAvailability|Process\.Start|new ProcessToolRunner|new InProcessSandbox\(",
        RegexOptions.Compiled);

    [Fact]
    public void ATestThatStartsARealProcess_BelongsToTheIsolatedCollection()
    {
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(TestSourceRoot(), "*Tests.cs", SearchOption.AllDirectories))
        {
            // The rule carries the shapes it hunts for in its own source, so it excludes itself
            // rather than softening the pattern — a narrower pattern would miss real offenders.
            if (Path.GetFileName(file) == nameof(ExternalProcessIsolationRuleTests) + ".cs") continue;
            var source = File.ReadAllText(file);
            if (!StartsAProcess.IsMatch(source)) continue;
            if (source.Contains($"[Collection({nameof(ExternalProcessCollection)}.Name)]")) continue;
            offenders.Add(Path.GetFileName(file));
        }

        offenders.Should().BeEmpty(
            "a test starting a real process saturates a two-core runner and makes unrelated "
            + "timing assertions fail; tag it [Collection(ExternalProcessCollection.Name)]");
    }

    [Fact]
    public void Rule_HasTeeth_TheMarkerMatchesAKnownOffenderShape()
    {
        StartsAProcess.IsMatch("var ok = SandboxToolAvailability.IsAvailable(\"npm\");")
            .Should().BeTrue("the rule must recognise the shape it exists to catch");
    }

    private static string TestSourceRoot()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AgentSmith.Tests.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the rule needs the test project's own sources");
        return dir!.FullName;
    }
}
