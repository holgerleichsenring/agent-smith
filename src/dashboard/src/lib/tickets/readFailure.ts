import { ApiResponseError, refusalIn } from "@/lib/apiResponse";
import type { Read } from "./TicketPageReads";

/** A failed re-read keeps what an earlier read showed; only the flags say it failed. */
export function failure<T>(thrown: unknown, held: Read<T> | undefined): Read<T> {
  const refusal = refusalIn(thrown);
  const notFound = thrown instanceof ApiResponseError && thrown.status === 404;
  return { data: held?.data ?? null, failed: refusal === null, refusal, loading: false, notFound };
}
