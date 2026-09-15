"use client";

import type { TemplateReference } from "@/lib/configApi";
import { TemplateEditor } from "./TemplateEditor";
import type { useProjectContexts } from "./useProjectContexts";
import type { ConfigCatalog } from "./useConfigCatalog";

// 2026-09-15-a2d0: one declared template as a ROW that carries all five of its values,
// with the field set of 2026-09-14-620e opening underneath it. A list that hid a value
// would be the length complaint one click deeper, so nothing here is ellipsised: a
// truncated sha is a wrong sha, not a shorter one, and the row wraps instead.
//
// What the row can judge without an outbound call, it says — an unfinished binding is
// marked, because the server refuses it at save time. Whether the TARGET is reachable is
// not claimed: that answer comes from the open row's own read, and probing every target
// on drawer open is the traffic this list exists to remove.

const MISSING = "—";

export function TemplateRow({
  template,
  index,
  open,
  localContexts,
  catalog,
  onToggle,
  onChange,
  onRemove,
  testId,
}: {
  template: TemplateReference;
  index: number;
  open: boolean;
  localContexts: ReturnType<typeof useProjectContexts>;
  catalog: ConfigCatalog;
  onToggle: () => void;
  onChange: (next: TemplateReference) => void;
  onRemove: () => void;
  testId: string;
}) {
  const context = template.context || MISSING;
  return (
    <div className="tpl-row" role="listitem" data-open={open ? "true" : "false"} data-testid={`${testId}-row`}>
      <div className="tpl-sum">
        <button
          type="button"
          className="tpl-open"
          aria-expanded={open}
          aria-controls={`${testId}-editor`}
          data-testid={`${testId}-open`}
          onClick={onToggle}
        >
          <span className="chev" aria-hidden="true">{open ? "▾" : "▸"}</span>
          <span className="tpl-ctx">{context}</span>
          {isUnfinished(template) && (
            <span className="tpl-todo" data-testid={`${testId}-unfinished`}>
              not finished
            </span>
          )}
        </button>
        <button
          type="button"
          className="pick"
          aria-label={`Remove template ${index + 1}`}
          data-testid={`${testId}-remove`}
          onClick={onRemove}
        >
          ×
        </button>
      </div>
      <div className="tpl-target" data-testid={`${testId}-target`}>
        built after {template.project || MISSING} / {template.repo || MISSING} ·{" "}
        {template.templateContext || MISSING}{" "}
        {/* An absent revision lands on the TARGET repository's own default branch: the
            clone carries no -b and the revision is only landed on when one is declared,
            so the catalog repo entry's branch never reaches this path and is not shown. */}
        <span className="tpl-rev">
          {template.revision ? `@ ${template.revision}` : "@ its default branch"}
        </span>
      </div>
      {open && (
        <TemplateEditor
          template={template}
          localContexts={localContexts}
          catalog={catalog}
          onChange={onChange}
          testId={testId}
        />
      )}
    </div>
  );
}

/** What the server refuses at save time, minus the revision, which is optional. */
const isUnfinished = (t: TemplateReference): boolean =>
  !t.context || !t.project || !t.repo || !t.templateContext;
