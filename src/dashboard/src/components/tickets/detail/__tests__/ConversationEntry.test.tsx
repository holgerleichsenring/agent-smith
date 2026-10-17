import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ConversationEntry } from "../ConversationEntry";
import { entry } from "./detailFixtures";

// An entry's body renders in its source's format; a pull-request note the record cut says so.

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

/** jsdom draws nothing: a clamped box reports `scroll` tall content while closed, its own height once open. */
function drawn(scroll: number, client = 117) {
  vi.spyOn(HTMLElement.prototype, "scrollHeight", "get").mockImplementation(function (this: HTMLElement) {
    return this.classList.contains("closed") ? scroll : client;
  });
  vi.spyOn(HTMLElement.prototype, "clientHeight", "get").mockReturnValue(client);
}

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

  it("ConversationEntry_Overflowing_ShowsShowMore_AndOpensAndClosesTheClamp", () => {
    drawn(400);
    show({ source: "ticket", kind: "comment", role: "agent", text: "Preflight failed:\n" + "a check\n".repeat(30) });

    const clamp = screen.getByTestId("conv-clamp");
    expect(clamp).toHaveClass("m-clamp", "closed", "over");
    const more = screen.getByTestId("conv-more");
    expect(more).toHaveTextContent("Show more");
    expect(more).toHaveAttribute("aria-expanded", "false");

    fireEvent.click(more);
    expect(clamp).not.toHaveClass("closed");
    expect(screen.getByTestId("conv-more")).toHaveTextContent("Show less");
    fireEvent.click(screen.getByTestId("conv-more"));
    expect(clamp).toHaveClass("closed");
  });

  it("ConversationEntry_Fitting_ShowsNoToggle_AndNoFade", () => {
    drawn(60, 60);
    show({ text: "Keep the outbox?" });

    expect(screen.queryByTestId("conv-more")).toBeNull();
    expect(screen.getByTestId("conv-clamp")).not.toHaveClass("over");
  });

  it("ConversationEntry_Opened_KeepsShowLess_ThoughTheOpenBoxNoLongerOverflows", () => {
    drawn(400);
    show({ text: "long" });

    fireEvent.click(screen.getByTestId("conv-more"));
    expect(screen.getByTestId("conv-more")).toHaveTextContent("Show less");
  });

  it("ConversationEntry_CutReviewFindings_AreInsideTheClamp", () => {
    drawn(400);
    show({ source: "spec-cut", kind: "cut-review", role: "system", text: "The cut leaves a criterion uncovered.",
      detail: { findings: [{ specId: "s1", criterion: "Builds", problem: "No step builds the worker.", cites: ["portal-worker/Program.cs:12"] }] } });

    const clamp = screen.getByTestId("conv-clamp");
    expect(clamp.querySelector("[data-testid=conv-detail]")).toHaveTextContent("No step builds the worker.");
    expect(clamp).toHaveTextContent("The cut leaves a criterion uncovered.");
  });
});
