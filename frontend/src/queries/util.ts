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

export type ApiResult<T> =
  | { ok: true; status: number; data: T | undefined }
  | { ok: false; status: number; problem?: TProblemDetails };

async function send(path: string | URL, init?: RequestInit) {
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

  return { response, body };
}

/**
 * Non-throwing variant, for endpoints where some 4xx is a valid answer rather than a
 * fault. A rejected fetch (offline, DNS) still throws -- only the status is demoted.
 */
export async function tryRequest<T>(
  path: string | URL,
  init?: RequestInit,
): Promise<ApiResult<T>> {
  const { response, body } = await send(path, init);

  return response.ok
    ? { ok: true, status: response.status, data: body as T | undefined }
    : {
        ok: false,
        status: response.status,
        problem: body as TProblemDetails | undefined,
      };
}

/**
 * Default variant. React Query keys isError/error/retry off a rejected promise, so any
 * status a caller has not deliberately accounted for has to throw to reach the UI.
 */
export async function request<T>(
  path: string | URL,
  init?: RequestInit,
): Promise<T | undefined> {
  const result = await tryRequest<T>(path, init);
  if (!result.ok) {
    throw new ApiError(result.status, result.problem);
  }

  return result.data;
}

/**
 * Fallback for statuses that mean the same thing everywhere. Anything whose meaning
 * depends on the endpoint -- 401, 404, 409 -- belongs in that endpoint's module, which
 * is the only place that knows what it means.
 */
export function describeApiError(error: unknown): string {
  if (!(error instanceof ApiError)) {
    return "Something went wrong. Please try again.";
  }

  switch (error.status) {
    case 400:
      return error.problem?.detail ?? "Please check your details.";
    case 429:
      return "Too many attempts. Try again shortly.";
    default:
      return "Something went wrong. Please try again.";
  }
}
