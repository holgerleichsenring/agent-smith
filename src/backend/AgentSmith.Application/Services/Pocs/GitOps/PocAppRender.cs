using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>
/// 2026-11-23-c81eb: renders a PoC's deploy folder from the app template — placeholders filled, each file parsed as a
/// YAML stream (one resource per document, kept in file order), the fields agent smith owns set as typed nodes on the
/// Deployment and the Ingress 'app', string fields quoted, configmap.yaml generated, then <see cref="PocManifestPolicy"/>.
/// Every file is written in one stable serialisation, so equal input renders equal bytes.
/// </summary>
public sealed class PocAppRender(
    PocDeploymentFields deployment, PocIngressFields ingress, PocConfigMapRender configMap, PocManifestPolicy policy)
{
    public (IReadOnlyList<PocRenderedFile>? Files, string? Refusal) Render(PocTemplate template, PocRenderInput input)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(input);
        var reserved = input.Seal.NeededFiles.Concat(input.Seal.Files.Select(f => f.FileName)).Append(PocManifestNames.ConfigMapFile).ToHashSet();
        if (template.Files.FirstOrDefault(f => reserved.Contains(f.Name)) is { } taken)
            return (null, $"the app template's {taken.Name} has the name of a file agent smith writes into the deploy folder; rename it");
        var (files, unreadable) = Parse(template, input);
        if (files is null) return (null, unreadable);
        if (Owned(files, input) is { } refused) return (null, refused);
        files.Add(configMap.Render(input.Namespace, input.Plain));
        foreach (var document in files.SelectMany(f => f.Documents)) PocStringFields.Requote(document);
        var refusals = policy.Check(files, input.Namespace);
        if (refusals.Count > 0) return (null, $"the render breaks the PoC hardening: {string.Join("; ", refusals)}");
        return (files.Select(f => new PocRenderedFile(f.Name, PocYamlWriter.Write(f.Documents))).ToList(), null);
    }

    private static (List<PocManifestFile>?, string?) Parse(PocTemplate template, PocRenderInput input)
    {
        var files = new List<PocManifestFile>();
        foreach (var file in template.Files)
        {
            var stream = new YamlStream();
            try { stream.Load(new StringReader(PocPlaceholders.Fill(file.Text, input))); }
            catch (YamlException ex) { return (null, $"the app template's {file.Name} is not readable YAML ({ex.Start}: {ex.Message})"); }
            var roots = stream.Documents.Select(d => d.RootNode).Where(r => r is not YamlScalarNode { Value: null or "" }).ToList();
            if (roots.FirstOrDefault(r => r is not YamlMappingNode) is not null)
                return (null, $"the app template's {file.Name} holds a document that is not one resource (a mapping)");
            files.Add(new PocManifestFile(file.Name, roots.Cast<YamlMappingNode>().ToList()));
        }
        return (files, null);
    }

    private string? Owned(List<PocManifestFile> files, PocRenderInput input)
    {
        var app = PocManifestNames.App;
        if (Find(files, "Deployment") is not { } deploy) return $"the app template has no Deployment '{app}'";
        if (deployment.Apply(deploy.Document, input) is { } badDeployment) return $"{deploy.File}: {badDeployment}";
        if (Find(files, "Ingress") is not { } route) return $"the app template has no Ingress '{app}'; every PoC is reached through it";
        return ingress.Apply(route.Document, input) is { } badIngress ? $"{route.File}: {badIngress}" : null;
    }

    private static (string File, YamlMappingNode Document)? Find(List<PocManifestFile> files, string kind) =>
        files.Select(f => (f.Name, Document: f.Find(kind, PocManifestNames.App))).FirstOrDefault(f => f.Document is not null)
            is { Document: { } document } found ? (found.Name, document) : null;
}
