using System.Text.Json.Nodes;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Argo;

/// <summary>
/// 2026-11-23-c81ec: the ONE ApplicationSet of a PoC target — a git directories generator over &lt;deploy_root&gt;/* of
/// argo.repo_url at the source repo's default branch, requeued every argo.generator_requeue_seconds; per folder an
/// Application 'poc-{{path.basename}}' (= the PoC's namespace, since the folder is the PoC's name) in argo.project,
/// sourced from the folder and deployed to destination_server into that namespace, with automated sync, prune and
/// selfHeal, and the resources finalizer so a removed folder's resources are pruned with its Application.
/// </summary>
public sealed class PocApplicationSet
{
    public const string ResourcesFinalizer = "resources-finalizer.argocd.argoproj.io";

    public JsonObject Render(PocTargetConfig target, PocArgoConfig argo, string branch)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(argo);
        var name = $"{PocNamespace.Prefix}{{{{path.basename}}}}";
        return new JsonObject
        {
            ["apiVersion"] = "argoproj.io/v1alpha1",
            ["kind"] = "ApplicationSet",
            ["metadata"] = new JsonObject { ["name"] = argo.ApplicationSetName(target.Name), ["namespace"] = argo.Namespace },
            ["spec"] = new JsonObject
            {
                ["generators"] = new JsonArray(Generator(target, argo, branch)),
                ["template"] = Template(argo, branch, name),
            },
        };
    }

    private static JsonObject Generator(PocTargetConfig target, PocArgoConfig argo, string branch) => new()
    {
        ["git"] = new JsonObject
        {
            ["repoURL"] = argo.RepoUrl,
            ["revision"] = branch,
            ["directories"] = new JsonArray(new JsonObject { ["path"] = $"{target.DeployRoot.Trim('/')}/*" }),
            ["requeueAfterSeconds"] = argo.GeneratorRequeueSeconds,
        },
    };

    private static JsonObject Template(PocArgoConfig argo, string branch, string name) => new()
    {
        ["metadata"] = new JsonObject { ["name"] = name, ["finalizers"] = new JsonArray(ResourcesFinalizer) },
        ["spec"] = new JsonObject
        {
            ["project"] = argo.Project,
            ["source"] = new JsonObject { ["repoURL"] = argo.RepoUrl, ["targetRevision"] = branch, ["path"] = "{{path}}" },
            ["destination"] = new JsonObject { ["server"] = argo.DestinationServer, ["namespace"] = name },
            ["syncPolicy"] = new JsonObject { ["automated"] = new JsonObject { ["prune"] = true, ["selfHeal"] = true } },
        },
    };
}
