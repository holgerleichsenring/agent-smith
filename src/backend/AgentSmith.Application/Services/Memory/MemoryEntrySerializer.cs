using System.Text;

namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// p0380: renders a <see cref="MemoryEntry"/> to its canonical file form —
/// frontmatter (name / description / metadata.type / optional status) plus
/// the Markdown body. The shape matches the methodology repo's shared memory
/// template so IDE sessions and agent runs write the identical store.
/// </summary>
public static class MemoryEntrySerializer
{
    public static string Serialize(MemoryEntry entry)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"name: {entry.Name}");
        sb.AppendLine($"description: {Sanitize(entry.Description)}");
        sb.AppendLine("metadata:");
        sb.AppendLine($"  type: {MemoryEntryTypes.ToSlug(entry.Type)}");
        if (entry.Status is not null)
            sb.AppendLine($"status: {entry.Status}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(entry.Body.Trim());
        return sb.ToString();
    }

    /// <summary>One-line guarantee: newlines in a description would break the
    /// frontmatter and the one-line-per-memory index contract.</summary>
    private static string Sanitize(string description) =>
        description.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
