"use client";

import type { ReactNode } from "react";
import Link from "next/link";
import { ArrowLeft } from "lucide-react";

// 2026-11-16-0440c: the list a settings area keeps beside its pages. The sidebar carries only the
// work; configuration and the running system are reached through the profile, and each brings
// its own sections plus the way back to the work.

export function SectionNav({ label, testId, children }: { label: string; testId: string; children: ReactNode }) {
  return (
    <nav aria-label={label} data-testid={testId} className="mock-shell section-nav">
      <Link href="/" className="sn-back" data-testid={`${testId}-back`}>
        <ArrowLeft size={14} strokeWidth={2} aria-hidden="true" />
        Back to work
      </Link>
      {children}
    </nav>
  );
}

export function SectionNavGroup({ label }: { label: string }) {
  return (
    <div className="nav-h" data-testid={`section-nav-group-${label}`}>
      {label}
    </div>
  );
}

/** A section list beside the page it belongs to. */
export function WithSectionNav({ nav, children }: { nav: ReactNode; children: ReactNode }) {
  return (
    <div className="with-section-nav">
      {nav}
      <div className="section-body">{children}</div>
    </div>
  );
}
