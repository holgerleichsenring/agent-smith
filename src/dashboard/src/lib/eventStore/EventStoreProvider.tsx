"use client";

import { createContext, useContext, useEffect, useRef, type ReactNode } from "react";
import { getJobsHubClient } from "@/lib/JobsHubClient";
import { EventStore } from "./eventStore";

// p0218: app-level holder for the shared EventStore. Living in the root layout
// (above every route), it keeps ONE system subscription alive for the app's
// lifetime, so navigating between subsystem/run views neither re-subscribes nor
// wipes the shared backlog. Tests inject a store built on a fake source.

const HUB_URL = process.env.NEXT_PUBLIC_HUB_URL ?? "/hub/jobs";

const EventStoreContext = createContext<EventStore | null>(null);

export function EventStoreProvider({
  children,
  store,
}: {
  children: ReactNode;
  store?: EventStore;
}) {
  const ref = useRef<EventStore | null>(store ?? null);
  if (ref.current === null) ref.current = new EventStore(getJobsHubClient(HUB_URL));
  const active = ref.current;

  // One system subscription, held for the provider's lifetime.
  useEffect(() => active.systemScope().acquire(), [active]);

  return <EventStoreContext.Provider value={active}>{children}</EventStoreContext.Provider>;
}

export function useEventStore(): EventStore {
  const store = useContext(EventStoreContext);
  if (!store) throw new Error("useEventStore must be used within an EventStoreProvider");
  return store;
}
