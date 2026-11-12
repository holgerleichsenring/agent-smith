using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.SpecDialog.Writes;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Tests.SpecDialog.Writes;

/// <summary>2026-11-12-cd4ca: the real write checks, and drafts to write.</summary>
internal static class DraftWriteRig
{
    public static readonly IReadOnlyList<string> Scope = ["api"];

    public static TicketDraftWrite Tickets()
    {
        var orderer = new TicketGraphOrderer();
        return new TicketDraftWrite(new TicketGraphChecker(orderer), orderer);
    }

    public static SpecDraftWrite Specs() => new(
        new SpecDraftIds(new SeriesIdFactory(TimeProvider.System)),
        new SpecDraftValidator(new SpecSchemaProvider()), DialogTestRepos.Real(), new SpecDraftReader(),
        new SpecDraftShape(new RequiresEdgeChecker()));

    public static ContractDraftWrite Contracts() => new(TestSupport.SeriesContracts.Reader());

    public static GraphTicket Ticket(string key, params string[] requires) =>
        new(key, $"Title of {key}", $"Requirement of {key}.", ["it is done"], requires, []);

    public static string Spec(string id, string goal = "A goal", params string[] requires) =>
        $"spec: {id}\ngoal: \"{goal}\"\ndone:\n  - \"it holds\""
        + (requires.Length == 0 ? string.Empty : $"\nrequires: [{string.Join(", ", requires)}]");

    public static ProposalDraft Written(this DraftWrite write) =>
        write.Next ?? throw new InvalidOperationException($"refused: {write.Refusal}");
}
