using AgentSmith.Application.Services.Pocs;

namespace AgentSmith.Server.Services.Pocs.Import;

/// <summary>2026-11-23-933db: the guard's verdict and the revision it compared with (null when none was validated).</summary>
internal sealed record PocIdentityAnswer(PocMatch Match, int? Revision, int? PreviousPort);
