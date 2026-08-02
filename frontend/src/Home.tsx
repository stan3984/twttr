import { useIdentity } from "./queries/identityQuery";
import { useSignOut } from "./queries/signOutMutation";

export function Home() {
  // Reads straight from the cache: RequireAuth already resolved this same query key,
  // so there is no second request.
  const { data: identity } = useIdentity();
  const signOut = useSignOut();

  return (
    <div className="max-w-sm px-4 mx-auto mt-24">
      <h1 className="mb-2 text-2xl font-semibold text-slate-900">
        Hello, {identity?.displayName}
      </h1>
      <p className="mb-6 text-sm text-slate-600">@{identity?.username}</p>
      <button
        onClick={() => signOut.mutate()}
        disabled={signOut.isPending}
        className="px-4 py-2 border rounded border-slate-300 disabled:opacity-50"
      >
        {signOut.isPending ? "Signing out..." : "Sign out"}
      </button>
    </div>
  );
}
