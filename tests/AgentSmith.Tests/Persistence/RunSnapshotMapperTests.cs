using System.Linq;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// p0246f: the persisted Run (read from the DB) maps to the dashboard's
/// RunSnapshot contract, so the run list/detail can be served from the
/// system-of-record (survives restart + Redis flush), not just the in-memory
/// broadcaster snapshots.
/// </summary>
public sealed class RunSnapshotMapperTests
{
    [Fact]
    public void ToSnapshot_MapsRunWithChildren_ToDashboardContract()
    {
        var run = new Run
        {
            Id = "run-1", Pipeline = "fix-bug", Trigger = "ticket", Status = "success",
            Summary = "Fixed it", StartedAt = DateTimeOffset.Parse("2026-06-07T10:00:00Z"),
            FinishedAt = DateTimeOffset.Parse("2026-06-07T10:05:00Z"), CostTotalUsd = 0.07m,
            TicketId = "42", TicketTitle = "The bug", AgentName = "claude", CancelRequested = false,
            Repos = [new RunRepo { RepoName = "primary", PrStatus = "opened", PrUrl = "https://pr/1" }],
            Steps =
            [
                new RunStep { StepIndex = 0, StepName = "LoadCatalog", Status = "ok" },
                new RunStep { StepIndex = 5, StepName = "AgenticMaster", DisplayName = "Agent", Status = "ok" },
            ],
            LlmCalls = [new RunLlmCall { Model = "gpt-4.1" }, new RunLlmCall { Model = "gpt-4.1" }],
            Sandboxes = [new RunSandbox { Key = "primary" }],
        };

        var snap = RunSnapshotMapper.ToSnapshot(run);

        snap.RunId.Should().Be("run-1");
        snap.Pipeline.Should().Be("fix-bug");
        snap.Status.Should().Be("success");
        snap.Summary.Should().Be("Fixed it");
        snap.Repos.Should().ContainSingle().Which.Should().Be("primary");
        snap.PrUrl.Should().Be("https://pr/1", "the opened PR's url surfaces");
        snap.StepIndex.Should().Be(5, "the latest step by index");
        snap.StepName.Should().Be("Agent", "the display name is preferred over the raw step name");
        snap.CostUsd.Should().Be(0.07m);
        snap.LlmCalls.Should().Be(2);
        snap.Sandboxes.Should().Be(1);
        snap.TicketId.Should().Be("42");
        snap.TicketTitle.Should().Be("The bug");
        snap.AgentName.Should().Be("claude");
        snap.Title.Should().Be("The bug", "the ticket title is the dashboard heading");
    }

    [Fact]
    public void Mapper_RunningRun_ShowsProducerTotal_NotStepRowCount()
    {
        // p0322a: step rows are created one-per-STARTED-step, so max(StepIndex)
        // == Steps.Count always and the list rendered x/x forever. The persisted
        // producer total (grows as BootstrapDispatch splices rounds) is the y.
        var run = new Run
        {
            Id = "run-1", Pipeline = "init-project", Status = "running", TotalSteps = 13,
            Steps =
            [
                new RunStep { StepIndex = 0, StepName = "LoadCatalog", Status = "ok" },
                new RunStep { StepIndex = 1, StepName = "PipelineNameInitializer", Status = "ok" },
                new RunStep { StepIndex = 2, StepName = "FetchTicket", Status = "ok" },
                new RunStep { StepIndex = 3, StepName = "CheckoutSource", Status = "running" },
            ],
        };

        var snap = RunSnapshotMapper.ToSnapshot(run);

        snap.StepIndex.Should().Be(3);
        snap.TotalSteps.Should().Be(13, "the producer's live total, not the step-row count");
    }

    [Fact]
    public void Mapper_PreMigrationRun_NoPersistedTotal_FallsBackToStepRowCount()
    {
        // p0322a: rows written before the TotalSteps column keep the old lower
        // bound (exact once finished).
        var run = new Run
        {
            Id = "run-1", Pipeline = "fix-bug", Status = "success", TotalSteps = null,
            Steps = [new RunStep { StepIndex = 0, StepName = "LoadCatalog", Status = "ok" }],
        };

        RunSnapshotMapper.ToSnapshot(run).TotalSteps.Should().Be(1);
    }

