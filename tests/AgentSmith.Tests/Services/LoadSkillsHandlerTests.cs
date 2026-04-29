using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class LoadSkillsHandlerTests : IDisposable
{
    private readonly string _tempDir;

    public LoadSkillsHandlerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "as-skills-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ResolvesSkillsPath_ViaSkillCatalog_WhenPostP0103Layout()
    {
        // Catalog under tempDir/.cache/skills, with skills/api-security/ inside it
        var catalogRoot = Path.Combine(_tempDir, "cache");
        var catalogSkills = Path.Combine(catalogRoot, "skills", "api-security");
        Directory.CreateDirectory(catalogSkills);

        var catalogPath = new Mock<ISkillsCatalogPath>();
        catalogPath.Setup(c => c.Root).Returns(catalogRoot);

        var skillLoader = new Mock<ISkillLoader>();
        skillLoader
            .Setup(l => l.LoadRoleDefinitions(catalogSkills))
            .Returns(new List<RoleSkillDefinition> { new() { Name = "test", DisplayName = "Test", Emoji = "x", Rules = "" } });

        var handler = new LoadSkillsHandler(
            skillLoader.Object,
            catalogPath.Object,
            NullLogger<LoadSkillsHandler>.Instance);

        var pipeline = new PipelineContext();
        var ctx = new LoadSkillsContext("skills/api-security", pipeline);

        var result = await handler.ExecuteAsync(ctx, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Loaded 1 skills");
        skillLoader.Verify(l => l.LoadRoleDefinitions(catalogSkills), Times.Once);
    }

    [Fact]
    public async Task ResolvesSkillsPath_AsIs_WhenAbsolutePathExists()
    {
        var absSkillsDir = Path.Combine(_tempDir, "abs-skills");
        Directory.CreateDirectory(absSkillsDir);

        var catalogPath = new Mock<ISkillsCatalogPath>();
        catalogPath.Setup(c => c.Root).Throws(new InvalidOperationException("not bootstrapped"));

        var skillLoader = new Mock<ISkillLoader>();
        skillLoader.Setup(l => l.LoadRoleDefinitions(absSkillsDir)).Returns(new List<RoleSkillDefinition>());

        var handler = new LoadSkillsHandler(skillLoader.Object, catalogPath.Object, NullLogger<LoadSkillsHandler>.Instance);
        var pipeline = new PipelineContext();
        var ctx = new LoadSkillsContext(absSkillsDir, pipeline);

        var result = await handler.ExecuteAsync(ctx, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        skillLoader.Verify(l => l.LoadRoleDefinitions(absSkillsDir), Times.Once);
    }

    [Fact]
    public async Task SoftFails_WhenNoPathResolves()
    {
        var catalogPath = new Mock<ISkillsCatalogPath>();
        catalogPath.Setup(c => c.Root).Returns(Path.Combine(_tempDir, "empty-cache"));

        var skillLoader = new Mock<ISkillLoader>();

        var handler = new LoadSkillsHandler(skillLoader.Object, catalogPath.Object, NullLogger<LoadSkillsHandler>.Instance);
        var pipeline = new PipelineContext();
        var ctx = new LoadSkillsContext("skills/nonexistent", pipeline);

        var result = await handler.ExecuteAsync(ctx, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("Skills directory not found");
        skillLoader.Verify(l => l.LoadRoleDefinitions(It.IsAny<string>()), Times.Never);
    }
}
