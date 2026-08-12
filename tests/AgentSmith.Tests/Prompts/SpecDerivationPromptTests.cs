using AgentSmith.Application.Prompts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Prompts;

/// <summary>
/// p0400a: run b9b0 proved that documenting ships_code was not enough — the
/// derivation omitted it on a pure-knowledge phase and the keystone failed a
/// 7/7-done phase for lacking a diff it never promised. The prompt now states
/// the obligation; this pins the load-bearing wording.
/// </summary>
public sealed class SpecDerivationPromptTests
{
    [Fact]
    public void DerivationPrompt_StatesShipsCodeRule()
    {
        var catalog = new EmbeddedPromptCatalog(
            new EnvDirectoryPromptOverrideSource(NullLogger<EnvDirectoryPromptOverrideSource>.Instance),
            NullLogger<EmbeddedPromptCatalog>.Instance);

        var prompt = catalog.Get("spec-derivation-master");

        prompt.Should().Contain("MUST set \"ships_code\": false",
            "the obligation must be stated, not merely the field documented");
        prompt.Should().Contain("THIS IS AN OBLIGATION, NOT AN OPTION",
            "the rule must read as binding, run b9b0 proved documentation alone is ignored");
    }
}
