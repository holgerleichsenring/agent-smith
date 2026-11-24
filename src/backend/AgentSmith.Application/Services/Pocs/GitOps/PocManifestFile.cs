using YamlDotNet.RepresentationModel;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: one template file parsed — its name and its documents in file order, each one resource,
/// changed in place by the render.</summary>
public sealed record PocManifestFile(string Name, IReadOnlyList<YamlMappingNode> Documents)
{
    public string? Kind(YamlMappingNode document) => PocYamlTree.Text(document, "kind");

    /// <summary>The document of <paramref name="kind"/> named <paramref name="name"/>, or null.</summary>
    public YamlMappingNode? Find(string kind, string name) => Documents.FirstOrDefault(d =>
        PocYamlTree.Text(d, "kind") == kind && PocYamlTree.MapOf(d, "metadata") is { } meta && PocYamlTree.Text(meta, "name") == name);
}
