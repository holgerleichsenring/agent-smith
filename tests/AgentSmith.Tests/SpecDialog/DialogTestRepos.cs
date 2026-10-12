using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>The real repos decision of a design dialog, for tests that compose its parsers by hand.</summary>
internal static class DialogTestRepos
{
    public static DialogDraftRepos Real() => new(new SpecReposRule(), new PhaseDraftReposWriter());
}
