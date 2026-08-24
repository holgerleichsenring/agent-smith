using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0513: a profile command states the path it needs to be there. One domain word
/// covers repositories of different shapes, and a command whose files are absent was
/// never measured against that shape — so it is SKIPPED, not failed. Verification
/// stops at the first non-zero exit, so a false red would hide every gate behind it.
/// </summary>
public sealed class VerifyStageResolverConditionTests
{
    private static VerifyStageResolver Sut(params string[] presentPaths)
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, CancellationToken _) => presentPaths.Contains(path));
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(reader.Object);
        return new VerifyStageResolver(
            new DotnetEntryPointDiscovery(factory.Object, NullLogger<DotnetEntryPointDiscovery>.Instance),
            new ProfileCommandPresence(factory.Object, NullLogger<ProfileCommandPresence>.Instance),
            NullLogger<VerifyStageResolver>.Instance);
    }

    private static IReadOnlyList<DomainProfileStages> Stages(params DomainProfileCommand[] commands) =>
        [new DomainProfileStages(
            new DomainProfile("sample-domain", "python:3.12-bookworm", [], commands), "/work")];

    private static ProjectMap Map() =>
        new("python", [], [], [], [], new Conventions(null, null, null),
            new CiConfig(false, null, null, null));

    private static async Task<IReadOnlyList<string>> CommandsAsync(
        VerifyStageResolver sut, IReadOnlyList<DomainProfileStages> stages) =>
        [.. (await sut.ResolveAsync(
            "data", Map(), new Mock<ISandbox>().Object, "/work", stages, [], CancellationToken.None))
            .Select(s => s.Command)];

    [Fact]
    public async Task Condition_CommandWithNoCondition_AlwaysRuns()
    {
        var commands = await CommandsAsync(
            Sut(), Stages(new DomainProfileCommand("parse", "tool parse")));

        commands.Should().Equal("tool parse");
    }

    [Fact]
    public async Task Condition_PathPresent_TheCommandRuns()
    {
        var commands = await CommandsAsync(
            Sut("/work/project.yml"),
            Stages(new DomainProfileCommand("parse", "tool parse", "project.yml")));

        commands.Should().Equal("tool parse");
    }

    [Fact]
    public async Task Condition_PathAbsent_TheCommandIsSkippedNotFailed()
    {
        var commands = await CommandsAsync(
            Sut("/work/other.yml"),
            Stages(new DomainProfileCommand("parse", "tool parse", "project.yml")));

        commands.Should().BeEmpty("an unmeasured shape yields no command, not a red one");
    }

    [Fact]
    public async Task Condition_PathAbsentForOneOfTwo_TheOtherStillRuns()
    {
        var commands = await CommandsAsync(
            Sut("/work/models"),
            Stages(
                new DomainProfileCommand("parse", "tool parse", "project.yml"),
                new DomainProfileCommand("lint", "tool lint models", "models"),
                new DomainProfileCommand("check", "tool check")));

        commands.Should().Equal("tool lint models", "tool check");
    }
}
