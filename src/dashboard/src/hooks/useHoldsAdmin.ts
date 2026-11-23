"use client";

import { useLacksPermission } from "@/hooks/useLacksPermission";

// 2026-11-23-31dcg: whether Your move adds the admin holds for this viewer — one who may save the setting an admin hold
// names (config.write). Unknown callers are not told no, as for every permission the dashboard asks; the list read
// itself is the same for every viewer, only what Your move shows of it differs.

export const ADMIN_HOLDS_PERMISSION = "config.write";

export function useHoldsAdmin(): boolean {
  return !useLacksPermission(ADMIN_HOLDS_PERMISSION);
}
