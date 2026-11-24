namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: the app template as the branch head holds it — the manifests rendered, and the files
/// under app/ that are not YAML and are skipped (said once in the step's outcome).</summary>
public sealed record PocTemplate(IReadOnlyList<PocTemplateFile> Files, IReadOnlyList<string> Skipped);
