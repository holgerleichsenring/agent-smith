using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>2026-11-18-9c1cd: remember with dispute_memory folded in.</summary>
public static partial class MergedTools
{
    /// <summary>remember with dispute_memory folded in as <c>dispute</c>.</summary>
    public static AIFunction Remember(
        Func<string, string, string, string, string?, CancellationToken, Task<string>>? write, string writeRefusal,
        MemoryDisputeToolHost? disputeHost, string toolDescription, bool withRepository) =>
        withRepository
            ? AIFunctionFactory.Create(
                async ([Description("Kebab-case slug, e.g. 'no-wrapper-shims' — the memory to write, or the one you dispute.")] string name,
                       [Description("Memory type: feedback, project, or reference. Not needed for a dispute.")] string? type = null,
                       [Description("One-line description for the index. Not needed for a dispute.")] string? description = null,
                       [Description("The Markdown body — the fact itself, compact. Not needed for a dispute.")] string? body = null,
                       [Description("The repository the memory is (or goes) in. Required when the run has more than one repository.")] string? repository = null,
                       [Description("Disputes the stored memory `name` instead of writing: what it got wrong and how you found out, a sentence or two. Only for a memory that led you wrong, not one you merely did not use.")] string? dispute = null,
                       CancellationToken ct = default) =>
                    await RememberAsync(write, writeRefusal, disputeHost, dispute, name, type, description, body, repository, ct),
                "remember", toolDescription)
            : AIFunctionFactory.Create(
                async ([Description("Kebab-case slug, e.g. 'never-drop-a-recorded-feature' — the memory to write, or the one you dispute.")] string name,
                       [Description("Memory type: feedback (how the person wants work done), project, or reference. Not needed for a dispute.")] string? type = null,
                       [Description("One line for the index, at most 200 characters. Not needed for a dispute.")] string? description = null,
                       [Description("The fact itself, compact, at most 4000 characters. Not needed for a dispute.")] string? body = null,
                       [Description("Disputes the stored memory `name` instead of writing: what it got wrong and how you found out.")] string? dispute = null,
                       CancellationToken ct = default) =>
                    await RememberAsync(write, writeRefusal, disputeHost, dispute, name, type, description, body, null, ct),
                "remember", toolDescription);

    private static async Task<string> RememberAsync(
        Func<string, string, string, string, string?, CancellationToken, Task<string>>? write, string writeRefusal,
        MemoryDisputeToolHost? disputeHost, string? dispute,
        string name, string? type, string? description, string? body, string? repository, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(dispute))
            return disputeHost is null
                ? "Not recorded: a dispute counts against a memory's record on a run, and this one keeps no record of memory use."
                : await disputeHost.DisputeMemory(name, dispute, repository, ct);
        if (write is null) return writeRefusal;
        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(body))
            return "Error: a memory needs type, description and body (or pass dispute to dispute one).";
        return await write(type, name, description, body, repository, ct);
    }
}
