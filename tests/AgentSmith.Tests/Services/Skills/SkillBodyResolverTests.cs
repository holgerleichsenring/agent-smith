using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Infrastructure.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Skills;

public sealed class SkillBodyResolverTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SkillBodyResolver _resolver;

    public SkillBodyResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "agentsmith-resolver-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _resolver = new SkillBodyResolver(NullLogger<SkillBodyResolver>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void RoleAssigned_ReturnsMatchingBodySection()
    {
        var skill = new RoleSkillDefinition
        {
            Name = "architect",
            Rules = "full body fallback",
            RoleBodies = new Dictionary<SkillRole, string>
            {
                [SkillRole.Lead] = "lead text",
                [SkillRole.Analyst] = "analyst text"
            }
        };

        _resolver.ResolveBody(skill, SkillRole.Lead).Should().Be("lead text");
        _resolver.ResolveBody(skill, SkillRole.Analyst).Should().Be("analyst text");
    }

    [Fact]
    public void NoPerRoleSection_FallsBackToFullBody()
    {
        var skill = new RoleSkillDefinition
        {
            Name = "legacy",
            Rules = "single body for all roles",
            RoleBodies = null
        };

        _resolver.ResolveBody(skill, SkillRole.Analyst).Should().Be("single body for all roles");
    }

    [Fact]
    public void RefPlaceholder_ResolvesAgainstReferenceFile()
    {
        var refPath = Path.Combine(_tempDir, "ddd.md");
        File.WriteAllText(refPath, "DDD pattern reference content.");

        var skill = new RoleSkillDefinition
        {
            Name = "architect",
            Rules = "Body cites {{ref:ddd}}.",
            SkillDirectory = _tempDir,
            References = [new SkillReference("ddd", "ddd.md")]
        };

        var resolved = _resolver.ResolveBody(skill, SkillRole.Analyst);

        resolved.Should().Contain("DDD pattern reference content.");
        resolved.Should().NotContain("{{ref:ddd}}");
    }

    [Fact]
    public void RefPlaceholder_UnknownId_LeavesPlaceholderIntact()
    {
        var skill = new RoleSkillDefinition
        {
            Name = "architect",
            Rules = "Body cites {{ref:unknown}}.",
            SkillDirectory = _tempDir,
            References = []
        };

        var resolved = _resolver.ResolveBody(skill, SkillRole.Analyst);

        resolved.Should().Contain("{{ref:unknown}}");
    }

    [Fact]
    public void RepeatedCalls_CachesResult()
    {
        var refPath = Path.Combine(_tempDir, "cached.md");
        File.WriteAllText(refPath, "v1");

        var skill = new RoleSkillDefinition
        {
            Name = "cache-test",
            Rules = "{{ref:cached}}",
            SkillDirectory = _tempDir,
            References = [new SkillReference("cached", "cached.md")]
        };

        var first = _resolver.ResolveBody(skill, SkillRole.Analyst);

        // Mutate the file. Cached resolver should keep the first read.
        File.WriteAllText(refPath, "v2");
        var second = _resolver.ResolveBody(skill, SkillRole.Analyst);

        first.Should().Be(second);
        first.Should().Contain("v1");
    }
}
