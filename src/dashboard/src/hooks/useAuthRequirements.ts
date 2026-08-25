"use client";

import { useEffect, useState } from "react";
import { fetchAuthRequirements, type AuthRequirements } from "@/lib/authRequirementsApi";

// 2026-08-25-4530: read once, not polled. This is bootstrap configuration on the
// server side — it cannot change without the server restarting, and a restart
// costs this tab its socket anyway.
//
// Unreachable answers null and every caller says NOTHING. A banner accusing an
// installation of being half-configured because one request did not land would
// be the second-worst outcome after saying nothing at all.

export function useAuthRequirements(): AuthRequirements | null {
  const [requirements, setRequirements] = useState<AuthRequirements | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    fetchAuthRequirements(controller.signal)
      .then(setRequirements)
      .catch((cause: Error) => {
        if (cause.name !== "AbortError") {
          console.debug("the server did not say what it expects of a caller", cause);
        }
      });
    return () => controller.abort();
  }, []);

  return requirements;
}
