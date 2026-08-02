import type { ReactNode } from "react";
import { Navigate } from "react-router";
import { useIdentity } from "./queries/identityQuery";

interface Props {
  children?: ReactNode;
}

export default function RequireAuth({ children }: Readonly<Props>) {
  const { data: identity, isPending } = useIdentity();

  if (isPending) {
    return import.meta.env.DEV ? <div>Loading...</div> : null;
  }

  if (!identity) {
    return <Navigate to="/signin" />;
  }

  return children;
}
