using AgentSmith.Sandbox.Wire;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-08-25-0d01: the supported protocol window has to be written down ONCE, or it is a
/// wish rather than a rule. Three wire records each declared their own
/// <c>CurrentSchemaVersion = 1</c>, written at about forty construction sites and read at
/// none — three copies of a number that would eventually disagree, guarding nothing.
/// </summary>
public sealed class WireProtocolWindowTests
{
    private const string WireProject = "AgentSmith.Sandbox.Wire";

    [Fact]
    public void Protocol_TheSupportedWindow_IsStatedInOnePlace()
    {
        Step.CurrentSchemaVersion.Should().Be(WireProtocol.Current);
        StepResult.CurrentSchemaVersion.Should().Be(WireProtocol.Current);
        StepEvent.CurrentSchemaVersion.Should().Be(WireProtocol.Current);

        var declarations = WireSources()
            .SelectMany(File.ReadLines)
            .Where(l => l.Contains("CurrentSchemaVersion =", StringComparison.Ordinal))
            .ToList();

        declarations.Should().NotBeEmpty("the wire records still declare the version they stamp");
        declarations.Should().OnlyContain(l => l.Contains("WireProtocol.Current", StringComparison.Ordinal),
            "a record that writes its own literal is a second statement of the window, and the "
            + "two will disagree the first time one of them is raised");
    }

    [Fact]
    public void Protocol_TheWindow_IsNotOperatorConfigurable()
    {
        WireSources()
            .SelectMany(File.ReadLines)
            .Should().NotContain(l => l.Contains("Environment.GetEnvironmentVariable", StringComparison.Ordinal),
                "an operator cannot make two builds agree by editing a value, and offering "
                + "the knob would only let them declare a compatibility that is not true");
    }

    private static IEnumerable<string> WireSources() =>
        Directory.EnumerateFiles(
            Path.Combine(ArchitectureSources.BackendRoot, WireProject), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
}
