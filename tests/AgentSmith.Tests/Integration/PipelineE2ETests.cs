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
        // legal-analysis: DeliverOutput requires non-empty CodeChanges
        // (the master skill is expected to write findings); with the
        // no-op LLM stub the master loop ends without writes. Needs
        // ChatClient scripting that emits a write_file tool call, or
        // pre-seeded CodeChanges — both change the test's "real flow"
        // semantics. Skipped here, planned as follow-up.
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
        // p0241: code-changing presets (fix-bug/fix-no-test/add-feature) cannot
        // ship a real change with the no-op LLM stub, so the keystone correctly
        // records them as FAILED — a business outcome, not a crash. The E2E
        // guarantee here is "no handler bug / no DI gap": the run reaches a
        // terminal result without an unhandled exception. Keystone success with a
        // real change is covered by the keystone unit tests + the Docker tier.
        var codeChanging = presetName is "fix-bug" or "fix-no-test" or "add-feature";
        if (codeChanging)
        {
            result.Exception.Should().BeNull(
                $"'{presetName}' must reach a terminal result without throwing: {result.Message}");
        }
        else
        {
            result.IsSuccess.Should().BeTrue(
                $"every non-code-changing preset must complete with stubbed boundaries. " +
                $"'{presetName}' returned: {result.Message}");
        }
    }
}
