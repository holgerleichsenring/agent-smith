using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Resolves the appropriate IStorageReader for a given AttachmentRef
/// by iterating registered readers and calling CanHandle.
/// </summary>
public sealed class StorageReaderRegistry(IEnumerable<IStorageReader> readers)
{
    private readonly IReadOnlyList<IStorageReader> _readers = readers.ToList();

    public IStorageReader Resolve(AttachmentRef attachmentRef)
    {
        foreach (var reader in _readers)
        {
            if (reader.CanHandle(attachmentRef))
                return reader;
        }

        throw new InvalidOperationException(
            $"No IStorageReader can handle attachment '{attachmentRef.FileName}' (URI: {attachmentRef.Uri}).");
    }

    public bool TryResolve(AttachmentRef attachmentRef, out IStorageReader? reader)
    {
        foreach (var r in _readers)
        {
            if (r.CanHandle(attachmentRef))
            {
                reader = r;
                return true;
            }
        }

        reader = null;
        return false;
    }
}
