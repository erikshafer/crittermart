// Token persistence + JWT decoding for the storefront's auth (ADR 023, slices 5.9/5.11). The JWT minted by
// Identity is a STATELESS bearer credential: the SPA holds it, sends it as `Authorization: Bearer …` to the
// resource servers, and discards it on logout (there is no server-side session — slice 5.11). We persist it
// in sessionStorage so a page refresh within the tab keeps the customer logged in, but it does NOT survive
// closing the tab (a deliberately conservative default for a bearer token with no refresh flow).
//
// The decoding here is for DISPLAY / cache-keying only — it reads the `sub` claim without verifying the
// signature. The SPA never trusts the token; the resource servers verify it offline (slice 5.10). A tampered
// token would still be rejected server-side with 401; decoding it client-side just tells the UI who it
// *claims* to be so it can render "logged in" state and key the cart/order caches by customer.

const STORAGE_KEY = "crittermart.auth.token";

function readStored(): string | null {
  try {
    return sessionStorage.getItem(STORAGE_KEY);
  } catch {
    return null; // sessionStorage can throw in some sandboxed contexts — treat as logged out.
  }
}

export function persistToken(token: string | null): void {
  try {
    if (token) {
      sessionStorage.setItem(STORAGE_KEY, token);
    } else {
      sessionStorage.removeItem(STORAGE_KEY);
    }
  } catch {
    // Non-fatal: the in-memory token still drives the session for this page load.
  }
}

// The initial token for a fresh page load — a still-valid persisted token, or null.
export function initialToken(): string | null {
  const token = readStored();
  if (token && isExpired(token)) {
    persistToken(null);
    return null;
  }
  return token;
}

interface JwtClaims {
  sub?: string;
  exp?: number;
}

function decodeClaims(token: string): JwtClaims {
  try {
    const payload = token.split(".")[1];
    // base64url → base64, then decode. atob is available in the browser + jsdom.
    const json = atob(payload.replace(/-/g, "+").replace(/_/g, "/"));
    return JSON.parse(json) as JwtClaims;
  } catch {
    return {};
  }
}

// The customer id the token claims (its `sub`), or null. Used for the "logged in" state and cache keys.
export function customerIdFromToken(token: string | null): string | null {
  if (!token) return null;
  return decodeClaims(token).sub ?? null;
}

// A token whose `exp` is in the past. Client-side expiry is a courtesy (avoid sending a known-dead token
// and gate the UI); the resource server's lifetime check is the real enforcement.
export function isExpired(token: string): boolean {
  const { exp } = decodeClaims(token);
  return typeof exp === "number" && exp * 1000 <= Date.now();
}
