using AgentSmith.PipelineHarness.Presets;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0199b operator-facing entry point for the docker-tier flow. Prints
/// readable single-line summaries: container lifecycle, pipeline result,
/// WIP-branch presence on the fake remote. Used by the --docker console
/// flag so an operator has ONE command to know the full pipeline works
/// locally without touching the UI or triggering a real ticket.
/// </summary>
internal static class DockerPresetRunner
{
    public static async Task<int> RunAsync(string preset)
    {
        if (preset != "fix-bug")
        {
            Console.Error.WriteLine(
                $"--docker currently supports only 'fix-bug' (p0199b scope). Other presets land in p0199c.");
            return 2;
        }
        Environment.SetEnvironmentVariable(DockerAvailability.OptInEnv, "1");
        if (!DockerAvailability.IsAvailable(out var detail))
        {
            Console.Error.WriteLine("Docker not available: " + detail);
            return 2;
        }

        Console.WriteLine($"=== docker-tier fix-bug ===");
        await using var session = await DockerHarnessSession.CreateAsync(FixturePaths.CsharpFixtureSource());
        Console.WriteLine($"bare repo : {session.BareRepoPath}");
        Console.WriteLine($"working   : {session.WorkingCopyPath}");

        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Docker), SandboxBackend.Docker, session);
        harness.ChatClient
            .EnqueueToolCall("write_file", """{"path":"primary/NOTE.md","content":"docker-harness-run"}""")
            .EnqueueText("Edit applied.");

        var runner = new PipelineRunner(harness.Services)
        {
            RepoOverride = DockerHarnessRepo.For(session),
        };
        var result = await runner.RunAsync("fix-bug");

        Console.WriteLine($"spawned   : {harness.DockerSandboxFactory!.Spawned.Count} sandbox container(s)");
        Console.WriteLine($"branches  : {string.Join(", ", session.BareBranches())}");
        Console.WriteLine($"result    : {(result.IsSuccess ? "SUCCESS" : "FAIL")} — {result.Message}");
        return result.IsSuccess ? 0 : 1;
    }
}
