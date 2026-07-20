import { useState, type FormEvent } from "react";
import { Link } from "@tanstack/react-router";
import { useQuery } from "@tanstack/react-query";

import { useApiContext } from "@/api/client";
import { usePlaceOrder } from "@/orders/placeOrderMutation";

import { cartQueryOptions } from "./cartQueries";
import { useChangeCartItemQuantity, useRemoveCartItem } from "./cartMutations";
import { useValidateCoupon } from "./couponQueries";
import type { CartLine } from "./cartSchema";

// W2 — Cart Review (workshop § 5.1). The storefront's cart screen: it renders the customer's open cart on a
// cold load by binding the slice-3.5 read `GET /carts/mine` (identity carried by the `Authorization: Bearer`
// token the shared client sets from the useCurrentCustomer seam — frontend SKILL Convention 4, ADR 023), and
// lets the customer edit it in place. Each line carries a [-] N [+] quantity stepper (slice 3.3
// `ChangeCartItemQuantity`) and an [x] remove (slice 3.2 `RemoveCartItem`), each an OPTIMISTIC mutation
// (Convention 3): the row updates instantly, then reconciles against the refetched CartView. The header badge
// and the Total derive from the same cart query, so they update for free.
//
// Slice 6.2 adds the COUPON affordance: a coupon field fires the advisory `GET /coupons/{code}/validate`
// (CouponField, below), and an applied code previews a Subtotal / Discount / Total breakdown — the discount
// priced CLIENT-SIDE against the cart total (the server never sees cart money on that read). The applied code
// is UI-held (a reload forgets it — accepted round-one behavior) and rides checkout as the `?couponCode=`
// query param. The advisory preview is NEVER a guard: the checkout DCB append is the sole authority, so a
// previewed discount can still lose the race at [ Place Order ].
//
// [ Place Order ] (slice 4.1) issues PlaceOrder (with the applied coupon, if any) and navigates to W3
// confirmation — the one cart command where optimism STOPS (usePlaceOrder is non-optimistic).

// One $-formatter, built once at module load (cheaper than per-render, stable reference).
const usd = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });

// Money is summed in INTEGER CENTS, never floating-point dollars: `0.1 + 0.2 !== 0.3` in binary float, so a
// naive `Σ price × qty` can drift a penny on a long cart. Rounding each price to cents up front keeps the
// displayed Total exact (2 plush @ $24.99 + 3 newt @ $18.00 = $103.98, to the cent). Prices arrive as JSON
// numbers in dollars, so `toCents` is the one conversion.
function toCents(dollars: number): number {
  return Math.round(dollars * 100);
}

function formatCents(cents: number): string {
  return usd.format(cents / 100);
}

function lineSubtotalCents(line: CartLine): number {
  return toCents(line.price) * line.quantity;
}

// Shared classes for the small square stepper/remove buttons — the neutral-token idiom from BrowsePage's
// add-to-cart button (raw <button> + shadcn tokens; no shadcn primitive is installed). `disabled:` styles
// give the at-minimum / in-flight states a visible, non-interactive look. `touch-manipulation` drops the
// 300ms double-tap-zoom delay on these repeated-tap controls; `select-none` stops the −/× glyphs being
// text-selected mid-tap. 32px square clears a comfortable touch target (web-interface-guidelines review).
const iconButton =
  "inline-flex h-8 w-8 items-center justify-center rounded-md border border-border text-base leading-none touch-manipulation select-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-40 disabled:pointer-events-none";

