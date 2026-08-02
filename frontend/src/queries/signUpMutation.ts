import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { TRegisterRequest } from "../types";
import { fetchIdentity, identityKey } from "./identityQuery";
import { ApiError, describeApiError, request } from "./util";

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

// 409 on register is a collision on username or email.
export function describeSignUpError(error: unknown): string {
  if (error instanceof ApiError && error.status === 409) {
    return "That username or email is already taken.";
  }

  return describeApiError(error);
}
