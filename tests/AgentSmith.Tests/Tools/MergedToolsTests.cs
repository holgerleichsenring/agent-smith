using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// 2026-11-18-9c1cd: each merged tool does the job of the tools it replaced — one test per merge — and a part that
/// cannot act answers why instead of disappearing.
/// </summary>
public sealed class MergedToolsTests
{
    private readonly List<(string Tool, AIFunctionArguments Args)> _calls = [];

    private AIFunction Fake(string name) =>
        AIFunctionFactory.Create((AIFunctionArguments args) =>
        {
            _calls.Add((name, args));
            return $"{name} called";
        }, name);

    private static async Task<string> Invoke(AIFunction tool, params (string Name, object? Value)[] args)
    {
        var arguments = new AIFunctionArguments();
        foreach (var (name, value) in args) arguments[name] = value;
        return (await tool.InvokeAsync(arguments))?.ToString() ?? string.Empty;
    }

    [Fact]
    public async Task RenderReference_WithReference_Compares_WithoutRenders()
    {
        var tool = MergedTools.RenderReference(Fake("render_reference"), Fake("compare_reference"));

        (await Invoke(tool, ("source", "app/index.html"))).Should().Contain("render_reference called");
        (await Invoke(tool, ("source", "app/index.html"), ("reference", "reference:site"), ("pairs", new[] { "h1" })))
            .Should().Contain("compare_reference called");
        _calls[1].Args["candidate"].Should().Be("app/index.html");
    }

    [Fact]
    public async Task WebFetch_FigmaLink_WithoutDesignSource_SaysWhy()
    {
        var tool = MergedTools.WebFetch(web: null, design: null);

        (await Invoke(tool, ("url", "https://www.figma.com/design/abc/t?node-id=1-2"))).Should().Contain("no Figma design source");
        (await Invoke(tool, ("url", "https://example.com"))).Should().Contain("no web access");
    }

    [Fact]
    public async Task ReadFile_ImageOfAnUpload_IsShown_MockAndNotesRead()
    {
        var notes = new ConversationNotesToolHost(new MemoryNotes("kept"), "s-1");
        var tool = MergedTools.ReadFile(null!, Fake("view_reference_image"), Fake("read_mock"), notes, design: true);

        (await Invoke(tool, ("path", "reference:site/shot.png"))).Should().Contain("view_reference_image called");
        (await Invoke(tool, ("path", "mock:checkout/v2"))).Should().Contain("read_mock called");
        _calls[1].Args["spec_id"].Should().Be("checkout");
        _calls[1].Args["version"].Should().Be(2);
        (await Invoke(tool, ("path", "notes:"))).Should().Contain("kept");
    }

    [Fact]
    public async Task LogDecision_Overrulable_WithoutRunId_SaysWhy_PlainLogs()
    {
        var log = new LogDecisionToolHost(new AgentSmith.Tests.Handlers.MasterHandlerFixture.NoOpDecisionLogger(), null);
        var tool = MergedTools.LogDecision(log, defaults: null);

        (await Invoke(tool, ("category", "Implementation"), ("decision", "use a queue"), ("overrulable", true)))
            .Should().StartWith("Error").And.Contain("ask_human");
        (await Invoke(tool, ("category", "Implementation"), ("decision", "use a queue"))).Should().Contain("Decision logged");
    }

    [Fact]
    public async Task Remember_Dispute_WithoutRecord_SaysWhy_WriteDelegates()
    {
        var written = new List<string>();
        var tool = MergedTools.Remember((type, name, d, b, r, ct) =>
        {
            written.Add(name);
            return Task.FromResult("stored");
        }, "cannot", disputeHost: null, "remember", withRepository: true);

        (await Invoke(tool, ("name", "old-fact"), ("dispute", "it was wrong"))).Should().Contain("Not recorded");
        (await Invoke(tool, ("name", "new-fact"), ("type", "project"), ("description", "d"), ("body", "b"))).Should().Contain("stored");
        written.Should().Equal("new-fact");
    }

    [Fact]
    public async Task FinishSpec_CarriesTheBuildNote()
    {
        var pipeline = new PipelineContext();
        var finish = new FinishSpecToolHost();
        var tool = MergedTools.FinishSpec(finish, new HandForwardToolHost(pipeline, "s1"));

        var answer = await Invoke(tool, ("status", "green"), ("summary", "done"), ("renamed", new[] { "A -> B" }));

        answer.Should().Contain("Build note for s1 recorded").And.Contain("Recorded: green");
        pipeline.Get<IReadOnlyDictionary<string, SpecBuildNote>>(ContextKeys.SpecBuildNotes)["s1"].Renamed
            .Should().Equal("A -> B");
        finish.Verification.Should().NotBeNull();
        await Invoke(tool, ("status", "green"), ("summary", "done again"));
        pipeline.Get<IReadOnlyDictionary<string, SpecBuildNote>>(ContextKeys.SpecBuildNotes)["s1"].Renamed
            .Should().Equal(["A -> B"], "a call naming no note field keeps the note");
    }

