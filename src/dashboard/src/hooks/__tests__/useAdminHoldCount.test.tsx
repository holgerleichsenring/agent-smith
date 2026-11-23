import { renderHook } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { TicketListView } from "@/types/tickets";
import { list } from "@/components/tickets/__tests__/ticketFixtures";

// 2026-11-23-31dci: the admin-hold count is the ticket list's own, for an admin only, and unknown while the list is unread.

const hoisted = vi.hoisted(() => ({ admin: true, tickets: null as TicketListView | null }));
vi.mock("@/hooks/useHoldsAdmin", () => ({ useHoldsAdmin: () => hoisted.admin }));
vi.mock("@/lib/tickets/TicketListProvider", () => ({ useTicketList: () => ({ list: hoisted.tickets }) }));

import { useAdminHoldCount } from "../useAdminHoldCount";

describe("useAdminHoldCount", () => {
  it("AdminHoldCount_AnAdminReadsTheListsCount_ANonAdminAndAnUnreadListNone", () => {
    hoisted.tickets = list([], { adminHolds: 3 });
    hoisted.admin = true;
    expect(renderHook(() => useAdminHoldCount()).result.current).toBe(3);
    hoisted.admin = false;
    expect(renderHook(() => useAdminHoldCount()).result.current).toBeUndefined();
    hoisted.admin = true;
    hoisted.tickets = null;
    expect(renderHook(() => useAdminHoldCount()).result.current).toBeUndefined();
  });
});
