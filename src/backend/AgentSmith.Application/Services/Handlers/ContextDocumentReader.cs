using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-04-cf3d: reads one named meta file from EVERY context of a sandbox. The loaders
/// used to read it from the sandbox's representative alone, so a second context sharing the
/// toolchain image never reached the master (run a109: the frontend context's principles.md
/// was never read).
/// </summary>
public sealed class ContextDocumentReader(ISandboxFileReaderFactory readerFactory)
{
    /// <summary>
    /// The <paramref name="fileName"/> of each context's meta directory, in the sandbox's
    /// context order; a context without the file contributes nothing.
    /// </summary>
    public async Task<IReadOnlyList<ContextDocument>> ReadAsync(
        ISandbox sandbox, string sandboxKey, IEnumerable<RemoteContextDiscovery> contexts,
        string fileName, CancellationToken cancellationToken)
    {
        var reader = readerFactory.Create(sandbox);
        var documents = new List<ContextDocument>();
        foreach (var context in contexts)
        {
            var path = $"{ProjectMetaPaths.MetaDirFor(context.ContextName)}/{fileName}";
            var content = await reader.TryReadAsync(path, cancellationToken);
            if (content is null) continue;
            documents.Add(new ContextDocument(sandboxKey, context.ContextName, context.Workdir, path, content));
        }
        return documents;
    }
}
