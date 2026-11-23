"use client";

import { useTicketList } from "@/lib/tickets/TicketListProvider";
import { useHoldsAdmin } from "@/hooks/useHoldsAdmin";

// 2026-11-23-31dci: how many admin holds System → Needs attention lists, for an admin only — the ticket list's own count
// (counts.adminHolds, read over the same projects), so the sidebar and the page cannot disagree. Undefined for a viewer
// known not to be an admin, and while the list is unread: never a made-up 0.
export function useAdminHoldCount(): number | undefined {
  const admin = useHoldsAdmin();
  const counts = useTicketList().list?.counts;
  return admin && counts ? counts.adminHolds ?? 0 : undefined;
}
