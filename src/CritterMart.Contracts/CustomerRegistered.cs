namespace CritterMart.Contracts;

// Published-Language integration event: a customer was added to the Identity registry.
// Carries { CustomerId, Email, DisplayName } — the stable contract both Identity (publisher)
// and any consuming BC (e.g. Orders) reference. It lives in Contracts from the moment a
// consumer exists; before that it was Identity-internal (Workshop 002 § 4, slice 5.4).
public record CustomerRegistered(string CustomerId, string Email, string DisplayName);
