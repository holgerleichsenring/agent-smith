"use client";

import { useT } from "@/hooks/useT";
import { POC_STATES } from "./pocStates";

/** 2026-11-23-933da: a PoC state's word in the language in force; an unknown state reads as itself. */
export function PocStateLabel({ state }: { state: string }) {
  const t = useT();
  const look = POC_STATES[state];
  return <>{look ? t(look.label) : state}</>;
}
