namespace AgentSmith.Server.Services.Catalog;

/// <summary>
/// 2026-09-18-84be: the binding the catalog contents on this page came from.
/// <see cref="Phrase"/> is the sentence the resolution mints for itself — source mode,
/// version or path, the overlay fingerprint when one is layered, and the root.
/// <para>
/// <see cref="OverlayPath"/> is the CONFIGURED overlay directory, present only when this
/// binding actually layered one. The phrase carries a fingerprint and the synthesised
/// union root, neither of which an operator ever typed; the configured path is the thing
/// they can go and look at — and it is not on the Skills settings form, so a page that
/// renders an overlaid body must say where that body really comes from.
/// </para>
/// <para>
/// <see cref="ReadAt"/> is when this reader loaded the catalog, because the contents are
/// cached and a version printed beside possibly-older contents would be a new false
/// witness. It is a reading time, not a resolution time: the resolve may be older still.
/// </para>
/// </summary>
public sealed record CatalogOrigin(string Phrase, string? OverlayPath, DateTimeOffset ReadAt);
