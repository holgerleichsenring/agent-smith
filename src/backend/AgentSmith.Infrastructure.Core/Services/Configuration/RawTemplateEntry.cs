namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-09-13-5fa0: raw YAML shape for one entry of a project's <c>templates:</c> list.
/// Flat with settable properties, the shape RawPipelineEntry uses — NOT the positional
/// RawRepoRef shape, which only works because a hand-written converter parses it.
/// </summary>
public sealed class RawTemplateEntry
{
    /// <summary>The context OF THIS PROJECT the binding applies to.</summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>The project the template belongs to.</summary>
    public string Project { get; set; } = string.Empty;

    /// <summary>A repo ref of THAT project — plain name or connection/Name.</summary>
    public string Repo { get; set; } = string.Empty;

    /// <summary>The context inside the template that this one is built after.</summary>
    public string TemplateContext { get; set; } = string.Empty;

    /// <summary>Opaque: a branch, a tag or a sha, verified where it is fetched.</summary>
    public string? Revision { get; set; }
}