// One editable cart row. Each row owns its OWN mutation hooks (mirroring BrowsePage's per-card ProductCard),
// so `isPending` is per-row — an in-flight edit disables only this row's controls, never freezing the table.
// Disabling the whole row while any edit is in flight also closes the rapid-double-click window where a second
// optimistic mutation could race the first's rollback. Both hooks target the shared cart key, so the badge and
// Total update regardless of which row fired.
function CartRow({ line }: { line: CartLine }) {
  const changeQuantity = useChangeCartItemQuantity();
  const removeItem = useRemoveCartItem();
  const isPending = changeQuantity.isPending || removeItem.isPending;
  const atMinimum = line.quantity <= 1;

  return (
    <tr className="border-b border-border">
      <td className="py-3 pr-4 font-medium">{line.name}</td>
      <td className="py-3 pr-4 font-mono text-muted-foreground">{line.sku}</td>
      <td className="py-3 pr-4 text-right tabular-nums">{formatCents(toCents(line.price))}</td>
      <td className="py-3 pr-4">
        <div
          className="flex items-center justify-end gap-2"
          role="group"
          aria-label={`Quantity for ${line.name}`}
        >
          <button
            type="button"
            // [-] is disabled at quantity 1: the backend rejects newQuantity <= 0 ("use remove for zero"),
            // so reaching empty is only ever the explicit [x] (locked decision 2 — one control = one command).
            onClick={() => changeQuantity.mutate({ sku: line.sku, newQuantity: line.quantity - 1 })}
            disabled={isPending || atMinimum}
            aria-label={`Decrease quantity of ${line.name}`}
            className={`${iconButton} hover:bg-accent hover:text-accent-foreground`}
          >
            −
          </button>
          {/* `aria-live` announces the new quantity to a screen reader when the optimistic update lands;
              `min-w-[2ch]` + `tabular-nums` reserve space so 1→2 digits never shifts the controls. */}
          <span className="min-w-[2ch] text-center tabular-nums" aria-live="polite">
            {line.quantity}
          </span>
          <button
            type="button"
            onClick={() => changeQuantity.mutate({ sku: line.sku, newQuantity: line.quantity + 1 })}
            disabled={isPending}
            aria-label={`Increase quantity of ${line.name}`}
            className={`${iconButton} hover:bg-accent hover:text-accent-foreground`}
          >
            +
          </button>
        </div>
      </td>
      <td className="py-3 pr-4 text-right tabular-nums">{formatCents(lineSubtotalCents(line))}</td>
      <td className="py-3 text-right">
        <button
          type="button"
          onClick={() => removeItem.mutate({ sku: line.sku })}
          disabled={isPending}
          aria-label={`Remove ${line.name} from cart`}
          className={`${iconButton} text-muted-foreground hover:border-destructive/30 hover:bg-destructive/10 hover:text-destructive`}
        >
          ×
        </button>
      </td>
    </tr>
  );
}

// The applied coupon, held in UI state (Workshop 003 §5.1 — a reload forgets it, accepted round-one behavior).
// Only the code + percent are held; the dollar discount is DERIVED from the live cart total, so it stays
// correct as the customer edits quantities after applying (no stale cached amount).
type AppliedCoupon = { code: string; discountPercent: number };

// W2 coupon field (slice 6.2). Before a coupon is applied it shows an input + Apply that fires the advisory
// validate query; a `valid` answer lifts the coupon to the parent (which prices + previews it), while
// `invalid` / `already_redeemed` (6.6) / `exhausted` render an inline error and hold nothing. Once applied it shows the granted code +
// percent with a Remove that clears it. The query is ADVISORY — it can be stale, and it never gates checkout;
// the parent still passes the code to [ Place Order ], where the DCB append is the sole authority.
function CouponField({
  applied,
  onApply,
  onRemove,
}: {
  applied: AppliedCoupon | null;
  onApply: (coupon: AppliedCoupon) => void;
  onRemove: () => void;
}) {
  const [code, setCode] = useState("");
  const validate = useValidateCoupon();

  // Applied state: show the granted coupon + Remove. No input while a coupon is active — remove first to swap.
  if (applied) {
    return (
      <div className="flex items-center justify-between gap-3 rounded-md border border-border bg-accent/40 px-3 py-2 text-sm">
        <span className="flex items-center gap-2">
          <span aria-hidden="true" className="text-emerald-600 dark:text-emerald-400">
            ✓
          </span>
          <span>
            <span className="font-medium">{applied.code}</span> — {applied.discountPercent}% off
          </span>
        </span>
        <button
          type="button"
          onClick={() => {
            onRemove();
            setCode("");
            validate.reset();
          }}
          className="text-muted-foreground underline underline-offset-4 hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          Remove
        </button>
      </div>
    );
  }

  // Not applied: the advisory failure answers become inline copy (Workshop 003 §5.1); a network failure is a
  // final, honest fallback. `validate.data` is the last check's result — it drives the error until re-applied.
  //
  // Slice 6.6 adds `already_redeemed` — the PERSONAL reason, which the server orders ahead of the crowd reason
  // `exhausted` so the copy points at the right remedy ("try another code", not "try again later"). It reaches
  // us only when signed in; a signed-out shopper sees the unchanged slice-6.2 answers.
  const result = validate.data;
  const errorMessage =
    result?.status === "invalid"
      ? "This code isn't valid."
      : result?.status === "already_redeemed"
        ? "You've already used this coupon."
        : result?.status === "exhausted"
          ? "This coupon is no longer available."
          : validate.isError
            ? "We couldn't check that code. Please try again."
            : null;

  const submit = (event: FormEvent) => {
    event.preventDefault();
    const trimmed = code.trim();
    if (!trimmed || validate.isPending) return;
    validate.mutate(trimmed, {
      onSuccess: (validation) => {
        // Only a `valid` answer holds; invalid/exhausted/already_redeemed fall through to the error above.
        if (validation.status === "valid" && validation.discountPercent != null) {
          onApply({ code: validation.code, discountPercent: validation.discountPercent });
        }
      },
    });
  };

  return (
    <form onSubmit={submit} className="space-y-2">
      <div className="flex items-end gap-2">
        <div className="flex-1 space-y-1">
          <label htmlFor="coupon-code" className="text-sm font-medium">
            Coupon code
          </label>
          <input
            id="coupon-code"
            type="text"
            value={code}
            onChange={(event) => setCode(event.target.value)}
            autoComplete="off"
            spellCheck={false}
            aria-invalid={errorMessage ? true : undefined}
            aria-describedby={errorMessage ? "coupon-error" : undefined}
            placeholder="e.g. FLASH20"
            className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          />
        </div>
        <button
          type="submit"
          disabled={!code.trim() || validate.isPending}
          className="rounded-md border border-border px-4 py-2 text-sm font-medium hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50 disabled:pointer-events-none"
        >
          {validate.isPending ? "Checking…" : "Apply"}
        </button>
      </div>
      {errorMessage && (
        <p id="coupon-error" role="alert" className="text-sm text-destructive">
          {errorMessage}
        </p>
      )}
    </form>
  );
}

