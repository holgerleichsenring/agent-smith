namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0423: implemented by every event that reports a finished unit of work. It is what
/// makes "the same five numbers for every kind" checkable rather than promised — a rule
/// test enumerates the completion events and fails on one that answers fewer questions
/// than its siblings.
/// </summary>
public interface IMeasuredWork
{
    WorkMeasure Measure { get; }
}
