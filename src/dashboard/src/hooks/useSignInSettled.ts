"use client";

import { useEffect, useState } from "react";
import { signInSettled } from "@/lib/auth/session";

// 2026-09-21-291b: the React reading of "the boot has finished trying". A surface
// that acts on a missing token needs this and the token, because the two are not
// the same question: 2026-08-28-0f46 made the boot settle WITHOUT awaiting the
// silent attempt, so for about a second "no token" means "not yet" rather than
// "nobody". Anything acting in that gap acts over its own working sign-in.

/** True once this tab's unprompted sign-in attempt has finished, either way. */
export function useSignInSettled(): boolean {
  const [settled, setSettled] = useState(false);

  useEffect(() => {
    let mounted = true;
    void signInSettled().then(() => {
      if (mounted) setSettled(true);
    });
    return () => {
      mounted = false;
    };
  }, []);

  return settled;
}
