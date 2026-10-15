import { act, fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { TicketServerBucket } from "@/types/tickets";
import { pr, ticket } from "../../__tests__/ticketFixtures";

// 2026-10-15-114fd: the Close dialog — hide only by default, both host options on request with one line per outcome,
// the server's refusal as is — and where Close is offered at all.

const hoisted = vi.hoisted(() => ({ apiFetch: vi.fn(), lacks: false }));
vi.mock("@/hooks/useLacksPermission", () => ({ useLacksPermission: () => hoisted.lacks }));
vi.mock("@/lib/apiResponse", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/apiResponse")>()),
  apiFetch: (...args: unknown[]) => hoisted.apiFetch(...args),
}));

const { TicketCloseDialog } = await import("../TicketCloseDialog");
const { TicketCloseAct } = await import("../TicketCloseAct");

function reply(status: number, body: string): Response {
  return { ok: status >= 200 && status < 300, status, text: async () => body, json: async () => JSON.parse(body) } as unknown as Response;
}

const mark = { tracker: "gh", ticketKey: "github-7", ticketId: "7", project: "shop", closedAt: "2026-10-15T10:00:00Z", by: "Ada", note: null, closedPullRequests: [], trackerClosed: false };
const onClose = vi.fn();
const onClosed = vi.fn();

function dialog(openPullRequests: number) {
  render(<TicketCloseDialog project="shop" ticketId="7" label="#7" tracker="GitHub" openPullRequests={openPullRequests} onClose={onClose} onClosed={onClosed} />);
}

const sent = () => JSON.parse(hoisted.apiFetch.mock.calls[0][1].body);

beforeEach(() => {
  hoisted.apiFetch.mockReset();
  hoisted.lacks = false;
  onClose.mockReset();
  onClosed.mockReset();
});

describe("TicketCloseDialog", () => {
  it("TicketCloseDialog_DefaultSend_PostsHideOnly", async () => {
    hoisted.apiFetch.mockResolvedValue(reply(200, JSON.stringify({ mark, pullRequests: [], tracker: null })));
    dialog(2);

    expect(screen.getByTestId("ticket-close-prs")).not.toBeChecked();
    expect(screen.getByTestId("ticket-close-tracker")).not.toBeChecked();
    await act(async () => fireEvent.click(screen.getByTestId("ticket-close-send")));

    expect(String(hoisted.apiFetch.mock.calls[0][0])).toBe("/api/tickets/shop/7/close");
    expect(sent()).toEqual({ note: null, closePullRequests: false, closeOnTracker: false });
    expect(onClosed).toHaveBeenCalledTimes(1);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("TicketCloseDialog_BothOptionsChecked_PostsBothAndShowsEachOutcome", async () => {
    hoisted.apiFetch.mockResolvedValue(reply(200, JSON.stringify({
      mark,
      pullRequests: [
        { url: "https://example.test/pr/71", closed: true, reason: null },
        { url: "https://example.test/pr/72", closed: true, reason: "already-closed" },
        { url: "https://example.test/pr/73", closed: false, reason: "refused" },
      ],
      tracker: { closed: true, reason: null },
    })));
    dialog(2);

    fireEvent.change(screen.getByTestId("ticket-close-note"), { target: { value: "  superseded  " } });
    fireEvent.click(screen.getByTestId("ticket-close-prs"));
    fireEvent.click(screen.getByTestId("ticket-close-tracker"));
    await act(async () => fireEvent.click(screen.getByTestId("ticket-close-send")));

    expect(sent()).toEqual({ note: "superseded", closePullRequests: true, closeOnTracker: true });
    expect(onClosed).toHaveBeenCalledTimes(1);
    const lines = [...screen.getByTestId("ticket-close-outcomes").children].map((l) => [l.className, l.textContent]);
    expect(lines).toEqual([
      ["dbh-out closed", "PR #71 — closed"],
      ["dbh-out closed", "PR #72 — already closed"],
      ["dbh-out kept", "PR #73 — not closed: refused"],
      ["dbh-out closed", "GitHub ticket — closed"],
    ]);
    expect(onClose).not.toHaveBeenCalled();
    fireEvent.click(screen.getByTestId("ticket-close-done"));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it("TicketCloseDialog_409_ShowsTheServersSentence", async () => {
    const sentence = "7 already counts as landed (merged on 2026-10-13 10:00:00Z); a landed ticket is done, not closed.";
    hoisted.apiFetch.mockResolvedValue(reply(409, sentence));
    dialog(0);

    await act(async () => fireEvent.click(screen.getByTestId("ticket-close-send")));

    expect(screen.getByTestId("ticket-close-said")).toHaveTextContent(sentence);
    expect(onClosed).not.toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
  });

  it("TicketCloseDialog_NoOpenPullRequest_DoesNotOfferThePullRequestOption", () => {
    dialog(0);
    expect(screen.queryByTestId("ticket-close-prs")).toBeNull();
    expect(screen.getByTestId("ticket-close-dialog")).toHaveTextContent("Also close #7 on GitHub");
  });
});

describe("TicketCloseAct", () => {
  const act7 = (bucket: TicketServerBucket) => render(
    <TicketCloseAct ticket={ticket({ ticketId: "7", bucket, pullRequests: [pr(), pr({ url: "u2", state: "closed" })] })} onClosed={onClosed} small />);

  it("TicketCloseAct_LackingRunsControlDoneLiveOrClosed_IsHidden", () => {
    for (const bucket of ["done", "working", "needs_you", "closed"] as const) {
      const view = act7(bucket);
      expect(screen.queryByTestId("ticket-close-7"), bucket).toBeNull();
      view.unmount();
    }
    hoisted.lacks = true;
    const lacking = act7("failed");
    expect(screen.queryByTestId("ticket-close-7")).toBeNull();
    lacking.unmount();

    hoisted.lacks = false;
    act7("failed");
    fireEvent.click(screen.getByTestId("ticket-close-7"));
    expect(screen.getByTestId("ticket-close-dialog")).toHaveTextContent("Also close its open pull requests (1)");
  });
});
