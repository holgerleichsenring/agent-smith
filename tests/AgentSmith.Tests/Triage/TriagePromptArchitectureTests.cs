using AgentSmith.Application.Prompts;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Triage;

/// <summary>
/// p0138 regression-guard: the triage-structured-system prompt must NOT
/// reference SkillMdFrontmatter fields that p0131a removed, and MUST
/// document the role→slot mapping enforced by SkillRoleMapping +
/// TriageOutputValidator. A future copy-paste from old docs that
/// reintroduces the stale terms is what this test exists to catch.
/// </summary>
public sealed class TriagePromptArchitectureTests
{
    private static string LoadPrompt(string name)
    {
        var catalog = new EmbeddedPromptCatalog(
            new NullPromptOverrideSource(),
            NullLogger<EmbeddedPromptCatalog>.Instance);
        return catalog.Get(name);
    }

    [Theory]
    [InlineData("activation.positive")]
    [InlineData("activation.negative")]
    [InlineData("role_assignment")]
    [InlineData("roles_supported")]
    public void TriageStructuredSystemMd_DoesNotReferenceRemovedFrontmatterFields(string staleTerm)
    {
        var prompt = LoadPrompt("triage-structured-system");

        prompt.Should().NotContain(staleTerm,
            $"'{staleTerm}' is a SkillMdFrontmatter field removed in p0131a — referencing it confuses the LLM");
    }

    [Fact]
    public void TriageStructuredSystemMd_ContainsRoleSlotMapping()
    {
        var prompt = LoadPrompt("triage-structured-system");

        prompt.Should().Contain("producer");
        prompt.Should().Contain("Lead");
        prompt.Should().Contain("investigator");
        prompt.Should().Contain("Analyst");
        prompt.Should().Contain("judge");
        prompt.Should().Contain("Reviewer");
        prompt.Should().Contain("filter");
        prompt.Should().Contain("Filter");
    }

    [Fact]
    public void TriageStructuredUserMd_HasConceptVocabularyPlaceholder()
    {
        var prompt = LoadPrompt("triage-structured-user");

        prompt.Should().Contain("{concept_vocabulary}",
            "the user prompt must inject the concept-vocabulary list verbatim — referencing it by path is not enough");
        prompt.Should().Contain("Available Rationale Keys");
    }

    private sealed class NullPromptOverrideSource : IPromptOverrideSource
    {
        public bool TryGet(string name, out string content)
        {
            content = string.Empty;
            return false;
        }
    }
}
