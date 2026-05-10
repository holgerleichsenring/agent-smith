using AgentSmith.Contracts.Activation;

namespace AgentSmith.Application.Services.Activation;

/// <summary>
/// DI-discovered registry of every <see cref="IConceptWriter"/> in the container.
/// Each writer's <c>DeclaredConcepts</c> list is folded into a concept-to-handlers
/// lookup; the runtime type name (<c>writer.GetType().Name</c>) identifies the
/// publishing handler. Consumed by the <c>validate-concepts</c> CLI verb to
/// cross-check vocabulary <c>writers</c> lists.
/// </summary>
public sealed class ConceptWriterRegistry
{
    public ConceptWriterRegistry(IEnumerable<IConceptWriter> writers)
    {
        var lookup = new Dictionary<string, List<HandlerConcept>>(StringComparer.Ordinal);
        var allWriters = new List<HandlerConceptWriter>();
        foreach (var writer in writers)
        {
            var className = writer.GetType().Name;
            var declared = new List<HandlerConcept>();
            foreach (var declaration in writer.DeclaredConcepts)
            {
                var entry = new HandlerConcept(className, declaration.Type);
                declared.Add(entry);
                if (!lookup.TryGetValue(declaration.ConceptName, out var list))
                {
                    list = [];
                    lookup[declaration.ConceptName] = list;
                }
                list.Add(entry);
            }
            allWriters.Add(new HandlerConceptWriter(className, declared));
        }

        ConceptToHandlers = lookup.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<HandlerConcept>)kv.Value.AsReadOnly(),
            StringComparer.Ordinal);
        Writers = allWriters.AsReadOnly();
    }

    /// <summary>Lookup from concept name → every handler that declares it (with declared type).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<HandlerConcept>> ConceptToHandlers { get; }

    /// <summary>Flat list of every registered writer, projected to (className, concepts).</summary>
    public IReadOnlyList<HandlerConceptWriter> Writers { get; }
}
