import { useQuery } from "@tanstack/react-query";
import type { TIdentity } from "../types";
import { ApiError, request } from "./util";

export const identityKey = ["api", "auth", "me"];

export function useIdentity() {
  return useQuery({
    queryKey: identityKey,
    queryFn: fetchIdentity,
    // Default is 3 retries with backoff. A throw here means a real network fault, so
    // retrying only leaves the app sitting on "Loading..." before it redirects.
    retry: false,
    staleTime: 30_000,
  });
}

export async function fetchIdentity() {
  try {
    return (await request<TIdentity>("/api/auth/me")) ?? null;
  } catch (error) {
    // 401 is the normal "not signed in" answer, not a failure. Returning null keeps the
    // query in a success state, so isError stays meaningful for real problems.
    if (error instanceof ApiError && error.status === 401) {
      return null;
    }

    throw error;
  }
}
