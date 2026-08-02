import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { TLoginRequest } from "../types";
import { fetchIdentity, identityKey } from "./identityQuery";
import { request } from "./util";

export function useSignIn() {
  const queryClient = useQueryClient();

  return useMutation({
    // Login answers 204 with no body, so the identity has to be fetched afterwards.
    // Doing both inside one mutationFn keeps isPending true for the whole sequence and
    // avoids racing a refetch against the cache write below.
    mutationFn: async (body: TLoginRequest) => {
      await request("/api/auth/login", {
        method: "POST",
        body: JSON.stringify(body),
      });

      return fetchIdentity();
    },
    onSuccess: (identity) => queryClient.setQueryData(identityKey, identity),
  });
}