    [Fact]
    public async Task CodingRunCommand_Stage_WithoutDeclaredStages_SaysWhy()
    {
        var tool = MergedTools.CodingRunCommand(null!, null!, new PipelineContext());

        (await Invoke(tool, ("stage", "build"), ("repo", "api"))).Should().Contain("declares a verify stage");
        (await Invoke(tool)).Should().StartWith("Error");
    }

    [Fact]
    public async Task DialogRunCommand_ReferenceRepo_RunsInTheUpload_PosterWithoutRunsSaysWhy()
    {
        var tool = MergedDialogTools.RunCommand(work: null, Fake("run_in_reference"), mayStartRuns: false);

        (await Invoke(tool, ("command", "ls"), ("repo", "reference:site"))).Should().Contain("run_in_reference called");
        (await Invoke(tool, ("command", "ls"), ("repo", "api"))).Should().Contain("runs.control");
    }

    [Fact]
    public async Task DialogWriteFile_DeleteMockNotes_AndStewardRefusal()
    {
        var notes = new ConversationNotesToolHost(new MemoryNotes(null), "s-1");
        IReadOnlyList<AITool> raw = [Fake("write_file"), Fake("delete_file"), Fake("write_mock")];
        var tool = MergedDialogTools.WriteFile(raw, notes, steward: false);

        (await Invoke(tool, ("path", "draft:specs/a.yaml"), ("delete", true))).Should().Contain("delete_file called");
        (await Invoke(tool, ("path", "mock:checkout"), ("content", "<html/>"))).Should().Contain("write_mock called");
        (await Invoke(tool, ("path", "notes:"), ("content", "what is settled"))).Should().Contain("Stored");
        (await Invoke(MergedDialogTools.WriteFile(raw, notes, steward: true), ("path", "draft:specs/a.yaml"), ("content", "x")))
            .Should().Contain("edit_series");
    }

    [Fact]
    public async Task DialogEditFile_EditsTheNotes()
    {
        var store = new MemoryNotes("open: the queue");
        var tool = MergedDialogTools.EditFile([], new ConversationNotesToolHost(store, "s-1"), steward: false);

        await Invoke(tool, ("path", "notes:"), ("old_string", "open"), ("new_string", "settled"));

        store.Text.Should().Be("settled: the queue");
    }

    [Fact]
    public async Task StateWorkPicture_CarriesOpenQuestions()
    {
        var tool = MergedDialogTools.StateWorkPicture(Fake("state_work_picture"), Fake("state_open_questions"));

        var answer = await Invoke(tool, ("open_questions", new[] { new OpenQuestion("which db?", "postgres", []) }));

        answer.Should().Contain("state_open_questions called").And.NotContain("state_work_picture called");
    }

    [Fact]
    public async Task ReadTicket_PartsReachTheirReads()
    {
        var tool = MergedDialogTools.ReadTicket(Fake("read_ticket"), Fake("ticket_work"), Fake("ticket_runs"), null, Fake("verify_series"));

        (await Invoke(tool)).Should().Contain("read_ticket called");
        (await Invoke(tool, ("part", "tracker"))).Should().Contain("ticket_work called");
        (await Invoke(tool, ("part", "runs"))).Should().Contain("ticket_runs called");
        (await Invoke(tool, ("part", "series"))).Should().Contain("no series yet");
        (await Invoke(tool, ("part", "series"), ("verify", true))).Should().Contain("verify_series called");
    }

    [Fact]
    public async Task StartNextAttempt_DeriveMode_Derives_PosterWithoutRunsSaysWhy()
    {
        var tool = MergedDialogTools.StartNextAttempt(null, Fake("derive_specs"), mayStartRuns: true);

        (await Invoke(tool, ("reason", "go"), ("mode", "preview"))).Should().Contain("derive_specs called");
        _calls[0].Args["mode"].Should().Be("preview");
        (await Invoke(tool, ("reason", "go"))).Should().Contain("no attempt");
        (await Invoke(MergedDialogTools.StartNextAttempt(null, null, mayStartRuns: false), ("reason", "go")))
            .Should().Contain("runs.control");
    }

    [Fact]
    public async Task AlwaysOfferedActs_WithoutTheirPort_SayWhy()
    {
        (await Invoke(MergedDialogTools.NameConversation(null), ("subject", "x"))).Should().Contain("already has a name");
        (await Invoke(MergedDialogTools.Withdraw(null), ("key", "T-1"))).Should().Contain("filed no ticket");
        (await Invoke(MergedDialogTools.EditSeries(null, mayStartRuns: false), ("edits", Array.Empty<object>())))
            .Should().Contain("runs.control");
    }

    private sealed class MemoryNotes(string? text) : IConversationNotes
    {
        public string? Text { get; private set; } = text;

        public Task<string?> ReadAsync(string sessionId, CancellationToken cancellationToken) => Task.FromResult(Text);

        public Task WriteAsync(string sessionId, string notes, CancellationToken cancellationToken)
        {
            Text = notes;
            return Task.CompletedTask;
        }
    }
}
