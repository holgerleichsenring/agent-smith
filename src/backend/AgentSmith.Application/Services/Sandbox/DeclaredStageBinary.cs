namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-08-31-7097: one binary a DECLARED verify stage names, together with the stage
/// and context that named it — so a report about the binary can say who asked for it.
/// </summary>
/// <param name="Binary">The bare command name the stage's command begins with.</param>
/// <param name="ContextName">The context whose context.yaml declares the stage.</param>
/// <param name="StageLabel">The stage's own label ("build", "lint", …).</param>
public sealed record DeclaredStageBinary(string Binary, string ContextName, string StageLabel);
