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

    /// <summary>
    /// Upload history table (all uploads to MES Cloud: success and failed)
    /// </summary>
    public DbSet<UploadHistory> UploadHistory => Set<UploadHistory>();

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

        modelBuilder.Entity<UploadHistory>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.MachineNumber)
                .HasMaxLength(50);

            entity.Property(e => e.TraceCode)
                .HasMaxLength(100);

            entity.Property(e => e.LotNo)
                .HasMaxLength(100);

            entity.Property(e => e.PartNumber)
                .HasMaxLength(100);

            entity.Property(e => e.ProcessName)
                .HasMaxLength(100);

            entity.Property(e => e.DeviceName)
                .HasMaxLength(100);

            entity.Property(e => e.ResponseMessage)
                .HasMaxLength(500);

            entity.Property(e => e.ErrorMessage)
                .HasMaxLength(2000);

            entity.Property(e => e.Source)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.InspectionDataJson)
                .HasMaxLength(100000); // 100KB for full JSON

            // Indexes for efficient queries
            entity.HasIndex(e => e.UploadedAt);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.MachineNumber);
            entity.HasIndex(e => e.TraceCode);
            entity.HasIndex(e => e.LotNo);
        });
    }
}
