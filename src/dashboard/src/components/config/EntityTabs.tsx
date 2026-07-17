"use client";

import Link from "next/link";
import type { ConfigEntityKind } from "@/lib/configApi";
import { cn } from "@/lib/utils";
import { ENTITY_KINDS, ENTITY_LABEL } from "./entities";
import type { StudioSection } from "./ConfigStudio";

// p0345: entity tabs across the top of the studio. Every tab is a real route
// (/config/{kind}, /config/changes) so selection is URL-stable and deep-linkable,
// consistent with the rest of the dashboard's route-driven navigation.

export function EntityTabs({ section }: { section: StudioSection }) {
  return (
    <nav className="flex flex-wrap gap-1 border-b border-stone-200 pb-2" data-testid="config-tabs">
      {ENTITY_KINDS.map((kind) => (
        <Tab key={kind} href={`/config/${kind}`} label={ENTITY_LABEL[kind]} active={section === kind} testId={`config-tab-${kind}`} />
      ))}
      <Tab href="/config/changes" label="Changes" active={section === "changes"} testId="config-tab-changes" />
    </nav>
  );
}

function Tab({
  href,
  label,
  active,
  testId,
}: {
  href: string;
  label: string;
  active: boolean;
  testId: string;
}) {
  return (
    <Link
      href={href}
      data-testid={testId}
      data-active={active ? "true" : "false"}
      aria-current={active ? "page" : undefined}
      className={cn(
        "rounded-md px-3 py-1.5 dsh-body font-medium transition",
        active
          ? "bg-emerald-50 text-emerald-700"
          : "text-stone-600 hover:bg-stone-100",
      )}
    >
      {label}
    </Link>
  );
}

export type { ConfigEntityKind };
