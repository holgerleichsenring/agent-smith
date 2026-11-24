"use client";

import { useAccessToken } from "@/hooks/useAccessToken";
import { useCallerIdentity } from "@/hooks/useCallerIdentity";
import type { CallerIdentity } from "@/lib/identityApi";

// 2026-11-23-933da: the caller's identity when it is KNOWN — read only while a token is held, as useLacksPermission
// reads it. Null means unknown, and an unknown caller is never told no.
export function useKnownIdentity(): CallerIdentity | null {
  const token = useAccessToken();
  return useCallerIdentity(token !== null).identity;
}
