namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-11-09-0620c/k: the opening and closing of a pull-request review's user prompt, inside the review prompt's own
/// sections. It used to be the security-scan prompt word for word, because the review master declares the same output
/// schema. The skill says how to judge the diff; this says what the checkout is and what the framework parses.
/// </summary>
internal static class PrReviewPrompt
{
    internal const string Opening =
        """
        This is a review of a pull request's change. The repository is checked out at
        its head; your tools read it and nothing you do changes it.
        """;

    internal const string Closing =
        "The framework parses your last message as the JSON observation array your methodology describes.";
}
