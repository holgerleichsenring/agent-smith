using AgentSmith.Application.Prompts;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Prompts;

/// <summary>
/// Regression: agent-execute-system resolves to the coding master, whose body
/// references the AgenticMaster-only tokens (RepoNames / PlanSection /
/// RunRecordDir / MaxFixIterations). GenerateTests / GenerateDocs / AgenticExecute
/// all render it via BuildExecutionSystemPrompt, which must bind EVERY known
/// master token or strict Render throws "unbound master token(s)".
/// </summary>
public sealed class AgentPromptBuilderTokenTests
{
    private sealed class CapturingCatalog : IPromptCatalog
    {
        public IReadOnlyDictionary<string, string>? Captured { get; private set; }
        public string Get(string name) => name;
        public string Render(string name, IReadOnlyDictionary<string, string> tokens)
        {
            Captured = tokens;
            return string.Empty;
        }
    }

    [Fact]
    public void BuildExecutionSystemPrompt_BindsEveryKnownMasterToken()
    {
        var catalog = new CapturingCatalog();

        new AgentPromptBuilder(catalog).BuildExecutionSystemPrompt("principles", "code-map", "context");

        catalog.Captured.Should().NotBeNull();
        foreach (var token in MasterPromptTokens.All)
            catalog.Captured!.Should().ContainKey(token,
                "the coding master may reference {{{0}}} and strict Render rejects an unbound token", token);
    }
}
