using FluentAssertions;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// Smoke tests on SKILL.md content for the three code-aware skills.
/// They are LLM skills — full behavior is exercised by integration runs;
/// these tests ensure the prompts mention the specific guidance the spec calls out.
/// </summary>
public sealed class CodeAwareSkillContentTests
{
    private static string SkillsRoot => Path.GetFullPath(Path.Combine(
        Directory.GetCurrentDirectory().Split("bin")[0], "..", "..",
        "config", "skills", "api-security"));

    private static string ReadSkill(string name) =>
        File.ReadAllText(Path.Combine(SkillsRoot, name, "SKILL.md"));

    [Fact]
    public void AuthConfigReviewer_DeadAuthorizationMiddleware_Critical()
    {
        var skill = ReadSkill("auth-config-reviewer");
        skill.Should().Contain("UseAuthentication").And.Contain("middleware");
        skill.Should().Contain("analyzed_from_source");
    }

    [Fact]
    public void OwnershipChecker_DbSetWithoutUserPredicate_FlagsIdor()
    {
        var skill = ReadSkill("ownership-checker");
        skill.Should().Contain("UserId").And.Contain("ownership");
        skill.Should().Contain("critical");
    }

    [Fact]
    public void UploadValidatorReviewer_HeaderOnlyMime_Medium()
    {
        var skill = ReadSkill("upload-validator-reviewer");
        skill.Should().Contain("Header-only MIME").And.Contain("medium");
        skill.Should().Contain("magic");
    }
}
