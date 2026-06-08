using System.Diagnostics;
using System.Reflection;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0260 audit: every outbound write to a ticket work item logs the in-process
/// caller chain. The "in-progress re-written every ~minute" loop proved a REAL
/// write (the optimistic-concurrency <c>op:test /rev</c> succeeded, so it was a
/// fresh write, not a stale read) issued from SOME agent-smith code path we
/// could not name once the loop's logs were lost. A ticket write always
/// originates in this program — this records WHICH part, so the next occurrence
/// is attributable on sight rather than reconstructed.
/// </summary>
internal static class TicketWriteAudit
{
    /// <summary>
    /// The agent-smith frames of the current call stack, innermost first, as
    /// "Type.Method (File.cs:line)". Built from <see cref="StackFrame"/>
    /// reflection — NOT by parsing <see cref="Environment.StackTrace"/>, whose
    /// "at"/"in"/"line" words are culture-localized ("bei"/"Zeile" on a German
    /// host) and would silently match nothing. Framework and async-plumbing
    /// frames are stripped so the originating component is obvious. Ticket writes
    /// are infrequent (claim, transition, finalize), so capturing the stack here
    /// is not on any hot path.
    /// </summary>
    public static string Caller()
    {
        var frames = new StackTrace(fNeedFileInfo: true).GetFrames();
        var chain = new List<string>(8);
        foreach (var frame in frames)
        {
            var method = frame.GetMethod();
            var type = method?.DeclaringType;
            if (type is null) continue;
            if ((type.Namespace ?? "").StartsWith("AgentSmith", StringComparison.Ordinal) is false) continue;
            if (type == typeof(TicketWriteAudit)) continue;

            var (typeName, methodName) = Resolve(type, method!);
            var file = frame.GetFileName();
            var loc = file is null ? "" : $" ({Path.GetFileName(file)}:{frame.GetFileLineNumber()})";
            chain.Add($"{typeName}.{methodName}{loc}");
            if (chain.Count >= 8) break;
        }
        return chain.Count == 0 ? "<no agent-smith frames>" : string.Join(" <- ", chain);
    }

    // Async methods compile to a `<Method>d__N` state-machine type whose frame is
    // `MoveNext`; unwrap to the owner type + the real method name. Iterators and
    // local functions use the same `<name>` convention, so the same parse applies.
    private static (string Type, string Method) Resolve(Type type, MethodBase method)
    {
        var name = type.Name;
        if (name.Length > 0 && name[0] == '<')
        {
            var end = name.IndexOf('>');
            var inner = end > 1 ? name[1..end] : method.Name;
            return (type.DeclaringType?.Name ?? type.Name, inner);
        }
        return (name, method.Name);
    }
}
