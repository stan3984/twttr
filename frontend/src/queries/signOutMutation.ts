import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router";
import { identityKey } from "./identityQuery";
import { request } from "./util";

export function useSignOut() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => request("/api/auth/logout", { method: "POST" }),
    onSuccess: () => {
      // Everything cached was fetched as the previous user, so drop all of it. Otherwise the next
      // account to sign in on this tab would briefly see the previous user's data.
      queryClient.clear();
      queryClient.setQueryData(identityKey, null);
      navigate("/signin", { replace: true });
    },
  });
}
