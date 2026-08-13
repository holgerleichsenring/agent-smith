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
        var prompt = DerivationPrompt();

        prompt.Should().Contain("MUST set \"ships_code\": false",
            "the obligation must be stated, not merely the field documented");
        prompt.Should().Contain("THIS IS AN OBLIGATION, NOT AN OPTION",
            "the rule must read as binding, run b9b0 proved documentation alone is ignored");
    }

    /// <summary>
    /// p0413: run 1b4b cut a mechanical ticket into three phases, each with a full
    /// master loop, and burned $10 without finishing the first. The prompt must
    /// state the cut-sizing rule the classified shape feeds — as a RULE about the
    /// work, with no example that names an ecosystem, a tool or a language.
    /// </summary>
    [Fact]
    public void DerivationPrompt_SizesTheCutToTheShapeOfTheWork()
    {
        var prompt = DerivationPrompt();

        prompt.Should().Contain("THE CUT IS SIZED TO THE SHAPE OF THE WORK");
        prompt.Should().Contain("FEWEST phases its deliverable allows",
            "deterministic work must be told to collapse, not merely allowed to");
        prompt.Should().Contain("a step per target turns one operation into one round of work",
            "the measured failure was one model round trip per target, not the phase count alone");
        prompt.Should().Contain("No shape stated means cut as you otherwise would",
            "an unclassified ticket must reach the cut it always got");
    }

    private static string DerivationPrompt() =>
        new EmbeddedPromptCatalog(
                new EnvDirectoryPromptOverrideSource(NullLogger<EnvDirectoryPromptOverrideSource>.Instance),
                NullLogger<EmbeddedPromptCatalog>.Instance)
            .Get("spec-derivation-master");
}
