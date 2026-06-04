import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

// p0220: the section-label (eyebrow) token. One uppercase, tracked, muted label
// rendered identically across every route's section headers and rails.

export function SectionLabel({
  children,
  className,
  testId,
}: {
  children: ReactNode;
  className?: string;
  testId?: string;
}) {
  return (
    <div data-testid={testId} className={cn("eyebrow-uppercase text-stone-400", className)}>
      {children}
    </div>
  );
}
