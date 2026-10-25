using System.Reflection;
using AgentSmith.Contracts.Events;

namespace AgentSmith.Tests.Events;

/// <summary>
/// 2026-09-03-b028: builds one populated instance of an event record from its
/// constructor, so the round-trip test can be driven off the enum instead of a
/// hand-written list that goes stale the day a new event ships. A parameter type
/// the factory cannot fill throws by name — a new event with a shape the envelope
/// cannot rebuild fails loudly here rather than silently on a dashboard.
/// </summary>
internal static class SampleEventFactory
{
    internal const string RunId = "2026-09-03T10-00-00-b028";

    internal static readonly DateTimeOffset Timestamp =
        new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);

    internal static object Build(Type eventType)
    {
        var ctor = eventType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .First();
        var arguments = ctor.GetParameters().Select(p => Value(eventType, p)).ToArray();
        return ctor.Invoke(arguments);
    }

    private static object? Value(Type eventType, ParameterInfo parameter)
    {
        var declared = parameter.ParameterType;
        var type = Nullable.GetUnderlyingType(declared) ?? declared;
        if (type == typeof(string)) return parameter.Name == "RunId" ? RunId : parameter.Name;
        if (type == typeof(DateTimeOffset)) return Timestamp;
        if (type == typeof(bool)) return true;
        if (type == typeof(int)) return 7;
        if (type == typeof(long)) return 11L;
        if (type == typeof(double)) return 13d;
        if (type == typeof(decimal)) return 0.17m;
        if (type.IsEnum) return Enum.GetValues(type).GetValue(0);
        if (declared == typeof(IReadOnlyList<string>)) return new[] { "one", "two" };
        if (declared == typeof(IReadOnlyList<AgentSmith.Contracts.Specs.SpecCutEntry>)) // 2026-10-13-9a59e
            return new[] { new AgentSmith.Contracts.Specs.SpecCutEntry("2026-10-13-1234a", 1, "2026-10-13-1234a-x", "goal",
                ["api"], [new AgentSmith.Contracts.Models.SpecStep("s1", "do it", "src/A.cs")], ["it builds"], "spec: x") };
        if (declared == typeof(IReadOnlyList<AgentSmith.Contracts.Passes.MasterPassCriterion>)) // 2026-10-13-9a59f
            return new[] { new AgentSmith.Contracts.Passes.MasterPassCriterion("it builds", "met", "src/A.cs") };
        if (declared == typeof(IReadOnlyList<AgentSmith.Domain.Models.CutFinding>)) // 2026-10-13-9a59g
            return new[] { new AgentSmith.Domain.Models.CutFinding("2026-10-13-1234a", "it builds", "two goals", "why", "c2", "E1") };
        if (declared == typeof(IReadOnlyList<SpecReviewCut>)) // 2026-10-13-8762e
            return new[] { new SpecReviewCut("goal", "the four word run", "a new run", "api/src/A.cs:3 [A1]", "2026-10-13-1234a") };
        if (declared == typeof(IReadOnlyList<SpecReviewFinding>))
            return new[] { new SpecReviewFinding("no caller reads it", "api search:'Reads' [A2]") };
        if (declared == typeof(IReadOnlyList<SpecEvidenceProblem>))
            return new[] { new SpecEvidenceProblem("it exists", "not a file (missing, or a directory): src/A.cs") };
        if (declared == typeof(IReadOnlyList<SpecReviewResidue>))
            return new[] { new SpecReviewResidue("done", "the four word run") };
        if (declared == typeof(IReadOnlyList<AgentSmith.Contracts.Memory.MemoryCitation>)) // 2026-10-25-9324b
            return new[] { new AgentSmith.Contracts.Memory.MemoryCitation("api", "github.com/o/api", "build-order") };
        if (type == typeof(AgentSmith.Contracts.Memory.MemoryCitation))
            return new AgentSmith.Contracts.Memory.MemoryCitation("api", "github.com/o/api", "no-shims");
        if (type == typeof(AgentSmith.Contracts.Runs.AttemptCause)) // 2026-10-13-9a59d
            return new AgentSmith.Contracts.Runs.AttemptCause("pull_request", "alice", Timestamp,
                new AgentSmith.Contracts.Runs.AttemptSource("review", "https://example.test/pull/4", "PRR_1", "detail"));
        throw new NotSupportedException(
            $"{eventType.Name}.{parameter.Name} is a {declared.Name}; teach SampleEventFactory "
            + "how to fill it, or the round-trip rule stops covering this event.");
    }
}
