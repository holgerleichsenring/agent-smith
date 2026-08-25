"use client";

import { useEffect, useState } from "react";
import { fetchAuthRequirements, type AuthRequirements } from "@/lib/authRequirementsApi";
import { useAccessToken } from "./useAccessToken";

// 2026-08-25-4530: read once, not polled. The authority and the enforce switch are
// bootstrap configuration on the server side — they cannot change without the server
// restarting, and a restart costs this tab its socket anyway.
//
// 2026-08-25-1806: re-read when the TOKEN changes, because the answer now also carries
// whether this caller's token was refused — which is a fact about the request, not about
// the installation. A signed-in tab that kept the answer it got while signed out would
// report a refusal that belongs to nobody.
//
// Unreachable answers null and every caller says NOTHING. A banner accusing an
// installation of being half-configured because one request did not land would
// be the second-worst outcome after saying nothing at all.

export function useAuthRequirements(): AuthRequirements | null {
  const token = useAccessToken();
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
  }, [token]);

  return requirements;
}
