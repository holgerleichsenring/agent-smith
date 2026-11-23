"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useKnownIdentity } from "@/hooks/useKnownIdentity";
import { isPocOnly } from "@/lib/pocAccess";

// 2026-11-23-933da: home is for new work, which a PoC-only caller cannot start, so / sends them to their PoCs. Only a
// known identity is redirected; an unknown caller stays on home as before.

export const POCS_HOME = "/pocs";

export function PocOnlyRedirect() {
  const router = useRouter();
  const identity = useKnownIdentity();
  const pocOnly = isPocOnly(identity);
  useEffect(() => {
    if (pocOnly) router.replace(POCS_HOME);
  }, [pocOnly, router]);
  return null;
}
