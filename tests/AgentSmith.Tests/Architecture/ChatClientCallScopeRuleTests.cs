using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0176a: build-time gate guarding the structural fix from p0176b. Every
/// direct call to <c>IChatClient.GetResponseAsync</c> in
/// <c>AgentSmith.Application</c> + <c>AgentSmith.Infrastructure</c> must
/// occur inside a method that also opens an <c>IRunContextAccessor.BeginCallScope</c>
/// using-statement. Without this, the LlmCallFinished event emitted by the
/// factory-wrapped decorator carries no role / phase / repo attribution,
/// and the next handler added to the codebase silently regresses the
/// per-call cost attribution wired up in this slice.
///
/// Exemptions (recognised at file-name level, not by attribute):
///   - <c>EventPublishingChatClient.cs</c> — the decorator that emits the
///     events; calls <c>inner.GetResponseAsync</c> after reading the scope.
///   - <c>TracingChatClient.cs</c> — pass-through decorator; the scope is
///     opened by the outermost caller and flows through AsyncLocal.
///   - <c>RetryCoordinator.cs</c> — runtime retry loop invoked by
///     <c>SkillCallRuntime</c>, which opens <c>BeginCallScope</c> around the
///     dispatch; the AsyncLocal scope is live throughout the retry.
/// </summary>
public sealed class ChatClientCallScopeRuleTests
{
    private static readonly string[] ExemptFileNames =
    [
        "EventPublishingChatClient.cs",
        "TracingChatClient.cs",
        "RetryCoordinator.cs",
        // p0188: pass-through decorator; the outer call (master handler, sub-agent,
        // analyzer, etc.) opens BeginCallScope and the scope flows via AsyncLocal
        // through the rate-limit wait + the inner GetResponseAsync.
        "RateLimitingChatClient.cs",
        // p0374: pass-through retry decorator — re-delegates the SAME materialised,
        // already-scoped messages on a transient network drop; the outer call's
        // BeginCallScope flows via AsyncLocal through the retry + backoff.
        "TransientRetryChatClient.cs",
        // p0191: pass-through decorator that mutates the message list before
        // delegating; the outer call's BeginCallScope is still live.
        "SensitiveToolHistoryScrubChatClient.cs",
        // p0341c: the master loop's in-pass governor — a DelegatingChatClient that sits
        // below UseFunctionInvocation and delegates each already-scoped tool iteration
        // (AgenticLoopRunner opened BeginCallScope; the scope flows via AsyncLocal). It
        // only mutates the message list (reminder injection) + checks the budget fence.
        "MasterLoopGovernorChatClient.cs",
        // p0341d: pass-through compaction decorator — reduces the already-scoped message
        // list in-flight and delegates; the outer master call's BeginCallScope is still live
        // (the summarizer call it makes goes through the instrumented factory path).
        "CompactingChatClient.cs",
        // p0293: ChatClientFactory.ProbeAsync (connection diagnostics) calls the
        // BARE builder client directly — no EventPublishingChatClient wrapper — so
        // it emits NO LlmCallFinished event. With nothing to attribute, the
        // BeginCallScope requirement does not apply to this out-of-run 1-token probe.
        "ChatClientFactory.cs",
    ];

    private static readonly string[] TargetProjectDirs =
    [
        "AgentSmith.Application",
        "AgentSmith.Infrastructure",
        "AgentSmith.Infrastructure.Core",
    ];

