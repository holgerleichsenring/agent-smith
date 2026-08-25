"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { startAuthSession } from "@/lib/auth/session";

// 2026-08-25-2de1: where the authority returns to — the route redirectPath names,
// which existed nowhere until this phase. The code exchange is the boot's own
// work, so this page waits for the one loop rather than starting a second, then
// puts the person back where the redirect took them from.
//
// A client component: the shell is prerendered, the authorization code exists
// only in a browser's URL, and a relative redirect URI has no meaning on a server.

export default function SignInCallbackPage() {
  const router = useRouter();
  const [refusal, setRefusal] = useState<string | null>(null);

  useEffect(() => {
    let live = true;
    void startAuthSession().then((session) => {
      if (!live) return;
      if (session?.error) setRefusal(session.error);
      else router.replace(session?.returnTo ?? "/");
    });
    return () => {
      live = false;
    };
  }, [router]);

  return (
    <div className="p-8 text-sm text-[var(--color-ink-mid)]">
      {refusal ? `The authority did not complete the sign-in: ${refusal}` : "Signing in…"}
    </div>
  );
}
