using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// 2026-09-22-6c46: the STRUCTURED half of a project's sandbox overrides. Its PRESENCE is
/// the discriminator, exactly as <see cref="ProjectEntity.Sandbox"/>'s presence is for the
/// scalar half: a client that does not render these three sends no block at all and the
/// stored values are left alone, while a client that DOES render them sends all three, so a
/// null field inside a sent block is a deliberate clear back to inherited. Without the
/// nesting the two cases would be indistinguishable after binding, and a scalar-only save
/// would silently delete a project's resources, images and secret references.
/// </summary>
/// <param name="Resources">All four cpu/memory quantities or none: the model refuses a
/// partial override, so the group is wholly given or wholly inherited.</param>
/// <param name="Images">Per-language image pins, merged OVER the code-default table per
/// KEY — pinning one language leaves every other language inheriting. Null means inherit
/// the whole table; an empty map is not a declaration and is stored as null.</param>
/// <param name="Secrets">NAMES only — a Kubernetes Secret and the keys taken from it. No
/// value field exists anywhere on this path; the values live in the cluster.</param>
public sealed record ProjectSandboxStructured(
    ResourceLimits? Resources = null,
    IReadOnlyDictionary<string, string>? Images = null,
    SandboxSecrets? Secrets = null);
