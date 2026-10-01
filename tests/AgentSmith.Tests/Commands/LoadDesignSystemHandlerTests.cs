using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>2026-10-01-283dg: a coding run reads the root DESIGN.md of each repository.</summary>
public sealed class LoadDesignSystemHandlerTests
{
    private const string Path = "/work/DESIGN.md";
    private const string Design = "---\ncolors:\n  primary: \"#22c55e\"\n---\n\nThe green is the only accent.\n";

    [Fact]
    public async Task LoadDesignSystem_TwoSandboxesOneWithDesignMd_PublishesOne()
    {
        var (pipeline, handler) = Arrange(
            ("repo-a", "repo-a", Design), ("repo-b", "repo-b", null));

        var result = await handler.ExecuteAsync(new LoadDesignSystemContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("1 of 2 repo(s)");
        var published = pipeline.Get<IReadOnlyList<ContextDocument>>(ContextKeys.DesignSystem);
        published.Should().ContainSingle().Which.Should().Be(
            new ContextDocument("repo-a", null, null, "DESIGN.md", Design));
    }

    [Fact]
    public async Task LoadDesignSystem_OneRepositoryInTwoSandboxes_PublishesItOnce()
    {
        var (pipeline, handler) = Arrange(
            ("repo-a/api", "repo-a", Design), ("repo-a/web", "repo-a", Design));

        await handler.ExecuteAsync(new LoadDesignSystemContext(pipeline), CancellationToken.None);

        pipeline.Get<IReadOnlyList<ContextDocument>>(ContextKeys.DesignSystem)
            .Should().ContainSingle("the file sits at the repository root, so both sandboxes hold the same one");
    }

    [Fact]
    public async Task LoadDesignSystem_NoRepositoryCarriesOne_PublishesNothing()
    {
        var (pipeline, handler) = Arrange(("repo-a", "repo-a", null));

        var result = await handler.ExecuteAsync(new LoadDesignSystemContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("an absent DESIGN.md is the common case, never an error");
        pipeline.TryGet<IReadOnlyList<ContextDocument>>(ContextKeys.DesignSystem, out _).Should().BeFalse();
    }

    [Fact]
    public void CodePreset_LoadDesignSystem_FollowsLoadCodingPrinciples()
    {
        var code = PipelinePresets.Code.ToList();

        code.IndexOf(CommandNames.LoadDesignSystem).Should().Be(code.IndexOf(CommandNames.LoadCodingPrinciples) + 2);
    }

    private static (PipelineContext, LoadDesignSystemHandler) Arrange(
        params (string Key, string Repo, string? Content)[] sandboxes)
    {
        var pipeline = new PipelineContext();
        var factory = new Mock<ISandboxFileReaderFactory>();
        var map = new Dictionary<string, ISandbox>(StringComparer.Ordinal);
        foreach (var (key, _, content) in sandboxes)
        {
            var sandbox = new Mock<ISandbox>().Object;
            var reader = new Mock<ISandboxFileReader>();
            reader.Setup(r => r.TryReadAsync(Path, It.IsAny<CancellationToken>())).ReturnsAsync(content);
            factory.Setup(f => f.Create(sandbox)).Returns(reader.Object);
            map[key] = sandbox;
        }
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, map);
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos,
            sandboxes.ToDictionary(s => s.Key, s => s.Repo, StringComparer.Ordinal));
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            [.. sandboxes.Select(s => s.Repo).Distinct().Select(name => new RepoConnection { Name = name })]);
        return (pipeline, new LoadDesignSystemHandler(
            factory.Object, new SandboxTargets(), NullLogger<LoadDesignSystemHandler>.Instance));
    }
}
