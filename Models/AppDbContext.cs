namespace BenchmarkBindVarsAndHardcodesInOracle.Models;

using Microsoft.EntityFrameworkCore;

public partial class AppDbContext : DbContext
{
    public required string ConnectionString { get; init; }

    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<TestDataRow> TestData { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseOracle(ConnectionString);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasDefaultSchema("BENCHMARK")
            .UseCollation("USING_NLS_COMP");

        modelBuilder.Entity<TestDataRow>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_TEST_DATA");

            entity.ToTable("T_TEST_DATA");

            entity.Property(e => e.Id)
                .HasColumnType("NUMBER(38)")
                .HasColumnName("ID");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
