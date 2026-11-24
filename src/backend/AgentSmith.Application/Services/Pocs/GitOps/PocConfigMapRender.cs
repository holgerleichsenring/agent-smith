using AgentSmith.Contracts.Pocs;
using YamlDotNet.RepresentationModel;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: the generated configmap.yaml — ConfigMap poc-config in the PoC's namespace, one data entry
/// per plain variable in name order, each value a string (set_by and set_at are not rendered). Wave 0, like everything
/// but the Deployment.</summary>
public sealed class PocConfigMapRender
{
    public PocManifestFile Render(string @namespace, IReadOnlyList<PocPlainVariable> plain)
    {
        ArgumentNullException.ThrowIfNull(plain);
        var data = new YamlMappingNode();
        foreach (var variable in plain.OrderBy(v => v.Name, StringComparer.Ordinal))
            data.Add(PocYamlText.Text(variable.Name), PocYamlText.Text(variable.Value));
        var document = new YamlMappingNode
        {
            { "apiVersion", "v1" }, { "kind", "ConfigMap" },
            { "metadata", new YamlMappingNode { { "name", PocYamlText.Text(PocSecretNames.Config) }, { "namespace", PocYamlText.Text(@namespace) } } },
            { "data", data },
        };
        return new PocManifestFile(PocManifestNames.ConfigMapFile, [document]);
    }
}
