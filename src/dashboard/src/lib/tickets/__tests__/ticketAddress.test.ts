import { describe, expect, it } from "vitest";
import { isTicketPage, ticketPath } from "../ticketAddress";

// 2026-11-17-8d1ca: one ticket's page, not the list, collapses the app sidebar to icons.

describe("ticketAddress", () => {
  it("isTicketPage_OneTicketsPage_NotTheList", () => {
    expect(isTicketPage(ticketPath("shop", "101"))).toBe(true);
    expect(isTicketPage(ticketPath("shop", "a/b"))).toBe(true);
    expect(isTicketPage("/tickets")).toBe(false);
    expect(isTicketPage("/tickets/shop")).toBe(false);
    expect(isTicketPage("/jobs/r-1")).toBe(false);
  });
});
