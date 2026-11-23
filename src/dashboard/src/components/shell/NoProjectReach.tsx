"use client";

import type { ReactNode } from "react";
import { useProjectReach } from "@/hooks/useProjectReach";
import { useT } from "@/hooks/useT";

// 2026-10-15-4a4de: an empty list's own words, unless this sign-in reaches no project — then the emptiness is a fact
// about the caller, not the installation, and the page says so. Rendered only inside an empty branch, so a list that
// has rows never asks.

export function NoProjectReach({ children }: { children: ReactNode }) {
  const reach = useProjectReach();
  const t = useT();
  if (reach !== "none") return <>{children}</>;
  return (
    <div className="msub" data-testid="no-project-reach">
      {t("shell.noProjectReach")}
    </div>
  );
}
