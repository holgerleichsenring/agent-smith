using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;

namespace AgentSmith.Tests.Persistence.SpecRename;

/// <summary>
/// 2026-10-12-967ab: every stored text in the shape the release before the rename wrote — this
/// release's real serializers, with the spec names put back as the phase names (<see cref="SpecShape"/>),
/// the old command, kind, type and key names spelled out, and the nested texts escaped as stored.
/// </summary>
internal static class OldShapeJson
{
    public static readonly DateTimeOffset T = ApprovedSets.Noon;
    public static readonly SpecDraft Draft = new("s1", "Read both", "spec: s1", ["s0"]) { Done = ["It reads."] };
    public static readonly SpecApprovalRecord Approval = ApprovedSets.Record("k", T, ["s1", "s2"], ["api"]);
    public static readonly SpecSequenceProgress Progress = new([new SpecProgress("s1", "Read both", SpecRunState.Done)]);

    private static readonly Dictionary<string, string> Kinds = new(SpecShape.Ids) { ["spec"] = "phase" };

    public static string Event(RunEvent e) => SpecShape.Of(JsonSerializer.Serialize(e, e.GetType()))
        .Replace(CommandNames.SelectSpec, "SelectPhaseCommand");

    public static string Commands() => JsonSerializer.Serialize(new List<CheckpointCommand>
    {
        new("VerifyPhaseCommand:api", SpecId: "s1"), new(CommandNames.AgenticMaster),
    }).Replace("\"SpecId\"", "\"PhaseId\"");

    public static string Context() => JsonSerializer.Serialize(new[]
    {
        Entry("PhaseSpec", "AgentSmith.Contracts.Models.PhaseDraft, AgentSmith.Contracts", SpecShape.Of(JsonSerializer.Serialize(Draft))),
        Entry(ContextKeys.ApprovedSpecSet, typeof(string).AssemblyQualifiedName!, JsonSerializer.Serialize(ApprovedSet())),
        Entry(ContextKeys.SpecSequenceProgress, typeof(SpecSequenceProgress).AssemblyQualifiedName!,
            SpecShape.Of(JsonSerializer.Serialize(Progress))),
    });

    public static string ApprovedSet() => SpecShape.Of(SpecApprovalJson.Write(Approval));

    public static string Acceptance() => SpecShape.Of(RunStoryJson.Serialize(
        new AcceptanceView([], "met", "operator", Declined: [new DeclinedCriterionView("c", "r", "s1")])), Kinds);

    public static string PlannedSteps() => SpecShape.Of(RunStoryJson.Serialize(new List<PlannedStepView>
    {
        new(4, CommandNames.SelectSpec, "Start the spec", "s1"),
    })).Replace(CommandNames.SelectSpec, "SelectPhaseCommand");

    public static string ResumePayload() => JsonSerializer.Serialize(new Dictionary<string, object>
    {
        ["Commands"] = JsonDocument.Parse(Commands()).RootElement,
        ["ContextJson"] = Context(), ["ExecutionCount"] = 7, ["QuestionJson"] = "{}", ["AnswerJson"] = "{}",
    });

    public static string InitialContext() => JsonSerializer.Serialize(new Dictionary<string, object>
    {
        [ContextKeys.ApprovedSpecSet] = ApprovedSet(), [ContextKeys.ResumeCheckpoint] = ResumePayload(),
    });

    public static string Request() =>
        $$"""{"ProjectName":"p","PipelineName":"code","Context":{{InitialContext()}}}""";

    public static string Outcome(OutcomeProposal proposal) =>
        SpecShape.Of(OutcomeProposalJson.Write(proposal), Kinds).Replace("\"kind\":\"spec\"", "\"kind\":\"phase\"");

    public static string Transcript() => SpecDialogSessionMapper.WriteTranscript(
            [new TranscriptTurn(TranscriptRole.Assistant, "Here it is.", T, SpecDialogTurnKind.SpecProposal)])
        .Replace("specProposal", "phaseProposal");

    public const string Filing =
        """{"filed":[{"reference":"R-1","title":"s1: read both"}],"error":null,"at":"2026-10-12T09:00:00Z","kind":"phase"}""";

    private static object Entry(string key, string type, string value) => new { K = key, T = type, V = value };
}
