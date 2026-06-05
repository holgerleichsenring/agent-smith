import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { CommandTimeline } from "@/components/execution/bodies/CommandTimeline";
import type { SandboxCommandEntry } from "@/hooks/execution-tree/buckets";

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

  it("CommandTimeline_CapsLongLists_WithShowAll", () => {
    const many = Array.from({ length: 30 }, (_, i) =>
      cmd({ summary: `file-${i}.cs`, timestamp: `2026-06-05T06:08:${String(i).padStart(2, "0")}.000Z` }),
    );
    render(<CommandTimeline commands={many} defaultCap={12} />);
    expect(screen.queryByText("file-20.cs")).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId("command-timeline-show-all"));
    expect(screen.getByText("file-20.cs")).toBeInTheDocument();
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
});
