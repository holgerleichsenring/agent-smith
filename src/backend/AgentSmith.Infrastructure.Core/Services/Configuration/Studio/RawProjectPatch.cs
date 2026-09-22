using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// 2026-09-22-6968: the studio-entity -&gt; raw-project patch, carved out of
/// <see cref="RawConfigPatch"/> — which sits over the file-length limit and so could not
/// take the sandbox block without giving something up first. Same semantics as before:
/// the EXISTING raw entry is mutated, so every section the entity says nothing about
/// survives the save.
/// </summary>
public static class RawProjectPatch
{
    public static RawProjectEntry Apply(ProjectEntity entity, RawProjectEntry? existing)
    {
        var project = existing ?? new RawProjectEntry();
        project.Agent = entity.Agent;
        project.Tracker = entity.Tracker;
        project.Repos = entity.Repos.Select(r => new RawRepoRef(r)).ToList();
        if (!string.IsNullOrWhiteSpace(entity.Pipeline)) project.Pipeline = entity.Pipeline!;
        if (entity.DefaultPipeline is not null) // p0392
            project.DefaultPipeline = string.IsNullOrWhiteSpace(entity.DefaultPipeline)
                ? null : entity.DefaultPipeline;
        // 2026-09-13-5fa0: NOT unconditional, unlike Repos three lines up — a client that
        // constructs a ProjectEntity without this field would otherwise wipe the stored
        // declaration, which is exactly how default_branch and consumes are already lost.
        if (entity.Templates is { } templates)
            project.Templates = ProjectEntityMapping.ToRaw(templates);
        if (entity.Resolution is { } resolution)
            project.Resolution = new Dictionary<string, string> { [resolution.Strategy] = resolution.Value };
        if (entity.Sandbox is { } sandbox) ApplySandbox(sandbox, project);
        ApplyPipelines(entity, project, existing);
        return project;
    }

    /// <summary>
    /// 2026-09-22-6968: written only when the entity CARRIES a block — until this phase the
    /// stored sandbox block survived a studio save by never being mentioned at all, and the
    /// moment the projection carries it a save becomes able to destroy it. Within a sent
    /// block each scalar is written AS GIVEN, null included: that is how the form clears an
    /// override back to inherited. The structured three are never assigned, so they survive
    /// a save through this block the way the whole block used to.
    /// </summary>
    private static void ApplySandbox(ProjectSandbox sandbox, RawProjectEntry project)
    {
        var block = project.Sandbox ??= new SandboxConfig();
        block.ToolchainImage = sandbox.ToolchainImage;
        block.StepTimeoutSeconds = sandbox.StepTimeoutSeconds;
        block.RunCommandTimeoutSeconds = sandbox.RunCommandTimeoutSeconds;
        block.AgentRegistry = sandbox.AgentRegistry;
        block.AgentVersion = sandbox.AgentVersion;
    }

    // 2026-09-16-74a2: the UNION of the names given and the default, never one or the
    // other. A default naming an undeclared pipeline is a BLOCKING startup finding that
    // disables the project, so the default is always written into the list; and the list
    // is not replaced by it, because each stored entry carries its own agent, skills
    // path, principles path and confidence threshold — sending only the default would
    // delete those silently, which is how default_branch and consumes are already lost.
    private static void ApplyPipelines(
        ProjectEntity entity, RawProjectEntry project, RawProjectEntry? existing)
    {
        var names = PipelineNames(entity);
        if (names.Count == 0) return;
        project.Pipelines = names
            .Select(name => existing?.Pipelines.FirstOrDefault(p => p.Name == name) ?? new RawPipelineEntry { Name = name })
            .ToList();
    }

    private static List<string> PipelineNames(ProjectEntity entity)
    {
        List<string> names = [.. entity.Pipelines];
        var fallback = entity.DefaultPipeline;
        if (!string.IsNullOrWhiteSpace(fallback)
            && !names.Any(n => string.Equals(n, fallback, StringComparison.OrdinalIgnoreCase)))
            names.Add(fallback);
        return names;
    }
}
