using System;
using System.Runtime.InteropServices;

namespace MesMiddleware.LabViewBridge.Models
{
    /// <summary>
    /// Represents a benchmark/specification limit for quality control.
    /// </summary>
    [ComVisible(true)]
    [Serializable]
    public class BenchmarkItem
    {
        /// <summary>
        /// Benchmark parameter name (e.g., "Diameter_USL", "Thickness_LSL").
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Benchmark value (e.g., "12.0", "0.5").
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Unit of measurement (should match ParamDataItem unit).
        /// </summary>
        public string Unit { get; set; }

        public BenchmarkItem()
        {
            Name = string.Empty;
            Value = string.Empty;
            Unit = string.Empty;
        }
    }
}
