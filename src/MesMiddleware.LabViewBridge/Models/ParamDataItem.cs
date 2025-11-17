using System;
using System.Runtime.InteropServices;

namespace MesMiddleware.LabViewBridge.Models
{
    /// <summary>
    /// Represents a single parameter measurement in an inspection record.
    /// </summary>
    [ComVisible(true)]
    [Serializable]
    public class ParamDataItem
    {
        /// <summary>
        /// Parameter name (e.g., "Diameter_X", "Height", "Defect_Count").
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Measured value as string (e.g., "10.5", "Pass", "3").
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Unit of measurement (e.g., "mm", "μm", "count", "").
        /// </summary>
        public string Unit { get; set; }

        /// <summary>
        /// Pass/Fail status (e.g., "Pass", "Fail", "Warning", "OK").
        /// </summary>
        public string Status { get; set; }

        public ParamDataItem()
        {
            Name = string.Empty;
            Value = string.Empty;
            Unit = string.Empty;
            Status = string.Empty;
        }
    }
}
