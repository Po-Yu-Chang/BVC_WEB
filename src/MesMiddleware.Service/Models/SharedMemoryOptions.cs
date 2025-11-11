namespace MesMiddleware.Service.Models;

/// <summary>
/// Configuration options for shared memory segments and event synchronization.
/// Bound from appsettings.json "SharedMemory" section.
/// </summary>
public class SharedMemoryOptions
{
    public const string SectionName = "SharedMemory";

    /// <summary>
    /// Name of shared memory segment for inspection data (equipment → middleware)
    /// </summary>
    public string InspectionDataSegmentName { get; set; } = "MES_INSPECTION_DATA";

    /// <summary>
    /// Name of shared memory segment for equipment commands (middleware → equipment)
    /// </summary>
    public string EquipmentCommandSegmentName { get; set; } = "MES_EQUIPMENT_CMD";

    /// <summary>
    /// Name of event wait handle for data ready signal
    /// </summary>
    public string DataReadyEventName { get; set; } = "MES_DATA_READY";

    /// <summary>
    /// Name of event wait handle for command ready signal
    /// </summary>
    public string CommandReadyEventName { get; set; } = "MES_CMD_READY";

    /// <summary>
    /// Size of shared memory segment in bytes (default 10MB)
    /// </summary>
    public long SegmentSize { get; set; } = 10 * 1024 * 1024; // 10MB

    /// <summary>
    /// Mutex timeout in milliseconds (default 5 seconds)
    /// </summary>
    public int MutexTimeout { get; set; } = 5000;
}
