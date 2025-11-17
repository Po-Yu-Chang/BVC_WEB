using System;
using System.Runtime.InteropServices;

namespace MesMiddleware.LabViewBridge.Models
{
    /// <summary>
    /// Represents additional metadata in an inspection record.
    /// </summary>
    [ComVisible(true)]
    [Serializable]
    public class OtherDataItem
    {
        /// <summary>
        /// Metadata key (e.g., "Temperature", "Humidity", "Machine_ID").
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Metadata value (e.g., "25.5", "60%", "AOI-001").
        /// </summary>
        public string Value { get; set; }

        public OtherDataItem()
        {
            Key = string.Empty;
            Value = string.Empty;
        }
    }
}
