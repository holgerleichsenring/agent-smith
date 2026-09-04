using System.Reflection;

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
        throw new NotSupportedException(
            $"{eventType.Name}.{parameter.Name} is a {declared.Name}; teach SampleEventFactory "
            + "how to fill it, or the round-trip rule stops covering this event.");
    }
}
