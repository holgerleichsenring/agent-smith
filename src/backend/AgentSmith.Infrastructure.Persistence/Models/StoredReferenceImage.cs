namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>
/// 2026-10-01-283da: one image with its bytes, whichever table it was read from — a legacy row's
/// base64 is decoded on the way out, so no caller knows there are two.
/// </summary>
public sealed record StoredReferenceImage(long Id, string SessionId, string MediaType, byte[] Content);
