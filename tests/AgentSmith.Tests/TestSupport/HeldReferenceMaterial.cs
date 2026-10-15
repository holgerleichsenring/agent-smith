using System.Text;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-10-14-dbcel: uploaded material held BY CONVERSATION, as the real store keeps it — a set or an image is read
/// under the conversation it was uploaded in and nowhere else, and an upload can be removed again.
/// </summary>
internal sealed class HeldReferenceMaterial : IReferenceSetReader
{
    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01];

    private readonly Dictionary<string, Dictionary<string, List<ReferenceSetFile>>> _sets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, byte[]>> _images = new(StringComparer.Ordinal);

    public HeldReferenceMaterial Set(string conversation, string id, params (string Path, string Text)[] files)
    {
        Of(_sets, conversation)[id] = files.Length == 0
            ? [new ReferenceSetFile("site/index.html", Encoding.UTF8.GetBytes($"<h1>{id}</h1>"))]
            : [.. files.Select(f => new ReferenceSetFile(f.Path, Encoding.UTF8.GetBytes(f.Text)))];
        return this;
    }

    public HeldReferenceMaterial Image(string conversation, string id)
    {
        Of(_images, conversation)[id] = Png;
        return this;
    }

    public void Remove(string conversation, string id)
    {
        Of(_sets, conversation).Remove(id);
        Of(_images, conversation).Remove(id);
    }

    public Task<IReadOnlyList<ReferenceSetFile>> FilesAsync(string sessionId, string setId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ReferenceSetFile>>(Of(_sets, sessionId).TryGetValue(setId, out var files) ? files : []);

    public Task<IReadOnlyList<string>> SetIdsAsync(string sessionId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([.. Of(_sets, sessionId).Keys]);

    public Task<IReadOnlyList<string>> ImageSetIdsAsync(string sessionId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([.. Of(_images, sessionId).Keys]);

    public Task<ReferenceImageFile?> ImageAsync(string sessionId, string setId, CancellationToken cancellationToken) =>
        Task.FromResult(Of(_images, sessionId).TryGetValue(setId, out var bytes) ? new ReferenceImageFile("image/png", bytes) : null);

    private static Dictionary<string, T> Of<T>(Dictionary<string, Dictionary<string, T>> held, string conversation)
    {
        if (!held.TryGetValue(conversation, out var of)) held[conversation] = of = new Dictionary<string, T>(StringComparer.Ordinal);
        return of;
    }
}
