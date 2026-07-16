namespace CritterMart.Orders.Promotions;

// The strong-typed DCB tag for a coupon (Workshop 003 § 4; ADR 024). Marten DCB tags are simple
// wrapper records around a primitive (string is supported). Every CouponRedeemed / CouponRedemptionReleased
// is tagged with `new CouponId(couponId)` via IEvent.WithTag; the cap boundary is opened with
// `new EventTagQuery().Or<CouponId>(new CouponId(couponId))`, and Marten aggregates the net redemption count
// across EVERY order stream carrying that tag. Registered in Program.cs:
//   opts.Events.RegisterTagType<CouponId>("coupon").ForAggregate<CouponUsage>()
//
// NOT the Polecat-flavored `[BoundaryModel]` / `EventTagQuery.For(…)` surface the installed
// marten-advanced-dynamic-consistency-boundary skill shows (docs/skills/DEBT.md row 3) — this is the
// Marten 9.15.1 path ADR 024 verified.
public record CouponId(string Value);
