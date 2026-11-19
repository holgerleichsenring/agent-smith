using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>
/// 2026-11-18-9c1cb: a review-objection question as a run before 9c1cb posted it. No run asks one any more; the
/// tickets that carry one must still read, pin and fold as they did, so the tests build the stored shape here.
/// </summary>
internal static class ReviewObjectionFixtures
{
    public const string BuildAsReviewed = "build the spec as reviewed; the objection stays on record";
    public const string ChangeAsAsked = "change the spec as the objection asks";

    private static readonly DateTimeOffset Asked = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    public static SpecHandback For(params string[] specIds) =>
        new(SpecHandbackCase.Question,
            "After its review rounds, what follows has no review that ended clean on its exact text.\n"
            + string.Join("\n", specIds.Select(id =>
                $"\n{ReviewObjectionComment.MarkerFor(id)}:\n- The cited handler does not exist. — shop/src/Handler.cs:12 [A1]")),
            Readings: [BuildAsReviewed, ChangeAsAsked],
            Taken: 0) { QuestionKind = SpecQuestionKind.ReviewObjection };

    public static TicketComment OurObjection(string specId) => Ours(For(specId));

    public static TicketComment Ours(SpecHandback handback) =>
        new("agent-smith", Asked, SpecHandbackComment.Build(handback, null, string.Empty));
}
