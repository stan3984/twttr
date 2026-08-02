import type { TProblemDetails } from "../types";

export class ApiError extends Error {
  readonly status: number;
  readonly problem?: TProblemDetails;

  constructor(status: number, problem?: TProblemDetails) {
    super(
      problem?.detail ?? problem?.title ?? `unexpected status code ${status}`,
    );
    this.name = "ApiError";
    this.status = status;
    this.problem = problem;
  }
}

export async function request<T>(
  path: string | URL,
  init?: RequestInit,
): Promise<T | undefined> {
  const response = await fetch(path, {
    ...init,
    headers: { "Content-Type": "application/json", ...init?.headers },
  });

  const text = await response.text();
  let body: unknown = undefined;
  if (text) {
    try {
      body = JSON.parse(text);
    } catch {
      body = undefined;
    }
  }

  if (!response.ok) {
    throw new ApiError(response.status, body as TProblemDetails | undefined);
  }

  return body as T | undefined;
}

export function describeAuthError(error: unknown): string {
  if (!(error instanceof ApiError)) {
    return "Something went wrong. Please try again.";
  }

  switch (error.status) {
    case 400:
      return error.problem?.detail ?? "Please check your details.";
    case 401:
      return "Incorrect username or password.";
    case 409:
      return "That username or email is already taken.";
    case 429:
      return "Too many attempts. Try again shortly.";
    default:
      return "Something went wrong. Please try again.";
  }
}
