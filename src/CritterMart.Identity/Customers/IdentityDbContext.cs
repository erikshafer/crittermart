using Microsoft.EntityFrameworkCore;

namespace CritterMart.Identity.Customers;

// The EF Core unit of work for Identity. AddDbContextWithWolverineIntegration<IdentityDbContext> in
// Program.cs registers this, maps Wolverine's inbox/outbox envelope tables into it, and pins the
// options lifetime — so a handler's entity write and its outgoing messages commit in ONE transaction.
public class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Schema-per-service (ADR 002), the EF-Core mirror of the Marten services' DatabaseSchemaName:
        // every table this context owns — the `customers` table AND the Wolverine envelope tables the
        // integration maps in — lands in the `identity` schema, matching the schema named in
        // PersistMessagesWithPostgresql(conn, "identity"). HasDefaultSchema is set first so the
        // envelope tables inherit it rather than leaking into `public`.
        modelBuilder.HasDefaultSchema("identity");

        // Explicit lowercase/snake_case column names. UseEntityFrameworkCoreWolverineManagedMigrations
        // drives Weasel, which emits the table DDL with UNQUOTED identifiers — so Postgres folds them to
        // lowercase (`Id` → `id`). EF Core's runtime, however, always QUOTES identifiers (`"Id"`), which
        // is case-sensitive and would miss the folded column. Naming the columns lowercase here makes the
        // two agree (EF emits `"id"`, Weasel creates `id`). The Marten services dodge this because Marten
        // owns both the DDL and the queries; the moment two tools share a table, casing must be reconciled.
        modelBuilder.Entity<Customer>(e =>
        {
            e.ToTable("customers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasMaxLength(100);
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(256).IsRequired();
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.RegisteredAt).HasColumnName("registered_at");

            // Email is the registry's natural key — a customer is unique by (normalized) email, NOT by the
            // server-minted opaque id. RegisterCustomer.ValidateAsync returns the friendly 409, but this
            // unique index is the TRUE backstop: it closes the check-then-insert race the app-level check
            // can't (two concurrent registrations both passing the check before either commits). The stored
            // email is already trimmed + lowercased, so the index enforces case-insensitive uniqueness.
            // Catalog needs no analogue — a product's SKU IS its Marten document id, so the PK enforces it free.
            e.HasIndex(x => x.Email).IsUnique();
        });
    }
}
