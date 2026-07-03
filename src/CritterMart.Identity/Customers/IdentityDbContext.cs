using Microsoft.EntityFrameworkCore;

namespace CritterMart.Identity.Customers;

// The EF Core unit of work for Identity. AddDbContextWithWolverineIntegration<IdentityDbContext> in
// Program.cs registers this, maps Wolverine's inbox/outbox envelope tables into it, and pins the
// options lifetime — so a handler's entity write and its outgoing messages commit in ONE transaction.
public class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<EmailChange> EmailChanges => Set<EmailChange>();

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

            // NOTE: the unique index on `email` (the registry's natural key — a customer is unique by
            // normalized email, NOT by the server-minted opaque id) is deliberately NOT declared here as
            // `e.HasIndex(...).IsUnique()`. Wolverine's UseEntityFrameworkCoreWolverineManagedMigrations
            // drives Weasel, which migrates tables, columns, primary keys, and foreign keys from the EF
            // model — but NOT secondary indexes (verified against the Wolverine EF-Core migration docs and
            // a live schema check). An EF `HasIndex` here would be silently dropped, giving a false sense of
            // a backstop that isn't in the database. The index is therefore applied as idempotent startup
            // DDL in Program.cs (see EnsureEmailUniqueIndex) — the single place it actually takes effect.
        });

        // EmailChange (Workshop 002 slices 5.5-5.7) — CritterMart's second convention Wolverine.Saga,
        // EF-Core-backed. Same lowercase-column discipline as Customer above. Keyed by its own PK
        // (CustomerId) — no secondary index needed, so the Weasel-skips-secondary-indexes gotcha above
        // does not apply here.
        //
        // Version — the base Wolverine.Saga class unconditionally declares `public int Version { get; set; }`
        // (reflection-confirmed; it backs IRevisioned-based optimistic concurrency even for sagas that don't
        // opt into IRevisioned). EF Core's default convention picks it up regardless, and Wolverine's own
        // generated saga-loading query references it — omitting this mapping produced a live
        // "column e.Version does not exist" failure (Postgres has `version`, EF queried `"Version"`), the
        // same casing mismatch Id/Email/etc. already guard against, just for an inherited property that's
        // easy to miss. Map it explicitly rather than ignore it, since Wolverine's own query depends on it.
        modelBuilder.Entity<EmailChange>(e =>
        {
            e.ToTable("email_changes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasMaxLength(100);
            e.Property(x => x.PendingEmail).HasColumnName("pending_email").HasMaxLength(256).IsRequired();
            e.Property(x => x.Version).HasColumnName("version");
        });
    }
}
