namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-24-35ff: who a run of this preset is for — a person's work, or the server's own housekeeping.</summary>
public enum PipelineOrigin
{
    /// <summary>A person chose it, or it works a ticket: a coding run, a scan, a deploy, a close, a conversation turn.</summary>
    Work,

    /// <summary>The server starts it itself: a series beat, a landing's follow-ups, a sweep.</summary>
    System,
}

public static partial class PipelinePresets
{
    // 2026-11-24-35ff: every preset states its origin here; PipelinePresetsSystemTests fails a preset that does not, so a
    // new pipeline has to decide. Per pipeline, not per run: poc-stop and poc-delete are an owner's acts that the lifetime
    // pass also starts on expiry, and they count as work. Only constant names are read, so the file order of the partial
    // field initializers does not matter.
    private static readonly Dictionary<string, PipelineOrigin> Origins = new(StringComparer.OrdinalIgnoreCase)
    {
        [CodeName] = PipelineOrigin.Work, [InitProjectName] = PipelineOrigin.Work,
        ["mad-discussion"] = PipelineOrigin.Work, ["legal-analysis"] = PipelineOrigin.Work,
        ["security-scan"] = PipelineOrigin.Work, ["api-security-scan"] = PipelineOrigin.Work, ["pr-review"] = PipelineOrigin.Work,
        [SpecDialogName] = PipelineOrigin.Work, [SpecDeriveName] = PipelineOrigin.Work, [FileName] = PipelineOrigin.Work,
        [DeployName] = PipelineOrigin.Work,
        [PocDeployName] = PipelineOrigin.Work, [PocVariablesName] = PipelineOrigin.Work, [PocStopName] = PipelineOrigin.Work,
        [PocStartName] = PipelineOrigin.Work, [PocRollbackName] = PipelineOrigin.Work, [PocApplyName] = PipelineOrigin.Work,
        [PocDeleteName] = PipelineOrigin.Work, [PocExtendName] = PipelineOrigin.Work,
        [CloseName] = PipelineOrigin.Work, [ReopenName] = PipelineOrigin.Work, [AmendName] = PipelineOrigin.Work,
        [CommentName] = PipelineOrigin.Work,
        [AdminTurnName] = PipelineOrigin.Work, [ConfigName] = PipelineOrigin.Work, [SecretName] = PipelineOrigin.Work,
        [PocTargetInstallName] = PipelineOrigin.Work,

        [SeriesRunName] = PipelineOrigin.System, [SeriesVerifyName] = PipelineOrigin.System,
        [RefreshBaseName] = PipelineOrigin.System, [BranchCleanupName] = PipelineOrigin.System,
        [StartName] = PipelineOrigin.System, [RetargetName] = PipelineOrigin.System, [MergeGreenName] = PipelineOrigin.System,
        [PocUploadPurgeName] = PipelineOrigin.System,
    };

    /// <summary>The origin a preset declared; null for a name that is no preset.</summary>
    public static PipelineOrigin? OriginOf(string pipelineName) =>
        Origins.TryGetValue(pipelineName, out var origin) ? origin : null;

    /// <summary>True for a preset the server starts itself. A name that is no preset is work, so an unknown run is never
    /// hidden from the runs list.</summary>
    public static bool IsSystem(string pipelineName) => OriginOf(pipelineName) == PipelineOrigin.System;

    /// <summary>The names of the system presets, for a query that filters runs by pipeline.</summary>
    public static IReadOnlyList<string> SystemNames { get; } =
        [.. Origins.Where(o => o.Value == PipelineOrigin.System).Select(o => o.Key)];
}
