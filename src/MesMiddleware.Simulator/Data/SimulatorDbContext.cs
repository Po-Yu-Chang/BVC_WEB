using Microsoft.EntityFrameworkCore;
using MesMiddleware.Simulator.Models;

namespace MesMiddleware.Simulator.Data;

/// <summary>
/// MES 模擬器數據庫上下文
/// </summary>
public class SimulatorDbContext : DbContext
{
    public SimulatorDbContext(DbContextOptions<SimulatorDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// HTTP 請求日誌 (保存所有通訊資料)
    /// </summary>
    public DbSet<RequestLog> RequestLogs { get; set; }

    /// <summary>
    /// 設備登錄記錄
    /// </summary>
    public DbSet<DeviceLogin> DeviceLogins { get; set; }

    /// <summary>
    /// 追溯數據記錄
    /// </summary>
    public DbSet<TraceDataRecord> TraceDataRecords { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // RequestLog 配置
        modelBuilder.Entity<RequestLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.Method).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Endpoint).HasMaxLength(500).IsRequired();
            entity.Property(e => e.ClientIp).HasMaxLength(50);
            entity.HasIndex(e => e.Timestamp);
        });

        // DeviceLogin 配置
        modelBuilder.Entity<DeviceLogin>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PrtMacNo).HasMaxLength(50).IsRequired();
            entity.Property(e => e.IpAddr).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Token).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.PrtMacNo);
            entity.HasIndex(e => e.Token);
        });

        // TraceDataRecord 配置
        modelBuilder.Entity<TraceDataRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TraceCode).HasMaxLength(100);
            entity.Property(e => e.LotNo).HasMaxLength(100);
            entity.Property(e => e.ProcName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.DevName).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.UploadTime);
            entity.HasIndex(e => e.TraceCode);
            entity.HasIndex(e => e.LotNo);
        });
    }
}
