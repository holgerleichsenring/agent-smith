import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { AdminHolds } from "@/types/tickets";
import { hold } from "@/components/tickets/__tests__/ticketFixtures";

// 2026-11-23-31dci: System → Needs attention — every admin hold across projects, with since, the ticket's link, the hold's
// sentence and, for an escalated wait, the value and the key it outlasted; the System nav lists it for an admin only.

const hoisted = vi.hoisted(() => ({ fetch: vi.fn(), admin: true, count: 2 as number | undefined, pathname: "/system/attention" }));
vi.mock("@/lib/holdsApi", () => ({ fetchAdminHolds: (...a: unknown[]) => hoisted.fetch(...a) }));
vi.mock("@/hooks/useHoldsAdmin", () => ({ useHoldsAdmin: () => hoisted.admin }));
vi.mock("@/hooks/useAdminHoldCount", () => ({ useAdminHoldCount: () => hoisted.count }));
vi.mock("next/navigation", () => ({ usePathname: () => hoisted.pathname }));

import { HOLDS_SETTINGS_PATH, NeedsAttentionView } from "../NeedsAttentionView";
import { NEEDS_ATTENTION_PATH, SystemSectionNav } from "../SystemSectionNav";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { silentEventStore } from "@/lib/eventStore/__tests__/fakes";

const SINCE = new Date(Date.now() - 90 * 60_000).toISOString();

const holds: AdminHolds = {
  holds: [
    {
      project: "alpha", ticketId: "41", ticketKey: "SCRUM-41", title: "Orders", since: SINCE,
      hold: hold("model_wait", { addressee: "admin", since: SINCE, escalation: { afterMinutes: 60, key: "holds.escalate_after_minutes" } }),
    },
    {
      project: "beta", ticketId: "7", ticketKey: null, title: null, since: SINCE,
      hold: hold("preflight", { act: { kind: "settings", target: "/config/agents" } }),
    },
  ],
};

beforeEach(() => {
  hoisted.fetch.mockReset();
  hoisted.admin = true;
  hoisted.count = 2;
});

describe("NeedsAttentionView", () => {
  it("NeedsAttention_ListsEveryAdminHold_WithTicketSinceSentenceAndForAnEscalationTheValueAndKey", async () => {
    hoisted.fetch.mockResolvedValue(holds);
    render(<NeedsAttentionView />);

    const sentence = await screen.findByTestId("needs-attention-sentence-alpha-41");
    expect(sentence.textContent).toMatch(
      /^Waiting for the model since .+, longer than 60 minutes \(holds\.escalate_after_minutes\)\. Reported to the admin\.$/);
    expect(screen.getByTestId("needs-attention-ticket-alpha-41")).toHaveAttribute("href", "/tickets/alpha/41");
    expect(screen.getByTestId("needs-attention-ticket-alpha-41")).toHaveTextContent("SCRUM-41 · Orders");
    expect(screen.getByTestId("needs-attention-limit-alpha-41"))
      .toHaveTextContent("Waited longer than 60 minutes, the value of holds.escalate_after_minutes.");
    expect(screen.getByTestId("needs-attention-settings-alpha-41")).toHaveAttribute("href", HOLDS_SETTINGS_PATH);
    expect(screen.getByTestId("needs-attention-row-alpha-41")).toHaveTextContent("since");
    expect(screen.getByTestId("needs-attention-settings-beta-7")).toHaveAttribute("href", "/config/agents");
    expect(screen.queryByTestId("needs-attention-limit-beta-7")).toBeNull();
  });

  it("NeedsAttention_NoAdminHold_SaysSo", async () => {
    hoisted.fetch.mockResolvedValue({ holds: [] });
    render(<NeedsAttentionView />);
    expect(await screen.findByTestId("needs-attention-empty")).toHaveTextContent("Nothing needs an admin right now.");
  });

  it("NeedsAttention_AFailedRead_SaysWhy", async () => {
    hoisted.fetch.mockRejectedValue(new Error("down"));
    render(<NeedsAttentionView />);
    await waitFor(() => expect(screen.getByTestId("needs-attention-error")).toHaveTextContent("down"));
  });
});

describe("SystemSectionNav · Needs attention", () => {
  const renderNav = () => render(<EventStoreProvider store={silentEventStore()}><SystemSectionNav /></EventStoreProvider>);

  it("SystemNav_AnAdmin_SeesNeedsAttentionWithItsCount", () => {
    renderNav();
    const item = screen.getByTestId("section-nav-item-Needs attention");
    expect(item).toHaveAttribute("href", NEEDS_ATTENTION_PATH);
    expect(item).toHaveAttribute("data-active", "true");
    expect(item).toHaveAttribute("data-hot", "true");
    expect(item).toHaveTextContent("2");
  });

  it("SystemNav_ANonAdmin_SeesNoNeedsAttention", () => {
    hoisted.admin = false;
    hoisted.count = undefined;
    renderNav();
    expect(screen.queryByTestId("section-nav-item-Needs attention")).toBeNull();
  });
});
