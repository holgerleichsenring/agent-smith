namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-09-25-8e51e: where the ticket a conversation is bound to and the specification somebody
/// approved for it do not say the same thing — COMPUTED, never suggested.
/// <para>
/// The turn is handed this and told to put it to the person. A prompt instruction with no
/// computed input ("check whether the ticket still matches") is a hallucination surface: the
/// model would have to invent the comparison, and it would invent it differently every turn.
/// </para>
/// </summary>
/// <param name="Phases">How many phases the approved specification holds.</param>
/// <param name="Unsaid">The approved goals the ticket's own text does not carry, verbatim.</param>
public sealed record SetDivergence(int Phases, IReadOnlyList<string> Unsaid);
