using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MesMiddleware.LabViewBridge.Models
{
    /// <summary>
    /// Complete inspection record matching WebAPI InspectionDataRequest schema.
    /// Represents equipment inspection data to be uploaded to MES system.
    /// </summary>
    [ComVisible(true)]
    [Serializable]
    public class InspectionRecord
    {
        /// <summary>
        /// Row number (sequence ID) for this inspection record.
        /// </summary>
        public string RowNo { get; set; }

        /// <summary>
        /// Process name (e.g., "Blind Hole Inspection", "AOI", "AVI").
        /// </summary>
        public string ProcName { get; set; }

        /// <summary>
        /// Device/Equipment name that performed the inspection.
        /// </summary>
        public string DevName { get; set; }

        /// <summary>
        /// Operator username who initiated or supervised the inspection.
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Work class/shift (e.g., "Day", "Night", "Morning").
        /// </summary>
        public string WorkClass { get; set; }

        /// <summary>
        /// Trace code for individual product tracking (required if LotNo is null).
        /// </summary>
        public string TraceCode { get; set; }

        /// <summary>
        /// Lot number for batch tracking (required if TraceCode is null).
        /// </summary>
        public string LotNo { get; set; }

        /// <summary>
        /// Measured parameter data (measurements, values, results).
        /// </summary>
        public List<ParamDataItem> ParamData { get; set; }

        /// <summary>
        /// Benchmark/specification limits (upper/lower limits, thresholds).
        /// </summary>
        public List<BenchmarkItem> Benchmarks { get; set; }

        /// <summary>
        /// Other metadata (timestamps, environmental conditions, equipment settings).
        /// </summary>
        public List<OtherDataItem> OtherData { get; set; }

        /// <summary>
        /// Inspection timestamp (when the inspection was performed).
        /// </summary>
        public DateTime InspectionTime { get; set; }

        public InspectionRecord()
        {
            RowNo = string.Empty;
            ProcName = string.Empty;
            DevName = string.Empty;
            UserName = string.Empty;
            WorkClass = string.Empty;
            TraceCode = null;
            LotNo = null;
            ParamData = new List<ParamDataItem>();
            Benchmarks = new List<BenchmarkItem>();
            OtherData = new List<OtherDataItem>();
            InspectionTime = DateTime.UtcNow;
        }
    }
}
