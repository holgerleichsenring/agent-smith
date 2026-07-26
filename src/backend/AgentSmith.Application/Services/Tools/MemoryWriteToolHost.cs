using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Memory;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0380: remember(type, name, description, body) — writes ONE memory file +
/// updates memory/MEMORY.md. A PROPOSAL tool on every master surface: it only
/// writes run-record-class paths (.agentsmith/memory/), so it never trips the
/// keystone, and a feedback/policy entry is flagged status: proposed — pending
/// operator ratification, never silently policy.
/// </summary>
public sealed class MemoryWriteToolHost(MemoryStore store) : IToolHost
{
    private const string ProposedStatus = "proposed";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(Remember, name: "remember")];
    }

    [Description("Proposes a new experiential memory (or updates one by name). One fact per " +
                 "entry; check recall first and update rather than duplicate. A 'feedback' " +
                 "entry becomes policy only after operator ratification.")]
    public async Task<string> Remember(
        [Description("Memory type: feedback, project, or reference.")] string type,
        [Description("Kebab-case slug, e.g. 'no-wrapper-shims'. Becomes the file name.")] string name,
        [Description("One-line description for the index.")] string description,
        [Description("The Markdown body — the fact itself, compact.")] string body,
        CancellationToken ct = default)
    {
        if (!MemoryEntryTypes.TryParse(type, out var entryType))
            return $"Error: invalid type '{type}' — use feedback, project, or reference.";
        var slug = MemorySlug.ToKebab(name);
        if (slug.Length == 0)
            return $"Error: '{name}' yields no usable kebab-case slug.";

        var status = entryType == MemoryEntryType.Feedback ? ProposedStatus : null;
        await store.UpsertAsync(new MemoryEntry(slug, description, entryType, body, status), ct);
        return entryType == MemoryEntryType.Feedback
            ? $"Memory '{slug}' recorded as PROPOSED feedback — it becomes policy only after operator ratification."
            : $"Memory '{slug}' recorded ({MemoryEntryTypes.ToSlug(entryType)}).";
    }
}
