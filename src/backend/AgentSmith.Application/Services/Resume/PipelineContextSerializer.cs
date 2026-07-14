using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Resume;

/// <summary>
/// p0327: serializes the DATA entries of a PipelineContext as
/// <c>[{k, t, v}]</c> (key, assembly-qualified runtime type, payload JSON).
/// Live objects are excluded by key (<see cref="ExcludedKeys"/>) — sandbox
/// handles, the borrowed coordinator, pricing/catalog bindings — and anything
/// else that fails to serialize is skipped with a warning: those entries are
/// re-established on resume by the normal pipeline seeding + re-provisioning.
/// </summary>
public sealed class PipelineContextSerializer(
    ILogger<PipelineContextSerializer> logger) : IPipelineContextSerializer
{
    // Live handles + values ExecutePipelineUseCase re-derives on every launch.
    // ContextKeys.Repos is deliberately NOT here: a ScopeRepos-narrowed list is
    // run state and must win over the standard all-repos seed on resume.
    private static readonly HashSet<string> ExcludedKeys = new(StringComparer.Ordinal)
    {
        ContextKeys.Sandbox, ContextKeys.Sandboxes, ContextKeys.SandboxCoordinator,
        ContextKeys.SandboxRepos, ContextKeys.SandboxDiscoveries, ContextKeys.SandboxContexts,
        ContextKeys.Repository, ContextKeys.ProjectConfig, ContextKeys.ResolvedPipeline,
        ContextKeys.CatalogResolution, ContextKeys.ConceptVocabulary, ContextKeys.ConfigDir,
        ContextKeys.SpecDialogReplySlot, ContextKeys.ActivePhaseStep,
        ContextKeys.RemainingCommands, ContextKeys.PipelineExecutionCount,
        ContextKeys.WaitingForInput, ContextKeys.ResumedDialogueAnswer, ContextKeys.ResumeCheckpoint,
        ContextKeys.DialogueHotWaitSeconds, ContextKeys.DialogueApprovalTimeoutSeconds,
        "ProjectPricing", "PipelineCostCap", "ModelPricingResolver", "PipelineCostTracker",
    };

    public string Serialize(PipelineContext context)
    {
        var entries = new List<SerializedEntry>();
        foreach (var (key, value) in context.Snapshot())
        {
            if (ExcludedKeys.Contains(key)) continue;
            var entry = TrySerializeEntry(key, value);
            if (entry is not null) entries.Add(entry);
        }
        return JsonSerializer.Serialize(entries);
    }

    public void Restore(string serialized, PipelineContext into)
    {
        var entries = JsonSerializer.Deserialize<List<SerializedEntry>>(serialized) ?? [];
        var restored = 0;
        foreach (var entry in entries)
        {
            if (TryRestoreEntry(entry, into)) restored++;
        }
        logger.LogInformation(
            "Restored {Restored}/{Total} checkpointed context entries", restored, entries.Count);
    }

    private SerializedEntry? TrySerializeEntry(string key, object value)
    {
        try
        {
            var type = value.GetType();
            return new SerializedEntry(key, type.AssemblyQualifiedName!,
                JsonSerializer.Serialize(value, type));
        }
        catch (Exception ex) when (ex is NotSupportedException or JsonException or InvalidOperationException)
        {
            logger.LogWarning(ex,
                "Context entry '{Key}' ({Type}) is not serializable — excluded from the checkpoint",
                key, value.GetType().Name);
            return null;
        }
    }

    private bool TryRestoreEntry(SerializedEntry entry, PipelineContext into)
    {
        var type = Type.GetType(entry.T, throwOnError: false);
        if (type is null)
        {
            logger.LogWarning("Checkpointed entry '{Key}' has unknown type '{Type}' — skipped", entry.K, entry.T);
            return false;
        }
        try
        {
            var value = JsonSerializer.Deserialize(entry.V, type);
            if (value is null) return false;
            into.Set(entry.K, value);
            return true;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Checkpointed entry '{Key}' failed to deserialize — skipped", entry.K);
            return false;
        }
    }

    /// <summary>One persisted entry: key, runtime type, payload JSON.</summary>
    private sealed record SerializedEntry(string K, string T, string V);
}
