import { useState, type SyntheticEvent } from "react";
import { useNavigate, Link } from "react-router";
import { describeSignInError, useSignIn } from "./queries/signInMutation";

export function SignIn() {
  const navigate = useNavigate();
  const signIn = useSignIn();

  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");

  function onSubmit(event: SyntheticEvent) {
    event.preventDefault();
    signIn.mutate(
      { username, password },
      { onSuccess: () => navigate("/", { replace: true }) },
    );
  }

  return (
    <div className="max-w-sm px-4 mx-auto mt-24">
      <h1 className="mb-6 text-2xl font-semibold text-slate-900">Sign in</h1>
      <form onSubmit={onSubmit} className="space-y-4">
        <input
          className="w-full px-3 py-2 border rounded border-slate-300"
          placeholder="Username"
          autoComplete="username"
          value={username}
          onChange={(e) => setUsername(e.target.value)}
        />
        <input
          className="w-full px-3 py-2 border rounded border-slate-300"
          type="password"
          placeholder="Password"
          autoComplete="current-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />
        {signIn.error && (
          <p className="text-sm text-red-600">
            {describeSignInError(signIn.error)}
          </p>
        )}
        <button
          type="submit"
          disabled={signIn.isPending}
          className="w-full py-2 text-white rounded bg-slate-900 disabled:opacity-50"
        >
          {signIn.isPending ? "Signing in..." : "Sign in"}
        </button>
      </form>
      <p className="mt-4 text-sm text-slate-600">
        No account?{" "}
        <Link to="/signup" className="underline">
          Sign up
        </Link>
      </p>
    </div>
  );
}
