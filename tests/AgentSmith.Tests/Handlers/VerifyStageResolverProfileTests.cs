using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0504: the profile is consulted IN THE SKIP — after declared ci commands, in the
/// branch a repository that is not .NET falls into. A .NET repository never enters it
/// and still discovers its own entry point.
/// </summary>
public sealed class VerifyStageResolverProfileTests
{
    private static readonly DomainProfile Profile = new(
        "sample-domain", "python:3.12-bookworm", [],
        [
            new DomainProfileCommand("parse", "tool parse"),
            new DomainProfileCommand("validate", "tool validate"),
        ]);

    private static readonly IReadOnlyList<DomainProfileStages> Stages =
        [new DomainProfileStages(Profile, "/work")];

    private static VerifyStageResolver Sut(params string[] entries)
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ListAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(reader.Object);
        return new VerifyStageResolver(
            new DotnetEntryPointDiscovery(factory.Object, NullLogger<DotnetEntryPointDiscovery>.Instance),
            NullLogger<VerifyStageResolver>.Instance);
    }

    private static ProjectMap Map(string language, CiConfig? ci = null) =>
        new(language, [], [], [], [], new Conventions(null, null, null),
            ci ?? new CiConfig(false, null, null, null));

    private static ISandbox Sandbox() => new Mock<ISandbox>().Object;

    [Fact]
    public async Task ResolveStages_RepoHasDeclaredCiCommands_TheProfileIsNotConsulted()
    {
        var stages = await Sut().ResolveAsync(
            "data", Map("python", new CiConfig(true, "make build", null, null)), Sandbox(),
            "/work", Stages, [], CancellationToken.None);

        stages.Select(s => s.Command).Should().Equal("make build");
    }

    [Fact]
    public async Task ResolveStages_NonDotnetContextWithADomain_RunsTheProfileCommandsInOrder()
    {
        var stages = await Sut().ResolveAsync(
            "data", Map("python"), Sandbox(), "/work", Stages, [], CancellationToken.None);

        stages.Select(s => s.Command).Should().Equal("tool parse", "tool validate");
        stages.Select(s => s.Stage).Should().Equal("parse", "validate");
        stages.Should().OnlyContain(s => s.Cwd == "/work");
    }

    [Fact]
    public async Task ResolveStages_DotnetContext_StillDiscoversItsOwnEntryPoint()
    {
        var stages = await Sut("Sample.sln").ResolveAsync(
            "server", Map("csharp"), Sandbox(), "/work", Stages, [], CancellationToken.None);

        stages.Select(s => s.Command).Should().Equal("dotnet build \"Sample.sln\"");
    }

    [Fact]
    public async Task ResolveStages_ProfileCommandCannotFail_IsDroppedLikeADeclaredOne()
    {
        var noop = new DomainProfile(
            "sample-domain", "python:3.12-bookworm", [],
            [
                new DomainProfileCommand("build", "echo build placeholder"),
                new DomainProfileCommand("test", "tool test"),
            ]);

        var stages = await Sut().ResolveAsync(
            "data", Map("python"), Sandbox(), "/work",
            [new DomainProfileStages(noop, "/work")], [], CancellationToken.None);

        stages.Select(s => s.Command).Should().Equal("tool test");
    }

    [Fact]
    public async Task ResolveStages_NoDomain_StillSkips()
    {
        var stages = await Sut().ResolveAsync(
            "docs", Map("python"), Sandbox(), "/work", [], [], CancellationToken.None);

        stages.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveStages_SharedSandboxWithTwoDomains_RunsEachAtItsOwnWorkdir()
    {
        var second = new DomainProfile(
            "other-domain", "python:3.12-bookworm", [],
            [new DomainProfileCommand("check", "tool check")]);

        var stages = await Sut().ResolveAsync(
            "data", Map("python"), Sandbox(), "/work",
            [new DomainProfileStages(Profile, "/work/a"), new DomainProfileStages(second, "/work/b")],
            [], CancellationToken.None);

        stages.Select(s => s.Cwd).Should().Equal("/work/a", "/work/a", "/work/b");
    }
}
