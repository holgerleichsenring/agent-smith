using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// 2026-09-13-5fa0: the raw-to-editable projection of one project, carved out of
/// <see cref="ConfigCatalogMapper"/> — which sat exactly at its file-length baseline and
/// so could not take the templates list without giving something up first.
/// </summary>
internal static class ProjectEntityMapping
{
    public static ProjectEntity ToProject(string id, RawProjectEntry project)
    {
        var pipelines = project.Pipelines.Count > 0
            ? project.Pipelines.Select(p => p.Name).ToList()
            : !string.IsNullOrWhiteSpace(project.Pipeline) ? [project.Pipeline] : new List<string>();
        return new ProjectEntity(
            id,
            project.Agent,
            project.Tracker,
            project.Repos.Select(r => r.Ref).ToList(),
            string.IsNullOrWhiteSpace(project.Pipeline) ? null : project.Pipeline,
            pipelines,
            ToResolution(project),
            project.DefaultPipeline,
            ToTemplates(project),
            ToSandbox(project));
    }

    /// <summary>
    /// 2026-09-22-6968: the six scalar sandbox overrides exactly as stored. NULL when the
    /// project declares no sandbox block at all — absence, not a copy of the process-wide
    /// defaults, because the form has to tell "this project says nothing" from "this project
    /// pins the same number the global one happens to hold".
    /// </summary>
    public static ProjectSandbox? ToSandbox(RawProjectEntry project) =>
        project.Sandbox is not { } sandbox
            ? null
            : new ProjectSandbox(
                sandbox.ToolchainImage,
                sandbox.StepTimeoutSeconds,
                sandbox.RunCommandTimeoutSeconds,
                sandbox.AgentRegistry,
                sandbox.AgentVersion,
                sandbox.HoldSeconds,
                ToStructured(sandbox));

    /// <summary>
    /// 2026-09-22-6c46: the structured three, always PRESENT on the way out — the studio is
    /// being told what the stored project holds and "none of the three" is an answer, the
    /// same way <see cref="ToTemplates"/> always returns a list. Absence means something
    /// else entirely on the way IN: see <see cref="ProjectSandboxStructured"/>. Each
    /// collection is copied, so nothing the studio hands back can alias the stored block.
    /// </summary>
    private static ProjectSandboxStructured ToStructured(SandboxConfig sandbox) =>
        new(sandbox.Resources is { } r ? r with { } : null,
            sandbox.Images is { Count: > 0 } images ? new Dictionary<string, string>(images) : null,
            ToSecrets(sandbox.Secrets));

    /// <summary>NAMES only — the secret's name and the keys taken from it. There is no
    /// value on this path to copy, by design.</summary>
    private static SandboxSecrets? ToSecrets(SandboxSecrets? secrets) =>
        secrets is null
            ? null
            : new SandboxSecrets
            {
                Env = secrets.Env is { Count: > 0 } env ? new Dictionary<string, string>(env) : null,
                Files = secrets.Files is { Count: > 0 } files
                    ? [.. files.Select(f => new SandboxSecretFile { Mount = f.Mount, Secret = f.Secret, Key = f.Key })]
                    : null,
            };

    /// <summary>
    /// Always a list, never null, on the way OUT: the studio is being told what the stored
    /// project holds, and "none" is an answer. Null on the way IN means something else
    /// entirely — see ProjectEntity.Templates.
    /// </summary>
    public static IReadOnlyList<TemplateReference> ToTemplates(RawProjectEntry project) =>
        [.. project.Templates.Select(t => new TemplateReference(
            t.Context, t.Project, t.Repo, t.TemplateContext, t.Revision, t.ContextRepo))];

    public static List<RawTemplateEntry> ToRaw(IReadOnlyList<TemplateReference> templates) =>
        [.. templates.Select(t => new RawTemplateEntry
        {
            Context = t.Context,
            Project = t.Project,
            Repo = t.Repo,
            TemplateContext = t.TemplateContext,
            Revision = t.Revision,
            ContextRepo = t.ContextRepo,
        })];

    // p0345c: surface the flat resolution shorthand; when the project instead
    // declares a full trigger wrapper, surface ITS resolution read-only so the
    // studio shows how the project actually routes either way.
    private static ProjectResolution? ToResolution(RawProjectEntry project)
    {
        if (project.Resolution is { Count: > 0 } shorthand)
        {
            var first = shorthand.First();
            return new ProjectResolution(first.Key, first.Value);
        }
        var wrapperResolution = new WebhookTriggerConfig?[]
            {
                project.JiraTrigger, project.GithubTrigger,
                project.GitlabTrigger, project.AzuredevopsTrigger,
            }
            .FirstOrDefault(t => t?.ProjectResolution is not null)?.ProjectResolution;
        return wrapperResolution is null
            ? null
            : new ProjectResolution(
                Contracts.Services.ConfigStudioCapabilities.WireName(wrapperResolution.Strategy),
                wrapperResolution.Value);
    }
}
