using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0513: a verification command states the path it needs to be there. One command list
/// covers repositories of different shapes, and a command whose files are absent was
/// never measured against that shape — so it is SKIPPED, not failed. Verification stops
/// at the first non-zero exit, so a false red would hide every gate behind it.
/// <para>
/// 2026-08-31-77a8: the domain profile that used to supply those commands is gone, so
/// the condition is measured here directly rather than through the stage resolver.
/// </para>
/// </summary>
public sealed class ProfileCommandPresenceTests
{
    private static ProfileCommandPresence Sut(params string[] presentPaths)
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, CancellationToken _) => presentPaths.Contains(path));
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(reader.Object);
        return new ProfileCommandPresence(factory.Object, NullLogger<ProfileCommandPresence>.Instance);
    }

    private static Task<bool> SatisfiedAsync(ProfileCommandPresence sut, string? whenPresent) =>
        sut.IsSatisfiedAsync(
            "data", "parse", whenPresent, new Mock<ISandbox>().Object, "/work",
            CancellationToken.None);

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
    public async Task Condition_ALeadingSlashInTheCondition_IsStillReadAgainstTheWorkdir() =>
        (await SatisfiedAsync(Sut("/work/models"), "/models")).Should().BeTrue();
}
