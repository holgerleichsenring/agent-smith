"use client";

import { useAccessToken } from "@/hooks/useAccessToken";
import { useCallerIdentity } from "@/hooks/useCallerIdentity";
import { holds } from "@/lib/projectPermissions";

// 2026-10-13-9a59r: whether the caller is KNOWN to lack a permission — the identity is present and does not hold
// it. An unknown caller is never told no: the server decides. 2026-10-15-4a4de: an act passes the project its route
// names, where a binding's grant counts too; without one the global rule holds.
export function useLacksPermission(permission: string, project?: string): boolean {
  const token = useAccessToken();
  const { identity } = useCallerIdentity(token !== null);
  return identity !== null && !holds(identity, permission, project);
}
