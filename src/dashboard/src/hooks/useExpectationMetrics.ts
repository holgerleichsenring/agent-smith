"use client";

import { useEffect, useState } from "react";
import { fetchExpectationMetrics, type ExpectationMetrics } from "@/lib/expectationsApi";

export interface ExpectationRead {
  data: ExpectationMetrics | null;
  error: Error | null;
}

// 2026-08-27-559e: the criteria read, lifted out of the view that used to own
// it. The Overview shows the same outcomes twice — as a card figure and as a
// panel — and a read owned by one of them would mean a second request for a
// number the first already answered. Read once here, passed to both.

export function useExpectationMetrics(): ExpectationRead {
  const [data, setData] = useState<ExpectationMetrics | null>(null);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    fetchExpectationMetrics(controller.signal)
      .then(setData)
      .catch((e: Error) => {
        if (e.name !== "AbortError") setError(e);
      });
    return () => controller.abort();
  }, []);

  return { data, error };
}
