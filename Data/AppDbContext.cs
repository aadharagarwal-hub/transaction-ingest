using Microsoft.EntityFrameworkCore;
using TransactionIngest.Models;

namespace TransactionIngest.Data;

public class AppDbContext : DbContext
{
    public DbSet<TransactionRecord> Transactions => Set<TransactionRecord>();
    public DbSet<TransactionAudit> TransactionAudits => Set<TransactionAudit>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TransactionRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TransactionId).IsUnique();
            entity.Property(x => x.LocationCode).HasMaxLength(20);
            entity.Property(x => x.ProductName).HasMaxLength(20);
            entity.Property(x => x.CardLast4).HasMaxLength(4);
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Status).HasMaxLength(20);
        });

        modelBuilder.Entity<TransactionAudit>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ChangeType).HasMaxLength(20);
            entity.Property(x => x.FieldName).HasMaxLength(50);

            entity.HasOne(x => x.TransactionRecord)
                .WithMany(x => x.Audits)
                .HasForeignKey(x => x.TransactionRecordId);
        });
    }
}