using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Contoso.Api.Models
{
    public class ContosoDbContext : DbContext
    {
        public ContosoDbContext(DbContextOptions<ContosoDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }

        public DbSet<Product> Products { get; set; }
        
        public DbSet<OrderItem> OrderItems { get; set; }

        public DbSet<Order> Orders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Store enums as strings
            modelBuilder.Entity<Order>()
                .Property(o => o.Status)
                .HasConversion<string>();

            // Map entities to Cosmos containers and set sensible partition keys.
            // Partition keys chosen to match requested access patterns:
            // - Users partitioned by `Email`
            // - Products partitioned by `Category` (fallback handled at runtime if null)
            // - Orders partitioned by `Id`
            // - OrderItems partitioned by `OrderId`

            modelBuilder.Entity<User>()
                .ToContainer("Users")
                .HasPartitionKey(u => u.Email)
                .HasNoDiscriminator();

            modelBuilder.Entity<Product>()
                .ToContainer("Products")
                .HasPartitionKey(p => p.Category)
                .HasNoDiscriminator();

            modelBuilder.Entity<Order>()
                .ToContainer("Orders")
                .HasPartitionKey(o => o.Id)
                .HasNoDiscriminator();

            modelBuilder.Entity<OrderItem>()
                .ToContainer("OrderItems")
                .HasPartitionKey(oi => oi.OrderId)
                .HasNoDiscriminator();

            // Note: EF Core Cosmos provider may create a shadow property (commonly named "__id")
            // to map CLR primary keys to the Cosmos document 'id' (string). Do NOT add an
            // unconditional shadow property named 'id' here — doing so can conflict with the
            // provider's internal mapping and cause duplicate 'id' mapping errors.
        }

        public override int SaveChanges()
        {
            EnsureCosmosIds();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            EnsureCosmosIds();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void EnsureCosmosIds()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added);

            foreach (var entry in entries)
            {
                // The Cosmos EF provider may use an internal shadow property like '__id'.
                // Check for commonly used shadow names and set whichever exists. If neither
                // shadow exists but the entity has a single string key property, set that.
                PropertyEntry? shadowProp = null;

                // Prefer provider-created '__id' shadow (if present)
                try { shadowProp = entry.Property("__id"); } catch { shadowProp = null; }

                // Fallback to explicit 'id' shadow (if present)
                if (shadowProp == null)
                {
                    try { shadowProp = entry.Property("id"); } catch { shadowProp = null; }
                }

                if (shadowProp != null)
                {
                    var cur = shadowProp.CurrentValue?.ToString();
                    if (string.IsNullOrEmpty(cur))
                    {
                        shadowProp.CurrentValue = Guid.NewGuid().ToString();
                    }

                    continue;
                }

                // If no shadow property exists, attempt to find a single string key property
                var key = entry.Metadata.FindPrimaryKey();
                if (key != null && key.Properties.Count == 1)
                {
                    var pk = key.Properties[0];
                    if (pk.ClrType == typeof(string))
                    {
                        var pkProp = entry.Property(pk.Name);
                        if (pkProp != null && (pkProp.CurrentValue == null || string.IsNullOrEmpty(pkProp.CurrentValue?.ToString())))
                        {
                            pkProp.CurrentValue = Guid.NewGuid().ToString();
                        }
                    }
                }
            }
        }
    }
}