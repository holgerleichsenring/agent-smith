import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { CommandTimeline } from "@/components/execution/bodies/CommandTimeline";
import type { SandboxCommandEntry } from "@/hooks/execution-tree/buckets";
import type { PairedLlmCall } from "@/hooks/execution-tree/llmPairing";

function llm(over: Partial<PairedLlmCall>): PairedLlmCall {
  return {
    id: "x", role: "agentic-executor", roleIsUnknown: false, model: "gpt-4.1",
    phase: null, startedAt: "2026-06-05T06:08:00.500Z", finishedAt: "2026-06-05T06:08:01.500Z",
    durationMs: 1000, tokensIn: 9016, tokensOut: 35, costUsd: 0.0183, cacheHit: false, ...over,
  };
}

function cmd(over: Partial<SandboxCommandEntry>): SandboxCommandEntry {
  return {
    repo: "sample-service",
    verb: "ReadFile",
    summary: "Controllers/FooController.cs",
    exitCode: 0,
    durationMs: 12,
    timestamp: "2026-06-05T06:08:00.000Z",
    ...over,
  };
}

describe("CommandTimeline (p0228)", () => {
  it("CommandTimeline_RendersActionsInOrder_WithTargets", () => {
    render(
      <CommandTimeline
        commands={[
          cmd({ verb: "ReadFile", summary: "FooController.cs" }),
          cmd({ verb: "Grep", summary: '"ResponseType"' }),
          cmd({ verb: "ListFiles", summary: "Controllers/" }),
        ]}
      />,
    );
    expect(screen.getByText("FooController.cs")).toBeInTheDocument();
    expect(screen.getByText('"ResponseType"')).toBeInTheDocument();
    // the action verbs are surfaced so "what did the LLM do" is answerable
    expect(screen.getByText("Grep")).toBeInTheDocument();
  });

  it("CommandTimeline_SurfacesWriteCount_SoZeroEditsIsObvious", () => {
    // The 0-changes case: lots of reads, never a write. The summary must make
    // "0 writes" visible at a glance.
    render(
      <CommandTimeline
        commands={[cmd({ verb: "ReadFile" }), cmd({ verb: "Grep" }), cmd({ verb: "ReadFile" })]}
      />,
    );
    expect(screen.getByTestId("command-timeline-summary")).toHaveTextContent("3 actions");
    expect(screen.getByTestId("command-timeline-summary")).toHaveTextContent("0 writes");
  });

  it("CommandTimeline_HighlightsWriteFile_AsAnEdit", () => {
    render(<CommandTimeline commands={[cmd({ verb: "WriteFile", summary: "Foo.cs" })]} />);
    const row = screen.getByTestId("command-row-WriteFile");
    expect(row).toHaveAttribute("data-write", "true");
    expect(screen.getByTestId("command-timeline-summary")).toHaveTextContent("1 write");
  });

  it("CommandTimeline_CountsFlaggedShellEdits_AsWrites", () => {
    // p0357: a RunCommand the backend classified as mutating (perl -i, cat > f,
    // git apply) counts as a write — script edits no longer read as plain actions.
    render(
      <CommandTimeline
        commands={[
          cmd({ verb: "/bin/sh", summary: "perl -pi -e 's/a/b/' Foo.cs", isWrite: true }),
          cmd({ verb: "/bin/sh", summary: "grep -rn foo .", isWrite: false }),
          cmd({ verb: "ReadFile" }),
        ]}
      />,
    );
    expect(screen.getByTestId("command-timeline-summary")).toHaveTextContent("3 actions");
    expect(screen.getByTestId("command-timeline-summary")).toHaveTextContent("1 write");
  });

  it("CommandTimeline_LongVerb_TruncatesInsteadOfOverlappingTheSummary", () => {
    // Regression: a 13-char verb (DirectoryTree) overflowed the fixed w-24
    // verb box and collided with the path summary ("DirectoryTreeSample.Tests…").
    // The column must clip its own text, never bleed into the next column.
    render(
      <CommandTimeline
        commands={[cmd({ verb: "DirectoryTree", summary: "Sample.Tests/External/SampleController" })]}
      />,
    );
    const verb = screen.getByText("DirectoryTree");
    expect(verb).toHaveClass("flex-none", "truncate");
    expect(verb).toHaveAttribute("title", "DirectoryTree");
  });

  it("CommandTimeline_CapsLongLists_WithShowAll", () => {
    const many = Array.from({ length: 30 }, (_, i) =>
      cmd({ summary: `file-${i}.cs`, timestamp: `2026-06-05T06:08:${String(i).padStart(2, "0")}.000Z` }),
    );
    render(<CommandTimeline commands={many} defaultCap={12} />);
    // p0232: newest-first — the first 12 shown are file-29..file-18, so an OLD
    // entry (file-3) is hidden until show-all.
    expect(screen.queryByText("file-3.cs")).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId("command-timeline-show-all"));
    expect(screen.getByText("file-3.cs")).toBeInTheDocument();
  });

  it("CommandTimeline_RunningCommand_ShowsRunning", () => {
    render(<CommandTimeline commands={[cmd({ exitCode: null, durationMs: null })]} />);
    expect(screen.getByText("running…")).toBeInTheDocument();
  });

  it("CommandTimeline_MultiRepo_StripsCommonPrefix_SoRepoIsDistinguishable", () => {
    // p0229: repos share a long common prefix; left-truncation hid exactly the
    // distinguishing suffix ("…-server" vs "…-client"). Strip the prefix.
    render(
      <CommandTimeline
        commands={[
          cmd({ repo: "acme-platform-server", verb: "ReadFile", summary: "A.cs" }),
          cmd({ repo: "acme-platform-client", verb: "ReadFile", summary: "B.ts" }),
        ]}
      />,
    );
    expect(screen.getByText("server")).toBeInTheDocument();
    expect(screen.getByText("client")).toBeInTheDocument();
    expect(screen.queryByText("acme-platform-server")).not.toBeInTheDocument();
    // full name still available on hover
    expect(screen.getByTitle("acme-platform-server")).toBeInTheDocument();
  });

  it("CommandTimeline_SingleRepo_ShowsFullName", () => {
    render(<CommandTimeline commands={[cmd({ repo: "only-repo", summary: "X.cs" })]} />);
    expect(screen.getByText("only-repo")).toBeInTheDocument();
  });

  it("CommandTimeline_MergesLlmCalls_AndCommands_IntoOneList_NewestFirst", () => {
    // p0231: the LLM turn and the commands it issued are ONE correlatable list.
    // p0232: ordered newest-first — the command (06:08:01.000) sits above its
    // call (06:08:00.500) so the latest activity is visible without scrolling.
    render(
      <CommandTimeline
        commands={[cmd({ verb: "ReadFile", summary: "Foo.cs", timestamp: "2026-06-05T06:08:01.000Z" })]}
        llmCalls={[llm({ id: "a", startedAt: "2026-06-05T06:08:00.500Z" })]}
      />,
    );
    const list = screen.getByTestId("command-timeline-list");
    const rows = list.querySelectorAll("[data-testid='timeline-llm-row'],[data-testid='command-row-ReadFile']");
    expect(rows).toHaveLength(2);
    expect(rows[0].getAttribute("data-testid")).toBe("command-row-ReadFile"); // newest first
    expect(rows[1].getAttribute("data-testid")).toBe("timeline-llm-row");
    // summary surfaces the llm count + cost
    expect(screen.getByTestId("command-timeline-summary")).toHaveTextContent("1 llm");
    expect(screen.getByTestId("command-timeline-summary")).toHaveTextContent("$0.0183");
  });

  it("CommandTimeline_LlmOnlyStep_StillRenders", () => {
    render(<CommandTimeline commands={[]} llmCalls={[llm({ id: "a" })]} />);
    expect(screen.getByTestId("timeline-llm-row")).toBeInTheDocument();
    expect(screen.getByText("agentic-executor")).toBeInTheDocument();
  });
});
