using AgentSmith.Application.Models.Sandbox;
using AgentSmith.Application.Services.Sandbox.Registry;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// 2026-10-12-50bdb: a registry that answers without the network. By default every image
/// exists, so a test about the write path is not told about registries it never asked about.
/// </summary>
internal sealed class FixedImageReferenceCheck(Func<string, ImageReferenceVerdict>? answer = null)
    : IImageReferenceCheck
{
    public List<string> Asked { get; } = [];

    public Task<ImageReferenceVerdict> CheckAsync(string image, CancellationToken cancellationToken)
    {
        Asked.Add(image);
        return Task.FromResult(answer?.Invoke(image) ?? ImageReferenceVerdict.Exists(image));
    }
}
