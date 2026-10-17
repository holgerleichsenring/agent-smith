using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-17-a196c: the head of the pass the loop opens for unresolved met claims — each claim with why its
/// citation resolves against nothing, the form a citation takes, and the commands this spec ran exactly as
/// they can be cited (the last <see cref="ListedCommands"/>; a shortened one is cited as shown).
/// </summary>
public static class AcceptanceClaimNudge
{
    public const int ListedCommands = 30;

    public static string Build(IReadOnlyList<string> unresolved, PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(unresolved);
        var ran = SpecEvidence.From([], pipeline).TakeLast(ListedCommands).ToList();
        return "Your verdict claims these criteria met, but what each one cites resolves against nothing this "
               + "spec changed or ran:\n" + string.Join("\n", unresolved.Select(u => "- " + u))
               + "\n\nIn \"citations\", a met claim names one path this spec changed, or one command copied character "
               + "for character from between the quotes of a line below. An absence is cited by the run_command search "
               + "that found nothing; a build or test by the command or gate stage that ran it. Run what you have not "
               + "run, then give your verdict again.\n\nCommands this spec ran:\n"
               + (ran.Count == 0 ? "- (none yet)" : string.Join("\n", ran.Select(r => "- " + r))) + "\n\n";
    }
}
