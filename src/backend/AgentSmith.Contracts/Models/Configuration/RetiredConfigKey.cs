namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// A configuration key this product no longer reads. The loaders ignore keys they do not bind,
/// so without a row here a file or a stored document that still sets one is silently a no-op.
/// </summary>
/// <param name="Path">Dotted YAML key path from the root. <c>*</c> matches any map key or any
/// list element — <c>trackers.*.parent_link_type</c>. Segments match stored documents too, whose
/// property names are the C# spelling: case and underscores are ignored.</param>
/// <param name="Since">When it stopped being read (a date), so an operator can place it.</param>
/// <param name="Reason">One or two sentences: why it went, and what replaces it if anything.</param>
public sealed record RetiredConfigKey(string Path, string Since, string Reason);
