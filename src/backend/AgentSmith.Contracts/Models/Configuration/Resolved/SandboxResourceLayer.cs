namespace AgentSmith.Contracts.Models.Configuration.Resolved;

/// <summary>
/// 2026-09-22-6c46: WHICH of the four layers answers a sandbox's cpu/memory. The resource
/// group cannot be described by <see cref="ResolutionSource"/>'s three values: its answer
/// depends on the pipeline (a pipeline that changes no code is forced onto the light
/// profile) and on a document written per run, so a control that showed one number without
/// naming its layer would be claiming an answer it does not have.
/// </summary>
public enum SandboxResourceLayer
{
    /// <summary>The project's own sandbox.resources block — wins for every pipeline.</summary>
    ProjectOverride,

    /// <summary>The fixed light profile every pipeline that changes no code is held to.</summary>
    LightProfile,

    /// <summary>The repository's context document, written per run by the model and
    /// accepted only whole — so whether it answers is not knowable at config time.</summary>
    ContextDocument,

    /// <summary>The process-wide SandboxOptions default.</summary>
    GlobalDefault,
}
