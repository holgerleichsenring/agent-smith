using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Skills;

public sealed class SkillIndexBuilderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SkillIndexBuilder _builder;

    public SkillIndexBuilderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-index-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _builder = new SkillIndexBuilder(NullLogger<SkillIndexBuilder>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void WritesPerCategoryYaml()
    {
        var codingDir = Path.Combine(_tempDir, "coding");
        Directory.CreateDirectory(codingDir);
        var architectDir = Path.Combine(codingDir, "architect-planner");

        var skill = new RoleSkillDefinition
        {
            Name = "architect-planner",
            Description = "test",
            SkillDirectory = architectDir,
            Role = "producer",
            OutputSchema = "plan",
            ActivatesWhen = "pipeline_name = \"fix-bug\"",
        };

        _builder.Build(_tempDir, [skill]);

        var indexPath = Path.Combine(_tempDir, "_index", "coding.yaml");
        File.Exists(indexPath).Should().BeTrue();
        var contents = File.ReadAllText(indexPath);
        contents.Should().Contain("architect-planner");
        contents.Should().Contain("role: producer");
        contents.Should().Contain("output_schema: plan");
    }

    [Fact]
    public void SkillsWithoutRole_OmittedFromIndex()
    {
        // p0131a: skills without the new-format `role` field (e.g. partial loads
        // or pre-2.0 catalog leftovers) are excluded from the index entirely.
        var codingDir = Path.Combine(_tempDir, "coding");
        Directory.CreateDirectory(codingDir);
        var skill = new RoleSkillDefinition
        {
            Name = "incomplete",
            Description = "missing role",
            SkillDirectory = Path.Combine(codingDir, "incomplete"),
            Role = null,
        };

        _builder.Build(_tempDir, [skill]);

        var indexPath = Path.Combine(_tempDir, "_index", "coding.yaml");
        File.Exists(indexPath).Should().BeFalse();
    }
}
