namespace CritterMart.Orders.Customers;

// The consumer-local customer read model (Workshop 002 slice 5.4). A plain Marten document
// in the Orders service's own store — NOT shared with Identity, NOT obtained via a synchronous
// call into Identity (ADR 001 forbids sync service-to-service HTTP). It is populated by
// CustomerRegisteredHandler when a CustomerRegistered Published-Language event arrives from
// RabbitMQ, and is keyed by the customer's id (same value as the X-Customer-Id header).
//
// Intentionally minimal: Orders only needs DisplayName for the read-time enrichment in
// GET /orders/{orderId} and GET /orders/mine. Email and registeredAt stay in Identity.
//
// Eventually consistent: the document may not exist yet for a customer whose first order
// arrived before their CustomerRegistered message was delivered — callers degrade gracefully
// to null (never throw, never call Identity).
public record LocalCustomerView(string Id, string DisplayName);