    [Fact]
    public void EveryDirectGetResponseAsyncCall_IsInsideAMethodThatOpensBeginCallScope()
    {
        var srcRoot = ResolveSrcBackendRoot();
        var violations = new List<string>();

        foreach (var dir in TargetProjectDirs)
        {
            var projectRoot = Path.Combine(srcRoot, dir);
            if (!Directory.Exists(projectRoot)) continue;
            foreach (var file in Directory.EnumerateFiles(projectRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (ExemptFileNames.Contains(Path.GetFileName(file))) continue;
                ScanFile(file, violations);
            }
        }

        violations.Should().BeEmpty(
            "every direct IChatClient.GetResponseAsync call must be enclosed by " +
            "an IRunContextAccessor.BeginCallScope using-statement in the same method. " +
            "If the call lives in a decorator or runtime collaborator that legitimately " +
            "delegates an already-scoped invocation, add the file name to " +
            "ChatClientCallScopeRuleTests.ExemptFileNames with a brief justification.");
    }

    [Fact]
    public void Rule_HasTeeth_SyntheticViolatorWithoutScope_GetsFlagged()
    {
        const string syntheticSource = """
            using Microsoft.Extensions.AI;
            class SyntheticBadHandler
            {
                private readonly IChatClient _chat = null!;
                public async Task RunAsync()
                {
                    var response = await _chat.GetResponseAsync(messages: null, options: null, ct: default);
                }
            }
            """;
        var violations = new List<string>();
        ScanText(syntheticSource, "Synthetic.cs", violations);
        violations.Should().NotBeEmpty();
        violations[0].Should().Contain("Synthetic.cs");
        violations[0].Should().Contain("RunAsync");
    }

    [Fact]
    public void Rule_HasTeeth_SyntheticPassingCase_DoesNotFlag()
    {
        const string syntheticSource = """
            using Microsoft.Extensions.AI;
            class SyntheticGoodHandler
            {
                private readonly IChatClient _chat = null!;
                private readonly IRunContextAccessor _runContext = null!;
                public async Task RunAsync()
                {
                    using var _scope = _runContext.BeginCallScope("role", "phase");
                    var response = await _chat.GetResponseAsync(messages: null, options: null, ct: default);
                }
            }
            """;
        var violations = new List<string>();
        ScanText(syntheticSource, "Synthetic.cs", violations);
        violations.Should().BeEmpty();
    }

    private static void ScanFile(string path, List<string> violations)
    {
        var text = File.ReadAllText(path);
        ScanText(text, Path.GetFileName(path), violations);
    }

    // Method body scan: walks the file line-by-line tracking brace depth so
    // we can identify method enclosures. When `.GetResponseAsync(` appears,
    // checks that some earlier line in the SAME method body (same enclosing
    // brace block opened by a method signature) calls `BeginCallScope`.
    // Heuristic — does not parse C# — but matches the patterns this slice
    // installs across the codebase. Synthetic-violator + passing-case tests
    // above pin the heuristic to the cases that matter.
    private static void ScanText(string source, string fileName, List<string> violations)
    {
        var lines = source.Split('\n');
        var braceDepth = 0;
        var methodOpenDepth = -1;
        var currentMethodName = "<file-scope>";
        var sawBeginCallScopeInMethod = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Detect method signature at the top of the next opening brace.
            // Match "<modifiers> <ret> Name(...)" patterns. Heuristic only.
            if (braceDepth >= 1 && methodOpenDepth < 0)
            {
                var name = TryExtractMethodName(line);
                if (name is not null) currentMethodName = name;
            }

            foreach (var ch in line)
            {
                if (ch == '{')
                {
                    braceDepth++;
                    if (methodOpenDepth < 0 && currentMethodName != "<file-scope>")
                    {
                        methodOpenDepth = braceDepth;
                        sawBeginCallScopeInMethod = false;
                    }
                }
                else if (ch == '}')
                {
                    if (braceDepth == methodOpenDepth)
                    {
                        methodOpenDepth = -1;
                        currentMethodName = "<file-scope>";
                        sawBeginCallScopeInMethod = false;
                    }
                    braceDepth--;
                }
            }

            if (methodOpenDepth > 0)
            {
                if (line.Contains("BeginCallScope"))
                    sawBeginCallScopeInMethod = true;

                if (line.Contains(".GetResponseAsync(") && !sawBeginCallScopeInMethod)
                {
                    violations.Add(
                        $"{fileName}:{i + 1} method '{currentMethodName}' calls " +
                        "IChatClient.GetResponseAsync without a preceding " +
                        "IRunContextAccessor.BeginCallScope using-statement.");
                }
            }
        }
    }

    // Recognises lines like:
    //   public async Task<X> FooAsync(...)
    //   private static int Bar(...)
    //   public void Run(...)
    // Skips ctor / property accessor / event handler shapes. False positives
    // here only matter when a method genuinely calls GetResponseAsync — the
    // synthetic-passing-case test guards against that.
    private static string? TryExtractMethodName(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*"))
            return null;
        // Pattern: ... <name>( ... ) with a balanced paren — return name.
        var paren = trimmed.IndexOf('(');
        if (paren < 0) return null;
        var before = trimmed[..paren].TrimEnd();
        var spaceIdx = before.LastIndexOfAny(new[] { ' ', '\t' });
        if (spaceIdx < 0) return null;
        var candidate = before[(spaceIdx + 1)..];
        if (candidate.Length == 0 || !char.IsLetter(candidate[0]) && candidate[0] != '_') return null;
        if (candidate.Contains('=') || candidate.Contains('"')) return null;
        // Filter out keywords like "if", "while", "switch".
        if (candidate is "if" or "while" or "switch" or "for" or "foreach" or "using" or "lock" or "return") return null;
        return candidate;
    }

    private static string ResolveSrcBackendRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "src", "backend");
            if (Directory.Exists(candidate)) return candidate;
            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }
        throw new InvalidOperationException(
            $"Could not locate src/backend from test base directory '{AppContext.BaseDirectory}'");
    }
}
