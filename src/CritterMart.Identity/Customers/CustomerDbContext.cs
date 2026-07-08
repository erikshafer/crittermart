using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CritterMart.Identity.Customers;

// The EF Core unit of work for Identity. AddDbContextWithWolverineIntegration<CustomerDbContext> in
// Program.cs registers this, maps Wolverine's inbox/outbox envelope tables into it, and pins the
// options lifetime — so a handler's entity write and its outgoing messages commit in ONE transaction.
//
// RENAMED from IdentityDbContext + now derives Microsoft.AspNetCore.Identity.EntityFrameworkCore's
// IdentityUserContext<IdentityUser> (ADR 023 / slice 5.8). Two reasons rolled into one change:
//   1. Real auth (ADR 023) makes Identity an ASP.NET Core Identity user store — the AspNetUsers table and
//      its satellites (user claims/logins/tokens) come from the ASP.NET base context. UserManager writes
//      through THIS context, so the Identity user, the Customer row, and the outbox all share ONE
//      transaction (the all-or-nothing register-with-credentials the spec requires).
//   2. The old name `IdentityDbContext` collided with the ASP.NET base class of the same simple name
//      (the handoff's load-bearing gotcha). Renaming to CustomerDbContext sidesteps the collision entirely.
// IdentityUserContext (user tables only) is chosen over IdentityDbContext (which adds AspNetRoles /
// AspNetUserRoles / AspNetRoleClaims): authorization/roles are out of scope this increment (Workshop 002
// § 8 item 16), so no unused role tables. The Customer row (the registry / CustomerRegistered contract
// shape) stays a plain entity, deliberately free of the framework base class — two id-linked tables, one
// context (the session's entity-shape decision), keyed by the same string id (Customer.Id == user.Id).
public class CustomerDbContext(DbContextOptions<CustomerDbContext> options)
    : IdentityUserContext<IdentityUser>(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<EmailChange> EmailChanges => Set<EmailChange>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // MUST call base FIRST so IdentityUserContext configures AspNetUsers + its satellite entities
        // before we layer the registry tables and the schema default on top.
        base.OnModelCreating(modelBuilder);

        // Schema-per-service (ADR 002), the EF-Core mirror of the Marten services' DatabaseSchemaName:
        // every table this context owns — the `customers`/`email_changes` tables, the ASP.NET Identity
        // user tables, AND the Wolverine envelope tables the integration maps in — lands in the `identity`
        // schema, matching the schema named in PersistMessagesWithPostgresql(conn, "identity"). Set as the
        // model default so the Identity tables (which set no explicit schema) inherit it rather than
        // leaking into `public`.
        modelBuilder.HasDefaultSchema("identity");

        // Lowercase the ASP.NET Identity tables' names AND columns to match Weasel's DDL. This is the SAME
        // casing reconciliation the Customer / EmailChange mappings below do by hand, but the framework owns
        // ~20 columns across AspNetUsers + its satellites (user claims/logins/tokens) with default PascalCase
        // names — too many to hand-map. UseEntityFrameworkCoreWolverineManagedMigrations drives Weasel, which
        // emits table DDL with UNQUOTED identifiers that Postgres folds to lowercase (`Id` → `id`); EF Core's
        // runtime always QUOTES (`"Id"`), which is case-sensitive and misses the folded column — a live
        // "column a.Id does not exist / Perhaps you meant a.id" failure on the very first UserManager query.
        // A scoped lowercase convention over just the Microsoft.AspNetCore.Identity entity types fixes it and
        // leaves the Wolverine envelope entities (a different namespace) and the CritterMart-owned tables
        // (mapped explicitly below) untouched.
        //
        // Weasel also skips SECONDARY indexes (the documented gotcha behind ux_customers_email below), so
        // ASP.NET Identity's unique indexes on the normalized username/email are absent — acceptable: email
        // uniqueness is already DB-enforced by ux_customers_email on `customers` (+ the app guard), and
        // UserManager.FindByEmailAsync matches on the normalized-email COLUMN (which Weasel does create) — an
        // unindexed scan, fine at demo scale.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType.Namespace != "Microsoft.AspNetCore.Identity")
            {
                continue;
            }

            var table = entityType.GetTableName();
            if (table is not null)
            {
                entityType.SetTableName(table.ToLowerInvariant());
            }

            foreach (var property in entityType.GetProperties())
            {
                var column = property.GetColumnName();
                if (column is not null)
                {
                    property.SetColumnName(column.ToLowerInvariant());
                }
            }
        }

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
