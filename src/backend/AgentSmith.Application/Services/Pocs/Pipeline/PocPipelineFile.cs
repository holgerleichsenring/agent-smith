namespace AgentSmith.Application.Services.Pocs.Pipeline;

/// <summary>2026-11-23-933dd: the build pipeline's definition for one host — where it lives in the source repo and its
/// text, which starts with <see cref="PocPipelineTemplate.Marker"/>.</summary>
public sealed record PocPipelineFile(string Path, string Content);
