import {
  useMutation,
  useQueryClient,
  type InfiniteData,
} from "@tanstack/react-query";
import type { TPost } from "../types";
import { postsKey } from "./postsQuery";
import { request } from "./util";

export function useDeletePost(id: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => request<void>(`/api/posts/${id}`, { method: "DELETE" }),
    onSuccess: () => {
      queryClient.setQueryData<InfiniteData<TPost[]>>(postsKey, (current) => {
        if (!current) {
          return current;
        }

        return {
          ...current,
          pages: current.pages.map((page) =>
            page.filter((post) => post.id !== id),
          ),
        };
      });
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: postsKey }),
  });
}
