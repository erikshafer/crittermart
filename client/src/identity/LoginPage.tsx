import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "@tanstack/react-router";

import { AuthError, useAuth } from "@/identity/useCurrentCustomer";

// The Login screen (Narrative 010 Moment 2). Posts LogIn { email, password } to Identity; on success the
// auth seam holds the returned JWT and the customer is sent to the storefront. A 401 surfaces as one
// message — "Incorrect email or password." — for a wrong password OR an unknown email alike (no user
// enumeration, slice 5.9).
export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await login(email, password);
      await navigate({ to: "/" });
    } catch (err) {
      setError(err instanceof AuthError ? err.message : "Something went wrong. Please try again.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="mx-auto max-w-sm">
      <h1 className="mb-6 text-2xl font-semibold tracking-tight">Log in</h1>
      <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
        <label className="flex flex-col gap-1 text-sm font-medium">
          Email
          <input
            type="email"
            name="email"
            autoComplete="email"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="rounded-md border border-border bg-background px-3 py-2 text-sm"
          />
        </label>
        <label className="flex flex-col gap-1 text-sm font-medium">
          Password
          <input
            type="password"
            name="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="rounded-md border border-border bg-background px-3 py-2 text-sm"
          />
        </label>

        {error && (
          <p role="alert" className="text-sm text-red-600">
            {error}
          </p>
        )}

        <button
          type="submit"
          disabled={submitting}
          className="rounded-md bg-foreground px-4 py-2 text-sm font-medium text-background disabled:opacity-50"
        >
          {submitting ? "Logging in…" : "Log in"}
        </button>
      </form>

      <p className="mt-6 text-sm text-muted-foreground">
        No account?{" "}
        <Link to="/register" className="font-medium text-foreground underline">
          Create one
        </Link>
      </p>
    </div>
  );
}
