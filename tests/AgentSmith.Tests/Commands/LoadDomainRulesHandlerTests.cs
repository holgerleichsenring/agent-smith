using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Commands;

public class LoadDomainRulesHandlerTests
{
    private readonly LoadDomainRulesHandler _handler = new(
        new ProjectMetaResolver(),
        NullLogger<LoadDomainRulesHandler>.Instance);

    [Fact]
    public async Task ExecuteAsync_FileExists_LoadsContent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var relativePath = ".agentsmith/coding-principles.md";
        var fullPath = Path.Combine(tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, "# Test Principles");

        var repo = new Repository(tempDir, new BranchName("main"), "https://example.com");
        var pipeline = new PipelineContext();
        var context = new LoadDomainRulesContext(relativePath, repo, pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<string>(ContextKeys.DomainRules).Should().Be("# Test Principles");

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task ExecuteAsync_FileNotFound_ReturnsOkSoftFail()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var repo = new Repository(tempDir, new BranchName("main"), "https://example.com");
        var pipeline = new PipelineContext();
        var context = new LoadDomainRulesContext("nonexistent/path.md", repo, pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<string>(ContextKeys.DomainRules, out _).Should().BeFalse();

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultPathInMonorepoSubdir_ResolvesViaProjectMetaResolver()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var subRepo = Path.Combine(tempDir, "services", "api-gateway");
        Directory.CreateDirectory(Path.Combine(subRepo, ".agentsmith"));
        await File.WriteAllTextAsync(Path.Combine(subRepo, ".agentsmith", "coding-principles.md"), "# Sub Rules");

        var repo = new Repository(tempDir, new BranchName("main"), "https://example.com");
        var pipeline = new PipelineContext();
        var context = new LoadDomainRulesContext(".agentsmith/coding-principles.md", repo, pipeline);

        var result = await _handler.ExecuteAsync(context, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<string>(ContextKeys.DomainRules).Should().Be("# Sub Rules");

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task ExecuteAsync_ContentAccessibleViaCodingPrinciplesAlias()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var relativePath = ".agentsmith/coding-principles.md";
        var fullPath = Path.Combine(tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, "# Rules");

        var repo = new Repository(tempDir, new BranchName("main"), "https://example.com");
        var pipeline = new PipelineContext();
        var context = new LoadDomainRulesContext(relativePath, repo, pipeline);

        await _handler.ExecuteAsync(context, CancellationToken.None);

        // CodingPrinciples is an alias for DomainRules — both resolve to same key
        pipeline.Get<string>(ContextKeys.CodingPrinciples).Should().Be("# Rules");

        Directory.Delete(tempDir, true);
    }
}
