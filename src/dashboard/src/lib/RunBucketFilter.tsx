"use client";

import { useRouter } from "next/navigation";
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";

// p0458: the rail's monitor items are a FILTER, not a scroll target — a label
// with a live count next to it promises to show that count's runs. The chosen
// bucket lives in the URL so a filtered view is linkable and survives a reload,
// and one shared value drives both the rail's highlight and which sections
// mission control renders, so the two can never disagree.
//
// The URL is read and written through the History API rather than
// useSearchParams: the provider sits in the ROOT LAYOUT, and a search-param
// hook there fails the build for every statically rendered route
// ("useSearchParams() should be wrapped in a suspense boundary at page /").

export const RUN_BUCKETS = ["needs-you", "running", "queued", "finished"] as const;

export type RunBucket = (typeof RUN_BUCKETS)[number];
/** "all" is the unfiltered home screen — every bucket, the way it always was. */
export type RunBucketFilter = RunBucket | "all";

const PARAM = "bucket";
const HOME = "/";

export function bucketHref(filter: RunBucketFilter): string {
  return filter === "all" ? HOME : `${HOME}?${PARAM}=${filter}`;
}

export function parseBucketFilter(search: string): RunBucketFilter {
  const value = new URLSearchParams(search).get(PARAM);
  return RUN_BUCKETS.includes(value as RunBucket) ? (value as RunBucket) : "all";
}

interface RunBucketFilterValue {
  filter: RunBucketFilter;
  select: (filter: RunBucketFilter) => void;
}

// No provider = no filter. Rendering a run surface outside the shell is honest
// about showing everything rather than throwing.
const Ctx = createContext<RunBucketFilterValue>({ filter: "all", select: () => {} });

export function RunBucketFilterProvider({ children }: { children: ReactNode }) {
  const router = useRouter();
  const [filter, setFilter] = useState<RunBucketFilter>("all");

  // Seeded AFTER mount, not during render: the prerendered shell has no URL, so
  // reading one during render would hydrate into a different tree. popstate
  // keeps back/forward honest.
  useEffect(() => {
    const readUrl = () => setFilter(parseBucketFilter(window.location.search));
    readUrl();
    window.addEventListener("popstate", readUrl);
    return () => window.removeEventListener("popstate", readUrl);
  }, []);

  const select = useCallback(
    (next: RunBucketFilter) => {
      setFilter(next);
      // On the home screen the filter is not a navigation: pushing history
      // alone keeps the run list mounted, so the live view never blanks and the
      // paged-in runs survive. From anywhere else the home route has to load.
      if (window.location.pathname === HOME) {
        window.history.pushState(null, "", bucketHref(next));
      } else {
        router.push(bucketHref(next));
      }
    },
    [router],
  );

  const value = useMemo(() => ({ filter, select }), [filter, select]);
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useRunBucketFilter(): RunBucketFilterValue {
  return useContext(Ctx);
}
