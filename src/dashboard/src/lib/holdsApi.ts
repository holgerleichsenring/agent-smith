// 2026-11-23-31dci: System → Needs attention's read — GET /api/holds/admin, every admin hold across the projects the
// caller reads; config.write, the admin's permission.

import { getJson } from "@/lib/apiResponse";
import type { AdminHolds } from "@/types/tickets";

export const ADMIN_HOLDS_PATH = "/api/holds/admin";

export function fetchAdminHolds(signal?: AbortSignal): Promise<AdminHolds> {
  return getJson<AdminHolds>(ADMIN_HOLDS_PATH, signal);
}
