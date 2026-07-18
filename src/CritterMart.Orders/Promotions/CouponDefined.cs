namespace CritterMart.Orders.Promotions;

// Genesis event of a coupon stream (Workshop 003 slice 6.1). A coupon came into existence with a
// code, a percentage discount, and a global redemption cap N. Keyed by a generated couponId (the
// stream key); the human-facing `Code` is what checkout resolves against (CouponView).
//
// This is CritterMart's first CONFIGURATION-AS-EVENTS (Bruun) use: the cap N is an event-sourced
// domain fact with an audit trail, not a config row. Issued by the seeder this round via POST /coupons;
// the identical contract is what a future standalone Promotions service would publish as Published
// Language — only the transport (a broker message vs. a local seed) would change, not the shape.
//
// Slice 6.5 (Workshop 003 §8 item 6) extends the configuration-as-events policy with a second dimension:
// `OneRedemptionPerCustomer`. When true, checkout enforces a SECOND DCB — the composite (coupon × customer)
// boundary — so a given customer may redeem this coupon at most once (ADR 024 §38's "more dynamic boundary").
// DEFAULTED to false so every already-persisted CouponDefined (which has no such property in its JSON) folds
// as a pure global-cap coupon — a non-breaking event evolution, the payoff of policy-as-event.
public record CouponDefined(
    string CouponId, string Code, int DiscountPercent, int Cap, bool OneRedemptionPerCustomer = false);
