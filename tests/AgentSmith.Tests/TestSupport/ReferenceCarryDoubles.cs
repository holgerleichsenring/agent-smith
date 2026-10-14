using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-10-14-dbcel: the real carry of an approval's uploaded material — the step's and the derivation's — over a
/// reader a test supplies; by default a process with no reference store, which carries nothing for a run that cites none.
/// </summary>
internal static class ReferenceCarryDoubles
{
    internal static ReferenceMaterializer Materializer(
        IReferenceSetReader? reader = null, IReferenceNotes? notes = null, ILogger<ReferenceMaterializer>? logger = null)
    {
        var sets = reader ?? new NoReferenceSetReader();
        var files = new SandboxFileReaderFactory();
        return new ReferenceMaterializer(ApprovedSetDoubles.Resolver(),
            new ReferenceSetCarrier(sets, new ReferenceSetMaterialiser(sets, files, new SandboxBinaryFileWriter()), new ReferenceGitExclusion(files), notes),
            new ReferenceImageCarrier(sets, new SandboxBinaryFileWriter(), new ReferenceGitExclusion(files)),
            new UploadImageAttachments(), logger ?? NullLogger<ReferenceMaterializer>.Instance);
    }
}
