using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// The ratified mock of each unexecuted spec of a filed set, read from the conversation the approval was
/// given in, as the file a branch write puts beside the spec: <c>{SeriesPaths.Planned}/{id}-mock-v{n}.html</c>
/// under the FILED id. A version is resolved only through <see cref="IConversationMockReader"/>, by the id
/// the proposal gave the spec. What cannot be written is a note, never an error. Without a reader — a
/// process that holds no conversation store — every ratified mock is such a note.
/// </summary>
public sealed class RatifiedMockFiles(IConversationMockReader? reader)
{
    public const string NoStore = "the ratified mock was not written: this process holds no conversation store";

    /// <param name="sources">Filed id to the proposal's spec id; a spec missing from it is looked up by its own id.</param>
    public async Task<RatifiedMockSet> ReadAsync(
        SpecApprovalRecord record, IReadOnlyDictionary<string, string>? sources, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);
        var stamped = record.Set.UnexecutedTail.Where(s => s.Draft.RatifiedMock is not null).ToList();
        if (stamped.Count == 0) return RatifiedMockSet.None;
        if (reader is null || record.Approval?.Conversation is not { Length: > 0 } conversation)
            return new RatifiedMockSet(new Dictionary<string, RepoFile>(), [NoStore]);
        var files = new Dictionary<string, RepoFile>(StringComparer.Ordinal);
        var notes = new List<string>();
        foreach (var spec in stamped)
        {
            var version = spec.Draft.RatifiedMock!.Value;
            var source = sources?.GetValueOrDefault(spec.SpecId) ?? spec.SpecId;
            var page = await reader.HtmlAsync(conversation, source, version, ct);
            if (page is null)
                notes.Add($"The ratified mock v{version} of {spec.SpecId} was not written: the conversation no longer holds it.");
            else
                files[spec.SpecId] = new RepoFile(PathOf(spec.SpecId, version), page.Html);
        }
        return new RatifiedMockSet(files, notes);
    }

    /// <summary>The ratified mock among the mocks read beside <paramref name="spec"/>, or null when it is not there.</summary>
    public static string? Beside(DerivedSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (spec.Draft.RatifiedMock is not { } version) return null;
        var name = MockVersion.NameOf(spec.SpecId, version) + ".html";
        return spec.MockPaths?.FirstOrDefault(p => p == name || p.EndsWith("/" + name, StringComparison.Ordinal));
    }

    /// <summary>Where a spec's ratified mock lies on the branch.</summary>
    public static string PathOf(string specId, int version) =>
        $"{SeriesPaths.Planned}/{MockVersion.NameOf(specId, version)}.html";
}

/// <summary>The mock files by filed spec id, and the notes on those not written.</summary>
public sealed record RatifiedMockSet(IReadOnlyDictionary<string, RepoFile> BySpec, IReadOnlyList<string> Notes)
{
    public static RatifiedMockSet None { get; } = new(new Dictionary<string, RepoFile>(), []);
}
