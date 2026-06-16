namespace CritterMart.Orders.Shopping;

// The product fields the frontend composed from the Catalog at render time and carried
// into AddToCart. The cart never reads the Catalog — this snapshot is authoritative
// through checkout (context map: presentation-layer composition, not a BC integration).
// (Workshop 001 § 4; Narrative 004 Moment 1.)
public record ProductSnapshot(string Name, decimal Price);
