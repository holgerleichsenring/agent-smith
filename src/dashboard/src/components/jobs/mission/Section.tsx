import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

// One bucket of the runs board: a slim .section-head rule with its count pill and
// hint, and the bucket's rows beneath. An empty bucket is omitted unless the
// operator asked for it, and then it says so. The action slot carries what the
// section offers for all of its runs at once (Finished: clear them).
export function Section({
  title,
  id,
  count,
  amber,
  testId,
  hint,
  action,
  alwaysShow,
  emptyLine,
  children,
}: {
  title: string;
  /** p0345b: DOM anchor for the AppRail monitor hash-links (/#needs-you …). */
  id: string;
  count: number;
  /** The mock's .cnt.amber attention pill (Needs-you > 0). */
  amber?: boolean;
  testId: string;
  hint?: string;
  /** Rendered at the end of the section head, beside the hint. */
  action?: ReactNode;
  alwaysShow?: boolean;
  emptyLine?: string;
  children: ReactNode;
}) {
  if (count === 0 && !alwaysShow) return null;
  return (
    <section id={id} data-testid={testId} className="scroll-mt-6">
      <div className="section-head">
        <h2>{title}</h2>
        <span data-testid={`${testId}-count`} className={cn("cnt", amber && "amber")}>
          {count}
        </span>
        {hint && <span className="sh-sub">{hint}</span>}
        {action && <span className="ml-auto">{action}</span>}
      </div>
      <div style={{ height: 14 }} />
      {count === 0 ? <div className="msub">{emptyLine}</div> : children}
    </section>
  );
}
