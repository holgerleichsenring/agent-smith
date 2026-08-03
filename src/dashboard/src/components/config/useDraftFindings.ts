"use client";

import { useEffect, useState } from "react";
import {
  validateProjectDraft,
  validateTrackerDraft,
  type ConfigEntityKind,
  type ConfigFinding,
  type StudioEntity,
  type StudioProject,
  type StudioTracker,
} from "@/lib/configApi";

// p0392: ask the SERVER what it would say about this draft, while it is still a draft.
// p0391a made the server report what is missing once it is running — after a deploy, from
// a container. The editor that produced the configuration is a better place to hear it,
// and it is the same answer: the studio holds no rules, it renders what it is told.
//
// Only the two kinds with rules beyond referential integrity are asked. A failed request
// yields NO findings rather than a fake one: the studio must never invent a block.

const DEBOUNCE_MS = 250;

export function useDraftFindings(kind: ConfigEntityKind, draft: StudioEntity): ConfigFinding[] {
  const [findings, setFindings] = useState<ConfigFinding[]>([]);
  const key = JSON.stringify(draft);

  useEffect(() => {
    if (kind !== "projects" && kind !== "trackers") {
      setFindings([]);
      return;
    }
    const controller = new AbortController();
    const timer = setTimeout(() => {
      const parsed = JSON.parse(key) as StudioEntity;
      try {
        const ask =
          kind === "projects"
            ? validateProjectDraft(parsed as StudioProject, controller.signal)
            : validateTrackerDraft(parsed as StudioTracker, controller.signal);
        ask.then(setFindings).catch(() => setFindings([]));
      } catch {
        setFindings([]);
      }
    }, DEBOUNCE_MS);

    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [kind, key]);

  return findings;
}

export function blockingFindings(findings: ConfigFinding[]): ConfigFinding[] {
  return findings.filter((f) => f.severity === "blocking");
}
