using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0513: a declared verification stage states the path it needs to be there. One
/// declaration covers repositories of different shapes, and a command whose files are
/// absent was never measured against that shape — so it is SKIPPED, not failed.
/// Verification stops at the first non-zero exit, so a false red would hide every gate
/// behind it.
/// <para>
/// 2026-08-31-26d4: the condition now belongs to the context's own verify stage, which
/// is where the commands come from.
/// </para>
/// </summary>
public sealed class DeclaredStagePresenceTests
{
    private static DeclaredStagePresence Sut(params string[] presentPaths)
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, CancellationToken _) => presentPaths.Contains(path));
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(reader.Object);
        return new DeclaredStagePresence(factory.Object, NullLogger<DeclaredStagePresence>.Instance);
    }

    private static Task<bool> SatisfiedAsync(DeclaredStagePresence sut, string? whenPresent) =>
        sut.IsSatisfiedAsync(
            "data", new ContextYamlVerifyStage("parse", "dbt parse", whenPresent),
            new Mock<ISandbox>().Object, CancellationToken.None);

    [Fact]
    public async Task Condition_NoConditionDeclared_TheCommandAlwaysRuns() =>
        (await SatisfiedAsync(Sut(), null)).Should().BeTrue();

    [Fact]
    public async Task Condition_ABlankCondition_IsNoConditionAtAll() =>
        (await SatisfiedAsync(Sut(), "   ")).Should().BeTrue();

    [Fact]
    public async Task Condition_PathPresent_TheCommandRuns() =>
        (await SatisfiedAsync(Sut("/work/project.yml"), "project.yml")).Should().BeTrue();

    [Fact]
    public async Task Condition_PathAbsent_TheCommandIsSkippedNotFailed() =>
        (await SatisfiedAsync(Sut("/work/other.yml"), "project.yml")).Should().BeFalse(
            "an unmeasured shape yields no command, not a red one");

    [Fact]
    public async Task Condition_ALeadingSlashInTheCondition_IsStillReadFromTheRepoRoot() =>
        (await SatisfiedAsync(Sut("/work/models"), "/models")).Should().BeTrue();
}
