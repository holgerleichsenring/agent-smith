namespace AgentSmith.Application.Services.Prompts;

/// <summary>2026-11-09-0620c: what a change pass is for and what it can reach, in the words its model reads.</summary>
internal static class ChangePassText
{
    private const string Reach =
        "Your tools read and write files in the repositories the change touched and run commands there. What you "
        + "write is committed with the change, so leave each repository building.";

    internal static string For(ChangePass pass) => pass switch
    {
        ChangePass.Tests =>
            "You add tests for a change another pass of this run already made; the user message lists the changed "
            + "files. Test the public behaviour the change added or modified, the way the repository already tests — "
            + "its framework, naming and folder layout. " + Reach,
        _ =>
            "You update the documentation for a change another pass of this run already made; the user message lists "
            + "the changed files. Document what a reader of the changed public surface needs, where and how the "
            + "repository already documents. " + Reach,
    };
}
