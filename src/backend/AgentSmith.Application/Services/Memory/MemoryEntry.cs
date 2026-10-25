namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// p0380: one experiential memory — a single Markdown fact file under
/// .agentsmith/memory/. Name is the kebab-slug (= file name without .md),
/// Description is the one-line index entry, Body is the Markdown content
/// below the frontmatter. Status is what an entry file written before
/// 2026-10-25-9324e may still carry; nothing writes one now — a memory's
/// standing comes from the runs' evidence, never from a ratification.
/// </summary>
public sealed record MemoryEntry(
    string Name,
    string Description,
    MemoryEntryType Type,
    string Body,
    string? Status = null);
