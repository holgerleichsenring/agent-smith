import { readFileSync } from "node:fs";
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { translator } from "@/lib/i18n/translate";
import { TicketActivity } from "../TicketActivity";
import { TicketStrip, cellTitle, deriveTitle } from "../TicketStrip";
import { ticket } from "./ticketFixtures";

// 2026-11-22-50d7a: the strip of the newest attempt says what it is, each cell its state in words; a ticket
// comment on the ticket page takes the column's width.

const en = translator("en");

describe("ticket legibility", () => {
  it("TicketStrip_Group_SaysWhatItShows", () => {
    render(<TicketStrip strip={{ derive: "cut", cells: [{ specId: "s-1a", state: "carried" }] }} pullRequests={[]} />);
    const group = screen.getByRole("group", { name: /^Progress: the spec cut/ });
    expect(group.querySelector("i.d")?.getAttribute("title")).toBe("Spec cut · cut");
    expect(group.querySelector("[data-cell]")?.getAttribute("title")).toBe("s-1a · carried from an earlier attempt");
  });

  it("StripTitles_UnknownState_FallBackToTheStateItself", () => {
    expect(deriveTitle(en, "re_cutting")).toBe("Spec cut · re cutting");
    expect(cellTitle(translator("de"), "s-1b", "done")).toBe("s-1b · fertig");
  });

  it("TicketActivity_RetryWithoutAName_SaysSomeoneNotAnId", () => {
    render(<TicketActivity ticket={ticket({ bucket: "working", status: "running", cause: { channel: "retry", actor: null } })} />);
    expect(screen.getByTestId("ticket-activity")).toHaveTextContent("restarted by someone");
  });

  it("ConversationEntryBody_HasNoReadingWidthCap", () => {
    const css = readFileSync("src/styles/ticket-thread.css", "utf8");
    const body = css.split("\n").find((line) => line.startsWith(".mock-tickets .tk-entry .mb {"));
    expect(body).toBeDefined();
    expect(body).not.toContain("max-width");
    expect(css).toMatch(/\.mock-tickets \.tk-entry \.mb pre \{[^}]*white-space: pre-wrap/);
  });
});
