"use client";

import { useMemo } from "react";
import { Tree } from "react-arborist";
import type { TrailNode } from "@/types/trail-node";
import { TrailNodeRow } from "./TrailNodeRow";

interface Props {
  root: TrailNode;
  selectedId: string | null;
  onSelect: (node: TrailNode | null) => void;
}

export function TrailTree({ root, selectedId, onSelect }: Props) {
  const data = useMemo(() => [root], [root]);
  return (
    <div className="rounded-lg border border-stone-200 bg-white" data-testid="trail-tree">
      <Tree<TrailNode>
        data={data}
        idAccessor={(n) => n.id}
        childrenAccessor={(n) => n.children}
        openByDefault
        rowHeight={32}
        width="100%"
        height={500}
        selection={selectedId ?? undefined}
        onSelect={(nodes) => onSelect(nodes[0]?.data ?? null)}
      >
        {TrailNodeRow}
      </Tree>
    </div>
  );
}
