import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { TicketActivity } from "../TicketActivity";
import { pr, ticket } from "./ticketFixtures";

// 2026-10-16-7ed3a: a failed head with open pull requests reads stuck or failed; both activity lines list them.

afterEach(cleanup);

describe("TicketActivity", () => {
  it("TicketActivity_StuckWithPullRequests_ListsThem", () => {
    render(<TicketActivity ticket={ticket({ bucket: "stuck", status: "failed", attempts: [{ runId: "r-101", status: "failed", endedOnInfrastructure: false, handedBack: false }], pullRequests: [pr({ number: "809", state: "open" })] })} />);

    expect(screen.getByTestId("ticket-activity")).toHaveTextContent("1 attempt failed");
    expect(screen.getByTestId("ticket-activity")).toHaveTextContent("PR #809 open");
  });

  it("TicketActivity_FailedWithPullRequests_ListsThem", () => {
    render(<TicketActivity ticket={ticket({ bucket: "failed", status: "failed", lastOutcome: "failed", pullRequests: [pr({ number: "809", state: "open" })] })} />);

    expect(screen.getByTestId("ticket-activity")).toHaveTextContent("PR #809 open");
  });
});
