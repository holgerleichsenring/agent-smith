using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Extensions;

/// <summary>
/// 2026-09-04-cf3d: renders the loaded context documents into the one string the prompts
/// bind. One document renders verbatim, so a single-context repository keeps its prompt
/// byte-identical. Several render as sections: a section is headed by its sandbox key when
/// the run has more than one sandbox (the heading multi-repo runs always had), and by its
/// context and workdir when its sandbox contributed more than one document — that is how the
/// master is told which rules govern which subtree.
/// </summary>
public static class ContextDocumentRendering
{
    private const string SectionSeparator = "\n\n---\n\n";

    public static string RenderLabelled(this IReadOnlyList<ContextDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        if (documents.Count == 0) return string.Empty;
        if (documents.Count == 1) return documents[0].Content;
        var perSandbox = documents
            .GroupBy(d => d.SandboxKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var manySandboxes = perSandbox.Count > 1;
        return string.Join(SectionSeparator, documents.Select(d =>
            $"## {Label(d, manySandboxes, perSandbox[d.SandboxKey] > 1)}\n\n{d.Content.TrimEnd()}"));
    }

    private static string Label(ContextDocument document, bool withSandbox, bool withContext)
    {
        var parts = new List<string>(2);
        if (withSandbox) parts.Add(document.SandboxKey);
        if (withContext && document.ContextName is not null)
            parts.Add($"Context: {document.ContextName} (workdir: {document.Workdir})");
        return string.Join(" — ", parts);
    }
}
