"use client";

import { useEffect, useState } from "react";

import { useAccessToken } from "@/hooks/useAccessToken";
import { useCallerIdentity } from "@/hooks/useCallerIdentity";
import { useRuntimeSettings } from "@/lib/runtimeSettings/RuntimeSettingsProvider";
import { greetingFor } from "./greetingText";

// 2026-09-27-481bd: what an empty conversation opens with, in place of 154 words of instruction.
//
// SPLIT INTO TWO COMPONENTS, exactly as HeaderIdentity is, and for the same reason: an
// installation with no authority configured must read no identity and subscribe to no token, and
// the hook that acquires one starts an auth session unconditionally. A hook cannot be called
// conditionally, so the condition has to be a component boundary.

export function DialogGreeting() {
  const { auth } = useRuntimeSettings();
  // BOTH arms are components. A conditional call to the clock hook would render identically and
  // break the rules of hooks, which the tests pass and the linter does not.
  return auth.authority ? <Named /> : <Unnamed />;
}

function Unnamed() {
  return <span>{greetingFor(useHour())}</span>;
}

function Named() {
  const token = useAccessToken();
  const { identity } = useCallerIdentity(token !== null);
  // A name that is not a name is no better than none: the subject carries the NAME-CLAIM value,
  // and on the default claim that value is a directory identifier.
  const name = identity?.nameIsReadable ? identity.subject : null;
  return <span>{greetingFor(useHour(), name)}</span>;
}

/** Read AFTER mount: decided during render it would be the build's clock, not the reader's. */
function useHour(): number {
  const [hour, setHour] = useState(12);
  useEffect(() => setHour(new Date().getHours()), []);
  return hour;
}
