using AgentSmith.Application.Models.Sandbox;

namespace AgentSmith.Application.Services.Sandbox.Registry;

/// <summary>
/// 2026-10-12-50bdb: asks an image's registry whether it has the image. Never throws for a
/// lookup that cannot answer — that is <see cref="ImageReferenceState.Unverified"/>.
/// </summary>
public interface IImageReferenceCheck
{
    Task<ImageReferenceVerdict> CheckAsync(string image, CancellationToken cancellationToken);
}
