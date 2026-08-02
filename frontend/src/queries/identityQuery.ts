import { useQuery } from "@tanstack/react-query";
import type { TIdentity } from "../types";
import { ApiError, tryRequest } from "./util";

export const identityKey = ["api", "auth", "me"];

export function useIdentity() {
  return useQuery({
    queryKey: identityKey,
    queryFn: fetchIdentity,
    retry: false,
    staleTime: 30_000,
  });
}

export async function fetchIdentity() {
  const result = await tryRequest<TIdentity>("/api/auth/me");
  if (result.ok) {
    return result.data ?? null;
  }

  // 401 is the normal "not signed in" answer, not a failure in this case.
  if (result.status === 401) {
    return null;
  }

  throw new ApiError(result.status, result.problem);
}
