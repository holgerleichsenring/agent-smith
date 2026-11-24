using System.Text;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Infrastructure.Services.Pocs.Sealing;

/// <summary>
/// 2026-11-23-933dg: a bitnami.com/v1alpha1 SealedSecret for one PoC secret — strict scope (the default, so no scope
/// annotation), each entry under spec.encryptedData in key order, and the template naming the Secret the controller
/// writes with its type (Opaque, or kubernetes.io/dockerconfigjson for the pull secret).
/// </summary>
public sealed class KubeSealedSecretManifest
{
    public string Render(string ns, string name, string type, IEnumerable<KeyValuePair<string, string>> encrypted)
    {
        ArgumentNullException.ThrowIfNull(encrypted);
        var yaml = new StringBuilder()
            .Append("# agent-smith: sealed for ").Append(ns).Append('/').Append(name).Append('\n')
            .Append("apiVersion: bitnami.com/v1alpha1\nkind: SealedSecret\nmetadata:\n")
            .Append("  name: ").Append(name).Append("\n  namespace: ").Append(ns).Append('\n')
            .Append("spec:\n  encryptedData:\n");
        foreach (var (key, value) in encrypted.OrderBy(e => e.Key, StringComparer.Ordinal))
            yaml.Append("    \"").Append(key).Append("\": ").Append(value).Append('\n');
        return yaml.Append("  template:\n    metadata:\n      name: ").Append(name).Append("\n      namespace: ").Append(ns)
            .Append("\n    type: ").Append(Quoted(type)).Append('\n').ToString();
    }

    // A type with a slash is quoted, so no YAML reader takes it for anything but a string.
    private static string Quoted(string type) => type == PocSecretTypes.Opaque ? type : $"\"{type}\"";
}
