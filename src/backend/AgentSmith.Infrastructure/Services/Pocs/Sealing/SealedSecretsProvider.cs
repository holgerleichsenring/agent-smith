using System.Text;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Infrastructure.Services.Pocs.Sealing;

/// <summary>
/// 2026-11-23-933dg: the Sealed Secrets provider — every entry sealed separately to the target's certificate under the
/// order's strict-scope label, base64, into one SealedSecret manifest named '&lt;name&gt;.sealed.yaml'. The values leave
/// their holders only into the sealer.
/// </summary>
public sealed class SealedSecretsProvider(KubeSealer sealer, KubeSealedSecretManifest manifest) : IPocSecretProvider
{
    public string Name => PocSecretsConfig.SealedSecrets;

    public PocSecretFile Render(PocSecretOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);
        var key = SealingCertificate.Parse(order.Certificate).PublicKey;
        var label = KubeSealer.Label(order.Namespace, order.Name);
        var encrypted = order.Entries.Select(entry => KeyValuePair.Create(entry.Key,
            Convert.ToBase64String(sealer.Seal(key, Encoding.UTF8.GetBytes(entry.Value.Reveal()), label))));
        return new PocSecretFile(FileName(order.Name), manifest.Render(order.Namespace, order.Name, order.Type, encrypted));
    }

    public string FileNameOf(string name) => FileName(name);

    public static string FileName(string name) => $"{name}.sealed.yaml";
}
