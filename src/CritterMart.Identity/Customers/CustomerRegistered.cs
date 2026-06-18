namespace CritterMart.Identity.Customers;

// An Identity-LOCAL domain event — deliberately NOT a CritterMart.Contracts published-language type,
// because nothing cross-BC consumes it yet. RegisterCustomer cascades it; with no handler anywhere,
// conventional RabbitMQ routing publishes it to its own exchange UNCONSUMED. That is the spike's
// transactional-outbox demonstration: the message is enrolled in the EF Core outbox inside the same
// transaction as the customer insert, and published only after the commit succeeds.
//
// If a future slice ever consumes it (e.g. Orders folding the customer's name into OrderStatusView),
// it graduates to Contracts and Identity becomes a KEPT bounded context — which per the handoff needs
// a context-map update + an Event Modeling workshop pass BEFORE that coupling lands.
public record CustomerRegistered(string CustomerId, string Email, string DisplayName);
