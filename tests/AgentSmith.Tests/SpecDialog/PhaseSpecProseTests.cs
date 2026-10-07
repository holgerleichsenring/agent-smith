using AgentSmith.Application.Services.SpecDialog;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>2026-10-01-f5c3b: the prose parts of a spec a filed ticket shows its reader.</summary>
public sealed class PhaseSpecProseTests
{
    private static readonly IReadOnlyDictionary<string, object?> Map = OutcomeYamlReader.ReadMap("""
        spec: p9000a
        goal: g
        scope:
          in: "  the table  "
          out: the API
        decisions:
          - "a bare line"
          - key: "A TABLE FIRST, because the API follows the storage."
        """);

    [Fact]
    public void Decisions_BareAndMapShaped_AreBothText() =>
        PhaseSpecProse.Decisions(Map).Should().Equal(
            "a bare line", "A TABLE FIRST, because the API follows the storage.");

    [Fact]
    public void ScopeIn_IsTrimmed() => PhaseSpecProse.ScopeIn(Map).Should().Be("the table");

    [Fact]
    public void ScopeOut_IsTheOutPart() => PhaseSpecProse.ScopeOut(Map).Should().Be("the API");

    [Fact]
    public void ScopeIn_NoScope_IsEmpty() =>
        PhaseSpecProse.ScopeIn(OutcomeYamlReader.ReadMap("spec: p1\ngoal: g")).Should().BeEmpty();
}
