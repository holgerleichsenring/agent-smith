using System.Text.Json.Nodes;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using YamlDotNet.RepresentationModel;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// 2026-11-23-c81ec: the target's Argo CD and cluster, faked for a composition run the way auto-sync behaves — an
/// Application poc-&lt;name&gt; exists while deploy/&lt;name&gt;/ is on main, is Synced and Healthy, and its live
/// Deployment is exactly what main's deployment.yaml declares (image, checksum annotation, replicas), all of it ready.
/// So whichever PoC's commit moved the branch last, each follow reads its own folder.
/// </summary>
internal sealed class PocHarnessArgo(Func<string[], Task<string>> origin) : IArgoCdClientFactory, IArgoCdClient
{
    internal List<JsonObject> Upserts { get; } = [];

    internal int Reads { get; private set; }

    public IArgoCdClient Create(PocArgoConfig argo) => this;

    public IArgoCdClient Create(PocArgoConfig argo, string token) => this;

    public Task<ArgoProbe> ProbeAsync(CancellationToken cancellationToken) => Task.FromResult(new ArgoProbe(true, "harness"));

    public async Task<ArgoApplicationState?> GetApplicationAsync(string name, CancellationToken cancellationToken)
    {
        Reads++;
        return await DeploymentAsync(name) is null ? null : new ArgoApplicationState("Synced", "Healthy", null, "Succeeded", null);
    }

    public Task<ArgoApplicationState?> RefreshAsync(string name, CancellationToken cancellationToken) => GetApplicationAsync(name, cancellationToken);

    public async Task<ArgoLiveDeployment?> GetLiveDeploymentAsync(string application, string ns, string name, CancellationToken cancellationToken)
    {
        if (await DeploymentAsync(application) is not { } text) return null;
        var stream = new YamlStream();
        stream.Load(new StringReader(text));
        var root = (YamlMappingNode)stream.Documents[0].RootNode;
        var spec = (YamlMappingNode)root["spec"];
        var template = (YamlMappingNode)spec["template"];
        var annotations = (YamlMappingNode)((YamlMappingNode)template["metadata"])["annotations"];
        var container = ((YamlSequenceNode)((YamlMappingNode)template["spec"])["containers"]).Cast<YamlMappingNode>()
            .First(c => ((YamlScalarNode)c["name"]).Value == "app");
        var replicas = int.Parse(((YamlScalarNode)spec["replicas"]).Value!, System.Globalization.CultureInfo.InvariantCulture);
        return new ArgoLiveDeployment(((YamlScalarNode)container["image"]).Value,
            ((YamlScalarNode)annotations["agent-smith.io/variables-checksum"]).Value, replicas, 1, 1, replicas, replicas);
    }

    public Task<JsonObject?> GetApplicationSetAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(Upserts.LastOrDefault());

    public Task UpsertApplicationSetAsync(JsonObject applicationSet, CancellationToken cancellationToken)
    {
        Upserts.Add(applicationSet);
        return Task.CompletedTask;
    }

    private async Task<string?> DeploymentAsync(string application)
    {
        var text = await origin(["show", $"main:deploy/{application["poc-".Length..]}/deployment.yaml"]);
        return text.Contains("kind: Deployment", StringComparison.Ordinal) ? text : null;
    }
}
