using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199 init-project coverage. Currently deferred to p0199b — see the
/// Skip facts below for the reason. Replacing the [Fact(Skip=...)] with
/// [Fact] is the work order for p0199b: either seed a real
/// RemoteContextDiscovery so BootstrapDiscover takes its skip path, OR
/// register a synthetic discovery skill in AvailableRoles and script a
/// JSON discovery response per RepoConnection.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class InitProjectTests
{
    [Fact(Skip = "Deferred to p0199b: BootstrapDiscover requires either a real " +
        "RemoteContextDiscovery (impossible without a non-empty source.Url so " +
        "SandboxLanguageResolver can list .agentsmith/contexts/) OR an " +
        "AvailableRoles entry with output_schema=discovery plus a scripted " +
        "JSON DiscoveryOutput response. The harness's StubSourceProvider " +
        "returns Local + empty Url; the synthetic-default discovery fails " +
        "the IsRealDiscovery check and falls through to the LLM path. " +
        "Honest scope-slicing per spec: documented and visible, not " +
        "silently faked.")]
    public Task InitProject_RealHandlerChain_PipelineGreen() => Task.CompletedTask;
}
