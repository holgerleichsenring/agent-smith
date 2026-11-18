using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// 2026-11-18-3863a: a project's template bindings between the raw document and the studio, both ways. Carved out of
/// <see cref="ProjectEntityMapping"/>, which sat at the line limit when the binding gained its kind and environments.
/// </summary>
internal static class TemplateReferenceMapping
{
    /// <summary>
    /// Always a list, never null, on the way OUT: the studio is being told what the stored
    /// project holds, and "none" is an answer. Null on the way IN means something else
    /// entirely — see ProjectEntity.Templates.
    /// </summary>
    public static IReadOnlyList<TemplateReference> ToTemplates(RawProjectEntry project) =>
        [.. project.Templates.Select(t => new TemplateReference(
            t.Context, t.Project, t.Repo, t.TemplateContext, t.Revision, t.ContextRepo, t.Kind,
            t.Environments is null ? null : [.. t.Environments.Select(ToReference)]))];

    public static List<RawTemplateEntry> ToRaw(IReadOnlyList<TemplateReference> templates) =>
        [.. templates.Select(t => new RawTemplateEntry
        {
            Context = t.Context,
            Project = t.Project,
            Repo = t.Repo,
            TemplateContext = t.TemplateContext,
            Revision = t.Revision,
            ContextRepo = t.ContextRepo,
            Kind = string.IsNullOrWhiteSpace(t.Kind) ? null : t.Kind,
            Environments = t.Environments is { Count: > 0 } environments ? [.. environments.Select(ToRaw)] : null,
        })];

    private static DeploymentEnvironmentReference ToReference(RawDeploymentEnvironment raw) =>
        new(raw.Name, raw.Repo, raw.Approval,
            raw.Parameters is null ? null : new Dictionary<string, string>(raw.Parameters, ConfigNames.Comparer));

    private static RawDeploymentEnvironment ToRaw(DeploymentEnvironmentReference environment) => new()
    {
        Name = environment.Name,
        Repo = string.IsNullOrWhiteSpace(environment.Repo) ? null : environment.Repo,
        Approval = environment.Approval,
        Parameters = environment.Parameters is { Count: > 0 } parameters ? new Dictionary<string, string>(parameters) : null,
    };
}
