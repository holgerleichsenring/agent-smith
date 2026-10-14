import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { ConversationEntry } from "../ConversationEntry";
import { entry } from "./detailFixtures";

// An entry's body renders in its source's format; a pull-request note the record cut says so.

afterEach(cleanup);

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
});
