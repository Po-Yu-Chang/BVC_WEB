using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MesMiddleware.LabViewBridge.Models
{
    /// <summary>
    /// Represents a command sent from WebAPI/middleware to equipment via shared memory.
    /// Part of User Story 3 (Bidirectional Command & Control).
    /// </summary>
    [ComVisible(true)]
    [Serializable]
    public class EquipmentCommand
    {
        /// <summary>
        /// Unique identifier for this command (used to track acknowledgment).
        /// </summary>
        public Guid CommandId { get; set; }

        /// <summary>
        /// Type of command (e.g., "ChangeParameter", "Calibrate", "Start", "Stop", "Reset").
        /// </summary>
        public string CommandType { get; set; }

        /// <summary>
        /// Command parameters as key-value pairs (e.g., { "ParameterName": "Threshold", "NewValue": "0.5" }).
        /// </summary>
        public Dictionary<string, string> Parameters { get; set; }

        /// <summary>
        /// UTC timestamp when command was issued by WebAPI/middleware.
        /// </summary>
        public DateTime IssuedAt { get; set; }

        public EquipmentCommand()
        {
            CommandId = Guid.Empty;
            CommandType = string.Empty;
            Parameters = new Dictionary<string, string>();
            IssuedAt = DateTime.UtcNow;
        }
    }
}
