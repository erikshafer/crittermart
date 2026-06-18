using CritterMart.Contracts;
using Marten;

namespace CritterMart.Orders.Customers;

// Subscribes to the CustomerRegistered Published-Language event arriving from Identity over
// RabbitMQ. Upserts a LocalCustomerView document in Orders' own Marten store so that the
// order endpoints can resolve a customer's display name without a synchronous call into the
// Identity service (ADR 001 forbids sync service-to-service HTTP).
//
// This is a plain Wolverine message handler — NOT a Marten event subscription or async-daemon
// projection. CustomerRegistered arrives as a Wolverine message (from the RabbitMQ exchange
// conventional-routing wires up), and AutoApplyTransactions commits session.Store() in one
// transaction after the handler returns. session.Store() is an upsert by Marten's document-id
// convention, so at-least-once redelivery is idempotent.
public static class CustomerRegisteredHandler
{
    public static void Handle(CustomerRegistered message, IDocumentSession session)
    {
        session.Store(new LocalCustomerView(message.CustomerId, message.DisplayName));
    }
}
