using System.ComponentModel;
using AgentSmith.Application.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-25-9324b: dispute_memory — a master names a memory that led it wrong, and why. The dispute counts
/// against the memory's standing; enough of it and the memory leaves the index every master is shown.
/// </summary>
public sealed class MemoryDisputeToolHost(MasterMemoryUse memory) : IToolHost
{
    public const string Name = "dispute_memory";
    public const int MaxReasonChars = 500;

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(DisputeMemory, name: Name)];
    }

    [Description("Disputes a stored memory that led you wrong: what it said was not so here, or following it broke " +
                 "the work. A memory is a hint with a track record, never a rule; a dispute counts against that " +
                 "record, and a memory whose record turns against it is no longer shown. Say what was wrong, in a " +
                 "sentence or two. Call it when you find the memory wrong, not for a memory you merely did not use.")]
    public async Task<string> DisputeMemory(
        [Description("The memory's slug, as the index lists it.")] string slug,
        [Description("What the memory got wrong, and how you found out.")] string reason,
        [Description("The repository whose memory it is. Needed when more than one repository stores that slug.")]
        string? repository = null,
        CancellationToken ct = default)
    {
        var why = reason?.Trim() ?? string.Empty;
        if (why.Length == 0) return "Error: dispute_memory needs a reason — what the memory got wrong.";
        if (why.Length > MaxReasonChars)
            return $"Error: the reason is {why.Length} characters, at most {MaxReasonChars}.";
        var read = await memory.ResolveAsync([repository is null ? slug : $"{repository}/{slug}"], ct);
        if (read.Problems.Count > 0) return $"Error: nothing was disputed: {string.Join("; ", read.Problems)}.";
        var cited = read.Citations[0];
        return await memory.RecordDisputeAsync(cited, why, ct)
            ? $"Dispute of '{cited.Slug}' in {cited.Repository} recorded; it counts against the memory's standing."
            : $"Error: the dispute of '{cited.Slug}' could not be recorded.";
    }
}
