import type { ZodType } from "zod";

import { useAuth } from "@/identity/useCurrentCustomer";

// The shared HTTP client for the Wolverine.Http services. Two CritterMart conventions live here:
//
//  - **Convention 4 — the Authorization: Bearer header (ADR 023).** Every request that needs identity
//    carries the authenticated customer's JWT in this header, sourced from the auth seam (useAuth). This
//    replaced the round-one X-Customer-Id header: the `sub` claim of the verified token is now the trust
//    boundary (slice 5.10). Public reads (Catalog browsing) carry no token and send no auth header. The
//    header is the single cutover point; call sites never restate identity in URLs or query params.
//
//  - **Convention 2 — Zod at the wire boundary.** Every response body is `parse()`d through a Zod schema
//    before the app trusts it. Independently-deployed services (ADR 006, no BFF) are separate contract
//    surfaces that can drift; the boundary parse is the only place a drift surfaces — loud and located.

// A non-2xx response. `NotFoundError` is split out because `404` is frequently a *domain state*, not a
// failure — e.g. `GET /carts/mine` 404s as "this customer has no open cart" (render an empty cart).
export class ApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

export class NotFoundError extends ApiError {
  constructor(url: string) {
    super(`Resource at ${url} was not found.`, 404);
    this.name = "NotFoundError";
  }
}

export interface RequestContext {
  /** The authenticated customer's JWT for the Authorization header, or null when logged out. */
  token: string | null;
  /** The current customer's id (the token's `sub`), for cache keys + optimistic updates. "" when logged out. */
  customerId: string;
}

// The auth header for a request: a Bearer token when the customer is authenticated, nothing otherwise
// (so Catalog's public reads work logged-out). The `sub` claim inside the token is what the resource
// server trusts — the id never rides a header of its own anymore.
function authHeaders(ctx: RequestContext): Record<string, string> {
  return ctx.token ? { Authorization: `Bearer ${ctx.token}` } : {};
}

// Fetch a JSON resource, attach the bearer token, and parse the body through `schema`. Pure (no React),
// so query/mutation factories can call it and tests can drive it with a literal context + mocked fetch.
// A `404` throws `NotFoundError` so callers can branch on the domain-empty case; any other non-2xx
// throws `ApiError`.
export async function fetchParsed<T>(
  url: string,
  schema: ZodType<T>,
  ctx: RequestContext,
): Promise<T> {
  const response = await fetch(url, {
    headers: {
      Accept: "application/json",
      ...authHeaders(ctx),
    },
  });

  if (response.status === 404) {
    throw new NotFoundError(url);
  }
  if (!response.ok) {
    throw new ApiError(`Request to ${url} failed with ${response.status}.`, response.status);
  }

  return schema.parse(await response.json());
}

// POST a command body, attach the bearer token, and (when the command answers with one) parse the
// response body through `schema`. `schema` is optional, and the overloads make the return type follow it:
// a command that returns a body passes a schema and gets the parsed `T`; a `204 No Content` command omits
// it and gets `void` (omitting it also skips the `.json()` call — reading a body off a 204 throws).
export function postCommand<T>(
  url: string,
  body: unknown,
  ctx: RequestContext,
  schema: ZodType<T>,
): Promise<T>;
export function postCommand(url: string, body: unknown, ctx: RequestContext): Promise<void>;
export async function postCommand<T>(
  url: string,
  body: unknown,
  ctx: RequestContext,
  schema?: ZodType<T>,
): Promise<T | void> {
  const response = await fetch(url, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      ...authHeaders(ctx),
    },
    body: JSON.stringify(body),
  });

  if (!response.ok) {
    throw new ApiError(`Command to ${url} failed with ${response.status}.`, response.status);
  }

  // 204 No Content (no schema): the command succeeded with no body to parse — do NOT call `.json()`.
  if (!schema) return;
  return schema.parse(await response.json());
}

// DELETE a route-keyed resource, attaching the bearer token. Like `postCommand` with no schema, the
// contract is `204 No Content`, so there is nothing to parse; a non-2xx throws `ApiError`. Pure (no React).
export async function deleteCommand(url: string, ctx: RequestContext): Promise<void> {
  const response = await fetch(url, {
    method: "DELETE",
    headers: {
      Accept: "application/json",
      ...authHeaders(ctx),
    },
  });

  if (!response.ok) {
    throw new ApiError(`Command to ${url} failed with ${response.status}.`, response.status);
  }
}

// The React binding: builds the per-request context from the auth seam. Components and query hooks call
// this to get the `RequestContext` they hand to `fetchParsed`, so identity always flows from the one seam
// (the token for the header, the id for cache keys) and never from a hardcoded value.
export function useApiContext(): RequestContext {
  const { token, customerId } = useAuth();
  return { token, customerId: customerId ?? "" };
}
