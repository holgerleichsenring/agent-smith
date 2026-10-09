import { describe, expect, it } from "vitest";
import { EventType, type TicketFetchedEvent } from "../hub-events";

// 2026-10-08-0781: the hub-event check compares names only, so the mirror's new field is pinned here.
describe("TicketFetchedEvent mirror", () => {
  it("carries the run's acts-read instant", () => {
    const event: TicketFetchedEvent = {
      runId: "run-1",
      type: EventType.TicketFetched,
      timestamp: "2026-10-08T12:00:00Z",
      ticketId: "T-1",
      title: "t",
      description: "",
      state: "Done",
      labels: [],
      attachmentCount: 0,
      source: "jira",
      actsReadAt: "2026-10-08T11:59:58Z",
    };
    expect(event.actsReadAt).toBe("2026-10-08T11:59:58Z");
  });
});
