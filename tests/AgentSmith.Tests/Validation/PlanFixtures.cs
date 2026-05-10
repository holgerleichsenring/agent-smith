using System.Text.Json;

namespace AgentSmith.Tests.Validation;

internal static class PlanFixtures
{
    public static string ValidComplete() => Build(status: "complete", openQuestionCount: 0);
    public static string ValidNeedsUserInput() => Build(status: "needs_user_input", openQuestionCount: 1);

    public static string Build(
        string summary = "summary",
        string status = "complete",
        int openQuestionCount = 0,
        int stepReasonChars = 50,
        int summaryChars = 0)
    {
        var resolvedSummary = summaryChars > 0 ? new string('a', summaryChars) : summary;
        var openQuestions = Enumerable.Range(1, openQuestionCount)
            .Select(i => new
            {
                id = $"q{i}",
                question = "Question?",
                options = new[] { "yes", "no" }
            })
            .ToArray();

        var plan = new
        {
            summary = resolvedSummary,
            scope = new { files = Array.Empty<string>(), modules = Array.Empty<string>() },
            steps = new[]
            {
                new
                {
                    id = 1,
                    action = "do",
                    file = "src/Foo.cs",
                    reason = new string('r', stepReasonChars)
                }
            },
            open_questions = openQuestions,
            test_impact = (string?)null,
            consumer_impact = (string?)null,
            status = status
        };
        return JsonSerializer.Serialize(plan);
    }

    public static string ValidDiff() => JsonSerializer.Serialize(new
    {
        changes = new[]
        {
            new { file = "src/Foo.cs", operation = "modify", summary = "tweaks", patch = "@@" }
        },
        tests_added = Array.Empty<object>(),
        tests_modified = Array.Empty<object>(),
        build_status = "ok",
        test_status = "ok"
    });

    public static string ValidBootstrapComplete() => JsonSerializer.Serialize(new
    {
        status = "complete",
        files_written = new[]
        {
            new { path = ".agentsmith/context.yaml", kind = "context_yaml" }
        },
        open_questions = Array.Empty<object>()
    });

    public static string ValidBootstrapNeedsUserInput() => JsonSerializer.Serialize(new
    {
        status = "needs_user_input",
        files_written = Array.Empty<object>(),
        open_questions = new[]
        {
            new { id = "q1", question = "What stack?", options = new[] { "go", "ts" } }
        }
    });

    public static string SingleObservation() => JsonSerializer.Serialize(new[]
    {
        new
        {
            concern = "correctness",
            description = "found something",
            blocking = false,
            severity = "low",
            confidence = 60
        }
    });
}
