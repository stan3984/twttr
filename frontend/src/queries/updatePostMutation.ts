import {
  useMutation,
  useQueryClient,
  type InfiniteData,
} from "@tanstack/react-query";
import {
  CONTENT_MAX,
  CONTENT_MIN,
  type TPost,
  type TUpdatePostRequest,
} from "../types";
import { postsKey } from "./postsQuery";
import { ApiError, describeApiError, request } from "./util";

export function useUpdatePost(id: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (body: TUpdatePostRequest) =>
      request<TPost>(`/api/posts/${id}`, {
        method: "PATCH",
        body: JSON.stringify(body),
      }),
    onSuccess: (updated) => {
      if (!updated) {
        return;
      }

      queryClient.setQueryData<InfiniteData<TPost[]>>(postsKey, (current) => {
        if (!current) {
          return current;
        }

        return {
          ...current,
          pages: current.pages.map((page) =>
            page.map((post) => (post.id === updated.id ? updated : post)),
          ),
        };
      });
    },
  });
}

export function describeUpdatePostError(error: unknown): string {
  if (!(error instanceof ApiError)) {
    return describeApiError(error);
  }

  switch (error.status) {
    case 400:
      return `Post must be ${CONTENT_MIN}-${CONTENT_MAX} characters with no leading or trailing spaces.`;
    case 403:
      return "You can only edit your own posts.";
    case 404:
      return "That post no longer exists.";
    default:
      return describeApiError(error);
  }
}
