import { useState, type SyntheticEvent } from "react";
import { useNavigate, Link } from "react-router";
import { describeSignUpError, useSignUp } from "./queries/signUpMutation";
import {
  PASSWORD_MAX,
  PASSWORD_MIN,
  USERNAME_MAX,
  USERNAME_MIN,
} from "./types";

function validate(username: string, password: string) {
  if (username.length < USERNAME_MIN || username.length > USERNAME_MAX) {
    return `Username must be ${USERNAME_MIN}-${USERNAME_MAX} characters.`;
  }

  if (!/^[a-zA-Z0-9]+$/.test(username)) {
    return "Username may only contain letters and digits.";
  }

  if (password.length < PASSWORD_MIN || password.length > PASSWORD_MAX) {
    return `Password must be ${PASSWORD_MIN}-${PASSWORD_MAX} characters.`;
  }

  return null;
}

export function SignUp() {
  const navigate = useNavigate();
  const signUp = useSignUp();

  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [invalid, setInvalid] = useState<string | null>(null);

  function onSubmit(event: SyntheticEvent) {
    event.preventDefault();

    const problem = validate(username, password);
    setInvalid(problem);
    if (problem) {
      return;
    }

    signUp.mutate(
      { username, password, email },
      { onSuccess: () => navigate("/", { replace: true }) },
    );
  }

  const error =
    invalid ?? (signUp.error ? describeSignUpError(signUp.error) : null);

  return (
    <div className="max-w-sm px-4 mx-auto mt-24">
      <h1 className="mb-6 text-2xl font-semibold text-slate-900">Sign up</h1>
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
          type="email"
          placeholder="Email"
          autoComplete="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
        />
        <input
          className="w-full px-3 py-2 border rounded border-slate-300"
          type="password"
          placeholder="Password"
          autoComplete="new-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />
        {error && <p className="text-sm text-red-600">{error}</p>}
        <button
          type="submit"
          disabled={signUp.isPending}
          className="w-full py-2 text-white rounded bg-slate-900 disabled:opacity-50"
        >
          {signUp.isPending ? "Signing up..." : "Sign up"}
        </button>
      </form>
      <p className="mt-4 text-sm text-slate-600">
        Already have an account?{" "}
        <Link to="/signin" className="underline">
          Sign in
        </Link>
      </p>
    </div>
  );
}
