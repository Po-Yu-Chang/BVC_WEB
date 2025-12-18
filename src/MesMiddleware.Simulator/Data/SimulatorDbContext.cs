using Microsoft.EntityFrameworkCore;
using MesMiddleware.Simulator.Models;

namespace MesMiddleware.Simulator.Data;

/// <summary>
/// Simulator 數據庫上下文 - 用於記錄模擬器的請求日誌、設備登錄和追溯數據
/// </summary>
public class SimulatorDbContext : DbContext
{
    public SimulatorDbContext(DbContextOptions<SimulatorDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// HTTP 請求日誌
    /// </summary>
    public DbSet<RequestLog> RequestLogs { get; set; } = null!;

    /// <summary>
    /// 設備登錄記錄
    /// </summary>
    public DbSet<DeviceLogin> DeviceLogins { get; set; } = null!;

    /// <summary>
    /// 追溯數據記錄
    /// </summary>
    public DbSet<TraceDataRecord> TraceDataRecords { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // RequestLog 配置
        modelBuilder.Entity<RequestLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Method).HasMaxLength(10);
            entity.Property(e => e.Endpoint).HasMaxLength(500);
            entity.Property(e => e.ClientIp).HasMaxLength(50);
        });

        // DeviceLogin 配置
        modelBuilder.Entity<DeviceLogin>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PrtMacNo).HasMaxLength(100);
            entity.Property(e => e.IpAddr).HasMaxLength(50);
            entity.Property(e => e.Token).HasMaxLength(100);
            entity.HasIndex(e => e.Token);
            entity.HasIndex(e => new { e.PrtMacNo, e.IsActive });
        });

        // TraceDataRecord 配置
        modelBuilder.Entity<TraceDataRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TraceCode).HasMaxLength(100);
            entity.Property(e => e.LotNo).HasMaxLength(100);
            entity.Property(e => e.ProcName).HasMaxLength(100);
            entity.Property(e => e.DevName).HasMaxLength(100);
            entity.Property(e => e.UserName).HasMaxLength(100);
            entity.Property(e => e.PartNumber).HasMaxLength(100);
        });
    }
}
