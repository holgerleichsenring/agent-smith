import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ConversationEntry } from "../ConversationEntry";
import { entry } from "./detailFixtures";

// An entry's body renders in its source's format; a pull-request note the record cut says so.

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

function show(over: Parameters<typeof entry>[0]) {
  render(<ConversationEntry entry={entry(over)} attemptNumber={() => 1} started={null} />);
}

describe("ConversationEntry", () => {
  it("ConversationEntry_TruncatedPrBody_EndsWithTheCutNote", () => {
    show({ source: "pr-review", kind: "comment", role: "reviewer", format: "markdown", text: "**Rename** this.",
      detail: { truncated: true } });

    const body = screen.getByTestId("conv-entry").querySelector(".mb")!;
    expect(body.textContent?.endsWith("… (cut at 4000 characters)")).toBe(true);
    expect(body.querySelector("strong")?.textContent).toBe("Rename");
  });

  it("ConversationEntry_UncutPrBody_HasNoCutNote", () => {
    show({ source: "pr-review", kind: "comment", role: "reviewer", format: "markdown", text: "Fine.", detail: { truncated: false } });

    expect(screen.queryByTestId("conv-cut")).toBeNull();
  });

  it("ConversationEntry_HtmlTicketComment_ShowsNoTagSource", () => {
    show({ source: "ticket", kind: "comment", role: "agent", isOurs: true, format: "html",
      text: '<h2 id="x">Agent Smith - Completed across 2 repo(s)</h2><ul><li><a href="https://example.com/pr/1">api#1</a></li></ul>' });

    const body = screen.getByTestId("conv-entry").querySelector(".mb")!;
    expect(screen.getByRole("heading", { level: 2 }).textContent).toBe("Agent Smith - Completed across 2 repo(s)");
    expect(body.textContent).not.toContain("<h2");
  });

  // 2026-11-17-8d1cb: a conversation shows long texts in full — one line of What happened, no clamp and no toggle.
  it("ConversationEntry_LongText_IsShownInFull_WithNoToggle", () => {
    const text = "Preflight failed:\n" + "a check\n".repeat(30) + "the last line";
    show({ source: "ticket", kind: "comment", role: "agent", text });

    expect(screen.queryByTestId("conv-more")).toBeNull();
    expect(screen.queryByTestId("conv-clamp")).toBeNull();
    expect(screen.getByTestId("conv-entry")).toHaveTextContent("the last line");
  });

  it("ConversationEntry_IsOneLine_TimeWhoWhereAndText_APersonInTheBrightColour", () => {
    show({ source: "conversation", kind: "turn", role: "person", author: "Ada", text: "Move the check into a helper." });

    const line = screen.getByTestId("conv-entry");
    expect(line).toHaveClass("tk-line", "you");
    expect(line.querySelector(".tk-who")).toHaveTextContent("Ada");
    expect(line.querySelector(".tk-text")).toHaveTextContent("ticket conversationMove the check into a helper.");
  });

  it("ConversationEntry_CutReviewFindings_ShowUnderTheText", () => {
    show({ source: "spec-cut", kind: "cut-review", role: "system", text: "The cut leaves a criterion uncovered.",
      detail: { findings: [{ specId: "s1", criterion: "Builds", problem: "No step builds the worker.", cites: ["portal-worker/Program.cs:12"] }] } });

    expect(screen.getByTestId("conv-detail")).toHaveTextContent("No step builds the worker.");
    expect(screen.getByTestId("conv-entry")).toHaveTextContent("The cut leaves a criterion uncovered.");
  });

  // 2026-11-17-dd05a: a posted cut comment arrives as one line; it is drawn as plain text, nothing around it.
  it("ConversationEntry_SpecSetLine_IsItsOneLine", () => {
    show({ source: "ticket", kind: "spec-set", role: "agent", isOurs: true, format: "text", text: "2 specs derived, revision 5" });

    const body = screen.getByTestId("conv-entry").querySelector(".mb")!;
    expect(body.textContent).toBe("2 specs derived, revision 5");
    expect(screen.queryByTestId("conv-detail")).toBeNull();
  });
});
