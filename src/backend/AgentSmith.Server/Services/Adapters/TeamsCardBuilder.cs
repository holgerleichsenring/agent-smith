using System.Text.Json.Nodes;
using AgentSmith.Contracts.Dialogue;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Facade for building Adaptive Cards. Delegates to TeamsQuestionCardBuilder
/// and TeamsStatusCardBuilder.
/// </summary>
public sealed class TeamsCardBuilder(
    TeamsQuestionCardBuilder questionCardBuilder,
    TeamsStatusCardBuilder statusCardBuilder)
{
    public JsonObject BuildQuestionCard(DialogQuestion question) =>
        questionCardBuilder.Build(question);

    public JsonObject BuildInfoCard(string title, string text) =>
        statusCardBuilder.BuildInfo(title, text);

    public JsonObject BuildClarificationCard(string suggestion) =>
        statusCardBuilder.BuildClarification(suggestion);

    public JsonObject BuildAnsweredCard(string questionText, string answer) =>
        statusCardBuilder.BuildAnswered(questionText, answer);
}