export function CartPage() {
  const ctx = useApiContext();
  const { data: cart, isPending, isError, refetch } = useQuery(cartQueryOptions(ctx));
  // The applied coupon, UI-held (Workshop 003 §5.1). null until Apply succeeds; cleared by Remove. Rides
  // checkout as `?couponCode=`. Reset would happen on reload — accepted round-one behavior.
  const [appliedCoupon, setAppliedCoupon] = useState<AppliedCoupon | null>(null);
  // Non-optimistic checkout: on success it resets the cart and navigates to W3 (@/orders/placeOrderMutation).
  // Lives here only as the button's wiring — the [ Place Order ] control renders in the populated-cart branch.
  const placeOrder = usePlaceOrder();

  // Loading — the read is in flight. (No cached cart yet on a cold load.)
  if (isPending) {
    return (
      <section className="space-y-2" aria-busy="true">
        <h1 className="text-3xl font-semibold tracking-tight">Your cart</h1>
        <p role="status" className="text-muted-foreground">
          Loading your cart…
        </p>
      </section>
    );
  }

  // Error — a genuine failure, NOT the empty-cart case (that resolves to `null` data, below). A 400 means the
  // identity header was missing (the seam misfired — a bug, Convention 4); a 5xx means Orders is unreachable.
  if (isError) {
    return (
      <section className="space-y-3">
        <h1 className="text-3xl font-semibold tracking-tight">Your cart</h1>
        <p role="alert" className="text-muted-foreground">
          We couldn&apos;t load your cart. Please try again.
        </p>
        <button
          type="button"
          onClick={() => refetch()}
          className="rounded-md border border-border px-4 py-2 text-sm font-medium hover:bg-accent hover:text-accent-foreground"
        >
          Retry
        </button>
      </section>
    );
  }

  // Resolved. `cart` is `CartView | null` — `null` (no open cart) renders identically to a cart with no lines.
  const lines = cart?.lines ?? [];

  // Empty — a legitimate domain state: the customer has no open cart, or emptied every line. The cart stays
  // ready for the next add (Narrative 005 Moment 3); the one thing it can't do is check out.
  if (lines.length === 0) {
    return (
      <section className="space-y-3">
        <h1 className="text-3xl font-semibold tracking-tight">Your cart</h1>
        <p className="text-muted-foreground">Your cart is empty.</p>
        <Link to="/" className="text-sm underline underline-offset-4">
          Browse the storefront
        </Link>
      </section>
    );
  }

  const totalCents = lines.reduce((sum, line) => sum + lineSubtotalCents(line), 0);
  // Advisory discount preview (slice 6.2), priced CLIENT-SIDE: the applied percent against the live cart total,
  // in integer cents so the previewed Total stays penny-exact and consistent with the displayed line subtotals.
  // This mirrors the server's `Math.Round(subtotal × pct / 100, 2)` (PlaceOrder.RedeemWithDcbAsync) — but it is
  // only a preview against the CURRENT cart; the authoritative discount is priced server-side at checkout.
  const discountCents = appliedCoupon
    ? Math.round((totalCents * appliedCoupon.discountPercent) / 100)
    : 0;
  const payableCents = totalCents - discountCents;

  return (
    <section className="space-y-6">
      <h1 className="text-3xl font-semibold tracking-tight">Your cart</h1>

      <table className="w-full border-collapse text-sm">
        <caption className="sr-only">
          Items in your cart, with unit price, quantity, and subtotal. Use the minus and plus buttons to change
          a quantity, or the remove button to take an item out.
        </caption>
        <thead>
          <tr className="border-b border-border text-left text-muted-foreground">
            <th scope="col" className="py-2 pr-4 font-medium">
              Item
            </th>
            <th scope="col" className="py-2 pr-4 font-medium">
              SKU
            </th>
            <th scope="col" className="py-2 pr-4 text-right font-medium">
              Price
            </th>
            <th scope="col" className="py-2 pr-4 text-right font-medium">
              Qty
            </th>
            <th scope="col" className="py-2 pr-4 text-right font-medium">
              Subtotal
            </th>
            <th scope="col" className="py-2 text-right font-medium">
              <span className="sr-only">Actions</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {lines.map((line) => (
            <CartRow key={line.sku} line={line} />
          ))}
        </tbody>
      </table>

      {/* Coupon + order summary (slice 6.2 W2 delta). The coupon field previews an advisory discount; the
          totals below show a Subtotal / Discount (CODE) / Total breakdown when a coupon is applied, or the plain
          single Total otherwise — the discounted total is priced client-side from the applied percent. The
          summary replaced the table's old <tfoot> Total row so the coupon affordance and the totals read as one
          block; the per-line "Subtotal" column in the table is unrelated (that is each line's own subtotal). */}
      <div className="ml-auto w-full max-w-xs space-y-4">
        <CouponField applied={appliedCoupon} onApply={setAppliedCoupon} onRemove={() => setAppliedCoupon(null)} />

        <dl className="space-y-1 text-sm">
          {appliedCoupon ? (
            <>
              <div className="flex items-center justify-between gap-4">
                <dt className="text-muted-foreground">Subtotal</dt>
                <dd className="tabular-nums">{formatCents(totalCents)}</dd>
              </div>
              <div className="flex items-center justify-between gap-4">
                <dt className="text-muted-foreground">Discount ({appliedCoupon.code})</dt>
                <dd className="tabular-nums text-emerald-600 dark:text-emerald-400">
                  −{formatCents(discountCents)}
                </dd>
              </div>
              <div className="flex items-center justify-between gap-4 border-t border-border pt-1 text-base font-semibold">
                <dt>Total</dt>
                <dd className="tabular-nums">{formatCents(payableCents)}</dd>
              </div>
            </>
          ) : (
            <div className="flex items-center justify-between gap-4 text-base font-semibold">
              <dt>Total</dt>
              <dd className="tabular-nums">{formatCents(totalCents)}</dd>
            </div>
          )}
        </dl>
      </div>

      {/* [ Place Order ] (slice 4.1 → W3). NON-optimistic (Convention 3's exception): the click fires
          PlaceOrder, and only on the server's success does usePlaceOrder reset the cart + navigate to the
          confirmation screen — no instant guess. The button only renders here, in the populated-cart branch,
          so an empty cart (the early-return above) offers no checkout — the workshop's CartEmpty guard made
          unreachable from the UI. A 409 (NoOpenCart, e.g. a stale duplicate submit) surfaces as the honest
          alert below; the cart stays put (no navigate). */}
      <div className="flex flex-col items-end gap-2">
        <button
          type="button"
          // Slice 6.2: the applied coupon code (if any) rides checkout as `?couponCode=`; `appliedCoupon?.code`
          // is `undefined` when none is applied → the unchanged no-coupon POST /orders. The advisory preview
          // never guards — a previewed discount can still lose the DCB race and come back CouponExhausted.
          onClick={() => placeOrder.mutate(appliedCoupon?.code)}
          disabled={placeOrder.isPending}
          className="rounded-md bg-primary px-5 py-2.5 text-sm font-medium text-primary-foreground hover:bg-primary/90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50"
        >
          {placeOrder.isPending ? "Placing…" : "Place Order"}
        </button>
        {placeOrder.isError && (
          <p role="alert" className="text-sm text-muted-foreground">
            We couldn&apos;t place your order. Please try again.
          </p>
        )}
      </div>
    </section>
  );
}
