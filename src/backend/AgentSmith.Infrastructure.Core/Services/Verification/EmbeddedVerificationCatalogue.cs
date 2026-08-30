using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Infrastructure.Core.Services.Verification;

/// <summary>
/// 2026-08-30-0ea8: the verification standard baked into this assembly. The export is
/// checked in under Resources/ and its SHA256 is verified by the
/// <c>ValidateVerificationCatalogue</c> step in AgentSmith.Infrastructure.Core.csproj,
/// so every binary carries the exact release it was tested against and no run depends on
/// a third party still serving the asset it was built from.
/// </summary>
internal sealed class EmbeddedVerificationCatalogue(AsvsFlatExportParser parser) : IVerificationCatalogue
{
    internal const string ResourceName = "AgentSmith.VerificationCatalogue.json";

    private readonly Lazy<IReadOnlyList<VerificationRequirement>> _requirements =
        new(() => parser.Parse(Open()));

    public string Version => AsvsRelease.Tag;

    public IReadOnlyList<VerificationRequirement> Requirements => _requirements.Value;

    private static Stream Open() =>
        typeof(EmbeddedVerificationCatalogue).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded verification catalogue resource '{ResourceName}' not found in "
                + "AgentSmith.Infrastructure.Core — the checked-in export is not embedded.");
}
