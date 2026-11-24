using System.Reflection;

namespace AgentSmith.Application.Services.Pocs.Pipeline;

/// <summary>2026-11-23-933dd: the build pipeline's pieces, embedded from Resources/PocPipeline — the per-host YAML, the
/// job scripts, Trivy's report template and gate policy.</summary>
public sealed class PocPipelineResources
{
    private const string Prefix = "AgentSmith.Application.Resources.PocPipeline.";

    public string Text(string name)
    {
        using var stream = typeof(PocPipelineResources).Assembly.GetManifestResourceStream(Prefix + name)
            ?? throw new InvalidOperationException($"The build pipeline piece '{name}' is not embedded in {Assembly.GetExecutingAssembly().GetName().Name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
    }
}
