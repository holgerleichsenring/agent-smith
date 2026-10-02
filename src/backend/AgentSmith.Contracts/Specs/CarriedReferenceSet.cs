namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-10-01-283df: one uploaded website a run carries — written into <paramref name="Repo"/>'s
/// working tree at <paramref name="Path"/>, outside the commit.
/// </summary>
/// <param name="SetId">The set's id, which is also its directory name, so two sets named alike never collide.</param>
/// <param name="Name">The set's name — the folder its files share, or <c>site</c>.</param>
/// <param name="Address">How render_reference names it: <c>reference:&lt;name&gt;</c>, unique in the run.</param>
/// <param name="Repo">The carrying repository, by configured name.</param>
/// <param name="Path">Its directory, relative to that repository's root.</param>
/// <param name="Session">The conversation the set was uploaded to — where the store keeps it.</param>
/// <param name="Files">How many files it holds.</param>
/// <param name="Note">2026-10-02-075dd: the set's note as the run began — live, not frozen at approval.</param>
public sealed record CarriedReferenceSet(
    string SetId, string Name, string Address, string Repo, string Path, string Session, int Files, string? Note = null);
