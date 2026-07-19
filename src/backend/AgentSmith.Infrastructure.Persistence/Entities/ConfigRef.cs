namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// p0349: one edge of the fixed config reference graph — a project referencing its
/// agent / tracker / repos / connections. The edge FKs its target back to
/// <see cref="ConfigEntity"/> with ON DELETE RESTRICT, so a referenced entity
/// cannot be dropped out from under a referrer; the referencing set is surfaced to
/// the operator ("used by N projects") instead of a silent cascade.
/// </summary>
public sealed class ConfigRef : EntityBase
{
    public long Id { get; set; }

    public string FromType { get; set; } = string.Empty;

    public string FromId { get; set; } = string.Empty;

    public string ToType { get; set; } = string.Empty;

    public string ToId { get; set; } = string.Empty;
}
