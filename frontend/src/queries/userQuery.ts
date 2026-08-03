import { queryOptions, useQueries } from "@tanstack/react-query";
import type { TPost, TUser } from "../types";
import { ApiError, tryRequest } from "./util";

async function fetchUser(id: string) {
  const result = await tryRequest<TUser>(`/api/users/${id}`);
  if (result.ok) {
    return result.data ?? null;
  }

  if (result.status === 404) {
    return null;
  }

  throw new ApiError(result.status, result.problem);
}

function userQueryOptions(id: string) {
  return queryOptions({
    queryKey: ["api", "users", id],
    queryFn: () => fetchUser(id),
    // Names change rarely and every post in the feed needs one.
    staleTime: 300_000,
  });
}

// Resolve user IDs to users.
export function useAuthors(posts: TPost[]) {
  // Deduplicate user IDs.
  const ids = [...new Set(posts.map((post) => post.authorId))].sort();

  return useQueries({
    queries: ids.map((id) => userQueryOptions(id)),
    combine: (results) => {
      const authors = new Map<string, TUser>();
      results.forEach((result, index) => {
        if (result.data) {
          authors.set(ids[index], result.data);
        }
      });

      return authors;
    },
  });
}
