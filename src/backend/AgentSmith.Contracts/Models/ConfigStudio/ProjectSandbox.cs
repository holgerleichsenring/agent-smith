namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// 2026-09-22-6968: the SCALAR half of a project's sandbox overrides, as the studio edits
/// them. Every field is null-means-inherit, and the block itself is null-means-absent: a
/// client that does not know this block sends none and the stored block is left alone,
/// while a client that DOES show the block sends all five, so a null field inside a sent
/// block is a deliberate clear. That is the only discriminator C# nullables leave — the
/// same rule <see cref="ProjectEntity.Templates"/> already applies at the block level.
/// <para>
/// The structured three (resources, the per-language image map and the pod's secrets) are
/// deliberately NOT here: each inherits by a different rule, and the patch never touches
/// what this block does not carry, so they survive a save through it.
/// </para>
/// </summary>
public sealed record ProjectSandbox(
    string? ToolchainImage = null,
    int? StepTimeoutSeconds = null,
    int? RunCommandTimeoutSeconds = null,
    string? AgentRegistry = null,
    string? AgentVersion = null);
