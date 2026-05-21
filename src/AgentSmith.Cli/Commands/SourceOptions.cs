using System.CommandLine;
using System.CommandLine.Invocation;
using AgentSmith.Contracts.Commands;

namespace AgentSmith.Cli.Commands;

/// <summary>
/// Shared source-override CLI options for commands that use source checkout.
/// p0158d: `--repo NAME` scopes a multi-repo run to a single configured repo
/// (populates ContextKeys.SourceOverrideRepo, read by ExecutePipelineUseCase).
/// The multi-repo guard (rejecting `--source-*` without `--repo` when the
/// project has &gt;1 repo) lives in ExecutePipelineUseCase, where the resolved
/// project is in scope.
/// </summary>
internal sealed class SourceOptions
{
    public Option<string?> Repo { get; } = new("--repo", "Scope to a single configured repo by name (multi-repo projects)");
    public Option<string?> Type { get; } = new("--source-type", "Source type (local, github, gitlab, azurerepos)");
    public Option<string?> Path { get; } = new("--source-path", "Local repository path");
    public Option<string?> Url { get; } = new("--source-url", "Remote repository URL");
    public Option<string?> Auth { get; } = new("--source-auth", "Source auth method override");

    public void AddTo(Command command)
    {
        command.Add(Repo);
        command.Add(Type);
        command.Add(Path);
        command.Add(Url);
        command.Add(Auth);
    }

    public void ApplyTo(InvocationContext ctx, Dictionary<string, object> context)
    {
        var repo = ctx.ParseResult.GetValueForOption(Repo);
        var type = ctx.ParseResult.GetValueForOption(Type);
        var path = ctx.ParseResult.GetValueForOption(Path);
        var url = ctx.ParseResult.GetValueForOption(Url);
        var auth = ctx.ParseResult.GetValueForOption(Auth);

        if (!string.IsNullOrWhiteSpace(repo))
            context[ContextKeys.SourceOverrideRepo] = repo;
        if (!string.IsNullOrWhiteSpace(type))
            context[ContextKeys.SourceType] = type;
        if (!string.IsNullOrWhiteSpace(path))
            context[ContextKeys.SourcePath] = System.IO.Path.GetFullPath(path);
        if (!string.IsNullOrWhiteSpace(url))
            context[ContextKeys.SourceUrl] = url;
        if (!string.IsNullOrWhiteSpace(auth))
            context[ContextKeys.SourceAuth] = auth;
    }
}
