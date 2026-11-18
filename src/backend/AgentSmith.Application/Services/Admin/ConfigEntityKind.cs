using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Application.Services.Admin;

/// <summary>
/// 2026-11-17-f3a8c: one Config studio kind as the admin assistant reads and writes it — the entity as the studio's
/// API carries it, by id, through the same store calls the studio's routes make.
/// </summary>
public sealed record ConfigEntityKind(
    Func<IConfigStore, string, object?> Read,
    Action<IConfigStore, string, JsonElement, ChangeAttribution> Upsert,
    Action<IConfigStore, string, ChangeAttribution> Delete)
{
    public static ConfigEntityKind For<T>(
        Func<IConfigStore, IReadOnlyList<T>> getAll, Func<T, string> idOf, Func<T, string, T> withId,
        Action<IConfigStore, T, ChangeAttribution> upsert, Action<IConfigStore, string, ChangeAttribution> delete) => new(
        (store, id) => getAll(store).FirstOrDefault(e => ConfigNames.AreSame(idOf(e), id)),
        (store, id, document, by) => upsert(store, withId(Parse<T>(document, id), id), by),
        delete);

    private static T Parse<T>(JsonElement document, string id) =>
        document.Deserialize<T>(ConfigEntityJson.Options)
        ?? throw new ConfigurationException($"The document drafted for '{id}' is empty.");
}
