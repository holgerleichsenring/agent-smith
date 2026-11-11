namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-10-14a3g: what a wrong draft costs the person, asked of a design turn's reviewer beyond its citations —
/// conversation 43's review cut two citation ranges and passed a whole backend in one child, done lines no one could
/// answer, and a capability the material approved but the draft dropped. 2026-11-11-2bdfa: an epic child is asked
/// for a capability NO spec of the epic takes — conversation 46's reviewers judged capabilities a sibling held as
/// postponed by the child, because they could not see the siblings.
/// </summary>
public static class SpecAttackJudgement
{
    private const string Questions =
        "This is a draft in a design conversation. Beyond its citations, answer three questions, each with a cut or a "
        + "finding when the answer is no: can ONE coding run build this spec, and one reviewer check every claim of it "
        + "against the code? Can each done line be answered yes or no by reading the code or running a test? ";

    /// <summary>A lone spec's judgement.</summary>
    public const string Lone = Questions
        + "Does the scope keep every capability the uploaded material lists as approved or required — open what it lists "
        + "under reference:<name> and name any that is dropped or postponed?";

    /// <summary>An epic child's judgement, read beside the other specs of its epic.</summary>
    public const string Epic = Questions
        + "Does the scope, together with the other specs of this epic listed below, keep every capability the uploaded "
        + "material lists as approved or required — open what it lists under reference:<name> and name any that no spec "
        + "of this epic takes?";

    /// <summary>The judgement for <paramref name="input"/>, or empty outside a design turn.</summary>
    public static string For(SpecAttackInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.Proposal) return string.Empty;
        return "\n" + (input.Siblings.Count > 0 ? Epic : Lone) + "\n";
    }
}
