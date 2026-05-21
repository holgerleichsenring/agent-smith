using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// p0154 invariant: the deprecated grep / glob / list_files alias names that
/// p0153 left registered as forwarders are gone from every phase. Re-introducing
/// them would re-open the cross-repo skill-catalogue / agent-binary surface drift.
/// </summary>
public sealed class FilesystemToolHostNoDeprecatedAliasesTests
{
    private static readonly string[] DeprecatedAliases = { "grep", "glob", "list_files" };

    [Theory]
    [InlineData(null)]                                  // ReadOnly / null phase
    [InlineData(SkillExecutionPhase.Plan)]              // Investigator surface
    [InlineData(SkillExecutionPhase.Verify)]            // Verify surface
    [InlineData(SkillExecutionPhase.Implementation)]    // Full surface
    [InlineData(SkillExecutionPhase.Bootstrap)]         // Bootstrap surface
    public void DoesNotIncludeDeprecatedAliases(SkillExecutionPhase? phase)
    {
        var host = new FilesystemToolHost(Mock.Of<ISandbox>());
        var toolNames = host.GetTools(phase, investigatorMode: null)
            .Select(t => t.Name)
            .ToList();

        toolNames.Should().NotContain(DeprecatedAliases);
    }
}
