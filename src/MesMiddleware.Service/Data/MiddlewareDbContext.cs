using Microsoft.EntityFrameworkCore;
using MesMiddleware.Service.Models;

namespace MesMiddleware.Service.Data;

/// <summary>
/// Entity Framework Core database context for middleware service.
/// Uses SQLite for offline queue storage.
/// </summary>
public class MiddlewareDbContext : DbContext
{
    public MiddlewareDbContext(DbContextOptions<MiddlewareDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Queued uploads table (failed/pending WebAPI uploads)
    /// </summary>
    public DbSet<QueuedUpload> QueuedUploads => Set<QueuedUpload>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<QueuedUpload>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.InspectionDataJson)
                .IsRequired()
                .HasMaxLength(100000); // Support up to 100KB JSON

            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.MachineNumber)
                .HasMaxLength(50);

            entity.Property(e => e.TraceCodeOrLotNo)
                .HasMaxLength(100);

            entity.Property(e => e.LastError)
                .HasMaxLength(2000); // Capture detailed error messages

            // Index for efficient queries
            entity.HasIndex(e => e.QueuedAt);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.NextRetryAt);
            entity.HasIndex(e => e.MachineNumber);
        });
    }
}
