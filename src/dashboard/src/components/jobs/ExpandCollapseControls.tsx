"use client";

import { useT } from "@/hooks/useT";

interface Props {
  repoNames: readonly string[];
  expanded: ReadonlySet<string>;
  onExpandAll: () => void;
  onCollapseAll: () => void;
}

export function ExpandCollapseControls({ repoNames, expanded, onExpandAll, onCollapseAll }: Props) {
  const t = useT();
  if (repoNames.length === 0) return null;
  const allExpanded = repoNames.every((r) => expanded.has(r));
  const noneExpanded = expanded.size === 0;
  return (
    <div className="flex items-center gap-3 text-xs" data-testid="expand-collapse-controls">
      <button
        type="button"
        onClick={onExpandAll}
        disabled={allExpanded}
        className="text-stone-600 underline-offset-2 hover:underline disabled:cursor-not-allowed disabled:text-stone-300 disabled:no-underline"
        data-testid="expand-all"
      >
        {t("runs.list.expandAll")}
      </button>
      <span className="text-stone-300">·</span>
      <button
        type="button"
        onClick={onCollapseAll}
        disabled={noneExpanded}
        className="text-stone-600 underline-offset-2 hover:underline disabled:cursor-not-allowed disabled:text-stone-300 disabled:no-underline"
        data-testid="collapse-all"
      >
        {t("runs.list.collapseAll")}
      </button>
    </div>
  );
}
