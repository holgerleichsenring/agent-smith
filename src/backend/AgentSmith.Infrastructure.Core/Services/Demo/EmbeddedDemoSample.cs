using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Demo;

/// <summary>
/// p0326: reads the demo sample project baked into this assembly by the
/// PackDemoSampleProject MSBuild step (sources checked in under
/// Resources/DemoSampleProject/, packed per build — same embedded-resource
/// shape as the p0325 skills catalog).
/// </summary>
public sealed class EmbeddedDemoSample : IEmbeddedDemoSample
{
    internal const string ResourceName = "AgentSmith.DemoSampleProject.tar.gz";

    public Stream Open() =>
        typeof(EmbeddedDemoSample).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded demo sample resource '{ResourceName}' not found in " +
                "AgentSmith.Infrastructure.Core — the PackDemoSampleProject build step did not run.");
}
