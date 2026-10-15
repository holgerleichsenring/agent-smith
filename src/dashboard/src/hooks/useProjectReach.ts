"use client";

import { useEffect, useState } from "react";
import { fetchAuthRequirements } from "@/lib/authRequirementsApi";
import { fetchReadableProjects } from "@/lib/readableProjectsApi";
import { useAccessToken } from "./useAccessToken";
import { useCallerIdentity } from "./useCallerIdentity";

// 2026-10-15-4a4de: whether an empty list means "this sign-in reaches no project". 'none' only for a signed-in
// caller without global runs.read, on a server that enforces, whose GET /api/projects answered an empty list.
// Everything else is 'unknown' and the page keeps its own words: no identity or a global reader asks nothing, an
// unenforced server is not asked for projects, and a pending, refused or failed read proves nothing.

export type ProjectReach = "none" | "unknown";

const RUNS_READ = "runs.read";

export function useProjectReach(): ProjectReach {
  const token = useAccessToken();
  const { identity } = useCallerIdentity(token !== null);
  const [reach, setReach] = useState<ProjectReach>("unknown");
  const asks = identity !== null && identity.authenticated && !identity.permissions.includes(RUNS_READ);

  useEffect(() => {
    setReach("unknown");
    if (!asks) return;
    const controller = new AbortController();
    fetchAuthRequirements(controller.signal)
      .then((requirements) => (requirements.enforced ? fetchReadableProjects(controller.signal) : null))
      .then((projects) => { if (projects !== null && projects.length === 0) setReach("none"); })
      .catch(() => {});
    return () => controller.abort();
  }, [asks]);

  return reach;
}
