using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Specs;

/// <summary>
/// p0393a: what one derivation call produced — the ordered set, plus the
/// ticket-embedded instructions that got NO SLOT.
/// <para>
/// Derivation is also the trust boundary. Ticket text is third-party input; the
/// transform is where prose becomes a schema-validated artifact before anything
/// acts on it, which is the security argument for the step independently of the
/// loop argument. An instruction that is not a requirement has nowhere to land and
/// is reported through the p0316 refusal record instead of quietly becoming scope.
/// </para>
/// </summary>
public sealed record SpecDerivation(
    SpecSet Set,
    IReadOnlyList<IgnoredInstruction> IgnoredInstructions);
