import type { ZodType } from "zod";

import { useCurrentCustomer } from "@/identity/useCurrentCustomer";

// The shared HTTP client for the three Wolverine.Http services. Two CritterMart conventions live here:
//
//  - **Convention 4 — the X-Customer-Id header.** Every request carries the current customer's identity
//    ambiently in this header, sourced from the useCurrentCustomer seam (ADR 009). The header name is
//    the single Polecat-promotion swap point; call sites never restate identity in URLs or query params.
//
//  - **Convention 2 — Zod at the wire boundary.** Every response body is `parse()`d through a Zod schema
//    before the app trusts it. Three independently-deployed services (ADR 006, no BFF) are three
//    contract surfaces that can drift; the boundary parse is the only place a drift surfaces — loud and
//    located, never a silent `undefined` deep in a component.
export const CUSTOMER_ID_HEADER = "X-Customer-Id";

// A non-2xx response. `NotFoundError` is split out because `404` is frequently a *domain state*, not a
// failure — e.g. `GET /carts/mine` 404s as "this customer has no open cart" (render an empty cart), and
// a query can map that to data rather than an error boundary.
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
  /** The current customer's id — set on the X-Customer-Id header. Sourced from the identity seam. */
  customerId: string;
}

// Fetch a JSON resource, set the identity header, and parse the body through `schema`. Pure (no React),
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
      [CUSTOMER_ID_HEADER]: ctx.customerId,
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

// POST a command body, set the identity header, and (when the command answers with one) parse the
// response body through `schema`. The command-side counterpart of `fetchParsed`: same X-Customer-Id seam
// (Convention 4) and same boundary parse (Convention 2 — a command's *response* is a wire surface that can
// drift too), plus a JSON body and `Content-Type`. Pure (no React), so mutation factories can call it and
// tests can drive it with a literal context + mocked fetch. Any non-2xx throws `ApiError` (a command has
// no domain-empty 404 case).
//
// `schema` is **optional**, and the overloads make the return type follow it: a command that returns a body
// (W1 `AddToCart` → 201 `{ cartId }`) passes a schema and gets the parsed `T`; a command that returns
// `204 No Content` (slice 3.3 change-qty) omits it and gets `void`. Omitting it also skips the `.json()`
// call — reading a body off a 204 throws "Unexpected end of JSON input", so the no-schema path must not.
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
      [CUSTOMER_ID_HEADER]: ctx.customerId,
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

// DELETE a route-keyed resource, setting the identity header. The SPA's first DELETE (slice 3.2 remove —
// both identifiers ride the route, there is no body either way). Like `postCommand` with no schema, the
// contract is `204 No Content`, so there is nothing to parse; a non-2xx throws `ApiError`. Pure (no React).
export async function deleteCommand(url: string, ctx: RequestContext): Promise<void> {
  const response = await fetch(url, {
    method: "DELETE",
    headers: {
      Accept: "application/json",
      [CUSTOMER_ID_HEADER]: ctx.customerId,
    },
  });

  if (!response.ok) {
    throw new ApiError(`Command to ${url} failed with ${response.status}.`, response.status);
  }
}

// The React binding: builds the per-request context from the identity seam. Components and query hooks
// call this to get the `RequestContext` they hand to `fetchParsed`, so identity always flows from the
// one seam and never from a hardcoded value.
export function useApiContext(): RequestContext {
  const customerId = useCurrentCustomer();
  return { customerId };
}
