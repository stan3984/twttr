import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { TRegisterRequest } from "../types";
import { fetchIdentity, identityKey } from "./identityQuery";
import { request } from "./util";

export function useSignUp() {
  const queryClient = useQueryClient();

  return useMutation({
    // Register answers 201 with no body and signs the user in server-side, so there is
    // never a follow-up login call -- only a fetch to learn who we now are.
    mutationFn: async (body: TRegisterRequest) => {
      await request("/api/auth/register", {
        method: "POST",
        body: JSON.stringify(body),
      });

      return fetchIdentity();
    },
    onSuccess: (identity) => queryClient.setQueryData(identityKey, identity),
  });
}
