import { useInfiniteQuery } from "@tanstack/react-query";
import { POSTS_PAGE_SIZE, type TPost } from "../types";
import { request } from "./util";

export const postsKey = ["api", "posts"];

export async function fetchPosts(skip: number, take: number) {
  const query = new URLSearchParams({ skip: String(skip), take: String(take) });
  return (await request<TPost[]>(`/api/posts?${query}`)) ?? [];
}

export function usePosts() {
  return useInfiniteQuery({
    queryKey: postsKey,
    queryFn: ({ pageParam }) => fetchPosts(pageParam, POSTS_PAGE_SIZE),
    initialPageParam: 0,
    // We only know whether we've reached the end if there are fewer than POSTS_PAGE_SIZE posts
    // in the page.
    getNextPageParam: (last, all) =>
      last.length < POSTS_PAGE_SIZE ? undefined : all.length * POSTS_PAGE_SIZE,
    staleTime: 30_000,
  });
}
