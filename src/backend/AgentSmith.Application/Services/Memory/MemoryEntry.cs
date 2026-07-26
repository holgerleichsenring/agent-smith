namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// p0380: one experiential memory — a single Markdown fact file under
/// .agentsmith/memory/. Name is the kebab-slug (= file name without .md),
/// Description is the one-line index entry, Body is the Markdown content
/// below the frontmatter. Status carries the ratification state for
/// feedback/policy entries ("proposed" until the operator ratifies); null
/// means no ratification workflow applies.
/// </summary>
public sealed record MemoryEntry(
    string Name,
    string Description,
    MemoryEntryType Type,
    string Body,
    string? Status = null);
