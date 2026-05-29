namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0173e: marks a record field that producers may stop emitting after a
/// grace window. Consumers stay tolerant of a missing value (rule (b) in
/// <c>docs/event-schema-policy.md</c>): the field remains readable from
/// historical fixtures, but new producers should not depend on it. Carrying
/// the attribute makes the deprecation status machine-readable for the
/// drift detector and human-readable for reviewers.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class DeprecatedFieldAttribute(string reason, string? removeAfter = null) : Attribute
{
    public string Reason { get; } = reason;
    public string? RemoveAfter { get; } = removeAfter;
}
