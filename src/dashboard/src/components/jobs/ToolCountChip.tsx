"use client";

import { useT } from "@/hooks/useT";

interface Props {
  count: number;
}

export function ToolCountChip({ count }: Props) {
  const t = useT();
  if (count === 0) return null;
  return (
    <span
      className="rounded-full bg-stone-100 px-2 py-0.5 text-xs text-stone-600"
      data-testid="tool-count-chip"
    >
      {t("runs.tools.count", { count })}
    </span>
  );
}
