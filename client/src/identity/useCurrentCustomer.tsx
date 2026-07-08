import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from "react";

import { serviceUrls } from "@/config";
import { customerIdFromToken, initialToken, persistToken } from "@/identity/authStore";

// The identity seam (ADR 009 → ADR 023). What was a hardcoded stub is now the real authenticated session:
// this provider holds the JWT the customer logged in for, and exposes it through one hook so **no component
// reads the raw token or restates identity** — they call useCurrentCustomer() for the id (cache keys, "who
// am I" branches) or useAuth() for the token + login/logout actions. The shared HTTP client
// (src/api/client.ts) reads this seam once to set the Authorization: Bearer header, so the header→claim
// cutover ADR 009 engineered is a single localized swap, every call site unchanged.
//
// This is still the ONE place identity is sourced. The `customerId` prop remains for testability — page and
// mutation tests inject a fixed id (and mock fetch) without a real login round-trip; production seeds the
// session from a persisted token (initialToken) and updates it through login/logout.

// Raised by login/register so pages can render a friendly message. Deliberately NOT the client's ApiError
// (that would couple the identity seam to the HTTP client, which imports THIS module — a cycle).
export class AuthError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "AuthError";
  }
}

interface AuthContextValue {
  /** The raw JWT for the Authorization header, or null when logged out. */
  token: string | null;
  /** The authenticated customer's id (the token's `sub`), or null when logged out. */
  customerId: string | null;
  isAuthenticated: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, displayName: string, password: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

async function postJson(url: string, body: unknown): Promise<Response> {
  return fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    body: JSON.stringify(body),
  });
}

// Named CurrentCustomerProvider for continuity with the ADR 009 seam (and the existing test call sites).
// `customerId`/`token` props are test/override seams; production passes neither and seeds from the persisted
// session token.
export function CurrentCustomerProvider({
  customerId: injectedCustomerId,
  token: injectedToken,
  children,
}: {
  customerId?: string;
  token?: string;
  children: ReactNode;
}) {
  // Lazy initializer — seed once from the injected override (tests) or the persisted session token, without
  // re-reading sessionStorage on every render.
  const [token, setToken] = useState<string | null>(() => injectedToken ?? initialToken());

  const applyToken = useCallback((next: string | null) => {
    setToken(next);
    persistToken(next);
  }, []);

  const login = useCallback(
    async (email: string, password: string) => {
      const res = await postJson(`${serviceUrls.identityUrl}/login`, { email, password });
      if (!res.ok) {
        // 401 with no enumeration — one message for wrong password OR unknown email (slice 5.9).
        throw new AuthError("Incorrect email or password.");
      }
      const body = (await res.json()) as { token: string };
      applyToken(body.token);
    },
    [applyToken],
  );

  const register = useCallback(
    async (email: string, displayName: string, password: string) => {
      const res = await postJson(`${serviceUrls.identityUrl}/register`, { email, displayName, password });
      if (!res.ok) {
        // 409 duplicate email or 400 weak password — surface the server's ProblemDetails detail.
        let detail = "Registration failed. Please check your details and try again.";
        try {
          const problem = (await res.json()) as { detail?: string; title?: string };
          detail = problem.detail ?? problem.title ?? detail;
        } catch {
          // non-JSON body — keep the generic message
        }
        throw new AuthError(detail);
      }
      // Registration does not return a token (slice 5.8 responds 201 with the id); log in to obtain one.
      await login(email, password);
    },
    [login],
  );

  // Logout is client-side token discard (slice 5.11): drop the token, no server call. The already-issued
  // token stays cryptographically valid until it expires (no revocation this increment), but the SPA no
  // longer holds it, so subsequent requests carry no bearer and gated routes send the customer to login.
  const logout = useCallback(() => applyToken(null), [applyToken]);

  const value = useMemo<AuthContextValue>(() => {
    const customerId = injectedCustomerId ?? customerIdFromToken(token);
    return {
      token,
      customerId,
      isAuthenticated: customerId !== null,
      login,
      register,
      logout,
    };
  }, [injectedCustomerId, token, login, register, logout]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

function useAuthContext(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (ctx === null) {
    throw new Error("useAuth/useCurrentCustomer must be used within a CurrentCustomerProvider.");
  }
  return ctx;
}

// The full auth surface: token (for the Authorization header), customerId, and the session actions.
export function useAuth(): AuthContextValue {
  return useAuthContext();
}

// The current customer's id (the token's `sub`), or null when logged out. Components that only need "who am
// I" call this; the shared client reads the token via useAuth for the Bearer header.
export function useCurrentCustomer(): string | null {
  return useAuthContext().customerId;
}
