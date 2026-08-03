import {
  useMutation,
  useQueryClient,
  type InfiniteData,
} from "@tanstack/react-query";
import {
  CONTENT_MAX,
  CONTENT_MIN,
  type TCreatePostRequest,
  type TPost,
} from "../types";
import { postsKey } from "./postsQuery";
import { ApiError, describeApiError, request } from "./util";

export function useCreatePost() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: TCreatePostRequest) =>
      request<TPost>("/api/posts", {
        method: "POST",
        body: JSON.stringify(body),
      }),
    onSuccess: (post) => {
      if (!post) {
        return;
      }

      // optimistic update
      queryClient.setQueryData<InfiniteData<TPost[]>>(postsKey, (current) => {
        if (!current) {
          return current;
        }

        const [first, ...rest] = current.pages;
        return { ...current, pages: [[post, ...first], ...rest] };
      });
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: postsKey }),
  });
}

export function describeCreatePostError(error: unknown): string {
  if (error instanceof ApiError && error.status === 429) {
    return "You're posting too fast. Wait a moment and try again.";
  }

  if (error instanceof ApiError && error.status === 400) {
    return `Post must be ${CONTENT_MIN}-${CONTENT_MAX} characters with no leading or trailing spaces.`;
  }

  return describeApiError(error);
}
