using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Tests.Verification;

/// <summary>
/// 2026-08-30-0ea8: a catalogue the test authors, so the lens can be handed an entry the
/// checked-in table was never written against.
/// </summary>
internal sealed record StubVerificationCatalogue(
    string Version,
    IReadOnlyList<VerificationRequirement> Requirements) : IVerificationCatalogue;
