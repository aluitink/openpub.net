using ActivityPub.Core.Migration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ActivityPub.Tests.Migration;

/// <summary>
/// Unit tests for <see cref="DatabaseMigrationService.OrderEntityTypes"/> — the
/// static helper that topologically orders a model's entity types so that
/// dependencies (FK principals) come before their dependents, ensuring foreign
/// keys resolve during the migration's bulk insert. This previously had no
/// direct unit test.
///
/// The test model encodes a chain (Child -> Middle -> Root) plus a diamond
/// (Child depends on both Root and Middle) and an independent entity
/// (Standalone). The ordering must place Root before Middle before Child, keep
/// every principal before its dependent, include each entity exactly once, and
/// never recurse infinitely on the diamond.
/// </summary>
public class OrderEntityTypesTests
{
    public class Root
    {
        public int Id { get; set; }
    }

    public class Middle
    {
        public int Id { get; set; }
        public int RootId { get; set; }
        public Root? Root { get; set; }
    }

    public class Child
    {
        public int Id { get; set; }
        public int RootId { get; set; }
        public int MiddleId { get; set; }
        public Root? Root { get; set; }
        public Middle? Middle { get; set; }
    }

    public class Standalone
    {
        public int Id { get; set; }
    }

    public class TestDbContext : DbContext
    {
        public DbSet<Root> Roots { get; set; } = null!;
        public DbSet<Middle> Middles { get; set; } = null!;
        public DbSet<Child> Children { get; set; } = null!;
        public DbSet<Standalone> Standalones { get; set; } = null!;

        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Root>().HasKey(r => r.Id);

            modelBuilder.Entity<Middle>()
                .HasOne(m => m.Root)
                .WithMany()
                .HasForeignKey(m => m.RootId);

            modelBuilder.Entity<Child>()
                .HasOne(c => c.Root)
                .WithMany()
                .HasForeignKey(c => c.RootId);

            modelBuilder.Entity<Child>()
                .HasOne(c => c.Middle)
                .WithMany()
                .HasForeignKey(c => c.MiddleId);

            modelBuilder.Entity<Standalone>().HasKey(s => s.Id);
        }
    }

    private static List<string> Order()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new TestDbContext(options);
        var ordered = DatabaseMigrationService.OrderEntityTypes(context.Model);
        return ordered.Select(et => et.ShortName()).ToList();
    }

    [Fact]
    public void OrderEntityTypes_PlacesDependenciesBeforeDependents()
    {
        var order = Order();

        var root = order.IndexOf("Root");
        var middle = order.IndexOf("Middle");
        var child = order.IndexOf("Child");

        // The chain must be respected: Root before Middle before Child.
        Assert.True(root < middle); // Root must precede Middle
        Assert.True(middle < child); // Middle must precede Child
        Assert.True(root < child); // Root must precede Child (diamond)
    }

    [Fact]
    public void OrderEntityTypes_IncludesEveryEntityTypeExactlyOnce()
    {
        var order = Order();

        Assert.Equal(4, order.Count); // every entity type included exactly once
        Assert.Equal(4, order.Distinct().Count()); // no entity type duplicated
        Assert.Contains("Root", order);
        Assert.Contains("Middle", order);
        Assert.Contains("Child", order);
        Assert.Contains("Standalone", order);
    }

    [Fact]
    public void OrderEntityTypes_TerminatesOnDiamondDependency()
    {
        // A diamond (Child -> Root and Child -> Middle -> Root) must not cause
        // infinite recursion; the visit is guarded by the visited set.
        var order = Order();

        Assert.True(order.Count <= 4); // the diamond must not duplicate entities or loop
        Assert.Contains("Root", order);
        Assert.Contains("Child", order);
    }
}
