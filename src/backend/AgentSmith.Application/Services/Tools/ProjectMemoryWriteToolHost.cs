using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Memory;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Memory;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-10-fcd7b: a design turn's remember — what a person states that holds beyond this turn, kept on the
/// PROJECT with where it was said: this conversation, the person's latest message by its transcript index, and
/// its author. Offered only to a turn whose poster may start runs (<see cref="DesignWorkToolGate"/>).
/// </summary>
public sealed class ProjectMemoryWriteToolHost(MemorySources sources, PipelineContext pipeline) : IToolHost
{
    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(Remember, name: "remember")];
    }

    [Description("Keeps what the person said that holds beyond this turn — how they want work done, a constraint of " +
                 "the project, a fact no repository records — as a memory of the PROJECT, which every later " +
                 "conversation and run of it sees, quoted with this conversation as its source. One fact per entry; " +
                 "an existing name is updated. A memory is a hint with a track record, never a rule.")]
    public async Task<string> Remember(
        [Description("Memory type: feedback (how the person wants work done), project, or reference.")] string type,
        [Description("Kebab-case slug, e.g. 'never-drop-a-recorded-feature'.")] string name,
        [Description("One line for the index, at most 200 characters.")] string description,
        [Description("The fact itself, compact, at most 4000 characters.")] string body,
        CancellationToken ct = default)
    {
        if (!MemoryEntryTypes.TryParse(type, out var entryType))
            return $"Error: invalid type '{type}' — use feedback, project, or reference.";
        var slug = MemorySlug.ToKebab(name);
        if (slug.Length == 0) return $"Error: '{name}' yields no usable kebab-case slug.";
        if (await sources.ProjectAsync(pipeline, ct) is not { } project)
            return "Not recorded: this project has no store for memories here.";
        var entry = new MemoryEntry(slug, description.Trim(), entryType, body.Trim());
        var record = new ProjectMemoryRecord(slug, type, entry.Description, entry.Body, string.Empty, string.Empty, null, null);
        if (ProjectMemoryLimits.Refusal(record) is { } refusal) return $"Not recorded: {refusal}.";
        await project.Store.UpsertAsync(entry, ct, Provenance());
        return $"Memory '{slug}' kept on the project ({MemoryEntryTypes.ToSlug(entryType)}), with this conversation " +
               "as its source.";
    }

    private MemoryProvenance Provenance()
    {
        var conversation = pipeline.TryGet<string>(ContextKeys.DialogueJobId, out var job) ? job ?? string.Empty : string.Empty;
        var transcript = pipeline.TryGet<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript, out var turns)
            ? turns ?? [] : [];
        var index = transcript.Count - 1;
        while (index >= 0 && transcript[index].Role != SpecDialogTurn.UserRole) index--;
        return new MemoryProvenance(ProjectMemorySourceKinds.Conversation, conversation,
            index < 0 ? null : index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            index < 0 ? null : transcript[index].Author);
    }
}
