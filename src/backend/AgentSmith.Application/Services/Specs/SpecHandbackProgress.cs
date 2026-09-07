using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: what the pointer records after a hand-back was posted — the case, and how
/// many times in a row it was the same one. Whether a repeat ends the loop is
/// <see cref="SpecHandbackRepeat"/>'s question, answered from the ticket thread.
/// </summary>
public static class SpecHandbackProgress
{
    /// <summary>The pointer to record after a hand-back was posted.</summary>
    public static SpecSetPointer Record(SpecSetPointer pointer, SpecHandbackCase current)
    {
        ArgumentNullException.ThrowIfNull(pointer);
        return pointer with
        {
            LastHandbackCase = current,
            RepeatedHandbackCount = pointer.LastHandbackCase == current
                ? pointer.RepeatedHandbackCount + 1
                : 1,
        };
    }
}
