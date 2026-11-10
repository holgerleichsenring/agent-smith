"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { Badge } from "@/components/ui/Badge";
import { SectionLabel } from "@/components/ui/SectionLabel";
import { deleteProjectMemory, fetchProjectMemories, type ProjectMemory } from "@/lib/projectMemoryApi";

// 2026-11-10-fcd7d: what the project's conversations and runs kept on the project — newest first, because
// entries reach later prompts unreviewed and the newest are the ones a person most needs to check. Each shows
// its standing, its record and where it came from; deleting one is the only change made here (a person who
// wants the text changed tells a turn, so the text keeps one author).

type Load = { kind: "loading" } | { kind: "refused" } | { kind: "loaded"; memories: ProjectMemory[] };

export function ProjectMemorySection({ project }: { project: string }) {
  const [load, setLoad] = useState<Load>({ kind: "loading" });

  useEffect(() => {
    const abort = new AbortController();
    fetchProjectMemories(project, abort.signal)
      .then((memories) => setLoad({ kind: "loaded", memories }))
      .catch(() => {
        if (!abort.signal.aborted) setLoad({ kind: "refused" });
      });
    return () => abort.abort();
  }, [project]);

  async function remove(slug: string) {
    if (!(await deleteProjectMemory(project, slug))) return;
    setLoad((held) => (held.kind === "loaded"
      ? { kind: "loaded", memories: held.memories.filter((m) => m.slug !== slug) }
      : held));
  }

  return (
    <section className="mt-4 border-t border-stone-100 pt-4" data-testid="project-memory-section">
      <SectionLabel>Project memory</SectionLabel>
      <div className="mt-2">
        {load.kind === "loading" && <span className="dsh-label text-stone-400">loading…</span>}
        {load.kind === "refused" && <span className="dsh-label text-stone-400">not available</span>}
        {load.kind === "loaded" && load.memories.length === 0 && (
          <span className="dsh-label text-stone-400" data-testid="project-memory-empty">nothing kept yet</span>
        )}
        {load.kind === "loaded" && load.memories.length > 0 && (
          <ul className="space-y-2">
            {load.memories.map((m) => <MemoryRow key={m.slug} memory={m} onDelete={() => remove(m.slug)} />)}
          </ul>
        )}
      </div>
    </section>
  );
}

function MemoryRow({ memory, onDelete }: { memory: ProjectMemory; onDelete: () => void }) {
  return (
    <li className="rounded-md border border-stone-100 px-3 py-2" data-testid={`project-memory-${memory.slug}`}>
      <div className="flex items-center gap-2">
        <span className="dsh-body font-semibold text-stone-700">{memory.slug}</span>
        <Badge tone="neutral">{memory.type}</Badge>
        <Badge tone={memory.standing === "holding" ? "green" : memory.standing === "disputed" ? "rose" : "neutral"}>
          {memory.standing}
        </Badge>
        <button
          type="button"
          className="ml-auto dsh-label text-stone-500 hover:text-rose-700"
          aria-label={`Delete memory ${memory.slug}`}
          data-testid={`project-memory-delete-${memory.slug}`}
          onClick={onDelete}
        >
          Delete
        </button>
      </div>
      <div className="mt-1 dsh-body text-stone-700">{memory.description}</div>
      {/* The text later prompts quote, folded until a person opens it to check what it says. */}
      <details className="mt-1" data-testid={`project-memory-body-${memory.slug}`}>
        <summary className="cursor-pointer dsh-label text-stone-500">Full text</summary>
        <p className="mt-1 whitespace-pre-wrap dsh-body text-stone-700">{memory.body}</p>
      </details>
      <div className="mt-1 flex flex-wrap gap-x-4 dsh-label text-stone-400">
        <span>{memory.record}</span>
        <span data-testid={`project-memory-source-${memory.slug}`}><Source memory={memory} /></span>
      </div>
    </li>
  );
}

function Source({ memory }: { memory: ProjectMemory }) {
  const by = memory.author ? `, by ${memory.author}` : "";
  if (memory.sourceKind === "run") {
    return (
      <>
        from run <Link href={`/jobs/${encodeURIComponent(memory.sourceId)}`}>{memory.sourceId}</Link>
        {memory.sourceRef ? `, spec ${memory.sourceRef}` : ""}{by}
      </>
    );
  }
  return <>from conversation {memory.sourceId}{memory.sourceRef ? `, turn ${memory.sourceRef}` : ""}{by}</>;
}
