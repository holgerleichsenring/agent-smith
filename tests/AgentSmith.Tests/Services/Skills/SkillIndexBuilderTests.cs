using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
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
        var architectDir = Path.Combine(codingDir, "architect");

        var skill = new RoleSkillDefinition
        {
            Name = "architect",
            Description = "test",
            SkillDirectory = architectDir,
            RolesSupported = [SkillRole.Lead, SkillRole.Analyst],
            Activation = ActivationCriteria.Empty,
            RoleAssignments = [],
            OutputContract = new OutputContract(
                "skill-observation", 8, 200,
                new Dictionary<SkillRole, OutputForm>
                {
                    [SkillRole.Lead] = OutputForm.Plan,
                    [SkillRole.Analyst] = OutputForm.List
                })
        };

        _builder.Build(_tempDir, [skill]);

        var indexPath = Path.Combine(_tempDir, "_index", "coding.yaml");
        File.Exists(indexPath).Should().BeTrue();
        var contents = File.ReadAllText(indexPath);
        contents.Should().Contain("architect");
        contents.Should().Contain("lead");
        contents.Should().Contain("analyst");
    }

    [Fact]
    public void SkillsWithoutRolesSupported_OmittedFromIndex()
    {
        var codingDir = Path.Combine(_tempDir, "coding");
        Directory.CreateDirectory(codingDir);
        var skill = new RoleSkillDefinition
        {
            Name = "legacy",
            Description = "no new fields",
            SkillDirectory = Path.Combine(codingDir, "legacy"),
            RolesSupported = null
        };

        _builder.Build(_tempDir, [skill]);

        var indexPath = Path.Combine(_tempDir, "_index", "coding.yaml");
        File.Exists(indexPath).Should().BeFalse(); // no skills in coding survived projection
    }
}
