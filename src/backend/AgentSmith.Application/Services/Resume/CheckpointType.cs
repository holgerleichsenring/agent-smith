using System.Collections;

namespace AgentSmith.Application.Services.Resume;

/// <summary>
/// p0478: what a checkpoint may NAME and WRITE for a context entry.
/// <para>
/// A collection expression assigned to an <c>IReadOnlyList&lt;T&gt;</c> is compiled to an
/// internal type — <c>&lt;&gt;z__ReadOnlySingleElementList&lt;T&gt;</c> for one element,
/// <c>&lt;&gt;z__ReadOnlyArray&lt;T&gt;</c> for several. Each serialises happily as a JSON
/// array and none of them can be READ back: System.Text.Json finds no constructor it can
/// use. A live run parked at phase c, the operator answered, and the resume threw
/// NotSupportedException on the way in, thirty-seven of fifty-five steps done.
/// </para>
/// <para>
/// Naming <c>List&lt;T&gt;</c> is not enough on its own: Serialize refuses an input type the
/// value does not derive from. So the value is MATERIALISED into that list and the pair
/// travels together. The bag holds <c>object</c>, so there is no declared type to fall back
/// on; the element type comes from the runtime type's own <c>IEnumerable&lt;T&gt;</c>, and a
/// <c>List&lt;T&gt;</c> satisfies every consumer, which reads these as read-only.
/// </para>
/// </summary>
internal static class CheckpointType
{
    /// <summary>The type to record and the value to write under it.</summary>
    public static (Type Type, object Value) Of(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var type = value.GetType();
        // A compiler-generated name is one C# cannot express, so it carries '<>'.
        if (!type.Name.Contains("<>", StringComparison.Ordinal)) return (type, value);

        var enumerable = type.GetInterfaces().FirstOrDefault(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (enumerable is null || value is not IEnumerable items) return (type, value);

        var listType = typeof(List<>).MakeGenericType(enumerable.GenericTypeArguments[0]);
        var list = (IList)Activator.CreateInstance(listType)!;
        foreach (var item in items) list.Add(item);
        return (listType, list);
    }
}
