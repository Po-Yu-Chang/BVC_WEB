using Microsoft.EntityFrameworkCore;
using MesMiddleware.Monitor.Models;

namespace MesMiddleware.Monitor.Data;

/// <summary>
/// Monitor 資料庫上下文 - SQLite 資料庫
/// </summary>
public class MonitorDbContext : DbContext
{
    public MonitorDbContext(DbContextOptions<MonitorDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// 上傳佇列 - 儲存失敗的上傳
    /// </summary>
    public DbSet<UploadQueueItem> UploadQueue { get; set; }

    /// <summary>
    /// 上傳歷史 - 記錄所有上傳嘗試
    /// </summary>
    public DbSet<UploadHistory> UploadHistory { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // UploadQueueItem 配置
        modelBuilder.Entity<UploadQueueItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RowNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TraceCode).HasMaxLength(100);
            entity.Property(e => e.LotNo).HasMaxLength(100);
            entity.Property(e => e.ProcName).HasMaxLength(100);
            entity.Property(e => e.DevName).HasMaxLength(100);
            entity.Property(e => e.UserName).HasMaxLength(100);
            entity.Property(e => e.WorkClass).HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.LastError).HasMaxLength(500);

            // 索引
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.NextRetryAt);
            entity.HasIndex(e => e.CreatedAt);
        });

        // UploadHistory 配置
        modelBuilder.Entity<UploadHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RowNo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TraceCode).HasMaxLength(100);
            entity.Property(e => e.LotNo).HasMaxLength(100);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Source).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ErrorMessage).HasMaxLength(500);

            // 索引
            entity.HasIndex(e => e.UploadedAt);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.QueueItemId);
        });
    }
}
