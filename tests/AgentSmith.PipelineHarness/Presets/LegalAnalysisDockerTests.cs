using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0199c docker-tier legal-analysis coverage — deferred.
///
/// BootstrapDocumentHandler runs <c>markitdown</c> inside the sandbox to
/// convert the source document to markdown. The harness's sandbox image
/// (mcr.microsoft.com/dotnet/sdk:8.0 — resolved by SandboxLanguageResolver
/// from the csharp fixture) doesn't ship markitdown, so the step fails
/// before AgenticMaster ever runs. Closing the gap needs either a
/// markitdown-equipped sandbox image (its own follow-up) or a per-test
/// boundary swap that replaces the markitdown subprocess with a host-side
/// stub (changes the legal-analysis-specific contract under test). Honest
/// scope-slicing per spec: documented gap, not silent skip.
/// </summary>
[Trait("Category", "PipelineHarness")]
[Trait("Tier", "Docker")]
public sealed class LegalAnalysisDockerTests(ITestOutputHelper output)
{
    [Fact]
    public void Docker_LegalAnalysis_DeferredToMarkdownImage_LoudSkip()
    {
        output.WriteLine(
            "DOCKER TIER NOT EXERCISED for legal-analysis — deferred. " +
            "BootstrapDocumentHandler runs `markitdown` inside the sandbox and the " +
            "default dotnet/sdk:8.0 toolchain image doesn't ship it. The fast tier " +
            "covers this preset's handler chain over StubSandbox; the docker tier " +
            "needs a markitdown-equipped sandbox image, which is its own follow-up.");
    }
}