    [Fact]
    public void Snapshot_ResourceTime_ComputedFromLifetimesAndRequests()
    {
        // p0332: reserved capacity-time = memory request x pod lifetime, summed
        // over sandboxes + the spawned orchestrator. It is a RESERVATION figure
        // (what a requests-based quota counted for the run), not consumption.
        var start = DateTimeOffset.Parse("2026-07-13T10:00:00Z");
        var run = new Run
        {
            Id = "run-1", Pipeline = "fix-bug", Status = "success",
            StartedAt = start, FinishedAt = start.AddMinutes(10),
            JobId = "job-1", // p0330: a spawned orchestrator pod lived start->finish
            Sandboxes =
            [
                // 8 minutes x 1Gi declared request = 8 Gi·min.
                new RunSandbox
                {
                    Key = "api", RepoName = "api", MemoryRequest = "1Gi",
                    SpawnedAt = start.AddMinutes(1), DisposedAt = start.AddMinutes(9),
                },
                // No close event: the run end is the closest honest boundary.
                // 5 minutes x default 1Gi request = 5 Gi·min.
                new RunSandbox { Key = "web", RepoName = "web", SpawnedAt = start.AddMinutes(5) },
                // Pre-p0332 row without a lifetime: contributes nothing.
                new RunSandbox { Key = "old", RepoName = "old" },
            ],
        };

        // Orchestrator: 10 minutes x 512Mi = 5 Gi·min. Total 8 + 5 + 5 = 18.
        var snap = RunSnapshotMapper.ToSnapshot(run, orchestratorMemoryRequest: "512Mi");

        snap.ReservedGiMinutes.Should().BeApproximately(18.0, 0.001);
    }

    [Fact]
    public void Snapshot_ResourceTime_NullWhileRunning_AndOnPreMigrationRows()
    {
        // A running run has no honest lifetime yet; a pre-p0332 run (no sandbox
        // lifetimes, no JobId) must show NOTHING rather than a fake zero.
        var start = DateTimeOffset.Parse("2026-07-13T10:00:00Z");

        var running = new Run
        {
            Id = "r1", Pipeline = "fix-bug", Status = "running", StartedAt = start, JobId = "job-1",
            Sandboxes = [new RunSandbox { Key = "api", RepoName = "api", SpawnedAt = start }],
        };
        RunSnapshotMapper.ToSnapshot(running).ReservedGiMinutes.Should().BeNull();

        var preMigration = new Run
        {
            Id = "r2", Pipeline = "fix-bug", Status = "success",
            StartedAt = start, FinishedAt = start.AddMinutes(3),
            Sandboxes = [new RunSandbox { Key = "api", RepoName = "api" }],
        };
        RunSnapshotMapper.ToSnapshot(preMigration).ReservedGiMinutes.Should().BeNull();
    }

    [Fact]
    public void Snapshot_LiveCompute_FromSpawnedSandboxes_NullWhenNone()
    {
        // p0348: COMPUTE shows the pods that ACTUALLY spawned (RunSandbox rows),
        // not the over-counting reservation. Their memory requests sum.
        var run = new Run
        {
            Id = "run-1", Pipeline = "fix-bug", Status = "running",
            Sandboxes =
            [
                new RunSandbox
                {
                    Key = "server", RepoName = "server",
                    ToolchainImage = "dotnet/sdk:8.0", MemoryRequest = "1Gi", Status = "created",
                },
                new RunSandbox
                {
                    Key = "bgw", RepoName = "backgroundworker",
                    ToolchainImage = "dotnet/sdk:8.0", MemoryRequest = "2Gi", Status = "created",
                },
            ],
        };

        var compute = RunSnapshotMapper.ToSnapshot(run).LiveCompute;

        compute.Should().NotBeNull();
        compute!.Pods.Should().HaveCount(2);
        compute.Pods.Select(p => p.Repo).Should().Contain(["server", "backgroundworker"]);
        compute.TotalMem.Should().Be("3Gi");

        // No spawned sandboxes (in-process run, or pods not yet up) → null, so the
        // client renders "calculating…"/omits, never a fabricated count.
        var noBoxes = new Run { Id = "r2", Pipeline = "fix-bug", Status = "running" };
        RunSnapshotMapper.ToSnapshot(noBoxes).LiveCompute.Should().BeNull();
    }

    [Fact]
    public void ToSnapshot_EmptyTicketId_MapsToNull_TitleFallsBackToPipeline()
    {
        var run = new Run { Id = "r", Pipeline = "security-scan", Status = "running", TicketId = "" };

        var snap = RunSnapshotMapper.ToSnapshot(run);

        snap.TicketId.Should().BeNull("an empty ticket id is absent, not a literal empty string");
        snap.Title.Should().Be("security-scan", "no ticket → the pipeline name is the title");
        snap.Trigger.Should().Be("unknown", "a null trigger maps to the contract's unknown");
    }
}
