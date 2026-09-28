"use client";

import { useEffect, useState } from "react";
import { fetchExpectationMetrics, type CriteriaMet } from "@/lib/expectationsApi";

export interface ExpectationRead {
  data: CriteriaMet | null;
  error: Error | null;
}

// The Criteria met read, made once for the Overview: the card and the panel below it
// show the same counts, and a read owned by one of them would be a second request for
// a number the first already answered.

export function useExpectationMetrics(): ExpectationRead {
  const [data, setData] = useState<CriteriaMet | null>(null);
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
