using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Memory;
using AgentSmith.Application.Services.Repositories;
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
/// </summary>
public sealed class MemoryWriteToolHost(IRunRepositories repositories, string repoRoot, ILogger? logger = null)
    : IToolHost
{
    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(Remember, name: "remember")];
    }

    [Description("Records a new experiential memory (or updates one by name) in the repository it is about. " +
                 "One fact per entry; check recall first and update rather than duplicate. A memory is a hint " +
                 "with a track record, never a rule: whether it holds is decided by the runs that rely on it " +
                 "(hand_forward relied_on) and by disputes (dispute_memory), never by approval. Only a repository " +
                 "with a code change this run can take one.")]
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
        var pick = RunRepositoryPick.From(repositories.Names(), repository);
        if (pick.Error is not null) return pick.Error;
        if (pick.Name is null || repositories.FilesOf(pick.Name) is not { } files)
            return "Error: this run has no repository to keep a memory in.";
        if (!await repositories.HasCodeChangeAsync(pick.Name, ct))
            return await NotLandingAsync(pick.Name, ct);

        await new MemoryStore(files, repoRoot, logger)
            .UpsertAsync(new MemoryEntry(slug, description, entryType, body), ct);
        return $"Memory '{slug}' recorded in {pick.Name} ({MemoryEntryTypes.ToSlug(entryType)}) — a hint whose " +
               "standing the runs that rely on it decide.";
    }

    private async Task<string> NotLandingAsync(string repository, CancellationToken ct)
    {
        var landing = new List<string>();
        foreach (var name in repositories.Names())
            if (await repositories.HasCodeChangeAsync(name, ct)) landing.Add(name);
        var instead = landing.Count == 0
            ? "No repository has a code change yet — remember once the work is in the repository it is about."
            : $"Repositories with a change this run: [{string.Join(", ", landing)}].";
        return $"Not recorded: '{repository}' has no code change this run, so no pull request would carry " +
               $"the memory. {instead}";
    }
}
