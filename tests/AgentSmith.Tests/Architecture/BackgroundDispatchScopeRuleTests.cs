using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-09-22-476a: a service scope opened inside a fire-and-forget background task in
/// <c>AgentSmith.Server</c> must be an ASYNC scope. A background dispatch is what reaches a
/// pipeline, and a pipeline resolves <c>IPipelineSandboxCoordinator</c> — transient, and
/// <c>IAsyncDisposable</c> and nothing else. A transient disposable resolved from a scope is
/// tracked by that scope, so a synchronous <c>Dispose</c> throws "type only implements
/// IAsyncDisposable" after the reply is already written: the turn looks healthy, the rest of
/// the scope is never disposed, and the operator collects a FAIL line on every message.
/// </summary>
public sealed class BackgroundDispatchScopeRuleTests
{
    [Fact]
    public void EveryBackgroundTaskInTheServer_OpensAnAsyncScope()
    {
        var serverRoot = Path.Combine(ArchitectureSources.BackendRoot, "AgentSmith.Server");
        var violations = new List<string>();
        foreach (var file in Directory.EnumerateFiles(serverRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                continue;
            ScanText(File.ReadAllText(file), Path.GetFileName(file), violations);
        }

        violations.Should().BeEmpty(
            "a scope opened on a background task can reach a pipeline, whose sandbox "
            + "coordinator is async-only: close it with `await using var scope = "
            + "scopeFactory.CreateAsyncScope();`");
    }

    [Fact]
    public void Rule_HasTeeth_ASynchronousScopeOnABackgroundTask_IsFlagged()
    {
        var violations = new List<string>();
        ScanText(
            """
            class SyntheticBadDispatch
            {
                void Dispatch()
                {
                    _ = Task.Run(async () =>
                    {
                        using var scope = scopeFactory.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<Thing>().GoAsync();
                    });
                }
            }
            """, "Synthetic.cs", violations);

        violations.Should().ContainSingle().Which.Should().Contain("Synthetic.cs");
    }

    [Fact]
    public void Rule_HasTeeth_AnAsyncScopeOnABackgroundTask_IsNotFlagged()
    {
        var violations = new List<string>();
        ScanText(
            """
            class SyntheticGoodDispatch
            {
                void Dispatch()
                {
                    _ = Task.Run(async () =>
                    {
                        await using var scope = scopeFactory.CreateAsyncScope();
                        await scope.ServiceProvider.GetRequiredService<Thing>().GoAsync();
                    });
                }
            }
            """, "Synthetic.cs", violations);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Rule_IsScopedToBackgroundTasks_ASynchronousScopeAwaitedInLine_IsNotFlagged()
    {
        var violations = new List<string>();
        ScanText(
            """
            class SyntheticSweeper
            {
                async Task SweepAsync()
                {
                    using var scope = services.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<RunRepository>().ReadAsync();
                }
            }
            """, "Synthetic.cs", violations);

        violations.Should().BeEmpty();
    }

    // A background task is `Task.Run(` and what follows it up to the matching parenthesis.
    // Nothing subtler is needed: the server has no other way of detaching a dispatch, and a
    // rule that guessed at more shapes would flag scopes the defect cannot reach.
    private static void ScanText(string source, string fileName, List<string> violations)
    {
        const string marker = "Task.Run(";
        for (var at = source.IndexOf(marker, StringComparison.Ordinal); at >= 0;
             at = source.IndexOf(marker, at + marker.Length, StringComparison.Ordinal))
        {
            var body = LambdaBody(source, at + marker.Length - 1);
            if (!body.Contains("CreateScope()", StringComparison.Ordinal)) continue;
            violations.Add(
                $"{fileName}: a background task opens a synchronous service scope "
                + "(CreateScope) — a pipeline's sandbox coordinator is async-only.");
        }
    }

    private static string LambdaBody(string source, int openParen)
    {
        var depth = 0;
        for (var i = openParen; i < source.Length; i++)
        {
            if (source[i] == '(') depth++;
            else if (source[i] == ')' && --depth == 0) return source[openParen..(i + 1)];
        }
        return source[openParen..];
    }
}
