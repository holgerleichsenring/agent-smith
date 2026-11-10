using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Memory;
using AgentSmith.Application.Services.Repositories;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Memory;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0380: remember(type, name, description, body) — writes ONE memory file + updates memory/MEMORY.md, on
/// every master surface: it only writes run-record-class paths (.agentsmith/memory/), so it never trips the
/// keystone.
/// <para>
/// 2026-10-12-f5f7c: the fact is written into the repository it is about, and only into one
/// whose commit lands this run — a memory-only change opens no pull request, so a write
/// anywhere else would never reach git. That case is answered, not written.
/// </para>
/// <para>2026-10-25-9324e: a memory is a hint with a track record, never a rule, and no type waits for a person:
/// the entry carries no status, and its standing is what the runs that rely on it make of it.</para>
/// <para>2026-11-10-fcd7c: '@project' keeps a lesson that holds beyond one repository on the PROJECT, in the
/// database, with the run and spec as its source and no landing commit needed. Offered only where a project store
/// exists: the description is built from the composition, so the CLI never advertises a target it refuses.</para>
/// </summary>
public sealed class MemoryWriteToolHost(
    IRunRepositories repositories, string repoRoot, ILogger? logger = null,
    MemorySources? sources = null, PipelineContext? pipeline = null)
    : IToolHost
{
    public const string NoProjectStore = "Not recorded: this installation keeps no project memory.";

    private const string Base =
        "Records a new experiential memory (or updates one by name) in the repository it is about. One fact per " +
        "entry; check recall first and update rather than duplicate. A memory is a hint with a track record, never " +
        "a rule: whether it holds is decided by the runs that rely on it (hand_forward relied_on) and by disputes " +
        "(dispute_memory), never by approval. Only a repository with a code change this run can take one.";

    private const string ProjectTarget =
        " A lesson that holds beyond this repository — how the project wants work done, a constraint every " +
        "repository shares — goes to repository '@project': it is kept on the project in the database, needs no " +
        "code change, and every later conversation and run of the project reads it.";

    public bool OffersProject => sources?.HasProjectStore == true;

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(Remember, new AIFunctionFactoryOptions
        {
            Name = "remember", Description = OffersProject ? Base + ProjectTarget : Base,
        })];
    }

    public async Task<string> Remember(
        [Description("Memory type: feedback, project, or reference.")] string type,
        [Description("Kebab-case slug, e.g. 'no-wrapper-shims'. Becomes the file name.")] string name,
        [Description("One-line description for the index.")] string description,
        [Description("The Markdown body — the fact itself, compact.")] string body,
        [Description("The repository this fact is about. Required when the run has more than one " +
                     "repository; omit it when there is one.")] string? repository = null,
        CancellationToken ct = default)
    {
        if (!MemoryEntryTypes.TryParse(type, out var entryType))
            return $"Error: invalid type '{type}' — use feedback, project, or reference.";
        var slug = MemorySlug.ToKebab(name);
        if (slug.Length == 0)
            return $"Error: '{name}' yields no usable kebab-case slug.";
        var entry = new MemoryEntry(slug, description, entryType, body);
        if (string.Equals(repository?.Trim(), ProjectMemorySource.Label, StringComparison.OrdinalIgnoreCase))
            return await RememberOnProjectAsync(entry, ct);
        var pick = RunRepositoryPick.From(repositories.Names(), repository);
        if (pick.Error is not null) return pick.Error;
        if (pick.Name is null || repositories.FilesOf(pick.Name) is not { } files)
            return "Error: this run has no repository to keep a memory in.";
        if (!await repositories.HasCodeChangeAsync(pick.Name, ct))
            return await NotLandingAsync(pick.Name, ct);

        await new FileMemoryStore(files, repoRoot, logger).UpsertAsync(entry, ct);
        return $"Memory '{slug}' recorded in {pick.Name} ({MemoryEntryTypes.ToSlug(entryType)}) — a hint whose " +
               "standing the runs that rely on it decide.";
    }

    private async Task<string> RememberOnProjectAsync(MemoryEntry entry, CancellationToken ct)
    {
        if (!OffersProject || pipeline is null || await sources!.ProjectAsync(pipeline, ct) is not { } project)
            return NoProjectStore;
        var record = new ProjectMemoryRecord(entry.Name, MemoryEntryTypes.ToSlug(entry.Type), entry.Description,
            entry.Body, ProjectMemorySourceKinds.Run, string.Empty, null, null);
        if (ProjectMemoryLimits.Refusal(record) is { } refusal) return $"Not recorded: {refusal}.";
        await project.Store.UpsertAsync(entry, ct, Provenance(pipeline));
        return $"Memory '{entry.Name}' kept on the project ({MemoryEntryTypes.ToSlug(entry.Type)}), with this run " +
               "as its source — a hint whose standing the runs that rely on it decide.";
    }

    private static MemoryProvenance Provenance(PipelineContext pipeline) => new(
        ProjectMemorySourceKinds.Run,
        pipeline.TryGet<string>(ContextKeys.RunId, out var run) ? run ?? string.Empty : string.Empty,
        pipeline.TryGet<SpecDraft>(ContextKeys.CurrentSpec, out var spec) ? spec?.SpecId : null,
        "coding master");

    private async Task<string> NotLandingAsync(string repository, CancellationToken ct)
    {
        var landing = new List<string>();
        foreach (var name in repositories.Names())
            if (await repositories.HasCodeChangeAsync(name, ct)) landing.Add(name);
        var instead = landing.Count == 0
            ? "No repository has a code change yet — remember once the work is in the repository it is about."
            : $"Repositories with a change this run: [{string.Join(", ", landing)}].";
        return $"Not recorded: '{repository}' has no code change this run, so no pull request would carry " +
               $"the memory. {instead}" + (OffersProject ? " A lesson for the whole project can go to '@project'." : string.Empty);
    }
}
