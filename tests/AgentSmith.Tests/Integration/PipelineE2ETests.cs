using FluentAssertions;
using Xunit.Abstractions;

namespace AgentSmith.Tests.Integration;

/// <summary>
/// p0196: every pipeline preset must run end-to-end through PipelineExecutor
/// with real handlers + stubbed external boundaries, returning IsSuccess.
/// One failure here means an operator triggering that preset would crash —
/// either a handler-internal bug, a DI gap, or a context-key the contract
/// test missed.
/// </summary>
public sealed class PipelineE2ETests(ITestOutputHelper output)
{
    // p0196 covers 5 of 10 presets with stubbed boundaries. The remaining
    // 5 need infrastructure the harness intentionally does not stub:
    //   - init-project + autonomous: LoadSkills needs the real skill
    //     catalog from TestSkillsRoot; harness uses a stub IPromptCatalog
    //     that doesn't enumerate skills.
    //   - api-security-scan: SpawnSpectral reads spectral.yaml from disk
    //     next to agentsmith.yml.
    //   - legal-analysis: BootstrapDocument lists workspace files + calls
    //     MarkItDown for PDF→markdown; StubSandbox returns empty file list.
    //   - skill-manager: pre-existing preset-shape bug (LoadContext at
    //     step 2 without CheckoutSource upstream — see
    //     PipelinePresetContextContractTests.KnownBrokenPresets).
    // Adding any of these means widening the stub surface or wiring the
    // real catalog/ruleset into the harness; each is a focused follow-up.
    public static readonly IEnumerable<object[]> AllPresets =
    [
        ["mad-discussion"],
        ["fix-bug"],
        ["fix-no-test"],
        ["add-feature"],
        ["security-scan"],
    ];

    [Theory]
    [MemberData(nameof(AllPresets))]
    public async Task Preset_RunsEndToEnd_WithStubs(string presetName)
    {
        await using var harness = new PipelineE2EHarness();
        var result = await harness.RunPresetAsync(presetName);

        if (!result.IsSuccess)
        {
            var inner = result.Exception is null
                ? string.Empty
                : $" inner={result.Exception.GetType().Name}: {result.Exception}";
            output.WriteLine($"Preset '{presetName}' failed: {result.Message}{inner}");
        }
        result.IsSuccess.Should().BeTrue(
            $"every preset must complete with stubbed boundaries. '{presetName}' returned: {result.Message}");
    }
}
