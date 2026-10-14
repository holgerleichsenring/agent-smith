"use client";

import { useAccessToken } from "@/hooks/useAccessToken";
import { useCallerIdentity } from "@/hooks/useCallerIdentity";

// 2026-10-13-9a59r: whether the caller is KNOWN to lack a permission — the identity is present and does
// not hold it (the ClearTerminalRunsButton rule). An unknown caller is never told no: the server decides.

export function useLacksPermission(permission: string): boolean {
  const token = useAccessToken();
  const { identity } = useCallerIdentity(token !== null);
  return identity !== null && !identity.permissions.includes(permission);
}
