namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-11-23-933da: one PoC — an uploaded app hosted on a target. It belongs to its owner's poc- project, so who may
/// see it is answered by that project's visibility. The desired state (<see cref="Replicas"/>, <see cref="Revision"/>,
/// <see cref="ImageDigest"/>) is read by every render and changed only by the lifecycle acts. Times are UTC ticks.
/// </summary>
public sealed class Poc : EntityBase, IProjectOwned
{
    public long Id { get; set; }

    /// <summary>A DNS label, unique per target among the live PoCs.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The name while the PoC is not deleted, null once deleted, so a deleted PoC frees its name and host.
    /// Only c81ed's settled delete clears it.</summary>
    public string? LiveName { get; set; }

    /// <summary>The owner's poc- project.</summary>
    public string Project { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;

    /// <summary>'&lt;root&gt;/&lt;project&gt;/&lt;name&gt;', copied at creation so a later settings edit never moves it.</summary>
    public string Folder { get; set; } = string.Empty;

    /// <summary>The owner's principal (SpecDialogOwnership.PrincipalOf).</summary>
    public string Owner { get; set; } = string.Empty;

    public string OwnerName { get; set; } = string.Empty;

    /// <summary>One of PocStates.</summary>
    public string State { get; set; } = string.Empty;

    public int Replicas { get; set; } = 1;

    /// <summary>The revision the GitOps manifest names.</summary>
    public int? Revision { get; set; }

    /// <summary>'sha256:&lt;64 hex&gt;'.</summary>
    public string? ImageDigest { get; set; }

    /// <summary>2026-11-23-c81eb: the readiness probe's HTTP path; null probes the port over TCP. Part of the desired
    /// state, written with it by the GitOps commit; set by the owner on the detail page (c81ed).</summary>
    public string? ReadinessPath { get; set; }

    /// <summary>Written only by c81ee's extend run; null means created + the target's lifetime_days.</summary>
    public long? ExpiresAtTicks { get; set; }

    public string? LastRunId { get; set; }

    /// <summary>2026-11-23-933dg: the plain variables, a JSON array of {name, value, set_by, set_at} ordered by name —
    /// committed in plain text as the PoC's ConfigMap, so they are not secret. Null holds none.</summary>
    public string? PlainVariablesJson { get; set; }

    /// <summary>2026-11-23-933dg: the seal fingerprint of the sealed files the GitOps branch holds; written only by
    /// 2026-11-23-c81eb's GitOps commit after the commit landed. A keyed digest: it reveals nothing of a value.</summary>
    public string? SealFingerprint { get; set; }
}
