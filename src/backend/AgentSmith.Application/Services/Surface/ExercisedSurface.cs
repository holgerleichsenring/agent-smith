using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Surface;

/// <summary>
/// 2026-08-30-c6ec: what the reported call sites are known to exercise, indexed by the
/// operation identity a served description and a client can share.
/// <para>
/// A client addresses an operation by its id or by its method and path, and both forms
/// index the same entry — an operation the served description names either way is
/// exercised when either form was seen.
/// </para>
/// </summary>
internal sealed class ExercisedSurface
{
    private readonly Dictionary<string, HashSet<string>> _sent = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _read = new(StringComparer.Ordinal);

    public static ExercisedSurface Of(IReadOnlyList<ClientCallSite> callSites)
    {
        var surface = new ExercisedSurface();
        foreach (var site in callSites)
        {
            var key = OperationKey.Of(site.Operation);
            if (key.Length == 0) continue;
            Add(surface._sent, key, site.PropertiesSent);
            Add(surface._read, key, site.PropertiesRead);
        }
        return surface;
    }

    public bool Calls(ServedOperation operation) => Keys(operation).Any(_sent.ContainsKey);

    public IReadOnlyCollection<string> Sent(ServedOperation operation) => Union(_sent, operation);

    public IReadOnlyCollection<string> Read(ServedOperation operation) => Union(_read, operation);

    private static IEnumerable<string> Keys(ServedOperation operation)
    {
        yield return OperationKey.Of(operation.Signature);
        if (!string.IsNullOrWhiteSpace(operation.OperationId))
            yield return OperationKey.Of(operation.OperationId);
    }

    private static void Add(
        Dictionary<string, HashSet<string>> index, string key, IReadOnlyList<string> properties)
    {
        if (!index.TryGetValue(key, out var known))
            index[key] = known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in properties.Where(p => !string.IsNullOrWhiteSpace(p)))
            known.Add(property.Trim());
    }

    private static IReadOnlyCollection<string> Union(
        Dictionary<string, HashSet<string>> index, ServedOperation operation)
    {
        var union = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in Keys(operation).Where(index.ContainsKey))
            union.UnionWith(index[key]);
        return union;
    }
}
